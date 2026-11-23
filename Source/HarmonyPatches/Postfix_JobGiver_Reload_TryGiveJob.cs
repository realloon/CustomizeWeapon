using JetBrains.Annotations;
using HarmonyLib;
using RimWorld.Utility;
using Verse.AI;

// ReSharper disable InconsistentNaming

namespace CWF.HarmonyPatches;

[HarmonyPatch(typeof(JobGiver_Reload), "TryGiveJob")]
public static class Postfix_JobGiver_Reload_TryGiveJob {
    [UsedImplicitly]
    public static Job? Postfix(Job? __result, Pawn pawn) {
        if (__result != null) {
            return __result;
        }

        var abilityProvider = pawn.equipment?.Primary?.TryGetComp<CompAbilityProvider>();
        if (abilityProvider == null) {
            return null;
        }

        foreach (var reloadable in abilityProvider.Reloadables) {
            var resourceDef = reloadable.AmmoDef;
            if (resourceDef == null || !reloadable.NeedsReload(allowForceReload: false)) {
                continue;
            }

            if (pawn.carryTracker.AvailableStackSpace(resourceDef) <
                reloadable.MinAmmoNeeded(allowForcedReload: true)) {
                continue;
            }

            var chosenResources = ReloadableUtility.FindEnoughAmmo(pawn, pawn.Position, reloadable, forceReload: false);
            if (!chosenResources.NullOrEmpty()) {
                return ReloadAbilityJobSource.Create(reloadable, chosenResources, playerForced: false);
            }
        }

        return null;
    }
}