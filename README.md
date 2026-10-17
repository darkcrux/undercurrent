# Undercurrent (working title)

The game's rules as a plain C# library, with no engine code, and a Godot project that plays a battle on a
3D board through it. Godot sends the library commands and animates the events it reports. Milestones M1
(the browser prototype's combat, ported and tested) and M2 (a battle on a 3D board) of the vertical slice.

## What's here

```
Undercurrent.sln
src/Undercurrent.Core/           the rules library (net8.0, no dependencies)
  Grid/                      Point, BattleMap
  Data/                      JSON definitions, RulesConfig, GameData loader
  Battles/                   Battle (state, turns, actions, combat math), Unit, events, forecast
  Ai/                        EnemyAi (the prototype's AI)
tests/Undercurrent.Core.Tests/   39 rule tests with a small built-in runner
tools/Undercurrent.Sim/          AI-vs-AI battle simulator for balancing
data/                        every tuning number and definition
  rules.json                 counters, hit chance, crits, Blood Price, statuses, turret...
  abilities.json  classes.json  units.json  maps/
game/                        the Godot 4.7 .NET project (renders, takes input, plays events)
  project.godot  Undercurrent.Game.csproj  Undercurrent.Game.sln
  scenes/Battle.tscn         the battle scene; everything else is built in code
  scripts/                   BattleController, BoardView, CameraRig, UnitView, EventPlayer, Hud, ...
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

`dotnet build` also compiles the Godot project (it needs the `Godot.NET.Sdk` package from NuGet once).

## Play the battle in Godot

Open `game/project.godot` in Godot 4.7 .NET (`godot-mono` from Homebrew) and press Play (F5). Pressing Play
builds the C# project first, and every build copies the repo's `data/` folder to `game/data/`
(gitignored), so `data/` stays the only copy you edit.

| Input | Does |
| --- | --- |
| Click a blue tile | Move there (the path shows on hover) |
| Ability button, then click a red tile | Use it; hovering a target shows the area and hit, damage, crit and counter odds |
| End turn / Enter | Choose a facing with the arrow keys or the mouse, then Enter or click |
| Esc or right-click | Back |
| Q / E, wheel, WASD or middle-drag | Rotate, zoom, pan the camera |
| F2 | Let the AI play your side |

The HUD shows the battle's seed; replay any battle from the command line:

```
godot-mono --path game -- --seed 42               # play a specific seed
godot-mono --headless --path game -- --autoplay   # AI vs AI, fast; prints the result and quits
godot-mono --path game -- --screenshot shot.png   # saves the view when your first turn starts, then quits
```

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

### In Godot

`game/scripts/BattleController.cs` is the working example. Data loads through Godot's `FileAccess`,
since exported games pack files inside the game:

```csharp
var maps = DirAccess.GetFilesAt("res://data/maps").Select(f => $"maps/{f}");
var data = GameData.Load(path => FileAccess.GetFileAsString($"res://data/{path}"), maps);
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
