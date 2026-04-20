using UnityEngine;
using RimWorld;
using Verse;
using CWF.Extensions;

namespace CWF;

public class CompDynamicGraphic : ThingComp {
    private CompProperties_DynamicGraphic Props => (CompProperties_DynamicGraphic)props;

    private Graphic? _cachedGraphic;
    private Texture2D? _cachedBakedTexture;

    private bool _isDirty = true;

    private CompDynamicTraits? _compDynamicTraits;

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

    public Texture? GetUIIconTexture() {
        return GetDynamicGraphic().MatSingleFor(parent).mainTexture;
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
        if (!Settings.Current.DynamicTexturesEnabled) return;
        if (parent.def.dontPrint) return;

        GetDynamicGraphic().Print(layer, parent, 0f);
    }

    /// <summary>
    /// Finds a weapon-specific graphic override for a given module trait.
    /// </summary>
    public ModuleGraphicData? GetGraphicDataFor(WeaponTraitDef traitDef) {
        if (!traitDef.TryGetModuleDef(out var moduleDef)) return null;

        if (AdapterDef.TryGetModuleGraphicOverride(parent.def, moduleDef, out var adapterGraphicData)) {
            return adapterGraphicData;
        }

        return null;
    }

    // === Helper ===
    private Graphic GenerateGraphic() {
        var originalGraphicData = parent.def.graphicData;
        if (originalGraphicData == null) return BaseContent.BadGraphic;

        var sizeReference = ContentFinder<Texture2D>.Get(originalGraphicData.texPath, false);
        var fallbackWidth = sizeReference?.width ?? _cachedBakedTexture?.width ?? 512;
        var fallbackHeight = sizeReference?.height ?? _cachedBakedTexture?.height ?? 512;

        var layersToDraw = new List<(Texture2D texture, Vector2 offset, float scale, int sortOrder,
            Color color, Texture2D? maskTexture)>();

        if (_compDynamicTraits != null) {
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
                    layersToDraw.Add((outlineTex, finalOffset, finalScale, baseSortOrder - 999, Color.white, null));
                }

                // module
                if (graphicToRender.texturePath.IsNullOrEmpty()) continue;
                var moduleTexture = ContentFinder<Texture2D>.Get(graphicToRender.texturePath, false);
                if (moduleTexture == null) continue;

                var color = point.receivesColor
                    ? parent.TryGetComp<CompColorable>()?.ColorDef?.color ?? originalGraphicData.color
                    : Color.white;

                var mask = GetMaskTexture(originalGraphicData, point);

                layersToDraw.Add((moduleTexture, finalOffset, finalScale, baseSortOrder, color, mask));
            }
        }

        layersToDraw.Sort((left, right) => left.sortOrder.CompareTo(right.sortOrder));

        var minX = 0f;
        var minY = 0f;
        float maxX = fallbackWidth;
        float maxY = fallbackHeight;

        foreach (var layer in layersToDraw) {
            var scaledWidth = layer.texture.width * layer.scale;
            var scaledHeight = layer.texture.height * layer.scale;

            var x = layer.offset.x;
            var y = (fallbackHeight - scaledHeight) - layer.offset.y;

            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x + scaledWidth > maxX) maxX = x + scaledWidth;
            if (y + scaledHeight > maxY) maxY = y + scaledHeight;
        }

        var renderWidth = Mathf.CeilToInt(maxX - minX);
        var renderHeight = Mathf.CeilToInt(maxY - minY);

        var shiftX = -minX;
        var shiftY = -minY;

        var renderTexture = RenderTexture.GetTemporary(renderWidth, renderHeight, 0, RenderTextureFormat.ARGB32);

        RenderTexture.active = renderTexture;
        GL.Clear(true, true, Color.clear);
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, renderTexture.width, renderTexture.height, 0);

        Material? tempMaterial = null;

        foreach (var layer in layersToDraw) {
            var scaledWidth = layer.texture.width * layer.scale;
            var scaledHeight = layer.texture.height * layer.scale;

            var x = layer.offset.x + shiftX;
            var y = ((fallbackHeight - scaledHeight) - layer.offset.y) + shiftY;

            var destRect = new Rect(x, y, scaledWidth, scaledHeight);

            if (layer.color != Color.white && originalGraphicData.shaderType?.Shader != null) {
                tempMaterial ??= new Material(originalGraphicData.shaderType.Shader);
                tempMaterial.color = layer.color;

                if (tempMaterial.HasProperty(ShaderPropertyIDs.ColorTwo)) {
                    tempMaterial.SetColor(ShaderPropertyIDs.ColorTwo, layer.color);
                }

                if (tempMaterial.HasProperty(ShaderPropertyIDs.MaskTex)) {
                    tempMaterial.SetTexture(ShaderPropertyIDs.MaskTex, layer.maskTexture);
                }

                Graphics.DrawTexture(destRect, layer.texture, tempMaterial);
            } else {
                Graphics.DrawTexture(destRect, layer.texture);
            }
        }

        if (tempMaterial != null) {
            UnityEngine.Object.Destroy(tempMaterial);
        }

        GL.PopMatrix();

        var finalBakedTexture = EnsureBakedTexture(renderTexture.width, renderTexture.height);
        Graphics.CopyTexture(renderTexture, finalBakedTexture);

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(renderTexture);

        var drawSizeScaleX = (float)renderTexture.width / fallbackWidth;
        var drawSizeScaleY = (float)renderTexture.height / fallbackHeight;

        var adjustedDrawSize = new Vector2(
            originalGraphicData.drawSize.x * drawSizeScaleX,
            originalGraphicData.drawSize.y * drawSizeScaleY
        );

        // Keep the original weapon body anchored when the baked canvas expands on only one side.
        var originalCenterDeltaX = shiftX + fallbackWidth * 0.5f - renderWidth * 0.5f;
        var originalCenterDeltaY = shiftY + fallbackHeight * 0.5f - renderHeight * 0.5f;

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

    private static Vector3 GetOffsetForRotation(GraphicData graphicData, Rot4 rotation) {
        return rotation.AsInt switch {
            Rot4.NorthInt => graphicData.drawOffsetNorth ?? graphicData.drawOffset,
            Rot4.EastInt => graphicData.drawOffsetEast ?? graphicData.drawOffset,
            Rot4.SouthInt => graphicData.drawOffsetSouth ?? graphicData.drawOffset,
            Rot4.WestInt => graphicData.drawOffsetWest ?? graphicData.drawOffset,
            _ => graphicData.drawOffset
        };
    }

    private static Vector3 RotateOffsetForRotation(Vector3 offset, Rot4 rotation) {
        return rotation.AsQuat * offset;
    }

    private Texture2D EnsureBakedTexture(int width, int height) {
        if (_cachedBakedTexture != null && _cachedBakedTexture.width == width && _cachedBakedTexture.height == height) {
            return _cachedBakedTexture;
        }

        ReleaseCachedTexture();

        _cachedBakedTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        return _cachedBakedTexture;
    }

    private void ReleaseCachedTexture() {
        if (_cachedBakedTexture == null) return;

        UnityEngine.Object.Destroy(_cachedBakedTexture);
        _cachedBakedTexture = null;
    }

    private static Texture2D? GetMaskTexture(GraphicData graphicData, AttachmentPointData point) {
        var maskPathToLoad = graphicData.maskPath.NullOrEmpty()
            ? graphicData.texPath + "_m"
            : graphicData.maskPath;

        return point.receivesColor
            ? ContentFinder<Texture2D>.Get(maskPathToLoad, false)
            : null;
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
}