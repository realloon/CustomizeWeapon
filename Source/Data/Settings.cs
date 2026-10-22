namespace CWF;

public class Settings : ModSettings {
    private const bool DefaultRandomModulesEnabled = true;
    private const int DefaultMaxRandomModules = 3;
    private const float DefaultStandardRarityWeight = 1f;
    private const float DefaultRareRarityWeight = 0.2f;
    private const float DefaultLegendaryRarityWeight = 0.01f;

    public bool RandomModulesEnabled = DefaultRandomModulesEnabled;
    public int MaxRandomModules = DefaultMaxRandomModules;
    public float StandardRarityWeight = DefaultStandardRarityWeight;
    public float RareRarityWeight = DefaultRareRarityWeight;
    public float LegendaryRarityWeight = DefaultLegendaryRarityWeight;

    public static Settings Current => LoadedModManager.GetMod<ConfigWindow>().GetSettings<Settings>();

    public float GetRarityWeight(Rarity rarity) => rarity switch {
        Rarity.Standard => StandardRarityWeight,
        Rarity.Rare => RareRarityWeight,
        Rarity.Legendary => LegendaryRarityWeight,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "[CWF] Unknown rarity value.")
    };

    public void Reset() {
        RandomModulesEnabled = DefaultRandomModulesEnabled;
        MaxRandomModules = DefaultMaxRandomModules;
        StandardRarityWeight = DefaultStandardRarityWeight;
        RareRarityWeight = DefaultRareRarityWeight;
        LegendaryRarityWeight = DefaultLegendaryRarityWeight;
    }

    public override void ExposeData() {
        base.ExposeData();
        Scribe_Values.Look(ref RandomModulesEnabled, "randomModulesEnabled", DefaultRandomModulesEnabled);
        Scribe_Values.Look(ref MaxRandomModules, "maxRandomModules", DefaultMaxRandomModules);
        Scribe_Values.Look(ref StandardRarityWeight, "standardRarityWeight", DefaultStandardRarityWeight);
        Scribe_Values.Look(ref RareRarityWeight, "rareRarityWeight", DefaultRareRarityWeight);
        Scribe_Values.Look(ref LegendaryRarityWeight, "legendaryRarityWeight", DefaultLegendaryRarityWeight);
    }
}