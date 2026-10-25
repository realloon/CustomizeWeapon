using System.Text;
using UnityEngine;

namespace CWF.ViewDrawers;

public class AsideDrawer(SpecDatabase specDatabase) {
    private const float StatRowHeight = 22f;
    private const float StatRowPaddingInline = 2f;
    private const float SectionGapHeight = 12f;
    private const float SectionHeaderHeight = 24f;

    private Vector2 _scrollPosition = Vector2.zero;

    private sealed record StatRow(
        string Label,
        string Format,
        string Unit,
        Func<SpecDatabase, Spec> Value,
        StatDef? Stat = null,
        Func<Thing, float?>? Base = null,
        Func<WeaponTraitDef, string?>? TraitLine = null) {
        public string Display(float value) => (Unit == "%" ? value * 100f : value).ToString(Format) + Unit;
    }

    private sealed record StatSection(string? Header, StatRow[] Rows);

    private static readonly StatSection[] Layout = [
        new(null, [
            new StatRow("Range".Translate(), "N0", "", s => s.Range, StatDefOf.RangedWeapon_RangeMultiplier),
            new StatRow("CWF_RangeDPS".Translate(), "N0", "", s => s.Dps),
            new StatRow("CWF_RangeBurstCount".Translate(), "N0", "", s => s.BurstShotCount,
                TraitLine: t => FormatIfNot(t.burstShotCountMultiplier, 1f, ToStringStyle.PercentZero)),
            new StatRow("RangedWarmupTime".Translate(), "F1", " s", s => s.WarmupTime,
                StatDefOf.RangedWeapon_WarmupMultiplier),
            OwnStatRow("CWF_RangeCooldown".Translate(), "F1", " s", s => s.Cooldown, StatDefOf.RangedWeapon_Cooldown)
        ]),
        new("CWF_Projectile".Translate(), [
            new StatRow("Damage".Translate(), "N0", "", s => s.Damage, StatDefOf.RangedWeapon_DamageMultiplier,
                w => Projectile(w) is { damageDef: not null } p ? p.GetDamageAmount(null) : null),
            new StatRow("ArmorPenetration".Translate(), "N0", "%", s => s.ArmorPenetration,
                StatDefOf.RangedWeapon_ArmorPenetrationMultiplier,
                w => Projectile(w) is { damageDef.armorCategory: not null } p ? p.GetArmorPenetration() : null),
            new StatRow("StoppingPower".Translate(), "F1", "", s => s.StoppingPower,
                TraitLine: t => FormatIfNot(t.additionalStoppingPower, 0f, ToStringStyle.FloatOne,
                    ToStringNumberSense.Offset))
        ]),
        new("CWF_Accuracy".Translate(), [
            OwnStatRow("CWF_Touch".Translate(), "N0", "%", s => s.AccuracyTouch, StatDefOf.AccuracyTouch),
            OwnStatRow("CWF_Short".Translate(), "N0", "%", s => s.AccuracyShort, StatDefOf.AccuracyShort),
            OwnStatRow("CWF_Medium".Translate(), "N0", "%", s => s.AccuracyMedium, StatDefOf.AccuracyMedium),
            OwnStatRow("CWF_Long".Translate(), "N0", "%", s => s.AccuracyLong, StatDefOf.AccuracyLong)
        ]),
        new(null, [
            new StatRow(StatDefOf.Mass.LabelCap, "F1", "Kg", s => s.Mass),
            new StatRow("MarketValueTip".Translate(), "N0", "", s => s.MarketValue)
        ])
    ];

    private static readonly float ContentHeight =
        Layout.Sum(section => section.Rows.Length) * StatRowHeight
        + (Layout.Length - 1) * SectionGapHeight
        + Layout.Count(section => section.Header != null) * SectionHeaderHeight;

    public void Draw(in Rect rect) {
        var contentRect = new Rect(0f, 0f, rect.width, Mathf.Max(rect.height, ContentHeight));
        Widgets.BeginScrollView(rect, ref _scrollPosition, contentRect, showScrollbars: false);

        var listing = new Listing_Standard();
        listing.Begin(contentRect);

        for (var i = 0; i < Layout.Length; i++) {
            if (i > 0) listing.GapLine();
            if (Layout[i].Header is { } header) DrawSectionHeader(listing, header);

            foreach (var row in Layout[i].Rows) {
                DrawStatRow(listing, row);
            }
        }

        listing.End();
        Widgets.EndScrollView();
    }

    private void DrawStatRow(Listing_Standard listing, StatRow row) {
        var value = row.Value(specDatabase);
        var rect = listing.GetRect(StatRowHeight);
        var inRect = rect.ContractedBy(StatRowPaddingInline, 0f);
        var delta = value.IsLowerValueBetter ? value.Raw - value.Dynamic : value.Dynamic - value.Raw;
        var color = delta switch {
            > 0f => new Color(0.15f, 0.85f, 0.15f),
            < 0f => new Color(0.87f, 0.49f, 0.51f),
            _ => Color.white
        };

        Widgets.DrawHighlightIfMouseover(rect);
        UIKit.WithStyle(() => Widgets.Label(inRect, row.Label), anchor: TextAnchor.MiddleLeft);
        UIKit.WithStyle(() => Widgets.Label(inRect, row.Display(value.Dynamic)), color: color,
            anchor: TextAnchor.MiddleRight);

        if (delta != 0f && Event.current.type == EventType.Repaint && Mouse.IsOver(rect)) {
            TooltipHandler.TipRegion(rect, BuildTooltip(row, value));
        }
    }

    private static void DrawSectionHeader(Listing_Standard listing, string label) {
        var height = Mathf.Max(SectionHeaderHeight, Text.CalcHeight(label, listing.ColumnWidth));
        var labelRect = listing.GetRect(height).ContractedBy(StatRowPaddingInline, 0f);
        UIKit.WithStyle(() => Widgets.Label(labelRect, $"<color=#999999><b>{label}</b></color>"),
            anchor: TextAnchor.MiddleLeft);
    }

    #region Tooltip

    private static StatRow OwnStatRow(string label, string format, string unit, Func<SpecDatabase, Spec> value,
        StatDef stat) => new(label, format, unit, value, stat, w => stat.Worker.GetBaseValueFor(StatRequest.For(w)));

    private static ProjectileProperties? Projectile(Thing weapon) => weapon.def.Verbs
        .First().defaultProjectile?.projectile;

    private static string? FormatIfNot(float value, float neutral, ToStringStyle style,
        ToStringNumberSense sense = ToStringNumberSense.Factor) =>
        Mathf.Approximately(value, neutral) ? null : value.ToStringByStyle(style, sense);

    private string BuildTooltip(StatRow row, Spec value) {
        var weapon = specDatabase.PreviewWeapon;
        if ((row.Base == null ? value.Raw : row.Base(weapon)) is not { } baseValue) {
            return row.Display(value.Dynamic);
        }

        var delta = value.Dynamic - baseValue;
        var sb = new StringBuilder();
        sb.AppendLine($"{row.Display(value.Dynamic)} ({(delta > 0f ? "+" : "")}{row.Display(delta)})");

        if (row.Stat != null) AppendStatModifiers(sb, row.Stat, weapon);

        if (row.TraitLine != null) {
            foreach (var trait in specDatabase.AllTraits) {
                AppendLine(sb, trait.LabelCap, row.TraitLine(trait));
            }
        }

        return sb.ToString();
    }

    private void AppendStatModifiers(StringBuilder sb, StatDef stat, Thing weapon) {
        // Material
        if (weapon.Stuff is { stuffProps: { } stuffProps } stuff) {
            var label = $"{"StatsReport_Material".Translate()} ({stuff.LabelCap})";
            AppendLine(sb, label, FormatIfNot(stuffProps.statFactors.GetStatFactorFromList(stat), 1f,
                ToStringStyle.PercentZero));
            AppendLine(sb, label, StatText(stuffProps.statOffsets.GetStatOffsetFromList(stat), 0f,
                ToStringNumberSense.Offset));
        }

        // Trait offsets
        foreach (var trait in specDatabase.AllTraits) {
            AppendLine(sb, trait.LabelCap, StatText(trait.statOffsets.GetStatOffsetFromList(stat), 0f,
                ToStringNumberSense.Offset));
        }

        // Quality (first line of each effective quality part)
        if (weapon.TryGetQuality(out _) && stat.parts != null) {
            var req = StatRequest.For(weapon);
            foreach (var part in stat.parts) {
                if (part is not (StatPart_Quality or StatPart_Quality_Offset)) continue;

                var neutral = part is StatPart_Quality ? 1f : 0f;
                var value = neutral;
                part.TransformValue(req, ref value);
                if (Mathf.Approximately(value, neutral)) continue;

                var text = part.ExplanationPart(req)?.Trim();
                if (!string.IsNullOrEmpty(text)) sb.AppendLine(text.Split('\n')[0].TrimEnd('\r'));
            }
        }

        // Trait factors
        foreach (var trait in specDatabase.AllTraits) {
            AppendLine(sb, trait.LabelCap, StatText(trait.statFactors.GetStatFactorFromList(stat), 1f,
                ToStringNumberSense.Factor));
        }

        return;

        string? StatText(float value, float neutral, ToStringNumberSense sense) =>
            Mathf.Approximately(value, neutral) ? null : stat.ValueToString(value, sense, false);
    }

    private static void AppendLine(StringBuilder sb, string label, string? valueText) {
        if (valueText != null) sb.AppendLine($"{label}: {valueText}");
    }

    #endregion
}