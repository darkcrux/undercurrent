using Undercurrent.Core.Battles;
using Undercurrent.Core.Data;
using Undercurrent.Core.Grid;
using static Undercurrent.Core.Tests.TestWorld;

namespace Undercurrent.Core.Tests;

public static class AngleTests
{
    [Test]
    public static void Angles_follow_the_defenders_facing()
    {
        var b = Battle();
        var d = b.Add("trooper", Team.Enemy, 3, 3, Point.North);
        Assert.Equal(AttackAngle.Front, Core.Battles.Battle.AngleOf(d, new Point(3, 2)), "straight ahead");
        Assert.Equal(AttackAngle.Flank, Core.Battles.Battle.AngleOf(d, new Point(4, 3)), "right side");
        Assert.Equal(AttackAngle.Flank, Core.Battles.Battle.AngleOf(d, new Point(2, 3)), "left side");
        Assert.Equal(AttackAngle.Back, Core.Battles.Battle.AngleOf(d, new Point(3, 4)), "behind");
        Assert.Equal(AttackAngle.Flank, Core.Battles.Battle.AngleOf(d, new Point(4, 2)), "front diagonal");
        Assert.Equal(AttackAngle.Back, Core.Battles.Battle.AngleOf(d, new Point(3, 6)), "far behind");
    }
}

public static class HitAndDamageTests
{
    [Test]
    public static void Hit_chance_uses_hit_minus_speed_and_angle_bonus()
    {
        var b = Battle();
        var v = b.Add("vanguard", Team.Player, 3, 4);
        var t = b.Add("cutthroat", Team.Enemy, 3, 3, Point.South); // SPD 17, weight 0
        var atk = Ability("attack");
        // 90 - (17 - 10) * 2 = 76 from the front
        Assert.Equal(76, b.Forecast(v, t, atk).HitChance, "front hit");
        t.Facing = Point.East;
        Assert.Equal(86, b.Forecast(v, t, atk).HitChance, "flank hit");
        t.Facing = Point.North;
        Assert.Equal(96, b.Forecast(v, t, atk).HitChance, "back hit");
    }

    [Test]
    public static void Exposed_targets_are_always_hit()
    {
        var b = Battle();
        var v = b.Add("vanguard", Team.Player, 3, 4);
        var t = b.Add("cutthroat", Team.Enemy, 3, 3, Point.South);
        t.SetStatus(StatusId.Exposed, 2);
        Assert.Equal(100, b.Forecast(v, t, Ability("attack")).HitChance);
    }

    [Test]
    public static void Damage_uses_percent_reduction()
    {
        var b = Battle();
        var v = b.Add("vanguard", Team.Player, 3, 4);       // ATK 30
        var t = b.Add("trooper", Team.Enemy, 3, 3);          // DEF 26
        // 30 * 100 / 126 = 23.8
        Assert.Equal(24, b.Forecast(v, t, Ability("attack")).Damage, "attack damage");
        var e = b.Add("elementalist", Team.Player, 2, 4);   // ATN 32, vs RES 12
        // spark 0.7: 32 * 0.7 * 100 / 112 = 20.0
        Assert.Equal(20, b.Forecast(e, t, Ability("spark")).Damage, "spark damage");
    }

    [Test]
    public static void Crits_double_damage_and_hidden_adds_crit_chance()
    {
        var b = Battle();
        var c = b.Add("cutthroat", Team.Enemy, 3, 3, Point.South); // LUK 14
        var v = b.Add("vanguard", Team.Player, 3, 4);
        var f = b.Forecast(c, v, Ability("attack"));
        Assert.Equal(7, f.CritChance, "base crit");
        Assert.Equal(f.Damage * 2, f.CritDamage, "crit damage");
        c.SetStatus(StatusId.Hidden, 3);
        Assert.Equal(32, b.Forecast(c, v, Ability("attack")).CritChance, "hidden crit");
    }

    [Test]
    public static void Backstab_adds_damage_from_behind_only()
    {
        var b = Battle();
        var c = b.Add("cutthroat", Team.Enemy, 3, 3);
        var v = b.Add("vanguard", Team.Player, 3, 4, Point.North);
        int front = b.Forecast(c, v, Ability("attack")).Damage;
        v.Facing = Point.South;
        int back = b.Forecast(c, v, Ability("attack")).Damage;
        Assert.Equal((int)Math.Round(Core.Battles.Battle.RawDamage(c, v, Ability("attack")) * 1.3), back, "backstab damage");
        Assert.True(back > front, "back damage is higher");
    }

    [Test]
    public static void Guns_gain_range_from_high_ground()
    {
        var map = MapWithHeights(
            new[] { 2, 0, 0, 0, 0, 0, 0 },
            new[] { 0, 0, 0, 0, 0, 0, 0 });
        var b = Battle(map: map);
        var g = b.Add("gunner", Team.Player, 0, 0, Point.East); // gun reach 2-4
        b.Add("trooper", Team.Enemy, 5, 0);
        Assert.True(b.InRange(g, Ability("attack"), g.Position, new Point(5, 0)), "5 tiles from 2 levels up");
        Assert.False(b.InRange(g, Ability("attack"), g.Position, new Point(6, 0)), "6 tiles is still too far");
    }
}

public static class CounterTests
{
    private static (Battle B, Unit Attacker, Unit Defender) Duel(Point defenderFacing)
    {
        var b = Battle();
        var a = b.Add("trooper", Team.Enemy, 3, 2);
        var d = b.Add("bloodcaller", Team.Player, 3, 3, defenderFacing);
        return (b, a, d);
    }

    [Test]
    public static void Counter_chance_is_75_front_50_flank_25_back()
    {
        var (b, a, d) = Duel(Point.North);
        Assert.Equal(75, b.Forecast(a, d, Ability("attack")).CounterChance, "front");
        d.Facing = Point.East;
        Assert.Equal(50, b.Forecast(a, d, Ability("attack")).CounterChance, "flank");
        d.Facing = Point.South;
        Assert.Equal(25, b.Forecast(a, d, Ability("attack")).CounterChance, "back");
    }

    [Test]
    public static void High_ground_lowers_counter_chance_10_per_level()
    {
        var map = MapWithHeights(
            new[] { 0, 0, 0 },
            new[] { 0, 2, 0 },
            new[] { 0, 0, 0 });
        var b = Battle(map: map);
        var a = b.Add("trooper", Team.Enemy, 1, 1);
        var d = b.Add("bloodcaller", Team.Player, 1, 2, Point.North);
        var f = b.Forecast(a, d, Ability("attack"));
        Assert.Equal(2, f.HeightAdvantage, "height advantage");
        Assert.Equal(55, f.CounterChance, "counter chance");
    }

    [Test]
    public static void Braced_raises_counter_chance_and_damage()
    {
        var (b, a, d) = Duel(Point.North);
        int normalDamage = b.Forecast(a, d, Ability("attack")).CounterDamage;
        d.SetStatus(StatusId.Braced, 1);
        var f = b.Forecast(a, d, Ability("attack"));
        Assert.Equal(100, f.CounterChance, "braced front counter (75 + 25)");
        Assert.True(f.CounterDamage > normalDamage, "braced counter hits harder");
    }

    [Test]
    public static void Crit_halves_and_parry_guarantees_on_miss()
    {
        var b = Battle();
        var a = b.Add("trooper", Team.Enemy, 3, 2);
        var v = b.Add("vanguard", Team.Player, 3, 3, Point.East); // flank: 50
        var f = b.Forecast(a, v, Ability("attack"));
        Assert.Equal(50, f.CounterChance, "flank");
        Assert.Equal(25, f.CounterChanceOnCrit, "on crit");
        Assert.Equal(100, f.CounterChanceOnMiss, "on miss with Parry");
        Assert.True(f.Parry, "parry flag");
    }

    [Test]
    public static void No_counter_when_hidden_suppressed_stunned_or_out_of_reach()
    {
        var (b, a, d) = Duel(Point.North);
        a.SetStatus(StatusId.Hidden, 3);
        Assert.Equal(NoCounterReason.AttackerHidden, b.Forecast(a, d, Ability("attack")).NoCounterReason, "hidden");
        a.RemoveStatus(StatusId.Hidden);

        d.SetStatus(StatusId.Suppressed, 1);
        Assert.Equal(NoCounterReason.TargetSuppressed, b.Forecast(a, d, Ability("attack")).NoCounterReason, "suppressed");
        d.RemoveStatus(StatusId.Suppressed);

        d.SetStatus(StatusId.Stunned, 1);
        Assert.Equal(NoCounterReason.TargetStunned, b.Forecast(a, d, Ability("attack")).NoCounterReason, "stunned");
        d.RemoveStatus(StatusId.Stunned);

        var g = b.Add("gunner", Team.Player, 6, 3);           // gun reach 2-4: can't counter at 1
        var near = b.Add("cutthroat", Team.Enemy, 6, 2);
        Assert.Equal(NoCounterReason.OutOfReach, b.Forecast(near, g, Ability("attack")).NoCounterReason, "gun at point blank");
        Assert.Equal(NoCounterReason.OutOfReach, b.Forecast(g, a, Ability("attack"), new Point(3, 5)).NoCounterReason, "melee at range");
        Assert.Equal(0, b.Forecast(g, a, Ability("attack"), new Point(3, 5)).CounterChance, "chance 0");
    }

    [Test]
    public static void A_landed_counter_damages_the_attacker()
    {
        var b = Battle(ScriptedRandom.AlwaysSucceed());
        var a = b.Add("trooper", Team.Enemy, 3, 2).First();
        var d = b.Add("vanguard", Team.Player, 3, 3, Point.North);
        b.Start();
        Assert.Equal(a, b.Active, "attacker acts first");
        int hpBefore = a.Hp;
        b.UseAbility("attack", d.Position);
        Assert.True(b.History.OfType<Countered>().Any(), "a counter happened");
        Assert.True(a.Hp < hpBefore, "attacker took counter damage");
    }
}
