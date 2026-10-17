using Godot;
using Undercurrent.Core.Battles;
using Undercurrent.Core.Data;
using Undercurrent.Core.Grid;

namespace Undercurrent.Game;

/// <summary>
/// One unit on the board: an upright billboard sprite, a facing arrow on the ground and a label with name
/// and HP. It shows what the event player tells it; <see cref="Sync"/> snaps it to the battle's state.
/// </summary>
public partial class UnitView : Node3D
{
    private const float PixelSize = 1f / 40f;

    public Unit Unit { get; private set; } = null!;
    public int ShownHp { get; private set; }

    private BoardView _board = null!;
    private Sprite3D _sprite = null!;
    private Label3D _label = null!;
    private MeshInstance3D _facingArrow = null!;
    private Color _tint = Colors.White;

    public static UnitView Create(Unit unit, BoardView board)
    {
        var view = new UnitView { Unit = unit, _board = board, Name = $"Unit_{unit.Id}_{unit.Name}" };
        view.Build();
        return view;
    }

    private void Build()
    {
        _sprite = new Sprite3D
        {
            Texture = PlaceholderArt.Unit(Unit.Class.Id, Unit.Team),
            PixelSize = PixelSize,
            Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            Shaded = false,
            Position = new Vector3(0, PlaceholderArt.Height * PixelSize / 2, 0),
        };
        AddChild(_sprite);

        _label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            FontSize = 28,
            OutlineSize = 8,
            PixelSize = 0.006f,
            Modulate = PlaceholderArt.TeamColor(Unit.Team),
            Position = new Vector3(0, PlaceholderArt.Height * PixelSize + 0.3f, 0),
        };
        AddChild(_label);

        _facingArrow = new MeshInstance3D
        {
            Mesh = ArrowMesh(),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = PlaceholderArt.TeamColor(Unit.Team),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
            Position = new Vector3(0, 0.03f, 0),
        };
        AddChild(_facingArrow);
        Sync();
    }

    /// <summary>Snaps every visual to the unit's current state in the battle.</summary>
    public void Sync(Battle? battle = null)
    {
        Position = _board.TileTop(Unit.Position);
        SetFacing(Unit.Facing);
        ShownHp = Unit.Hp;
        UpdateLabel();
        Visible = !Unit.Lost && !(battle is not null && HiddenFromPlayer(battle));
        _facingArrow.Visible = Unit.IsAlive;
        _tint = Unit.Fallen ? new Color(0.35f, 0.35f, 0.35f, 0.8f)
            : Unit.Has(StatusId.Hidden) ? new Color(1, 1, 1, 0.45f)
            : Unit.Channeling is not null ? new Color(1.15f, 0.9f, 1.3f)
            : Colors.White;
        _sprite.Modulate = _tint;
    }

    /// <summary>Enemies that are Hidden and not inside any of your units' vision aren't drawn.</summary>
    public bool HiddenFromPlayer(Battle battle) =>
        Unit.Team == Team.Enemy && Unit.IsAlive && Unit.Has(StatusId.Hidden) && !battle.IsSpotted(Unit);

    public void SetFacing(Point facing) =>
        _facingArrow.Rotation = new Vector3(0, Mathf.Atan2(-facing.X, -facing.Y), 0);

    public void ChangeHp(int delta)
    {
        ShownHp = Math.Clamp(ShownHp + delta, 0, Unit.MaxHp);
        UpdateLabel();
    }

    public void ShowFallen()
    {
        ShownHp = 0;
        _tint = new Color(0.35f, 0.35f, 0.35f, 0.8f);
        _sprite.Modulate = _tint;
        _facingArrow.Visible = false;
        UpdateLabel();
    }

    public void Flash(Color color)
    {
        var tween = CreateTween();
        tween.TweenProperty(_sprite, "modulate", color, 0.06f);
        tween.TweenProperty(_sprite, "modulate", _tint, 0.18f);
    }

    /// <summary>Walks the path one tile at a time, hopping when the height changes.</summary>
    public async Task MoveAlong(IReadOnlyList<Point> path, float secondsPerTile)
    {
        // The battle already has the unit at the destination; walk from the tile it is drawn on.
        var current = Position;
        var at = new Point(Mathf.RoundToInt(Position.X), Mathf.RoundToInt(Position.Z));
        foreach (var step in path)
        {
            SetFacing(Point.FacingToward(at, step));
            var target = _board.TileTop(step);
            var tween = CreateTween();
            if (Mathf.Abs(target.Y - current.Y) > 0.01f)
            {
                var peak = (current + target) / 2 + new Vector3(0, Mathf.Max(current.Y, target.Y) - (current.Y + target.Y) / 2 + 0.4f, 0);
                tween.TweenProperty(this, "position", peak, secondsPerTile / 2);
                tween.TweenProperty(this, "position", target, secondsPerTile / 2);
            }
            else
            {
                tween.TweenProperty(this, "position", target, secondsPerTile);
            }
            await ToSignal(tween, Tween.SignalName.Finished);
            current = target;
            at = step;
        }
    }

    private void UpdateLabel()
    {
        string state = Unit.Lost ? "Lost"
            : Unit.Fallen ? $"Fallen ({Unit.FallenCountdown})"
            : $"{ShownHp}/{Unit.MaxHp}";
        _label.Text = $"{Unit.Name}\n{state}";
    }

    private static ArrayMesh ArrowMesh()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.AddVertex(new Vector3(0, 0, -0.48f));
        st.AddVertex(new Vector3(0.2f, 0, -0.22f));
        st.AddVertex(new Vector3(-0.2f, 0, -0.22f));
        return st.Commit();
    }
}
