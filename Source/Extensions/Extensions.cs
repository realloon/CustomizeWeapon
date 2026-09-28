using System.Diagnostics.CodeAnalysis;
using System.Text;
using UnityEngine;

namespace CWF.Extensions;

internal static class Extensions {
    extension(ThingDef moduleDef) {
        internal bool IsCompatibleWith(ThingDef weaponDef) {
            var ext = moduleDef.GetModExtension<TraitModuleExtension>();
            if (ext == null) return false;

            if (ext.excludeWeaponDefs != null && ext.excludeWeaponDefs.Contains(weaponDef)) {
                return false;
            }

            if (!ext.excludeWeaponTags.NullOrEmpty() && !weaponDef.weaponTags.NullOrEmpty() &&
                ext.excludeWeaponTags.Any(t => weaponDef.weaponTags.Contains(t))) {
                return false;
            }

            if (!ext.requiredWeaponDefs.NullOrEmpty() && ext.requiredWeaponDefs!.Contains(weaponDef)) {
                return true;
            }

            if (!ext.requiredWeaponTags.NullOrEmpty() && !weaponDef.weaponTags.NullOrEmpty() &&
                ext.requiredWeaponTags.Any(tag => weaponDef.weaponTags.Contains(tag))) {
                return true;
            }

            return ext.requiredWeaponDefs.NullOrEmpty() && ext.requiredWeaponTags.NullOrEmpty();
        }

        internal float GetRarityWeight() => Settings.Current.GetRarityWeight(moduleDef.GetRarity());

        internal string GetRarityLabel() => moduleDef.GetRarity() switch {
            Rarity.Standard => "CWF_Rarity_Standard".Translate(),
            Rarity.Rare => "CWF_Rarity_Rare".Translate(),
            Rarity.Legendary => "CWF_Rarity_Legendary".Translate(),
            _ => throw new ArgumentOutOfRangeException()
        };

        private Rarity GetRarity() {
            var ext = moduleDef.GetModExtension<TraitModuleExtension>();
            if (ext == null) {
                throw new InvalidOperationException(
                    $"[CWF] Module '{moduleDef.defName}' is missing {nameof(TraitModuleExtension)}.");
            }

            return ext.rarity;
        }
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

        internal bool TryGetPart(out PartDef part) => ModuleDatabase.TryGetPart(traitDef, out part);

        internal bool TryGetModuleDef([NotNullWhen(true)] out ThingDef? moduleDef) {
            return ModuleDatabase.TryGetModuleDef(traitDef, out moduleDef);
        }
    }
}