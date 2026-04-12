using JetBrains.Annotations;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using RimWorld;
using RimWorld.Utility;
using Verse;
using Verse.Sound;

namespace CWF;

public class CompAbilityProvider : ThingComp {
    private static readonly FieldInfo InCooldownField = AccessTools.Field(typeof(Ability), "inCooldown");
    private static readonly FieldInfo CooldownEndTickField = AccessTools.Field(typeof(Ability), "cooldownEndTick");
    private static readonly FieldInfo CooldownDurationField = AccessTools.Field(typeof(Ability), "cooldownDuration");

    private readonly HashSet<AbilityDef> _managedAbilityDefs = [];
    private List<CompProperties_EquippableAbilityReloadable> _abilityPropsToManage = [];
    private List<AbilityState> _abilityStates = [];

    private Pawn? CurrentHolder => parent.ParentHolder is Pawn_EquipmentTracker { pawn: not null } equipmentTracker
        ? equipmentTracker.pawn
        : null;

    private static Pawn_AbilityTracker AbilityTrackerFor(Pawn pawn) {
        return pawn.abilities ?? throw new InvalidOperationException(
            $"[CWF] Pawn '{pawn.LabelShortCap}' is missing Pawn_AbilityTracker.");
    }

    public IEnumerable<ReloadableAbility> Reloadables {
        get {
            foreach (var abilityProps in _abilityPropsToManage) {
                if (TryGetManagedAbility(abilityProps.abilityDef, out _, out _)) {
                    yield return new ReloadableAbility(this, abilityProps.abilityDef);
                }
            }
        }
    }

    public void SetOrUpdateAbilities(List<CompProperties_EquippableAbilityReloadable> newPropsList, bool isPostLoad) {
        _abilityPropsToManage = SanitizeAbilityProps(newPropsList);
        PruneStoredStates();

        var holder = CurrentHolder;
        if (holder != null) {
            SyncAbilities(holder, isPostLoad);
        }
    }

    public override void Notify_Equipped(Pawn pawn) {
        base.Notify_Equipped(pawn);
        SyncAbilities(pawn, false);
    }

    public override void Notify_Unequipped(Pawn pawn) {
        base.Notify_Unequipped(pawn);
        CaptureManagedAbilityStates(pawn);

        var abilityTracker = AbilityTrackerFor(pawn);
        foreach (var abilityDef in _managedAbilityDefs) {
            abilityTracker.RemoveAbility(abilityDef);
        }

        _managedAbilityDefs.Clear();
    }

    public override void PostExposeData() {
        base.PostExposeData();

        if (Scribe.mode == LoadSaveMode.Saving) {
            var holder = CurrentHolder;
            if (holder != null) {
                CaptureManagedAbilityStates(holder);
            }
        }

        Scribe_Collections.Look(ref _abilityStates, "abilityStates", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit) {
            _abilityStates ??= [];
            _managedAbilityDefs.Clear();
        }
    }

    public bool IsEquippedBy(Pawn pawn) => CurrentHolder == pawn;

    public IEnumerable<ReloadableAbility> GetReloadablesUsingResource(ThingDef resourceDef, bool allowForcedReload) {
        foreach (var reloadable in Reloadables) {
            if (reloadable.AmmoDef == resourceDef && reloadable.NeedsReload(allowForcedReload)) {
                yield return reloadable;
            }
        }
    }

    public bool TryGetReloadable(AbilityDef abilityDef, out ReloadableAbility reloadable) {
        if (TryGetManagedAbility(abilityDef, out _, out _)) {
            reloadable = new ReloadableAbility(this, abilityDef);
            return true;
        }

        reloadable = null!;
        return false;
    }

    internal bool TryGetReloadableData(AbilityDef abilityDef, out Ability ability,
        out CompProperties_EquippableAbilityReloadable abilityProps) {
        return TryGetManagedAbility(abilityDef, out ability, out abilityProps) && ability.UsesCharges;
    }

    internal void ReloadFrom(Thing reloadResource, AbilityDef abilityDef) {
        if (!TryGetReloadableData(abilityDef, out var ability, out var abilityProps)) {
            return;
        }

        if (abilityProps.ammoDef != reloadResource.def || ability.RemainingCharges >= ability.maxCharges) {
            return;
        }

        var resourceCountPerCharge = abilityProps.ammoCountPerCharge;
        if (resourceCountPerCharge <= 0) {
            return;
        }

        var chargesToRefill = ability.maxCharges - ability.RemainingCharges;
        var resourceNeeded = chargesToRefill * resourceCountPerCharge;
        var resourceToConsume = Mathf.Min(reloadResource.stackCount, resourceNeeded);
        var chargesGained = resourceToConsume / resourceCountPerCharge;
        if (chargesGained <= 0) {
            return;
        }

        reloadResource.SplitOff(chargesGained * resourceCountPerCharge).Destroy();
        ability.RemainingCharges += chargesGained;
        abilityProps.soundReload?.PlayOneShot(new TargetInfo(parent.PositionHeld, parent.MapHeld));
        StoreState(new AbilityState(ability));
    }

    private List<CompProperties_EquippableAbilityReloadable> SanitizeAbilityProps(
        IEnumerable<CompProperties_EquippableAbilityReloadable> propsList) {
        var sanitized = new List<CompProperties_EquippableAbilityReloadable>();
        var seenAbilityDefs = new HashSet<AbilityDef>();

        foreach (var abilityProps in propsList) {
            if (abilityProps.abilityDef == null) {
                Log.Error($"[CWF] {parent.def.defName} has an ability provider entry without an AbilityDef.");
                continue;
            }

            if (!seenAbilityDefs.Add(abilityProps.abilityDef)) {
                Log.Error(
                    $"[CWF] {parent.def.defName} tries to manage ability '{abilityProps.abilityDef.defName}' more than once.");
                continue;
            }

            if (abilityProps.ammoCountToRefill != 0) {
                Log.Error(
                    $"[CWF] {parent.def.defName} uses unsupported ammoCountToRefill on '{abilityProps.abilityDef.defName}'. " +
                    "CompAbilityProvider only supports ammoCountPerCharge.");
                continue;
            }

            if (abilityProps.replenishAfterCooldown) {
                Log.Error(
                    $"[CWF] {parent.def.defName} uses unsupported replenishAfterCooldown on '{abilityProps.abilityDef.defName}'.");
                continue;
            }

            sanitized.Add(abilityProps);
        }

        return sanitized;
    }

    private void SyncAbilities(Pawn holder, bool isPostLoad) {
        var abilityTracker = AbilityTrackerFor(holder);

        CaptureManagedAbilityStates(holder);

        var desiredAbilityDefs = _abilityPropsToManage
            .Select(abilityProps => abilityProps.abilityDef)
            .ToHashSet();

        foreach (var managedAbilityDef in
                 _managedAbilityDefs.Where(def => !desiredAbilityDefs.Contains(def)).ToList()) {
            abilityTracker.RemoveAbility(managedAbilityDef);
            _managedAbilityDefs.Remove(managedAbilityDef);
            RemoveStoredState(managedAbilityDef);
        }

        foreach (var abilityProps in _abilityPropsToManage) {
            SyncAbility(holder, abilityTracker, abilityProps, isPostLoad);
        }
    }

    private void SyncAbility(Pawn holder, Pawn_AbilityTracker abilityTracker,
        CompProperties_EquippableAbilityReloadable abilityProps, bool isPostLoad) {
        var abilityDef = abilityProps.abilityDef;
        var wasManaged = _managedAbilityDefs.Contains(abilityDef);
        var ability = abilityTracker.GetAbility(abilityDef);

        if (!wasManaged && ability != null) {
            if (isPostLoad && TryGetStoredState(abilityDef, out var storedState)) {
                _managedAbilityDefs.Add(abilityDef);
                ApplyProps(ability, abilityProps);
                ApplyStoredState(ability, storedState);
                return;
            }

            Log.Error(
                $"[CWF] {parent.def.defName} cannot manage ability '{abilityDef.defName}' on {holder.LabelShortCap} " +
                "because the pawn already has the same AbilityDef from another source.");
            return;
        }

        var created = false;
        if (ability == null) {
            abilityTracker.GainAbility(abilityDef);
            ability = abilityTracker.GetAbility(abilityDef);
            if (ability == null) {
                Log.Error(wasManaged
                    ? $"[CWF] Failed to recreate managed ability '{abilityDef.defName}' for {holder.LabelShortCap}."
                    : $"[CWF] Failed to add ability '{abilityDef.defName}' for {holder.LabelShortCap}.");
                _managedAbilityDefs.Remove(abilityDef);
                return;
            }

            created = true;
        }

        if (!wasManaged) {
            _managedAbilityDefs.Add(abilityDef);
        }

        ApplyProps(ability, abilityProps);

        if (!created) {
            return;
        }

        if (TryGetStoredState(abilityDef, out var restoredState)) {
            ApplyStoredState(ability, restoredState);
            return;
        }

        if (!isPostLoad) {
            ability.RemainingCharges = ability.maxCharges;
        }
    }

    private static void ApplyProps(Ability ability, CompProperties_EquippableAbilityReloadable abilityProps) {
        ability.maxCharges = abilityProps.maxCharges;
        if (ability.RemainingCharges > ability.maxCharges) {
            ability.RemainingCharges = ability.maxCharges;
        }
    }

    private static void ApplyStoredState(Ability ability, AbilityState storedState) {
        ability.RemainingCharges = Mathf.Clamp(storedState.remainingCharges, 0, ability.maxCharges);

        if (storedState.cooldownTicksRemaining > 0) {
            InCooldownField.SetValue(ability, true);
            CooldownEndTickField.SetValue(ability, GenTicks.TicksGame + storedState.cooldownTicksRemaining);
            CooldownDurationField.SetValue(ability, storedState.cooldownTicksTotal);
            return;
        }

        InCooldownField.SetValue(ability, false);
        CooldownEndTickField.SetValue(ability, 0);
        CooldownDurationField.SetValue(ability, 0);
    }

    private void CaptureManagedAbilityStates(Pawn holder) {
        var abilityTracker = AbilityTrackerFor(holder);

        foreach (var abilityDef in _managedAbilityDefs.ToList()) {
            var ability = abilityTracker.GetAbility(abilityDef);
            if (ability == null) {
                _managedAbilityDefs.Remove(abilityDef);
                continue;
            }

            StoreState(new AbilityState(ability));
        }
    }

    private void PruneStoredStates() {
        var desiredAbilityDefs = _abilityPropsToManage
            .Select(abilityProps => abilityProps.abilityDef)
            .ToHashSet();

        _abilityStates.RemoveAll(state => state.abilityDef == null || !desiredAbilityDefs.Contains(state.abilityDef));
    }

    private bool TryGetStoredState(AbilityDef abilityDef, out AbilityState storedState) {
        var matchedState = _abilityStates.FirstOrDefault(state => state.abilityDef == abilityDef);
        if (matchedState == null) {
            storedState = null!;
            return false;
        }

        storedState = matchedState;
        return true;
    }

    private void StoreState(AbilityState newState) {
        RemoveStoredState(newState.abilityDef);
        _abilityStates.Add(newState);
    }

    private void RemoveStoredState(AbilityDef? abilityDef) {
        if (abilityDef == null) {
            return;
        }

        _abilityStates.RemoveAll(state => state.abilityDef == abilityDef);
    }

    private bool TryGetManagedAbility(AbilityDef abilityDef, out Ability ability,
        out CompProperties_EquippableAbilityReloadable abilityProps) {
        ability = null!;

        var matchedProps = _abilityPropsToManage.FirstOrDefault(candidate => candidate.abilityDef == abilityDef);
        if (matchedProps == null || !_managedAbilityDefs.Contains(abilityDef)) {
            abilityProps = null!;
            return false;
        }

        abilityProps = matchedProps;

        var holder = CurrentHolder;
        if (holder == null) {
            return false;
        }

        ability = AbilityTrackerFor(holder).GetAbility(abilityDef);
        return ability != null;
    }
}

public sealed class ReloadableAbility(CompAbilityProvider provider, AbilityDef abilityDef) : IReloadableComp {
    private CompAbilityProvider Provider { get; } = provider;
    public AbilityDef AbilityDef { get; } = abilityDef;

    public string AbilityLabel => TryResolve(out var ability, out _)
        ? ability.def.LabelCap
        : AbilityDef.label.CapitalizeFirst();

    public Thing ReloadableThing => Provider.parent;
    public ThingDef? AmmoDef => TryResolve(out _, out var abilityProps) ? abilityProps.ammoDef : null;
    public int BaseReloadTicks => TryResolve(out _, out var abilityProps) ? abilityProps.baseReloadTicks : 60;
    public int RemainingCharges => TryResolve(out var ability, out _) ? ability.RemainingCharges : 0;
    public int MaxCharges => TryResolve(out var ability, out _) ? ability.maxCharges : 0;
    public string LabelRemaining => $"{RemainingCharges} / {MaxCharges}";

    public bool CanBeUsed(out string reason) {
        if (!TryResolve(out var ability, out _)) {
            reason = null!;
            return false;
        }

        if (ability.RemainingCharges > 0) {
            reason = null!;
            return true;
        }

        reason = DisabledReason(MinAmmoNeeded(false), MaxAmmoNeeded(false));
        return false;
    }

    public bool NeedsReload(bool allowForceReload) {
        if (!TryResolve(out var ability, out _)) {
            return false;
        }

        return allowForceReload
            ? ability.RemainingCharges < ability.maxCharges
            : ability.RemainingCharges <= 0;
    }

    public int MinAmmoNeeded(bool allowForcedReload) {
        if (!NeedsReload(allowForcedReload)) {
            return 0;
        }

        return TryResolve(out _, out var abilityProps)
            ? abilityProps.ammoCountPerCharge
            : 0;
    }

    public int MaxAmmoNeeded(bool allowForcedReload) {
        if (!TryResolve(out var ability, out var abilityProps) || !NeedsReload(allowForcedReload)) {
            return 0;
        }

        return (ability.maxCharges - ability.RemainingCharges) * abilityProps.ammoCountPerCharge;
    }

    public int MaxAmmoAmount() {
        if (!TryResolve(out var ability, out var abilityProps)) {
            return 0;
        }

        return ability.maxCharges * abilityProps.ammoCountPerCharge;
    }

    public void ReloadFrom(Thing ammo) {
        Provider.ReloadFrom(ammo, AbilityDef);
    }

    public string DisabledReason(int minNeeded, int maxNeeded) {
        if (!TryResolve(out _, out var abilityProps)) {
            return "CommandReload_NoCharges".Translate();
        }

        var resourceDef = abilityProps.ammoDef;
        if (resourceDef == null) {
            return "CommandReload_NoCharges".Translate(abilityProps.ChargeNounArgument);
        }

        var countLabel = minNeeded == maxNeeded ? minNeeded.ToString() : $"{minNeeded}-{maxNeeded}";
        return "CommandReload_NoAmmo".Translate(abilityProps.ChargeNounArgument, resourceDef.Named("AMMO"),
            countLabel.Named("COUNT"));
    }

    private bool TryResolve(out Ability ability, out CompProperties_EquippableAbilityReloadable abilityProps) {
        return Provider.TryGetReloadableData(AbilityDef, out ability, out abilityProps);
    }
}

public class AbilityState : IExposable {
    public AbilityDef? abilityDef;
    public int remainingCharges;
    public int cooldownTicksRemaining;
    public int cooldownTicksTotal;

    [UsedImplicitly]
    public AbilityState() { }

    public AbilityState(Ability ability) {
        abilityDef = ability.def;
        remainingCharges = ability.RemainingCharges;
        cooldownTicksRemaining = ability.CooldownTicksRemaining;
        cooldownTicksTotal = ability.CooldownTicksTotal;
    }

    public void ExposeData() {
        Scribe_Defs.Look(ref abilityDef, "abilityDef");
        Scribe_Values.Look(ref remainingCharges, "remainingCharges", -1);
        Scribe_Values.Look(ref cooldownTicksRemaining, "cooldownTicksRemaining");
        Scribe_Values.Look(ref cooldownTicksTotal, "cooldownTicksTotal");
    }
}