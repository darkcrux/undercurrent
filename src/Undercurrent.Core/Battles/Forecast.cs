using Undercurrent.Core.Data;
using Undercurrent.Core.Grid;

namespace Undercurrent.Core.Battles;

/// <summary>Everything the attack preview shows, computed before anything is rolled.</summary>
public sealed record Forecast(
    Unit Attacker,
    Unit Target,
    AbilityDef Ability,
    Point From,
    AttackAngle Angle,
    int AngleHitBonus,
    int HitChance,
    int Damage,
    int CritChance,
    int CritDamage,
    bool CanCounter,
    NoCounterReason NoCounterReason,
    int CounterChance,
    int CounterChanceOnCrit,
    int CounterChanceOnMiss,
    int CounterDamage,
    bool Parry,
    int HeightAdvantage,
    int BloodPriceHp,
    bool Channeled)
{
    public bool LethalOnHit => Damage >= Target.Hp;
    public bool PaysBloodPrice => BloodPriceHp > 0;
}

public enum NoCounterReason
{
    None,
    AttackerHidden,
    TargetSuppressed,
    TargetStunned,
    OutOfReach,
    NotAnAttack,
}

/// <summary>Why an ability can't be used right now.</summary>
public enum UseBlock
{
    None,
    NotYourTurn,
    AlreadyActed,
    Burnout,
    ActOnlyAfterMoving,
    OnCooldown,
    NotEnoughResource,
    TurretAlreadyPlaced,
    TooFewHp,
    UnknownAbility,
}
