using Godot;
using Undercurrent.Core.Battles;

namespace Undercurrent.Game;

/// <summary>A placed device such as a turret: a squat placeholder cylinder in the owner's team color.</summary>
public partial class DeployableView : Node3D
{
    public Deployable Deployable { get; private set; } = null!;

    public static DeployableView Create(Deployable deployable, BoardView board)
    {
        var view = new DeployableView { Deployable = deployable, Name = $"{deployable.Kind}_{deployable.Id}" };
        view.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.3f, Height = 0.45f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = PlaceholderArt.TeamColor(deployable.Team).Darkened(0.3f), Metallic = 0.6f, Roughness = 0.4f },
            Position = new Vector3(0, 0.225f, 0),
        });
        view.Position = board.TileTop(deployable.Position);
        return view;
    }

    public void Sync() => Visible = !Deployable.Destroyed;
}
