using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Audio.Tests;

[Trait("Category", "RequiresGpu")]
public class AudioSystemOutputsTests
{
    private static World BuildWorldWithPlatform() =>
        new WorldBuilder()
            .AddWindow("Audio Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .AddAudio()
            .Build();

    [Fact]
    public void Constructor_CreatesADefaultOutput()
    {
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();

        var act = () => audio.DefaultOutput;

        act.Should().NotThrow();
    }

    [Fact]
    public void AddOutput_WithDefaultDeviceId_ReturnsANewOutputDistinctFromDefaultOutput()
    {
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();

        var second = audio.AddOutput();

        second.Should().NotBe(audio.DefaultOutput);
    }

    [Fact]
    public void SetDefaultOutput_ChangesDefaultOutput()
    {
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var second = audio.AddOutput();

        audio.SetDefaultOutput(second);

        audio.DefaultOutput.Should().Be(second);
    }

    [Fact]
    public void GetAvailableOutputDevices_ReturnsAtLeastOneDevice()
    {
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();

        var devices = audio.GetAvailableOutputDevices();

        devices.Should().NotBeEmpty();
    }

    [Fact]
    public void RemoveSystem_RunsCleanupWithoutThrowing()
    {
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();

        var act = () => world.RemoveSystem(audio);

        act.Should().NotThrow();
    }
}
