namespace CWF;

public class ModificationData : IExposable {
    public ModificationType Type;
    public PartDef Part = null!;
    public WeaponTraitDef Trait = null!;
    public ThingDef ModuleDef = null!;

    public void ExposeData() {
        Scribe_Values.Look(ref Type, "type");
        Scribe_Defs.Look(ref Part, "part");
        Scribe_Defs.Look(ref Trait, "trait");
        Scribe_Defs.Look(ref ModuleDef, "moduleDef");
    }
}

public enum ModificationType {
    Install,
    Uninstall
}

internal static class ModificationOperations {
    internal static bool HasRequiredModules(Pawn pawn, List<ModificationData> modifications) {
        return modifications
            .Where(modification => modification.Type == ModificationType.Install)
            .All(modification => pawn.inventory.innerContainer.Any(thing => thing.def == modification.ModuleDef));
    }

    internal static void Apply(CompDynamicTraits comp, Pawn pawn, List<ModificationData> modifications,
        bool addUninstalledModulesToInventory) {
        foreach (var modification in
                 modifications.Where(modification => modification.Type == ModificationType.Uninstall)) {
            comp.UninstallTrait(modification.Part);
            var module = ThingMaker.MakeThing(modification.ModuleDef);
            if (!addUninstalledModulesToInventory || !pawn.inventory.innerContainer.TryAdd(module)) {
                GenPlace.TryPlaceThing(module, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            }
        }

        foreach (var modification in
                 modifications.Where(modification => modification.Type == ModificationType.Install)) {
            var module = pawn.inventory.innerContainer.FirstOrDefault(thing => thing.def == modification.ModuleDef);
            comp.InstallTrait(modification.Part, modification.Trait);
            module!.SplitOff(1).Destroy();
        }
    }
}