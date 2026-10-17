using Godot;
using Undercurrent.Core.Battles;
using Undercurrent.Core.Data;

namespace Undercurrent.Game;

/// <summary>
/// The M2 battle HUD: active unit panel, action buttons, a prompt line, hover info, the event log and the
/// end-of-battle banner. The full battle UI (attack preview, turn order) is M3.
/// </summary>
public partial class Hud : CanvasLayer
{
    public sealed record ActionButton(string Text, bool Enabled, string Tooltip, Action OnPress);

    public event Action<bool>? RestartChosen;

    private Label _info = null!, _prompt = null!, _hover = null!, _bannerText = null!;
    private HBoxContainer _actions = null!;
    private RichTextLabel _log = null!;
    private PanelContainer _banner = null!;

    public override void _Ready()
    {
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var infoPanel = Panel(root, Control.LayoutPreset.TopLeft);
        infoPanel.Position = new Vector2(12, 12);
        _info = new Label { CustomMinimumSize = new Vector2(300, 0) };
        infoPanel.AddChild(_info);


        // Top-right and short, so it covers as little of the board as possible.
        var logPanel = Panel(root, Control.LayoutPreset.TopRight);
        logPanel.OffsetLeft = -392;
        logPanel.OffsetTop = 12;
        logPanel.OffsetRight = -12;
        logPanel.OffsetBottom = 292;
        _log = new RichTextLabel { ScrollFollowing = true, BbcodeEnabled = true, FitContent = false };
        _log.AddThemeFontSizeOverride("normal_font_size", 14);
        logPanel.AddChild(_log);

        var bottom = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        bottom.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        bottom.OffsetLeft = 12;
        bottom.OffsetRight = -12;
        bottom.OffsetTop = -160;
        bottom.OffsetBottom = -12;
        bottom.Alignment = BoxContainer.AlignmentMode.End;
        root.AddChild(bottom);
        var help = new Label
        {
            Text = "Click: move or target   Q/E: rotate   Wheel: zoom   WASD / middle-drag: pan   Esc: back   Enter: end turn   F2: auto-play your side",
            Modulate = new Color(1, 1, 1, 0.5f),
        };
        help.AddThemeFontSizeOverride("font_size", 13);
        _hover = new Label { Modulate = new Color(1, 1, 1, 0.85f) };
        _prompt = new Label();
        _prompt.AddThemeFontSizeOverride("font_size", 18);
        _actions = new HBoxContainer();
        _actions.AddThemeConstantOverride("separation", 8);
        bottom.AddChild(help);
        bottom.AddChild(_hover);
        bottom.AddChild(_prompt);
        bottom.AddChild(_actions);

        _banner = Panel(root, Control.LayoutPreset.Center);
        _banner.GrowHorizontal = Control.GrowDirection.Both;
        _banner.GrowVertical = Control.GrowDirection.Both;
        _banner.Visible = false;
        var bannerBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        bannerBox.AddThemeConstantOverride("separation", 12);
        _banner.AddChild(bannerBox);
        _bannerText = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _bannerText.AddThemeFontSizeOverride("font_size", 32);
        bannerBox.AddChild(_bannerText);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 12);
        bannerBox.AddChild(buttons);
        var again = new Button { Text = "Replay this seed" };
        again.Pressed += () => RestartChosen?.Invoke(false);
        var fresh = new Button { Text = "New battle" };
        fresh.Pressed += () => RestartChosen?.Invoke(true);
        buttons.AddChild(again);
        buttons.AddChild(fresh);
    }

    public void ShowUnit(Unit? u, Battle battle, int seed)
    {
        string header = $"Turn {battle.TurnNumber}   Seed {seed}";
        if (u is null) { _info.Text = header; return; }

        string resource = u.ResourceKind == ResourceKind.Cooldown ? "Cooldowns" : $"{u.ResourceKind} {u.Resource}/{u.ResourceMax}";
        if (u.ResourceKind == ResourceKind.Resonance && u.Strain > 0) resource += $"   Strain {u.Strain}";
        var statuses = u.Statuses.Where(s => s.Key != StatusId.StunImmune).Select(s => $"{s.Key} {s.Value}").ToList();
        if (u.Channeling is { } c) statuses.Add($"Channeling {c.Ability.Name}");
        _info.Text = string.Join("\n", new[]
        {
            header,
            $"{u.Name} ({u.Class.Name}, {(u.Team == Team.Player ? "yours" : "enemy")})",
            $"HP {u.Hp}/{u.MaxHp}   {resource}",
            $"Move {u.Move}   Jump {u.Jump}   Reach {u.Reach}",
            statuses.Count > 0 ? string.Join(", ", statuses) : "No statuses",
        });
    }

    public void SetActions(IEnumerable<ActionButton> actions)
    {
        ClearActions();
        foreach (var a in actions)
        {
            var b = new Button { Text = a.Text, Disabled = !a.Enabled, TooltipText = a.Tooltip, FocusMode = Control.FocusModeEnum.None };
            b.Pressed += a.OnPress;
            _actions.AddChild(b);
        }
    }

    public void ClearActions()
    {
        foreach (var child in _actions.GetChildren()) child.QueueFree();
    }

    public void SetPrompt(string text) => _prompt.Text = text;
    public void SetHover(string text) => _hover.Text = text;

    public void Log(string text, Color? color = null)
    {
        if (color is { } c) _log.AppendText($"[color=#{c.ToHtml(false)}]{EscapeBbcode(text)}[/color]\n");
        else _log.AppendText(EscapeBbcode(text) + "\n");
    }

    public void ClearLog() => _log.Clear();

    public void ShowBanner(string text)
    {
        _bannerText.Text = text;
        _banner.Visible = true;
    }

    public void HideBanner() => _banner.Visible = false;

    private static PanelContainer Panel(Control parent, Control.LayoutPreset preset)
    {
        var panel = new PanelContainer();
        panel.SetAnchorsAndOffsetsPreset(preset);
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.06f, 0.08f, 0.8f),
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        });
        parent.AddChild(panel);
        return panel;
    }

    private static string EscapeBbcode(string s) => s.Replace("[", "[lb]");
}
