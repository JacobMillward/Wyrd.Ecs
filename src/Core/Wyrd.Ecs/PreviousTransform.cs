using System.Numerics;

namespace Wyrd.Ecs;

/// <summary>
/// <see cref="Transform"/>'s value as of the start of the current fixed step, kept to
/// blend against the live value using <see cref="World.FixedStepAlpha"/>. Written only by
/// <see cref="TransformSnapshotSystem"/>. Never write this directly.
/// </summary>
[SystemManaged]
public struct PreviousTransform : IComponent
{
    /// <inheritdoc cref="Transform.Position"/>
    public Vector3 Position;

    /// <inheritdoc cref="Transform.Rotation"/>
    public Quaternion Rotation;

    /// <inheritdoc cref="Transform.Scale"/>
    public Vector3 Scale;
}
