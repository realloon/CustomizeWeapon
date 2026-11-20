using Verse.AI;

namespace CWF.Controllers;

public class JobDispatcher(Thing weapon) {
    public void Dispatch(List<ModificationData> netChanges) {
        if (netChanges.Count == 0) return;

        var ownerPawn = weapon.ParentHolder switch {
            Pawn_EquipmentTracker equipment => equipment.pawn,
            Pawn_InventoryTracker inventory => inventory.pawn,
            _ => null
        };

        if (ownerPawn != null) {
            DispatchFieldModificationJobs(ownerPawn, netChanges); // Equip
        } else {
            DispatchHaulModificationJob(netChanges); // Ground
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

        foreach (var change in netChanges) {
            if (change.Type != ModificationType.Install) continue;

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
        job.count = 1;

        // fill queue only when it needs haul
        if (modulesToHaul.Count != 0) {
            job.targetQueueB = [.. modulesToHaul.Select(thing => new LocalTargetInfo(thing))];
        }

        job.source = new ModificationJobSource { ModDataList = netChanges };
        job.playerForced = true;
        bestPawn.jobs.ClearQueuedJobs();
        bestPawn.jobs.StartJob(job, JobCondition.InterruptForced, tag: JobTag.Misc);

        Messages.Message("CWF_ModificationJobDispatched"
                .Translate(bestPawn.Named("PAWN"), weapon.Named("WEAPON")),
            new LookTargets(bestPawn, weapon), MessageTypeDefOf.PositiveEvent);
    }

    private Thing? FindBestAvailableModuleFor(ModificationData change, Pawn pawn) => GenClosest.ClosestThingReachable(
        weapon.Position,
        weapon.Map,
        ThingRequest.ForDef(change.ModuleDef),
        PathEndMode.ClosestTouch,
        TraverseParms.For(pawn),
        validator: t => !t.IsForbidden(pawn) && !t.IsBurning() && pawn.CanReserve(t)
    );

    private static Pawn? FindBestPawnForJob(IntVec3 jobLocation, Map map) => map.mapPawns.FreeColonistsSpawned
        .Where(p => !p.Downed && !p.Drafted && p.workSettings.WorkIsActive(WorkTypeDefOf.Crafting) &&
                    p.health.capacities.CanBeAwake)
        .MinBy(p => p.Position.DistanceToSquared(jobLocation));
}