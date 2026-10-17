using Undercurrent.Core.Data;
using Undercurrent.Core.Grid;

namespace Undercurrent.Core.Battles;

/// <summary>
/// Something that happened in battle. The engine reads these in order and plays them
/// (walk animations, damage numbers, sounds). Text gives a log line for each.
/// </summary>
public abstract record BattleEvent
{
    public abstract string Text { get; }
    public override string ToString() => Text;
}

public sealed record BattleStarted(BattleMap Map) : BattleEvent
{ public override string Text => $"Battle begins at {Map.Name}."; }

public sealed record TurnStarted(Unit Unit) : BattleEvent
{ public override string Text => $"{Unit.Name}'s turn."; }

public sealed record TurnSkipped(Unit Unit, string Reason) : BattleEvent
{ public override string Text => $"{Unit.Name} loses the turn: {Reason}."; }

public sealed record TurnEnded(Unit Unit, int WaitCost) : BattleEvent
{ public override string Text => $"{Unit.Name} ends the turn facing {FacingName(Unit.Facing)}.";
  internal static string FacingName(Point f) => f == Point.North ? "north" : f == Point.East ? "east" : f == Point.South ? "south" : "west"; }

public sealed record UnitMoved(Unit Unit, IReadOnlyList<Point> Path) : BattleEvent
{ public override string Text => $"{Unit.Name} moves to {Path[^1]}."; }

public sealed record FacingChanged(Unit Unit, Point Facing) : BattleEvent
{ public override string Text => $"{Unit.Name} turns {TurnEnded.FacingName(Facing)}."; }

public sealed record AbilityUsed(Unit Unit, AbilityDef Ability, Point Target, bool Channeled) : BattleEvent
{ public override string Text => Channeled ? $"{Unit.Name} releases {Ability.Name}." : $"{Unit.Name} uses {Ability.Name}."; }

public sealed record AttackHit(Unit Attacker, Unit Target, AbilityDef Ability, int Damage, bool Critical, AttackAngle Angle) : BattleEvent
{ public override string Text => $"{Attacker.Name} hits {Target.Name}{(Angle == AttackAngle.Front ? "" : $" from the {Angle.ToString().ToLowerInvariant()}")} for {Damage}{(Critical ? " (critical)" : "")}."; }

public sealed record AttackMissed(Unit Attacker, Unit Target, AbilityDef Ability) : BattleEvent
{ public override string Text => $"{Attacker.Name} misses {Target.Name}."; }

public sealed record Countered(Unit Defender, Unit Attacker, int Damage, bool Parry) : BattleEvent
{ public override string Text => $"{Defender.Name} counters {Attacker.Name} for {Damage}{(Parry ? " (Parry)" : "")}."; }

public sealed record UnitDamaged(Unit Unit, int Amount, string Source) : BattleEvent
{ public override string Text => $"{Unit.Name} takes {Amount} from {Source}."; }

public sealed record UnitHealed(Unit Unit, int Amount, Unit? Source) : BattleEvent
{ public override string Text => Source is null || Source == Unit ? $"{Unit.Name} recovers {Amount} HP." : $"{Source.Name} heals {Unit.Name} for {Amount}."; }

public sealed record BloodPricePaid(Unit Unit, int Hp, int Strain) : BattleEvent
{ public override string Text => $"{Unit.Name} pays the Blood Price: {Hp} HP (Strained {Strain})."; }

public sealed record StatusApplied(Unit Unit, StatusId Status, int Turns) : BattleEvent
{ public override string Text => $"{Unit.Name} is {Status}."; }

public sealed record StatusEnded(Unit Unit, StatusId Status) : BattleEvent
{ public override string Text => $"{Unit.Name} is no longer {Status}."; }

public sealed record StatusResisted(Unit Unit, StatusId Status) : BattleEvent
{ public override string Text => $"{Unit.Name} resists {Status}."; }

public sealed record TurnDelayed(Unit Unit, int Amount) : BattleEvent
{ public override string Text => $"{Unit.Name} is Shocked; their next turn comes later."; }

public sealed record HiddenRevealed(Unit Unit) : BattleEvent
{ public override string Text => $"{Unit.Name} is revealed."; }

public sealed record ChannelStarted(Unit Unit, AbilityDef Ability, Point Target) : BattleEvent
{ public override string Text => $"{Unit.Name} begins channeling {Ability.Name}."; }

public sealed record ChannelInterrupted(Unit Unit, AbilityDef Ability) : BattleEvent
{ public override string Text => $"{Unit.Name}'s {Ability.Name} is interrupted."; }

public sealed record AbilityFizzled(Unit Unit, AbilityDef Ability) : BattleEvent
{ public override string Text => $"{Unit.Name}'s {Ability.Name} hits nothing."; }

public sealed record UnitChallenged(Unit Challenger, Unit Target) : BattleEvent
{ public override string Text => $"{Challenger.Name} challenges {Target.Name}."; }

public sealed record DeployablePlaced(Deployable Deployable) : BattleEvent
{ public override string Text => $"{Deployable.Owner.Name} deploys a {Deployable.Kind}."; }

public sealed record DeployableFired(Deployable Deployable, Unit Target, int Damage) : BattleEvent
{ public override string Text => $"The {Deployable.Kind} fires at {Target.Name} for {Damage}."; }

public sealed record BurnoutStarted(Unit Unit, int Turns) : BattleEvent
{ public override string Text => $"{Unit.Name} burns out and can't use abilities for {Turns} turns."; }

public sealed record BurnoutEnded(Unit Unit) : BattleEvent
{ public override string Text => $"{Unit.Name} recovers from Burnout; Strain resets."; }

public sealed record UnitFell(Unit Unit, int Countdown) : BattleEvent
{ public override string Text => $"{Unit.Name} falls. {Countdown} turns until they are lost."; }

public sealed record UnitLost(Unit Unit) : BattleEvent
{ public override string Text => $"{Unit.Name} is lost for good."; }

public sealed record BattleEnded(Team Winner) : BattleEvent
{ public override string Text => Winner == Team.Player ? "Victory: every hostile is down." : "Defeat: your company has fallen."; }
