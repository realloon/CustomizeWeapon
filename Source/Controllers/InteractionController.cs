using RimWorld;
using Verse;
using Verse.Sound;
using CWF.Extensions;

namespace CWF.Controllers;

public class InteractionController(Thing weapon) {
    private readonly AssemblyPresetManager? _presetManager = Current.Game?.GetComponent<AssemblyPresetManager>();

    public event Action OnDataChanged = delegate { };

    public ModificationSession Session { get; } = new(weapon);

    public bool HasInstalledModules => Session.Traits.Any();

    public bool HasApplicablePresets => _presetManager?.GetPresetsFor(weapon.def).Any() == true;

    public IEnumerable<AssemblyPresetData> GetApplicablePresets() {
        return _presetManager?.GetPresetsFor(weapon.def) ?? [];
    }

    private string FailureReason => weapon.ParentHolder is Pawn_EquipmentTracker
        ? "CWF_NoCompatibleModulesInInventory".Translate()
        : "CWF_NoCompatibleModulesOnMap".Translate();

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

        if (Enumerable.Any(options)) {
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    private void BuildInstallOptions(PartDef part, List<FloatMenuOption> options) {
        var installCandidates = new Dictionary<WeaponTraitDef, ThingDef>();

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
                compatibleModuleDefs.Contains(t.def) &&
                (ownerPawn != null || !t.IsForbidden(Faction.OfPlayer))
            );

            foreach (var module in availableModules) {
                var trait = module.def.GetModExtension<TraitModuleExtension>().weaponTraitDef; // todo: fixme
                installCandidates.TryAdd(trait, module.def);
            }
        }

        // from stack
        var stagedCompatibleTraits = Session.GetReinstallableTraitsFor(part);
        foreach (var trait in stagedCompatibleTraits) {
            if (trait.TryGetModuleDef(out var moduleDef)) {
                installCandidates.TryAdd(trait, moduleDef);
            }
        }

        if (!installCandidates.Any()) {
            options.Add(new FloatMenuOption(FailureReason, null));
            return;
        }

        foreach (var (traitToInstall, _) in installCandidates) {
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
        Session.InstallTrait(part, traitToInstall);

        SoundDefOf.Tick_High.PlayOneShotOnCamera();
        OnDataChanged();
    }

    private void DoUninstall(PartDef part) {
        Session.UninstallTrait(part);
        SoundDefOf.Tick_High.PlayOneShotOnCamera();
        OnDataChanged();
    }

    public void ClearAllModules() {
        Session.ClearTraits();

        SoundDefOf.Click.PlayOneShotOnCamera();
        OnDataChanged();
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

        _presetManager.SavePreset(weapon, normalizedName, Session.InstalledTraits);
        Messages.Message(
            "CWF_PresetSaved".Translate(normalizedName.Named("NAME")),
            MessageTypeDefOf.PositiveEvent,
            false);
    }

    public void DeletePreset(AssemblyPresetData preset) {
        if (_presetManager == null) {
            Messages.Message("CWF_PresetManagerUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
            return;
        }

        var deleted = _presetManager.DeletePreset(weapon.def, preset.Name);
        if (!deleted) {
            Messages.Message(
                "CWF_PresetDeleteFailed".Translate(preset.Name.Named("NAME")),
                MessageTypeDefOf.RejectInput,
                false);
            return;
        }

        Messages.Message(
            "CWF_PresetDeleted".Translate(preset.Name.Named("NAME")),
            MessageTypeDefOf.PositiveEvent,
            false);
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
        Session.InstalledTraits = nextTraits;

        SoundDefOf.Tick_High.PlayOneShotOnCamera();
        OnDataChanged();

        var skippedCount = missingDefsCount + analysis.SkippedCount;
        var message = skippedCount > 0
            ? "CWF_PresetAppliedWithSkipped".Translate(
                preset.Name.Named("NAME"),
                skippedCount.Named("COUNT"))
            : "CWF_PresetApplied".Translate(preset.Name.Named("NAME"));
        Messages.Message(message, MessageTypeDefOf.PositiveEvent, false);
    }

    #region Helpers

    private Action CreateInstallAction(PartDef part, WeaponTraitDef traitToInstall) {
        return () => {
            if (!traitToInstall.TryGetModuleDef(out _)) {
                DoInstall(part, traitToInstall);
                return;
            }

            var analysis = AnalyzeInstallConflict(part, traitToInstall);
            if (!analysis.HasConflict) {
                DoInstall(part, traitToInstall);
                return;
            }

            ShowConfirmationDialog(
                "CWF_ConfirmInstallTitle".Translate(),
                "CWF_ConfirmInstallBody".Translate(
                    traitToInstall.LabelCap.Named("MODULE"),
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
    }

    private ConflictAnalysisResult AnalyzeInstallConflict(PartDef partToInstall, WeaponTraitDef traitToInstall) {
        var currentTraits = Session.InstalledTraits;
        currentTraits[partToInstall] = traitToInstall;

        var analysis = PartAvailabilityAnalyzer.Analyze(weapon, currentTraits);
        return CollectRemovedTraits(currentTraits, analysis.ActiveTraits, excludePart: partToInstall);
    }

    private ConflictAnalysisResult AnalyzeUninstallConflict(PartDef partToUninstall) {
        var currentTraits = Session.InstalledTraits;

        if (!currentTraits.Remove(partToUninstall)) return new ConflictAnalysisResult();

        var analysis = PartAvailabilityAnalyzer.Analyze(weapon, currentTraits);
        return CollectRemovedTraits(currentTraits, analysis.ActiveTraits);
    }

    private static ConflictAnalysisResult CollectRemovedTraits(
        IReadOnlyDictionary<PartDef, WeaponTraitDef> desiredTraits,
        IReadOnlyDictionary<PartDef, WeaponTraitDef> activeTraits,
        PartDef? excludePart = null) {
        var result = new ConflictAnalysisResult();

        foreach (var (part, trait) in desiredTraits) {
            if (excludePart == part) {
                continue;
            }

            if (activeTraits.TryGetValue(part, out var activeTrait) && activeTrait == trait) {
                continue;
            }

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

    private IEnumerable<ThingDef> GetCompatibleModuleDefsFor(PartDef part) {
        return ModuleDatabase.AllModuleDefs
            .Where(moduleDef => moduleDef.GetModExtension<TraitModuleExtension>().part == part)
            .Where(moduleDef => moduleDef.IsCompatibleWith(weapon.def));
    }

    #endregion
}

public class ConflictAnalysisResult {
    public List<WeaponTraitDef> ModulesToRemove { get; } = [];

    public bool HasConflict => !ModulesToRemove.NullOrEmpty();
}