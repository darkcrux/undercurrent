using Undercurrent.Core.Battles;
using Undercurrent.Core.Data;
using Undercurrent.Core.Grid;
using static Undercurrent.Core.Tests.TestWorld;

namespace Undercurrent.Core.Tests;

public static class BloodPriceTests
{
    [Test]
    public static void Short_resonance_is_paid_in_hp_for_bonus_damage_and_strain()
    {
        var b = Battle(ScriptedRandom.AlwaysFail());
        var bc = b.Add("bloodcaller", Team.Player, 3, 5).First();
        var t = b.Add("trooper", Team.Enemy, 3, 2);
        b.Start();
        bc.Resource = 10; // lance costs 30: 20 short, 20 * 0.6 = 12 HP

        var f = b.Forecast(bc, t, Ability("lance"));
        Assert.Equal(12, f.BloodPriceHp, "blood price");
        int plain = (int)Math.Round(Core.Battles.Battle.RawDamage(bc, t, Ability("lance")));
        Assert.Equal((int)Math.Round(Core.Battles.Battle.RawDamage(bc, t, Ability("lance")) * 1.25), f.Damage, "boosted damage");
        Assert.True(f.Damage > plain, "boost applies");

        int hp = bc.Hp;
        b.UseAbility("lance", t.Position);
        Assert.Equal(hp - 12, bc.Hp, "hp paid");
        Assert.Equal(0, bc.Resource, "resonance emptied");
        Assert.Equal(1, bc.Strain, "strain");
    }

    [Test]
    public static void Fifth_strain_stack_causes_burnout_which_blocks_abilities_but_not_attacks()
    {
        var b = Battle(ScriptedRandom.AlwaysFail());
        var bc = b.Add("bloodcaller", Team.Player, 3, 5).First();
        var t = b.Add("trooper", Team.Enemy, 3, 2);
        b.Start();
        bc.Resource = 0;
        bc.Strain = 4;
        b.UseAbility("lance", t.Position);
        Assert.True(bc.Has(StatusId.Burnout), "burnout");
        Assert.True(b.History.OfType<BurnoutStarted>().Any(), "burnout event");

        b.EndTurn();
        b.PassUntil(bc);
        Assert.Equal(UseBlock.Burnout, b.CanUse("hemorrhage"), "abilities blocked");
        Assert.Equal(UseBlock.None, b.CanUse("attack"), "attack still allowed");
    }

    [Test]
    public static void Each_strain_stack_adds_ten_percent_damage()
    {
        var b = Battle();
        var bc = b.Add("bloodcaller", Team.Player, 3, 5);
        var t = b.Add("trooper", Team.Enemy, 3, 2);
        bc.Resource = 100;
        double raw = Core.Battles.Battle.RawDamage(bc, t, Ability("lance"));
        bc.Strain = 3;
        Assert.Equal((int)Math.Round(raw * 1.3), b.Forecast(bc, t, Ability("lance")).Damage);
    }

    [Test]
    public static void Strain_decays_on_turns_without_paying_hp()
    {
        var b = Battle();
        var bc = b.Add("bloodcaller", Team.Player, 3, 5).First();
        b.Add("trooper", Team.Enemy, 3, 0);
        b.Start();
        bc.Strain = 2;
        b.EndTurn();
        Assert.Equal(1, bc.Strain);
    }
}

public static class ChannelTests
{
    [Test]
    public static void Channeled_spell_resolves_next_turn_at_higher_power_with_sure_status()
    {
        var b = Battle(new ScriptedRandom(0, 99.9)); // hit, then fail every other roll
        var el = b.Add("elementalist", Team.Player, 3, 6).First();
        var t = b.Add("trooper", Team.Enemy, 3, 3);
        b.Start();
        el.Resource = 100;
        int expected = (int)Math.Round(Core.Battles.Battle.RawDamage(el, t, Ability("frost")) * 1.5);

        b.UseAbility("frost", t.Position, channel: true);
        Assert.NotNull(el.Channeling, "channeling");
        Assert.Equal(70, el.Resource, "focus paid up front");
        b.EndTurn();
        b.PassUntil(el);

        var hit = b.History.OfType<AttackHit>().LastOrDefault();
        Assert.NotNull(hit, "the channel resolved");
        Assert.Equal(expected, hit!.Damage, "x1.5 damage");
        Assert.True(t.Has(StatusId.Frozen) || b.History.OfType<StatusApplied>().Any(s => s.Status == StatusId.Frozen), "frozen for sure");
        Assert.True(b.HasActed, "resolving uses the turn's action");
    }

    [Test]
    public static void A_hit_interrupts_the_channel()
    {
        var b = Battle(ScriptedRandom.AlwaysSucceed());
        var el = b.Add("elementalist", Team.Player, 3, 4).First();
        var t = b.Add("trooper", Team.Enemy, 3, 3);
        b.Start();
        el.Resource = 100;
        b.UseAbility("flame", new Point(3, 2), channel: true);
        t.Wait = 99; // the trooper acts before the channel resolves
        b.EndTurn();
        Assert.Equal(t, b.Active, "trooper's turn");
        b.UseAbility("attack", el.Position);
        Assert.True(el.Channeling is null, "channel cleared");
        Assert.True(b.History.OfType<ChannelInterrupted>().Any(), "interrupted event");
    }
}

public static class DeployableTests
{
    [Test]
    public static void Turret_fires_at_the_nearest_enemy_at_the_start_of_the_owners_turn()
    {
        var b = Battle();
        var g = b.Add("gunner", Team.Player, 3, 6).First();
        var t = b.Add("trooper", Team.Enemy, 3, 3);
        b.Start();
        b.UseAbility("turret", new Point(3, 5));
        Assert.NotNull(b.DeployableAt(new Point(3, 5)), "turret placed");
        Assert.Equal(UseBlock.AlreadyActed, b.CanUse("turret"), "one action per turn");
        b.EndTurn();
        b.PassUntil(g);
        Assert.True(b.History.OfType<DeployableFired>().Any(e => e.Target == t), "turret fired");
        Assert.Equal(UseBlock.TurretAlreadyPlaced, b.CanUse("turret"), "only one turret");
    }

    [Test]
    public static void Charge_never_refills()
    {
        var b = Battle();
        var g = b.Add("gunner", Team.Player, 3, 6).First();
        b.Add("trooper", Team.Enemy, 3, 0);
        b.Start();
        b.UseAbility("turret", new Point(3, 5));
        Assert.Equal(55, g.Resource, "after turret");
        b.EndTurn();
        b.PassUntil(g);
        Assert.Equal(55, g.Resource, "still 55 next turn");
    }
}
