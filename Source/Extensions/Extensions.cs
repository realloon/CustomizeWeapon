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

            // Market value and mass are already shown by the item stats, so they are skipped here.
            AppendStatModifiers(sb, traitDef.statOffsets, ToStringNumberSense.Offset, skipMassAndMarketValue: true);
            AppendStatModifiers(sb, traitDef.statFactors, ToStringNumberSense.Factor);

            AppendScalarLine(sb, "CWF_BurstShotCountMultiplier", traitDef.burstShotCountMultiplier, 1f,
                ToStringStyle.PercentZero, ToStringNumberSense.Factor);
            AppendScalarLine(sb, "CWF_BurstShotSpeedMultiplier", traitDef.burstShotSpeedMultiplier, 1f,
                ToStringStyle.PercentZero, ToStringNumberSense.Factor);
            AppendScalarLine(sb, "CWF_AdditionalStoppingPower", traitDef.additionalStoppingPower, 0f,
                ToStringStyle.FloatOne, ToStringNumberSense.Offset);

            AppendStatModifiers(sb, traitDef.equippedStatOffsets, ToStringNumberSense.Offset);

            return sb.ToString();
        }
    }

    private static void AppendStatModifiers(StringBuilder sb, List<StatModifier>? modifiers,
        ToStringNumberSense numberSense, bool skipMassAndMarketValue = false) {
        if (modifiers is not { Count: > 0 }) return;

        foreach (var modifier in modifiers) {
            if (skipMassAndMarketValue && (modifier.stat == StatDefOf.MarketValue || modifier.stat == StatDefOf.Mass)) {
                continue;
            }

            var value = modifier.stat.ValueToString(modifier.value, numberSense, false);
            sb.AppendLine($" - {modifier.stat.LabelCap}: {value}");
        }
    }

    private static void AppendScalarLine(StringBuilder sb, string labelKey, float value, float neutral,
        ToStringStyle style, ToStringNumberSense numberSense) {
        if (Mathf.Approximately(value, neutral)) return;

        sb.AppendLine($" - {labelKey.Translate()}: {value.ToStringByStyle(style, numberSense)}");
    }
}