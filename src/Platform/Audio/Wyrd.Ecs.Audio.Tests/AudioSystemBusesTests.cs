using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Audio.Tests;

[Trait("Category", "RequiresGpu")]
public class AudioSystemBusesTests
{
    private static World BuildWorldWithPlatform() =>
        new WorldBuilder()
            .AddWindow("Audio Buses Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .AddAudio()
            .Build();

    [Fact]
    public void Bus_SameKindDifferentOutputs_AreNotEqual()
    {
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var otherOutput = audio.AddOutput();

        var defaultSfx = audio.Bus(BusKind.Sfx);
        var otherSfx = audio.Bus(BusKind.Sfx, otherOutput);

        defaultSfx.Should().NotBe(otherSfx);
    }

    [Fact]
    public void Bus_CalledTwiceSameArguments_AreEqual()
    {
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();

        var first = audio.Bus(BusKind.Music);
        var second = audio.Bus(BusKind.Music);

        first.Should().Be(second);
    }

    [Fact]
    public void SetBusVolume_ThenGetBusVolume_RoundTrips()
    {
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var bus = audio.Bus(BusKind.Sfx);

        audio.SetBusVolume(bus, 0.5f);

        audio.GetBusVolume(bus).Should().BeApproximately(0.5f, 0.001f);
    }

    [Fact]
    public void SetBusVolume_AboveOne_ClampsToOne()
    {
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var bus = audio.Bus(BusKind.Sfx);

        audio.SetBusVolume(bus, 2f);

        audio.GetBusVolume(bus).Should().BeApproximately(1f, 0.001f);
    }

    [Fact]
    public void SetBusVolume_BelowZero_ClampsToZero()
    {
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var bus = audio.Bus(BusKind.Sfx);

        audio.SetBusVolume(bus, -1f);

        audio.GetBusVolume(bus).Should().BeApproximately(0f, 0.001f);
    }

    [Fact]
    public void CustomBus_SameNameDifferentOutputs_AreNotEqual()
    {
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var otherOutput = audio.AddOutput();

        var defaultDialogue = audio.CustomBus("dialogue");
        var otherDialogue = audio.CustomBus("dialogue", otherOutput);

        defaultDialogue.Should().NotBe(otherDialogue);
    }
}
