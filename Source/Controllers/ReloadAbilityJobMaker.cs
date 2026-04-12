using Verse;
using Verse.AI;

namespace CWF.Controllers;

public static class ReloadAbilityJobMaker {
    public static Job Make(ReloadableAbility reloadable, List<Thing> chosenResources, bool playerForced) {
        var job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("CWF_ReloadAbility"), reloadable.ReloadableThing);
        job.targetQueueB = chosenResources.Select(thing => new LocalTargetInfo(thing)).ToList();
        job.count = chosenResources.Sum(thing => thing.stackCount);
        job.count = Math.Min(job.count, reloadable.MaxAmmoNeeded(allowForcedReload: true));
        job.source = new ReloadAbilityJobSource { AbilityDef = reloadable.AbilityDef };
        job.playerForced = playerForced;
        return job;
    }
}