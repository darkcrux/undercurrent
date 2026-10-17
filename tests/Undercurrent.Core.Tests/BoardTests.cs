using Undercurrent.Core.Ai;
using Undercurrent.Core.Battles;
using Undercurrent.Core.Data;
using Undercurrent.Core.Grid;
using static Undercurrent.Core.Tests.TestWorld;

namespace Undercurrent.Core.Tests;

public static class VisionTests
{
    [Test]
    public static void Vision_covers_front_and_sides_within_two_tiles_but_not_behind()
    {
        var b = Battle();
        var v = b.Add("trooper", Team.Enemy, 3, 3, Point.North);
        Assert.True(b.Sees(v, new Point(3, 1)), "2 ahead");
        Assert.True(b.Sees(v, new Point(4, 2)), "front diagonal");
        Assert.True(b.Sees(v, new Point(5, 3)), "2 to the side");
        Assert.True(b.Sees(v, new Point(1, 3)), "2 to the other side");
        Assert.False(b.Sees(v, new Point(3, 4)), "directly behind");
        Assert.False(b.Sees(v, new Point(4, 4)), "back diagonal");
        Assert.False(b.Sees(v, new Point(3, 0)), "3 ahead is too far");
    }

    [Test]
    public static void Hidden_units_can_only_be_targeted_when_spotted()
    {
        var b = Battle();
        var v = b.Add("vanguard", Team.Player, 3, 3, Point.North).First();
        var c = b.Add("cutthroat", Team.Enemy, 3, 4); // behind the vanguard
        b.Start();
        c.SetStatus(StatusId.Hidden, 3);
        Assert.False(b.IsTargetable(c), "hidden behind is not targetable");
        Assert.False(b.IsValidTarget(v, Ability("attack"), c.Position), "attack not allowed");

        v.Facing = Point.South;
        Assert.True(b.IsSpotted(c), "spotted once faced");
        Assert.True(b.IsValidTarget(v, Ability("attack"), c.Position), "attack allowed");
        Assert.True(c.Has(StatusId.Hidden), "still hidden (keeps its advantages)");
    }

    [Test]
    public static void Area_attacks_hit_hidden_units_and_reveal_them()
    {
        var b = Battle(new ScriptedRandom(0, 99.9));
        var el = b.Add("elementalist", Team.Player, 3, 6).First();
        var c = b.Add("cutthroat", Team.Enemy, 3, 3);
        b.Start();
        el.Resource = 100;
        c.SetStatus(StatusId.Hidden, 3);
        Assert.False(b.IsTargetable(c), "out of sight");
        b.UseAbility("flame", new Point(3, 4)); // area 1 covers (3,3)
        Assert.True(b.History.OfType<AttackHit>().Any(h => h.Target == c), "hit by the area");
        Assert.False(c.Has(StatusId.Hidden), "revealed");
    }
}

public static class MovementTests
{
    [Test]
    public static void Climb_up_to_jump_and_drop_up_to_twice_jump()
    {
        var map = MapWithHeights(
            new[] { 0, 2, 0, 3 },
            new[] { 0, 0, 0, 0 });
        var b = Battle(map: map);
        var v = b.Add("vanguard", Team.Player, 0, 0, Point.East); // jump 1
        var reach = b.ReachableTiles(v);
        Assert.False(reach.ContainsKey(new Point(1, 0)), "can't climb 2 levels with jump 1");

        var b2 = Battle(map: map);
        var s = b2.Add("vanguard", Team.Player, 1, 0, Point.East); // standing on height 2, jump 1
        var reach2 = b2.ReachableTiles(s);
        Assert.True(reach2.ContainsKey(new Point(2, 0)), "drop 2 levels (twice jump) is fine");
    }

    [Test]
    public static void Enemies_block_allies_can_be_passed_but_not_stopped_on()
    {
        var b = Battle();
        var v = b.Add("gunner", Team.Player, 0, 0, Point.East); // move 4
        b.Add("bloodcaller", Team.Player, 1, 0);
        b.Add("trooper", Team.Enemy, 0, 1);
        var reach = b.ReachableTiles(v);
        Assert.False(reach.ContainsKey(new Point(1, 0)), "can't stop on an ally");
        Assert.True(reach.ContainsKey(new Point(2, 0)), "can pass through an ally");
        Assert.False(reach.ContainsKey(new Point(0, 1)), "can't enter an enemy tile");
        Assert.True(reach[new Point(2, 0)].Count == 2, "path goes through the ally");
    }

    [Test]
    public static void Moving_ends_braced_and_act_only_blocks_moving()
    {
        var b = Battle();
        var g = b.Add("gunner", Team.Player, 0, 7).First();
        b.Add("trooper", Team.Enemy, 0, 2);
        b.Start();
        b.UseAbility("longshot", new Point(0, 2));
        Assert.False(b.CanMove, "can't move after an act-only ability");

        b.EndTurn();
        b.PassUntil(g);
        b.Move(new Point(1, 7));
        Assert.Equal(UseBlock.ActOnlyAfterMoving, b.CanUse("longshot"), "can't use act-only after moving");
    }
}

public static class TurnTests
{
    [Test]
    public static void Faster_units_act_first_and_wait_costs_depend_on_what_was_done()
    {
        var b = Battle();
        var fast = b.Add("cutthroat", Team.Enemy, 0, 0);   // SPD 17
        var slow = b.Add("vanguard", Team.Player, 7, 7);   // SPD 12 - weight 3
        b.Start();
        Assert.Equal(fast, b.Active, "fastest first");
        b.EndTurn();
        Assert.Equal(60, b.History.OfType<TurnEnded>().First().WaitCost, "doing nothing costs 60");
        Assert.True(b.PredictTurnOrder(4).Count == 4, "predicts a turn order");
    }

    [Test]
    public static void Cooldowns_wait_the_listed_number_of_turns()
    {
        var b = Battle();
        var v = b.Add("vanguard", Team.Player, 3, 6).First();
        b.Add("trooper", Team.Enemy, 3, 0);
        b.Start();
        b.UseAbility("guard", v.Position); // cooldown 2
        b.EndTurn();
        b.PassUntil(v);
        Assert.Equal(UseBlock.OnCooldown, b.CanUse("guard"), "next turn");
        b.EndTurn();
        b.PassUntil(v);
        Assert.Equal(UseBlock.OnCooldown, b.CanUse("guard"), "turn after");
        b.EndTurn();
        b.PassUntil(v);
        Assert.Equal(UseBlock.None, b.CanUse("guard"), "ready again");
    }

    [Test]
    public static void Stunned_units_lose_a_turn_then_resist_the_next_stun()
    {
        var b = Battle(ScriptedRandom.AlwaysSucceed());
        var v = b.Add("vanguard", Team.Player, 3, 4).First();
        var t = b.Add("trooper", Team.Enemy, 3, 3, Point.North); // facing away: back attack
        b.Start();
        b.UseAbility("bash", t.Position);
        Assert.True(t.Has(StatusId.Stunned), "stunned");
        b.EndTurn();
        b.PassUntil(v);
        Assert.True(b.History.OfType<TurnSkipped>().Any(s => s.Unit == t), "trooper lost its turn");
        Assert.True(t.Has(StatusId.StunImmune), "now immune");
    }

    [Test]
    public static void Shock_delays_the_next_turn()
    {
        var b = Battle(ScriptedRandom.AlwaysSucceed());
        var el = b.Add("elementalist", Team.Player, 3, 6).First();
        var t = b.Add("trooper", Team.Enemy, 3, 3);
        b.Start();
        el.Resource = 100;
        int wait = t.Wait;
        b.UseAbility("spark", t.Position);
        Assert.Equal(wait - 35, t.Wait, "35 wait lost");
    }

    [Test]
    public static void Fallen_units_are_lost_after_three_of_their_turns()
    {
        var b = Battle();
        var v = b.Add("vanguard", Team.Player, 0, 7);
        var a = b.Add("acolyte", Team.Player, 1, 7);
        b.Add("trooper", Team.Enemy, 7, 0);
        b.Start();
        a.Hp = 0;
        a.Fallen = true;
        a.FallenCountdown = 3;
        for (int i = 0; i < 60 && !a.Lost; i++) b.EndTurn();
        Assert.True(a.Lost, "lost after the countdown");
        Assert.False(b.IsOver, "the battle goes on while the vanguard stands");
        Assert.True(v.IsAlive);
    }
}

public static class AiTests
{
    [Test]
    public static void Full_ai_battles_finish_on_the_prototype_map()
    {
        for (int seed = 1; seed <= 40; seed++)
        {
            var b = Core.Battles.Battle.FromMap(TestWorld.Data, "ashfield-test", new SeededRandom(seed));
            b.Start();
            int turns = 0;
            while (!b.IsOver && turns++ < 600) EnemyAi.TakeTurn(b);
            Assert.True(b.IsOver, $"battle with seed {seed} finished");
        }
    }

    [Test]
    public static void Same_seed_gives_the_same_battle()
    {
        string Run(int seed)
        {
            var b = Core.Battles.Battle.FromMap(TestWorld.Data, "ashfield-test", new SeededRandom(seed));
            b.Start();
            for (int i = 0; i < 600 && !b.IsOver; i++) EnemyAi.TakeTurn(b);
            return string.Join("\n", b.History.Select(e => e.Text));
        }
        Assert.Equal(Run(7), Run(7), "history");
    }

    [Test]
    public static void Challenged_units_must_attack_the_challenger_if_they_can()
    {
        var b = Battle();
        var v = b.Add("vanguard", Team.Player, 3, 5).First();
        var weak = b.Add("acolyte", Team.Player, 1, 3);
        var t = b.Add("trooper", Team.Enemy, 2, 3);
        b.Start();
        weak.Hp = 5; // a tempting kill
        b.UseAbility("challenge", t.Position);
        b.EndTurn();
        b.PassUntil(t);
        var plan = EnemyAi.Plan(b);
        Assert.Equal((Point?)v.Position, plan.Target, "targets the challenger");
    }
}

public static class DataTests
{
    [Test]
    public static void Data_files_load_and_cross_reference()
    {
        Assert.True(TestWorld.Data.Abilities.Count >= 15, "abilities loaded");
        Assert.True(TestWorld.Data.Classes.ContainsKey("vanguard"), "vanguard class");
        Assert.Equal(75, TestWorld.Data.Rules.CounterChanceFront, "rules loaded");
        Assert.Equal(ResourceKind.Resonance, TestWorld.Data.Class("bloodcaller").Resource.Kind, "enum parsing");
        Assert.Equal(TargetKind.EmptyAdjacent, TestWorld.Data.Ability("turret").Target, "camelCase enum");
    }

    [Test]
    public static void Data_loads_through_a_custom_file_reader_as_godot_will()
    {
        string dir = GameData.FindDataDirectory();
        var data = GameData.Load(path => File.ReadAllText(Path.Combine(dir, path)), new[] { "maps/ashfield-test.json" });
        Assert.True(data.Maps.ContainsKey("ashfield-test"), "map loaded");
        Assert.Equal(TestWorld.Data.Abilities.Count, data.Abilities.Count, "same abilities");
    }

    [Test]
    public static void Broken_references_are_reported()
    {
        var bad = new ClassDef { Id = "broken", Abilities = { "no-such-ability" } };
        var e = Assert.Throws<InvalidDataException>(() => new GameData(new RulesConfig(), TestWorld.Data.Abilities.Values, new[] { bad }, Array.Empty<UnitTemplate>(), Array.Empty<MapDef>()));
        Assert.True(e.Message.Contains("no-such-ability"), "names the missing ability");
    }
}
