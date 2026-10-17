using Undercurrent.Core.Ai;
using Undercurrent.Core.Battles;
using Undercurrent.Core.Data;

// Runs AI-vs-AI battles and prints win rates and per-unit numbers, for balancing.
//
//   dotnet run --project tools/Undercurrent.Sim                       200 battles on ashfield-test
//   dotnet run --project tools/Undercurrent.Sim -- --battles 1000 --map ashfield-test --seed 1
//   dotnet run --project tools/Undercurrent.Sim -- --log 3             print the full log of battle 3

int battles = 200, seed = 1, logBattle = 0;
string mapId = "ashfield-test";
string? dataDir = null;
for (int i = 0; i < args.Length; i++)
{
    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");
    switch (args[i])
    {
        case "--battles": battles = int.Parse(Next()); break;
        case "--seed": seed = int.Parse(Next()); break;
        case "--map": mapId = Next(); break;
        case "--data": dataDir = Next(); break;
        case "--log": logBattle = int.Parse(Next()); break;
        default: Console.Error.WriteLine($"Unknown option {args[i]}"); return 1;
    }
}

var data = GameData.LoadFromDirectory(dataDir ?? GameData.FindDataDirectory(Directory.GetCurrentDirectory()));
if (logBattle > 0)
{
    var b = RunBattle(logBattle);
    foreach (var e in b.History) Console.WriteLine(e.Text);
    return 0;
}

int playerWins = 0, stalls = 0;
long totalTurns = 0;
var stats = new Dictionary<string, UnitStats>();
for (int n = 1; n <= battles; n++)
{
    var b = RunBattle(n);
    if (!b.IsOver) { stalls++; continue; }
    if (b.Winner == Team.Player) playerWins++;
    totalTurns += b.TurnNumber;
    foreach (var e in b.History)
    {
        switch (e)
        {
            case AttackHit h: Get(h.Attacker).Damage += h.Damage; Get(h.Attacker).Hits++; if (h.Critical) Get(h.Attacker).Crits++; break;
            case AttackMissed m: Get(m.Attacker).Misses++; break;
            case Countered c: Get(c.Defender).Counters++; Get(c.Defender).Damage += c.Damage; Get(c.Attacker).CountersTaken++; break;
            case UnitFell f: Get(f.Unit).Falls++; break;
            case BloodPricePaid p: Get(p.Unit).BloodPrice += p.Hp; break;
            case BurnoutStarted bo: Get(bo.Unit).Burnouts++; break;
        }
    }
}

int done = battles - stalls;
Console.WriteLine($"Map {mapId}: {battles} battles, seeds {seed} to {seed + battles - 1}");
Console.WriteLine($"Player side wins {Pct(playerWins, done)} ({playerWins}/{done}); average {(done == 0 ? 0 : totalTurns / (double)done):0.0} turns per battle; {stalls} stalled.");
Console.WriteLine();
Console.WriteLine($"{"Unit",-14}{"dmg/battle",11}{"hit %",8}{"crit %",8}{"counters",10}{"countered",11}{"falls %",9}{"blood HP",10}{"burnouts",10}");
foreach (var (name, s) in stats.OrderBy(kv => kv.Key))
{
    int swings = s.Hits + s.Misses;
    Console.WriteLine($"{name,-14}{s.Damage / (double)Math.Max(1, done),11:0.0}{Pct(s.Hits, swings),8}{Pct(s.Crits, s.Hits),8}{s.Counters / (double)Math.Max(1, done),10:0.00}{s.CountersTaken / (double)Math.Max(1, done),11:0.00}{Pct(s.Falls, done),9}{s.BloodPrice / (double)Math.Max(1, done),10:0.0}{s.Burnouts / (double)Math.Max(1, done),10:0.00}");
}
return 0;

Battle RunBattle(int n)
{
    var b = Battle.FromMap(data, mapId, new SeededRandom(seed + n - 1));
    b.Start();
    for (int t = 0; t < 1000 && !b.IsOver; t++) EnemyAi.TakeTurn(b);
    return b;
}

UnitStats Get(Unit u)
{
    string key = $"{u.Name} ({(u.Team == Team.Player ? "P" : "E")})";
    if (!stats.TryGetValue(key, out var s)) stats[key] = s = new UnitStats();
    return s;
}

static string Pct(int part, int whole) => whole == 0 ? "-" : $"{100.0 * part / whole:0}%";

sealed class UnitStats
{
    public int Damage, Hits, Misses, Crits, Counters, CountersTaken, Falls, BloodPrice, Burnouts;
}
