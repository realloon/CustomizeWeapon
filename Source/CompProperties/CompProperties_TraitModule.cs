using JetBrains.Annotations;
using Verse;

namespace CWF;

/// <summary>
/// Properties for the `CompTraitModule` component. This is attached to the module *item* ThingDef (not the weapon) to provide specific in-game interactions, such as the 'Pick Up' float menu option.
/// </summary>
[UsedImplicitly]
// ReSharper disable once InconsistentNaming
public class CompProperties_TraitModule : CompProperties {
    public CompProperties_TraitModule() => compClass = typeof(CompTraitModule);
}