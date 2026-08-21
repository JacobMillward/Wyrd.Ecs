using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Audio.Tests;

[Trait("Category", "RequiresGpu")]
public class AudioSystemPlaybackTests
{
    private static World BuildWorldWithPlatform() =>
        new WorldBuilder()
            .AddWindow("Audio Playback Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .AddAudio()
            .Build();

    private static string WriteTinyTestWav()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wyrd-test-{Guid.NewGuid():N}.wav");
        const int sampleRate = 8000;
        const int sampleCount = 800;
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
    public async Task Play_LoadedSound_ReturnsAPlayback()
    {
        var path = WriteTinyTestWav();
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var handle = audio.LoadSound(path);
        await audio.WaitForLoadAsync(handle);

        var act = () => audio.Play(handle);

        act.Should().NotThrow();
        File.Delete(path);
    }

    [Fact]
    public async Task Play_WithNullBus_UsesDefaultSfxBus()
    {
        var path = WriteTinyTestWav();
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var handle = audio.LoadSound(path);
        await audio.WaitForLoadAsync(handle);

        var playback = audio.Play(handle);

        audio.IsPlaying(playback).Should().BeTrue();
        File.Delete(path);
    }

    [Fact]
    public async Task Play_TwiceWithSameHandle_ReturnsDistinctPlaybacks()
    {
        var path = WriteTinyTestWav();
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var handle = audio.LoadSound(path);
        await audio.WaitForLoadAsync(handle);

        var first = audio.Play(handle);
        var second = audio.Play(handle);

        first.Should().NotBe(second);
        File.Delete(path);
    }

    [Fact]
    public async Task Play_WithExplicitBusOnOtherOutput_Succeeds()
    {
        var path = WriteTinyTestWav();
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var otherOutput = audio.AddOutput();
        var handle = audio.LoadSound(path);
        await audio.WaitForLoadAsync(handle);

        var act = () => audio.Play(handle, bus: audio.Bus(BusKind.Sfx, otherOutput));

        act.Should().NotThrow();
        File.Delete(path);
    }

    [Fact]
    public void Play_StreamedPath_ReturnsAPlayback()
    {
        var path = WriteTinyTestWav();
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();

        var act = () => audio.Play(path);

        act.Should().NotThrow();
        File.Delete(path);
    }

    [Fact]
    public void Play_StreamedPathTwice_ReturnsDistinctPlaybacksNoDedup()
    {
        var path = WriteTinyTestWav();
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();

        var first = audio.Play(path);
        var second = audio.Play(path);

        first.Should().NotBe(second);
        File.Delete(path);
    }

    [Fact]
    public void Play_StreamedMissingFile_Throws()
    {
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();

        var act = () => audio.Play("does/not/exist.wav");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Stop_PlayingPlayback_StopsIt()
    {
        var path = WriteTinyTestWav();
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var playback = audio.Play(path, loop: true);

        audio.Stop(playback);

        audio.IsPlaying(playback).Should().BeFalse();
        File.Delete(path);
    }

    [Fact]
    public void Stop_WithFadeOut_DoesNotThrow()
    {
        var path = WriteTinyTestWav();
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var playback = audio.Play(path, loop: true);

        var act = () => audio.Stop(playback, TimeSpan.FromMilliseconds(50));

        act.Should().NotThrow();
        File.Delete(path);
    }

    [Fact]
    public void SetVolume_DoesNotThrow()
    {
        var path = WriteTinyTestWav();
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var playback = audio.Play(path);

        var act = () => audio.SetVolume(playback, 0.3f);

        act.Should().NotThrow();
        File.Delete(path);
    }
}
