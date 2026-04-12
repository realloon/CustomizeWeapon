using JetBrains.Annotations;
using RimWorld;
using RimWorld.Utility;
using Verse;
using Verse.AI;
using CWF.Controllers;

// ReSharper disable InconsistentNaming

namespace CWF.HarmonyPatches;

[HarmonyLib.HarmonyPatch(typeof(FloatMenuOptionProvider_Reload), "GetOptionsFor")]
public static class Postfix_FloatMenuOptionProvider_Reload_GetOptionsFor {
    [UsedImplicitly]
    public static IEnumerable<FloatMenuOption> Postfix(IEnumerable<FloatMenuOption> __result, Thing clickedThing,
        FloatMenuContext context) {
        foreach (var originalResult in __result) {
            yield return originalResult;
        }

        var pawn = context.FirstSelectedPawn;
        var abilityProvider = pawn.equipment?.Primary?.TryGetComp<CompAbilityProvider>();
        if (abilityProvider == null) {
            yield break;
        }

        foreach (var reloadable in abilityProvider.GetReloadablesUsingResource(clickedThing.def, allowForcedReload: true)) {
            var resourceDef = reloadable.AmmoDef;
            if (resourceDef == null) {
                continue;
            }

            var text = "Reload".Translate(reloadable.ReloadableThing.Named("GEAR"), resourceDef.Named("AMMO")) +
                       $" ({reloadable.AbilityLabel}: {reloadable.LabelRemaining})";

            if (!pawn.CanReach(clickedThing, PathEndMode.ClosestTouch, Danger.Deadly)) {
                yield return new FloatMenuOption(text + ": " + "NoPath".Translate().CapitalizeFirst(), null);
                continue;
            }

            if (!reloadable.NeedsReload(allowForceReload: true)) {
                yield return new FloatMenuOption(text + ": " + "ReloadFull".Translate(), null);
                continue;
            }

            var chosenResources =
                ReloadableUtility.FindEnoughAmmo(pawn, clickedThing.Position, reloadable, forceReload: true);
            if (chosenResources == null) {
                yield return new FloatMenuOption(text + ": " + "ReloadNotEnough".Translate(), null);
                continue;
            }

            if (pawn.carryTracker.AvailableStackSpace(resourceDef) < reloadable.MinAmmoNeeded(allowForcedReload: true)) {
                yield return new FloatMenuOption(
                    text + ": " + "ReloadCannotCarryEnough".Translate(resourceDef.Named("AMMO")), null);
                continue;
            }

            var action = new Action(() => {
                pawn.jobs.TryTakeOrderedJob(
                    ReloadAbilityJobMaker.Make(reloadable, chosenResources, playerForced: true),
                    JobTag.Misc);
            });

            yield return
                FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text, action), pawn, clickedThing);
        }
    }
}