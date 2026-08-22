using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Assets.Tests;

/// <summary>
/// Unloading an asset while its load is still in flight is legal and ordinary (a scene
/// switching faster than disk). The load's background work then finishes holding a handle
/// whose slot no longer exists - and since <see cref="AssetArena{TKey,TAsset}.MarkLoaded"/>/
/// <see cref="AssetArena{TKey,TAsset}.MarkFailed"/> are documented first-resolution-wins
/// ("no-op if the slot is no longer Loading"), a destroyed slot must be the same benign
/// discard, not an exception: the renderer's copy pass calls these between GPU resource
/// creations, and a throw there kills the frame and leaks everything created above it.
/// </summary>
public class AssetArenaUnloadDuringLoadTests
{
    private sealed record Sound(string Data);

    [Fact]
    public void MarkLoaded_AfterUnloadWhileLoading_IsDiscardedNotThrown()
    {
        var arena = new AssetArena<string, Sound>();
        var handle = arena.Reserve("late-texture.png", out var isNew);
        isNew.Should().BeTrue();

        arena.Unload(handle, out _);

        // The decode task finishes after the unload; first-resolution-wins means lost.
        var act = () => arena.MarkLoaded(handle, new Sound("pcm-bytes"));
        act.Should().NotThrow();
    }

    [Fact]
    public void MarkFailed_AfterUnloadWhileLoading_IsDiscardedNotThrown()
    {
        var arena = new AssetArena<string, Sound>();
        var handle = arena.Reserve("late-failure.png", out _);

        arena.Unload(handle, out _);

        var act = () => arena.MarkFailed(handle, new InvalidOperationException("decode failed"));
        act.Should().NotThrow();
    }

    [Fact]
    public void Unload_WhileLoading_FaultsPendingWaiterInsteadOfHanging()
    {
        var arena = new AssetArena<string, Sound>();
        var handle = arena.Reserve("awaited-then-unloaded.png", out _);
        var wait = arena.WaitForLoadAsync(handle);

        arena.Unload(handle, out _);

        // A waiter that subscribed before the unload must observe it; only teardown's
        // FaultAllPending did this before, so a per-slot unload left the task pending forever.
        wait.IsFaulted.Should().BeTrue("unloading a slot resolves nothing later");
    }

    [Fact]
    public void Unload_WhileOtherUsersRemain_DoesNotFaultPendingWaiter()
    {
        var arena = new AssetArena<string, Sound>();
        var handle = arena.Reserve("shared.png", out _);
        var second = arena.Reserve("shared.png", out _);
        var wait = arena.WaitForLoadAsync(handle);

        arena.Unload(handle, out _); // use-count 2 -> 1: the load is still wanted

        wait.IsCompleted.Should().BeFalse("the slot survives while any user remains");
        arena.GetState(second).Should().Be(LoadState.Loading);
    }

    [Fact]
    public void SecondResolutionOnLiveSlot_ReportsAlreadyResolved()
    {
        var arena = new AssetArena<string, Sound>();
        var handle = arena.Reserve("once.png", out _);

        var winner = arena.MarkLoaded(handle, new Sound("first"));
        var loserLoaded = arena.MarkLoaded(handle, new Sound("second"));
        var loserFailed = arena.MarkFailed(handle, new InvalidOperationException("late"));

        winner.Should().Be(AssetResolution.Landed);
        loserLoaded.Should().Be(AssetResolution.AlreadyResolved);
        loserFailed.Should().Be(AssetResolution.AlreadyResolved);
        arena.TryGet(handle).Should().BeEquivalentTo(new Sound("first"), "first resolution wins");
    }

    [Fact]
    public void DiscardedLoad_DoesNotPoisonFreshReservationOfSameKey()
    {
        var arena = new AssetArena<string, Sound>();
        var stale = arena.Reserve("reloaded.png", out _);
        arena.Unload(stale, out _);
        arena.MarkLoaded(stale, new Sound("ghost")); // discarded

        var fresh = arena.Reserve("reloaded.png", out var isNew);
        isNew.Should().BeTrue("the key was removed by the unload");
        arena.GetState(fresh).Should().Be(LoadState.Loading);

        arena.MarkLoaded(fresh, new Sound("real"));
        arena.GetState(fresh).Should().Be(LoadState.Loaded);
        arena.TryGet(fresh).Should().BeEquivalentTo(new Sound("real"));
    }
}
