using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace CWF;

[UsedImplicitly]
public class AssemblyPresetManager : GameComponent {
    private List<AssemblyPresetData> _presets = [];

    public AssemblyPresetManager(Game game) { }

    public IEnumerable<AssemblyPresetData> GetPresetsFor(ThingDef weaponDef) {
        return _presets
            .Where(preset => preset.WeaponDef == weaponDef)
            .OrderBy(preset => preset.Name);
    }

    public void SavePreset(Thing weapon, string name, IReadOnlyDictionary<PartDef, WeaponTraitDef> traits) {
        var normalizedName = name.Trim();
        var existingPreset = FindPreset(weapon.def, normalizedName);

        var preset = existingPreset ?? new AssemblyPresetData();
        preset.Name = normalizedName;
        preset.WeaponDef = weapon.def;
        preset.Entries = traits
            .OrderBy(pair => pair.Key.order)
            .ThenBy(pair => pair.Key.defName)
            .Select(pair => new AssemblyPresetEntryData(pair.Key, pair.Value))
            .ToList();

        if (existingPreset == null) {
            _presets.Add(preset);
        }
    }

    public bool DeletePreset(ThingDef weaponDef, string name) {
        var preset = FindPreset(weaponDef, name);
        return preset != null && _presets.Remove(preset);
    }

    private AssemblyPresetData? FindPreset(ThingDef weaponDef, string name) {
        return _presets.FirstOrDefault(preset =>
            preset.WeaponDef == weaponDef &&
            string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public override void ExposeData() {
        Scribe_Collections.Look(ref _presets, "assemblyPresets", LookMode.Deep);
        _presets ??= [];
    }
}