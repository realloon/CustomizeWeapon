using JetBrains.Annotations;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace CWF.HarmonyPatches;

[HarmonyPatch(typeof(StatWorker), nameof(StatWorker.StatOffsetFromGear))]
// ReSharper disable once InconsistentNaming
public static class Transpiler_StatWorker_StatOffsetFromGear {
    private static readonly MethodInfo ContributionMethod = AccessTools
        .Method(typeof(Transpiler_StatWorker_StatOffsetFromGear), nameof(Contribution))!;

    private static readonly Dictionary<OpCode, OpCode> ShortFormStores = new() {
        [OpCodes.Ldloc_0] = OpCodes.Stloc_0,
        [OpCodes.Ldloc_1] = OpCodes.Stloc_1,
        [OpCodes.Ldloc_2] = OpCodes.Stloc_2,
        [OpCodes.Ldloc_3] = OpCodes.Stloc_3
    };

    [UsedImplicitly]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
        var abs = AccessTools.Method(typeof(Math), nameof(Math.Abs), [typeof(float)]);
        var matcher = new CodeMatcher(instructions)
            .MatchStartForward(new CodeMatch(OpCodes.Call, abs));

        var access = matcher.IsInvalid ? null : FindValueAccess(matcher);
        if (access is not var (store, reload)) {
            Log.Error("[CWF] Could not locate the offset anchor in StatWorker.StatOffsetFromGear");
            return matcher.InstructionEnumeration();
        }

        // keep any branch/exception labels on the injected sequence
        var anchor = matcher.Instruction;
        var contribution = new CodeInstruction(OpCodes.Ldarg_0);
        contribution.labels.AddRange(anchor.labels);
        anchor.labels.Clear();

        return matcher
            .Insert(
                contribution,
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, ContributionMethod),
                new CodeInstruction(OpCodes.Add),
                store,
                reload
            )
            .InstructionEnumeration();
    }

    public static float Contribution(Thing gear, StatDef stat) {
        return gear.TryGetComp<CompDynamicTraits>(out var comp) && comp.TryGetEquippedOffset(stat, out var value)
            ? value
            : 0f;
    }

    private static (CodeInstruction Store, CodeInstruction Reload)? FindValueAccess(CodeMatcher matcher) {
        var instructions = matcher.Instructions();
        var index = matcher.Pos - 1;

        while (index >= 0 && instructions[index].opcode == OpCodes.Nop) {
            index -= 1;
        }

        if (index < 0) {
            return null;
        }

        var instruction = instructions[index];

        if (instruction.operand is not LocalBuilder local) {
            return ShortFormStores.TryGetValue(instruction.opcode, out var shortStoreOpcode)
                ? (new CodeInstruction(shortStoreOpcode), new CodeInstruction(instruction.opcode))
                : null;
        }

        if (instruction.opcode != OpCodes.Ldloc && instruction.opcode != OpCodes.Ldloc_S) {
            return null;
        }

        var storeOpcode = instruction.opcode == OpCodes.Ldloc ? OpCodes.Stloc : OpCodes.Stloc_S;

        return (new CodeInstruction(storeOpcode, local), new CodeInstruction(instruction.opcode, local));
    }
}