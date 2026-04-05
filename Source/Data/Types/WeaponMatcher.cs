using JetBrains.Annotations;
using Verse;

// ReSharper disable InconsistentNaming

namespace CWF;

/// <summary>
/// A reusable data class that defines a set of criteria for matching a weapon. It checks if a given weapon's ThingDef is present in `weaponDefs` or if it has any of the specified `weaponTags`. Used in `ConditionalPartModifier` to apply rules selectively.
/// </summary>
[UsedImplicitly]
public class WeaponMatcher {
    /// <summary>
    /// A list of specific weapon ThingDefs to match against.
    /// </summary>
    [UsedImplicitly]
    public List<ThingDef> weaponDefs = [];

    /// <summary>
    /// A list of weaponTags to match against.
    /// </summary>
    [UsedImplicitly]
    public List<string> weaponTags = [];

    public bool IsMatch(ThingDef weaponDef) {
        if (weaponDefs.Contains(weaponDef)) return true;

        if (weaponTags.Empty() || weaponDef.weaponTags.NullOrEmpty()) return false;

        return weaponTags.Any(tag => weaponDef.weaponTags.Contains(tag));
    }
}