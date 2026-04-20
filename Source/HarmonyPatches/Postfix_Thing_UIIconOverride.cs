using JetBrains.Annotations;
using HarmonyLib;
using UnityEngine;
using Verse;

// ReSharper disable InconsistentNaming

namespace CWF.HarmonyPatches;

[HarmonyPatch(typeof(Thing), nameof(Thing.UIIconOverride), MethodType.Getter)]
public static class Postfix_Thing_UIIconOverride {
    [UsedImplicitly]
    [HarmonyPostfix]
    public static void Postfix(Thing __instance, ref Texture? __result) {
        if (__result != null) return;
        if (!Settings.Current.DynamicTexturesEnabled) return;

        if (__instance.TryGetComp<CompDynamicGraphic>(out var compDynamicGraphic)) {
            __result = compDynamicGraphic.GetUIIconTexture();
        }
    }
}