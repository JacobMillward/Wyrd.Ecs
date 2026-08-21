using SDL3;
using Wyrd.Ecs;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Audio.Tests;

[Trait("Category", "RequiresGpu")]
public class AudioSystemListenerTests
{
    private static World BuildWorldWithPlatform() =>
        new WorldBuilder()
            .AddWindow("Audio Listener Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .AddAudio()
            .Build();

    [Fact]
    public void SetListener_LivingEntity_DoesNotThrow()
    {
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var entity = world.Commands.CreateEntity();
        world.Commands.AddComponent(entity, Transform.Identity);
        world.ApplyCommands();

        audio.SetListener(audio.DefaultOutput, entity);
        var act = () => world.Update(TimeSpan.FromMilliseconds(16));

        act.Should().NotThrow();
    }

    [Fact]
    public void SetListener_ThenEntityRemoved_UpdateDoesNotThrow()
    {
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var entity = world.Commands.CreateEntity();
        world.Commands.AddComponent(entity, Transform.Identity);
        world.ApplyCommands();
        audio.SetListener(audio.DefaultOutput, entity);

        world.Commands.DestroyEntity(entity);
        world.ApplyCommands();
        var act = () => world.Update(TimeSpan.FromMilliseconds(16));

        act.Should().NotThrow();
    }
}
