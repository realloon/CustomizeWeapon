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
public static class Postfix_Projectile_Launch {
    [UsedImplicitly]
    public static void Postfix(Projectile __instance, Thing? equipment) {
        if (equipment == null || !equipment.TryGetComp<CompDynamicTraits>(out var compDynamicTraits)) return;

        foreach (var trait in compDynamicTraits.Traits) {
            if (trait.damageDefOverride != null) {
                __instance.damageDefOverride = trait.damageDefOverride;
            }

            if (!trait.extraDamages.NullOrEmpty()) {
                __instance.extraDamages.AddRange(trait.extraDamages);
            }

            if (!Mathf.Approximately(trait.additionalStoppingPower, 0f)) {
                __instance.stoppingPower += trait.additionalStoppingPower;
            }
        }
    }
}
