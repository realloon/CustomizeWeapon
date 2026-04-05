using Verse;

namespace CWF;

public class ModificationJobSource : IExposable, ILoadReferenceable {
    private int _loadId = -1;

    public List<ModificationData> ModDataList = [];

    public void ExposeData() {
        Scribe_Collections.Look(ref ModDataList, "modDataList", LookMode.Deep);
    }

    public string GetUniqueLoadID() {
        if (_loadId < 0) {
            _loadId = Find.UniqueIDsManager.GetNextThingID();
        }

        return $"CWF_ModificationJobSource_{_loadId}";
    }
}