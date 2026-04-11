using JetBrains.Annotations;
using Verse;

// ReSharper disable InconsistentNaming

namespace CWF;

public class PartDef : Def {
    [UsedImplicitly]
    public readonly PartGroup group;

    [UsedImplicitly]
    public readonly int order;

    [NoTranslate]
    [UsedImplicitly]
    public string? fallbackTexturePath;

    public override IEnumerable<string> ConfigErrors() {
        foreach (var item in base.ConfigErrors()) {
            yield return item;
        }

        if (group == PartGroup.None) {
            yield return "group not set";
        }
    }
}

public enum PartGroup {
    None = 0,
    Top,
    Bottom,
    Left,
    Right
}