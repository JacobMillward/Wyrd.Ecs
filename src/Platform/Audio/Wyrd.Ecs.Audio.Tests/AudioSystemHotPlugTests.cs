using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Audio.Tests;

[Trait("Category", "RequiresGpu")]
public class AudioSystemHotPlugTests
{
    [Fact]
    public void Update_AudioOutputDisconnectEvent_DoesNotThrow()
    {
        var world = new WorldBuilder()
            .AddWindow("Audio HotPlug Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .AddAudio()
            .Build();
        var pushed = new SDL.Event { Type = (uint)SDL.EventType.AudioDeviceRemoved, ADevice = new SDL.AudioDeviceEvent { Type = SDL.EventType.AudioDeviceRemoved, Which = 999, Recording = false } };
        SDL.PushEvent(ref pushed);

        var act = () => world.Update(TimeSpan.FromMilliseconds(16));

        act.Should().NotThrow();
    }

    // A nonexistent path fails MIX_LoadAudio synchronously fast enough on its background thread
    // to race world.RemoveSystem's very next statement - to actually exercise "destroyed while
    // still decoding" instead of "destroyed after it already failed on its own", this writes a
    // large WAV at a sample rate the mixer has to resample, which reliably keeps the decode
    // in flight for well over a hundred milliseconds (empirically ~150-200ms locally).
    private static string WriteSlowToDecodeTestWav()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wyrd-test-{Guid.NewGuid():N}.wav");
        const int sampleRate = 11025; // mismatched vs the mixer's own output format, forcing real resampling work
        const int sampleCount = sampleRate * 3000; // ~50 minutes - far more than needed to outlast RemoveSystem's call
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
    public async Task RemoveSystem_WhileLoadInFlight_FaultsWaitForLoadAsyncInsteadOfHanging()
    {
        var path = WriteSlowToDecodeTestWav();
        var world = new WorldBuilder()
            .AddWindow("Audio HotPlug Teardown Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .AddAudio()
            .Build();
        var audio = world.GetSystem<AudioSystem>();
        var handle = audio.LoadSound(path);
        var waitTask = audio.WaitForLoadAsync(handle);

        world.RemoveSystem(audio);

        var act = async () => await waitTask.WaitAsync(TimeSpan.FromSeconds(5));
        await act.Should().ThrowAsync<ObjectDisposedException>();
        File.Delete(path);
    }
}
