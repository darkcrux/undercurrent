using Godot;
using Undercurrent.Core.Grid;

namespace Undercurrent.Game;

public enum Highlight { None, Move, Range, Facing, Area, Choice }

/// <summary>
/// The tile board: one box column per tile, built from a map's heights, with a colored overlay on each
/// top face and a collider for mouse picking. Sizes here are rendering values, not rules.
/// </summary>
public partial class BoardView : Node3D
{
    [Export] public float HeightStep = 0.5f;
    [Export] public float BaseDepth = 0.6f;

    private static readonly Color[] GroundColors =
    {
        new(0.33f, 0.31f, 0.28f), new(0.40f, 0.36f, 0.30f), new(0.47f, 0.42f, 0.34f),
        new(0.54f, 0.49f, 0.40f), new(0.60f, 0.56f, 0.47f),
    };

    private static readonly Dictionary<Highlight, Color> HighlightColors = new()
    {
        [Highlight.Move] = new(0.30f, 0.60f, 1.00f, 0.45f),
        [Highlight.Range] = new(1.00f, 0.30f, 0.25f, 0.35f),
        [Highlight.Facing] = new(1.00f, 0.85f, 0.30f, 0.30f),
        [Highlight.Area] = new(1.00f, 0.55f, 0.15f, 0.60f),
        [Highlight.Choice] = new(1.00f, 0.95f, 0.50f, 0.70f),
    };

    private BattleMap _map = null!;
    private readonly Dictionary<Point, MeshInstance3D> _overlays = new();
    private readonly Dictionary<Point, Highlight> _highlights = new();
    private readonly Dictionary<ulong, Point> _colliders = new();
    private readonly Dictionary<Highlight, StandardMaterial3D> _highlightMaterials = new();
    private MeshInstance3D _cursor = null!;
    private MeshInstance3D _activeRing = null!;

    public BattleMap Map => _map;

    public void Build(BattleMap map)
    {
        _map = map;
        foreach (var (kind, color) in HighlightColors) _highlightMaterials[kind] = Overlay(color);

        var overlayMesh = new PlaneMesh { Size = new Vector2(0.92f, 0.92f) };
        foreach (var p in map.AllTiles())
        {
            int h = map.HeightAt(p);
            float columnHeight = BaseDepth + h * HeightStep;
            var top = TileTop(p);

            // Alternate shades so individual tiles read on flat ground.
            var ground = GroundColors[Math.Clamp(h, 0, GroundColors.Length - 1)];
            if ((p.X + p.Y) % 2 == 1) ground = ground.Darkened(0.08f);
            var column = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1, columnHeight, 1) },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = ground, Roughness = 0.95f },
                Position = top - new Vector3(0, columnHeight / 2, 0),
                Name = $"Tile_{p.X}_{p.Y}",
            };
            AddChild(column);

            var body = new StaticBody3D();
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1, columnHeight, 1) } });
            column.AddChild(body);
            _colliders[body.GetInstanceId()] = p;

            var overlay = new MeshInstance3D { Mesh = overlayMesh, Position = top + new Vector3(0, 0.01f, 0), Visible = false };
            AddChild(overlay);
            _overlays[p] = overlay;
        }

        _cursor = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(1.0f, 1.0f) },
            MaterialOverride = Overlay(new Color(1, 1, 1, 0.25f)),
            Visible = false,
        };
        AddChild(_cursor);

        _activeRing = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.32f, OuterRadius = 0.42f, Rings = 24, RingSegments = 6 },
            MaterialOverride = Overlay(new Color(1.0f, 0.9f, 0.4f, 0.9f)),
            Scale = new Vector3(1, 0.15f, 1),
            Visible = false,
        };
        AddChild(_activeRing);
    }

    /// <summary>The world position of the centre of a tile's top face.</summary>
    public Vector3 TileTop(Point p) => new(p.X, _map.HeightAt(p) * HeightStep, p.Y);

    public Vector3 Center => new((_map.Columns - 1) / 2f, 0, (_map.Rows - 1) / 2f);

    public void ClearHighlights()
    {
        _highlights.Clear();
        foreach (var o in _overlays.Values) o.Visible = false;
    }

    /// <summary>Marks tiles; a stronger highlight (later in the enum) wins over a weaker one.</summary>
    public void SetHighlight(IEnumerable<Point> tiles, Highlight kind)
    {
        foreach (var p in tiles)
        {
            if (!_overlays.TryGetValue(p, out var overlay)) continue;
            if (_highlights.TryGetValue(p, out var existing) && existing >= kind) continue;
            _highlights[p] = kind;
            overlay.MaterialOverride = _highlightMaterials[kind];
            overlay.Visible = true;
        }
    }

    public void SetCursor(Point? tile)
    {
        _cursor.Visible = tile is not null && _map.Contains(tile.Value);
        if (_cursor.Visible) _cursor.Position = TileTop(tile!.Value) + new Vector3(0, 0.02f, 0);
    }

    public void SetActive(Point? tile)
    {
        _activeRing.Visible = tile is not null;
        if (tile is { } t) _activeRing.Position = TileTop(t) + new Vector3(0, 0.03f, 0);
    }

    /// <summary>The tile under a screen position, or null.</summary>
    public Point? PickTile(Camera3D camera, Vector2 screen)
    {
        var from = camera.ProjectRayOrigin(screen);
        var to = from + camera.ProjectRayNormal(screen) * 500f;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to));
        if (hit.Count == 0) return null;
        var id = hit["collider_id"].AsUInt64();
        return _colliders.TryGetValue(id, out var p) ? p : null;
    }

    private static StandardMaterial3D Overlay(Color color) => new()
    {
        AlbedoColor = color,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };
}
