using SDL3;
using Wyrd.Ecs;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Audio.Tests;

[Trait("Category", "RequiresGpu")]
public class AudioSystemFollowTests
{
    private static World BuildWorldWithPlatform() =>
        new WorldBuilder()
            .AddWindow("Audio Follow Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .AddAudio()
            .Build();

    private static string WriteTinyTestWav()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wyrd-test-{Guid.NewGuid():N}.wav");
        const int sampleRate = 8000;
        const int sampleCount = 8000; // 1s - long enough to still be playing after a few ticks
        var dataSize = sampleCount * 2;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataSize);
        writer.Write(new byte[dataSize]);
        return path;
    }

    [Fact]
    public void Follow_LivingEntity_DoesNotStopPlayback()
    {
        var path = WriteTinyTestWav();
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var entity = world.Commands.CreateEntity();
        world.Commands.AddComponent(entity, Transform.Identity);
        world.ApplyCommands();
        var playback = audio.Play(path, loop: true);

        audio.Follow(playback, entity);
        world.Update(TimeSpan.FromMilliseconds(16));

        audio.IsPlaying(playback).Should().BeTrue();
        File.Delete(path);
    }

    [Fact]
    public void Follow_ThenEntityRemoved_StopsPlaybackOnNextUpdate()
    {
        var path = WriteTinyTestWav();
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var entity = world.Commands.CreateEntity();
        world.Commands.AddComponent(entity, Transform.Identity);
        world.ApplyCommands();
        var playback = audio.Play(path, loop: true);
        audio.Follow(playback, entity);

        world.Commands.DestroyEntity(entity);
        world.ApplyCommands();
        world.Update(TimeSpan.FromMilliseconds(16));

        audio.IsPlaying(playback).Should().BeFalse();
        File.Delete(path);
    }
}
