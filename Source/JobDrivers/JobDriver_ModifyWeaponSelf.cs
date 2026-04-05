using JetBrains.Annotations;
using Verse;
using Verse.AI;
using Verse.Sound;
using RimWorld;

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

        modifyToil.AddEndCondition(() => {
            return modDataList
                .Where(modData => modData.Type == ModificationType.Install)
                .Any(modData => pawn.inventory.innerContainer.All(t => t.def != modData.ModuleDef))
                ? JobCondition.Incompletable
                : JobCondition.Ongoing;
        });

        // finished progress
        modifyToil.AddFinishAction(() => {
            if (ended) return;

            if (!Weapon.TryGetComp<CompDynamicTraits>(out var compDynamicTraits)) return;

            PerformModifications(compDynamicTraits, modDataList);
            SoundDefOf.Replant_Complete.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
        });

        yield return modifyToil;
    }

    // === Helper ===
    private void PerformModifications(CompDynamicTraits comp, List<ModificationData> modList) {
        foreach (var modData in modList.Where(md => md.Type == ModificationType.Uninstall)) {
            comp.UninstallTrait(modData.Part);
            var moduleThing = ThingMaker.MakeThing(modData.ModuleDef);
            if (!pawn.inventory.innerContainer.TryAdd(moduleThing)) {
                GenPlace.TryPlaceThing(moduleThing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            }
        }

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