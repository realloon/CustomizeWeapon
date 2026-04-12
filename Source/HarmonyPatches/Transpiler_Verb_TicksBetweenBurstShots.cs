using JetBrains.Annotations;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CWF.HarmonyPatches;

[HarmonyPatch(typeof(Verb), "get_TicksBetweenBurstShots")]
// ReSharper disable once InconsistentNaming
public static class Transpiler_Verb_TicksBetweenBurstShots {
    [UsedImplicitly]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
        var codes = new List<CodeInstruction>(instructions);

        var loopStartIndex = codes.FindIndex(code => code.opcode == OpCodes.Stloc_0) + 1;

        var loopEndIndex = -1;
        for (var i = loopStartIndex; i < codes.Count; i++) {
            if (codes[i].opcode == OpCodes.Ldarg_0 &&
                codes[i + 1].opcode == OpCodes.Ldloc_0 &&
                codes[i + 2].opcode == OpCodes.Call &&
                codes[i + 2].operand.ToString().Contains("RoundToInt")) {
                loopEndIndex = i;
                break;
            }
        }

        if (loopStartIndex > 0 && loopEndIndex != -1) {
            codes.RemoveRange(loopStartIndex, loopEndIndex - loopStartIndex);

            var newInstructions = new List<CodeInstruction> {
                new(OpCodes.Ldloc_0),
                new(OpCodes.Ldarg_0),
                CodeInstruction.Call(typeof(Transpiler_Verb_TicksBetweenBurstShots), nameof(ApplyAllMultipliers)),
                new(OpCodes.Stloc_0)
            };

            codes.InsertRange(loopStartIndex, newInstructions);
        } else {
            Log.Error(
                "[CWF] Transpiler for Verb.get_TicksBetweenBurstShots failed. The mod may not function correctly with this version of RimWorld.");
        }

        return codes.AsEnumerable();
    }

    private static float ApplyAllMultipliers(float originalTicks, Verb verb) {
        var ticks = originalTicks;
        var equipment = verb.EquipmentSource;

        if (equipment == null) return ticks;

        if (equipment.TryGetComp<CompUniqueWeapon>(out var compUniqueWeapon)) {
            ticks = compUniqueWeapon.TraitsListForReading
                .Where(trait => trait.burstShotSpeedMultiplier != 0)
                .Aggregate(ticks, (current, trait) => current / trait.burstShotSpeedMultiplier);
        }

        if (equipment.TryGetComp<CompDynamicTraits>(out var compDynamicTraits)) {
            ticks = compDynamicTraits.Traits
                .Where(trait => trait.burstShotSpeedMultiplier != 0)
                .Aggregate(ticks, (current, trait) => current / trait.burstShotSpeedMultiplier);
        }

        return ticks;
    }
}