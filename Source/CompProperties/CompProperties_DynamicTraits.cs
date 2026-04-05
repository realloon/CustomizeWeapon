using JetBrains.Annotations;
using RimWorld;
using Verse;

// ReSharper disable InconsistentNaming

namespace CWF;

[UsedImplicitly]
public class CompProperties_DynamicTraits : CompProperties {
    public readonly List<PartDef> supportParts = [];

    public readonly List<WeaponTraitDef> defaultWeaponTraitDefs = [];

    public CompProperties_DynamicTraits() => compClass = typeof(CompDynamicTraits);
}
