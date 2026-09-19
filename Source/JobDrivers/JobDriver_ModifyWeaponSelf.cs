using JetBrains.Annotations;
using Verse.AI;
using Verse.Sound;

namespace CWF;

// ReSharper disable once InconsistentNaming
[UsedImplicitly]
public class JobDriver_ModifyWeaponSelf : JobDriver {
    private Thing Weapon => TargetA.Thing;
    private List<ModificationData>? _modDataList;

    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

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
        // safe check
        if (_modDataList == null || _modDataList.Count == 0) {
            Log.Error("[CWF] JobDriver_ModifyWeaponSelf started with empty ModDataList. Aborting.");
            yield break; // end job
        }

        var modDataList = _modDataList;

        // wait and show progress
        var modifyToil = Toils_General.Wait(60 * modDataList.Count);
        modifyToil.WithProgressBarToilDelay(TargetIndex.A);

        modifyToil.AddEndCondition(() => ModificationOperations.HasRequiredModules(pawn, modDataList)
            ? JobCondition.Ongoing
            : JobCondition.Incompletable);

        // finished progress
        modifyToil.AddFinishAction(() => {
            if (ended || !Weapon.TryGetComp<CompDynamicTraits>(out var compDynamicTraits)) return;

            ModificationOperations.Apply(compDynamicTraits, pawn, modDataList, addUninstalledModulesToInventory: true);
            SoundDefOf.Replant_Complete.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
        });

        yield return modifyToil;
    }
}