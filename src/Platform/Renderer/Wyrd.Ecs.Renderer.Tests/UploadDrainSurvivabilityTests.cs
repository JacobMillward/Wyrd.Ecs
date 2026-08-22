using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Renderer.Tests;

/// <summary>
/// The copy pass drains pending uploads with no per-callback isolation (one throw aborts the
/// pass and the frame), so upload callbacks must survive every legal race. Unloading an asset
/// while its background load runs is one such race: the callback finishes holding a stale
/// handle, and the arena's first-resolution-wins contract makes that a discard, not an error.
/// </summary>
public class UploadDrainSurvivabilityTests
{
    private sealed record FakeAsset(string Tag);

    [Fact]
    public void DrainInto_SurvivesCallbackWhoseSlotWasUnloadedMidLoad()
    {
        var arena = new AssetArena<string, FakeAsset>();
        var handle = arena.Reserve("scene-texture.png", out _);

        // Simulates the load losing the unload race between decode and copy pass.
        arena.Unload(handle, out _);

        var queue = new PendingUploadQueue();
        queue.Enqueue(_ => arena.MarkFailed(handle, new InvalidOperationException("late decode failure")));
        queue.Enqueue(_ => arena.MarkLoaded(handle, new FakeAsset("late pixels")));

        var drain = () => queue.DrainInto(copyPass: IntPtr.Zero);
        drain.Should().NotThrow();
        arena.IsLive(handle).Should().BeFalse();
    }

    [Fact]
    public void DrainInto_NormalLoadStillResolves()
    {
        var arena = new AssetArena<string, FakeAsset>();
        var handle = arena.Reserve("healthy.png", out _);

        var queue = new PendingUploadQueue();
        AssetResolution resolution = default;
        queue.Enqueue(_ => resolution = arena.MarkLoaded(handle, new FakeAsset("pixels")));
        queue.DrainInto(IntPtr.Zero);

        resolution.Should().Be(AssetResolution.Landed);
        arena.GetState(handle).Should().Be(LoadState.Loaded);
    }
}
