using JetBrains.Annotations;
using RimWorld;
using Verse;

// ReSharper disable InconsistentNaming

namespace CWF;

/// <summary>
/// A DefModExtension that serves as the primary data container for a weapon module item (ThingDef). It links the module item to a specific `weaponTraitDef` and a `part` slot. It also defines compatibility rules such as `requiredWeaponTags` and `excludeWeaponDefs`.
/// </summary>
[UsedImplicitly]
public class TraitModuleExtension : DefModExtension {
    /// <summary>
    /// The actual trait (stat bonuses, abilities, etc.) that this module will grant to a weapon.
    /// </summary>
    [UsedImplicitly]
    public readonly WeaponTraitDef weaponTraitDef = new();

    /// <summary>
    /// The weapon part slot (e.g., Muzzle, Stock) that this module attaches to.
    /// </summary>
    [UsedImplicitly]
    public PartDef part = new();

    /// <summary>
    /// A list of rules that allow this module to enable or disable other part slots on a weapon.
    /// </summary>
    [UsedImplicitly]
    public List<ConditionalPartModifier>? conditionalPartModifiers;

    /// <summary>
    /// A list of specific weapon ThingDefs this module is compatible with.
    /// </summary>
    [UsedImplicitly]
    public List<ThingDef>? requiredWeaponDefs;

    /// <summary>
    /// A list of weaponTags that a weapon must have for this module to be compatible.
    /// </summary>
    [UsedImplicitly]
    public List<string>? requiredWeaponTags;

    /// <summary>
    /// A list of specific weapon ThingDefs this module is incompatible with.
    /// </summary>
    [UsedImplicitly]
    public List<ThingDef>? excludeWeaponDefs;

    /// <summary>
    /// A list of weaponTags that will make a weapon incompatible with this module.
    /// </summary>
    [UsedImplicitly]
    public List<string>? excludeWeaponTags;

    // public Rarity rarity; // enum

    public override IEnumerable<string> ConfigErrors() {
        if (weaponTraitDef.defName == Def.DefaultDefName) {
            yield return "Required field 'weaponTraitDef' is missing in XML.";
        }

        if (part.defName == Def.DefaultDefName) {
            yield return "Required field 'part' is missing in XML.";
        }
    }
}