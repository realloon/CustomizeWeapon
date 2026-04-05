using System.Diagnostics.CodeAnalysis;
using System.Text;
using UnityEngine;
using RimWorld;
using Verse;
using CWF.Extensions;

namespace CWF;

public static class ModuleDatabase {
    private static readonly Dictionary<WeaponTraitDef, PartDef> TraitToPart = new();
    private static readonly Dictionary<WeaponTraitDef, ThingDef> TraitToModule = new();
    private static readonly Dictionary<string, List<ThingDef>> WeaponsByTag = new();

    public static IEnumerable<ThingDef> AllModuleDefs => TraitToModule.Values;

    internal static string GetTraitEffect(this WeaponTraitDef traitDef) {
        var sb = new StringBuilder();

        // offset
        if (!traitDef.statOffsets.IsNullOrEmpty()) {
            foreach (var modifier in traitDef.statOffsets) {
                if (modifier.stat == StatDefOf.MarketValue || modifier.stat == StatDefOf.Mass) continue;

                sb.AppendLine($" - {modifier.stat.LabelCap}: " +
                              modifier.stat.Worker.ValueToString(modifier.value, false, ToStringNumberSense.Offset));
            }
        }

        // factor
        if (!traitDef.statFactors.IsNullOrEmpty()) {
            foreach (var modifier in traitDef.statFactors) {
                sb.AppendLine($" - {modifier.stat.LabelCap}: " +
                              modifier.stat.Worker.ValueToString(modifier.value, false, ToStringNumberSense.Factor));
            }
        }

        if (!Mathf.Approximately(traitDef.burstShotCountMultiplier, 1f)) {
            sb.AppendLine($" - {"CWF_BurstShotCountMultiplier".Translate()}: " +
                          traitDef.burstShotCountMultiplier.ToStringByStyle(ToStringStyle.PercentZero,
                              ToStringNumberSense.Factor));
        }

        if (!Mathf.Approximately(traitDef.burstShotSpeedMultiplier, 1f)) {
            sb.AppendLine($" - {"CWF_BurstShotSpeedMultiplier".Translate()}: " +
                          traitDef.burstShotSpeedMultiplier.ToStringByStyle(ToStringStyle.PercentZero,
                              ToStringNumberSense.Factor));
        }

        if (!Mathf.Approximately(traitDef.additionalStoppingPower, 0.0f)) {
            sb.AppendLine($" - {"CWF_AdditionalStoppingPower".Translate()}: " +
                          traitDef.additionalStoppingPower.ToStringByStyle(ToStringStyle.FloatOne,
                              ToStringNumberSense.Offset));
        }

        // equippedStat
        if (!traitDef.equippedStatOffsets.IsNullOrEmpty()) {
            foreach (var modifier in traitDef.equippedStatOffsets) {
                sb.AppendLine($" - {modifier.stat.LabelCap}: {modifier.stat.ValueToString(modifier.value)}");
            }
        }

        return sb.ToString();
    }

    internal static void BuildCacheAndInject() {
        foreach (var thingDef in DefDatabase<ThingDef>.AllDefs) {
            // fill weapon caches
            if (thingDef.IsWeapon && !thingDef.weaponTags.IsNullOrEmpty() && thingDef.race == null &&
                !thingDef.IsCorpse) {
                foreach (var tag in thingDef.weaponTags) {
                    if (!WeaponsByTag.ContainsKey(tag)) {
                        WeaponsByTag[tag] = [];
                    }

                    WeaponsByTag[tag].Add(thingDef);
                }
            }

            var ext = thingDef.GetModExtension<TraitModuleExtension>();
            if (ext?.weaponTraitDef == null) continue;

            // fill trait caches
            if (TraitToPart.ContainsKey(ext.weaponTraitDef)) {
                Log.Warning(
                    $"[CWF] Cache building warning: WeaponTraitDef '{ext.weaponTraitDef.defName}' is defined by multiple TraitModules. " +
                    $"The one in '{thingDef.defName}' will overwrite previous entries. This may cause unpredictable behavior when uninstalling parts.");
            }

            TraitToPart[ext.weaponTraitDef] = ext.part;
            TraitToModule[ext.weaponTraitDef] = thingDef;
        }

        foreach (var moduleDef in TraitToModule.Values) {
            // inject description
            var traitDef = moduleDef.GetModExtension<TraitModuleExtension>()?.weaponTraitDef;
            if (traitDef?.description != null) {
                moduleDef.description = traitDef.description;
            }

            // inject hyperlinks
            var weaponDefs = GetCompatibleWeaponDefsFor(moduleDef).ToList();
            if (weaponDefs.Empty()) continue;

            moduleDef.descriptionHyperlinks ??= [];
            foreach (var weaponDef in weaponDefs) {
                if (moduleDef.descriptionHyperlinks.Any(h => h.def == weaponDef)) continue;

                moduleDef.descriptionHyperlinks.Add(new DefHyperlink(weaponDef));
            }
        }
    }

    internal static bool TryGetPart(this WeaponTraitDef traitDef, out PartDef part) {
        return TraitToPart.TryGetValue(traitDef, out part);
    }

    internal static bool TryGetModuleDef(this WeaponTraitDef traitDef, [NotNullWhen(true)] out ThingDef? moduleDef) {
        return TraitToModule.TryGetValue(traitDef, out moduleDef);
    }

    internal static bool IsCompatibleWith(this ThingDef moduleDef, ThingDef weaponDef) {
        var ext = moduleDef.GetModExtension<TraitModuleExtension>();
        if (ext == null) return false;

        // exclude first 
        if (ext.excludeWeaponDefs != null && ext.excludeWeaponDefs.Contains(weaponDef)) {
            return false;
        }

        if (!ext.excludeWeaponTags.IsNullOrEmpty() && !weaponDef.weaponTags.IsNullOrEmpty() &&
            ext.excludeWeaponTags.Any(t => weaponDef.weaponTags.Contains(t))) {
            return false;
        }

        // defs
        if (!ext.requiredWeaponDefs.IsNullOrEmpty() && ext.requiredWeaponDefs.Contains(weaponDef)) {
            return true;
        }

        // tags
        if (!ext.requiredWeaponTags.IsNullOrEmpty() && !weaponDef.weaponTags.IsNullOrEmpty() &&
            ext.requiredWeaponTags.Any(tag => weaponDef.weaponTags.Contains(tag))) {
            return true;
        }

        return ext.requiredWeaponDefs.IsNullOrEmpty() && ext.requiredWeaponTags.IsNullOrEmpty();
    }

    #region Helpers

    private static IEnumerable<ThingDef> GetCompatibleWeaponDefsFor(ThingDef moduleDef) {
        var ext = moduleDef.GetModExtension<TraitModuleExtension>();
        if (ext == null) yield break;

        var results = new HashSet<ThingDef>();

        if (!ext.requiredWeaponDefs.IsNullOrEmpty()) {
            results.AddRange(ext.requiredWeaponDefs);
        }

        if (!ext.requiredWeaponTags.IsNullOrEmpty()) {
            foreach (var tag in ext.requiredWeaponTags) {
                if (WeaponsByTag.TryGetValue(tag, out var weapons)) {
                    results.AddRange(weapons);
                }
            }
        }

        if (!ext.excludeWeaponDefs.IsNullOrEmpty()) {
            results.ExceptWith(ext.excludeWeaponDefs);
        }

        if (!ext.excludeWeaponTags.IsNullOrEmpty()) {
            foreach (var tag in ext.excludeWeaponTags) {
                if (WeaponsByTag.TryGetValue(tag, out var weaponsToExclude)) {
                    results.ExceptWith(weaponsToExclude);
                }
            }
        }

        foreach (var weaponDef in results) {
            yield return weaponDef;
        }
    }

    #endregion
}