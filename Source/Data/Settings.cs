using Verse;

namespace CWF;

public class Settings : ModSettings {
    private const bool DefaultDynamicTexturesEnabled = true;
    private const bool DefaultRandomModulesEnabled = true;
    private const int DefaultMinRandomModules = 0;
    private const int DefaultMaxRandomModules = 3;
    private const float DefaultStandardRarityWeight = 1f;
    private const float DefaultRareRarityWeight = 0.2f;
    private const float DefaultLegendaryRarityWeight = 0.01f;

    public bool DynamicTexturesEnabled = DefaultDynamicTexturesEnabled;
    public bool RandomModulesEnabled = DefaultRandomModulesEnabled;
    public int MinRandomModules = DefaultMinRandomModules;
    public int MaxRandomModules = DefaultMaxRandomModules;
    public float StandardRarityWeight = DefaultStandardRarityWeight;
    public float RareRarityWeight = DefaultRareRarityWeight;
    public float LegendaryRarityWeight = DefaultLegendaryRarityWeight;

    public static Settings Current => LoadedModManager.GetMod<ConfigWindow>().GetSettings<Settings>();

    public float GetRarityWeight(Rarity rarity) {
        return rarity switch {
            Rarity.Standard => StandardRarityWeight,
            Rarity.Rare => RareRarityWeight,
            Rarity.Legendary => LegendaryRarityWeight,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "[CWF] Unknown rarity value.")
        };
    }

    public void Reset() {
        DynamicTexturesEnabled = DefaultDynamicTexturesEnabled;
        RandomModulesEnabled = DefaultRandomModulesEnabled;
        MinRandomModules = DefaultMinRandomModules;
        MaxRandomModules = DefaultMaxRandomModules;
        StandardRarityWeight = DefaultStandardRarityWeight;
        RareRarityWeight = DefaultRareRarityWeight;
        LegendaryRarityWeight = DefaultLegendaryRarityWeight;
    }

    public override void ExposeData() {
        base.ExposeData();
        Scribe_Values.Look(ref DynamicTexturesEnabled, "dynamicTexturesEnabled", DefaultDynamicTexturesEnabled);
        Scribe_Values.Look(ref RandomModulesEnabled, "randomModulesEnabled", DefaultRandomModulesEnabled);
        Scribe_Values.Look(ref MinRandomModules, "minRandomModules", DefaultMinRandomModules);
        Scribe_Values.Look(ref MaxRandomModules, "maxRandomModules", DefaultMaxRandomModules);
        Scribe_Values.Look(ref StandardRarityWeight, "standardRarityWeight", DefaultStandardRarityWeight);
        Scribe_Values.Look(ref RareRarityWeight, "rareRarityWeight", DefaultRareRarityWeight);
        Scribe_Values.Look(ref LegendaryRarityWeight, "legendaryRarityWeight", DefaultLegendaryRarityWeight);
    }
}