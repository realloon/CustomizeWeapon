using JetBrains.Annotations;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CWF.HarmonyPatches;

// ReSharper disable once InconsistentNaming
[HarmonyPatch(typeof(PawnWeaponGenerator), nameof(PawnWeaponGenerator.TryGenerateWeaponFor))]
public static class Postfix_PawnWeaponGenerator_TryGenerateWeaponFor {
    [UsedImplicitly]
    public static void Postfix(Pawn pawn, PawnGenerationRequest request) {
        if (pawn.Faction is null || pawn.Faction.IsPlayer || !pawn.RaceProps.Humanlike) return;

        var weapon = pawn.equipment?.Primary;
        var compDynamicTraits = weapon?.TryGetComp<CompDynamicTraits>();

        compDynamicTraits?.RandomizeTraits();
    }
}