using System.Numerics;
using Wyrd.Ecs.Audio;
using Wyrd.Ecs.Examples.Asteroids.Components;
using Wyrd.Ecs.Examples.Asteroids.Events;
using Wyrd.Ecs.Input;
using Wyrd.Ecs.Renderer;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

// Plain EcsSystem, not QuerySystem: a QuerySystem's generated Execute only calls Update()
// per matching entity, so it could never react to ShipDestroyed once the ship (and this
// system's query match) is gone. A hand-written Execute always runs, same as CollisionSystem.
[FixedTimestep]
[RunBefore(typeof(MovementSystem))]
public sealed class ShipControlSystem : EcsSystem
{
    private const float ThrustAcceleration = 420f;
    private const float MaxSpeed = 280f;
    private static readonly Angle TurnRate = Angle.Rad(3.2f);

    private readonly EventReader<ShipDestroyed> _shipDestroyed;

    private Playback? _engineLoop;
    private bool _wasThrusting;

    public ShipControlSystem(World world) => _shipDestroyed = world.CreateEventReader<ShipDestroyed>();

    protected override void Execute(World world, Time time)
    {
        var audio = world.GetResource<AudioPlayer>();
        if (_shipDestroyed.Read().Count > 0)
        {
            if (_engineLoop is { } loop) audio.Stop(loop);
            _engineLoop = null;
            _wasThrusting = false;
        }

        var dt = (float)time.Delta.TotalSeconds;
        var input = world.GetResource<IntentState<GameAction>>();
        var assets = world.GetResource<GameAssets>();

        world.Query().With<Ship, Transform, Velocity>().ForEach((EntityView entity, ref Ship ship, ref Transform transform, ref Velocity velocity) =>
        {
            var turn = (input[GameAction.TurnRight].IsHeld ? 1f : 0f) - (input[GameAction.TurnLeft].IsHeld ? 1f : 0f);
            ship.Heading += TurnRate * turn * dt;
            transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, ship.Heading.Radians);

            var thrusting = input[GameAction.Thrust].IsHeld;
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
                ref var flameSprite = ref world.GetComponent<Sprite>(child);
                flameSprite = flameSprite with { Tint = flameSprite.Tint with { A = thrusting ? 1f : 0f } };
            }

            if (thrusting && !_wasThrusting)
                _engineLoop = audio.Play(assets.EngineSound, audio.CustomBus("Engine"), loop: true);
            else if (!thrusting && _wasThrusting && _engineLoop is { } loop)
                audio.Stop(loop);
            _wasThrusting = thrusting;
        });
    }
}
