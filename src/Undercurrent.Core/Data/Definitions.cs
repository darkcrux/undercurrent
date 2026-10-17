namespace Undercurrent.Core.Data;

// Plain data loaded from the JSON files in /data. Nothing here holds battle state.

public enum Team { Player, Enemy }

public enum Faction { Mercenary, Commonwealth, Ordained, Unbound }

/// <summary>How a class pays for abilities.</summary>
public enum ResourceKind
{
    /// <summary>No resource; abilities wait a number of turns (Mercenary line).</summary>
    Cooldown,
    /// <summary>Starts full, does not refill on its own (Commonwealth).</summary>
    Charge,
    /// <summary>Starts empty, builds every turn (Ordained).</summary>
    Focus,
    /// <summary>Starts half full, recovers slowly; when short, pays the rest in HP (Unbound).</summary>
    Resonance,
}

public enum DamageType
{
    None,
    /// <summary>Uses ATK against DEF.</summary>
    Weapon,
    /// <summary>Uses ATN against RES.</summary>
    Elemental,
}

public enum Element { None, Fire, Lightning, Frost, Raw }

public enum TargetKind { Enemy, Ally, Self, Tile, EmptyAdjacent }

public enum StatusId
{
    Hidden,
    Exposed,
    Braced,
    Burning,
    Bleeding,
    Frozen,
    Suppressed,
    Challenged,
    Stunned,
    StunImmune,
    Burnout,
    /// <summary>Instant: delays the target's next turn. Never stays on a unit.</summary>
    Shocked,
}

public enum Passive { Parry, Backstab }

public enum SpecialEffect { None, Challenge, DeployTurret, Sacrifice }

public enum AttackAngle { Front, Flank, Back }

public sealed class StatBlock
{
    public int Hp { get; set; }
    public int Atk { get; set; }
    public int Def { get; set; }
    public int Atn { get; set; }
    public int Res { get; set; }
    public int Hit { get; set; }
    public int Spd { get; set; }
    public int Luk { get; set; }

    public StatBlock Plus(StatBlock? other) => other is null ? Clone() : new StatBlock
    {
        Hp = Hp + other.Hp, Atk = Atk + other.Atk, Def = Def + other.Def, Atn = Atn + other.Atn,
        Res = Res + other.Res, Hit = Hit + other.Hit, Spd = Spd + other.Spd, Luk = Luk + other.Luk,
    };

    public StatBlock Clone() => (StatBlock)MemberwiseClone();
}

public sealed class RangeDef
{
    public int Min { get; set; } = 1;
    public int Max { get; set; } = 1;

    public RangeDef() { }
    public RangeDef(int min, int max) { Min = min; Max = max; }
    public override string ToString() => Min == Max ? $"{Min}" : $"{Min}-{Max}";
}

public sealed class ResourceDef
{
    public ResourceKind Kind { get; set; } = ResourceKind.Cooldown;
    public int Max { get; set; }
    public int Start { get; set; }
    public int Regen { get; set; }
}

public sealed class StatusEffectDef
{
    public StatusId Id { get; set; }
    /// <summary>Percent chance to apply on a hit (channeled abilities always apply).</summary>
    public int Chance { get; set; } = 100;
    /// <summary>In the affected unit's own turns.</summary>
    public int Duration { get; set; } = 1;
}

public sealed class AbilityDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DamageType Type { get; set; } = DamageType.None;
    public Element Element { get; set; } = Element.None;
    public double Power { get; set; } = 1.0;
    /// <summary>Null means "use the class's weapon reach".</summary>
    public RangeDef? Range { get; set; }
    /// <summary>0 = single target; 1 = the target tile and its four neighbours; and so on.</summary>
    public int Area { get; set; }
    public TargetKind Target { get; set; } = TargetKind.Enemy;
    /// <summary>Resource cost (Charge, Focus or Resonance).</summary>
    public int Cost { get; set; }
    /// <summary>Own turns to wait before using it again (cooldown classes).</summary>
    public int Cooldown { get; set; }
    /// <summary>Can't be used on a turn the unit moves, and the unit can't move after.</summary>
    public bool ActOnly { get; set; }
    public bool Channelable { get; set; }
    public StatusEffectDef? Status { get; set; }
    /// <summary>Fraction of damage dealt that heals the user.</summary>
    public double Drain { get; set; }
    /// <summary>Heal amount as a multiple of the user's ATN.</summary>
    public double Heal { get; set; }
    public SpecialEffect Effect { get; set; } = SpecialEffect.None;

    public bool DealsDamage => Type != DamageType.None;
}

public sealed class ClassDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Line { get; set; } = "";
    public Faction Faction { get; set; }
    public ResourceDef Resource { get; set; } = new();
    public int Move { get; set; } = 4;
    public int Jump { get; set; } = 2;
    public int Weight { get; set; }
    public RangeDef Reach { get; set; } = new(1, 1);
    /// <summary>Guns gain range from high ground.</summary>
    public bool Gun { get; set; }
    public List<string> Abilities { get; set; } = new();
    public List<Passive> Passives { get; set; } = new();
    /// <summary>Stat changes while a unit is in this class.</summary>
    public StatBlock? Modifiers { get; set; }
}

public sealed class UnitTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Class { get; set; } = "";
    public StatBlock Stats { get; set; } = new();
}

public sealed class PlacementDef
{
    public string Unit { get; set; } = "";
    public Team Team { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string Facing { get; set; } = "north";
}

public sealed class MapDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<List<int>> Heights { get; set; } = new();
    public List<PlacementDef> Units { get; set; } = new();
}
