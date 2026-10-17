namespace Undercurrent.Core.Battles;

/// <summary>Source of dice rolls. Inject a seeded or scripted one to make battles repeatable.</summary>
public interface IRandom
{
    /// <summary>A number from 0 (inclusive) to 100 (exclusive).</summary>
    double NextPercent();
}

public static class RandomExtensions
{
    /// <summary>True with a <paramref name="chance"/> percent probability. 0 never succeeds, 100 always does.</summary>
    public static bool Roll(this IRandom random, int chance) =>
        chance >= 100 || (chance > 0 && random.NextPercent() < chance);
}

public sealed class SeededRandom : IRandom
{
    private readonly Random _random;
    public SeededRandom(int seed) => _random = new Random(seed);
    public double NextPercent() => _random.NextDouble() * 100.0;
}

/// <summary>Returns the given values in order, then repeats. Handy in tests: 0 makes every roll succeed, 99.9 makes every roll under 100 fail.</summary>
public sealed class ScriptedRandom : IRandom
{
    private readonly double[] _values;
    private int _next;

    public ScriptedRandom(params double[] values)
    {
        if (values.Length == 0) throw new ArgumentException("Give at least one value.", nameof(values));
        _values = values;
    }

    public double NextPercent() => _values[_next++ % _values.Length];

    public static ScriptedRandom AlwaysSucceed() => new(0);
    public static ScriptedRandom AlwaysFail() => new(99.999);
}
