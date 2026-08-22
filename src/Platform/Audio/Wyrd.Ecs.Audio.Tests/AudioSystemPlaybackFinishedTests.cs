using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Audio.Tests;

[Trait("Category", "RequiresGpu")]
public class AudioSystemPlaybackFinishedTests
{
    private static World BuildWorldWithPlatform() =>
        new WorldBuilder()
            .AddWindow("Audio PlaybackFinished Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .AddAudio()
            .Build();

    private static string WriteTinyTestWav()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wyrd-test-{Guid.NewGuid():N}.wav");
        const int sampleRate = 8000;
        const int sampleCount = 80; // 10ms - short, so the test doesn't wait long for natural completion
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
    public async Task Update_AfterExplicitStop_EmitsPlaybackFinished()
    {
        var path = WriteTinyTestWav();
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var reader = world.CreateEventReader<PlaybackFinished>();
        var playback = audio.Play(path, loop: true);

        audio.Stop(playback);
        var found = false;
        for (var i = 0; i < 20 && !found; i++)
        {
            world.Update(TimeSpan.FromMilliseconds(16));
            await Task.Delay(10);
            found = reader.Read().Any(e => e.Playback == playback);
        }

        found.Should().BeTrue();
        File.Delete(path);
    }

    [Fact]
    public async Task Update_AfterNaturalCompletion_EmitsPlaybackFinished()
    {
        var path = WriteTinyTestWav();
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var reader = world.CreateEventReader<PlaybackFinished>();
        var playback = audio.Play(path); // not looping - finishes on its own after ~10ms

        var found = false;
        for (var i = 0; i < 50 && !found; i++)
        {
            world.Update(TimeSpan.FromMilliseconds(16));
            await Task.Delay(10);
            found = reader.Read().Any(e => e.Playback == playback);
        }

        found.Should().BeTrue();
        File.Delete(path);
    }

    [Fact]
    public async Task Update_AfterNaturalCompletion_InvalidatesThePlaybackHandle()
    {
        var path = WriteTinyTestWav();
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var reader = world.CreateEventReader<PlaybackFinished>();
        var playback = audio.Play(path); // not looping - finishes on its own after ~10ms

        var found = false;
        for (var i = 0; i < 50 && !found; i++)
        {
            world.Update(TimeSpan.FromMilliseconds(16));
            await Task.Delay(10);
            found = reader.Read().Any(e => e.Playback == playback);
        }

        found.Should().BeTrue();
        var act = () => audio.IsPlaying(playback);
        act.Should().Throw<InvalidOperationException>();
        File.Delete(path);
    }

    [Fact]
    public async Task Update_AfterNaturalCompletion_RecyclesTheSlotForANewPlayback()
    {
        var pathA = WriteTinyTestWav();
        var pathB = WriteTinyTestWav();
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var reader = world.CreateEventReader<PlaybackFinished>();
        var first = audio.Play(pathA); // not looping - finishes on its own after ~10ms

        var found = false;
        for (var i = 0; i < 50 && !found; i++)
        {
            world.Update(TimeSpan.FromMilliseconds(16));
            await Task.Delay(10);
            found = reader.Read().Any(e => e.Playback == first);
        }
        found.Should().BeTrue();

        var second = audio.Play(pathB);

        second.Index.Should().Be(first.Index);
        second.Generation.Should().Be(first.Generation + 1);
        File.Delete(pathA);
        File.Delete(pathB);
    }
}
