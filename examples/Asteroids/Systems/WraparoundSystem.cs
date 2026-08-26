using Wyrd.Ecs.Examples.Asteroids.Components;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

[FixedTimestep]
[RunAfter(typeof(MovementSystem))]
public sealed partial class WraparoundSystem : QuerySystem
{
    protected override IQuery DefineQuery(Query query) => query.With<Transform>().Has<Velocity>().Without<Bullet>();

    public void Update(Time time, ref Transform transform)
    {
        var position = transform.Position;
        if (position.X > Playfield.HalfWidth) position.X = -Playfield.HalfWidth;
        else if (position.X < -Playfield.HalfWidth) position.X = Playfield.HalfWidth;
        if (position.Y > Playfield.HalfHeight) position.Y = -Playfield.HalfHeight;
        else if (position.Y < -Playfield.HalfHeight) position.Y = Playfield.HalfHeight;
        transform.Position = position;
    }
}
