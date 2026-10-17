# Undercurrent (working title)

The game's rules as a plain C# library, with no engine code. Godot (or any engine) references it,
sends it commands, and animates the events it reports. This is milestone M1 of the vertical slice:
the browser prototype's combat, ported and tested.

## What's here

```
Undercurrent.sln
src/Undercurrent.Core/           the rules library (net8.0, no dependencies)
  Grid/                      Point, BattleMap
  Data/                      JSON definitions, RulesConfig, GameData loader
  Battles/                   Battle (state, turns, actions, combat math), Unit, events, forecast
  Ai/                        EnemyAi (the prototype's AI)
tests/Undercurrent.Core.Tests/   38 rule tests with a small built-in runner
tools/Undercurrent.Sim/          AI-vs-AI battle simulator for balancing
data/                        every tuning number and definition
  rules.json                 counters, hit chance, crits, Blood Price, statuses, turret...
  abilities.json  classes.json  units.json  maps/
```

## Run it

Needs the .NET 8 SDK or newer (the .NET 10 SDK that comes with Homebrew's Godot works). The library targets net8.0 for Godot compatibility; the test and simulator programs roll forward to newer runtimes.

```
dotnet build
dotnet run --project tests/Undercurrent.Core.Tests            # all tests
dotnet run --project tests/Undercurrent.Core.Tests -- Counter  # only tests whose names contain "Counter"
dotnet run --project tools/Undercurrent.Sim -- --battles 500   # win rates and per-unit numbers
dotnet run --project tools/Undercurrent.Sim -- --log 3         # full log of one battle
```

Balancing means editing `data/*.json` and rerunning the simulator. No code changes needed.

## How the engine uses it

```csharp
var data = GameData.LoadFromDirectory("path/to/data");
var battle = Battle.FromMap(data, "ashfield-test", new SeededRandom(seed));
battle.Start();

while (!battle.IsOver)
{
    var unit = battle.Active!;
    if (unit.Team == Team.Enemy)
    {
        var plan = EnemyAi.Plan(battle);      // show intent, then:
        EnemyAi.Execute(battle, plan);
    }
    else
    {
        // From player input:
        // battle.ReachableTiles(unit)               -> highlight move range
        // battle.Move(tile)
        // battle.CanUse("spark")                    -> enable or grey out the button (and why)
        // battle.TilesInRange(unit, ability)        -> highlight targets
        // battle.Forecast(unit, target, ability)    -> the attack preview panel
        // battle.UseAbility("spark", tile, channel: false)
        // battle.EndTurn(Point.North)               -> facing chosen by the player
    }

    foreach (var e in battle.TakeNewEvents())
        Play(e);   // UnitMoved, AttackHit, Countered, StatusApplied, UnitFell, ...
}
```

Every event has a `Text` for the battle log. `battle.PredictTurnOrder(8)` feeds the turn order strip.

### In Godot (next milestone)

Reference the library from the Godot C# project's `.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="../src/Undercurrent.Core/Undercurrent.Core.csproj" />
</ItemGroup>
```

Exported games pack files inside the game, so load data through Godot's `FileAccess`:

```csharp
var data = GameData.Load(
    path => FileAccess.GetFileAsString($"res://data/{path}"),
    new[] { "maps/ashfield-test.json" });
```

## Rules ported from the prototype

Speed-based wait timer · move and act in either order · facing · hit chance from HIT and speed with
flank and back bonuses · percent-reduction damage · crits · counters at 75/50/25% by angle, lowered
by height, raised by Braced, halved by crits, Parry on misses, blocked by Hidden, Suppressed, Stunned
and reach · vision cones and spotting · Hidden (area attacks reveal) · cooldowns, Charge, Focus,
Resonance and the Blood Price with Strain and Burnout · channeling and interruption · the turret ·
Burning, Bleeding, Shocked, Frozen, Stunned with immunity, Challenged · fallen countdown · gun range
from high ground · act-only abilities · deterministic battles from a seed.

## Known gaps

- The AI doesn't weigh how dangerous a tile is, so fragile casters walk into the open
  (the simulator shows the Elementalist falling in every battle). First thing to improve.
- Hit chances come out at 93 to 100% with current stats. Worth tuning in `rules.json` or unit stats.
- Not yet in: class mastery and switching, borrowed abilities, items, revives, Exposed sources,
  the remaining statuses and classes, difficulty modes.

## Tests without NuGet

The tests use a tiny runner (`TestRunner.cs`) because this was built where NuGet wasn't reachable.
To move to xUnit later: add the xUnit packages, replace `[Test]` with `[Fact]`, delete `TestRunner.cs`,
and drop the description argument from `Assert.Equal` calls (xUnit's `Equal` has no message parameter).
