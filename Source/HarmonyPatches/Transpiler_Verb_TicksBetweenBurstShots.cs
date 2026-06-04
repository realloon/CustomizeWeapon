using JetBrains.Annotations;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace CWF.HarmonyPatches;

// ReSharper disable once InconsistentNaming
[HarmonyPatch(typeof(Verb), "get_TicksBetweenBurstShots")]
public static class Transpiler_Verb_TicksBetweenBurstShots {
    [UsedImplicitly]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
        var codes = new List<CodeInstruction>(instructions);
        var roundToInt = AccessTools.Method(typeof(Mathf), nameof(Mathf.RoundToInt), new[] { typeof(float) });

        for (var i = 0; i < codes.Count - 2; i++) {
            if (codes[i].opcode != OpCodes.Ldarg_0 ||
                codes[i + 1].opcode != OpCodes.Ldloc_0 ||
                !codes[i + 2].Calls(roundToInt)) continue;

            codes.InsertRange(i, new List<CodeInstruction> {
                new(OpCodes.Ldloc_0),
                new(OpCodes.Ldarg_0),
                CodeInstruction.Call(typeof(Transpiler_Verb_TicksBetweenBurstShots), nameof(ApplyDynamicMultipliers)),
                new(OpCodes.Stloc_0)
            });
            return codes;
        }

        Log.Error("[CWF] Verb.get_TicksBetweenBurstShots transpiler failed.");
        return codes;
    }

    private static float ApplyDynamicMultipliers(float original, Verb verb) {
        var equipment = verb.EquipmentSource;
        if (equipment == null || !equipment.TryGetComp<CompDynamicTraits>(out var compDynamicTraits)) return original;

        return compDynamicTraits.Traits.Where(trait => trait.burstShotSpeedMultiplier != 0)
            .Aggregate(original, (current, trait) => current / trait.burstShotSpeedMultiplier);
    }
}