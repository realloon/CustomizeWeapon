using System.Text;
using HarmonyLib;
using UnityEngine;
using CWF.Extensions;

namespace CWF;

public class CompDynamicTraits : ThingComp {
    private Dictionary<PartDef, WeaponTraitDef> _installedTraits = new();
    private DamageDef? _projectileDamageOverride;
    private float _projectileStoppingPowerOffset;
    private readonly List<ExtraDamage> _projectileExtraDamages = [];

    /// <summary>
    /// Gets a copy of all installed Traits, or overwrites them with a new dictionary.
    /// </summary>
    public Dictionary<PartDef, WeaponTraitDef> InstalledTraits {
        get => new(_installedTraits);
        set {
            _installedTraits = new Dictionary<PartDef, WeaponTraitDef>(value);
            OnTraitsChanged();
        }
    }

    private CompProperties_DynamicTraits Props => (CompProperties_DynamicTraits)props;

    public IReadOnlyCollection<WeaponTraitDef> Traits => _installedTraits.Values;

    public void InstallTrait(PartDef part, WeaponTraitDef traitDef) {
        if (!_installedTraits.TryAdd(part, traitDef)) {
            Log.Warning($"[CWF] {traitDef.defName}'s slot {part} on {parent.LabelCap} already occupied.");
            return;
        }

        OnTraitsChanged();
    }

    public void UninstallTrait(PartDef part) {
        if (!_installedTraits.Remove(part)) return;

        OnTraitsChanged();
    }

    public void ClearTraits() {
        _installedTraits.Clear();

        OnTraitsChanged();
    }

    public WeaponTraitDef? GetInstalledTraitFor(PartDef part) {
        _installedTraits.TryGetValue(part, out var traitDef);
        return traitDef;
    }

    public void RandomizeTraits() {
        var settings = Settings.Current;
        if (!settings.RandomModulesEnabled) return;

        var modulesToInstallCount = Rand.RangeInclusive(0, settings.MaxRandomModules);
        if (modulesToInstallCount == 0) return;

        var availableParts = Props.supportParts.Except(_installedTraits.Keys).ToList();
        if (availableParts.Count == 0) return;

        var installedCount = 0;

        while (installedCount < modulesToInstallCount && availableParts.Count > 0) {
            var randomPart = availableParts.RandomElement();
            availableParts.Remove(randomPart);

            var compatibleModules = ModuleDatabase.AllModuleDefs
                .Where(moduleDef => moduleDef.TraitModule().part == randomPart)
                .Where(moduleDef => moduleDef.IsCompatibleWith(parent.def))
                .ToList();
            if (compatibleModules.Count == 0) continue;

            if (!compatibleModules.TryRandomElementByWeight(moduleDef => moduleDef.GetRarityWeight(),
                    out var moduleToInstall)) {
                Log.Error(
                    $"[CWF] No valid rarity weight for trait on part '{randomPart.defName}' ('{parent.def.defName}'). " +
                    $"Ensure at least one rarity weight > 0 in mod settings.");
                continue;
            }

            _installedTraits.Add(randomPart, moduleToInstall.TraitModule().weaponTraitDef);
            installedCount += 1;
        }

        // Rebuild stat, ability and graphic caches once instead of once per installed module.
        if (installedCount > 0) {
            OnTraitsChanged();
        }
    }

    #region Effects

    public override float GetStatOffset(StatDef stat) => Traits
        .Sum(traitDef => traitDef.statOffsets.GetStatOffsetFromList(stat));

    public override float GetStatFactor(StatDef stat) => Traits
        .Aggregate(1f, (current, traitDef) => current * traitDef.statFactors.GetStatFactorFromList(stat));

    internal void ApplyProjectileEffects(Projectile projectile) {
        if (_projectileDamageOverride != null) {
            projectile.damageDefOverride = _projectileDamageOverride;
        }

        projectile.extraDamages.AddRange(_projectileExtraDamages);
        projectile.stoppingPower += _projectileStoppingPowerOffset;
    }

    #endregion

    #region Display

    public override void GetStatsExplanation(StatDef stat, StringBuilder sb, string whitespace = "") {
        StringBuilder? stringBuilder = null;

        foreach (var weaponTraitDef in Traits) {
            // offset
            var statOffsetFromList = weaponTraitDef.statOffsets.GetStatOffsetFromList(stat);
            if (!Mathf.Approximately(statOffsetFromList, 0.0f)) {
                stringBuilder ??= new StringBuilder();
                var valueStr = stat.Worker.ValueToString(statOffsetFromList, false,
                    ToStringNumberSense.Offset);
                stringBuilder.AppendLine($"{whitespace} - {weaponTraitDef.LabelCap}: {valueStr}");
            }

            // factor
            var statFactorFromList = weaponTraitDef.statFactors.GetStatFactorFromList(stat);
            if (!Mathf.Approximately(statFactorFromList, 1f)) {
                stringBuilder ??= new StringBuilder();
                var valueStr = stat.Worker.ValueToString(statFactorFromList, false,
                    ToStringNumberSense.Factor);
                stringBuilder.AppendLine($"{whitespace} - {weaponTraitDef.LabelCap}: {valueStr}");
            }
        }

        if (stringBuilder == null) return;

        sb.AppendLine();
        sb.AppendLine(whitespace + "CWF_WeaponModules".Translate() + ":");
        sb.Append(stringBuilder);
    }

    public override string? CompInspectStringExtra() {
        if (_installedTraits.Count == 0) return null;

        var label = "CWF_WeaponModules".Translate();
        var traits = Traits.Select(traitDef => traitDef.label).ToCommaList().CapitalizeFirst();
        return $"{label}: {traits}";
    }

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats() {
        if (_installedTraits.Count == 0) yield break;

        var sb = new StringBuilder();
        sb.AppendLine("CWF_WeaponModules_Desc".Translate());
        sb.AppendLine();

        var traitDescriptionBlocks = Traits.Select(trait => {
            var blockBuilder = new StringBuilder();
            blockBuilder.AppendLine(trait.LabelCap.Colorize(ColorLibrary.Green));
            blockBuilder.AppendLine(trait.description);

            var effect = trait.GetTraitEffect();
            if (effect.Length > 0) {
                blockBuilder.AppendLine(effect);
            }

            return blockBuilder.ToString();
        });

        sb.Append(traitDescriptionBlocks.ToLineList());

        yield return new StatDrawEntry(
            StatCategoryDefOf.Weapon_Ranged,
            "CWF_WeaponModules".Translate(),
            Traits.Select(x => x.label).ToCommaList().CapitalizeFirst(),
            sb.ToString(),
            1105
        );
    }

    #endregion

    #region Callback

    public override void PostPostMake() {
        base.PostPostMake();

        InitializeTraits();
        RebuildProjectileEffects();
        SetupAbility(false);
    }

    public override void PostExposeData() {
        base.PostExposeData();

        if (Scribe.mode is LoadSaveMode.Saving or LoadSaveMode.LoadingVars) {
            Scribe_Collections.Look(ref _installedTraits, "installedTraits", LookMode.Def, LookMode.Def);
        }

        if (Scribe.mode is not LoadSaveMode.PostLoadInit) return;

        #region AutoFixMissing

        _installedTraits ??= new Dictionary<PartDef, WeaponTraitDef>();

        var partsWithMissingTraits = _installedTraits
            .Where(pair => pair.Value == null)
            .Select(pair => pair.Key)
            .ToArray();

        if (partsWithMissingTraits.Length > 0) {
            _installedTraits.RemoveRange(partsWithMissingTraits);

            Log.Warning($"[CWF] Removed {partsWithMissingTraits.Length} missing traits from '{parent.LabelCap}'. " +
                        $"This is a safe, one-time operation.");
        }

        #endregion

        RebuildProjectileEffects();
        SetupAbility(true);
    }

    #endregion

    #region Gizmos

    public override IEnumerable<Gizmo> CompGetGizmosExtra() {
        foreach (var g in base.CompGetGizmosExtra()) {
            yield return g;
        }

        if (parent.IsForbidden(Faction.OfPlayer)) yield break;

        yield return CreatePanelGizmo();

        if (!DebugSettings.ShowDevGizmos) yield break;
        var addTraitGizmo = new Command_Action {
            defaultLabel = "Dev: + Add Trait",
            action = () => {
                var availableTraits = DefDatabase<WeaponTraitDef>.AllDefs
                    .Where(traitDef => {
                        if (!traitDef.TryGetPart(out var part)) return false;
                        if (_installedTraits.ContainsKey(part)) return false;

                        return traitDef.TryGetModuleDef(out var moduleDef) &&
                               moduleDef.IsCompatibleWith(parent.def);
                    })
                    .ToList();

                if (availableTraits.Count == 0) {
                    Messages.Message("Debug: No available traits to install.", MessageTypeDefOf.NeutralEvent);
                    return;
                }

                var options = new List<FloatMenuOption>();

                foreach (var traitDef in availableTraits) {
                    var option = new FloatMenuOption(
                        traitDef.LabelCap,
                        () => {
                            if (!traitDef.TryGetPart(out var part)) return;
                            InstallTrait(part, traitDef);
                        }
                    );
                    options.Add(option);
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
        };
        yield return addTraitGizmo;

        var removeTraitGizmo = new Command_Action {
            defaultLabel = "Dev: - Remove Trait",
            action = () => {
                var options = new List<FloatMenuOption>();

                var availableTraits = Traits;

                foreach (var traitDef in availableTraits) {
                    var option = new FloatMenuOption(
                        traitDef.LabelCap,
                        () => {
                            if (!traitDef.TryGetPart(out var part)) return;
                            UninstallTrait(part);
                        }
                    );
                    options.Add(option);
                }

                if (options.Count == 0) {
                    options.Add(new FloatMenuOption("Nothing to remove", null));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
        };
        yield return removeTraitGizmo;
    }

    public IEnumerable<Gizmo> CompGetEquippedGizmosExtra(Pawn owner) {
        // harmony patched
        if (owner.Faction == Faction.OfPlayer) {
            yield return CreatePanelGizmo();
        }
    }

    private Command_Action CreatePanelGizmo() => new() {
        defaultLabel = "CWF_WeaponPanel".Translate(),
        defaultDesc = "CWF_WeaponPanelDesc".Translate(),
        icon = ContentFinder<Texture2D>.Get("CustomizeWeapon/Gizmos/Panel"),
        action = () => Find.WindowStack.Add(new WeaponWindow(parent))
    };

    #endregion

    #region Helper

    private void InitializeTraits() {
        var defaultTraitsList = Props.defaultWeaponTraitDefs;
        if (defaultTraitsList.Count == 0) return;

        foreach (var traitDef in defaultTraitsList) {
            if (traitDef.TryGetPart(out var part)) {
                // Found the corresponding slot; now check whether the slot is already occupied (conflict check).
                if (_installedTraits.TryGetValue(part, out var existingTrait)) {
                    Log.Error($"[CWF] Initialization Error for {parent.def.defName}: " +
                              $"Both '{traitDef.defName}' and '{existingTrait.defName}' are configured as default traits for the same part slot '{part}'. " +
                              $"Ignoring the latter: '{traitDef.defName}'.");
                } else {
                    // No conflict—install this default trait into the corresponding slot.
                    _installedTraits.Add(part, traitDef);
                }
            } else {
                // If no corresponding module definition is found, skip it.
                Log.Warning($"[CWF] Initialization Warning for {parent.def.defName}: " +
                            $"Default trait '{traitDef.defName}' has no corresponding TraitModule with a Part defined. It will be ignored.");
            }
        }
    }

    private void OnTraitsChanged() {
        RebuildProjectileEffects();
        SetupAbility(false);
        ClearAllCaches();

        parent.TryGetComp<CompDynamicGraphic>()?.Notify_GraphicDirty();
    }

    private void RebuildProjectileEffects() {
        _projectileDamageOverride = null;
        _projectileStoppingPowerOffset = 0f;
        _projectileExtraDamages.Clear();

        foreach (var trait in _installedTraits.Values) {
            if (trait.damageDefOverride != null) {
                _projectileDamageOverride = trait.damageDefOverride;
            }

            if (!trait.extraDamages.NullOrEmpty()) {
                _projectileExtraDamages.AddRange(trait.extraDamages);
            }

            if (!Mathf.Approximately(trait.additionalStoppingPower, 0f)) {
                _projectileStoppingPowerOffset += trait.additionalStoppingPower;
            }
        }
    }

    private void SetupAbility(bool isPostLoad) {
        var abilityProvider = parent.TryGetComp<CompAbilityProvider>();
        if (abilityProvider == null) return;

        var propsList = Traits
            .Where(trait => trait.abilityProps != null)
            .Select(trait => trait.abilityProps)
            .ToList();

        abilityProvider.SetOrUpdateAbilities(propsList, isPostLoad);
    }

    private void ClearAllCaches() {
        // === stats ===
        foreach (var statDef in DefDatabase<StatDef>.AllDefs) {
            statDef.Worker.ClearCacheForThing(parent);
        }

        // === verbs ===
        var verb = parent.TryGetComp<CompEquippable>()?.PrimaryVerb;
        if (verb == null) return;

        // === cached ===
        AccessTools.Field(typeof(Verb), "cachedBurstShotCount").SetValue(verb, null);
        AccessTools.Field(typeof(Verb), "cachedTicksBetweenBurstShots").SetValue(verb, null);
    }

    #endregion
}