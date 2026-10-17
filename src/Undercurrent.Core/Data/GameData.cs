using System.Text.Json;
using System.Text.Json.Serialization;

namespace Undercurrent.Core.Data;

/// <summary>All game data loaded from JSON, checked for broken references.</summary>
public sealed class GameData
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public RulesConfig Rules { get; }
    public IReadOnlyDictionary<string, AbilityDef> Abilities { get; }
    public IReadOnlyDictionary<string, ClassDef> Classes { get; }
    public IReadOnlyDictionary<string, UnitTemplate> Units { get; }
    public IReadOnlyDictionary<string, MapDef> Maps { get; }

    public GameData(RulesConfig rules, IEnumerable<AbilityDef> abilities, IEnumerable<ClassDef> classes,
        IEnumerable<UnitTemplate> units, IEnumerable<MapDef> maps)
    {
        Rules = rules;
        Abilities = ToDictionary(abilities, a => a.Id, "ability");
        Classes = ToDictionary(classes, c => c.Id, "class");
        Units = ToDictionary(units, u => u.Id, "unit");
        Maps = ToDictionary(maps, m => m.Id, "map");
        Validate();
    }

    /// <summary>Loads rules.json, abilities.json, classes.json, units.json and maps/*.json from a folder.</summary>
    public static GameData LoadFromDirectory(string directory)
    {
        T Read<T>(string file) where T : class
        {
            string path = Path.Combine(directory, file);
            if (!File.Exists(path)) throw new FileNotFoundException($"Missing data file: {path}");
            return Parse<T>(File.ReadAllText(path), path);
        }

        string mapsDir = Path.Combine(directory, "maps");
        var maps = Directory.Exists(mapsDir)
            ? Directory.GetFiles(mapsDir, "*.json").OrderBy(f => f).Select(f => Parse<MapDef>(File.ReadAllText(f), f))
            : Enumerable.Empty<MapDef>();

        return new GameData(
            Read<RulesConfig>("rules.json"),
            Read<List<AbilityDef>>("abilities.json"),
            Read<List<ClassDef>>("classes.json"),
            Read<List<UnitTemplate>>("units.json"),
            maps.ToList());
    }

    /// <summary>
    /// Loads through any file reader, for engines that pack data into the game (in Godot, read with FileAccess
    /// from res://data). Paths are relative to the data folder, such as "rules.json" or "maps/ashfield-test.json".
    /// </summary>
    public static GameData Load(Func<string, string> readText, IEnumerable<string> mapFiles) => new(
        Parse<RulesConfig>(readText("rules.json"), "rules.json"),
        Parse<List<AbilityDef>>(readText("abilities.json"), "abilities.json"),
        Parse<List<ClassDef>>(readText("classes.json"), "classes.json"),
        Parse<List<UnitTemplate>>(readText("units.json"), "units.json"),
        mapFiles.Select(f => Parse<MapDef>(readText(f), f)).ToList());

    /// <summary>Walks up from <paramref name="start"/> until it finds a folder containing data/rules.json.</summary>
    public static string FindDataDirectory(string? start = null)
    {
        var dir = new DirectoryInfo(start ?? AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "rules.json"))) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Couldn't find a data folder with rules.json above " + (start ?? AppContext.BaseDirectory));
    }

    public static T Parse<T>(string json, string source = "json") where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                ?? throw new InvalidDataException($"{source} is empty.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{source}: {e.Message}", e);
        }
    }

    public AbilityDef Ability(string id) => Abilities.TryGetValue(id, out var a) ? a : throw new KeyNotFoundException($"No ability '{id}'.");
    public ClassDef Class(string id) => Classes.TryGetValue(id, out var c) ? c : throw new KeyNotFoundException($"No class '{id}'.");
    public UnitTemplate Unit(string id) => Units.TryGetValue(id, out var u) ? u : throw new KeyNotFoundException($"No unit '{id}'.");
    public MapDef Map(string id) => Maps.TryGetValue(id, out var m) ? m : throw new KeyNotFoundException($"No map '{id}'.");

    private void Validate()
    {
        var problems = new List<string>();
        foreach (var c in Classes.Values)
            foreach (var a in c.Abilities)
                if (!Abilities.ContainsKey(a)) problems.Add($"Class '{c.Id}' lists unknown ability '{a}'.");
        foreach (var u in Units.Values)
            if (!Classes.ContainsKey(u.Class)) problems.Add($"Unit '{u.Id}' uses unknown class '{u.Class}'.");
        foreach (var m in Maps.Values)
        {
            if (m.Heights.Count == 0 || m.Heights.Any(r => r.Count != m.Heights[0].Count))
                problems.Add($"Map '{m.Id}' heights must be a non-empty rectangle.");
            foreach (var p in m.Units)
                if (!Units.ContainsKey(p.Unit)) problems.Add($"Map '{m.Id}' places unknown unit '{p.Unit}'.");
        }
        foreach (var a in Abilities.Values)
        {
            if (a.Target == TargetKind.Self && (a.Area > 0 || a.DealsDamage))
                problems.Add($"Ability '{a.Id}' targets self but deals damage or has an area.");
            if (a.Range is { } r && (r.Min < 0 || r.Max < r.Min))
                problems.Add($"Ability '{a.Id}' has an invalid range {r}.");
        }
        if (problems.Count > 0)
            throw new InvalidDataException("Game data has problems:\n- " + string.Join("\n- ", problems));
    }

    private static Dictionary<string, T> ToDictionary<T>(IEnumerable<T> items, Func<T, string> key, string kind)
    {
        var dict = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            string k = key(item);
            if (string.IsNullOrWhiteSpace(k)) throw new InvalidDataException($"A {kind} has no id.");
            if (!dict.TryAdd(k, item)) throw new InvalidDataException($"Duplicate {kind} id '{k}'.");
        }
        return dict;
    }
}
