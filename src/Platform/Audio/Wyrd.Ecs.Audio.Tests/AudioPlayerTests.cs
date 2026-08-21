using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Audio.Tests;

[Trait("Category", "RequiresGpu")]
public class AudioPlayerTests
{
    [Fact]
    public void AddAudio_RegistersAudioPlayerResource()
    {
        var world = new WorldBuilder()
            .AddWindow("AudioPlayer Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .AddAudio()
            .Build();

        var player = world.GetResource<AudioPlayer>();

        player.Audio.Should().BeSameAs(world.GetSystem<AudioSystem>());
    }
}
