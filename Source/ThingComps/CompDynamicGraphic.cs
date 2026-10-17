using UnityEngine;
using CWF.Extensions;

namespace CWF;

public class CompDynamicGraphic : ThingComp {
    private const float BakeCameraHeight = 10f;
    private const byte DyeMinChannel = 200;
    private const int OutlineWidth = 8;

    private static Camera? _bakeCamera;
    private static IReadOnlyList<Quad> _quads = [];
    private static readonly Dictionary<(Texture2D, Color), Material> Materials = [];
    private static readonly Dictionary<Texture2D, Texture2D?> DyeMasks = [];
    private static readonly Dictionary<Texture2D, Texture2D?> ModuleOutlines = [];

    private Graphic? _cachedGraphic;
    private Texture2D? _cachedBakedTexture;
    private bool _isDirty = true;
    private static RenderTexture? _bakeTarget;
    private CompDynamicTraits? _compDynamicTraits;

    private readonly record struct Quad(Texture2D Texture, Color Tint, int X, int Y, int Width, int Height);

    private readonly record struct TextureLayer(Texture2D Texture, Placement Placement, int SortOrder, Color Tint);

    private readonly record struct Placement(Vector2 Offset, float Scale, int Width, int Height);

    private readonly record struct Canvas(int Width, int Height, float ShiftX, float ShiftY) {
        internal float BottomUpShiftY(int fallbackHeight) => Height - fallbackHeight - ShiftY;
    }

    private CompProperties_DynamicGraphic Props => (CompProperties_DynamicGraphic)props;

    public override void Initialize(CompProperties properties) {
        base.Initialize(properties);
        _compDynamicTraits = parent.TryGetComp<CompDynamicTraits>();
    }

    public Graphic GetDynamicGraphic() {
        if (!_isDirty && _cachedGraphic != null) {
            return _cachedGraphic;
        }

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
        if (!traitDef.TryGetModuleDef(out var moduleDef)) {
            return null;
        }

        return AdapterDef.TryGetModuleGraphicOverride(parent.def, moduleDef, out var adapterGraphicData)
            ? adapterGraphicData
            : null;
    }

    public static Texture2D? GetModuleOutline(Texture2D source) {
        if (ModuleOutlines.TryGetValue(source, out var cached)) {
            return cached;
        }

        var outline = BuildModuleOutline(source);
        ModuleOutlines[source] = outline;
        return outline;
    }

    #region MyRegion

    private Graphic GenerateGraphic() {
        var originalGraphicData = parent.def.graphicData;
        if (originalGraphicData == null) {
            return BaseContent.BadGraphic;
        }

        var sizeReference = ContentFinder<Texture2D>.Get(originalGraphicData.texPath, false);
        var fallbackWidth = sizeReference?.width ?? _cachedBakedTexture?.width ?? 512;
        var fallbackHeight = sizeReference?.height ?? _cachedBakedTexture?.height ?? 512;
        var layersToDraw = CollectLayers();
        layersToDraw.Sort((left, right) => left.SortOrder.CompareTo(right.SortOrder));

        var canvas = MeasureCanvas(layersToDraw, fallbackWidth, fallbackHeight);

        var bakeTarget = EnsureBakeTarget(canvas.Width, canvas.Height);
        Bake(layersToDraw, canvas, fallbackHeight, bakeTarget);

        var finalBakedTexture = EnsureBakedTexture(canvas.Width, canvas.Height);
        ReadBackInto(bakeTarget, finalBakedTexture);
        ApplyCompositeOutline(finalBakedTexture);

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

        _cachedGraphic = adjustedGraphicData.onGroundRandomRotateAngle > 0.01f
            ? new Graphic_RandomRotated(graphicSingle, adjustedGraphicData.onGroundRandomRotateAngle)
            : graphicSingle;

        return _cachedGraphic;
    }

    private List<TextureLayer> CollectLayers() {
        var layers = new List<TextureLayer>();
        if (_compDynamicTraits == null) {
            return layers;
        }

        var dyeColor = parent.TryGetComp<CompColorable>()?.ColorDef?.color ?? Color.white;

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

            // module
            if (graphicToRender.texturePath.NullOrEmpty()) continue;
            var moduleTexture = ContentFinder<Texture2D>.Get(graphicToRender.texturePath, false);
            if (moduleTexture == null) continue;

            var disableDyeing = installedTrait != null &&
                                installedTrait.TryGetModuleDef(out var moduleDef) &&
                                moduleDef.GetModExtension<TraitModuleExtension>() is { disableDyeing: true };
            var layerTint = disableDyeing ? Color.white : dyeColor;
            if (!layerTint.IndistinguishableFrom(Color.white)) EnsureDyeMask(moduleTexture);

            layers.Add(new TextureLayer(moduleTexture,
                new Placement(finalOffset, finalScale, moduleTexture.width, moduleTexture.height), baseSortOrder,
                layerTint));
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
        Mathf.Max(1, Mathf.RoundToInt(layer.Placement.Height * layer.Placement.Scale))
    );

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

    private static RenderTexture EnsureBakeTarget(int width, int height) {
        if (_bakeTarget != null && _bakeTarget.width == width && _bakeTarget.height == height) {
            return _bakeTarget;
        }

        ReleaseBakeTarget();

        _bakeTarget = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        _bakeTarget.Create();
        return _bakeTarget;
    }

    private static void ReleaseBakeTarget() {
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

    private static void EnsureDyeMask(Texture2D source) {
        if (DyeMasks.ContainsKey(source)) return;

        DyeMasks[source] = BuildDyeMask(source);
    }

    private static Texture2D? BuildDyeMask(Texture2D source) {
        Texture2D? readableCopy = null;
        Color32[] pixels;

        // Mod textures are loaded with makeNoLongerReadable, so grab a temporary CPU
        // copy through the game's own blit helper and discard it after building the mask.
        if (source.isReadable) {
            pixels = source.GetPixels32();
        } else {
            readableCopy = TextureAtlasHelper.MakeReadableTextureInstance(source);
            pixels = readableCopy.GetPixels32();
        }

        var maskPixels = new Color32[pixels.Length];
        var dyeable = new bool[pixels.Length];
        var hasDyePixels = false;

        for (var i = 0; i < pixels.Length; i++) {
            var pixel = pixels[i];
            var isDyeable = pixel is { a: > 0, r: >= DyeMinChannel, g: >= DyeMinChannel, b: >= DyeMinChannel };

            // Keep the source RGB on every pixel so bilinear filtering of the mask
            // fades smoothly at the boundary; the alpha channel is the actual mask.
            maskPixels[i] = new Color32(pixel.r, pixel.g, pixel.b, 0);
            if (!isDyeable) continue;

            dyeable[i] = true;
            hasDyePixels = true;
        }

        if (!hasDyePixels) {
            if (readableCopy != null) UnityEngine.Object.Destroy(readableCopy);
            return null;
        }

        // Expand the mask by one pixel. Hard dark/white borders (and texture
        // compression) otherwise leave a one-pixel un-dyed line at the edge.
        var width = source.width;
        var height = source.height;
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                if (!dyeable[y * width + x]) continue;

                for (var dy = -1; dy <= 1; dy++) {
                    var ny = y + dy;
                    if (ny < 0 || ny >= height) continue;

                    for (var dx = -1; dx <= 1; dx++) {
                        var nx = x + dx;
                        if (nx < 0 || nx >= width) continue;

                        maskPixels[ny * width + nx].a = 255;
                    }
                }
            }
        }

        if (readableCopy != null) {
            UnityEngine.Object.Destroy(readableCopy);
        }

        return CreateTexture(source, maskPixels, "_dyeMask");
    }

    private static void ApplyCompositeOutline(Texture2D texture) {
        var width = texture.width;
        var height = texture.height;
        var pixels = texture.GetPixels32();
        var opaque = new bool[pixels.Length];

        for (var i = 0; i < pixels.Length; i++) {
            opaque[i] = pixels[i].a > 0;
        }

        var squaredDistance = SquaredDistanceTransform(opaque, width, height);

        for (var i = 0; i < pixels.Length; i++) {
            var a = pixels[i].a / 255f;
            var distance = Mathf.Sqrt(squaredDistance[i]);
            var outlineAlpha = Mathf.Clamp01(OutlineWidth + 0.5f - distance);
            var finalAlpha = a + outlineAlpha * (1f - a);

            if (finalAlpha <= 0f) {
                pixels[i] = default;
                continue;
            }

            var scale = a / finalAlpha;
            pixels[i] = new Color32(
                (byte)Mathf.Clamp(pixels[i].r * scale, 0f, 255f),
                (byte)Mathf.Clamp(pixels[i].g * scale, 0f, 255f),
                (byte)Mathf.Clamp(pixels[i].b * scale, 0f, 255f),
                (byte)Mathf.Clamp(finalAlpha * 255f, 0f, 255f)
            );
        }

        texture.SetPixels32(pixels);
        texture.Apply(false);
    }

    private static Texture2D? BuildModuleOutline(Texture2D source) {
        Texture2D? readableCopy = null;
        Color32[] pixels;

        if (source.isReadable) {
            pixels = source.GetPixels32();
        } else {
            readableCopy = TextureAtlasHelper.MakeReadableTextureInstance(source);
            pixels = readableCopy.GetPixels32();
        }

        var opaque = new bool[pixels.Length];
        var hasOpaque = false;
        for (var i = 0; i < pixels.Length; i++) {
            opaque[i] = pixels[i].a > 0;
            if (opaque[i]) hasOpaque = true;
        }

        if (!hasOpaque) {
            if (readableCopy != null) {
                UnityEngine.Object.Destroy(readableCopy);
            }

            return null;
        }

        var squaredDistance = SquaredDistanceTransform(opaque, source.width, source.height);
        var outlinePixels = new Color32[pixels.Length];
        for (var i = 0; i < pixels.Length; i++) {
            var distance = Mathf.Sqrt(squaredDistance[i]);
            var outlineAlpha = Mathf.Clamp01(OutlineWidth + 0.5f - distance);
            outlinePixels[i] = new Color32(0, 0, 0, (byte)Mathf.Clamp(outlineAlpha * 255f, 0f, 255f));
        }

        if (readableCopy != null) {
            UnityEngine.Object.Destroy(readableCopy);
        }

        return CreateTexture(source, outlinePixels, "_slotOutline");
    }

    private static float[] SquaredDistanceTransform(bool[] mask, int width, int height) {
        var f = new float[width * height];
        var d = new float[width * height];
        var v = new int[Mathf.Max(width, height)];
        var z = new float[Mathf.Max(width, height) + 1];

        for (var i = 0; i < f.Length; i++) {
            f[i] = mask[i] ? 0f : 1e20f;
        }

        for (var y = 0; y < height; y++) {
            DistanceTransform1D(f, y * width, 1, width, d, v, z);
        }

        for (var x = 0; x < width; x++) {
            DistanceTransform1D(d, x, width, height, f, v, z);
        }

        return f;
    }

    private static void DistanceTransform1D(float[] f, int start, int stride, int count, float[] d, int[] v,
        float[] z) {
        var k = 0;
        v[0] = 0;
        z[0] = float.NegativeInfinity;
        z[1] = float.PositiveInfinity;

        for (var q = 1; q < count; q++) {
            var s = (f[start + q * stride] + q * q - (f[start + v[k] * stride] + v[k] * v[k])) /
                    (2f * q - 2f * v[k]);

            while (s <= z[k]) {
                k -= 1;
                s = (f[start + q * stride] + q * q - (f[start + v[k] * stride] + v[k] * v[k])) / (2f * q - 2f * v[k]);
            }

            k += 1;
            v[k] = q;
            z[k] = s;
            z[k + 1] = float.PositiveInfinity;
        }

        k = 0;
        for (var q = 0; q < count; q++) {
            while (z[k + 1] < q) {
                k += 1;
            }

            var dq = q - v[k];
            d[start + q * stride] = dq * dq + f[start + v[k] * stride];
        }
    }

    private static Texture2D CreateTexture(Texture2D source, Color32[] pixels, string suffix) {
        var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false) {
            name = source.name + suffix,
            filterMode = source.filterMode,
            wrapMode = source.wrapMode
        };
        texture.SetPixels32(pixels);
        texture.Apply(false);
        return texture;
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

    private static Material MaterialFor(Texture2D texture, Color tint) {
        if (Materials.TryGetValue((texture, tint), out var cached)) {
            return cached;
        }

        var material = new Material(ShaderDatabase.CutoutComplexBlend) {
            mainTexture = texture,
            color = Color.white
        };
        material.SetColor(ShaderPropertyIDs.ColorTwo, tint);

        if (!tint.IndistinguishableFrom(Color.white)
            && DyeMasks.TryGetValue(texture, out var mask)
            && mask != null) {
            material.SetTexture(ShaderPropertyIDs.MaskTex, mask);
        }

        material.SetFloat("_ZTestMode", 8f);
        material.SetFloat("_ZWriteMode", 0f);

        Materials[(texture, tint)] = material;
        return material;
    }

    #endregion Helper
}