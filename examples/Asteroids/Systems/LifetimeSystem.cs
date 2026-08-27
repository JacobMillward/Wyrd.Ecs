using Wyrd.Ecs.Examples.Asteroids.Components;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

[FixedTimestep]
public sealed partial class LifetimeSystem : QuerySystem
{
    protected override IQuery DefineQuery(Query query) => query.With<Lifetime>();

    public void Update(Time time, EntityView entity, ref Lifetime lifetime)
    {
        lifetime.SecondsRemaining -= (float)time.Delta.TotalSeconds;
        if (lifetime.SecondsRemaining <= 0f) entity.DestroyEntity();
    }
}
