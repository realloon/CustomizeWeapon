using RimWorld;
using Verse;
using Verse.AI;

namespace CWF.Controllers;

public class JobDispatcher(Thing weapon) {
    public void Dispatch(List<ModificationData> netChanges) {
        if (!Enumerable.Any(netChanges)) return;

        var ownerPawn = weapon.ParentHolder switch {
            Pawn_EquipmentTracker equipment => equipment.pawn,
            Pawn_InventoryTracker inventory => inventory.pawn,
            _ => null
        };

        if (ownerPawn != null) {
            // Equip
            DispatchFieldModificationJobs(ownerPawn, netChanges);
        } else {
            // Ground
            DispatchHaulModificationJob(netChanges);
        }
    }

    // === Helper ===
    private void DispatchFieldModificationJobs(Pawn ownerPawn, List<ModificationData> netChanges) {
        var job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("CWF_ModifyWeaponSelf"), weapon);
        job.source = new ModificationJobSource { ModDataList = netChanges };
        job.playerForced = true;
        ownerPawn.jobs.ClearQueuedJobs();
        ownerPawn.jobs.StartJob(job, JobCondition.InterruptForced, tag: JobTag.Misc);
    }

    private void DispatchHaulModificationJob(List<ModificationData> netChanges) {
        var bestPawn = FindBestPawnForJob(weapon.Position, weapon.Map);
        if (bestPawn == null) {
            Messages.Message("CWF_NoColonistToModifyWeapon".Translate(), MessageTypeDefOf.NeutralEvent, false);
            return;
        }

        var modulesToHaul = new List<Thing>();
        var installChanges = netChanges
            .Where(c => c.Type == ModificationType.Install)
            .ToList();

        foreach (var change in installChanges) {
            var module = FindBestAvailableModuleFor(change, bestPawn);
            if (module == null) {
                Messages.Message("CWF_CannotFindModuleForModification"
                        .Translate(change.ModuleDef.Named("MODULE")),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            modulesToHaul.Add(module);
        }

        // create a big job merged all modification
        var job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("CWF_ModifyWeaponHaul"), weapon);

        // fill queue only when it needs haul
        if (Enumerable.Any(modulesToHaul)) {
            job.targetQueueB = modulesToHaul.Select(t => new LocalTargetInfo(t)).ToList();
        }

        job.source = new ModificationJobSource { ModDataList = netChanges };
        job.playerForced = true;
        bestPawn.jobs.ClearQueuedJobs();
        bestPawn.jobs.StartJob(job, JobCondition.InterruptForced, tag: JobTag.Misc);

        Messages.Message("CWF_ModificationJobDispatched"
                .Translate(bestPawn.Named("PAWN"), weapon.Named("WEAPON")),
            new LookTargets(bestPawn, weapon), MessageTypeDefOf.PositiveEvent);
    }

    private Thing? FindBestAvailableModuleFor(ModificationData change, Pawn pawn) {
        if (change.Type != ModificationType.Install) return null;

        return GenClosest.ClosestThingReachable(
            weapon.Position,
            weapon.Map,
            ThingRequest.ForDef(change.ModuleDef),
            PathEndMode.ClosestTouch,
            TraverseParms.For(pawn),
            validator: t => !t.IsForbidden(pawn) && !t.IsBurning() && pawn.CanReserve(t)
        );
    }

    private static Pawn? FindBestPawnForJob(IntVec3 jobLocation, Map map) {
        return map.mapPawns.FreeColonistsSpawned
            .Where(p => !p.Downed && !p.Drafted && p.workSettings.WorkIsActive(WorkTypeDefOf.Crafting) &&
                        p.health.capacities.CanBeAwake)
            .OrderBy(p => p.Position.DistanceToSquared(jobLocation))
            .FirstOrFallback();
    }
}