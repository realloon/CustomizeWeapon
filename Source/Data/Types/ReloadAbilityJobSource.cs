using RimWorld;
using Verse;

namespace CWF;

public class ReloadAbilityJobSource : IExposable, ILoadReferenceable {
    private int _loadId = -1;

    public AbilityDef AbilityDef = null!;

    public void ExposeData() {
        Scribe_Defs.Look(ref AbilityDef, "abilityDef");
    }

    public string GetUniqueLoadID() {
        if (_loadId < 0) {
            _loadId = Find.UniqueIDsManager.GetNextThingID();
        }

        return $"CWF_ReloadAbilityJobSource_{_loadId}";
    }
}