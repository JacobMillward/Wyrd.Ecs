using System.Numerics;
using Wyrd.Ecs.Examples.Asteroids.Components;
using Wyrd.Ecs.Input;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

[FixedTimestep]
[RunBefore(typeof(MovementSystem))]
public sealed partial class ShipControlSystem : QuerySystem
{
    private const float ThrustAcceleration = 420f;
    private const float MaxSpeed = 280f;
    private static readonly Angle TurnRate = Angle.Rad(3.2f);

    [Resource] public IntentState<GameAction> Input { get; private set; }

    protected override IQuery DefineQuery(Query query) => query.With<Ship, Transform, Velocity>();

    public void Update(Time time, World world, EntityView entity, ref Ship ship, ref Transform transform, ref Velocity velocity)
    {
        var dt = (float)time.Delta.TotalSeconds;

        var turn = (Input[GameAction.TurnRight].IsHeld ? 1f : 0f) - (Input[GameAction.TurnLeft].IsHeld ? 1f : 0f);
        ship.Heading += TurnRate * turn * dt;
        transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, ship.Heading.Radians);

        var thrusting = Input[GameAction.Thrust].IsHeld;
        if (thrusting)
        {
            var forward = new Vector3(MathF.Cos(ship.Heading.Radians), MathF.Sin(ship.Heading.Radians), 0f);
            velocity.Value += forward * ThrustAcceleration * dt;
            if (velocity.Value.LengthSquared() > MaxSpeed * MaxSpeed)
                velocity.Value = Vector3.Normalize(velocity.Value) * MaxSpeed;
        }

        foreach (var child in entity.Children())
        {
            if (!world.HasTag<EngineFlame>(child)) continue;
            ref var flameTransform = ref world.GetComponent<Transform>(child);
            flameTransform.Scale = thrusting ? Vector3.One : Vector3.Zero;
        }
    }
}
