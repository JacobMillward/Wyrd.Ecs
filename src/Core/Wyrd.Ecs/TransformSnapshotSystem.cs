namespace Wyrd.Ecs;

/// <summary>
/// Copies <see cref="Transform"/> into <see cref="PreviousTransform"/> once per fixed
/// step. Every other <c>[FixedTimestep]</c> system that writes <see cref="Transform"/>
/// is automatically ordered after this one: see <see cref="Transform"/>'s
/// <see cref="RequiresSnapshotBeforeAttribute"/> and the query-chain generator's
/// edge-inference for how, not a hand-declared edge on each of them. A hand-written
/// <see cref="EcsSystem"/>, not a <see cref="QuerySystem"/>, since its own access
/// footprint (reads <see cref="Transform"/>, writes <see cref="PreviousTransform"/>) is
/// fixed and known, the same reasoning that already kept <c>PlatformSystem</c>/
/// <c>RendererSystem</c> off the generator.
/// </summary>
public sealed class TransformSnapshotSystem : EcsSystem
{
    /// <inheritdoc/>
    protected override void Execute(World world, Time time)
    {
        world.Query<Ref<Transform>, Mut<PreviousTransform>>((transform, previous) =>
        {
            for (var i = 0; i < transform.Length; i++)
            {
                previous[i].Position = transform[i].Position;
                previous[i].Rotation = transform[i].Rotation;
                previous[i].Scale = transform[i].Scale;
            }
        });
    }
}
