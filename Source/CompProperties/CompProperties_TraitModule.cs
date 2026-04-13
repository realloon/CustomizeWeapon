using JetBrains.Annotations;
using System.Text;
using RimWorld;
using Verse;
using CWF.Extensions;

namespace CWF;

// ReSharper disable once InconsistentNaming
[UsedImplicitly]
public class CompProperties_TraitModule : CompProperties {
    public CompProperties_TraitModule() => compClass = typeof(CompTraitModule);

    public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req) {
        var ext = req.BuildableDef.GetModExtension<TraitModuleExtension>();
        if (ext?.weaponTraitDef == null) yield break;

        var traitDef = ext.weaponTraitDef;
        var part = ext.part;

        var sb = new StringBuilder();
        var effect = traitDef.GetTraitEffect();

        if (effect.Any()) {
            sb.AppendLine("CWF_ModuleEffectsDesc".Translate(traitDef.Named("MODULE")) + ":");
            sb.AppendLine();
            sb.AppendLine(effect);
        }

        yield return new StatDrawEntry(
            CWF_DefOf.CWF_WeaponModule,
            "CWF_ModuleEffects".Translate(),
            traitDef.LabelCap,
            sb.ToString().TrimEndNewlines(),
            1000
        );

        yield return new StatDrawEntry(
            CWF_DefOf.CWF_WeaponModule,
            "CWF_PartOf".Translate(),
            part.LabelCap,
            "CWF_PartOf".Translate() + ": " + part.LabelCap,
            999
        );

        yield return new StatDrawEntry(
            CWF_DefOf.CWF_WeaponModule,
            "CWF_Rarity".Translate(),
            ((ThingDef)req.BuildableDef).GetRarityLabel(),
            "CWF_RarityDesc".Translate(),
            998
        );
    }

    public override void PostLoadSpecial(ThingDef parent) {
        var ext = parent.GetModExtension<TraitModuleExtension>();
        if (ext?.weaponTraitDef.description != null) {
            parent.description = ext.weaponTraitDef.description;
        }
    }
}