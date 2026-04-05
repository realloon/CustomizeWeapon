using RimWorld;
using Verse;

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

    private readonly Thing _previewWeapon;
    private readonly CompDynamicTraits? _previewDynamicTraits;

    private enum Mode {
        Raw,
        Dynamic
    }

    public bool IsMeleeWeapon => _previewWeapon.def.IsMeleeWeapon;

    public SpecDatabase(WeaponModificationSession session) {
        _previewWeapon = session.PreviewWeapon;
        _previewDynamicTraits = _previewWeapon.TryGetComp<CompDynamicTraits>();

        // Raw values
        // === Stat ===
        var weaponDef = _previewWeapon.def;
        Mass = new Spec(weaponDef.GetStatValueAbstract(StatDefOf.Mass), true);
        MarketValue = new Spec(weaponDef.GetStatValueAbstract(StatDefOf.MarketValue));
        Cooldown = new Spec(weaponDef.GetStatValueAbstract(StatDefOf.RangedWeapon_Cooldown), true);
        AccuracyTouch = new Spec(weaponDef.GetStatValueAbstract(StatDefOf.AccuracyTouch));
        AccuracyShort = new Spec(weaponDef.GetStatValueAbstract(StatDefOf.AccuracyShort));
        AccuracyMedium = new Spec(weaponDef.GetStatValueAbstract(StatDefOf.AccuracyMedium));
        AccuracyLong = new Spec(weaponDef.GetStatValueAbstract(StatDefOf.AccuracyLong));

        // === Verb ===
        var weaponDefVerb = weaponDef.Verbs.FirstOrFallback();
        if (weaponDefVerb != null) {
            Range = new Spec(weaponDefVerb.range);
            WarmupTime = new Spec(weaponDefVerb.warmupTime, true);
            BurstShotCount = new Spec(weaponDefVerb.burstShotCount);
            _ticksBetweenBurstShots = new Spec(weaponDefVerb.ticksBetweenBurstShots);

            // === Projectile ===
            var weaponDefProjectile = weaponDefVerb.defaultProjectile?.projectile;
            if (weaponDefProjectile != null) {
                Damage = new Spec(weaponDefProjectile.GetDamageAmount(weaponDef, _previewWeapon.Stuff));
                ArmorPenetration = new Spec(weaponDefProjectile.GetArmorPenetration());
                StoppingPower = new Spec(weaponDefProjectile.stoppingPower);
            }
        }

        Dps = new Spec(CalculateDps(Mode.Raw));

        Recalculate(); // init calc
    }

    public void Recalculate() {
        // === Stat ===
        Mass.Dynamic = _previewWeapon.GetStatValue(StatDefOf.Mass);
        MarketValue.Dynamic = _previewWeapon.GetStatValue(StatDefOf.MarketValue);
        Cooldown.Dynamic = _previewWeapon.GetStatValue(StatDefOf.RangedWeapon_Cooldown);
        AccuracyTouch.Dynamic = _previewWeapon.GetStatValue(StatDefOf.AccuracyTouch);
        AccuracyShort.Dynamic = _previewWeapon.GetStatValue(StatDefOf.AccuracyShort);
        AccuracyMedium.Dynamic = _previewWeapon.GetStatValue(StatDefOf.AccuracyMedium);
        AccuracyLong.Dynamic = _previewWeapon.GetStatValue(StatDefOf.AccuracyLong);

        // === Verb ===
        var weaponVerb = _previewWeapon.TryGetComp<CompEquippableAbilityReloadable>()?.PrimaryVerb
                         ?? _previewWeapon.TryGetComp<CompEquippable>()?.PrimaryVerb;

        Range.Dynamic = Range.Raw * _previewWeapon.GetStatValue(StatDefOf.RangedWeapon_RangeMultiplier);
        WarmupTime.Dynamic = WarmupTime.Raw * _previewWeapon.GetStatValue(StatDefOf.RangedWeapon_WarmupMultiplier);
        BurstShotCount.Dynamic = weaponVerb?.BurstShotCount ?? -1; // harmony patched
        _ticksBetweenBurstShots.Dynamic = weaponVerb?.TicksBetweenBurstShots ?? -1; // harmony patched

        // === Projectile ===
        var weaponDefProjectile = _previewWeapon.def.Verbs.FirstOrFallback()?.defaultProjectile?.projectile;
        if (weaponDefProjectile != null) {
            Damage.Dynamic = weaponDefProjectile.GetDamageAmount(_previewWeapon);
            ArmorPenetration.Dynamic = weaponDefProjectile.GetArmorPenetration(_previewWeapon);
            StoppingPower.Dynamic = GetComputedStoppingPower(); // harmony patched
        }

        Dps.Dynamic = CalculateDps(Mode.Dynamic);
    }

    // === Helper ===
    private float CalculateDps(Mode mode) {
        var damage = mode == Mode.Raw ? Damage.Raw : Damage.Dynamic;
        var burstCount = mode == Mode.Raw ? BurstShotCount.Raw : BurstShotCount.Dynamic;
        var ticksBetweenShots = mode == Mode.Raw
            ? _ticksBetweenBurstShots.Raw
            : _ticksBetweenBurstShots.Dynamic;
        var warmup = mode == Mode.Raw ? WarmupTime.Raw : WarmupTime.Dynamic;
        var cooldown = mode == Mode.Raw ? Cooldown.Raw : Cooldown.Dynamic;

        var totalDamage = damage * burstCount;
        var totalBurstSec = ticksBetweenShots * (burstCount - 1) / 60f;
        var totalCycleSec = warmup + cooldown + totalBurstSec;
        return totalCycleSec <= 0 ? 0f : totalDamage / totalCycleSec;
    }

    private float GetComputedStoppingPower() {
        var basePower = _previewWeapon.def.Verbs
            .FirstOrFallback()?.defaultProjectile?.projectile.stoppingPower ?? 0.5f;

        // CompUniqueWeapon
        if (_previewWeapon.TryGetComp<CompUniqueWeapon>(out var compUniqueWeapon)) {
            basePower += compUniqueWeapon.TraitsListForReading.Sum(trait => trait.additionalStoppingPower);
        }

        // CompDynamicTraits
        var additional = _previewDynamicTraits?.Traits.Sum(traitDef => traitDef.additionalStoppingPower) ?? 0;

        return basePower + additional;
    }
}

internal struct Spec {
    public readonly float Raw;
    public float Dynamic = 0f;
    public bool IsLowerValueBetter { get; }

    internal Spec(float raw, bool isLowerValueBetter = false) {
        Raw = raw;
        IsLowerValueBetter = isLowerValueBetter;
    }
}