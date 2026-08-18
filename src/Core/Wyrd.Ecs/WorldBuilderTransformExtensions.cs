namespace Wyrd.Ecs;

/// <summary>
/// Registers a <see cref="TransformSnapshotSystem"/> on a <see cref="WorldBuilder"/>, at
/// Fixed cadence. Registered via <see cref="WorldBuilder.AddSystemCore"/> directly with a
/// hand-supplied <see cref="SystemAccess"/> rather than the generated
/// <c>AddSystem&lt;T&gt;()</c> sugar, since <see cref="TransformSnapshotSystem"/> is a
/// hand-written <see cref="EcsSystem"/>, not a <see cref="QuerySystem"/>, and never
/// touched by the query-chain generator at all.
/// </summary>
public static class WorldBuilderTransformExtensions
{
    extension(WorldBuilder builder)
    {
        /// <summary>Registers a <see cref="TransformSnapshotSystem"/> at Fixed cadence.</summary>
        public WorldBuilder AddTransformSystem()
        {
            builder.AddSystemCore(
                typeof(TransformSnapshotSystem),
                access: new SystemAccess(Reads: [typeof(Transform)], Writes: [typeof(PreviousTransform)]),
                construct: _ => new TransformSnapshotSystem(),
                generatedBeforeTargets: [],
                generatedAfterTargets: [],
                cadence: SystemCadence.Fixed);
            return builder;
        }
    }
}
