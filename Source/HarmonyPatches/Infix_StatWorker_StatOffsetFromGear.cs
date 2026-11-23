using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace CWF.HarmonyPatches;

[HarmonyPatch(typeof(StatWorker), nameof(StatWorker.StatOffsetFromGear))]
public static class Infix_StatWorker_StatOffsetFromGear {
    [UsedImplicitly]
    [HarmonyPostfix]
    [HarmonyInfix(typeof(StatUtility), nameof(StatUtility.GetStatOffsetFromList),
        typeof(List<StatModifier>), typeof(StatDef), Positions = [1])]
    public static void Postfix(ref float __result, [HarmonyOuter] Thing gear, [HarmonyOuter] StatDef stat) {
        if (gear.TryGetComp<CompDynamicTraits>(out var comp) && comp.TryGetEquippedOffset(stat, out var value)) {
            __result += value;
        }
    }
}