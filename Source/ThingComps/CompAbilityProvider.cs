using HarmonyLib;
using UnityEngine;
using RimWorld;
using RimWorld.Utility;
using Verse;
using Verse.Sound;

namespace CWF;

public class CompAbilityProvider : ThingComp, IReloadableComp {
    private Pawn? _holder;
    private List<CompProperties_EquippableAbility> _abilityPropsToManage = [];
    private Dictionary<Ability, CompProperties_EquippableAbility> _managedAbilities = new();
    private List<AbilityState>? _savedAbilityStates = [];

    public void SetOrUpdateAbilities(List<CompProperties_EquippableAbilityReloadable> newPropsList, bool isPostLoad) {
        _abilityPropsToManage = newPropsList
            .Cast<CompProperties_EquippableAbility>()
            .ToList();

        if (_holder != null) {
            ApplyAbilityChanges(isPostLoad);
        }
    }

    public override void Notify_Equipped(Pawn pawn) {
        base.Notify_Equipped(pawn);
        _holder = pawn;
        _managedAbilities = new Dictionary<Ability, CompProperties_EquippableAbility>();
        ApplyAbilityChanges(false);
    }

    public override void Notify_Unequipped(Pawn pawn) {
        base.Notify_Unequipped(pawn);
        if (_holder == null) return;

        foreach (var ability in _managedAbilities.Keys) {
            _holder.abilities?.RemoveAbility(ability.def);
        }

        _holder.abilities?.Notify_TemporaryAbilitiesChanged();

        _managedAbilities.Clear();
        _holder = null;
    }

    private void ApplyAbilityChanges(bool isPostLoad) {
        if (_holder == null) return;

        var propsToAdd = new List<CompProperties_EquippableAbility>(_abilityPropsToManage);
        var abilitiesToRemove = new List<Ability>();

        foreach (var kvp in _managedAbilities) {
            if (!_abilityPropsToManage.Contains(kvp.Value)) {
                abilitiesToRemove.Add(kvp.Key);
            } else {
                propsToAdd.Remove(kvp.Value);
            }
        }

        foreach (var ability in abilitiesToRemove) {
            _holder.abilities.RemoveAbility(ability.def);
            _managedAbilities.Remove(ability);
        }

        foreach (var abilityProps in propsToAdd) {
            _holder.abilities.GainAbility(abilityProps.abilityDef);
            var newAbility = _holder.abilities.GetAbility(abilityProps.abilityDef);
            if (newAbility == null) continue;

            _managedAbilities.Add(newAbility, abilityProps);

            if (abilityProps is not CompProperties_EquippableAbilityReloadable reloadableProps) continue;

            newAbility.maxCharges = reloadableProps.maxCharges;
            var savedState = _savedAbilityStates?
                .FirstOrFallback(s => s?.defName == newAbility.def.defName);

            if (savedState != null) {
                newAbility.RemainingCharges = savedState.remainingCharges;

                if (savedState.cooldownTicksRemaining > 0) {
                    var inCooldownField = AccessTools.Field(typeof(Ability), "inCooldown");
                    var cooldownEndTickField = AccessTools.Field(typeof(Ability), "cooldownEndTick");
                    var cooldownDurationField = AccessTools.Field(typeof(Ability), "cooldownDuration");

                    inCooldownField.SetValue(newAbility, true);
                    cooldownEndTickField.SetValue(newAbility, GenTicks.TicksGame + savedState.cooldownTicksRemaining);
                    cooldownDurationField.SetValue(newAbility, savedState.cooldownTicksTotal);
                }

                _savedAbilityStates?.Remove(savedState);
            } else if (!isPostLoad) {
                newAbility.RemainingCharges = newAbility.maxCharges;
            }
        }

        if (abilitiesToRemove.Any() || propsToAdd.Any()) {
            _holder.abilities.Notify_TemporaryAbilitiesChanged();
        }

        if (isPostLoad) {
            _savedAbilityStates = null;
        }
    }

    public override void PostExposeData() {
        base.PostExposeData();
        Scribe_References.Look(ref _holder, "holder");

        if (Scribe.mode is LoadSaveMode.Saving) {
            var defNameList = _abilityPropsToManage
                .Select(p => p.abilityDef.defName).ToList();
            Scribe_Collections.Look(ref defNameList, "abilityPropsToManageDefNames", LookMode.Value);
        }

        if (Scribe.mode is LoadSaveMode.LoadingVars) {
            List<string>? defNameList = null;
            Scribe_Collections.Look(ref defNameList, "abilityPropsToManageDefNames", LookMode.Value);
            if (defNameList != null) {
                _abilityPropsToManage = [];
                foreach (var defName in defNameList) {
                    var trait = DefDatabase<WeaponTraitDef>.AllDefs
                        .FirstOrFallback(t => t?.abilityProps?.abilityDef.defName == defName);
                    if (trait != null) {
                        _abilityPropsToManage.Add(trait.abilityProps);
                    }
                }
            }
        }

        if (Scribe.mode is LoadSaveMode.Saving) {
            var abilityStates = _managedAbilities.Keys.Select(ability => new AbilityState(ability)).ToList();
            Scribe_Collections.Look(ref abilityStates, "abilityStates", LookMode.Deep);
        }

        if (Scribe.mode is LoadSaveMode.LoadingVars) {
            Scribe_Collections.Look(ref _savedAbilityStates, "abilityStates", LookMode.Deep);
        }
    }

    #region impl IReloadableComp

    private Ability? FirstReloadableAbility =>
        _managedAbilities.Keys.FirstOrDefault(a =>
            a.UsesCharges && _managedAbilities[a] is CompProperties_EquippableAbilityReloadable);

    private CompProperties_EquippableAbilityReloadable? ReloadableProps =>
        _managedAbilities.Values.OfType<CompProperties_EquippableAbilityReloadable>().FirstOrFallback();

    public Thing ReloadableThing => parent;
    public ThingDef? AmmoDef => ReloadableProps?.ammoDef;
    public int BaseReloadTicks => ReloadableProps?.baseReloadTicks ?? 60;
    public int RemainingCharges => FirstReloadableAbility?.RemainingCharges ?? 0;
    public int MaxCharges => FirstReloadableAbility?.maxCharges ?? 0;
    public string LabelRemaining => $"{RemainingCharges} / {MaxCharges}";

    public bool CanBeUsed(out string? reason) {
        reason = null;
        var ability = FirstReloadableAbility;
        if (ability == null || ability.RemainingCharges > 0) return true;
        reason = DisabledReason(MinAmmoNeeded(false), MaxAmmoNeeded(false));
        return false;
    }

    public bool NeedsReload(bool allowForcedReload) {
        return _managedAbilities.Keys.Any(a => {
            if (_managedAbilities[a] is not CompProperties_EquippableAbilityReloadable) return false;
            return allowForcedReload
                ? a.RemainingCharges < a.maxCharges
                : a.RemainingCharges <= 0;
        });
    }

    public void ReloadFrom(Thing ammo) {
        var abilityToReload = _managedAbilities.Keys.FirstOrDefault(a => {
            var targetProps = _managedAbilities[a] as CompProperties_EquippableAbilityReloadable;
            return targetProps?.ammoDef == ammo.def && a.RemainingCharges < a.maxCharges;
        });

        if (abilityToReload == null) return;
        if (_managedAbilities[abilityToReload] is not CompProperties_EquippableAbilityReloadable abilityProps) return;

        var chargesToRefill = abilityToReload.maxCharges - abilityToReload.RemainingCharges;
        var ammoPerCharge = abilityProps.ammoCountPerCharge;
        if (ammoPerCharge <= 0) return;

        var ammoNeeded = chargesToRefill * ammoPerCharge;
        var ammoToConsume = Mathf.Min(ammo.stackCount, ammoNeeded);

        var chargesGained = ammoToConsume / ammoPerCharge;
        if (chargesGained <= 0) return;

        ammo.SplitOff(chargesGained * ammoPerCharge).Destroy();
        abilityToReload.RemainingCharges += chargesGained;
        abilityProps.soundReload?.PlayOneShot(new TargetInfo(parent.Position, parent.Map));
    }

    public int MinAmmoNeeded(bool allowForcedReload) {
        if (!NeedsReload(allowForcedReload)) return 0;

        var ability = _managedAbilities.Keys.FirstOrDefault(a => a.RemainingCharges < a.maxCharges);
        var abilityProps = ability != null
            ? _managedAbilities[ability] as CompProperties_EquippableAbilityReloadable
            : null;
        return abilityProps?.ammoCountPerCharge ?? 0;
    }

    public int MaxAmmoNeeded(bool allowForcedReload) {
        if (!NeedsReload(allowForcedReload)) return 0;

        var totalAmmoNeeded = 0;
        foreach (var ability in _managedAbilities.Keys) {
            if (_managedAbilities[ability] is CompProperties_EquippableAbilityReloadable abilityProps &&
                ability.RemainingCharges < ability.maxCharges) {
                totalAmmoNeeded += (ability.maxCharges - ability.RemainingCharges) * abilityProps.ammoCountPerCharge;
            }
        }

        return totalAmmoNeeded;
    }

    public int MaxAmmoAmount() {
        var totalMaxAmmo = 0;

        foreach (var ability in _managedAbilities.Keys) {
            if (_managedAbilities[ability] is CompProperties_EquippableAbilityReloadable abilityProps) {
                totalMaxAmmo += ability.maxCharges * abilityProps.ammoCountPerCharge;
            }
        }

        return totalMaxAmmo;
    }

    public string DisabledReason(int minNeeded, int maxNeeded) {
        return AmmoDef == null
            // Caller guarantees non-null context by checking FirstReloadableAbility first.
            ? "CommandReload_NoCharges".Translate(ReloadableProps!.ChargeNounArgument)
            : "CommandReload_NoAmmo".Translate(ReloadableProps!.ChargeNounArgument, AmmoDef.Named("AMMO"),
                minNeeded.Named("COUNT"));
    }

    #endregion

    // Helper
    public bool CanBeReloadedWith(ThingDef ammoDef) {
        return _managedAbilities.Keys.Any(ability => {
            var abilityProps = _managedAbilities[ability] as CompProperties_EquippableAbilityReloadable;
            return abilityProps?.ammoDef == ammoDef && ability.RemainingCharges < ability.maxCharges;
        });
    }
}

public class AbilityState : IExposable {
    public string? defName;
    public int remainingCharges;
    public int cooldownTicksRemaining;
    public int cooldownTicksTotal;

    public AbilityState() { }

    public AbilityState(Ability ability) {
        defName = ability.def.defName;
        remainingCharges = ability.RemainingCharges;
        cooldownTicksRemaining = ability.CooldownTicksRemaining;
        cooldownTicksTotal = ability.CooldownTicksTotal;
    }

    public void ExposeData() {
        Scribe_Values.Look(ref defName, "defName");
        Scribe_Values.Look(ref remainingCharges, "remainingCharges", -1);
        Scribe_Values.Look(ref cooldownTicksRemaining, "cooldownTicksRemaining");
        Scribe_Values.Look(ref cooldownTicksTotal, "cooldownTicksTotal");
    }
}
