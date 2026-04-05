using RimWorld;

// ReSharper disable InconsistentNaming

namespace CWF;

[DefOf]
public class CWF_DefOf {
    public static readonly StatCategoryDef CWF_WeaponModule = null!;

    static CWF_DefOf() {
        DefOfHelper.EnsureInitializedInCtor(typeof(CWF_DefOf));
    }
}