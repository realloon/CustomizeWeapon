using JetBrains.Annotations;
using UnityEngine;
using Verse;

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

    public override string SettingsCategory() => "Customize Weapon";

    public override void DoSettingsWindowContents(Rect inRect) {
        var listing = new Listing_Standard();
        listing.Begin(inRect);

        listing.CheckboxLabeled("CWF_DynamicTextures".Translate(), ref _settings.DynamicTexturesEnabled,
            "CWF_DynamicTexturesDesc".Translate());

        listing.Gap();
        listing.CheckboxLabeled("CWF_RandomModuleGeneration".Translate(), ref _settings.RandomModulesEnabled,
            "CWF_RandomModuleGenerationDesc".Translate());

        if (_settings.RandomModulesEnabled) {
            var range = new IntRange(_settings.MinRandomModules, _settings.MaxRandomModules);
            listing.IntRange(ref range, 0, 10);

            _settings.MinRandomModules = range.min;
            _settings.MaxRandomModules = range.max;

            listing.Gap();
            listing.Label("CWF_RarityWeights".Translate());

            var descRect = listing.GetRect(Text.LineHeight);
            UIKit.WithStyle(
                () => Widgets.Label(descRect, "CWF_RarityWeightsDesc".Translate()),
                GameFont.Tiny,
                Color.gray
            );

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
        _standardWeightBuffer = _settings.StandardRarityWeight.ToString("0.##########");
        _rareWeightBuffer = _settings.RareRarityWeight.ToString("0.##########");
        _legendaryWeightBuffer = _settings.LegendaryRarityWeight.ToString("0.##########");
    }

    private static void DrawRarityWeightRow(Listing_Standard listing, string label, ref float value,
        ref string buffer) {
        const float rowHeight = 28f;
        const float fieldWidth = 120f;
        const float gap = 12f;

        var rowRect = listing.GetRect(rowHeight);
        var labelRect = new Rect(rowRect.x, rowRect.y, rowRect.width - fieldWidth - gap, rowHeight);
        var fieldRect = new Rect(rowRect.xMax - fieldWidth, rowRect.y, fieldWidth, rowHeight);

        UIKit.WithStyle(() => Widgets.Label(labelRect, label), anchor: TextAnchor.MiddleLeft);
        Widgets.TextFieldNumeric(fieldRect, ref value, ref buffer);
    }
}