using JetBrains.Annotations;
using HarmonyLib;

// ReSharper disable InconsistentNaming

namespace CWF.HarmonyPatches;

[HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
public static class Infix_PawnRenderUtility_DrawEquipmentAiming {
    [UsedImplicitly]
    [HarmonyPrefix, HarmonyInfix(typeof(Thing), nameof(Thing.Graphic), InnerTargetKind.Getter)]
    public static bool Prefix(Thing __instance, ref Graphic __result) {
        if (__instance.TryGetComp<CompDynamicGraphic>() is not { UsesDynamicComposite: true } comp) return true;

        __result = comp.GetDynamicGraphic();
        return false;
    }
}