using System.Numerics;
using Wyrd.Ecs.Audio;
using Wyrd.Ecs.Examples.Asteroids.Components;
using Wyrd.Ecs.Input;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

[FixedTimestep]
public sealed partial class WeaponSystem : QuerySystem
{
    private const float BulletSpeed = 600f;
    private const float NoseOffset = 30f;

    [Resource] public IntentState<GameAction> Input { get; private set; }
    [Resource] public GameAssets Assets { get; private set; }
    [Resource] public AudioPlayer Audio { get; private set; }

    protected override IQuery DefineQuery(Query query) => query.With<Transform, Velocity, Ship>();

    public void Update(Time time, World world, in Transform transform, in Velocity velocity, in Ship ship)
    {
        if (!Input[GameAction.Fire].JustPressed) return;

        var forward = new Vector3(MathF.Cos(ship.Heading.Radians), MathF.Sin(ship.Heading.Radians), 0f);
        world.Commands.CreateEntity(Assets.BulletTemplate)
            .AddTransform(transform.Position + forward * NoseOffset, ship.Heading)
            .AddComponent(new Velocity { Value = velocity.Value + forward * BulletSpeed });

        Audio.Play(Assets.LaserSound);
    }
}
