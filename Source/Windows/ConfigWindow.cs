using System.Globalization;
using JetBrains.Annotations;
using UnityEngine;

namespace CWF;

[UsedImplicitly]
public class ConfigWindow : Mod {
    private readonly Settings _settings;
    private string _standardWeightBuffer = string.Empty;
    private string _rareWeightBuffer = string.Empty;
    private string _legendaryWeightBuffer = string.Empty;

    public ConfigWindow(ModContentPack content) : base(content) {
        _settings = GetSettings<Settings>();
        RefreshWeightBuffers();
    }

    public override string SettingsCategory() => "CWF_SettingsCategory".Translate();

    public override void DoSettingsWindowContents(Rect inRect) {
        var listing = new Listing_Standard();
        listing.Begin(inRect);

        listing.CheckboxLabeled("CWF_RandomModuleGeneration".Translate(), ref _settings.RandomModulesEnabled,
            "CWF_RandomModuleGenerationDesc".Translate());

        if (_settings.RandomModulesEnabled) {
            listing.Label("CWF_RandomModuleLimit".Translate(_settings.MaxRandomModules));
            var countSliderRect = listing.GetRect(22f);
            _settings.MaxRandomModules = Mathf.RoundToInt(
                Widgets.HorizontalSlider(countSliderRect, _settings.MaxRandomModules, 0f, 10f, roundTo: 1f));

            listing.Label("CWF_RarityWeights".Translate());
            var descRect = listing.GetRect(Text.LineHeight);
            using (UIKit.Style(GameFont.Tiny, Color.gray)) {
                Widgets.Label(descRect, "CWF_RarityWeightsDesc".Translate());
            }

            DrawRarityWeightRow(listing, "CWF_RarityWeight_Standard".Translate(), ref _settings.StandardRarityWeight,
                ref _standardWeightBuffer);
            DrawRarityWeightRow(listing, "CWF_RarityWeight_Rare".Translate(), ref _settings.RareRarityWeight,
                ref _rareWeightBuffer);
            DrawRarityWeightRow(listing, "CWF_RarityWeight_Legendary".Translate(),
                ref _settings.LegendaryRarityWeight, ref _legendaryWeightBuffer);
        }

        listing.Gap(24f);

        var resetLabel = "Reset".Translate();
        var resetRowRect = listing.GetRect(30f);
        var resetButtonWidth = Mathf.Min(Text.CalcSize(resetLabel).x + 32f, resetRowRect.width);
        var resetButtonRect = new Rect(resetRowRect.x, resetRowRect.y, resetButtonWidth, 30f);

        if (Widgets.ButtonText(resetButtonRect, resetLabel)) {
            _settings.Reset();
            RefreshWeightBuffers();
        }

        listing.Gap(listing.verticalSpacing);

        listing.End();
        base.DoSettingsWindowContents(inRect);
    }

    private void RefreshWeightBuffers() {
        _standardWeightBuffer = FormatWeight(_settings.StandardRarityWeight);
        _rareWeightBuffer = FormatWeight(_settings.RareRarityWeight);
        _legendaryWeightBuffer = FormatWeight(_settings.LegendaryRarityWeight);
    }

    private static string FormatWeight(float weight) => weight.ToString("0.##########", CultureInfo.InvariantCulture);

    private static void DrawRarityWeightRow(Listing_Standard listing, string label, ref float value,
        ref string buffer) {
        const float rowHeight = 28f;
        const float fieldWidth = 120f;
        const float gap = 12f;

        var rowRect = listing.GetRect(rowHeight);
        var labelRect = new Rect(rowRect.x, rowRect.y, rowRect.width - fieldWidth - gap, rowHeight);
        var fieldRect = new Rect(rowRect.xMax - fieldWidth, rowRect.y, fieldWidth, rowHeight);

        using (UIKit.Style(anchor: TextAnchor.MiddleLeft)) {
            Widgets.Label(labelRect, label);
        }

        Widgets.TextFieldNumeric(fieldRect, ref value, ref buffer);
    }
}