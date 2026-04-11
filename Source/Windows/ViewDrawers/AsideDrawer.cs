using UnityEngine;
using Verse;

namespace CWF.ViewDrawers;

public class AsideDrawer(SpecDatabase specDatabase) {
    private Vector2 _scrollPosition = Vector2.zero;

    private const float StatRowHeight = 22f;
    private const float SectionGapHeight = 12f;
    private const float SectionHeaderHeight = 24f;
    private const float MeleeLabelHeight = 24f;

    public void Draw(in Rect rect) {
        var contentRect = new Rect(0f, 0f, rect.width, Mathf.Max(rect.height, EstimateContentHeight()));
        Widgets.BeginScrollView(rect, ref _scrollPosition, contentRect, showScrollbars: false);

        var listing = new Listing_Standard();
        listing.Begin(contentRect);

        if (specDatabase.IsMeleeWeapon) {
            listing.Label("CWF_NotRanged".Translate());
            listing.End();
            Widgets.EndScrollView();
            return;
        }

        // === Weapon Stats ===
        DrawStatRow(listing, "CWF_Range".Translate(), specDatabase.Range);
        DrawStatRow(listing, "CWF_RangeDPS".Translate(), specDatabase.Dps);
        DrawStatRow(listing, "CWF_RangeBurstCount".Translate(), specDatabase.BurstShotCount);
        DrawStatRow(listing, "CWF_RangeWarmupTime".Translate(), specDatabase.WarmupTime, "F1", " s");
        DrawStatRow(listing, "CWF_RangeCooldown".Translate(), specDatabase.Cooldown, "F1", " s");

        // === Projectile Stats ===
        listing.GapLine();
        listing.Label($"<color=#999999><b>{"CWF_Projectile".Translate()}</b></color>", 22f);
        DrawStatRow(listing, "CWF_Damage".Translate(), specDatabase.Damage);
        DrawStatRow(listing, "CWF_ArmorPenetration".Translate(), specDatabase.ArmorPenetration, unit: "%");
        DrawStatRow(listing, "CWF_StoppingPower".Translate(), specDatabase.StoppingPower, "F1");

        // === Accuracy Stats ===
        listing.GapLine();
        listing.Label($"<color=#999999><b>{"CWF_Accuracy".Translate()}</b></color>", 22f);
        DrawStatRow(listing, "CWF_Touch".Translate(), specDatabase.AccuracyTouch, unit: "%");
        DrawStatRow(listing, "CWF_Short".Translate(), specDatabase.AccuracyShort, unit: "%");
        DrawStatRow(listing, "CWF_Medium".Translate(), specDatabase.AccuracyMedium, unit: "%");
        DrawStatRow(listing, "CWF_Long".Translate(), specDatabase.AccuracyLong, unit: "%");

        // === Other Stats ===
        listing.GapLine();
        DrawStatRow(listing, "Mass".Translate(), specDatabase.Mass, "F1", "Kg");
        DrawStatRow(listing, "MarketValueTip".Translate(), specDatabase.MarketValue);

        listing.End();
        Widgets.EndScrollView();
    }

    // helper
    private float EstimateContentHeight() {
        if (specDatabase.IsMeleeWeapon) {
            return MeleeLabelHeight;
        }

        const int totalStatRowCount = 14;
        const int sectionGapCount = 3;
        const int sectionHeaderCount = 2;

        return totalStatRowCount * StatRowHeight
               + sectionGapCount * SectionGapHeight
               + sectionHeaderCount * SectionHeaderHeight;
    }

    private static void DrawStatRow(
        Listing_Standard listing, string label, Spec spec,
        string format = "N0", string unit = "") {
        var value = unit == "%" ? spec.Dynamic * 100 : spec.Dynamic;
        var valueString = value.ToString(format) + unit;
        var delta = spec.IsLowerValueBetter ? spec.Raw - spec.Dynamic : spec.Dynamic - spec.Raw;

        DrawLabelRow(listing.GetRect(22), label, valueString, delta);
    }

    private static void DrawLabelRow(in Rect rect, string label, string value, float deltaValue = 0f) {
        var inRect = rect;

        // Label
        UIKit.WithStyle(() => Widgets.Label(inRect, label), anchor: TextAnchor.MiddleLeft);

        // Value with color
        var color = deltaValue switch {
            > 0f => new Color(0.15f, 0.85f, 0.15f),
            < 0f => new Color(0.87f, 0.49f, 0.51f),
            _ => Color.white
        };

        UIKit.WithStyle(() => Widgets.Label(inRect, value), color: color, anchor: TextAnchor.MiddleRight);
    }
}