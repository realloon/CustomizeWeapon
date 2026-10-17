using UnityEngine;

namespace CWF;

public class CompColorable : ThingComp {
    private ColorDef? _colorDef;
    private static IReadOnlyList<ColorDef>? _availableColors;

    public static IReadOnlyList<ColorDef> AvailableColors => _availableColors ??= [
        .. DefDatabase<ColorDef>.AllDefs
            .Where(c => c.defName.StartsWith("CWF_Color_"))
            .OrderBy(c => c.label)
    ];

    public ColorDef? ColorDef {
        get => _colorDef;
        set {
            if (_colorDef == value) return;

            _colorDef = value;
            parent.Notify_ColorChanged();
        }
    }

    // Override default color if defined.
    public override Color? ForceColor() {
        if (ColorDef == null) {
            return null;
        }

        return parent.TryGetComp<CompDynamicGraphic>() != null
            ? Color.white
            : ColorDef.color;
    }

    public override void PostPostMake() {
        base.PostPostMake();

        if (Scribe.mode != LoadSaveMode.Inactive || ColorDef != null) return;

        ColorDef = DefDatabase<ColorDef>.GetNamed("CWF_Color_Gray");
    }

    public override void PostExposeData() {
        base.PostExposeData();
        Scribe_Defs.Look(ref _colorDef, "colorDef");
    }
}