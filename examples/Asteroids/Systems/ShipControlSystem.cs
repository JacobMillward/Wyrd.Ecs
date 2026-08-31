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
public sealed partial class ShipControlSystem : EcsSystem
{
    private const float ThrustAcceleration = 420f;
    private const float MaxSpeed = 280f;
    private static readonly Angle TurnRate = Angle.Rad(3.2f);

    [Resource] public partial AudioPlayer Audio { get; }
    [Resource] public partial IntentState<GameAction> Input { get; }
    [Resource] public partial GameAssets Assets { get; }

    private readonly EventReader<ShipDestroyed> _shipDestroyed;

    private Playback? _engineLoop;
    private bool _wasThrusting;

    public ShipControlSystem(World world) => _shipDestroyed = world.CreateEventReader<ShipDestroyed>();

    protected override void Execute(World world, Time time)
    {
        if (_shipDestroyed.Read().Count > 0)
        {
            if (_engineLoop is { } loop) Audio.Stop(loop);
            _engineLoop = null;
            _wasThrusting = false;
        }

        var dt = (float)time.Delta.TotalSeconds;

        if (!world.Query().WithMut<Ship, Transform, Velocity>().TrySingle(out var row)) return;

        var turn = (Input[GameAction.TurnRight].IsHeld ? 1f : 0f) - (Input[GameAction.TurnLeft].IsHeld ? 1f : 0f);
        row.Ship.Heading += TurnRate * turn * dt;
        row.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, row.Ship.Heading.Radians);

        var thrusting = Input[GameAction.Thrust].IsHeld;
        if (thrusting)
        {
            var forward = new Vector3(MathF.Cos(row.Ship.Heading.Radians), MathF.Sin(row.Ship.Heading.Radians), 0f);
            row.Velocity.Value += forward * ThrustAcceleration * dt;
            if (row.Velocity.Value.LengthSquared() > MaxSpeed * MaxSpeed)
                row.Velocity.Value = Vector3.Normalize(row.Velocity.Value) * MaxSpeed;
        }

        if (world.Query().WithMut<Sprite>().Has<EngineFlame>().TrySingle(out var flameRow))
            flameRow.Sprite = flameRow.Sprite with { Tint = flameRow.Sprite.Tint with { A = thrusting ? 1f : 0f } };

        if (thrusting && !_wasThrusting)
            _engineLoop = Audio.Play(Assets.EngineSound, Audio.CustomBus("Engine"), loop: true);
        else if (!thrusting && _wasThrusting && _engineLoop is { } loop)
            Audio.Stop(loop);
        _wasThrusting = thrusting;
    }
}
