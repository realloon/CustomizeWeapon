using JetBrains.Annotations;
using RimWorld;
using Verse;

// ReSharper disable InconsistentNaming

namespace CWF;

[UsedImplicitly]
public class TraitModuleExtension : DefModExtension {
    [UsedImplicitly]
    public readonly WeaponTraitDef weaponTraitDef = new();

    [UsedImplicitly]
    public PartDef part = new();

    [UsedImplicitly]
    public List<ConditionalPartModifier>? conditionalPartModifiers;

    [UsedImplicitly]
    public List<ThingDef>? requiredWeaponDefs;

    [UsedImplicitly]
    public List<string>? requiredWeaponTags;

    [UsedImplicitly]
    public List<ThingDef>? excludeWeaponDefs;

    [UsedImplicitly]
    public List<string>? excludeWeaponTags;

    [UsedImplicitly]
    public Rarity rarity = Rarity.Standard;

    public override IEnumerable<string> ConfigErrors() {
        if (weaponTraitDef.defName == Def.DefaultDefName) {
            yield return "Required field 'weaponTraitDef' is missing in XML.";
        }

        if (part.defName == Def.DefaultDefName) {
            yield return "Required field 'part' is missing in XML.";
        }
    }
}