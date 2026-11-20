using System.Diagnostics.CodeAnalysis;
using CWF.Extensions;

namespace CWF;

public static class ModuleDatabase {
    private static readonly Dictionary<WeaponTraitDef, PartDef> TraitToPart = new();
    private static readonly Dictionary<WeaponTraitDef, ThingDef> TraitToModule = new();

    public static IEnumerable<ThingDef> AllModuleDefs => TraitToModule.Values;

    internal static void BuildCacheAndInject() {
        var weaponsByTag = new Dictionary<string, List<ThingDef>>();

        foreach (var thingDef in DefDatabase<ThingDef>.AllDefs) {
            // fill weapon caches
            if (thingDef.IsWeapon && !thingDef.weaponTags.NullOrEmpty() && thingDef.race == null &&
                !thingDef.IsCorpse) {
                foreach (var tag in thingDef.weaponTags) {
                    weaponsByTag.TryAdd(tag, []);
                    weaponsByTag[tag].Add(thingDef);
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
            foreach (var weaponDef in GetCompatibleWeaponDefsFor(moduleDef, weaponsByTag)) {
                moduleDef.descriptionHyperlinks ??= [];
                if (moduleDef.descriptionHyperlinks.Any(h => h.def == weaponDef)) continue;

                moduleDef.descriptionHyperlinks.Add(new DefHyperlink(weaponDef));
            }
        }
    }

    extension(WeaponTraitDef traitDef) {
        internal bool TryGetPart([NotNullWhen(true)] out PartDef? part) =>
            TraitToPart.TryGetValue(traitDef, out part);

        internal bool TryGetModuleDef([NotNullWhen(true)] out ThingDef? moduleDef) =>
            TraitToModule.TryGetValue(traitDef, out moduleDef);
    }

    private static IEnumerable<ThingDef> GetCompatibleWeaponDefsFor(ThingDef moduleDef,
        Dictionary<string, List<ThingDef>> weaponsByTag) {
        var ext = moduleDef.TraitModule();
        var candidates = new HashSet<ThingDef>(ext.requiredWeaponDefs);

        foreach (var tag in ext.requiredWeaponTags) {
            if (weaponsByTag.TryGetValue(tag, out var weapons)) {
                candidates.UnionWith(weapons);
            }
        }

        return candidates.Where(moduleDef.IsCompatibleWith);
    }
}