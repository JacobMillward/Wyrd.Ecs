using Wyrd.Ecs.Examples.Asteroids.Components;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

[FixedTimestep]
public sealed partial class MovementSystem : QuerySystem
{
    protected override IQuery DefineQuery(Query query) => query.With<Transform, Velocity>();

    public void Update(Time time, ref Transform transform, in Velocity velocity) =>
        transform.Position += velocity.Value * (float)time.Delta.TotalSeconds;
}
