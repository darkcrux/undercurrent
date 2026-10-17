using Undercurrent.Core.Data;

namespace Undercurrent.Core.Battles;

public sealed partial class Battle
{
    /// <summary>Predicts who acts next, starting with the active unit. Doesn't change the battle.</summary>
    public IReadOnlyList<Unit> PredictTurnOrder(int count)
    {
        var pool = _units.Where(u => u.IsAlive).Select(u => (Unit: u, Wait: u.Wait)).ToList();
        var order = new List<Unit>();
        if (Active is { IsAlive: true } active)
        {
            order.Add(active);
            int i = pool.FindIndex(p => p.Unit == active);
            if (i >= 0) pool[i] = (active, pool[i].Wait - Rules.TurnThreshold);
        }
        int guard = 0;
        while (order.Count < count && pool.Count > 0 && guard++ < 10_000)
        {
            int best = -1;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].Wait < Rules.TurnThreshold) continue;
                if (best < 0 || pool[i].Wait > pool[best].Wait ||
                    (pool[i].Wait == pool[best].Wait && pool[i].Unit.Id < pool[best].Unit.Id)) best = i;
            }
            if (best < 0)
            {
                for (int i = 0; i < pool.Count; i++)
                    pool[i] = (pool[i].Unit, pool[i].Wait + pool[i].Unit.EffectiveSpeed(Rules));
                continue;
            }
            order.Add(pool[best].Unit);
            pool[best] = (pool[best].Unit, pool[best].Wait - Rules.TurnThreshold);
        }
        return order;
    }

    private void AdvanceToNextTurn()
    {
        Active = null;
        while (!IsOver)
        {
            if (StartTurn(NextReadyUnit())) return;
        }
    }

    private Unit NextReadyUnit()
    {
        var pool = _units.Where(u => !u.Lost).ToList();
        int threshold = Rules.TurnThreshold;
        if (!pool.Any(u => u.Wait >= threshold))
        {
            int ticks = pool.Min(u => (int)Math.Ceiling((threshold - u.Wait) / (double)u.EffectiveSpeed(Rules)));
            foreach (var u in pool) u.Wait += u.EffectiveSpeed(Rules) * ticks;
        }
        return pool.Where(u => u.Wait >= threshold)
            .OrderByDescending(u => u.Wait).ThenBy(u => u.Id).First();
    }

    /// <summary>Runs start-of-turn effects. Returns true if the unit now waits for a command.</summary>
    private bool StartTurn(Unit u)
    {
        Active = u;
        HasMoved = HasActed = false;
        _usedActOnly = false;
        u.PaidHpThisTurn = false;
        TurnNumber++;

        if (u.Fallen)
        {
            u.FallenCountdown--;
            u.Wait -= Rules.TurnThreshold;
            if (u.FallenCountdown <= 0)
            {
                u.Lost = true;
                Emit(new UnitLost(u));
            }
            Active = null;
            return false;
        }

        Emit(new TurnStarted(u));
        if (u.RemoveStatus(StatusId.Braced)) Emit(new StatusEnded(u, StatusId.Braced));
        if (u.RemoveStatus(StatusId.Suppressed)) Emit(new StatusEnded(u, StatusId.Suppressed));

        foreach (var (status, damage) in new[] { (StatusId.Burning, Rules.BurningDamage), (StatusId.Bleeding, Rules.BleedingDamage) })
        {
            if (!u.Has(status)) continue;
            DealDamage(u, damage, status.ToString().ToLowerInvariant());
            if (u.TickStatus(status)) Emit(new StatusEnded(u, status));
            if (!u.IsAlive)
            {
                u.Wait -= Rules.WaitCostNeither;
                Active = null;
                return false;
            }
        }

        if (u.Has(StatusId.Stunned))
        {
            u.RemoveStatus(StatusId.Stunned);
            u.SetStatus(StatusId.StunImmune, Rules.StunImmunityTurns);
            Emit(new TurnSkipped(u, "Stunned"));
            u.Wait -= Rules.WaitCostNeither;
            EndOfTurnStatuses(u);
            Active = null;
            return false;
        }

        if (u.ResourceKind is ResourceKind.Focus or ResourceKind.Resonance)
            u.Resource = Math.Min(u.ResourceMax, u.Resource + u.Class.Resource.Regen);
        u.TickCooldowns();

        foreach (var device in _deployables.Where(d => d.Owner == u && !d.Destroyed).ToList())
        {
            TurretFire(device);
            if (IsOver) return true;
        }

        if (u.Channeling is { } channel)
        {
            u.Channeling = null;
            ResolveAbility(u, channel.Ability, channel.Target, channeled: true);
            HasActed = true;
            if (IsOver) return true;
            if (!u.IsAlive)
            {
                FinishTurn();
                return false;
            }
        }
        return true;
    }

    private void FinishTurn()
    {
        var u = Active!;
        int cost = HasMoved && HasActed ? Rules.WaitCostMoveAndAct
            : HasMoved || HasActed ? Rules.WaitCostMoveOrAct
            : Rules.WaitCostNeither;
        u.Wait -= cost;
        if (u.IsAlive) EndOfTurnStatuses(u);
        Emit(new TurnEnded(u, cost));
        Active = null;
    }

    private void EndOfTurnStatuses(Unit u)
    {
        foreach (var status in new[] { StatusId.Frozen, StatusId.Challenged, StatusId.Hidden, StatusId.Exposed, StatusId.StunImmune, StatusId.Burnout })
        {
            if (!u.TickStatus(status)) continue;
            if (status == StatusId.Challenged) u.ChallengedBy = null;
            if (status == StatusId.Burnout)
            {
                u.Strain = 0;
                Emit(new BurnoutEnded(u));
            }
            else if (status != StatusId.StunImmune)
            {
                Emit(new StatusEnded(u, status));
            }
        }
        if (!u.PaidHpThisTurn && !u.Has(StatusId.Burnout) && u.Strain > 0) u.Strain--;
    }

    private void TurretFire(Deployable turret)
    {
        var target = Enemies(turret.Owner)
            .Where(v => IsTargetable(v) && v.Position.DistanceTo(turret.Position) <= Rules.TurretRange)
            .OrderBy(v => v.Position.DistanceTo(turret.Position)).ThenBy(v => v.Hp).ThenBy(v => v.Id)
            .FirstOrDefault();
        if (target is null) return;
        int damage = Math.Max(1, (int)Math.Round(Rules.TurretPower * 100.0 / (100 + target.Stats.Def)));
        Emit(new DeployableFired(turret, target, damage));
        DealDamage(target, damage, source: null);
    }

    private void CheckForWinner()
    {
        if (IsOver) return;
        bool players = LivingUnits(Team.Player).Any();
        bool enemies = LivingUnits(Team.Enemy).Any();
        if (players && enemies) return;
        Winner = players ? Team.Player : Team.Enemy;
        Emit(new BattleEnded(Winner.Value));
    }
}
