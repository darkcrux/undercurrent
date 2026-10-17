namespace Undercurrent.Core.Grid;

/// <summary>The battlefield's shape: a grid of tiles, each with a height level.</summary>
public sealed class BattleMap
{
    private readonly int[,] _heights;

    public string Id { get; }
    public string Name { get; }
    public int Columns { get; }
    public int Rows { get; }

    /// <param name="heights">One array per row (y), each holding one height per column (x).</param>
    public BattleMap(string id, string name, IReadOnlyList<IReadOnlyList<int>> heights)
    {
        if (heights.Count == 0) throw new ArgumentException("A map needs at least one row.", nameof(heights));
        Id = id;
        Name = name;
        Rows = heights.Count;
        Columns = heights[0].Count;
        _heights = new int[Columns, Rows];
        for (int y = 0; y < Rows; y++)
        {
            if (heights[y].Count != Columns)
                throw new ArgumentException($"Map '{id}': row {y} has {heights[y].Count} tiles, expected {Columns}.");
            for (int x = 0; x < Columns; x++) _heights[x, y] = heights[y][x];
        }
    }

    public bool Contains(Point p) => p.X >= 0 && p.Y >= 0 && p.X < Columns && p.Y < Rows;

    public int HeightAt(Point p) => Contains(p)
        ? _heights[p.X, p.Y]
        : throw new ArgumentOutOfRangeException(nameof(p), $"{p} is outside the map.");

    public IEnumerable<Point> AllTiles()
    {
        for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Columns; x++)
                yield return new Point(x, y);
    }

    /// <summary>Tiles within <paramref name="radius"/> steps of <paramref name="center"/> (a diamond), inside the map.</summary>
    public IEnumerable<Point> Diamond(Point center, int radius) =>
        AllTiles().Where(p => p.DistanceTo(center) <= radius);
}
