using JetBrains.Annotations;
using Verse;
using Verse.AI;
using Verse.Sound;
using RimWorld;
using CWF.Extensions;

namespace CWF;

// ReSharper disable once InconsistentNaming
[UsedImplicitly]
public class JobDriver_ModifyWeaponHaul : JobDriver {
    private const TargetIndex WeaponInd = TargetIndex.A;
    private const TargetIndex ModuleToHaulInd = TargetIndex.B;
    private const int TicksPerModification = 60;

    private Thing Weapon => job.GetTarget(WeaponInd).Thing;
    private List<ModificationData>? _modDataList;

    public override bool TryMakePreToilReservations(bool errorOnFailed) {
        // reserve weapon
        if (!pawn.Reserve(Weapon, job, 1, -1, null, errorOnFailed)) {
            return false;
        }

        // no modules to haul.
        if (job.targetQueueB.NullOrEmpty()) {
            return true;
        }

        var succeedReserved = job.targetQueueB
            .Where(target => pawn.Reserve(target.Thing, job, 1, -1, null, errorOnFailed))
            .ToList();

        // replace queue with succeed reserved modules.
        job.targetQueueB = Enumerable.Any(succeedReserved)
            ? succeedReserved
            : null;

        return true; // always succeed while holding a weapon.
    }

    public override void Notify_Starting() {
        base.Notify_Starting();

        _modDataList = (job.source as ModificationJobSource)?.ModDataList;
        job.source = null;
    }

    public override void ExposeData() {
        base.ExposeData();
        Scribe_Collections.Look(ref _modDataList, "modDataList", LookMode.Deep);
    }

    protected override IEnumerable<Toil> MakeNewToils() {
        // === Phase 1: Preparation ===
        var startModificationPhase = Toils_General.Label();
        yield return Toils_Jump.JumpIf(startModificationPhase, () => job.targetQueueB.NullOrEmpty());

        // === Phase 2: Loop-Carry all required modules ===
        var haulLoop = Toils_General.Label();
        yield return haulLoop;

        yield return Toils_JobTransforms.ExtractNextTargetFromQueue(ModuleToHaulInd);

        yield return Toils_Goto
            .GotoThing(ModuleToHaulInd, PathEndMode.ClosestTouch)
            .FailOnDespawnedNullOrForbidden(ModuleToHaulInd);

        var customCarryToil = new Toil {
            initAction = () => {
                var actor = pawn;
                var thingToCarry = job.GetTarget(ModuleToHaulInd).Thing;

                if (thingToCarry == null || thingToCarry.Destroyed || thingToCarry.stackCount <= 0) {
                    // skip uncatchable modules.
                    actor.jobs.curDriver.ReadyForNextToil();
                    return;
                }

                actor.carryTracker.TryStartCarry(thingToCarry, 1);
            },
            defaultCompleteMode = ToilCompleteMode.Instant
        };
        yield return customCarryToil;

        var moveToInventory = new Toil {
            initAction = () => {
                var carriedThing = pawn.carryTracker.CarriedThing;
                if (carriedThing != null) {
                    pawn.inventory.innerContainer.TryAddOrTransfer(carriedThing);
                }
            },
            defaultCompleteMode = ToilCompleteMode.Instant
        };
        yield return moveToInventory;

        yield return Toils_Jump.JumpIfHaveTargetInQueue(ModuleToHaulInd, haulLoop);

        // === Phase 3: Perform Modification ===
        yield return startModificationPhase;
        yield return Toils_Goto.GotoThing(WeaponInd, PathEndMode.Touch);

        var finalToil =
            Toils_General.WaitWith(WeaponInd, TicksPerModification * (_modDataList?.Count ?? 1), true, true);
        finalToil.FailOnCannotTouch(WeaponInd, PathEndMode.Touch);

        finalToil.AddEndCondition(() => {
            if (_modDataList.IsNullOrEmpty()) return JobCondition.Ongoing;

            return _modDataList
                .Where(modData => modData.Type == ModificationType.Install)
                .Any(modData => pawn.inventory.innerContainer
                    .All(t => t.def != modData.ModuleDef))
                ? JobCondition.Incompletable
                : JobCondition.Ongoing;
        });

        finalToil.AddFinishAction(() => {
            if (ended) return;

            var comp = Weapon.TryGetComp<CompDynamicTraits>();
            if (comp == null || _modDataList == null) return;

            PerformModifications(comp, _modDataList);

            Messages.Message("CWF_ModificationComplete"
                    .Translate(pawn.Named("PAWN"), Weapon.Named("WEAPON")),
                new LookTargets(pawn, Weapon), MessageTypeDefOf.PositiveEvent);

            SoundDefOf.Replant_Complete.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
        });

        yield return finalToil;
    }

    // helper
    private void PerformModifications(CompDynamicTraits comp, List<ModificationData> modList) {
        // uninstall
        foreach (var modData in modList.Where(md => md.Type == ModificationType.Uninstall)) {
            comp.UninstallTrait(modData.Part);
            var moduleThing = ThingMaker.MakeThing(modData.ModuleDef);
            GenPlace.TryPlaceThing(moduleThing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
        }

        // install
        foreach (var modData in modList.Where(md => md.Type == ModificationType.Install)) {
            var moduleToUse = pawn.inventory.innerContainer.FirstOrDefault(t => t.def == modData.ModuleDef);

            if (moduleToUse != null) {
                comp.InstallTrait(modData.Part, modData.Trait);
                moduleToUse.SplitOff(1).Destroy();
            } else {
                Log.Error($"[CWF] '{modData.ModuleDef.defName}' missing in FinishAction despite passing EndCondition.");
            }
        }
    }
}