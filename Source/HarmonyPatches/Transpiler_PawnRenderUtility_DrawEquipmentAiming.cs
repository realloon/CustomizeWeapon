using JetBrains.Annotations;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace CWF.HarmonyPatches;

// ReSharper disable once InconsistentNaming
[HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
public static class Transpiler_PawnRenderUtility_DrawEquipmentAiming {
    [UsedImplicitly]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
        var thingGraphicGetter = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Graphic));
        var replacementMethod = AccessTools.Method(typeof(Transpiler_PawnRenderUtility_DrawEquipmentAiming),
            nameof(GetDynamicGraphicOrOriginal));

        foreach (var instruction in instructions) {
            if (instruction.Calls(thingGraphicGetter)) {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacementMethod;
            }

            yield return instruction;
        }
    }

    private static Graphic GetDynamicGraphicOrOriginal(Thing thing) {
        return thing.TryGetComp<CompDynamicGraphic>(out var compDynamicGraphic)
            ? compDynamicGraphic.GetDynamicGraphic()
            : thing.Graphic;
    }
}
