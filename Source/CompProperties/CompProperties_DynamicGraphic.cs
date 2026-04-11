using JetBrains.Annotations;
using Verse;

// ReSharper disable InconsistentNaming

namespace CWF;

public class CompProperties_DynamicGraphic : CompProperties {
    [UsedImplicitly]
    public readonly List<AttachmentPointData> attachmentPoints = [];

    public CompProperties_DynamicGraphic() => compClass = typeof(CompDynamicGraphic);
}

[UsedImplicitly]
public class AttachmentPointData {
    public PartDef? part;

    public ModuleGraphicData? baseTexture;

    public int layer;

    public bool receivesColor;

    [UsedImplicitly]
    public void ExposeData() {
        Scribe_Defs.Look(ref part, "part");

        Scribe_Values.Look(ref baseTexture, "baseTexture");
        Scribe_Values.Look(ref layer, "layer");
        Scribe_Values.Look(ref receivesColor, "receivesColor");
    }
}