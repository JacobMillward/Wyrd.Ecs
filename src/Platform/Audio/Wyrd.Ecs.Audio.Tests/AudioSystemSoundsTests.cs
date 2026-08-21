using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Audio.Tests;

[Trait("Category", "RequiresGpu")]
public class AudioSystemSoundsTests
{
    private static World BuildWorldWithPlatform() =>
        new WorldBuilder()
            .AddWindow("Audio Sounds Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .AddAudio()
            .Build();

    private static string WriteTinyTestWav()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wyrd-test-{Guid.NewGuid():N}.wav");
        const int sampleRate = 8000;
        const int sampleCount = 800; // 100ms of silence at 8kHz mono 16-bit
        var dataSize = sampleCount * 2;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);  // PCM
        writer.Write((short)1);  // mono
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2); // byte rate
        writer.Write((short)2);  // block align
        writer.Write((short)16); // bits per sample
        writer.Write("data"u8);
        writer.Write(dataSize);
        writer.Write(new byte[dataSize]); // silence
        return path;
    }

    [Fact]
    public async Task LoadSound_ValidWav_ResolvesToLoaded()
    {
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var path = WriteTinyTestWav();

        var handle = audio.LoadSound(path);
        await audio.WaitForLoadAsync(handle);

        File.Delete(path);
    }

    [Fact]
    public async Task LoadSound_MissingFile_ResolvesToFailed()
    {
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();

        var handle = audio.LoadSound("does/not/exist.wav");
        var loadTask = audio.WaitForLoadAsync(handle);

        var act = async () => await loadTask;
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task LoadSound_SamePathTwice_DecodesOnlyOnce()
    {
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var path = WriteTinyTestWav();

        var first = audio.LoadSound(path);
        var second = audio.LoadSound(path);
        await audio.WaitForLoadAsync(first);

        second.Should().Be(first);

        File.Delete(path);
    }

    [Fact]
    public void Unload_DoesNotThrow()
    {
        var world = BuildWorldWithPlatform();
        var audio = world.GetSystem<AudioSystem>();
        var path = WriteTinyTestWav();
        var handle = audio.LoadSound(path);

        var act = () => audio.Unload(handle);

        act.Should().NotThrow();
        File.Delete(path);
    }
}
