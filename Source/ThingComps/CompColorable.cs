using UnityEngine;
using RimWorld;
using Verse;

namespace CWF;

public class CompColorable : ThingComp {
    private ColorDef? _colorDef;

    public ColorDef? ColorDef {
        get => _colorDef;
        private set {
            if (_colorDef == value) return;

            _colorDef = value;
            parent.Notify_ColorChanged();
        }
    }

    private void RandomizeColor() {
        var randomColor = DefDatabase<ColorDef>.AllDefs
            .Where(c => c.colorType == ColorType.Weapon && c.randomlyPickable)
            .RandomElementWithFallback();

        if (randomColor != null) {
            ColorDef = randomColor;
        }
    }

    // Override default color if defined.
    public override Color? ForceColor() {
        if (ColorDef == null) return null;

        return parent.TryGetComp<CompDynamicGraphic>() != null
            ? Color.white
            : ColorDef.color;
    }

    public override void PostPostMake() {
        base.PostPostMake();

        if (Scribe.mode != LoadSaveMode.Inactive || ColorDef != null) return;

        RandomizeColor();
    }

    public override void PostExposeData() {
        base.PostExposeData();
        Scribe_Defs.Look(ref _colorDef, "colorDef");
    }

    // debug
    public override IEnumerable<Gizmo> CompGetGizmosExtra() {
        foreach (var g in base.CompGetGizmosExtra()) {
            yield return g;
        }

        if (!Prefs.DevMode || parent.IsForbidden(Faction.OfPlayer)) yield break;

        yield return new Command_Action {
            defaultLabel = "Dev: Randomize color",
            action = RandomizeColor
        };
    }
}
