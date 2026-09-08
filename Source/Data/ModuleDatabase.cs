using CWF.Extensions;

namespace CWF;

public static class ModuleDatabase {
    private static readonly Dictionary<WeaponTraitDef, PartDef> TraitToPart = new();
    private static readonly Dictionary<WeaponTraitDef, ThingDef> TraitToModule = new();
    private static readonly Dictionary<string, List<ThingDef>> WeaponsByTag = new();

    public static IEnumerable<ThingDef> AllModuleDefs => TraitToModule.Values;

    internal static void BuildCacheAndInject() {
        foreach (var thingDef in DefDatabase<ThingDef>.AllDefs) {
            // fill weapon caches
            if (thingDef.IsWeapon && !thingDef.weaponTags.NullOrEmpty() && thingDef.race == null &&
                !thingDef.IsCorpse) {
                foreach (var tag in thingDef.weaponTags) {
                    WeaponsByTag.TryAdd(tag, []);
                    WeaponsByTag[tag].Add(thingDef);
                }
            }

            var ext = thingDef.GetModExtension<TraitModuleExtension>();
            if (ext == null) continue;

            if (ext.weaponTraitDef.defName == Def.DefaultDefName || ext.part.defName == Def.DefaultDefName) {
                Log.Error(
                    $"[CWF] Module '{thingDef.defName}' has an invalid {nameof(TraitModuleExtension)}."); // todo: fixme
                continue;
            }

            // fill trait caches
            if (TraitToPart.ContainsKey(ext.weaponTraitDef)) {
                Log.Warning(
                    $"[CWF] Cache building warning: WeaponTraitDef '{ext.weaponTraitDef.defName}' is defined by multiple TraitModules. " +
                    $"The one in '{thingDef.defName}' will overwrite previous entries. This may cause unpredictable behavior when uninstalling parts.");
            }

            TraitToPart[ext.weaponTraitDef] = ext.part;
            TraitToModule[ext.weaponTraitDef] = thingDef;
        }

        foreach (var (traitDef, moduleDef) in TraitToModule) {
            // inject description
            moduleDef.description = traitDef.description;

            // inject hyperlinks
            foreach (var weaponDef in GetCompatibleWeaponDefsFor(moduleDef)) {
                moduleDef.descriptionHyperlinks ??= [];
                if (moduleDef.descriptionHyperlinks.Any(h => h.def == weaponDef)) continue;

                moduleDef.descriptionHyperlinks.Add(new DefHyperlink(weaponDef));
            }
        }
    }


    internal static bool TryGetPart(WeaponTraitDef traitDef, out PartDef part) {
        return TraitToPart.TryGetValue(traitDef, out part);
    }

    internal static bool TryGetModuleDef(WeaponTraitDef traitDef, out ThingDef? moduleDef) {
        return TraitToModule.TryGetValue(traitDef, out moduleDef);
    }

    #region Helpers

    private static IEnumerable<ThingDef> GetCompatibleWeaponDefsFor(ThingDef moduleDef) {
        var ext = moduleDef.GetModExtension<TraitModuleExtension>();
        var candidates = new HashSet<ThingDef>(ext.requiredWeaponDefs ?? []);

        foreach (var tag in ext.requiredWeaponTags ?? []) {
            if (WeaponsByTag.TryGetValue(tag, out var weapons)) {
                candidates.UnionWith(weapons);
            }
        }

        return candidates.Where(moduleDef.IsCompatibleWith);
    }

    #endregion
}