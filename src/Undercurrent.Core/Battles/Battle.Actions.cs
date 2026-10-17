using Undercurrent.Core.Data;
using Undercurrent.Core.Grid;

namespace Undercurrent.Core.Battles;

public sealed partial class Battle
{
    // ---------- Commands for the active unit ----------

    public bool CanMove =>
        AwaitingCommand && !HasMoved && !_usedActOnly && !Active!.Has(StatusId.Frozen);

    /// <summary>Moves the active unit along the shortest path to <paramref name="destination"/>.</summary>
    public void Move(Point destination)
    {
        var u = RequireActive();
        if (!CanMove) throw new InvalidOperationException($"{u.Name} can't move right now.");
        if (!ReachableTiles(u).TryGetValue(destination, out var path) || path.Count == 0)
            throw new InvalidOperationException($"{u.Name} can't reach {destination}.");

        foreach (var step in path)
        {
            u.Facing = Point.FacingToward(u.Position, step);
            u.Position = step;
        }
        HasMoved = true;
        Emit(new UnitMoved(u, path));
        if (u.RemoveStatus(StatusId.Braced)) Emit(new StatusEnded(u, StatusId.Braced));
    }

    /// <summary>Why the active unit can't use the ability now, or <see cref="UseBlock.None"/>.</summary>
    public UseBlock CanUse(string abilityId)
    {
        if (!AwaitingCommand) return UseBlock.NotYourTurn;
        return CanUse(Active!, abilityId, HasMoved, HasActed);
    }

    /// <summary>Checks an ability for any unit under assumed turn state (used by the AI to look ahead).</summary>
    public UseBlock CanUse(Unit u, string abilityId, bool hasMoved, bool hasActed)
    {
        if (!u.Abilities.Contains(abilityId) || !Data.Abilities.TryGetValue(abilityId, out var ab)) return UseBlock.UnknownAbility;
        if (hasActed) return UseBlock.AlreadyActed;
        if (abilityId != BasicAttackId && u.Has(StatusId.Burnout)) return UseBlock.Burnout;
        if (ab.ActOnly && hasMoved) return UseBlock.ActOnlyAfterMoving;
        if (ab.Cooldown > 0 && u.CooldownLeft(abilityId) > 0) return UseBlock.OnCooldown;
        if (ab.Cost > 0 && u.ResourceKind != ResourceKind.Resonance && u.Resource < ab.Cost) return UseBlock.NotEnoughResource;
        if (ab.Effect == SpecialEffect.DeployTurret && _deployables.Any(d => d.Owner == u && !d.Destroyed)) return UseBlock.TurretAlreadyPlaced;
        if (ab.Effect == SpecialEffect.Sacrifice && u.Hp <= Rules.SacrificeHp) return UseBlock.TooFewHp;
        return UseBlock.None;
    }

    /// <summary>Is <paramref name="target"/> a legal target for the ability when used from <paramref name="from"/>?</summary>
    public bool IsValidTarget(Unit u, AbilityDef ab, Point target, Point? from = null)
    {
        var origin = from ?? u.Position;
        if (!Map.Contains(target)) return false;
        switch (ab.Target)
        {
            case TargetKind.Self:
                return target == origin;
            case TargetKind.Enemy:
            {
                var t = LivingUnitAt(target);
                return t is not null && t.Team != u.Team && IsTargetable(t) && InRange(u, ab, origin, target);
            }
            case TargetKind.Ally:
            {
                var t = target == origin ? u : LivingUnitAt(target);
                return t is not null && t.Team == u.Team && InRange(u, ab, origin, target);
            }
            case TargetKind.Tile:
                return InRange(u, ab, origin, target);
            case TargetKind.EmptyAdjacent:
                return origin.DistanceTo(target) == 1 && !IsOccupied(target);
            default:
                return false;
        }
    }

    /// <summary>Tiles the ability can reach from <paramref name="from"/> (whether or not anything is there).</summary>
    public IEnumerable<Point> TilesInRange(Unit u, AbilityDef ab, Point? from = null)
    {
        var origin = from ?? u.Position;
        if (ab.Target == TargetKind.Self) return new[] { origin };
        return Map.AllTiles().Where(p => InRange(u, ab, origin, p));
    }

    public bool InRange(Unit u, AbilityDef ab, Point from, Point target)
    {
        var range = ab.Range ?? u.Reach;
        int max = range.Max;
        if (ab.Type == DamageType.Weapon && u.Class.Gun &&
            Map.HeightAt(from) - Map.HeightAt(target) >= Rules.GunHighGroundLevels)
            max += Rules.GunHighGroundRangeBonus;
        int d = from.DistanceTo(target);
        return d >= range.Min && d <= max;
    }

    /// <summary>
    /// Uses an ability. With <paramref name="channel"/> on a channelable ability, the cost is paid now and the
    /// ability resolves at the start of the unit's next turn at higher power, unless the unit is hit first.
    /// </summary>
    public void UseAbility(string abilityId, Point target, bool channel = false)
    {
        var u = RequireActive();
        var block = CanUse(abilityId);
        if (block != UseBlock.None) throw new InvalidOperationException($"{u.Name} can't use {abilityId}: {block}.");
        var ab = Data.Ability(abilityId);
        if (!IsValidTarget(u, ab, target)) throw new InvalidOperationException($"{target} isn't a valid target for {ab.Name}.");
        if (channel && !ab.Channelable) throw new InvalidOperationException($"{ab.Name} can't be channeled.");

        if (channel)
        {
            Pay(u, ab);
            Face(u, target);
            u.Channeling = new ChannelState(ab, target);
            Emit(new ChannelStarted(u, ab, target));
        }
        else
        {
            ResolveAbility(u, ab, target, channeled: false);
        }
        HasActed = true;
        if (ab.ActOnly) _usedActOnly = true;
    }

    /// <summary>Ends the active unit's turn, optionally turning it to face a direction first.</summary>
    public void EndTurn(Point? facing = null)
    {
        var u = RequireActive();
        if (facing is { } f)
        {
            if (!f.IsDirection) throw new ArgumentException($"{f} is not a facing.", nameof(facing));
            if (u.IsAlive && u.Facing != f)
            {
                u.Facing = f;
                Emit(new FacingChanged(u, f));
            }
        }
        FinishTurn();
        AdvanceToNextTurn();
    }

    private Unit RequireActive()
    {
        if (!AwaitingCommand) throw new InvalidOperationException("No unit is waiting for a command.");
        return Active!;
    }

    // ---------- Resolution ----------

    private void ResolveAbility(Unit u, AbilityDef ab, Point target, bool channeled)
    {
        Emit(new AbilityUsed(u, ab, target, channeled));

        if (ab.Target == TargetKind.Self)
        {
            Pay(u, ab);
            if (ab.Status is { } selfStatus) ApplyStatus(u, selfStatus.Id, selfStatus.Duration);
            if (ab.Effect == SpecialEffect.Sacrifice)
            {
                u.Hp = Math.Max(1, u.Hp - Rules.SacrificeHp);
                u.Resource = Math.Min(u.ResourceMax, u.Resource + Rules.SacrificeResonance);
                u.Strain++;
                u.PaidHpThisTurn = true;
                Emit(new BloodPricePaid(u, Rules.SacrificeHp, u.Strain));
                CheckBurnout(u);
            }
            return;
        }

        Face(u, target);

        if (ab.Effect == SpecialEffect.DeployTurret)
        {
            Pay(u, ab);
            var turret = new Deployable(_nextId++, "turret", u, target, Rules.TurretHp);
            _deployables.Add(turret);
            Emit(new DeployablePlaced(turret));
            return;
        }

        if (ab.Heal > 0)
        {
            Pay(u, ab);
            var ally = target == u.Position ? u : LivingUnitAt(target);
            if (ally is not null) Heal(ally, (int)Math.Round(u.Stats.Atn * ab.Heal), u);
            return;
        }

        if (ab.Effect == SpecialEffect.Challenge)
        {
            Pay(u, ab);
            var foe = LivingUnitAt(target);
            if (foe is not null)
            {
                foe.SetStatus(StatusId.Challenged, 1);
                foe.ChallengedBy = u.Id;
                Emit(new UnitChallenged(u, foe));
            }
            return;
        }

        var tiles = ab.Area > 0 ? Map.Diamond(target, ab.Area) : new[] { target };
        var targets = tiles.Select(LivingUnitAt).OfType<Unit>().Where(t => t.Team != u.Team).ToList();
        if (targets.Count == 0)
        {
            if (!channeled) Pay(u, ab);
            Emit(new AbilityFizzled(u, ab));
            return;
        }

        // Odds are fixed before paying, so a Blood Price bonus applies to this ability.
        var forecasts = targets.Select(t => Forecast(u, t, ab, u.Position, channeled)).ToList();
        if (!channeled) Pay(u, ab);
        if (u.RemoveStatus(StatusId.Hidden)) Emit(new StatusEnded(u, StatusId.Hidden));

        foreach (var f in forecasts)
        {
            var t = f.Target;
            if (!t.IsAlive) continue;

            bool hit = _random.Roll(f.HitChance);
            bool crit = false;
            if (hit)
            {
                crit = _random.Roll(f.CritChance);
                int damage = crit ? f.CritDamage : f.Damage;
                Emit(new AttackHit(u, t, ab, damage, crit, f.Angle));
                DealDamage(t, damage, source: null);
                if (t.IsAlive && t.RemoveStatus(StatusId.Hidden)) Emit(new HiddenRevealed(t));
                if (ab.Drain > 0 && u.IsAlive) Heal(u, (int)Math.Round(damage * ab.Drain), u);
                if (ab.Status is { } s && t.IsAlive && (channeled || _random.Roll(s.Chance)))
                    ApplyStatus(t, s.Id, s.Duration);
            }
            else
            {
                Emit(new AttackMissed(u, t, ab));
            }

            // A target stunned by this very hit can't strike back.
            if (t.IsAlive && u.IsAlive && f.CanCounter && !t.Has(StatusId.Stunned))
            {
                int chance = !hit ? f.CounterChanceOnMiss : crit ? f.CounterChanceOnCrit : f.CounterChance;
                if (_random.Roll(chance))
                {
                    Face(t, u.Position);
                    Emit(new Countered(t, u, f.CounterDamage, !hit && f.Parry));
                    DealDamage(u, f.CounterDamage, source: null);
                }
            }
            if (!u.IsAlive || IsOver) break;
        }
    }

    private void Pay(Unit u, AbilityDef ab)
    {
        if (ab.Cooldown > 0) u.SetCooldown(ab.Id, ab.Cooldown + 1); // +1 because it counts down at the start of each own turn
        if (ab.Cost <= 0) return;
        if (u.ResourceKind == ResourceKind.Resonance)
        {
            int hp = BloodPriceHp(u, ab);
            if (hp > 0)
            {
                u.Resource = 0;
                u.Hp = Math.Max(1, u.Hp - hp);
                u.Strain++;
                u.PaidHpThisTurn = true;
                Emit(new BloodPricePaid(u, hp, u.Strain));
                CheckBurnout(u);
                return;
            }
        }
        u.Resource -= ab.Cost;
    }

    /// <summary>HP an Unbound unit would pay for this ability (0 if Resonance covers it).</summary>
    public int BloodPriceHp(Unit u, AbilityDef ab)
    {
        if (u.ResourceKind != ResourceKind.Resonance || ab.Cost <= 0 || u.Resource >= ab.Cost) return 0;
        return (int)Math.Ceiling((ab.Cost - u.Resource) * Rules.BloodPriceHpPerResonance);
    }

    private void CheckBurnout(Unit u)
    {
        if (u.Strain < Rules.StrainLimit || u.Has(StatusId.Burnout)) return;
        u.SetStatus(StatusId.Burnout, Rules.BurnoutTurns);
        Emit(new BurnoutStarted(u, Rules.BurnoutTurns));
    }

    private void ApplyStatus(Unit t, StatusId status, int turns)
    {
        switch (status)
        {
            case StatusId.Stunned when t.Has(StatusId.StunImmune):
                Emit(new StatusResisted(t, status));
                return;
            case StatusId.Shocked:
                t.Wait -= Rules.ShockDelay;
                Emit(new TurnDelayed(t, Rules.ShockDelay));
                return;
            default:
                t.SetStatus(status, turns);
                Emit(new StatusApplied(t, status, turns));
                return;
        }
    }

    /// <param name="source">Log name for damage that has no attack event (burning, bleeding). Null when an event already reports it.</param>
    private void DealDamage(Unit u, int amount, string? source)
    {
        amount = Math.Max(1, amount);
        u.Hp = Math.Max(0, u.Hp - amount);
        if (source is not null) Emit(new UnitDamaged(u, amount, source));
        if (u.Channeling is { } c && u.IsAlive)
        {
            u.Channeling = null;
            Emit(new ChannelInterrupted(u, c.Ability));
        }
        if (u.Hp <= 0 && !u.Fallen) Fall(u);
    }

    private void Heal(Unit u, int amount, Unit? source)
    {
        int healed = Math.Min(amount, u.MaxHp - u.Hp);
        if (healed <= 0) return;
        u.Hp += healed;
        Emit(new UnitHealed(u, healed, source));
    }

    private void Fall(Unit u)
    {
        u.Fallen = true;
        u.FallenCountdown = Rules.FallenCountdown;
        u.ClearStatuses();
        u.Channeling = null;
        u.ChallengedBy = null;
        u.Strain = 0;
        Emit(new UnitFell(u, u.FallenCountdown));
        CheckForWinner();
    }

    private void Face(Unit u, Point toward)
    {
        if (toward == u.Position) return;
        var f = Point.FacingToward(u.Position, toward);
        if (f == u.Facing) return;
        u.Facing = f;
        Emit(new FacingChanged(u, f));
    }
}
