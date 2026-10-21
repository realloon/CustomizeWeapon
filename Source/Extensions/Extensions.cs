using System.Diagnostics.CodeAnalysis;
using System.Text;
using UnityEngine;

namespace CWF.Extensions;

internal static class Extensions {
    extension(ThingDef moduleDef) {
        internal bool IsCompatibleWith(ThingDef weaponDef) {
            var ext = moduleDef.TraitModule();
            var weaponTags = weaponDef.weaponTags;

            if (ext.excludeWeaponDefs.Contains(weaponDef)) return false;
            if (weaponTags != null && ext.excludeWeaponTags.Any(weaponTags.Contains)) return false;

            if (ext.requiredWeaponDefs.Contains(weaponDef)) return true;
            if (weaponTags != null && ext.requiredWeaponTags.Any(weaponTags.Contains)) return true;

            return ext.requiredWeaponDefs.Count == 0 && ext.requiredWeaponTags.Count == 0;
        }

        internal float GetRarityWeight() => Settings.Current.GetRarityWeight(moduleDef.GetRarity());

        internal string GetRarityLabel() => moduleDef.GetRarity() switch {
            Rarity.Standard => "CWF_Rarity_Standard".Translate(),
            Rarity.Rare => "CWF_Rarity_Rare".Translate(),
            Rarity.Legendary => "CWF_Rarity_Legendary".Translate(),
            _ => throw new ArgumentOutOfRangeException()
        };

        private Rarity GetRarity() => moduleDef.TraitModule().rarity;

        internal TraitModuleExtension TraitModule() =>
            moduleDef.GetModExtension<TraitModuleExtension>() ??
            throw new InvalidOperationException(
                $"[CWF] Module '{moduleDef.defName}' is missing {nameof(TraitModuleExtension)}.");
    }

    extension(WeaponTraitDef traitDef) {
        internal string GetTraitEffect() {
            var sb = new StringBuilder();

            if (!traitDef.statOffsets.NullOrEmpty()) {
                foreach (var modifier in traitDef.statOffsets) {
                    if (modifier.stat == StatDefOf.MarketValue || modifier.stat == StatDefOf.Mass) continue;

                    sb.AppendLine($" - {modifier.stat.LabelCap}: " +
                                  modifier.stat.Worker.ValueToString(modifier.value, false,
                                      ToStringNumberSense.Offset));
                }
            }

            if (!traitDef.statFactors.NullOrEmpty()) {
                foreach (var modifier in traitDef.statFactors) {
                    sb.AppendLine($" - {modifier.stat.LabelCap}: " +
                                  modifier.stat.Worker.ValueToString(modifier.value, false,
                                      ToStringNumberSense.Factor));
                }
            }

            if (!Mathf.Approximately(traitDef.burstShotCountMultiplier, 1f)) {
                sb.AppendLine($" - {"CWF_BurstShotCountMultiplier".Translate()}: " +
                              traitDef.burstShotCountMultiplier.ToStringByStyle(ToStringStyle.PercentZero,
                                  ToStringNumberSense.Factor));
            }

            if (!Mathf.Approximately(traitDef.burstShotSpeedMultiplier, 1f)) {
                sb.AppendLine($" - {"CWF_BurstShotSpeedMultiplier".Translate()}: " +
                              traitDef.burstShotSpeedMultiplier.ToStringByStyle(ToStringStyle.PercentZero,
                                  ToStringNumberSense.Factor));
            }

            if (!Mathf.Approximately(traitDef.additionalStoppingPower, 0.0f)) {
                sb.AppendLine($" - {"CWF_AdditionalStoppingPower".Translate()}: " +
                              traitDef.additionalStoppingPower.ToStringByStyle(ToStringStyle.FloatOne,
                                  ToStringNumberSense.Offset));
            }

            if (!traitDef.equippedStatOffsets.NullOrEmpty()) {
                foreach (var modifier in traitDef.equippedStatOffsets) {
                    sb.AppendLine($" - {modifier.stat.LabelCap}: {modifier.stat.ValueToString(modifier.value)}");
                }
            }

            return sb.ToString();
        }

        internal bool TryGetPart([NotNullWhen(true)] out PartDef? part) =>
            ModuleDatabase.TryGetPart(traitDef, out part);

        internal bool TryGetModuleDef([NotNullWhen(true)] out ThingDef? moduleDef) =>
            ModuleDatabase.TryGetModuleDef(traitDef, out moduleDef);
    }
}