using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace CWF;

public class AssemblyPresetData : IExposable {
    public string Name = string.Empty;
    public ThingDef? WeaponDef;
    public List<AssemblyPresetEntryData> Entries = [];

    public void ExposeData() {
        var name = Name;
        Scribe_Values.Look(ref name, "name");
        Name = name ?? string.Empty;
        Scribe_Defs.Look(ref WeaponDef, "weaponDef");
        Scribe_Collections.Look(ref Entries, "entries", LookMode.Deep);

        Entries ??= [];
    }
}

public class AssemblyPresetEntryData : IExposable {
    public PartDef? Part;
    public WeaponTraitDef? Trait;

    [UsedImplicitly]
    public AssemblyPresetEntryData() { }

    public AssemblyPresetEntryData(PartDef part, WeaponTraitDef trait) {
        Part = part;
        Trait = trait;
    }

    public void ExposeData() {
        Scribe_Defs.Look(ref Part, "part");
        Scribe_Defs.Look(ref Trait, "trait");
    }
}