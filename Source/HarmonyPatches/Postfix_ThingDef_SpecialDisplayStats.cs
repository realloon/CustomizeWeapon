using JetBrains.Annotations;
using System.Text;
using HarmonyLib;
using UnityEngine;
using RimWorld;
using Verse;

// ReSharper disable InconsistentNaming

namespace CWF.HarmonyPatches;

[HarmonyPatch(typeof(ThingDef), nameof(ThingDef.SpecialDisplayStats))]
public static class Postfix_ThingDef_SpecialDisplayStats {
    [UsedImplicitly]
    public static IEnumerable<StatDrawEntry> Postfix(IEnumerable<StatDrawEntry> __result, ThingDef __instance,
        StatRequest req) {
        var resultList = __result.ToList();
        var customEntries = GetDynamicWeaponStats(__instance, req, resultList);

        foreach (var entry in customEntries) {
            yield return entry;
        }

        foreach (var entry in resultList) {
            yield return entry;
        }
    }

    private static List<StatDrawEntry> GetDynamicWeaponStats(ThingDef thingDef, StatRequest req,
        List<StatDrawEntry> resultList) {
        if (!req.HasThing || req.Thing.TryGetComp<CompDynamicTraits>() is not { Traits.Count: > 0 } comp) {
            return [];
        }

        var verb = thingDef.Verbs?.FirstOrDefault(v => v.isPrimary);
        if (verb == null) return [];

        var statCat = thingDef.IsMeleeWeapon ? StatCategoryDefOf.Weapon_Melee : StatCategoryDefOf.Weapon_Ranged;
        var customEntries = new List<StatDrawEntry>();

        if (verb is { showBurstShotStats: true, burstShotCount: > 1 }) {
            resultList.RemoveAll(entry => entry.DisplayPriorityWithinCategory == 5391);
            var baseBurstCount = (float)verb.burstShotCount;
            var burstCountMultiplier =
                comp.Traits.Aggregate(1f, (current, trait) => current * trait.burstShotCountMultiplier);
            var finalBurstCount = baseBurstCount * burstCountMultiplier;

            var burstCountSb = new StringBuilder("Stat_Thing_Weapon_BurstShotFireRate_Desc".Translate());
            burstCountSb.AppendLine().AppendLine();
            burstCountSb.AppendLine("StatsReport_BaseValue".Translate() + ": " + verb.burstShotCount);
            comp.GetStatsExplanation(burstCountSb, "    ", t => t.burstShotCountMultiplier, 1f,
                ToStringNumberSense.Factor, ToStringStyle.PercentZero);
            burstCountSb.AppendLine()
                .AppendLine("StatsReport_FinalValue".Translate() + ": " + Mathf.CeilToInt(finalBurstCount));

            customEntries.Add(new StatDrawEntry(statCat, "BurstShotCount".Translate(),
                Mathf.CeilToInt(finalBurstCount).ToString(), burstCountSb.ToString(), 5391));

            resultList.RemoveAll(entry => entry.DisplayPriorityWithinCategory == 5395);
            var baseTicksBetweenShots = (float)verb.ticksBetweenBurstShots;
            var burstSpeedMultiplier =
                comp.Traits.Aggregate(1f, (current, trait) => current * trait.burstShotSpeedMultiplier);
            var finalTicksBetweenShots = baseTicksBetweenShots / burstSpeedMultiplier;
            var finalFireRate = 60f / (finalTicksBetweenShots / 60f);

            var fireRateSb = new StringBuilder("Stat_Thing_Weapon_BurstShotFireRate_Desc".Translate());
            fireRateSb.AppendLine().AppendLine();
            fireRateSb.AppendLine("StatsReport_BaseValue".Translate() + ": " +
                                  (60f / verb.ticksBetweenBurstShots.TicksToSeconds()).ToString("0.##") + " rpm");
            comp.GetStatsExplanation(fireRateSb, "    ", t => t.burstShotSpeedMultiplier, 1f,
                ToStringNumberSense.Factor, ToStringStyle.PercentZero);
            fireRateSb.AppendLine().AppendLine("StatsReport_FinalValue".Translate() + ": " +
                                               finalFireRate.ToString("0.##") + " rpm");

            customEntries.Add(new StatDrawEntry(statCat, "BurstShotFireRate".Translate(),
                finalFireRate.ToString("0.##") + " rpm", fireRateSb.ToString(), 5395));
        }

        var stoppingPowerStat = verb.defaultProjectile?.projectile?.stoppingPower;
        if (stoppingPowerStat is not > 0f) return customEntries;

        resultList.RemoveAll(entry => entry.DisplayPriorityWithinCategory == 5402);
        var baseStoppingPower = stoppingPowerStat.Value;
        var additionalStoppingPower = comp.Traits.Sum(t => t.additionalStoppingPower);
        var finalStoppingPower = baseStoppingPower + additionalStoppingPower;

        var stoppingPowerSb = new StringBuilder("StoppingPowerExplanation".Translate());
        stoppingPowerSb.AppendLine().AppendLine();
        stoppingPowerSb.AppendLine(
            "StatsReport_BaseValue".Translate() + ": " + baseStoppingPower.ToString("F1"));
        comp.GetStatsExplanation(stoppingPowerSb, "    ", t => t.additionalStoppingPower, 0f,
            ToStringNumberSense.Offset, ToStringStyle.FloatOne);
        stoppingPowerSb.AppendLine()
            .AppendLine("StatsReport_FinalValue".Translate() + ": " + finalStoppingPower.ToString("F1"));

        customEntries.Add(new StatDrawEntry(statCat, "StoppingPower".Translate(),
            finalStoppingPower.ToString("F1"), stoppingPowerSb.ToString(), 5402));

        return customEntries;
    }

    private static void GetStatsExplanation(
        this CompDynamicTraits comp,
        StringBuilder sb,
        string whitespace,
        Func<WeaponTraitDef, float> valueSelector,
        float defaultValue,
        ToStringNumberSense numberSense,
        ToStringStyle toStringStyle) {
        var stringBuilder = new StringBuilder();

        foreach (var weaponTraitDef in comp.Traits) {
            var value = valueSelector(weaponTraitDef);
            if (Mathf.Approximately(value, defaultValue)) continue;

            var valueStr = value.ToStringByStyle(toStringStyle, numberSense);
            stringBuilder.AppendLine($"{whitespace} - {weaponTraitDef.LabelCap}: {valueStr}");
        }

        if (stringBuilder.Length == 0) return;

        sb.AppendLine(whitespace + "CWF_WeaponModules".Translate() + ":");
        sb.Append(stringBuilder);
    }
}