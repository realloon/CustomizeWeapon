using JetBrains.Annotations;
using HarmonyLib;
using Verse;

// ReSharper disable InconsistentNaming

namespace CWF.HarmonyPatches;

[HarmonyPatch(typeof(CompEquippable), nameof(CompEquippable.CompGetEquippedGizmosExtra))]
public static class Postfix_CompEquippable_CompGetEquippedGizmosExtra {
    [UsedImplicitly]
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, CompEquippable __instance) {
        foreach (var gizmo in __result) {
            yield return gizmo;
        }

        if (__instance.parent.TryGetComp<CompDynamicTraits>() is not { } comp) yield break;
        if (__instance.parent.ParentHolder is not Pawn_EquipmentTracker { pawn: { } pawn }) yield break;

        foreach (var extraGizmo in comp.CompGetEquippedGizmosExtra(pawn)) {
            yield return extraGizmo;
        }
    }
}