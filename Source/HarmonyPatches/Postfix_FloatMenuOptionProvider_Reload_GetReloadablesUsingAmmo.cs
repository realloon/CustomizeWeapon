using JetBrains.Annotations;
using HarmonyLib;
using RimWorld;
using RimWorld.Utility;
using Verse;

// ReSharper disable InconsistentNaming

namespace CWF.HarmonyPatches;

[HarmonyPatch(typeof(FloatMenuOptionProvider_Reload), "GetReloadablesUsingAmmo")]
public static class Postfix_FloatMenuOptionProvider_Reload_GetReloadablesUsingAmmo {
    [UsedImplicitly]
    public static IEnumerable<IReloadableComp> Postfix(IEnumerable<IReloadableComp> __result, Pawn pawn,
        Thing clickedThing) {
        foreach (var originalResult in __result) {
            yield return originalResult;
        }

        var primaryEqComp = pawn.equipment?.PrimaryEq;
        if (primaryEqComp is null) yield break;

        var weapon = primaryEqComp.parent;
        var abilityProvider = weapon.TryGetComp<CompAbilityProvider>();

        if (abilityProvider is not null && abilityProvider.CanBeReloadedWith(clickedThing.def)) {
            yield return abilityProvider;
        }
    }
}