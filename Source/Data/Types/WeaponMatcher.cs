using JetBrains.Annotations;

// ReSharper disable InconsistentNaming

namespace CWF;

[UsedImplicitly]
public class WeaponMatcher {
    [UsedImplicitly]
    public List<ThingDef> weaponDefs = [];

    [UsedImplicitly]
    public List<string> weaponTags = [];

    public bool IsMatch(ThingDef weaponDef) {
        if (weaponDefs.Contains(weaponDef)) return true;

        return weaponDef.weaponTags != null && weaponTags.Any(weaponDef.weaponTags.Contains);
    }
}