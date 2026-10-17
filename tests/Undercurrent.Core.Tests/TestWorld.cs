using Undercurrent.Core.Battles;
using Undercurrent.Core.Data;
using Undercurrent.Core.Grid;

namespace Undercurrent.Core.Tests;

/// <summary>Builds small, exact battle situations for tests, using the real data files.</summary>
public static class TestWorld
{
    private static GameData? _data;
    public static GameData Data => _data ??= GameData.LoadFromDirectory(GameData.FindDataDirectory());

    public static MapDef FlatMap(int size = 8) => new()
    {
        Id = "flat",
        Name = "Flat test map",
        Heights = Enumerable.Range(0, size).Select(_ => Enumerable.Repeat(0, size).ToList()).ToList(),
    };

    public static MapDef MapWithHeights(params int[][] rows) => new()
    {
        Id = "custom",
        Name = "Custom test map",
        Heights = rows.Select(r => r.ToList()).ToList(),
    };

    public static Battle Battle(IRandom? random = null, MapDef? map = null) =>
        new(Data, map ?? FlatMap(), random ?? ScriptedRandom.AlwaysSucceed(), placeMapUnits: false);

    public static Unit Add(this Battle b, string unit, Team team, int x, int y, Point? facing = null) =>
        b.AddUnit(unit, team, new Point(x, y), facing ?? (team == Team.Player ? Point.North : Point.South));

    /// <summary>Gives the unit the first turn when the battle starts.</summary>
    public static Unit First(this Unit u) { u.Wait = Data.Rules.TurnThreshold; return u; }

    /// <summary>Ends turns (doing nothing) until it is <paramref name="u"/>'s turn.</summary>
    public static void PassUntil(this Battle b, Unit u, int limit = 200)
    {
        for (int i = 0; i < limit && !b.IsOver && b.Active != u; i++) b.EndTurn();
        if (b.Active != u) throw new AssertionException($"{u.Name} never got a turn.");
    }

    public static AbilityDef Ability(string id) => Data.Ability(id);
}
