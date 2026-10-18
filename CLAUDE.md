# Undercurrent (working title)

A turn-based tactical RPG in the tradition of Tactics Ogre and Final Fantasy Tactics. Grim steampunk
world, HD-2D look, solo developer (darkcrux), PC first. Engine: Godot 4 (.NET edition), C#.

## Design documents (source of truth for design decisions)

All in `docs/`. Update them in the same commit as code that follows from a design change.

- `docs/concept.md`: world, factions, story structure, endings, art direction, decisions table
- `docs/combat-rules.md`: turns, stats, counters, statuses, classes, unlocks, switching, all abilities
- `docs/vertical-slice.md`: scope, battles, systems in and out, milestones M1 to M6
- `docs/prototype/ashfield-skirmish.html`: the browser combat prototype (open in a browser)

Live tuning numbers are in `data/rules.json`; if a doc and the data disagree, the data wins and the doc
should be fixed. The original claude.ai docs are a read-only archive as of 17 Oct 2026 (links in
`docs/README.md`).

## Repository layout

```
Undercurrent.sln
src/Undercurrent.Core/          game rules as a plain C# library (net8.0), NO Godot code
  Grid/  Data/  Battles/  Ai/
tests/Undercurrent.Core.Tests/  rule tests, small built-in runner ([Test], Assert)
tools/Undercurrent.Sim/         AI-vs-AI simulator for balancing
data/                           rules.json, abilities.json, classes.json, units.json, maps/*.json
docs/                           design docs and the browser prototype
game/                           the Godot 4.7 .NET project: scenes/, scripts/; game/data is copied from data/ at build
```

## Commands

```
dotnet build
dotnet run --project tests/Undercurrent.Core.Tests            # all tests (43)
dotnet run --project tests/Undercurrent.Core.Tests -- Counter  # filter by name
dotnet run --project tools/Undercurrent.Sim -- --battles 500   # balance numbers
dotnet run --project tools/Undercurrent.Sim -- --log 3         # one battle's full log
godot-mono --path game                                         # play (or open game/project.godot, F5)
godot-mono --headless --path game -- --autoplay --seed 3       # AI vs AI smoke test, prints the result
```

The machine has the .NET 10 SDK (Homebrew). The library targets net8.0 for Godot compatibility;
test and sim projects use RollForward=Major.

## Conventions

- All rules live in Undercurrent.Core. Godot only renders, takes input and plays events.
- The engine drives a `Battle` with commands (`Move`, `UseAbility`, `EndTurn(facing)`), queries
  (`ReachableTiles`, `CanUse`, `TilesInRange`, `Forecast`, `PredictTurnOrder`) and reads
  `TakeNewEvents()` to animate. Enemy turns: `EnemyAi.Plan` then `EnemyAi.Execute`.
- Every tuning number belongs in data/*.json, never hard-coded. Balancing = edit JSON + run the sim.
- Every rule change gets a test. Battles are deterministic from a seed (`SeededRandom`); tests use
  `ScriptedRandom`.
- Godot loads data with `GameData.Load(path => FileAccess.GetFileAsString($"res://data/{path}"), maps)`.
  Edit data/ only; the game csproj copies it to game/data (gitignored) on every build.
- In Godot, views follow the numbers in events while they play, then snap to the battle's state.
  Rendering sizes (tile height step, camera pitch) are exported fields on the nodes, not rules.
- Commit messages: short imperative subject, a body explaining why.

## Key design decisions (details in the docs)

- Individual turns by a wait timer from SPD minus gear weight. Move and act in either order;
  act-only abilities can't be combined with moving. Choose facing at end of turn.
- Hit = HIT - (target SPD - 10) x 2, +10 flank, +20 back, 0 to 100%. Damage = ATK x power x 100 /
  (100 + DEF) (ATN vs RES for elemental). Crit = LUK / 2 (+25 Hidden), doubles damage. No random spread.
- Counters on every attack in reach: 75% front, 50% flank, 25% back, -10% per level the attacker is
  higher, +25% Braced, halved by a crit; 50% damage (75% Braced). Hidden attackers, Suppressed or
  Stunned defenders, and out-of-reach attackers draw none. Parry always counters a miss.
- Vision cone: 2 tiles ahead, front diagonals, 2 to each side, nothing behind. Hidden units can't be
  targeted directly unless inside an enemy's cone (spotted); area attacks hit and reveal them.
- Factions and resources: Ordained (magic, Focus starts 0 and builds, channeling), Commonwealth
  (tech, Charge starts full and never refills, deployables), Unbound (Resonance starts 50%; the
  Blood Price pays HP when short, +damage, Strain stacks, Burnout at 5). Mercenary line uses cooldowns.
- 25 classes: Mercenary, Engineer, Acolyte lines (5 each) and two Unbound lines (Awakened, Vessel).
  Starter, advanced, expert tiers; mastery 1 to 5; borrowed abilities (starters 2 actions + 1 passive,
  others 1 + 1, max one expert ability). Unlocks need mastery, a stat minimum and a visible deed.
- Story: origin prologue (Commonwealth, Ordained or Unbound), shared Chapter 1 in Ashfield, faction
  earned by choices across Chapters 1 to 3 (defection points), path locks after Chapter 3, shared
  final Chapter 4. Six endings (Accord or faction ending per path), a catastrophe on losing the final
  battle, and a condition-based true ending.
- Perma-death: Normal = 3-turn countdown to revive; Casual = out 1 mission per turn down (max 3);
  Hard = instant death.
- Art: HD-2D. 3D tile world (GridMap or meshes), billboarded pixel sprites, Forward+ renderer,
  depth of field, fog, glow. The Current always glows violet; Unbound power glows red.

## Status and next steps

- M1 done: core rules library, tests, simulator.
- M2 done: Godot project in game/ with a 3D board from map heights, a rotating 30 degree camera,
  placeholder billboard units, move, every ability with area and forecast on hover, facing, enemy
  turns via EnemyAi, victory or defeat with replay. The HUD is minimal on purpose.
- Known gaps: AI-vs-AI wins on ashfield-test lean to the player side (about 94%) since the AI weighs
  tile danger; hit chances run 93 to 100% with current stats; mastery, switching, items, revives and
  most of the 25 classes aren't in the library yet.
- Next, M3: the full battle UI (attack preview panel, turn order strip, statuses), prologue battle
  playable end to end.
