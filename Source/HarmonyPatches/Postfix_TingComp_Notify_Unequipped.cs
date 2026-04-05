using JetBrains.Annotations;
using HarmonyLib;
using Verse;

// ReSharper disable InconsistentNaming

namespace CWF.HarmonyPatches;

[HarmonyPatch(typeof(ThingComp), nameof(ThingComp.Notify_Unequipped))]
public static class Postfix_ThingComp_Notify_Unequipped {
    [UsedImplicitly]
    public static void Postfix(ThingComp __instance, Pawn pawn) {
        var compAbilityProvider = __instance.parent.GetComp<CompAbilityProvider>();
        compAbilityProvider?.OnUnequipped(pawn);
    }
}