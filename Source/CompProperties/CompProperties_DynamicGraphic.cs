using JetBrains.Annotations;

namespace CWF;

// ReSharper disable once InconsistentNaming
public class CompProperties_DynamicGraphic : CompProperties {
    [UsedImplicitly]
    public readonly List<AttachmentPointData> attachmentPoints = [];

    public CompProperties_DynamicGraphic() => compClass = typeof(CompDynamicGraphic);

    public override IEnumerable<string> ConfigErrors(ThingDef parentDef) {
        foreach (var error in base.ConfigErrors(parentDef)) {
            yield return error;
        }

        if (parentDef.graphicData == null) {
            yield return $"{parentDef.defName} has CompProperties_DynamicGraphic but no graphicData.";
        }

        foreach (var attachmentPoint in attachmentPoints) {
            if (attachmentPoint.part.defName == Def.DefaultDefName) {
                yield return $"{parentDef.defName} has an attachment point without a part.";
            }
        }
    }
}

public class AttachmentPointData {
    [UsedImplicitly]
    public readonly PartDef part = new();

    [UsedImplicitly]
    public readonly ModuleGraphicData? baseTexture;

    [UsedImplicitly]
    public readonly int layer;
}