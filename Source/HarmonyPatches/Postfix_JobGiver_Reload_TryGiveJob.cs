using JetBrains.Annotations;
using RimWorld;
using RimWorld.Utility;
using Verse;
using Verse.AI;

// ReSharper disable InconsistentNaming

namespace CWF.HarmonyPatches;

[HarmonyLib.HarmonyPatch(typeof(JobGiver_Reload), "TryGiveJob")]
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

            if (pawn.carryTracker.AvailableStackSpace(resourceDef) < reloadable.MinAmmoNeeded(allowForcedReload: true)) {
                continue;
            }

            var chosenResources = ReloadableUtility.FindEnoughAmmo(pawn, pawn.Position, reloadable, forceReload: false);
            if (!chosenResources.NullOrEmpty()) {
                var job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("CWF_ReloadAbility"),
                    reloadable.ReloadableThing);
                job.targetQueueB = chosenResources.Select(thing => new LocalTargetInfo(thing)).ToList();
                job.count = Math.Min(chosenResources.Sum(thing => thing.stackCount),
                    reloadable.MaxAmmoNeeded(allowForcedReload: true));
                job.source = new ReloadAbilityJobSource { AbilityDef = reloadable.AbilityDef };
                job.playerForced = false;
                return job;
            }
        }

        return null;
    }
}