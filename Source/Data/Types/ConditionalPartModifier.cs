using JetBrains.Annotations;

// ReSharper disable InconsistentNaming

namespace CWF;

[UsedImplicitly]
public class ConditionalPartModifier {
    [UsedImplicitly]
    public readonly WeaponMatcher matcher = new();

    [UsedImplicitly]
    public readonly List<PartDef> enablesParts = [];

    [UsedImplicitly]
    public readonly List<PartDef> disablesParts = [];
}