using Godot;
using Undercurrent.Core.Battles;
using Undercurrent.Core.Data;

namespace Undercurrent.Game;

/// <summary>
/// Plays the events from <see cref="Battle.TakeNewEvents"/> in order. The battle has already resolved
/// everything, so views are driven by the numbers in each event (not by live unit state), and
/// <see cref="BattleController"/> snaps every view to the real state once the queue is done.
/// </summary>
public partial class EventPlayer : Node
{
    private static readonly Color Damage = new(1f, 0.85f, 0.8f), Crit = new(1f, 0.8f, 0.2f), Heal = new(0.5f, 1f, 0.55f),
        Blood = new(0.95f, 0.2f, 0.25f), Info = new(0.85f, 0.85f, 0.95f), Miss = new(0.7f, 0.7f, 0.7f);

    /// <summary>Multiplies every pause and animation; small values make auto-play fast.</summary>
    public float Speed = 1f;

    private Battle _battle = null!;
    private BoardView _board = null!;
    private CameraRig _camera = null!;
    private Hud _hud = null!;
    private Node3D _world = null!;
    private Dictionary<int, UnitView> _units = null!;
    private Dictionary<int, DeployableView> _devices = null!;

    public void Bind(Battle battle, BoardView board, CameraRig camera, Hud hud, Node3D world,
        Dictionary<int, UnitView> units, Dictionary<int, DeployableView> devices)
    {
        (_battle, _board, _camera, _hud, _world, _units, _devices) = (battle, board, camera, hud, world, units, devices);
    }

    public async Task Play(IReadOnlyList<BattleEvent> events)
    {
        foreach (var e in events)
        {
            // Don't give away where an unseen Hidden enemy went.
            _hud.Log(e is UnitMoved m && !Visible(m.Unit) ? $"{m.Unit.Name} moves unseen." : e.Text, LogColor(e));
            await PlayOne(e);
            if (!IsInstanceValid(this)) return;
        }
    }

    private async Task PlayOne(BattleEvent e)
    {
        switch (e)
        {
            case TurnStarted t:
                _board.SetActive(Visible(t.Unit) ? t.Unit.Position : null);
                if (Visible(t.Unit)) _camera.Focus(View(t.Unit).Position, 0.3f * Speed);
                await Pause(0.25f);
                break;

            case UnitMoved m:
                _board.SetActive(null);
                if (Visible(m.Unit)) await View(m.Unit).MoveAlong(m.Path, 0.16f * Speed);
                else View(m.Unit).Position = _board.TileTop(m.Path[^1]);
                _board.SetActive(Visible(m.Unit) ? m.Unit.Position : null);
                break;

            case FacingChanged f:
                View(f.Unit).SetFacing(f.Facing);
                break;

            case AbilityUsed a when a.Ability.Id != Battle.BasicAttackId || a.Channeled:
                Float(a.Unit, a.Channeled ? $"{a.Ability.Name}!" : a.Ability.Name, Info, 0.8f);
                await Pause(0.35f);
                break;

            case AttackHit h:
                View(h.Target).Flash(new Color(1.6f, 0.4f, 0.4f));
                View(h.Target).ChangeHp(-h.Damage);
                Float(h.Target, h.Critical ? $"{h.Damage}!" : h.Damage.ToString(), h.Critical ? Crit : Damage, h.Critical ? 1.3f : 1f);
                await Pause(0.45f);
                break;

            case AttackMissed miss:
                Float(miss.Target, "Miss", Miss);
                await Pause(0.4f);
                break;

            case Countered c:
                Float(c.Defender, c.Parry ? "Parry" : "Counter", Info, 0.8f);
                await Pause(0.2f);
                View(c.Attacker).Flash(new Color(1.6f, 0.4f, 0.4f));
                View(c.Attacker).ChangeHp(-c.Damage);
                Float(c.Attacker, c.Damage.ToString(), Damage);
                await Pause(0.45f);
                break;

            case UnitDamaged d:
                View(d.Unit).ChangeHp(-d.Amount);
                Float(d.Unit, $"{d.Amount} {d.Source}", Damage, 0.8f);
                await Pause(0.4f);
                break;

            case UnitHealed h:
                View(h.Unit).ChangeHp(h.Amount);
                Float(h.Unit, $"+{h.Amount}", Heal);
                await Pause(0.35f);
                break;

            case BloodPricePaid bp:
                View(bp.Unit).ChangeHp(-bp.Hp);
                Float(bp.Unit, $"-{bp.Hp} Blood Price", Blood, 0.8f);
                await Pause(0.4f);
                break;

            case StatusApplied s:
                Float(s.Unit, s.Status.ToString(), Info, 0.8f);
                if (s.Status == StatusId.Hidden) View(s.Unit).Sync(_battle);
                await Pause(0.3f);
                break;

            case StatusResisted r:
                Float(r.Unit, $"Resists {r.Status}", Info, 0.7f);
                await Pause(0.3f);
                break;

            case TurnDelayed d:
                Float(d.Unit, "Shocked", Info, 0.8f);
                await Pause(0.3f);
                break;

            case StatusEnded { Status: StatusId.Hidden } s when !Visible(s.Unit):
                // A Hidden enemy that attacks shows itself where it stands.
                View(s.Unit).Visible = true;
                break;

            case HiddenRevealed r:
                View(r.Unit).Visible = true;
                Float(r.Unit, "Revealed", Info, 0.8f);
                await Pause(0.3f);
                break;

            case ChannelStarted c:
                Float(c.Unit, $"Channeling {c.Ability.Name}", new Color(0.75f, 0.55f, 1f), 0.7f);
                await Pause(0.4f);
                break;

            case ChannelInterrupted c:
                Float(c.Unit, "Interrupted", Info, 0.7f);
                await Pause(0.3f);
                break;

            case AbilityFizzled f:
                Float(f.Unit, "No effect", Miss, 0.7f);
                await Pause(0.3f);
                break;

            case UnitChallenged c:
                Float(c.Target, "Challenged", Info, 0.8f);
                await Pause(0.3f);
                break;

            case DeployablePlaced p:
                var device = DeployableView.Create(p.Deployable, _board);
                _devices[p.Deployable.Id] = device;
                _world.AddChild(device);
                await Pause(0.3f);
                break;

            case DeployableFired f:
                View(f.Target).Flash(new Color(1.6f, 0.4f, 0.4f));
                View(f.Target).ChangeHp(-f.Damage);
                Float(f.Target, f.Damage.ToString(), Damage);
                await Pause(0.4f);
                break;

            case BurnoutStarted b:
                Float(b.Unit, "Burnout", Blood, 0.9f);
                await Pause(0.35f);
                break;

            case BurnoutEnded b:
                Float(b.Unit, "Recovered", Info, 0.7f);
                await Pause(0.3f);
                break;

            case TurnSkipped s:
                _board.SetActive(s.Unit.Position);
                Float(s.Unit, s.Reason, Info, 0.8f);
                await Pause(0.4f);
                break;

            case UnitFell f:
                View(f.Unit).ShowFallen();
                Float(f.Unit, "Falls", Blood, 0.9f);
                await Pause(0.5f);
                break;

            case UnitLost l:
                View(l.Unit).Visible = false;
                break;

            case TurnEnded:
                _board.SetActive(null);
                break;
        }
    }

    private UnitView View(Unit u) => _units[u.Id];

    private bool Visible(Unit u) => View(u).Visible;

    private void Float(Unit u, string text, Color color, float scale = 1f)
    {
        var view = View(u);
        if (!view.Visible) return;
        FloatingText.Spawn(_world, view.Position + new Vector3(0, 1.6f, 0), text, color, scale, Mathf.Max(0.3f, 1.0f * Speed));
    }

    private async Task Pause(float seconds)
    {
        if (seconds * Speed <= 0.001f) return;
        await ToSignal(GetTree().CreateTimer(seconds * Speed), SceneTreeTimer.SignalName.Timeout);
    }

    private static Color? LogColor(BattleEvent e) => e switch
    {
        TurnStarted t => PlaceholderArt.TeamColor(t.Unit.Team),
        UnitFell or UnitLost or BloodPricePaid => Blood,
        BattleEnded => Crit,
        _ => null,
    };
}
