using Godot;
using Undercurrent.Core.Ai;
using Undercurrent.Core.Battles;
using Undercurrent.Core.Data;
using Undercurrent.Core.Grid;
using FileAccess = Godot.FileAccess;

namespace Undercurrent.Game;

/// <summary>
/// Runs one battle: loads the data, builds the board and units, then loops: play the battle's new events,
/// and either let <see cref="EnemyAi"/> act or wait for the player's command. Every rule question goes to
/// the core <see cref="Battle"/>; this class only turns input into commands.
///
/// Command-line options (after "--"): --seed N, --map ID, --autoplay (both sides play themselves fast and
/// the game quits when the battle ends, printing the result; used for headless smoke tests),
/// --screenshot FILE (saves the view to a PNG once you're asked for a command, then quits).
/// </summary>
public partial class BattleController : Node3D
{
    [Export] public string MapId = "ashfield-test";

    private enum Mode { Busy, Command, Targeting, Facing, Over }

    private GameData _data = null!;
    private Battle _battle = null!;
    private int _seed;
    private int _generation;

    private CameraRig _camera = null!;
    private Hud _hud = null!;
    private Node3D _world = null!;
    private BoardView _board = null!;
    private EventPlayer _player = null!;
    private Dictionary<int, UnitView> _units = new();
    private Dictionary<int, DeployableView> _devices = new();

    private Mode _mode = Mode.Busy;
    private TaskCompletionSource<Action>? _pending;
    private AbilityDef? _ability;
    private bool _channel;
    private Point _facing;
    private Point? _hover;

    private bool _autoPlayer;
    private bool _quitOnEnd;
    private const int StallTurns = 2000;

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        int? seed = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--seed" && i + 1 < args.Length && int.TryParse(args[i + 1], out int s)) seed = s;
            if (args[i] == "--map" && i + 1 < args.Length) MapId = args[i + 1];
            if (args[i] == "--autoplay") _autoPlayer = _quitOnEnd = true;
            if (args[i] == "--screenshot" && i + 1 < args.Length) RunSafe(Screenshot(args[i + 1]));
        }

        _data = LoadData();
        BuildEnvironment();

        _hud = new Hud();
        AddChild(_hud);
        _hud.RestartChosen += fresh => StartBattle(fresh ? NewSeed() : _seed);

        _camera = new CameraRig();
        AddChild(_camera);

        StartBattle(seed ?? NewSeed());
    }

    private static GameData LoadData()
    {
        var maps = DirAccess.GetFilesAt("res://data/maps").Where(f => f.EndsWith(".json")).Select(f => $"maps/{f}").ToList();
        if (maps.Count == 0)
            GD.PushError("No maps in res://data/maps. Build the C# project first: it copies the repo's data/ folder into game/data.");
        return GameData.Load(path => FileAccess.GetFileAsString($"res://data/{path}"), maps);
    }

    private static int NewSeed() => (int)(GD.Randi() % 1_000_000);

    private void StartBattle(int seed)
    {
        _generation++;
        _seed = seed;
        _pending = null;
        _mode = Mode.Busy;
        _world?.QueueFree();

        _battle = Battle.FromMap(_data, MapId, new SeededRandom(seed));
        _world = new Node3D { Name = "World" };
        AddChild(_world);

        _board = new BoardView();
        _world.AddChild(_board);
        _board.Build(_battle.Map);
        int maxHeight = _battle.Map.AllTiles().Max(_battle.Map.HeightAt);
        _camera.SetBounds(Vector3.Zero, new Vector3(_battle.Map.Columns - 1, maxHeight * _board.HeightStep, _battle.Map.Rows - 1));
        _camera.Focus(_board.Center, 0);

        _units = new Dictionary<int, UnitView>();
        _devices = new Dictionary<int, DeployableView>();
        foreach (var u in _battle.Units)
        {
            var view = UnitView.Create(u, _board);
            _units[u.Id] = view;
            _world.AddChild(view);
        }

        _player = new EventPlayer { Speed = _quitOnEnd ? 0.02f : 1f };
        _world.AddChild(_player);
        _player.Bind(_battle, _board, _camera, _hud, _world, _units, _devices);

        _hud.ClearLog();
        _hud.HideBanner();
        _hud.SetPrompt("");
        _hud.SetHover("");
        _battle.Start();
        RunSafe(Run(_generation));
    }

    // ---------- The battle loop ----------

    private async Task Run(int generation)
    {
        while (generation == _generation)
        {
            await _player.Play(_battle.TakeNewEvents());
            if (generation != _generation) return;
            SyncAll();

            if (_battle.IsOver) { EndBattle(); return; }
            if (_battle.TurnNumber > StallTurns) { EndBattle(stalled: true); return; }

            var u = _battle.Active!;
            if (u.Team == Team.Enemy || _autoPlayer)
            {
                await AiTurn(u);
                continue;
            }

            _pending = new TaskCompletionSource<Action>();
            EnterCommand();
            var command = await _pending.Task;
            if (generation != _generation) return;
            try { command(); }
            catch (InvalidOperationException ex) { _hud.Log($"Can't do that: {ex.Message}", new Color(1, 0.6f, 0.4f)); }
        }
    }

    private async Task AiTurn(Unit u)
    {
        _mode = Mode.Busy;
        _board.ClearHighlights();
        _hud.ClearActions();
        _hud.SetPrompt(u.Team == Team.Enemy ? $"Enemy turn: {u.Name}" : $"Auto-playing {u.Name} (F2 to stop)");
        await Pause(0.3f);

        var plan = EnemyAi.Plan(_battle);
        if (plan.Moves(u) && !_units[u.Id].HiddenFromPlayer(_battle))
        {
            _board.SetHighlight(new[] { plan.Destination }, Highlight.Choice);
            await Pause(0.25f);
            _board.ClearHighlights();
        }
        EnemyAi.Execute(_battle, plan);
    }

    private void EndBattle(bool stalled = false)
    {
        _mode = Mode.Over;
        _board.ClearHighlights();
        _board.SetActive(null);
        _hud.ClearActions();
        _hud.SetPrompt("");
        string result = stalled ? "Stalled" : _battle.Winner == Team.Player ? "Victory" : "Defeat";
        _hud.ShowBanner($"{result} after {_battle.TurnNumber} turns");

        if (_quitOnEnd)
        {
            var survivors = string.Join(", ", _battle.Units.Where(x => x.IsAlive).Select(x => $"{x.Name} {x.Hp}/{x.MaxHp}"));
            GD.Print($"AUTOPLAY seed={_seed} map={MapId} result={result} turns={_battle.TurnNumber} events={_battle.History.Count} survivors=[{survivors}]");
            GetTree().Quit(stalled ? 2 : 0);
        }
    }

    private void SyncAll()
    {
        foreach (var view in _units.Values) view.Sync(_battle);
        foreach (var d in _battle.Deployables)
        {
            if (!_devices.ContainsKey(d.Id))
            {
                _devices[d.Id] = DeployableView.Create(d, _board);
                _world.AddChild(_devices[d.Id]);
            }
            _devices[d.Id].Sync();
        }
        var active = _battle.Active;
        _board.SetActive(active is not null && _units[active.Id].Visible ? active.Position : null);
        _hud.ShowUnit(active, _battle, _seed);
    }

    // ---------- Player modes ----------

    private void EnterCommand()
    {
        var u = _battle.Active!;
        _mode = Mode.Command;
        _ability = null;
        _units[u.Id].SetFacing(u.Facing);
        if (!_battle.CanMove && _battle.HasActed) { EnterFacing(); return; }

        var actions = new List<Hud.ActionButton>();
        foreach (var id in u.Abilities)
        {
            var ab = _data.Ability(id);
            var block = _battle.CanUse(id);
            string tooltip = ab.Description + (block == UseBlock.None ? "" : $"\nUnavailable: {Describe(block, u, ab)}");
            actions.Add(new Hud.ActionButton(ab.Name + CostText(u, ab), block == UseBlock.None, tooltip, () => ChooseAbility(ab, false)));
            if (ab.Channelable)
                actions.Add(new Hud.ActionButton($"{ab.Name} (channel)", block == UseBlock.None,
                    "Pay now; it resolves at the start of your next turn at higher power, unless you are hit first.",
                    () => ChooseAbility(ab, true)));
        }
        actions.Add(new Hud.ActionButton("End turn", true, "Choose a facing, then end the turn.", EnterFacing));
        _hud.SetActions(actions);
        _hud.SetPrompt(_battle.CanMove
            ? $"{u.Name}: click a blue tile to move, or choose an action."
            : $"{u.Name}: choose an action, or end the turn.");
        RefreshHighlights();
    }

    private void ChooseAbility(AbilityDef ab, bool channel)
    {
        if (_mode is not (Mode.Command or Mode.Targeting)) return;
        var u = _battle.Active!;
        if (ab.Target == TargetKind.Self)
        {
            Commit(() => _battle.UseAbility(ab.Id, u.Position, channel));
            return;
        }
        _mode = Mode.Targeting;
        _ability = ab;
        _channel = channel;
        _hud.SetActions(new[] { new Hud.ActionButton("Back", true, "Esc or right-click", EnterCommand) });
        _hud.SetPrompt($"{ab.Name}{(channel ? " (channel)" : "")}: choose a target. Esc to go back.");
        RefreshHighlights();
    }

    private void EnterFacing()
    {
        if (_mode is not (Mode.Command or Mode.Targeting)) return;
        var u = _battle.Active!;
        _mode = Mode.Facing;
        _facing = u.Facing;
        var actions = new List<Hud.ActionButton>
        {
            new("Confirm facing", true, "Enter", () => Commit(() => _battle.EndTurn(_facing))),
        };
        if (CanStillAct()) actions.Add(new Hud.ActionButton("Back", true, "Esc or right-click", EnterCommand));
        _hud.SetActions(actions);
        _hud.SetPrompt($"{u.Name}: choose a facing with the arrow keys or the mouse, then Enter or click.");
        RefreshHighlights();
    }

    private bool CanStillAct() => _battle.CanMove || !_battle.HasActed;

    private void Back()
    {
        switch (_mode)
        {
            case Mode.Targeting:
                EnterCommand();
                break;
            case Mode.Facing when CanStillAct():
                EnterCommand();
                break;
        }
    }

    private void Commit(Action command)
    {
        if (_pending is null) return;
        _mode = Mode.Busy;
        _board.ClearHighlights();
        _board.SetCursor(null);
        _hud.ClearActions();
        _hud.SetPrompt("");
        var pending = _pending;
        _pending = null;
        pending.SetResult(command);
    }

    // ---------- Input ----------

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F2 })
        {
            _autoPlayer = !_autoPlayer;
            _hud.Log(_autoPlayer ? "Auto-play on: the AI plays your side." : "Auto-play off.");
            if (_autoPlayer && _mode is Mode.Command or Mode.Targeting or Mode.Facing)
                Commit(() => EnemyAi.TakeTurn(_battle));
            return;
        }
        if (_mode is Mode.Busy or Mode.Over) return;

        switch (e)
        {
            case InputEventMouseMotion motion:
                _hover = _board.PickTile(_camera.Camera, motion.Position);
                if (_mode == Mode.Facing && _hover is { } h && h != _battle.Active!.Position)
                    _facing = Point.FacingToward(_battle.Active.Position, h);
                RefreshHighlights();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                _hover = _board.PickTile(_camera.Camera, click.Position);
                OnClick();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }:
            case InputEventKey { Pressed: true, Keycode: Key.Escape }:
                Back();
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Enter or Key.KpEnter or Key.Space }:
                if (_mode == Mode.Facing) Commit(() => _battle.EndTurn(_facing));
                else EnterFacing();
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Up or Key.Down or Key.Left or Key.Right } key when _mode == Mode.Facing:
                var dir = key.Keycode switch { Key.Up => Vector2.Up, Key.Down => Vector2.Down, Key.Left => Vector2.Left, _ => Vector2.Right };
                _facing = _camera.DirectionForScreen(dir);
                RefreshHighlights();
                break;
        }
    }

    private void OnClick()
    {
        var u = _battle.Active!;
        switch (_mode)
        {
            case Mode.Command when _hover is { } tile && _battle.CanMove && tile != u.Position
                                   && _battle.ReachableTiles(u).ContainsKey(tile):
                Commit(() => _battle.Move(tile));
                break;
            case Mode.Targeting when _hover is { } tile && _ability is not null && _battle.IsValidTarget(u, _ability, tile):
                var ab = _ability;
                bool channel = _channel;
                Commit(() => _battle.UseAbility(ab.Id, tile, channel));
                break;
            case Mode.Facing:
                Commit(() => _battle.EndTurn(_facing));
                break;
        }
    }

    // ---------- Highlights and hover info ----------

    private void RefreshHighlights()
    {
        var u = _battle.Active;
        _board.ClearHighlights();
        _board.SetCursor(_hover);
        if (u is null) return;
        var lines = new List<string>();
        if (_hover is { } tile) lines.Add(TileInfo(tile));

        switch (_mode)
        {
            case Mode.Command when _battle.CanMove:
                var reachable = _battle.ReachableTiles(u);
                _board.SetHighlight(reachable.Keys.Where(p => p != u.Position), Highlight.Move);
                if (_hover is { } h && reachable.TryGetValue(h, out var path)) _board.SetHighlight(path, Highlight.Choice);
                break;

            case Mode.Targeting when _ability is { } ab:
                _board.SetHighlight(_battle.TilesInRange(u, ab), Highlight.Range);
                if (_hover is { } target && _battle.IsValidTarget(u, ab, target))
                {
                    var area = _battle.AffectedTiles(ab, target).ToList();
                    _board.SetHighlight(area, Highlight.Area);
                    _board.SetHighlight(new[] { target }, Highlight.Choice);
                    lines.AddRange(ForecastLines(u, ab, target, area));
                }
                break;

            case Mode.Facing:
                _board.SetHighlight(Point.Directions.Select(d => u.Position + d), Highlight.Facing);
                _board.SetHighlight(new[] { u.Position + _facing }, Highlight.Choice);
                _units[u.Id].SetFacing(_facing);
                break;
        }
        _hud.SetHover(string.Join("\n", lines));
    }

    private string TileInfo(Point tile)
    {
        string text = $"Tile {tile}  height {_battle.Map.HeightAt(tile)}";
        var unit = _battle.UnitAt(tile);
        if (unit is not null && !_units[unit.Id].HiddenFromPlayer(_battle))
            text += $"   {unit.Name} ({(unit.Team == Team.Player ? "yours" : "enemy")}) HP {unit.Hp}/{unit.MaxHp}"
                    + (unit.Fallen ? $", fallen ({unit.FallenCountdown})" : "");
        return text;
    }

    private IEnumerable<string> ForecastLines(Unit u, AbilityDef ab, Point target, List<Point> area)
    {
        if (_channel) yield return "Channeled: resolves at the start of your next turn.";
        if (!ab.DealsDamage)
        {
            var who = _battle.LivingUnitAt(target);
            yield return who is null ? ab.Name : $"{ab.Name} on {who.Name}";
            yield break;
        }
        foreach (var t in area.Select(_battle.LivingUnitAt).OfType<Unit>())
        {
            if (t.Team == u.Team || _units[t.Id].HiddenFromPlayer(_battle)) continue;
            var f = _battle.Forecast(u, t, ab, channeled: _channel);
            string line = $"{t.Name}: hit {f.HitChance}%, {f.Damage} damage{(f.LethalOnHit ? " (lethal)" : "")}, crit {f.CritChance}% for {f.CritDamage}";
            line += f.CanCounter ? $", counter {f.CounterChance}% for {f.CounterDamage}" : ", no counter";
            if (f.PaysBloodPrice) line += $", you pay {f.BloodPriceHp} HP";
            yield return line;
        }
    }

    private string CostText(Unit u, AbilityDef ab)
    {
        if (ab.Cooldown > 0) return u.CooldownLeft(ab.Id) > 0 ? $" ({u.CooldownLeft(ab.Id)} turns)" : "";
        if (ab.Cost <= 0) return "";
        int blood = _battle.BloodPriceHp(u, ab);
        return blood > 0 ? $" ({ab.Cost}, {blood} HP)" : $" ({ab.Cost})";
    }

    private static string Describe(UseBlock block, Unit u, AbilityDef ab) => block switch
    {
        UseBlock.AlreadyActed => "already acted this turn",
        UseBlock.Burnout => "burned out",
        UseBlock.ActOnlyAfterMoving => "can't be used after moving",
        UseBlock.OnCooldown => $"ready in {u.CooldownLeft(ab.Id)} turns",
        UseBlock.NotEnoughResource => $"needs {ab.Cost} {u.ResourceKind}",
        UseBlock.TurretAlreadyPlaced => "a turret is already placed",
        UseBlock.TooFewHp => "too few HP",
        _ => block.ToString(),
    };

    // ---------- Helpers ----------

    private async Task Pause(float seconds)
    {
        float scaled = seconds * _player.Speed;
        if (scaled <= 0.001f) return;
        await ToSignal(GetTree().CreateTimer(scaled), SceneTreeTimer.SignalName.Timeout);
    }

    private async Task Screenshot(string file)
    {
        for (int i = 0; i < 60 && _mode is not (Mode.Command or Mode.Over); i++)
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var error = GetViewport().GetTexture().GetImage().SavePng(file);
        GD.Print(error == Error.Ok ? $"Saved screenshot to {file}" : $"Couldn't save screenshot: {error}");
        GetTree().Quit();
    }

    private static async void RunSafe(Task task)
    {
        try { await task; }
        catch (ObjectDisposedException) { } // a restart freed the old battle's nodes mid-animation
        catch (Exception ex) { GD.PushError($"Battle loop failed: {ex}"); }
    }

    private void BuildEnvironment()
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.09f, 0.09f, 0.12f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.55f, 0.55f, 0.66f),
            AmbientLightEnergy = 0.6f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            GlowEnabled = true,
            GlowIntensity = 0.6f,
            FogEnabled = true,
            FogLightColor = new Color(0.32f, 0.30f, 0.38f),
            FogDensity = 0.008f,
        };
        AddChild(new WorldEnvironment
        {
            Environment = env,
            CameraAttributes = new CameraAttributesPractical
            {
                DofBlurFarEnabled = true,
                DofBlurFarDistance = 30f,
                DofBlurFarTransition = 12f,
                DofBlurAmount = 0.06f,
            },
        });
        AddChild(new DirectionalLight3D
        {
            ShadowEnabled = true,
            LightEnergy = 1.1f,
            LightColor = new Color(1f, 0.93f, 0.82f),
            RotationDegrees = new Vector3(-50, -35, 0),
        });
    }
}
