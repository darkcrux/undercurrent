using Godot;

namespace Undercurrent.Game;

/// <summary>Short-lived text that rises above a point and fades: damage numbers, "Miss", status names.</summary>
public static class FloatingText
{
    public static void Spawn(Node3D parent, Vector3 at, string text, Color color, float scale = 1f, float seconds = 1.0f)
    {
        var label = new Label3D
        {
            Text = text,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            FontSize = (int)(44 * scale),
            OutlineSize = 10,
            PixelSize = 0.006f,
            Modulate = color,
            Position = at,
        };
        parent.AddChild(label);
        var tween = label.CreateTween().SetParallel();
        tween.TweenProperty(label, "position", at + new Vector3(0, 0.7f, 0), seconds)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0f, seconds * 0.4f).SetDelay(seconds * 0.6f);
        tween.Chain().TweenCallback(Callable.From(label.QueueFree));
    }
}
