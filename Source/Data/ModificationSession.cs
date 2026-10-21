using CWF.Extensions;

namespace CWF;

public class ModificationSession {
    private readonly Thing _weapon;
    private Dictionary<PartDef, WeaponTraitDef> _desiredTraits;

    private Dictionary<PartDef, WeaponTraitDef> InitialTraits { get; }
    public Thing PreviewWeapon { get; }
    public IReadOnlyCollection<PartDef> AvailableParts { get; private set; } = [];

    public Dictionary<PartDef, WeaponTraitDef> InstalledTraits {
        get => new(_desiredTraits);
        set {
            _desiredTraits = new Dictionary<PartDef, WeaponTraitDef>(value);
            RefreshPreview();
        }
    }

    public IReadOnlyCollection<WeaponTraitDef> Traits => _desiredTraits.Values;

    public ModificationSession(Thing weapon) {
        _weapon = weapon;

        InitialTraits = weapon.TryGetComp<CompDynamicTraits>(out var compDynamicTraits)
            ? compDynamicTraits.InstalledTraits
            : [];

        _desiredTraits = new Dictionary<PartDef, WeaponTraitDef>(InitialTraits);
        PreviewWeapon = CreatePreviewWeapon();
        RefreshPreview();
    }

    public void InstallTrait(PartDef part, WeaponTraitDef traitDef) {
        _desiredTraits[part] = traitDef;
        RefreshPreview();
    }

    public void UninstallTrait(PartDef part) {
        if (_desiredTraits.Remove(part)) {
            RefreshPreview();
        }
    }

    public void ClearTraits() {
        if (_desiredTraits.Count == 0) return;

        _desiredTraits.Clear();
        RefreshPreview();
    }

    public WeaponTraitDef? GetInstalledTraitFor(PartDef part) {
        _desiredTraits.TryGetValue(part, out var traitDef);
        return traitDef;
    }

    public IEnumerable<WeaponTraitDef> GetReinstallableTraitsFor(PartDef part) => InitialTraits
        .Where(pair => pair.Key == part && !_desiredTraits.Values.Contains(pair.Value))
        .Select(pair => pair.Value);

    public List<ModificationData> CalculateNetChanges() {
        var changes = new List<ModificationData>();

        foreach (var part in InitialTraits.Keys.Union(_desiredTraits.Keys)) {
            InitialTraits.TryGetValue(part, out var initialTrait);
            _desiredTraits.TryGetValue(part, out var finalTrait);
            if (initialTrait == finalTrait) continue;

            if (initialTrait != null && initialTrait.TryGetModuleDef(out var uninstallModule)) {
                changes.Add(new ModificationData {
                    Type = ModificationType.Uninstall,
                    Part = part,
                    Trait = initialTrait,
                    ModuleDef = uninstallModule
                });
            }

            if (finalTrait != null && finalTrait.TryGetModuleDef(out var installModule)) {
                changes.Add(new ModificationData {
                    Type = ModificationType.Install,
                    Part = part,
                    Trait = finalTrait,
                    ModuleDef = installModule
                });
            }
        }

        return changes.OrderBy(change => change.Type).ToList();
    }

    public void DisposePreview() {
        if (!PreviewWeapon.Destroyed) {
            PreviewWeapon.Destroy();
        }
    }

    private Thing CreatePreviewWeapon() {
        var previewWeapon = ThingMaker.MakeThing(_weapon.def, _weapon.Stuff);
        previewWeapon.TryGetComp<CompDynamicGraphic>()?.UseFullResolutionBake = true;

        CopyComp<CompQuality>(_weapon, previewWeapon, (source, preview) => preview.SetQuality(source.Quality, null));
        CopyComp<CompRenamable>(_weapon, previewWeapon, (source, preview) => preview.Nickname = source.Nickname);
        CopyComp<CompColorable>(_weapon, previewWeapon, (source, preview) => preview.ColorDef = source.ColorDef);
        CopyComp<CompDynamicGraphic>(_weapon, previewWeapon,
            (source, preview) => preview.UseDynamicTextures = source.UseDynamicTextures);
        CopyComp<CompUniqueWeapon>(_weapon, previewWeapon, (source, preview) => {
            preview.TraitsListForReading.Clear();
            preview.TraitsListForReading.AddRange(source.TraitsListForReading);
            preview.Setup(true);
        });
        CopyStyle(_weapon, previewWeapon);

        return previewWeapon;
    }

    private void RefreshPreview() {
        AvailableParts = PartAvailabilityAnalyzer.Analyze(_weapon, _desiredTraits).AvailableParts;

        if (PreviewWeapon.TryGetComp<CompDynamicTraits>(out var previewDynamicTraits)) {
            previewDynamicTraits.InstalledTraits = _desiredTraits;
        }
    }

    private static void CopyComp<T>(Thing source, Thing preview, Action<T, T> copy) where T : ThingComp {
        if (source.TryGetComp<T>(out var sourceComp) && preview.TryGetComp<T>(out var previewComp)) {
            copy(sourceComp, previewComp);
        }
    }

    private static void CopyStyle(Thing source, Thing preview) {
        if (source.StyleDef == preview.StyleDef) return;

        preview.StyleDef = source.StyleDef;
    }
}