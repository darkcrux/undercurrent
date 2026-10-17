using Godot;
using Undercurrent.Core.Data;

namespace Undercurrent.Game;

/// <summary>Generated stand-in sprites until real pixel art exists: a silhouette per class, outlined by team.</summary>
public static class PlaceholderArt
{
    public const int Width = 32, Height = 48;

    private static readonly Dictionary<string, Color> ClassColors = new()
    {
        ["vanguard"] = new(0.45f, 0.55f, 0.70f),
        ["mercenary"] = new(0.55f, 0.50f, 0.42f),
        ["skirmisher"] = new(0.35f, 0.45f, 0.35f),
        ["gunner"] = new(0.75f, 0.60f, 0.30f),
        ["rifleman"] = new(0.62f, 0.52f, 0.32f),
        ["acolyte"] = new(0.70f, 0.66f, 0.85f),
        ["elementalist"] = new(0.58f, 0.36f, 0.88f), // the Current glows violet
        ["bloodcaller"] = new(0.78f, 0.16f, 0.20f),  // Unbound power glows red
    };

    private static readonly Dictionary<(string, Team), ImageTexture> Cache = new();

    public static Color ClassColor(string classId) =>
        ClassColors.TryGetValue(classId, out var c) ? c : new Color(0.6f, 0.6f, 0.6f);

    public static Color TeamColor(Team team) =>
        team == Team.Player ? new Color(0.55f, 0.85f, 1.0f) : new Color(1.0f, 0.40f, 0.35f);

    public static ImageTexture Unit(string classId, Team team)
    {
        if (Cache.TryGetValue((classId, team), out var cached)) return cached;

        var body = ClassColor(classId);
        var image = Image.CreateEmpty(Width, Height, false, Image.Format.Rgba8);
        bool[,] fill = new bool[Width, Height];
        void Mark(int x, int y) { if (x >= 0 && y >= 0 && x < Width && y < Height) fill[x, y] = true; }

        // Head, torso, legs.
        for (int y = 4; y <= 15; y++)
            for (int x = 10; x <= 21; x++)
                if ((x - 15.5f) * (x - 15.5f) + (y - 9.5f) * (y - 9.5f) <= 36) Mark(x, y);
        for (int y = 16; y <= 33; y++)
        {
            int half = 7 + (y - 16) / 6;
            for (int x = 16 - half; x < 16 + half; x++) Mark(x, y);
        }
        for (int y = 34; y <= 45; y++)
        {
            for (int x = 10; x <= 14; x++) Mark(x, y);
            for (int x = 17; x <= 21; x++) Mark(x, y);
        }

        var outline = TeamColor(team);
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (fill[x, y])
                {
                    float shade = 1f - 0.25f * y / Height;
                    image.SetPixel(x, y, new Color(body.R * shade, body.G * shade, body.B * shade));
                }
                else if (Neighbour(fill, x, y))
                {
                    image.SetPixel(x, y, outline);
                }
            }

        var texture = ImageTexture.CreateFromImage(image);
        Cache[(classId, team)] = texture;
        return texture;
    }

    private static bool Neighbour(bool[,] fill, int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < Width && ny < Height && fill[nx, ny]) return true;
            }
        return false;
    }
}
