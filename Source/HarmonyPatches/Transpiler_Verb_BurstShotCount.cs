using JetBrains.Annotations;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CWF.HarmonyPatches;

// ReSharper disable once InconsistentNaming
[HarmonyPatch(typeof(Verb), nameof(Verb.BurstShotCount), MethodType.Getter)]
public static class Transpiler_Verb_BurstShotCount {
    [UsedImplicitly]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
        var codes = new List<CodeInstruction>(instructions);

        var loopStartIndex = codes.FindIndex(code => code.opcode == OpCodes.Stloc_0) + 1;

        var loopEndIndex = -1;
        for (var i = loopStartIndex; i < codes.Count; i++) {
            if (codes[i].opcode != OpCodes.Ldarg_0 ||
                codes[i + 1].opcode != OpCodes.Ldloc_0 ||
                codes[i + 2].opcode != OpCodes.Call ||
                !codes[i + 2].operand.ToString().Contains("CeilToInt")) continue;
            loopEndIndex = i;
            break;
        }

        if (loopStartIndex > 0 && loopEndIndex != -1) {
            codes.RemoveRange(loopStartIndex, loopEndIndex - loopStartIndex);

            var newInstructions = new List<CodeInstruction> {
                new(OpCodes.Ldloc_0),
                new(OpCodes.Ldarg_0),
                CodeInstruction.Call(typeof(Transpiler_Verb_BurstShotCount), nameof(ApplyAllMultipliers)),
                new(OpCodes.Stloc_0)
            };

            codes.InsertRange(loopStartIndex, newInstructions);
        } else {
            Log.Error(
                "[CWF] Transpiler for Verb.get_BurstShotCount failed. The mod may not function correctly with this version of RimWorld.");
        }

        return codes.AsEnumerable();
    }

    private static float ApplyAllMultipliers(float originalNum, Verb verb) {
        var num = originalNum;
        var equipment = verb.EquipmentSource;

        if (equipment == null) return num;

        if (equipment.TryGetComp<CompUniqueWeapon>(out var uniqueWeapon)) {
            var traits = uniqueWeapon.TraitsListForReading;
            num = traits.Aggregate(num, (cur, t) => cur * t.burstShotCountMultiplier);
        }

        if (equipment.TryGetComp<CompDynamicTraits>(out var dynamicTraits)) {
            num = dynamicTraits.Traits.Aggregate(num, (cur, t) => cur * t.burstShotCountMultiplier);
        }

        return num;
    }
}