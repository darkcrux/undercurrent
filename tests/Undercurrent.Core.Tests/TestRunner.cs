using System.Reflection;

namespace Undercurrent.Core.Tests;

/// <summary>Marks a method as a test. Public static or instance, no parameters.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute { }

public sealed class AssertionException(string message) : Exception(message);

public static class Assert
{
    public static void True(bool condition, string what = "condition")
    {
        if (!condition) throw new AssertionException($"Expected {what} to be true.");
    }

    public static void False(bool condition, string what = "condition")
    {
        if (condition) throw new AssertionException($"Expected {what} to be false.");
    }

    public static void Equal<T>(T expected, T actual, string what = "value")
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new AssertionException($"Expected {what} to be {expected}, but it was {actual}.");
    }

    public static void NotNull(object? value, string what = "value")
    {
        if (value is null) throw new AssertionException($"Expected {what} not to be null.");
    }

    public static TException Throws<TException>(Action action, string what = "action") where TException : Exception
    {
        try { action(); }
        catch (TException e) { return e; }
        catch (Exception e) { throw new AssertionException($"Expected {what} to throw {typeof(TException).Name}, but it threw {e.GetType().Name}: {e.Message}"); }
        throw new AssertionException($"Expected {what} to throw {typeof(TException).Name}, but it didn't.");
    }
}

public static class Program
{
    /// <summary>Runs every [Test]. Pass words to run only tests whose names contain one of them.</summary>
    public static int Main(string[] args)
    {
        var tests = typeof(Program).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<TestAttribute>() is not null)
                .Select(m => (Type: t, Method: m)))
            .Where(x => args.Length == 0 || args.Any(a => $"{x.Type.Name}.{x.Method.Name}".Contains(a, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.Type.Name).ThenBy(x => x.Method.Name)
            .ToList();

        int passed = 0;
        var failures = new List<string>();
        foreach (var (type, method) in tests)
        {
            string name = $"{type.Name}.{method.Name}";
            try
            {
                object? instance = method.IsStatic ? null : Activator.CreateInstance(type);
                method.Invoke(instance, null);
                passed++;
                Console.WriteLine($"  pass  {name}");
            }
            catch (TargetInvocationException e) when (e.InnerException is not null)
            {
                failures.Add($"{name}: {e.InnerException.Message}");
                Console.WriteLine($"  FAIL  {name}\n        {e.InnerException.GetType().Name}: {e.InnerException.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{passed} passed, {failures.Count} failed, {tests.Count} total.");
        return failures.Count == 0 ? 0 : 1;
    }
}
