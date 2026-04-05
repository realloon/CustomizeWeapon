using JetBrains.Annotations;
using HarmonyLib;
using UnityEngine;
using Verse;

// ReSharper disable InconsistentNaming

namespace CWF.HarmonyPatches;

[HarmonyPatch(
    typeof(Projectile),
    nameof(Projectile.Launch), new[] {
        typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo),
        typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef)
    }
)]
public static class Postfix_Projectile_Launch_StoppingPower {
    [UsedImplicitly]
    public static void Postfix(Projectile __instance, Thing? equipment) {
        if (equipment == null || !equipment.TryGetComp<CompDynamicTraits>(out var comp)) return;

        foreach (var trait in comp.Traits) {
            if (!Mathf.Approximately(trait.additionalStoppingPower, 0.0f)) {
                __instance.stoppingPower += trait.additionalStoppingPower;
            }
        }
    }
}