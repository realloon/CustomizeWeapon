using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using RimWorld.Utility;
using Verse;

namespace CWF.HarmonyPatches;

// ReSharper disable once InconsistentNaming
[HarmonyPatch]
public static class Transpiler_JobDriver_Reload_MakeNewToils {
    [UsedImplicitly]
    [HarmonyTargetMethod]
    public static MethodBase? TargetMethod() {
        var stateMachineType = AccessTools
            .FirstInner(typeof(JobDriver_Reload), t => t.Name.Contains("MakeNewToils"));

        if (stateMachineType is not null) {
            return AccessTools.Method(stateMachineType, "MoveNext");
        }

        Log.Error("[CWF] Could not find the state machine type for JobDriver_Reload.MakeNewToils.");
        return null;
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il) {
        var codeInstructions = instructions as CodeInstruction[] ?? instructions.ToArray();
        var codeMatcher = new CodeMatcher(codeInstructions, il);

        codeMatcher.MatchStartForward(
            new CodeMatch(instr => {
                if (instr.opcode != OpCodes.Call) return false;
                if (instr.operand is not MethodInfo method) return false;
                if (method.Name != nameof(ThingCompUtility.TryGetComp) || !method.IsGenericMethod) return false;

                var genericArgs = method.GetGenericArguments();
                if (genericArgs.Length != 1) return false;

                return genericArgs[0].Name == "CompEquippableAbilityReloadable";
            })
        );

        if (codeMatcher.IsInvalid) {
            Log.Error("[CWF] Transpiler in MoveNext failed: " +
                      "Could not find the call to TryGetComp<CompEquippableAbilityReloadable>.");
            return codeInstructions;
        }

        codeMatcher.Advance(1);

        // this maybe unnecessary
        var stateMachineType = AccessTools
            .FirstInner(typeof(JobDriver_Reload), t => t.Name.Contains("MakeNewToils"));
        if (stateMachineType is null) {
            Log.Error("[CWF] Transpiler in MoveNext failed: Could not re-find the state machine type.");
            return codeInstructions;
        }

        var thisField = AccessTools.Field(stateMachineType, "<>4__this");
        if (thisField is null) {
            Log.Error("[CWF] Transpiler in MoveNext failed: Could not find the '<>4__this' field.");
            return codeInstructions;
        }

        codeMatcher.Insert(
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld, thisField),
            new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(JobDriver_Reload), "Gear")),
            new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(Transpiler_JobDriver_Reload_MakeNewToils), nameof(FindCustomReloadable)))
        );

        return codeMatcher.InstructionEnumeration();
    }

    public static IReloadableComp? FindCustomReloadable(IReloadableComp? originalResult, Thing? gear) {
        return originalResult ?? gear?.TryGetComp<CompAbilityProvider>();
    }
}