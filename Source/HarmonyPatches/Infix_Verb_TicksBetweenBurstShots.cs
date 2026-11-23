using JetBrains.Annotations;
using HarmonyLib;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace CWF.HarmonyPatches;

[HarmonyPatch(typeof(Verb), nameof(Verb.TicksBetweenBurstShots), MethodType.Getter)]
public static class Infix_Verb_TicksBetweenBurstShots {
    [UsedImplicitly]
    [HarmonyPrefix, HarmonyInfix(typeof(Mathf), nameof(Mathf.RoundToInt), typeof(float))]
    public static void Prefix(ref float __0, [HarmonyOuter] Verb __instance) {
        if (__instance.EquipmentSource?.TryGetComp<CompDynamicTraits>() is not { } comp) return;

        __0 = comp.Traits
            .Where(trait => trait.burstShotSpeedMultiplier != 0f)
            .Aggregate(__0, (current, trait) => current / trait.burstShotSpeedMultiplier);
    }
}