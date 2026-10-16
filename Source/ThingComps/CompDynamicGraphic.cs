using UnityEngine;
using CWF.Extensions;

namespace CWF;

public class CompDynamicGraphic : ThingComp {
    private const float BakeCameraHeight = 10f;
    private Graphic? _cachedGraphic;
    private Texture2D? _cachedBakedTexture;
    private RenderTexture? _bakeTarget;
    private bool _isDirty = true;
    private CompDynamicTraits? _compDynamicTraits;

    private CompProperties_DynamicGraphic Props => (CompProperties_DynamicGraphic)props;

    private readonly record struct TextureLayer(Texture2D Texture, Placement Placement, int SortOrder, Color Tint);

    private readonly record struct Placement(Vector2 Offset, float Scale, int Width, int Height);

    private readonly record struct Canvas(int Width, int Height, float ShiftX, float ShiftY) {
        internal float BottomUpShiftY(int fallbackHeight) => Height - fallbackHeight - ShiftY;
    }

    private static Camera? _bakeCamera;
    private static IReadOnlyList<Quad> _quads = [];
    private static readonly Dictionary<(Texture, Color), Material> Materials = [];

    public override void Initialize(CompProperties properties) {
        base.Initialize(properties);
        _compDynamicTraits = parent.TryGetComp<CompDynamicTraits>();
    }

    public Graphic GetDynamicGraphic() {
        if (!_isDirty && _cachedGraphic != null) return _cachedGraphic;

        _cachedGraphic = GenerateGraphic();
        _isDirty = false;

        return _cachedGraphic;
    }

    public Texture? GetUiIconTexture() {
        GetDynamicGraphic();
        return _cachedBakedTexture;
    }

    public void Notify_GraphicDirty() {
        _isDirty = true;
    }

    public override void Notify_ColorChanged() {
        base.Notify_ColorChanged();
        Notify_GraphicDirty();
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap) {
        base.PostDestroy(mode, previousMap);
        ReleaseCachedTexture();
        ReleaseBakeTarget();
    }

    public override bool DontDrawParent() => Settings.Current.DynamicTexturesEnabled;

    public override void DrawAt(Vector3 drawLoc, bool flip = false) {
        if (!Settings.Current.DynamicTexturesEnabled) return;
        if (parent.def.drawerType != DrawerType.RealtimeOnly && parent.Spawned) return;

        var rotation = flip ? parent.Rotation.Opposite : parent.Rotation;
        GetDynamicGraphic().Draw(drawLoc, rotation, parent);
    }

    public override void PostPrintOnto(SectionLayer layer) {
        if (!Settings.Current.DynamicTexturesEnabled || parent.def.dontPrint) return;

        GetDynamicGraphic().Print(layer, parent, 0f);
    }

    /// <summary>
    /// Finds a weapon-specific graphic override for a given module trait.
    /// </summary>
    public ModuleGraphicData? GetGraphicDataFor(WeaponTraitDef traitDef) {
        if (!traitDef.TryGetModuleDef(out var moduleDef)) return null;

        return AdapterDef.TryGetModuleGraphicOverride(parent.def, moduleDef, out var adapterGraphicData)
            ? adapterGraphicData
            : null;
    }

    // === Helper ===
    private Graphic GenerateGraphic() {
        var originalGraphicData = parent.def.graphicData;
        if (originalGraphicData == null) {
            return BaseContent.BadGraphic;
        }

        var sizeReference = ContentFinder<Texture2D>.Get(originalGraphicData.texPath, false);
        var fallbackWidth = sizeReference?.width ?? _cachedBakedTexture?.width ?? 512;
        var fallbackHeight = sizeReference?.height ?? _cachedBakedTexture?.height ?? 512;

        var layersToDraw = CollectLayers(originalGraphicData);
        layersToDraw.Sort((left, right) => left.SortOrder.CompareTo(right.SortOrder));

        var canvas = MeasureCanvas(layersToDraw, fallbackWidth, fallbackHeight);

        var bakeTarget = EnsureBakeTarget(canvas.Width, canvas.Height);
        Bake(layersToDraw, canvas, fallbackHeight, bakeTarget);

        var finalBakedTexture = EnsureBakedTexture(canvas.Width, canvas.Height);
        ReadBackInto(bakeTarget, finalBakedTexture);

        var drawSizeScaleX = (float)canvas.Width / fallbackWidth;
        var drawSizeScaleY = (float)canvas.Height / fallbackHeight;

        var adjustedDrawSize = new Vector2(
            originalGraphicData.drawSize.x * drawSizeScaleX,
            originalGraphicData.drawSize.y * drawSizeScaleY
        );

        // Keep the original weapon body anchored when the baked canvas expands on only one side.
        var originalCenterDeltaX = canvas.ShiftX + fallbackWidth * 0.5f - canvas.Width * 0.5f;
        var originalCenterDeltaY = canvas.ShiftY + fallbackHeight * 0.5f - canvas.Height * 0.5f;

        var drawOffsetCompensation = new Vector3(
            -originalCenterDeltaX / fallbackWidth * originalGraphicData.drawSize.x,
            0f,
            originalCenterDeltaY / fallbackHeight * originalGraphicData.drawSize.y
        );

        var adjustedGraphicData = CreateAdjustedGraphicData(originalGraphicData, drawOffsetCompensation);

        var graphicSingle = new Graphic_Single();
        var request = new GraphicRequest(
            typeof(Graphic_Single), finalBakedTexture, ShaderDatabase.Cutout,
            adjustedDrawSize, Color.white, Color.white,
            adjustedGraphicData, 0, null, null
        );

        graphicSingle.Init(request);
        _cachedGraphic = WrapGraphicIfNeeded(graphicSingle, adjustedGraphicData);

        return _cachedGraphic;
    }

    private List<TextureLayer> CollectLayers(GraphicData originalGraphicData) {
        var layers = new List<TextureLayer>();
        if (_compDynamicTraits == null) return layers;

        var canTint = originalGraphicData.shaderType?.Shader != null;

        foreach (var point in Props.attachmentPoints) {
            if (point.part == null) continue;

            var installedTrait = _compDynamicTraits.GetInstalledTraitFor(point.part);
            ModuleGraphicData? graphicToRender = null;

            if (installedTrait != null) {
                graphicToRender = GetGraphicDataFor(installedTrait);
            }

            graphicToRender ??= point.baseTexture;

            if (graphicToRender == null) continue;

            var finalOffset = graphicToRender.offset ?? point.baseTexture?.offset ?? Vector2.zero;
            var finalScale = graphicToRender.scale ?? point.baseTexture?.scale ?? 1f;
            var baseSortOrder = point.layer * 10;

            // outline
            var outlineTex = GetOutlineTexture(graphicToRender);
            if (outlineTex != null) {
                layers.Add(new TextureLayer(outlineTex,
                    new Placement(finalOffset, finalScale, outlineTex.width, outlineTex.height), baseSortOrder - 999,
                    Color.white));
            }

            // module
            if (graphicToRender.texturePath.NullOrEmpty()) continue;
            var moduleTexture = ContentFinder<Texture2D>.Get(graphicToRender.texturePath, false);
            if (moduleTexture == null) continue;

            var tint = point.receivesColor && canTint
                ? parent.TryGetComp<CompColorable>()?.ColorDef?.color ?? originalGraphicData.color
                : Color.white;

            layers.Add(new TextureLayer(moduleTexture,
                new Placement(finalOffset, finalScale, moduleTexture.width, moduleTexture.height), baseSortOrder,
                tint));
        }

        return layers;
    }

    private static Canvas MeasureCanvas(List<TextureLayer> layers, int fallbackWidth, int fallbackHeight) {
        var minX = 0f;
        var minY = 0f;
        float maxX = fallbackWidth;
        float maxY = fallbackHeight;

        foreach (var layer in layers) {
            var scaledWidth = layer.Placement.Width * layer.Placement.Scale;
            var scaledHeight = layer.Placement.Height * layer.Placement.Scale;
            var x = layer.Placement.Offset.x;
            var y = fallbackHeight - scaledHeight - layer.Placement.Offset.y;

            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x + scaledWidth > maxX) maxX = x + scaledWidth;
            if (y + scaledHeight > maxY) maxY = y + scaledHeight;
        }

        return new Canvas(Mathf.CeilToInt(maxX - minX), Mathf.CeilToInt(maxY - minY), -minX, -minY);
    }

    private static (int X, int Y, int Width, int Height) PlaceLayer(TextureLayer layer, Canvas canvas,
        float bottomUpShiftY) => (
        Mathf.RoundToInt(layer.Placement.Offset.x + canvas.ShiftX),
        Mathf.RoundToInt(layer.Placement.Offset.y + bottomUpShiftY),
        Mathf.Max(1, Mathf.RoundToInt(layer.Placement.Width * layer.Placement.Scale)),
        Mathf.Max(1, Mathf.RoundToInt(layer.Placement.Height * layer.Placement.Scale)));

    private static Graphic WrapGraphicIfNeeded(Graphic graphic, GraphicData originalGraphicData) {
        if (originalGraphicData.onGroundRandomRotateAngle > 0.01f) {
            graphic = new Graphic_RandomRotated(graphic, originalGraphicData.onGroundRandomRotateAngle);
        }

        return graphic;
    }

    private static GraphicData CreateAdjustedGraphicData(GraphicData originalGraphicData,
        Vector3 drawOffsetCompensation) {
        var adjustedGraphicData = new GraphicData();
        adjustedGraphicData.CopyFrom(originalGraphicData);

        adjustedGraphicData.drawOffset = GetOffsetForRotation(originalGraphicData, Rot4.North)
                                         + RotateOffsetForRotation(drawOffsetCompensation, Rot4.North);
        adjustedGraphicData.drawOffsetNorth = GetOffsetForRotation(originalGraphicData, Rot4.North)
                                              + RotateOffsetForRotation(drawOffsetCompensation, Rot4.North);
        adjustedGraphicData.drawOffsetEast = GetOffsetForRotation(originalGraphicData, Rot4.East)
                                             + RotateOffsetForRotation(drawOffsetCompensation, Rot4.East);
        adjustedGraphicData.drawOffsetSouth = GetOffsetForRotation(originalGraphicData, Rot4.South)
                                              + RotateOffsetForRotation(drawOffsetCompensation, Rot4.South);
        adjustedGraphicData.drawOffsetWest = GetOffsetForRotation(originalGraphicData, Rot4.West)
                                             + RotateOffsetForRotation(drawOffsetCompensation, Rot4.West);

        return adjustedGraphicData;
    }

    private static Vector3 GetOffsetForRotation(GraphicData graphicData, Rot4 rotation) => rotation.AsInt switch {
        Rot4.NorthInt => graphicData.drawOffsetNorth ?? graphicData.drawOffset,
        Rot4.EastInt => graphicData.drawOffsetEast ?? graphicData.drawOffset,
        Rot4.SouthInt => graphicData.drawOffsetSouth ?? graphicData.drawOffset,
        Rot4.WestInt => graphicData.drawOffsetWest ?? graphicData.drawOffset,
        _ => graphicData.drawOffset
    };

    private static Vector3 RotateOffsetForRotation(Vector3 offset, Rot4 rotation) => rotation.AsQuat * offset;

    private RenderTexture EnsureBakeTarget(int width, int height) {
        if (_bakeTarget != null && _bakeTarget.width == width && _bakeTarget.height == height) {
            return _bakeTarget;
        }

        ReleaseBakeTarget();

        _bakeTarget = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        _bakeTarget.Create();
        return _bakeTarget;
    }

    private void ReleaseBakeTarget() {
        if (_bakeTarget == null) return;

        _bakeTarget.Release();
        _bakeTarget = null;
    }

    private Texture2D EnsureBakedTexture(int width, int height) {
        if (_cachedBakedTexture != null && _cachedBakedTexture.width == width && _cachedBakedTexture.height == height) {
            return _cachedBakedTexture;
        }

        ReleaseCachedTexture();

        _cachedBakedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        return _cachedBakedTexture;
    }

    private void ReleaseCachedTexture() {
        if (_cachedBakedTexture == null) return;

        UnityEngine.Object.Destroy(_cachedBakedTexture);
        _cachedBakedTexture = null;
    }

    private static void ReadBackInto(RenderTexture source, Texture2D destination) {
        var previous = RenderTexture.active;
        RenderTexture.active = source;

        destination.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
        destination.Apply(false);

        RenderTexture.active = previous;
    }

    public static Texture2D? GetOutlineTexture(ModuleGraphicData graphicData) {
        string? outlinePathToLoad = null;

        if (!graphicData.outlinePath.NullOrEmpty()) {
            outlinePathToLoad = graphicData.outlinePath;
        } else if (!graphicData.texturePath.NullOrEmpty()) {
            outlinePathToLoad = graphicData.texturePath + "_o";
        }

        return outlinePathToLoad != null ? ContentFinder<Texture2D>.Get(outlinePathToLoad, false) : null;
    }

    private static void Bake(IReadOnlyList<TextureLayer> layers, Canvas canvas, int fallbackHeight,
        RenderTexture target) {
        var camera = EnsureBakeCamera();
        var bottomUpShiftY = canvas.BottomUpShiftY(fallbackHeight);

        var quads = new List<Quad>(layers.Count);
        foreach (var layer in layers) {
            var (x, y, width, height) = PlaceLayer(layer, canvas, bottomUpShiftY);
            quads.Add(new Quad(layer.Texture, layer.Tint, x, y, width, height));
        }

        // One world unit per canvas pixel, centred on the canvas, looking straight down.
        camera.transform.position = new Vector3(canvas.Width * 0.5f, BakeCameraHeight, canvas.Height * 0.5f);
        camera.orthographicSize = canvas.Height * 0.5f;
        camera.targetTexture = target;

        _quads = quads;
        camera.Render();
        _quads = [];
        camera.targetTexture = null;
    }

    private static Camera EnsureBakeCamera() {
        if (_bakeCamera != null) {
            return _bakeCamera;
        }

        var gameObject = new GameObject("CWF_BakeCamera", typeof(Camera));
        gameObject.SetActive(false);

        var camera = gameObject.GetComponent<Camera>();
        camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        camera.orthographic = true;
        camera.cullingMask = 0;
        camera.clearFlags = CameraClearFlags.Color;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.useOcclusionCulling = false;
        camera.renderingPath = RenderingPath.Forward;
        camera.nearClipPlane = BakeCameraHeight - 5f;
        camera.farClipPlane = BakeCameraHeight + 5f;

        Camera.onPostRender += DrawQuads;

        _bakeCamera = camera;
        return camera;
    }

    private static void DrawQuads(Camera camera) {
        if (!ReferenceEquals(camera, _bakeCamera)) return;

        foreach (var quad in _quads) {
            var center = new Vector3(quad.X + quad.Width * 0.5f, 0f, quad.Y + quad.Height * 0.5f);
            var matrix = Matrix4x4.TRS(center, Quaternion.identity, new Vector3(quad.Width, 1f, quad.Height));

            MaterialFor(quad.Texture, quad.Tint).SetPass(0);
            Graphics.DrawMeshNow(MeshPool.plane10, matrix);
        }
    }

    private static Material MaterialFor(Texture texture, Color tint) {
        if (Materials.TryGetValue((texture, tint), out var cached)) return cached;

        var material = new Material(ShaderDatabase.CutoutComplexBlend) {
            mainTexture = texture,
            color = tint
        };
        material.SetColor(ShaderPropertyIDs.ColorTwo, tint);
        material.SetFloat("_ZTestMode", 8f);
        material.SetFloat("_ZWriteMode", 0f);

        Materials[(texture, tint)] = material;
        return material;
    }

    private readonly record struct Quad(Texture2D Texture, Color Tint, int X, int Y, int Width, int Height);
}