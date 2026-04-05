using JetBrains.Annotations;
using Verse;

namespace CWF;

[UsedImplicitly]
// ReSharper disable once InconsistentNaming
public class CompProperties_TraitModule : CompProperties {
    public CompProperties_TraitModule() => compClass = typeof(CompTraitModule);
}
