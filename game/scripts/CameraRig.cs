using Godot;
using Undercurrent.Core.Grid;

namespace Undercurrent.Game;

/// <summary>
/// An angled camera orbiting a pivot on the board. Q and E rotate in 90 degree steps, the wheel zooms,
/// WASD or middle-drag pans.
/// </summary>
public partial class CameraRig : Node3D
{
    [Export] public float PitchDegrees = 30f;
    [Export] public float Fov = 30f;
    [Export] public float Distance = 22f;
    [Export] public float MinDistance = 10f;
    [Export] public float MaxDistance = 34f;
    [Export] public float PanSpeed = 6f;

    public Camera3D Camera { get; private set; } = null!;

    private float _yawDegrees = 45f; // start looking from the south-east corner, so grid north is up-right
    private Vector3 _boundsMin, _boundsMax;
    private Tween? _yawTween, _focusTween;
    private bool _dragging;

    public override void _Ready()
    {
        Camera = new Camera3D { Fov = Fov, Current = true };
        AddChild(Camera);
        RotationDegrees = new Vector3(0, _yawDegrees, 0);
        PlaceCamera();
    }

    public void SetBounds(Vector3 min, Vector3 max)
    {
        _boundsMin = min;
        _boundsMax = max;
    }

    public void Focus(Vector3 target, float seconds = 0.35f)
    {
        target = Clamp(target);
        _focusTween?.Kill();
        if (seconds <= 0) { Position = target; return; }
        _focusTween = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _focusTween.TweenProperty(this, "position", target, seconds);
    }

    public void Turn(int steps)
    {
        _yawDegrees += 90f * steps;
        _yawTween?.Kill();
        _yawTween = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _yawTween.TweenProperty(this, "rotation_degrees:y", _yawDegrees, 0.3f);
    }

    /// <summary>
    /// The grid direction that the given arrow key means on screen right now (Up is "away from the camera",
    /// leaning right on a diagonal view, so it matches the grid-north-is-up-right convention).
    /// </summary>
    public Point DirectionForScreen(Vector2 screenDir)
    {
        var basis = Basis.FromEuler(new Vector3(0, Mathf.DegToRad(_yawDegrees), 0));
        var forward = -basis.Z;
        var right = basis.X;
        var wanted = (forward * -screenDir.Y + right * screenDir.X).Normalized();
        var tieBreak = (right * -screenDir.Y - forward * screenDir.X) * 0.01f;
        return Point.Directions
            .OrderByDescending(d => new Vector3(d.X, 0, d.Y).Dot(wanted + tieBreak))
            .First();
    }

    public override void _Process(double delta)
    {
        var input = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W)) input.Y -= 1;
        if (Input.IsKeyPressed(Key.S)) input.Y += 1;
        if (Input.IsKeyPressed(Key.A)) input.X -= 1;
        if (Input.IsKeyPressed(Key.D)) input.X += 1;
        if (input != Vector2.Zero) Pan(input.Normalized() * PanSpeed * (float)delta);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Q }:
                Turn(-1);
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.E }:
                Turn(1);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                Zoom(-1.5f);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                Zoom(1.5f);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Middle } mb:
                _dragging = mb.Pressed;
                break;
            case InputEventMouseMotion motion when _dragging:
                Pan(new Vector2(-motion.Relative.X, -motion.Relative.Y) * 0.02f * Distance / 22f);
                break;
        }
    }

    private void Zoom(float amount)
    {
        Distance = Mathf.Clamp(Distance + amount, MinDistance, MaxDistance);
        PlaceCamera();
    }

    private void Pan(Vector2 screen)
    {
        _focusTween?.Kill();
        var basis = Basis.FromEuler(new Vector3(0, Rotation.Y, 0));
        Position = Clamp(Position + basis.X * screen.X + basis.Z * screen.Y);
    }

    private Vector3 Clamp(Vector3 p) => _boundsMax == _boundsMin ? p : p.Clamp(_boundsMin, _boundsMax);

    private void PlaceCamera()
    {
        float pitch = Mathf.DegToRad(PitchDegrees);
        Camera.Position = new Vector3(0, Distance * Mathf.Sin(pitch), Distance * Mathf.Cos(pitch));
        Camera.RotationDegrees = new Vector3(-PitchDegrees, 0, 0);
    }
}
