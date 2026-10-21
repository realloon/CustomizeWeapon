using Verse.Sound;
using CWF.Extensions;

namespace CWF.Controllers;

public class InteractionController(Thing weapon, ModificationSession session, Action onDataChanged) {
    private readonly AssemblyPresetManager? _presetManager = Current.Game?.GetComponent<AssemblyPresetManager>();
    private readonly CompDynamicGraphic? _dynamicGraphic = weapon.TryGetComp<CompDynamicGraphic>();
    private readonly CompColorable? _colorable = weapon.TryGetComp<CompColorable>();

    private readonly List<StyleCategoryDef> _styleCategories = weapon.def.CanBeStyled()
        ? [
            .. DefDatabase<StyleCategoryDef>.AllDefs.Where(styleCategory =>
                styleCategory.GetStyleForThingDef(weapon.def)?.Graphic != null)
        ]
        : [];

    public bool HasInstalledModules => session.Traits.Count != 0;

    public bool HasAppearanceOptions =>
        _dynamicGraphic != null || weapon.StyleDef != null || _styleCategories.Count > 0;

    public bool HasDyeOptions => _colorable != null && _dynamicGraphic is { UsesDynamicComposite: true };

    public IEnumerable<AssemblyPresetData> GetApplicablePresets() => _presetManager?.GetPresetsFor(weapon.def) ?? [];

    public void OpenColorMenu() {
        if (!HasDyeOptions) return;

        var options = CompColorable.AvailableColors
            .Select(color => new FloatMenuOption(color.LabelCap, () => ApplyColor(color),
                UIKit.GetColorSwatch(color.color), color.color))
            .ToList();
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private void ApplyColor(ColorDef colorDef) {
        if (!HasDyeOptions) return;

        _colorable!.ColorDef = colorDef;

        if (session.PreviewWeapon.TryGetComp<CompColorable>(out var previewColorable)) {
            previewColorable.ColorDef = colorDef;
        }

        onDataChanged();
    }

    public void OpenAppearanceMenu() {
        var currentStyle = weapon.StyleDef;
        var isDynamic = currentStyle == null && _dynamicGraphic is { UsesDynamicComposite: true };

        var options = new List<FloatMenuOption> {
            new(FormatMenuLabel("CWF_AppearanceOriginal".Translate(), currentStyle == null && !isDynamic),
                () => ApplyAppearance(null, false))
        };

        if (_dynamicGraphic != null) {
            options.Add(new FloatMenuOption(
                FormatMenuLabel("CWF_AppearanceDynamic".Translate(), isDynamic),
                () => ApplyAppearance(null, true)));
        }

        foreach (var styleCategory in _styleCategories) {
            var styleDef = styleCategory.GetStyleForThingDef(weapon.def);
            if (styleDef == null) continue;

            var label = FormatMenuLabel(styleCategory.LabelCap, styleDef == currentStyle);
            options.Add(new FloatMenuOption(label, () => ApplyAppearance(styleDef, true)));
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static string FormatMenuLabel(string label, bool selected) => (selected ? "✓ " : string.Empty) + label;

    private void ApplyAppearance(ThingStyleDef? styleDef, bool useDynamicTextures) {
        var changed = false;

        if (weapon.StyleDef != styleDef) {
            weapon.StyleDef = styleDef;
            changed = true;
        }

        if (session.PreviewWeapon.StyleDef != styleDef) {
            session.PreviewWeapon.StyleDef = styleDef;
        }

        if (_dynamicGraphic != null && _dynamicGraphic.UseDynamicTextures != useDynamicTextures) {
            _dynamicGraphic.UseDynamicTextures = useDynamicTextures;
            changed = true;
        }

        if (session.PreviewWeapon.TryGetComp<CompDynamicGraphic>(out var previewDynamicGraphic) &&
            previewDynamicGraphic.UseDynamicTextures != useDynamicTextures) {
            previewDynamicGraphic.UseDynamicTextures = useDynamicTextures;
        }

        if (!changed) return;

        CompDynamicGraphic.Notify_ThingGraphicDirty(weapon);
        SoundDefOf.Tick_High.PlayOneShotOnCamera();
        onDataChanged();
    }

    /// <summary>
    /// Opens a float-menu for the clicked slot.  
    /// If installedTrait is null, lists traits to install; otherwise offers to uninstall the installed trait.
    /// </summary>
    public void HandleSlotClick(PartDef part, WeaponTraitDef? installedTrait) {
        var options = new List<FloatMenuOption>();

        if (installedTrait == null) {
            BuildInstallOptions(part, options);
        } else {
            BuildUninstallOption(part, options, installedTrait);
        }

        if (options.Count != 0) {
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    private void BuildInstallOptions(PartDef part, List<FloatMenuOption> options) {
        var installCandidates = new HashSet<WeaponTraitDef>();

        var compatibleModuleDefs = new HashSet<ThingDef>(GetCompatibleModuleDefsFor(part));

        var ownerPawn = weapon.ParentHolder switch {
            Pawn_EquipmentTracker equipment => equipment.pawn,
            Pawn_InventoryTracker inventory => inventory.pawn,
            _ => null
        };

        // from inventory or map
        if (compatibleModuleDefs.Any()) {
            var searchScope = ownerPawn != null
                ? ownerPawn.inventory.innerContainer
                : weapon.Map?.listerThings.AllThings ?? Enumerable.Empty<Thing>();

            var availableModules = searchScope.Where(t =>
                compatibleModuleDefs.Contains(t.def) && (ownerPawn != null || !t.IsForbidden(Faction.OfPlayer))
            );

            foreach (var module in availableModules) {
                var trait = module.def.TraitModule().weaponTraitDef;
                installCandidates.Add(trait);
            }
        }

        // from stack
        var stagedCompatibleTraits = session.GetReinstallableTraitsFor(part);
        foreach (var trait in stagedCompatibleTraits) {
            if (trait.TryGetModuleDef(out _)) {
                installCandidates.Add(trait);
            }
        }

        if (!installCandidates.Any()) {
            options.Add(new FloatMenuOption(weapon.ParentHolder is Pawn_EquipmentTracker
                ? "CWF_NoCompatibleModulesInInventory".Translate()
                : "CWF_NoCompatibleModulesOnMap".Translate(), null));
            return;
        }

        foreach (var traitToInstall in installCandidates) {
            var installAction = CreateInstallAction(part, traitToInstall);
            options.Add(new FloatMenuOption(traitToInstall.LabelCap, installAction));
        }
    }

    private void BuildUninstallOption(PartDef part, List<FloatMenuOption> options, WeaponTraitDef installedTrait) {
        options.Add(new FloatMenuOption("CWF_Uninstall".Translate(installedTrait.LabelCap), () => {
            var analysis = AnalyzeUninstallConflict(part);
            if (!analysis.HasConflict) {
                DoUninstall(part);
            } else {
                ShowConfirmationDialog(
                    "CWF_ConfirmUninstallTitle".Translate(),
                    "CWF_ConfirmUninstallBody".Translate(
                        installedTrait.LabelCap.Named("MODULE"),
                        analysis.ModulesToRemove
                            .Select(t => " - " + t.LabelCap.ToString()).ToLineList()
                            .Named("DEPENDENCIES")
                    ),
                    () => {
                        foreach (var dependencyTrait in analysis.ModulesToRemove) {
                            if (dependencyTrait.TryGetPart(out var dependencyPart)) {
                                DoUninstall(dependencyPart);
                            }
                        }

                        DoUninstall(part);
                    }
                );
            }
        }));
    }

    private void DoInstall(PartDef part, WeaponTraitDef traitToInstall) {
        session.InstallTrait(part, traitToInstall);
        SoundDefOf.Tick_High.PlayOneShotOnCamera();
        onDataChanged();
    }

    private void DoUninstall(PartDef part) {
        session.UninstallTrait(part);
        SoundDefOf.Tick_High.PlayOneShotOnCamera();
        onDataChanged();
    }

    public void ClearAllModules() {
        session.ClearTraits();
        SoundDefOf.Click.PlayOneShotOnCamera();
        onDataChanged();
    }

    public void SaveCurrentPreset(string name) {
        var normalizedName = name.Trim();
        if (string.IsNullOrEmpty(normalizedName)) {
            Messages.Message("CWF_PresetNameEmpty".Translate(), MessageTypeDefOf.RejectInput, false);
            return;
        }

        if (_presetManager == null) {
            Messages.Message("CWF_PresetManagerUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
            return;
        }

        _presetManager.SavePreset(weapon, normalizedName, session.InstalledTraits);
        Messages.Message("CWF_PresetSaved".Translate(normalizedName.Named("NAME")),
            MessageTypeDefOf.PositiveEvent, false);
    }

    public void DeletePreset(AssemblyPresetData preset) {
        if (_presetManager == null) {
            Messages.Message("CWF_PresetManagerUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
            return;
        }

        var deleted = _presetManager.DeletePreset(weapon.def, preset.Name);
        if (!deleted) {
            Messages.Message("CWF_PresetDeleteFailed".Translate(preset.Name.Named("NAME")),
                MessageTypeDefOf.RejectInput, false);
            return;
        }

        Messages.Message("CWF_PresetDeleted".Translate(preset.Name.Named("NAME")),
            MessageTypeDefOf.PositiveEvent, false);
    }

    public void ApplyPreset(AssemblyPresetData preset) {
        var desiredTraits = new Dictionary<PartDef, WeaponTraitDef>();
        var missingDefsCount = 0;

        foreach (var entry in preset.Entries) {
            if (entry.Part == null || entry.Trait == null) {
                missingDefsCount++;
                continue;
            }

            desiredTraits[entry.Part] = entry.Trait;
        }

        var analysis = PartAvailabilityAnalyzer.Analyze(weapon, desiredTraits);
        var nextTraits = new Dictionary<PartDef, WeaponTraitDef>(analysis.ActiveTraits);
        session.InstalledTraits = nextTraits;

        SoundDefOf.Tick_High.PlayOneShotOnCamera();
        onDataChanged();

        var skippedCount = missingDefsCount + analysis.SkippedCount;
        var message = skippedCount > 0
            ? "CWF_PresetAppliedWithSkipped".Translate(
                preset.Name.Named("NAME"),
                skippedCount.Named("COUNT"))
            : "CWF_PresetApplied".Translate(preset.Name.Named("NAME"));
        Messages.Message(message, MessageTypeDefOf.PositiveEvent, false);
    }

    #region Helpers

    private Action CreateInstallAction(PartDef part, WeaponTraitDef traitToInstall) => () => {
        var analysis = AnalyzeInstallConflict(part, traitToInstall);
        if (!analysis.HasConflict) {
            DoInstall(part, traitToInstall);
            return;
        }

        ShowConfirmationDialog(
            "CWF_ConfirmInstallTitle".Translate(),
            "CWF_ConfirmInstallBody".Translate(traitToInstall.LabelCap.Named("MODULE"),
                analysis.ModulesToRemove
                    .Select(t => " - " + t.LabelCap.ToString()).ToLineList()
                    .Named("CONFLICTS")
            ),
            () => {
                foreach (var conflictTrait in analysis.ModulesToRemove) {
                    if (conflictTrait.TryGetPart(out var conflictPart)) {
                        DoUninstall(conflictPart);
                    }
                }

                DoInstall(part, traitToInstall);
            }
        );
    };

    private ConflictAnalysisResult AnalyzeInstallConflict(PartDef partToInstall, WeaponTraitDef traitToInstall) {
        var currentTraits = session.InstalledTraits;
        currentTraits[partToInstall] = traitToInstall;

        var analysis = PartAvailabilityAnalyzer.Analyze(weapon, currentTraits);
        return CollectRemovedTraits(currentTraits, analysis.ActiveTraits, excludePart: partToInstall);
    }

    private ConflictAnalysisResult AnalyzeUninstallConflict(PartDef partToUninstall) {
        var currentTraits = session.InstalledTraits;
        if (!currentTraits.Remove(partToUninstall)) {
            return new ConflictAnalysisResult();
        }

        var analysis = PartAvailabilityAnalyzer.Analyze(weapon, currentTraits);
        return CollectRemovedTraits(currentTraits, analysis.ActiveTraits);
    }

    private static ConflictAnalysisResult CollectRemovedTraits(
        IReadOnlyDictionary<PartDef, WeaponTraitDef> desiredTraits,
        IReadOnlyDictionary<PartDef, WeaponTraitDef> activeTraits,
        PartDef? excludePart = null) {
        var result = new ConflictAnalysisResult();

        foreach (var (part, trait) in desiredTraits) {
            if (excludePart == part) continue;
            if (activeTraits.TryGetValue(part, out var activeTrait) && activeTrait == trait) continue;

            result.ModulesToRemove.Add(trait);
        }

        return result;
    }

    private static void ShowConfirmationDialog(string title, string text, Action onConfirm) {
        var dialog = new Dialog_MessageBox(
            text,
            "Confirm".Translate(), onConfirm,
            "Cancel".Translate(), null,
            title,
            true, onConfirm
        );
        Find.WindowStack.Add(dialog);
    }

    private IEnumerable<ThingDef> GetCompatibleModuleDefsFor(PartDef part) => ModuleDatabase.AllModuleDefs
        .Where(moduleDef => moduleDef.TraitModule().part == part)
        .Where(moduleDef => moduleDef.IsCompatibleWith(weapon.def));

    #endregion
}

public class ConflictAnalysisResult {
    public List<WeaponTraitDef> ModulesToRemove { get; } = [];

    public bool HasConflict => !ModulesToRemove.NullOrEmpty();
}