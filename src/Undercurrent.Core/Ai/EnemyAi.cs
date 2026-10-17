using Undercurrent.Core.Battles;
using Undercurrent.Core.Data;
using Undercurrent.Core.Grid;

namespace Undercurrent.Core.Ai;

/// <summary>What the AI intends to do this turn. The engine can animate it step by step with <see cref="EnemyAi.Execute"/>.</summary>
public sealed record AiPlan(Point Destination, string? AbilityId, Point? Target, double Score)
{
    public bool Moves(Unit u) => Destination != u.Position;
}

/// <summary>
/// The prototype's AI: tries every reachable tile and every usable ability, scores expected damage
/// against counter risk, and otherwise closes in on the nearest enemy. Works for either team.
/// </summary>
public static class EnemyAi
{
    /// <summary>Scores below this mean "just reposition".</summary>
    public const double ActThreshold = 4;

    public static AiPlan Plan(Battle battle)
    {
        var u = battle.Active ?? throw new InvalidOperationException("No active unit.");
        var foes = battle.Enemies(u).ToList();
        var allies = battle.Allies(u).ToList();
        var reachable = battle.ReachableTiles(u);
        var challenger = u.ChallengedBy is int id ? battle.Units.FirstOrDefault(v => v.Id == id && v.IsAlive) : null;

        AiPlan? best = null;
        void Consider(Point tile, string ability, Point target, double score)
        {
            if (best is null || score > best.Score) best = new AiPlan(tile, ability, target, score);
        }

        foreach (var (tile, path) in reachable)
        {
            bool moved = path.Count > 0;
            foreach (var abilityId in u.Abilities)
            {
                if (battle.CanUse(u, abilityId, moved, hasActed: false) != UseBlock.None) continue;
                var ab = battle.Data.Ability(abilityId);

                if (ab.Target == TargetKind.Enemy && ab.DealsDamage)
                {
                    foreach (var t in foes)
                    {
                        if (challenger is not null && t != challenger) continue;
                        if (!battle.IsValidTarget(u, ab, t.Position, tile)) continue;
                        var f = battle.Forecast(u, t, ab, tile);
                        double expected = f.HitChance / 100.0 * f.Damage * (1 + f.CritChance / 100.0);
                        double kill = expected >= t.Hp ? 40 : 0;
                        double counterOdds = (f.HitChance * f.CounterChance + (100 - f.HitChance) * f.CounterChanceOnMiss) / 100.0;
                        double risk = kill > 0 ? 0 : counterOdds / 100.0 * f.CounterDamage * 0.9;
                        double score = expected + kill - risk
                            + (f.Angle == AttackAngle.Back ? 4 : f.Angle == AttackAngle.Flank ? 2 : 0)
                            + (ab.Status is not null ? 5 : 0)
                            + (1 - (double)t.Hp / t.MaxHp) * 8;
                        if (challenger is not null) score += 100;
                        Consider(tile, abilityId, t.Position, score);
                    }
                }
                else if (ab.Target == TargetKind.Enemy && ab.Effect == SpecialEffect.Challenge)
                {
                    // Challenge the strongest foe that could reach a weaker ally.
                    foreach (var t in foes)
                        if (battle.IsValidTarget(u, ab, t.Position, tile) && t.Hp > t.MaxHp / 2)
                            Consider(tile, abilityId, t.Position, 6);
                }
                else if (ab.Target == TargetKind.Tile && ab.DealsDamage)
                {
                    foreach (var center in battle.TilesInRange(u, ab, tile))
                    {
                        double score = 0;
                        foreach (var p in battle.Map.Diamond(center, ab.Area))
                        {
                            var t = battle.LivingUnitAt(p);
                            if (t is null) continue;
                            if (t.Team == u.Team) continue;
                            var f = battle.Forecast(u, t, ab, tile);
                            score += f.HitChance / 100.0 * f.Damage + (ab.Status is not null ? 4 : 0);
                        }
                        if (score > 0) Consider(tile, abilityId, center, score * 0.9);
                    }
                }
                else if (ab.Target == TargetKind.Ally && ab.Heal > 0)
                {
                    foreach (var a in allies)
                    {
                        if (a.Hp > a.MaxHp * 0.6 || !battle.IsValidTarget(u, ab, a.Position == u.Position ? tile : a.Position, tile)) continue;
                        int healed = Math.Min((int)Math.Round(u.Stats.Atn * ab.Heal), a.MaxHp - a.Hp);
                        Consider(tile, abilityId, a.Position == u.Position ? tile : a.Position, healed * 1.2);
                    }
                }
                else if (ab.Target == TargetKind.EmptyAdjacent && ab.Effect == SpecialEffect.DeployTurret)
                {
                    int nearestFoe = foes.Count == 0 ? 99 : foes.Min(f => f.Position.DistanceTo(tile));
                    if (nearestFoe <= battle.Rules.TurretRange + 2)
                        foreach (var dir in Point.Directions)
                            if (battle.IsValidTarget(u, ab, tile + dir, tile))
                            {
                                Consider(tile, abilityId, tile + dir, 10);
                                break;
                            }
                }
                else if (ab.Target == TargetKind.Self)
                {
                    int nearest = foes.Count == 0 ? 99 : foes.Min(f => f.Position.DistanceTo(tile));
                    if (ab.Status?.Id == StatusId.Braced && nearest <= 2) Consider(tile, abilityId, tile, 7);
                    if (ab.Status?.Id == StatusId.Hidden && !u.Has(StatusId.Hidden)) Consider(tile, abilityId, tile, 17 - nearest * 1.5);
                    if (ab.Effect == SpecialEffect.Sacrifice && u.Resource < 25 && u.Hp > u.MaxHp / 2 && u.Strain < 3)
                        Consider(tile, abilityId, tile, 5);
                }
            }
        }

        if (best is not null && best.Score > ActThreshold) return best;

        // Nothing worth doing: close in (guns keep a distance of 3), preferring high ground.
        var goal = challenger ?? foes.OrderBy(f => f.Position.DistanceTo(u.Position)).FirstOrDefault();
        if (goal is null) return new AiPlan(u.Position, null, null, 0);
        int preferred = u.Class.Gun ? 3 : 1;
        var destination = reachable.Keys
            .OrderBy(t =>
            {
                int d = t.DistanceTo(goal.Position);
                return Math.Abs(d - preferred) * 10 + d * 0.1 - battle.Map.HeightAt(t) * 0.6;
            })
            .First();
        return new AiPlan(destination, null, null, 0);
    }

    /// <summary>Carries out a plan: move, act, then face the nearest enemy and end the turn.</summary>
    public static void Execute(Battle battle, AiPlan plan)
    {
        var u = battle.Active ?? throw new InvalidOperationException("No active unit.");
        if (plan.Moves(u) && battle.CanMove) battle.Move(plan.Destination);
        if (!battle.IsOver && battle.AwaitingCommand && plan.AbilityId is { } ability && plan.Target is { } target
            && battle.CanUse(ability) == UseBlock.None
            && battle.IsValidTarget(u, battle.Data.Ability(ability), target))
        {
            battle.UseAbility(ability, target);
        }
        if (battle.IsOver || !battle.AwaitingCommand) return;

        Point? facing = null;
        if (u.IsAlive)
        {
            var near = battle.Enemies(u).OrderBy(v => v.Position.DistanceTo(u.Position)).FirstOrDefault();
            if (near is not null && near.Position != u.Position) facing = Point.FacingToward(u.Position, near.Position);
        }
        battle.EndTurn(facing);
    }

    public static void TakeTurn(Battle battle) => Execute(battle, Plan(battle));
}
