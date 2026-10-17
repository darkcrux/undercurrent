using Undercurrent.Core.Data;
using Undercurrent.Core.Grid;

namespace Undercurrent.Core.Battles;

public sealed partial class Battle
{
    /// <summary>Which side of the defender an attack from <paramref name="from"/> lands on.</summary>
    public static AttackAngle AngleOf(Unit defender, Point from)
    {
        int rx = from.X - defender.Position.X, ry = from.Y - defender.Position.Y;
        int forward = defender.Facing.X * rx + defender.Facing.Y * ry;
        int side = Math.Abs(defender.Facing.X * ry - defender.Facing.Y * rx);
        if (forward > side) return AttackAngle.Front;
        if (-forward > side) return AttackAngle.Back;
        return AttackAngle.Flank;
    }

    /// <summary>
    /// The attack preview: hit, damage, crit and counter odds for <paramref name="attacker"/> using
    /// <paramref name="ability"/> on <paramref name="target"/> from <paramref name="from"/> (default: where it stands).
    /// </summary>
    public Forecast Forecast(Unit attacker, Unit target, AbilityDef ability, Point? from = null, bool channeled = false)
    {
        var origin = from ?? attacker.Position;
        var angle = AngleOf(target, origin);
        int angleBonus = angle switch
        {
            AttackAngle.Flank => Rules.FlankHitBonus,
            AttackAngle.Back => Rules.BackHitBonus,
            _ => 0,
        };

        int hit = target.Has(StatusId.Exposed)
            ? 100
            : Math.Clamp(attacker.Stats.Hit - (target.EffectiveSpeed(Rules) - Rules.HitSpeedPivot) * Rules.HitSpeedFactor + angleBonus, 0, 100);

        int bloodPrice = channeled ? 0 : BloodPriceHp(attacker, ability);
        double multiplier = 1 + Rules.StrainDamagePerStack * attacker.Strain;
        if (attacker.HasPassive(Passive.Backstab) && angle == AttackAngle.Back) multiplier *= Rules.BackstabMultiplier;
        if (bloodPrice > 0) multiplier *= Rules.BloodPriceDamageMultiplier;
        if (channeled) multiplier *= Rules.ChannelMultiplier;
        int damage = ability.DealsDamage ? Math.Max(1, (int)Math.Round(RawDamage(attacker, target, ability) * multiplier)) : 0;

        bool hidden = attacker.Has(StatusId.Hidden);
        int crit = (int)Math.Round(attacker.Stats.Luk * Rules.CritLuckFactor)
                   + (hidden ? Rules.HiddenCritBonus : 0)
                   - (attacker.Has(StatusId.Exposed) ? Rules.ExposedCritPenalty : 0);
        crit = Math.Clamp(crit, 0, 100);

        int height = Map.HeightAt(origin) - Map.HeightAt(target.Position);

        var reason = !ability.DealsDamage ? NoCounterReason.NotAnAttack
            : hidden ? NoCounterReason.AttackerHidden
            : target.Has(StatusId.Suppressed) ? NoCounterReason.TargetSuppressed
            : target.Has(StatusId.Stunned) ? NoCounterReason.TargetStunned
            : !canReachForCounter(target, origin) ? NoCounterReason.OutOfReach
            : NoCounterReason.None;
        bool canCounter = reason == NoCounterReason.None;

        int counter = 0, counterDamage = 0;
        bool parry = false;
        if (canCounter)
        {
            counter = angle switch
            {
                AttackAngle.Front => Rules.CounterChanceFront,
                AttackAngle.Flank => Rules.CounterChanceFlank,
                _ => Rules.CounterChanceBack,
            };
            if (height > 0) counter -= Rules.CounterHeightPenaltyPerLevel * height;
            if (target.Has(StatusId.Braced)) counter += Rules.BracedCounterBonus;
            counter = Math.Clamp(counter, 0, 100);
            parry = target.HasPassive(Passive.Parry);
            double factor = target.Has(StatusId.Braced) ? Rules.BracedCounterDamageFactor : Rules.CounterDamageFactor;
            counterDamage = Math.Max(1, (int)Math.Round(RawDamage(target, attacker, Data.Ability(BasicAttackId)) * factor));
        }

        return new Forecast(
            attacker, target, ability, origin, angle, angleBonus, hit, damage, crit,
            CritDamage: (int)Math.Round(damage * Rules.CritMultiplier),
            CanCounter: canCounter,
            NoCounterReason: reason,
            CounterChance: counter,
            CounterChanceOnCrit: (int)Math.Floor(counter * Rules.CritCounterFactor),
            CounterChanceOnMiss: canCounter ? (parry ? 100 : counter) : 0,
            CounterDamage: counterDamage,
            Parry: parry,
            HeightAdvantage: height,
            BloodPriceHp: bloodPrice,
            Channeled: channeled);

        static bool canReachForCounter(Unit defender, Point attackerAt)
        {
            int d = defender.Position.DistanceTo(attackerAt);
            return d >= defender.Reach.Min && d <= defender.Reach.Max;
        }
    }

    /// <summary>Damage before modifiers: ATK × power × 100 ÷ (100 + DEF), or ATN against RES for elemental abilities.</summary>
    public static double RawDamage(Unit attacker, Unit target, AbilityDef ability)
    {
        if (!ability.DealsDamage) return 0;
        bool weapon = ability.Type == DamageType.Weapon;
        int power = weapon ? attacker.Stats.Atk : attacker.Stats.Atn;
        int defense = weapon ? target.Stats.Def : target.Stats.Res;
        return power * ability.Power * 100.0 / (100 + defense);
    }
}
