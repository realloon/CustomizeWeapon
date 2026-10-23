namespace CWF;

public class SpecDatabase {
    internal Spec Range;
    internal Spec BurstShotCount;
    internal Spec WarmupTime;
    internal Spec Cooldown;
    internal Spec Damage;
    internal Spec ArmorPenetration;
    internal Spec StoppingPower;
    internal Spec AccuracyTouch;
    internal Spec AccuracyShort;
    internal Spec AccuracyMedium;
    internal Spec AccuracyLong;
    internal Spec Mass;
    internal Spec MarketValue;
    internal Spec Dps;
    private Spec _ticksBetweenBurstShots;

    internal Thing PreviewWeapon { get; }

    internal IEnumerable<WeaponTraitDef> AllTraits =>
        (PreviewWeapon.TryGetComp<CompDynamicTraits>()?.Traits ?? [])
        .Concat(PreviewWeapon.TryGetComp<CompUniqueWeapon>()?.TraitsListForReading ?? []);

    // Note: downstream mod CWE transpiles `Verbs.First()` in the ctor, Recalculate, and GetComputedStoppingPower.
    public SpecDatabase(ModificationSession session) {
        PreviewWeapon = session.PreviewWeapon;

        // === Stat ===
        var weaponDef = PreviewWeapon.def;
        Mass = new Spec(weaponDef.GetStatValueAbstract(StatDefOf.Mass), true);
        MarketValue = new Spec(weaponDef.GetStatValueAbstract(StatDefOf.MarketValue));
        Cooldown = new Spec(weaponDef.GetStatValueAbstract(StatDefOf.RangedWeapon_Cooldown), true);
        AccuracyTouch = new Spec(weaponDef.GetStatValueAbstract(StatDefOf.AccuracyTouch));
        AccuracyShort = new Spec(weaponDef.GetStatValueAbstract(StatDefOf.AccuracyShort));
        AccuracyMedium = new Spec(weaponDef.GetStatValueAbstract(StatDefOf.AccuracyMedium));
        AccuracyLong = new Spec(weaponDef.GetStatValueAbstract(StatDefOf.AccuracyLong));

        // === Verb ===
        var weaponDefVerb = weaponDef.Verbs.First();
        Range = new Spec(weaponDefVerb.range);
        WarmupTime = new Spec(weaponDefVerb.warmupTime, true);
        BurstShotCount = new Spec(weaponDefVerb.burstShotCount);
        _ticksBetweenBurstShots = new Spec(weaponDefVerb.ticksBetweenBurstShots);

        // === Projectile ===
        if (weaponDefVerb.defaultProjectile?.projectile is { } projectile) {
            Damage = new Spec(projectile.GetDamageAmount(weaponDef, PreviewWeapon.Stuff));
            ArmorPenetration = new Spec(projectile.GetArmorPenetration());
            StoppingPower = new Spec(projectile.stoppingPower);
        }

        Dps = new Spec(CalculateDps(s => s.Raw));
        Recalculate();
    }

    public void Recalculate() {
        Mass.Dynamic = PreviewWeapon.GetStatValue(StatDefOf.Mass);
        MarketValue.Dynamic = PreviewWeapon.GetStatValue(StatDefOf.MarketValue);
        Cooldown.Dynamic = PreviewWeapon.GetStatValue(StatDefOf.RangedWeapon_Cooldown);
        AccuracyTouch.Dynamic = PreviewWeapon.GetStatValue(StatDefOf.AccuracyTouch);
        AccuracyShort.Dynamic = PreviewWeapon.GetStatValue(StatDefOf.AccuracyShort);
        AccuracyMedium.Dynamic = PreviewWeapon.GetStatValue(StatDefOf.AccuracyMedium);
        AccuracyLong.Dynamic = PreviewWeapon.GetStatValue(StatDefOf.AccuracyLong);

        var weaponVerb = PreviewWeapon.TryGetComp<CompEquippableAbilityReloadable>()?.PrimaryVerb
                         ?? PreviewWeapon.TryGetComp<CompEquippable>()?.PrimaryVerb;

        Range.Dynamic = Range.Raw * PreviewWeapon.GetStatValue(StatDefOf.RangedWeapon_RangeMultiplier);
        WarmupTime.Dynamic = WarmupTime.Raw * PreviewWeapon.GetStatValue(StatDefOf.RangedWeapon_WarmupMultiplier);
        BurstShotCount.Dynamic = weaponVerb?.BurstShotCount ?? -1; // harmony patched
        _ticksBetweenBurstShots.Dynamic = weaponVerb?.TicksBetweenBurstShots ?? -1; // harmony patched

        if (PreviewWeapon.def.Verbs.First().defaultProjectile?.projectile is { } projectile) {
            Damage.Dynamic = projectile.GetDamageAmount(PreviewWeapon);
            ArmorPenetration.Dynamic = projectile.GetArmorPenetration(PreviewWeapon);
            StoppingPower.Dynamic = GetComputedStoppingPower(); // harmony patched
        }

        Dps.Dynamic = CalculateDps(s => s.Dynamic);
    }

    private float CalculateDps(Func<Spec, float> get) {
        var burstCount = get(BurstShotCount);
        var burstSec = get(_ticksBetweenBurstShots) * (burstCount - 1) / 60f;
        var cycleSec = get(WarmupTime) + get(Cooldown) + burstSec;
        return cycleSec <= 0 ? 0f : get(Damage) * burstCount / cycleSec;
    }

    private float GetComputedStoppingPower() =>
        (PreviewWeapon.def.Verbs.First().defaultProjectile?.projectile.stoppingPower ?? 0.5f)
        + AllTraits.Sum(trait => trait.additionalStoppingPower);
}

public struct Spec(float raw, bool isLowerValueBetter = false) {
    public readonly float Raw = raw;
    public float Dynamic;
    public bool IsLowerValueBetter { get; } = isLowerValueBetter;
}
