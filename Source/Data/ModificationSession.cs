using HarmonyLib;
using RimWorld;
using Verse;
using CWF.Extensions;

namespace CWF;

public class WeaponModificationSession {
    private static readonly AccessTools.FieldRef<CompColorable, ColorDef?> ColorableColorRef =
        AccessTools.FieldRefAccess<CompColorable, ColorDef?>("_colorDef");

    private static readonly AccessTools.FieldRef<CompUniqueWeapon, ColorDef> UniqueWeaponColorRef =
        AccessTools.FieldRefAccess<CompUniqueWeapon, ColorDef>("color");

    private static readonly AccessTools.FieldRef<CompUniqueWeapon, string> UniqueWeaponNameRef =
        AccessTools.FieldRefAccess<CompUniqueWeapon, string>("name");

    private readonly Thing _weapon;
    private Dictionary<PartDef, WeaponTraitDef> _desiredTraits;

    public WeaponModificationSession(Thing weapon) {
        _weapon = weapon;

        InitialTraits = weapon.TryGetComp<CompDynamicTraits>(out var compDynamicTraits)
            ? compDynamicTraits.InstalledTraits
            : [];

        _desiredTraits = new Dictionary<PartDef, WeaponTraitDef>(InitialTraits);
        PreviewWeapon = CreatePreviewWeapon();
        RefreshPreview();
    }

    private Dictionary<PartDef, WeaponTraitDef> InitialTraits { get; }

    public Thing PreviewWeapon { get; }

    public IReadOnlyCollection<WeaponTraitDef> Traits => _desiredTraits.Values;

    public IReadOnlyCollection<PartDef> AvailableParts { get; private set; } = [];

    public Dictionary<PartDef, WeaponTraitDef> InstalledTraits {
        get => new(_desiredTraits);
        set {
            _desiredTraits = new Dictionary<PartDef, WeaponTraitDef>(value);
            RefreshPreview();
        }
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

    public IEnumerable<WeaponTraitDef> GetReinstallableTraitsFor(PartDef part) {
        return InitialTraits
            .Where(pair => pair.Key == part && !_desiredTraits.Values.Contains(pair.Value))
            .Select(pair => pair.Value);
    }

    public List<ModificationData> CalculateNetChanges() {
        var changes = new List<ModificationData>();

        foreach (var part in DefDatabase<PartDef>.AllDefs) {
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

        CopyQuality(_weapon, previewWeapon);
        CopyRenamable(_weapon, previewWeapon);
        CopyColorable(_weapon, previewWeapon);
        CopyUniqueWeapon(_weapon, previewWeapon);

        return previewWeapon;
    }

    private void RefreshPreview() {
        AvailableParts = PartAvailabilityAnalyzer.Analyze(_weapon, _desiredTraits).AvailableParts;

        if (PreviewWeapon.TryGetComp<CompDynamicTraits>(out var previewDynamicTraits)) {
            previewDynamicTraits.InstalledTraits = _desiredTraits;
        }
    }

    private static void CopyQuality(Thing source, Thing preview) {
        if (source.TryGetComp<CompQuality>(out var sourceQuality) &&
            preview.TryGetComp<CompQuality>(out var previewQuality)) {
            previewQuality.SetQuality(sourceQuality.Quality, null);
        }
    }

    private static void CopyRenamable(Thing source, Thing preview) {
        if (source.TryGetComp<CompRenamable>(out var sourceRenamable) &&
            preview.TryGetComp<CompRenamable>(out var previewRenamable)) {
            previewRenamable.Nickname = sourceRenamable.Nickname;
        }
    }

    private static void CopyColorable(Thing source, Thing preview) {
        if (source.TryGetComp<CompColorable>(out var sourceColorable) &&
            preview.TryGetComp<CompColorable>(out var previewColorable)) {
            ColorableColorRef(previewColorable) = sourceColorable.ColorDef;
            preview.Notify_ColorChanged();
        }
    }

    private static void CopyUniqueWeapon(Thing source, Thing preview) {
        if (!source.TryGetComp<CompUniqueWeapon>(out var sourceUniqueWeapon) ||
            !preview.TryGetComp<CompUniqueWeapon>(out var previewUniqueWeapon)) {
            return;
        }

        previewUniqueWeapon.TraitsListForReading.Clear();
        previewUniqueWeapon.TraitsListForReading.AddRange(sourceUniqueWeapon.TraitsListForReading);
        UniqueWeaponColorRef(previewUniqueWeapon) = UniqueWeaponColorRef(sourceUniqueWeapon);
        UniqueWeaponNameRef(previewUniqueWeapon) = UniqueWeaponNameRef(sourceUniqueWeapon);
        previewUniqueWeapon.Setup(true);
    }
}