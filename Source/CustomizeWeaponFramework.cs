global using Verse;
global using RimWorld;
using JetBrains.Annotations;
using HarmonyLib;

namespace CWF;

[UsedImplicitly]
[StaticConstructorOnStartup]
public class CustomizeWeaponFramework {
    static CustomizeWeaponFramework() {
        var harmony = new Harmony("Vortex.CustomizeWeaponFramework");
        harmony.PatchAll();

        AdapterDef.Inject();
        ModuleDatabase.BuildCacheAndInject();
    }
}