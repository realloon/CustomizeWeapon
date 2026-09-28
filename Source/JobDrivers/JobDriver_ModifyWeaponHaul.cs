using JetBrains.Annotations;
using Verse.AI;
using Verse.Sound;

namespace CWF;

// ReSharper disable once InconsistentNaming
[UsedImplicitly]
public class JobDriver_ModifyWeaponHaul : JobDriver {
    private const TargetIndex WeaponInd = TargetIndex.A;
    private const TargetIndex ModuleToHaulInd = TargetIndex.B;
    private const int TicksPerModification = 60;

    private Thing Weapon => job.GetTarget(WeaponInd).Thing;
    private List<ModificationData> _modDataList = [];

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

        _modDataList = ((ModificationJobSource)job.source).ModDataList;
        job.source = null;
    }

    public override void ExposeData() {
        base.ExposeData();
        Scribe_Collections.Look(ref _modDataList, "modDataList", LookMode.Deep);
    }

    protected override IEnumerable<Toil> MakeNewToils() {
        if (!job.targetQueueB.NullOrEmpty()) {
            var haulLoop = Toils_General.Label();
            yield return haulLoop;

            yield return Toils_JobTransforms.ExtractNextTargetFromQueue(ModuleToHaulInd);

            yield return Toils_Goto
                .GotoThing(ModuleToHaulInd, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(ModuleToHaulInd);

            yield return Toils_Haul.TakeToInventory(ModuleToHaulInd, 1);

            yield return Toils_Jump.JumpIfHaveTargetInQueue(ModuleToHaulInd, haulLoop);
        }

        yield return Toils_Goto.GotoThing(WeaponInd, PathEndMode.Touch);

        var finalToil =
            Toils_General.WaitWith(WeaponInd, TicksPerModification * _modDataList.Count, true, true);
        finalToil.FailOnCannotTouch(WeaponInd, PathEndMode.Touch);

        finalToil.AddEndCondition(() => ModificationOperations.HasRequiredModules(pawn, _modDataList)
            ? JobCondition.Ongoing
            : JobCondition.Incompletable);

        finalToil.AddFinishAction(() => {
            if (ended) return;

            var comp = Weapon.TryGetComp<CompDynamicTraits>();
            if (comp == null) return;

            ModificationOperations.Apply(comp, pawn, _modDataList, addUninstalledModulesToInventory: false);

            Messages.Message("CWF_ModificationComplete"
                    .Translate(pawn.Named("PAWN"), Weapon.Named("WEAPON")),
                new LookTargets(pawn, Weapon), MessageTypeDefOf.PositiveEvent);

            SoundDefOf.Replant_Complete.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
        });

        yield return finalToil;
    }
}