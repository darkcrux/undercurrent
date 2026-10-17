using Undercurrent.Core.Data;
using Undercurrent.Core.Grid;

namespace Undercurrent.Core.Battles;

/// <summary>A unit's state during one battle.</summary>
public sealed class Unit
{
    private readonly Dictionary<StatusId, int> _statuses = new();
    private readonly Dictionary<string, int> _cooldowns = new(StringComparer.Ordinal);

    public int Id { get; }
    public string Name { get; }
    public Team Team { get; }
    public UnitTemplate Template { get; }
    public ClassDef Class { get; }
    /// <summary>Base stats plus the class's modifiers.</summary>
    public StatBlock Stats { get; }

    public Point Position { get; internal set; }
    public Point Facing { get; internal set; }
    public int Hp { get; internal set; }
    public int MaxHp => Stats.Hp;
    /// <summary>Charge, Focus or Resonance. Unused for cooldown classes.</summary>
    public int Resource { get; internal set; }
    public int ResourceMax => Class.Resource.Max;
    public ResourceKind ResourceKind => Class.Resource.Kind;
    /// <summary>Wait timer: the unit acts when it reaches the turn threshold.</summary>
    public int Wait { get; internal set; }

    public int Strain { get; internal set; }
    internal bool PaidHpThisTurn { get; set; }

    public bool Fallen { get; internal set; }
    /// <summary>Turns left to save a fallen unit before it is lost.</summary>
    public int FallenCountdown { get; internal set; }
    public bool Lost { get; internal set; }
    public bool IsAlive => !Fallen && !Lost;

    public ChannelState? Channeling { get; internal set; }
    public int? ChallengedBy { get; internal set; }

    public IReadOnlyDictionary<StatusId, int> Statuses => _statuses;
    public IReadOnlyDictionary<string, int> Cooldowns => _cooldowns;
    public IReadOnlyList<string> Abilities => Class.Abilities;

    public int Move => Class.Move;
    public int Jump => Class.Jump;
    public RangeDef Reach => Class.Reach;

    internal Unit(int id, UnitTemplate template, ClassDef cls, Team team, Point position, Point facing)
    {
        Id = id;
        Name = template.Name;
        Template = template;
        Class = cls;
        Team = team;
        Stats = template.Stats.Plus(cls.Modifiers);
        Position = position;
        Facing = facing;
        Hp = MaxHp;
        Resource = cls.Resource.Kind == ResourceKind.Cooldown ? 0 : cls.Resource.Start;
    }

    public int EffectiveSpeed(RulesConfig rules) => Math.Max(rules.MinimumSpeed, Stats.Spd - Class.Weight);
    public bool Has(StatusId status) => _statuses.ContainsKey(status);
    public int TurnsLeft(StatusId status) => _statuses.TryGetValue(status, out int t) ? t : 0;
    public bool HasPassive(Passive p) => Class.Passives.Contains(p);
    public int CooldownLeft(string abilityId) => _cooldowns.TryGetValue(abilityId, out int t) ? t : 0;

    internal void SetStatus(StatusId status, int turns)
    {
        if (turns <= 0) _statuses.Remove(status);
        else _statuses[status] = Math.Max(TurnsLeft(status), turns);
    }

    internal bool RemoveStatus(StatusId status) => _statuses.Remove(status);
    internal void ClearStatuses() => _statuses.Clear();

    /// <summary>Counts a status down by one turn. Returns true if it just ended.</summary>
    internal bool TickStatus(StatusId status)
    {
        if (!_statuses.TryGetValue(status, out int t)) return false;
        if (t <= 1) { _statuses.Remove(status); return true; }
        _statuses[status] = t - 1;
        return false;
    }

    internal void SetCooldown(string abilityId, int turns) => _cooldowns[abilityId] = turns;

    internal void TickCooldowns()
    {
        foreach (var key in _cooldowns.Keys.ToList())
            if (_cooldowns[key] > 0) _cooldowns[key]--;
    }

    public override string ToString() => $"{Name}#{Id}";
}

public sealed record ChannelState(AbilityDef Ability, Point Target);

/// <summary>A device placed on the map, such as a turret.</summary>
public sealed class Deployable
{
    public int Id { get; }
    public string Kind { get; }
    public Unit Owner { get; }
    public Team Team => Owner.Team;
    public Point Position { get; }
    public int Hp { get; internal set; }
    public int MaxHp { get; }
    public bool Destroyed => Hp <= 0;

    internal Deployable(int id, string kind, Unit owner, Point position, int hp)
    {
        Id = id; Kind = kind; Owner = owner; Position = position; Hp = hp; MaxHp = hp;
    }

    public override string ToString() => $"{Kind}#{Id}";
}
