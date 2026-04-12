using JetBrains.Annotations;
using Verse;
using Verse.AI;
using RimWorld;

namespace CWF;

// ReSharper disable once InconsistentNaming
[UsedImplicitly]
public class JobDriver_ReloadAbility : JobDriver {
    private const TargetIndex GearInd = TargetIndex.A;
    private const TargetIndex ReloadResourceInd = TargetIndex.B;

    private Thing Gear => job.GetTarget(GearInd).Thing;
    private AbilityDef? _abilityDef;
    private AbilityDef AbilityDef => _abilityDef!;

    public override bool TryMakePreToilReservations(bool errorOnFailed) {
        pawn.ReserveAsManyAsPossible(job.GetTargetQueue(ReloadResourceInd), job);
        return true;
    }

    public override void Notify_Starting() {
        base.Notify_Starting();
        _abilityDef = (job.source as ReloadAbilityJobSource)?.AbilityDef;
        job.source = null;
    }

    public override void ExposeData() {
        base.ExposeData();
        Scribe_Defs.Look(ref _abilityDef, "abilityDef");
    }

    protected override IEnumerable<Toil> MakeNewToils() {
        if (_abilityDef == null) {
            Log.Error("[CWF] JobDriver_ReloadAbility started without an AbilityDef.");
            yield break;
        }

        this.FailOn(() => !HasReloadable());
        this.FailOn(() => !TryGetProvider(out var provider) || !provider.IsEquippedBy(pawn));
        this.FailOn(() => !CurrentReloadable.NeedsReload(allowForceReload: true));
        this.FailOnDestroyedOrNull(GearInd);
        this.FailOnIncapable(PawnCapacityDefOf.Manipulation);

        var initialReloadable = CurrentReloadable;

        var getNextIngredient = Toils_General.Label();
        yield return getNextIngredient;

        foreach (var toil in ReloadAsMuchAsPossible(initialReloadable.BaseReloadTicks)) {
            yield return toil;
        }

        yield return Toils_JobTransforms.ExtractNextTargetFromQueue(ReloadResourceInd);
        yield return Toils_Goto.GotoThing(ReloadResourceInd, PathEndMode.ClosestTouch)
            .FailOnDespawnedNullOrForbidden(ReloadResourceInd)
            .FailOnSomeonePhysicallyInteracting(ReloadResourceInd);
        yield return Toils_Haul.StartCarryThing(ReloadResourceInd, putRemainderInQueue: false, subtractNumTakenFromJobCount: true)
            .FailOnDestroyedNullOrForbidden(ReloadResourceInd);
        yield return Toils_Jump.JumpIf(getNextIngredient, () => !job.GetTargetQueue(ReloadResourceInd).NullOrEmpty());

        foreach (var toil in ReloadAsMuchAsPossible(initialReloadable.BaseReloadTicks)) {
            yield return toil;
        }

        var dropRemainder = ToilMaker.MakeToil();
        dropRemainder.initAction = delegate {
            var carriedThing = pawn.carryTracker.CarriedThing;
            if (carriedThing is { Destroyed: false }) {
                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
            }
        };
        dropRemainder.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return dropRemainder;
    }

    private IEnumerable<Toil> ReloadAsMuchAsPossible(int baseReloadTicks) {
        var done = Toils_General.Label();
        yield return Toils_Jump.JumpIf(done, () => {
            var carriedThing = pawn.carryTracker.CarriedThing;
            return carriedThing == null ||
                   carriedThing.stackCount < CurrentReloadable.MinAmmoNeeded(allowForcedReload: true);
        });

        yield return Toils_General.Wait(baseReloadTicks).WithProgressBarToilDelay(GearInd);

        var reload = ToilMaker.MakeToil();
        reload.initAction = delegate {
            var carriedThing = pawn.carryTracker.CarriedThing;
            if (carriedThing != null) {
                CurrentReloadable.ReloadFrom(carriedThing);
            }
        };
        reload.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return reload;

        yield return done;
    }

    private bool TryGetProvider(out CompAbilityProvider provider) {
        var matchedProvider = Gear.TryGetComp<CompAbilityProvider>();
        if (matchedProvider == null) {
            provider = null!;
            return false;
        }

        provider = matchedProvider;
        return true;
    }

    private bool HasReloadable() {
        return TryGetProvider(out var provider) && provider.TryGetReloadable(AbilityDef, out _);
    }

    private ReloadableAbility CurrentReloadable {
        get {
            if (TryGetProvider(out var provider) && provider.TryGetReloadable(AbilityDef, out var reloadable)) {
                return reloadable;
            }

            throw new InvalidOperationException("[CWF] Reload job lost its target reloadable ability.");
        }
    }
}