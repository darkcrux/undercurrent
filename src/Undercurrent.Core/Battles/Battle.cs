using Undercurrent.Core.Data;
using Undercurrent.Core.Grid;

namespace Undercurrent.Core.Battles;

/// <summary>
/// One battle. The engine drives it with commands (<see cref="Move"/>, <see cref="UseAbility"/>,
/// <see cref="EndTurn"/>) for player units, or <see cref="Ai.EnemyAi"/> for hostile ones,
/// then reads <see cref="TakeNewEvents"/> to animate what happened.
/// </summary>
public sealed partial class Battle
{
    public const string BasicAttackId = "attack";

    private readonly List<Unit> _units = new();
    private readonly List<Deployable> _deployables = new();
    private readonly List<BattleEvent> _history = new();
    private readonly IRandom _random;
    private int _nextId = 1;
    private int _readCursor;
    private bool _started;
    private bool _usedActOnly;

    public GameData Data { get; }
    public RulesConfig Rules => Data.Rules;
    public BattleMap Map { get; }

    public IReadOnlyList<Unit> Units => _units;
    public IReadOnlyList<Deployable> Deployables => _deployables;
    /// <summary>Every event since the battle began.</summary>
    public IReadOnlyList<BattleEvent> History => _history;

    /// <summary>The unit whose turn it is, or null between turns or after the battle.</summary>
    public Unit? Active { get; private set; }
    public bool HasMoved { get; private set; }
    public bool HasActed { get; private set; }
    public Team? Winner { get; private set; }
    public bool IsOver => Winner is not null;
    /// <summary>True when the active unit is waiting for a command.</summary>
    public bool AwaitingCommand => _started && !IsOver && Active is not null;
    /// <summary>Number of turns taken so far, by any unit.</summary>
    public int TurnNumber { get; private set; }

    public Battle(GameData data, MapDef map, IRandom random, bool placeMapUnits = true)
    {
        Data = data;
        Map = new BattleMap(map.Id, map.Name, map.Heights);
        _random = random;
        if (placeMapUnits)
            foreach (var p in map.Units)
                AddUnit(p.Unit, p.Team, new Point(p.X, p.Y), Point.ParseFacing(p.Facing));
    }

    public static Battle FromMap(GameData data, string mapId, IRandom random) => new(data, data.Map(mapId), random);

    /// <summary>Adds a unit before the battle starts.</summary>
    public Unit AddUnit(string templateId, Team team, Point position, Point facing)
    {
        if (_started) throw new InvalidOperationException("Units can only be added before the battle starts.");
        if (!Map.Contains(position)) throw new ArgumentException($"{position} is outside the map.");
        if (IsOccupied(position)) throw new ArgumentException($"{position} is already occupied.");
        if (!facing.IsDirection) throw new ArgumentException($"{facing} is not a facing.");
        var template = Data.Unit(templateId);
        var unit = new Unit(_nextId++, template, Data.Class(template.Class), team, position, facing);
        unit.Wait = unit.EffectiveSpeed(Rules) * Rules.StartingWaitMultiplier;
        _units.Add(unit);
        return unit;
    }

    public void Start()
    {
        if (_started) throw new InvalidOperationException("The battle has already started.");
        if (!_units.Any(u => u.Team == Team.Player) || !_units.Any(u => u.Team == Team.Enemy))
            throw new InvalidOperationException("A battle needs at least one unit on each side.");
        _started = true;
        Emit(new BattleStarted(Map));
        AdvanceToNextTurn();
    }

    /// <summary>Events since the last call, in order.</summary>
    public IReadOnlyList<BattleEvent> TakeNewEvents()
    {
        var fresh = _history.Skip(_readCursor).ToList();
        _readCursor = _history.Count;
        return fresh;
    }

    // ---------- Board queries ----------

    /// <summary>Any unit standing on the tile, including fallen bodies.</summary>
    public Unit? UnitAt(Point p) => _units.FirstOrDefault(u => !u.Lost && u.Position == p);
    public Unit? LivingUnitAt(Point p) => _units.FirstOrDefault(u => u.IsAlive && u.Position == p);
    public Deployable? DeployableAt(Point p) => _deployables.FirstOrDefault(d => !d.Destroyed && d.Position == p);
    public bool IsOccupied(Point p) => UnitAt(p) is not null || DeployableAt(p) is not null;

    public IEnumerable<Unit> LivingUnits(Team team) => _units.Where(u => u.IsAlive && u.Team == team);
    public IEnumerable<Unit> Enemies(Unit of) => _units.Where(u => u.IsAlive && u.Team != of.Team);
    public IEnumerable<Unit> Allies(Unit of) => _units.Where(u => u.IsAlive && u.Team == of.Team);

    public Unit UnitById(int id) => _units.First(u => u.Id == id);

    /// <summary>
    /// Tiles the unit can move to this turn, each with its path (excluding the start).
    /// Includes the current tile with an empty path. Allies can be passed through but not stopped on;
    /// enemies, fallen bodies and other teams' devices block.
    /// </summary>
    public IReadOnlyDictionary<Point, IReadOnlyList<Point>> ReachableTiles(Unit unit)
    {
        var result = new Dictionary<Point, IReadOnlyList<Point>> { [unit.Position] = Array.Empty<Point>() };
        if (unit.Has(StatusId.Frozen)) return result;

        var queue = new Queue<(Point At, List<Point> Path)>();
        var seen = new HashSet<Point> { unit.Position };
        queue.Enqueue((unit.Position, new List<Point>()));
        while (queue.Count > 0)
        {
            var (at, path) = queue.Dequeue();
            if (path.Count >= unit.Move) continue;
            foreach (var dir in Point.Directions)
            {
                var next = at + dir;
                if (!Map.Contains(next) || seen.Contains(next)) continue;
                int rise = Map.HeightAt(next) - Map.HeightAt(at);
                if (rise > unit.Jump || -rise > unit.Jump * 2) continue;

                var unitThere = UnitAt(next);
                var deviceThere = DeployableAt(next);
                bool blocked =
                    (unitThere is not null && (unitThere.Team != unit.Team || !unitThere.IsAlive)) ||
                    (deviceThere is not null && deviceThere.Team != unit.Team);
                if (blocked) continue;

                seen.Add(next);
                var nextPath = new List<Point>(path) { next };
                if (unitThere is null && deviceThere is null) result[next] = nextPath;
                queue.Enqueue((next, nextPath));
            }
        }
        return result;
    }

    // ---------- Vision and Hidden ----------

    /// <summary>A unit sees tiles within vision range that are ahead of it or beside it, never behind.</summary>
    public bool Sees(Unit viewer, Point p)
    {
        if (!viewer.IsAlive || p == viewer.Position) return false;
        var d = p - viewer.Position;
        if (Math.Abs(d.X) + Math.Abs(d.Y) > Rules.VisionRange) return false;
        return viewer.Facing.X * d.X + viewer.Facing.Y * d.Y >= 0;
    }

    /// <summary>A Hidden unit inside any enemy's vision. Spotted units can be targeted but keep Hidden's other effects.</summary>
    public bool IsSpotted(Unit unit) =>
        unit.Has(StatusId.Hidden) && Enemies(unit).Any(v => Sees(v, unit.Position));

    /// <summary>Can this unit be picked as the target of an attack or single-target ability?</summary>
    public bool IsTargetable(Unit unit) => unit.IsAlive && (!unit.Has(StatusId.Hidden) || IsSpotted(unit));

    private void Emit(BattleEvent e) => _history.Add(e);
}
