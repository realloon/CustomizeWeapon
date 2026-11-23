using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace CWF.HarmonyPatches;

[HarmonyPatch(typeof(CompEquippableAbility), nameof(CompEquippableAbility.AbilityForReading), MethodType.Getter)]
public static class Postfix_CompEquippableAbility_AbilityForReading {
    private static readonly AccessTools.FieldRef<CompEquippableAbility, Ability?> AbilityRef =
        AccessTools.FieldRefAccess<CompEquippableAbility, Ability?>("ability");

    [UsedImplicitly]
    public static void Postfix(CompEquippableAbility __instance, ref Ability? __result) {
        var props = __instance.props as CompProperties_EquippableAbility;
        var expectedAbilityDef = props?.abilityDef;

        if (__result == null || __result.def == expectedAbilityDef) return;

        AbilityRef(__instance) = null;
        __result = __instance.AbilityForReading;
    }
}