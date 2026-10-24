using HarmonyLib;
using JetBrains.Annotations;

// ReSharper disable InconsistentNaming

namespace CWF.HarmonyPatches;

[HarmonyPatch(typeof(StatWorker), "GearHasCompsThatAffectStat")]
public static class Postfix_StatWorker_GearHasCompsThatAffectStat {
    [UsedImplicitly]
    public static void Postfix(Thing gear, StatDef stat, ref bool __result) {
        if (__result) return;

        __result = gear.TryGetComp<CompDynamicTraits>(out var comp) && comp.HasEquippedOffset(stat);
    }
}