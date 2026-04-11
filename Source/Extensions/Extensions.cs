using System.Diagnostics.CodeAnalysis;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace CWF.Extensions;

internal static class Extensions {
    internal static bool IsNullOrEmpty([NotNullWhen(false)] this string? str) {
        return string.IsNullOrEmpty(str);
    }

    internal static bool IsNullOrEmpty<T>([NotNullWhen(false)] this IReadOnlyCollection<T>? collection) {
        return collection == null || collection.Count == 0;
    }

    extension(ThingDef moduleDef) {
        internal bool IsCompatibleWith(ThingDef weaponDef) {
            var ext = moduleDef.GetModExtension<TraitModuleExtension>();
            if (ext == null) return false;

            if (ext.excludeWeaponDefs != null && ext.excludeWeaponDefs.Contains(weaponDef)) {
                return false;
            }

            if (!ext.excludeWeaponTags.IsNullOrEmpty() && !weaponDef.weaponTags.IsNullOrEmpty() &&
                ext.excludeWeaponTags.Any(t => weaponDef.weaponTags.Contains(t))) {
                return false;
            }

            if (!ext.requiredWeaponDefs.IsNullOrEmpty() && ext.requiredWeaponDefs.Contains(weaponDef)) {
                return true;
            }

            if (!ext.requiredWeaponTags.IsNullOrEmpty() && !weaponDef.weaponTags.IsNullOrEmpty() &&
                ext.requiredWeaponTags.Any(tag => weaponDef.weaponTags.Contains(tag))) {
                return true;
            }

            return ext.requiredWeaponDefs.IsNullOrEmpty() && ext.requiredWeaponTags.IsNullOrEmpty();
        }

        internal float GetRarityWeight() {
            return Settings.Current.GetRarityWeight(moduleDef.GetRarity());
        }

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

            if (!traitDef.statOffsets.IsNullOrEmpty()) {
                foreach (var modifier in traitDef.statOffsets) {
                    if (modifier.stat == StatDefOf.MarketValue || modifier.stat == StatDefOf.Mass) continue;

                    sb.AppendLine($" - {modifier.stat.LabelCap}: " +
                                  modifier.stat.Worker.ValueToString(modifier.value, false,
                                      ToStringNumberSense.Offset));
                }
            }

            if (!traitDef.statFactors.IsNullOrEmpty()) {
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

            if (!traitDef.equippedStatOffsets.IsNullOrEmpty()) {
                foreach (var modifier in traitDef.equippedStatOffsets) {
                    sb.AppendLine($" - {modifier.stat.LabelCap}: {modifier.stat.ValueToString(modifier.value)}");
                }
            }

            return sb.ToString();
        }

        internal bool TryGetPart(out PartDef part) {
            return ModuleDatabase.TryGetPart(traitDef, out part);
        }

        internal bool TryGetModuleDef([NotNullWhen(true)] out ThingDef? moduleDef) {
            return ModuleDatabase.TryGetModuleDef(traitDef, out moduleDef);
        }
    }
}