using System.Text;
using RimWorld;
using Verse;
using CWF.Extensions;

namespace CWF;

public class TraitEquippedOffsets : StatPart {
    public override void TransformValue(StatRequest req, ref float val) {
        if (!TryGetApplicableTraits(req, out var traits)) return;

        var totalOffset = traits
            .SelectMany(trait => trait.equippedStatOffsets)
            .Where(modifier => modifier?.stat == parentStat)
            .Sum(modifier => modifier!.value);

        if (totalOffset != 0f) {
            val += totalOffset;
        }
    }

    public override string ExplanationPart(StatRequest req) {
        if (!TryGetApplicableTraits(req, out var traits)) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.Append("CWF_FromEquipped".Translate());

        foreach (var trait in traits) {
            var modifiers = trait.equippedStatOffsets;
            if (modifiers.NullOrEmpty()) continue;

            var localOffset = modifiers
                .Where(modifier => modifier?.stat == parentStat)
                .Sum(modifier => modifier.value);

            if (localOffset == 0f) continue;

            var valueStr = parentStat.ValueToString(localOffset, numberSense: ToStringNumberSense.Offset);
            sb.AppendInNewLine($"    {trait.LabelCap}: {valueStr}"); // consistent with interface format
        }

        sb.AppendLine();

        return sb.ToString();
    }

    public override IEnumerable<Dialog_InfoCard.Hyperlink> GetInfoCardHyperlinks(StatRequest req) {
        if (!TryGetApplicableTraits(req, out var traits)) yield break;

        var seenModules = new HashSet<ThingDef>();
        foreach (var trait in traits) {
            if (trait.TryGetModuleDef(out var moduleDef) && seenModules.Add(moduleDef)) {
                yield return new Dialog_InfoCard.Hyperlink(moduleDef);
            }
        }
    }

    internal static void Inject() {
        var targetStats = new HashSet<StatDef>();
        var allTraits = DefDatabase<WeaponTraitDef>.AllDefsListForReading;

        foreach (var t in allTraits) {
            var modifiers = t.equippedStatOffsets;
            if (modifiers.NullOrEmpty()) continue;

            foreach (var modifier in modifiers) {
                if (modifier?.stat == null) continue;
                if (!modifier.stat.showOnPawns || !modifier.stat.showOnHumanlikes) continue;

                targetStats.Add(modifier.stat);
            }
        }

        if (targetStats.IsNullOrEmpty()) {
            Log.Warning("[CWF] No suitable StatDefs found to inject TraitEquippedOffsets.");
            return;
        }

        foreach (var stat in targetStats) {
            stat.parts ??= [];
            if (stat.parts.Any(p => p is TraitEquippedOffsets)) continue;

            stat.parts.Add(new TraitEquippedOffsets { parentStat = stat });
        }
    }

    // helper
    private bool TryGetApplicableTraits(StatRequest req, out IReadOnlyList<WeaponTraitDef> applicableTraits) {
        applicableTraits = [];
        if (!req.HasThing || req.Thing is not Pawn pawn) return false;

        var weapon = pawn.equipment?.Primary;
        if (weapon == null) return false;

        var traits = weapon.TryGetComp<CompDynamicTraits>()?.Traits;
        if (traits.IsNullOrEmpty()) return false;

        var list = traits
            .Where(trait => trait.equippedStatOffsets?
                .Any(modifier => modifier?.stat == parentStat) == true)
            .ToList();

        if (list.Count == 0) return false;

        applicableTraits = list;
        return true;
    }
}