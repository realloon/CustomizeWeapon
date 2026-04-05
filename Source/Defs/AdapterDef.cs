using JetBrains.Annotations;
using Verse;

// ReSharper disable InconsistentNaming

namespace CWF;

public class AdapterDef : Def {
    private static readonly Dictionary<ThingDef, Dictionary<ThingDef, ModuleGraphicData>>
        ModuleOverridesByWeapon = new();

    [UsedImplicitly]
    public readonly GraphicData? graphicData;

    [UsedImplicitly]
    public readonly List<string> weaponTags = [];

    [UsedImplicitly]
    public readonly List<CompProperties> comps = [];

    [UsedImplicitly]
    public readonly List<AdapterModuleGraphicOverride> moduleGraphicOverrides = [];

    public override IEnumerable<string> ConfigErrors() {
        foreach (var item in base.ConfigErrors()) {
            yield return item;
        }

        if (graphicData == null) {
            yield return "graphicData is null";
        }

        var seenModuleDefs = new HashSet<ThingDef>();
        foreach (var moduleGraphicOverride in moduleGraphicOverrides) {
            if (moduleGraphicOverride.moduleDef == null) {
                yield return "moduleGraphicOverrides contains an entry with null moduleDef";
                continue;
            }

            if (moduleGraphicOverride.graphicData == null) {
                yield return
                    $"moduleGraphicOverrides for '{moduleGraphicOverride.moduleDef.defName}' has null graphicData";
            }

            if (moduleGraphicOverride.moduleDef.GetModExtension<TraitModuleExtension>() == null) {
                yield return
                    $"moduleGraphicOverrides references '{moduleGraphicOverride.moduleDef.defName}', but it is not a CWF module";
            }

            if (!seenModuleDefs.Add(moduleGraphicOverride.moduleDef)) {
                yield return
                    $"moduleGraphicOverrides contains duplicate moduleDef '{moduleGraphicOverride.moduleDef.defName}'";
            }
        }
    }

    internal static void Inject() {
        var allAdapters = DefDatabase<AdapterDef>.AllDefs;
        ModuleOverridesByWeapon.Clear();

        foreach (var adapter in allAdapters) {
            var weaponDef = DefDatabase<ThingDef>.GetNamed(adapter.defName, false);

            if (weaponDef == null) {
                Log.Warning($"[CWF] AdapterDef '{adapter.defName}' could not find a matching ThingDef to adapt.");
                continue;
            }

            AdaptWeapon(weaponDef, adapter);
        }
    }

    internal static bool TryGetModuleGraphicOverride(ThingDef weaponDef, ThingDef moduleDef,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out ModuleGraphicData? graphicData) {
        graphicData = null;

        return ModuleOverridesByWeapon.TryGetValue(weaponDef, out var overridesByModule)
               && overridesByModule.TryGetValue(moduleDef, out graphicData);
    }

    #region Helper

    private static void AdaptWeapon(ThingDef weaponDef, AdapterDef adapter) {
        // === graphicData ===
        weaponDef.graphicData.texPath = adapter.graphicData?.texPath;
        weaponDef.graphicData.graphicClass = adapter.graphicData?.graphicClass;
        weaponDef.graphicData.shaderType = adapter.graphicData?.shaderType;

        // === weaponTags ===
        if (adapter.weaponTags.Count > 0) {
            weaponDef.weaponTags ??= [];
            weaponDef.weaponTags = weaponDef.weaponTags.Union(adapter.weaponTags).ToList();
        }

        // === comps ===
        weaponDef.comps ??= [];

        // injector default comps
        TryAddComp(weaponDef, new CompProperties_Renamable());
        TryAddComp(weaponDef, new CompProperties_Colorable());
        TryAddComp(weaponDef, new CompProperties_AbilityProvider());

        // injector adapter inner comps
        foreach (var compToAdd in adapter.comps) {
            TryAddComp(weaponDef, compToAdd);
        }

        RegisterModuleGraphicOverrides(weaponDef, adapter);
    }

    private static void TryAddComp(ThingDef weaponDef, CompProperties newComp) {
        if (!weaponDef.comps.Any(comp => comp.compClass == newComp.compClass)) {
            weaponDef.comps.Add(newComp);
        }
    }

    private static void RegisterModuleGraphicOverrides(ThingDef weaponDef, AdapterDef adapter) {
        if (adapter.moduleGraphicOverrides.Count == 0) return;

        if (!ModuleOverridesByWeapon.TryGetValue(weaponDef, out var overridesByModule)) {
            overridesByModule = new Dictionary<ThingDef, ModuleGraphicData>();
            ModuleOverridesByWeapon[weaponDef] = overridesByModule;
        }

        foreach (var moduleGraphicOverride in adapter.moduleGraphicOverrides) {
            if (moduleGraphicOverride.moduleDef == null || moduleGraphicOverride.graphicData == null) {
                continue;
            }

            if (overridesByModule.ContainsKey(moduleGraphicOverride.moduleDef)) {
                Log.Warning(
                    $"[CWF] AdapterDef '{adapter.defName}' defines duplicate module graphic override for '{moduleGraphicOverride.moduleDef.defName}'. The last one wins.");
            }

            overridesByModule[moduleGraphicOverride.moduleDef] = moduleGraphicOverride.graphicData;
        }
    }

    #endregion
}

[UsedImplicitly]
public class AdapterModuleGraphicOverride {
    [UsedImplicitly]
    public ThingDef? moduleDef;

    [UsedImplicitly]
    public ModuleGraphicData? graphicData;
}
