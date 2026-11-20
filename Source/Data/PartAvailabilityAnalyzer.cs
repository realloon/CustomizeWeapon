using CWF.Extensions;

namespace CWF;

public static class PartAvailabilityAnalyzer {
    public static PartAvailabilityResult Analyze(Thing weapon,
        IReadOnlyDictionary<PartDef, WeaponTraitDef> desiredTraits) {
        if (weapon.TryGetComp<CompDynamicTraits>()?.props is not CompProperties_DynamicTraits props) {
            return new PartAvailabilityResult(
                new HashSet<PartDef>(),
                new Dictionary<PartDef, WeaponTraitDef>(),
                0);
        }

        var supportedParts = new HashSet<PartDef>(props.supportParts);
        var candidateTraits = new Dictionary<PartDef, WeaponTraitDef>();
        var skippedCount = 0;
        var moduleCache = new Dictionary<PartDef, ThingDef>();

        foreach (var (part, trait) in desiredTraits) {
            if (!trait.TryGetPart(out var expectedPart)
                || expectedPart != part
                || !trait.TryGetModuleDef(out var moduleDef)
                || !moduleDef.IsCompatibleWith(weapon.def)) {
                skippedCount += 1;
                continue;
            }

            candidateTraits[part] = trait;
            moduleCache[part] = moduleDef;
        }

        var availableParts = new HashSet<PartDef>(supportedParts);

        while (true) {
            var nextAvailableParts = new HashSet<PartDef>(supportedParts);
            var disabledParts = new HashSet<PartDef>();

            foreach (var part in candidateTraits.Keys) {
                if (!availableParts.Contains(part)) continue;

                foreach (var rule in moduleCache[part].TraitModule().conditionalPartModifiers) {
                    if (!rule.matcher.IsMatch(weapon.def)) continue;

                    nextAvailableParts.UnionWith(rule.enablesParts);
                    disabledParts.UnionWith(rule.disablesParts);
                }
            }

            nextAvailableParts.ExceptWith(disabledParts);

            var invalidParts = candidateTraits.Keys
                .Where(part => !nextAvailableParts.Contains(part))
                .ToList();

            if (invalidParts.Count == 0 && nextAvailableParts.SetEquals(availableParts)) {
                return new PartAvailabilityResult(nextAvailableParts, candidateTraits, skippedCount);
            }

            foreach (var invalidPart in invalidParts) {
                candidateTraits.Remove(invalidPart);
                moduleCache.Remove(invalidPart);
                skippedCount += 1;
            }

            availableParts = nextAvailableParts;
        }
    }
}

public class PartAvailabilityResult(
    IReadOnlyCollection<PartDef> availableParts,
    IReadOnlyDictionary<PartDef, WeaponTraitDef> activeTraits,
    int skippedCount) {
    public IReadOnlyCollection<PartDef> AvailableParts { get; } = availableParts;
    public IReadOnlyDictionary<PartDef, WeaponTraitDef> ActiveTraits { get; } = activeTraits;
    public int SkippedCount { get; } = skippedCount;
}