using System.Numerics;

namespace Wyrd.Ecs;

/// <summary>
/// Local (parent-relative) position/rotation/scale. 3D-native; 2D is the degenerate case
/// (<c>Position.Z == 0</c>, rotation around Z only), not a separate type. World-space
/// value is computed by walking the <see cref="Parent"/> chain. A default-constructed
/// <see cref="Transform"/> has <see cref="Scale"/> zeroed (the struct default), which is
/// a degenerate, invisible-everything scale. Use <see cref="Identity"/>, not <c>default</c>.
/// </summary>
[RequiresSnapshotBefore(typeof(TransformSnapshotSystem))]
public struct Transform : IComponent
{
    /// <summary>Position relative to the parent, or world space with no parent.</summary>
    public Vector3 Position;

    /// <summary>Rotation relative to the parent, or world space with no parent.</summary>
    public Quaternion Rotation;

    /// <summary>Scale relative to the parent, or world space with no parent.</summary>
    public Vector3 Scale;

    /// <summary>Origin, no rotation, unit scale.</summary>
    public static readonly Transform Identity = new()
    {
        Position = Vector3.Zero,
        Rotation = Quaternion.Identity,
        Scale = Vector3.One,
    };
}
