namespace Undercurrent.Core.Grid;

/// <summary>A tile position on the battle grid. Also used as a facing direction (one of the four unit vectors).</summary>
public readonly record struct Point(int X, int Y)
{
    // Grid directions. On the isometric screen, North is up-right, East is down-right,
    // South is down-left and West is up-left.
    public static readonly Point North = new(0, -1);
    public static readonly Point East = new(1, 0);
    public static readonly Point South = new(0, 1);
    public static readonly Point West = new(-1, 0);

    public static readonly IReadOnlyList<Point> Directions = new[] { North, East, South, West };

    public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);
    public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);

    public int DistanceTo(Point other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    public bool IsDirection => Math.Abs(X) + Math.Abs(Y) == 1;

    /// <summary>The facing that points from <paramref name="from"/> toward <paramref name="to"/> (dominant axis wins).</summary>
    public static Point FacingToward(Point from, Point to)
    {
        int dx = to.X - from.X, dy = to.Y - from.Y;
        if (dx == 0 && dy == 0) return North;
        return Math.Abs(dx) >= Math.Abs(dy) ? new Point(Math.Sign(dx), 0) : new Point(0, Math.Sign(dy));
    }

    public static Point ParseFacing(string? name) => (name ?? "north").ToLowerInvariant() switch
    {
        "north" => North,
        "east" => East,
        "south" => South,
        "west" => West,
        _ => throw new FormatException($"Unknown facing '{name}'. Use north, east, south or west."),
    };

    public override string ToString() => $"({X},{Y})";
}
