using JetBrains.Annotations;
using System.Reflection;
using HarmonyLib;
using RimWorld;

// ReSharper disable InconsistentNaming

namespace CWF.HarmonyPatches;

[HarmonyPatch(typeof(CompEquippableAbility), "get_AbilityForReading")]
public static class Postfix_CompEquippableAbility_AbilityForReading {
    private static readonly FieldInfo? AbilityField = AccessTools.Field(typeof(CompEquippableAbility), "ability");

    [UsedImplicitly]
    [HarmonyPostfix]
    public static void Postfix(CompEquippableAbility __instance, ref Ability? __result) {
        var props = __instance.props as CompProperties_EquippableAbility;
        var expectedAbilityDef = props?.abilityDef;

        if ((__result == null || __result.def == expectedAbilityDef) &&
            (__result == null || expectedAbilityDef != null)) return;

        if (AbilityField != null) {
            AbilityField.SetValue(__instance, null);
        }

        __result = __instance.AbilityForReading;
    }
}