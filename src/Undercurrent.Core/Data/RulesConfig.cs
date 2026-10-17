namespace Undercurrent.Core.Data;

/// <summary>
/// Every tuning number in the combat rules, loaded from data/rules.json.
/// The defaults here match the browser prototype; the JSON overrides them.
/// </summary>
public sealed class RulesConfig
{
    // Turn order
    public int TurnThreshold { get; set; } = 100;
    public int StartingWaitMultiplier { get; set; } = 4;
    public int MinimumSpeed { get; set; } = 4;
    public int WaitCostMoveAndAct { get; set; } = 100;
    public int WaitCostMoveOrAct { get; set; } = 80;
    public int WaitCostNeither { get; set; } = 60;

    // Hit chance: HIT - (target speed - pivot) * factor + angle bonus
    public int HitSpeedPivot { get; set; } = 10;
    public int HitSpeedFactor { get; set; } = 2;
    public int FlankHitBonus { get; set; } = 10;
    public int BackHitBonus { get; set; } = 20;

    // Critical hits
    public double CritLuckFactor { get; set; } = 0.5;
    public int HiddenCritBonus { get; set; } = 25;
    public int ExposedCritPenalty { get; set; } = 10;
    public double CritMultiplier { get; set; } = 2.0;

    // Counters
    public int CounterChanceFront { get; set; } = 75;
    public int CounterChanceFlank { get; set; } = 50;
    public int CounterChanceBack { get; set; } = 25;
    public int CounterHeightPenaltyPerLevel { get; set; } = 10;
    public int BracedCounterBonus { get; set; } = 25;
    public double CounterDamageFactor { get; set; } = 0.5;
    public double BracedCounterDamageFactor { get; set; } = 0.75;
    public double CritCounterFactor { get; set; } = 0.5;

    // Damage modifiers
    public double BackstabMultiplier { get; set; } = 1.3;
    public double ChannelMultiplier { get; set; } = 1.5;
    public int GunHighGroundLevels { get; set; } = 2;
    public int GunHighGroundRangeBonus { get; set; } = 1;

    // The Blood Price (Unbound)
    public double BloodPriceHpPerResonance { get; set; } = 0.6;
    public double BloodPriceDamageMultiplier { get; set; } = 1.25;
    public double StrainDamagePerStack { get; set; } = 0.10;
    public int StrainLimit { get; set; } = 5;
    public int BurnoutTurns { get; set; } = 2;
    public int SacrificeHp { get; set; } = 15;
    public int SacrificeResonance { get; set; } = 30;

    // Statuses
    public int BurningDamage { get; set; } = 7;
    public int BleedingDamage { get; set; } = 5;
    public int ShockDelay { get; set; } = 35;
    public int StunImmunityTurns { get; set; } = 2;

    // Vision and perma-death
    public int VisionRange { get; set; } = 2;
    public int FallenCountdown { get; set; } = 3;

    // Deployables
    public int TurretHp { get; set; } = 30;
    public int TurretPower { get; set; } = 18;
    public int TurretRange { get; set; } = 3;

    // Enemy AI: how much a unit cares about the damage foes could deal it on a tile next turn.
    // Expected damage leaves out crits and angles, so a tile counts as lethal from a share of current HP.
    public double AiDangerWeight { get; set; } = 0.2;
    public double AiLethalDangerShare { get; set; } = 0.6;
    public double AiLethalDangerPenalty { get; set; } = 40;
}
