namespace Wyrd.Ecs.Assets.Tests;

public class AssetArenaTests
{
    private sealed record Sound(string Data);

    [Fact]
    public void Reserve_NewKey_ReturnsIsNewTrue()
    {
        var arena = new AssetArena<string, Sound>();

        arena.Reserve("foo.wav", out var isNew);

        isNew.Should().BeTrue();
    }

    [Fact]
    public void Reserve_SameKeyTwice_SecondCallReturnsIsNewFalseAndSameHandle()
    {
        var arena = new AssetArena<string, Sound>();

        var first = arena.Reserve("foo.wav", out _);
        var second = arena.Reserve("foo.wav", out var isNew);

        isNew.Should().BeFalse();
        second.Should().Be(first);
    }

    [Fact]
    public void Reserve_DifferentKeys_ReturnsDifferentHandles()
    {
        var arena = new AssetArena<string, Sound>();

        var a = arena.Reserve("a.wav", out _);
        var b = arena.Reserve("b.wav", out _);

        a.Should().NotBe(b);
    }

    [Fact]
    public void GetState_BeforeAnyMark_IsLoading()
    {
        var arena = new AssetArena<string, Sound>();
        var handle = arena.Reserve("foo.wav", out _);

        arena.GetState(handle).Should().Be(LoadState.Loading);
    }

    [Fact]
    public void TryGet_BeforeMarkLoaded_ReturnsNull()
    {
        var arena = new AssetArena<string, Sound>();
        var handle = arena.Reserve("foo.wav", out _);

        arena.TryGet(handle).Should().BeNull();
    }

    [Fact]
    public void MarkLoaded_SetsStateLoadedAndStoresAsset()
    {
        var arena = new AssetArena<string, Sound>();
        var handle = arena.Reserve("foo.wav", out _);
        var sound = new Sound("pcm-bytes");

        arena.MarkLoaded(handle, sound);

        arena.GetState(handle).Should().Be(LoadState.Loaded);
        arena.TryGet(handle).Should().BeSameAs(sound);
    }

    [Fact]
    public void MarkFailed_SetsStateFailed()
    {
        var arena = new AssetArena<string, Sound>();
        var handle = arena.Reserve("foo.wav", out _);

        arena.MarkFailed(handle, new InvalidOperationException("decode failed"));

        arena.GetState(handle).Should().Be(LoadState.Failed);
    }

    [Fact]
    public void MarkLoaded_AfterAlreadyFailed_IsNoOp()
    {
        var arena = new AssetArena<string, Sound>();
        var handle = arena.Reserve("foo.wav", out _);
        arena.MarkFailed(handle, new InvalidOperationException("decode failed"));

        arena.MarkLoaded(handle, new Sound("pcm-bytes"));

        arena.GetState(handle).Should().Be(LoadState.Failed);
        arena.TryGet(handle).Should().BeNull();
    }

    [Fact]
    public void MarkFailed_AfterAlreadyLoaded_IsNoOp()
    {
        var arena = new AssetArena<string, Sound>();
        var handle = arena.Reserve("foo.wav", out _);
        var sound = new Sound("pcm-bytes");
        arena.MarkLoaded(handle, sound);

        arena.MarkFailed(handle, new InvalidOperationException("late failure"));

        arena.GetState(handle).Should().Be(LoadState.Loaded);
        arena.TryGet(handle).Should().BeSameAs(sound);
    }

    [Fact]
    public void GetState_InvalidHandle_Throws()
    {
        var arena = new AssetArena<string, Sound>();

        var act = () => arena.GetState(new Handle<Sound>(99, 0));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task WaitForLoadAsync_ThenMarkLoaded_CompletesTask()
    {
        var arena = new AssetArena<string, Sound>();
        var handle = arena.Reserve("foo.wav", out _);

        var waitTask = arena.WaitForLoadAsync(handle);
        arena.MarkLoaded(handle, new Sound("pcm-bytes"));

        await waitTask;
        waitTask.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task WaitForLoadAsync_ThenMarkFailed_FaultsWithSameException()
    {
        var arena = new AssetArena<string, Sound>();
        var handle = arena.Reserve("foo.wav", out _);
        var exception = new InvalidOperationException("decode failed");

        var waitTask = arena.WaitForLoadAsync(handle);
        arena.MarkFailed(handle, exception);

        var act = async () => await waitTask;
        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(exception);
    }

    [Fact]
    public async Task WaitForLoadAsync_OnDedupHandle_SharesOriginalCompletion()
    {
        var arena = new AssetArena<string, Sound>();
        var first = arena.Reserve("foo.wav", out _);
        var second = arena.Reserve("foo.wav", out _);
        var sound = new Sound("pcm-bytes");

        var waitTask = arena.WaitForLoadAsync(second);
        arena.MarkLoaded(first, sound);

        await waitTask;
        arena.TryGet(second).Should().BeSameAs(sound);
    }

    [Fact]
    public void Unload_WhileOtherUsersRemain_ReturnsFalse()
    {
        var arena = new AssetArena<string, Sound>();
        arena.Reserve("foo.wav", out _);
        var handle = arena.Reserve("foo.wav", out _);

        var released = arena.Unload(handle, out var readyForRelease);

        released.Should().BeFalse();
        readyForRelease.Should().BeNull();
        arena.GetState(handle).Should().Be(LoadState.Loading);
    }

    [Fact]
    public void Unload_LastUser_ReturnsTrueWithAssetAndBumpsGeneration()
    {
        var arena = new AssetArena<string, Sound>();
        var handle = arena.Reserve("foo.wav", out _);
        var sound = new Sound("pcm-bytes");
        arena.MarkLoaded(handle, sound);

        var released = arena.Unload(handle, out var readyForRelease);

        released.Should().BeTrue();
        readyForRelease.Should().BeSameAs(sound);

        var act = () => arena.GetState(handle);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reserve_AfterSlotFreedByUnload_ReusesIndexWithNewGeneration()
    {
        var arena = new AssetArena<string, Sound>();
        var original = arena.Reserve("foo.wav", out _);
        arena.Unload(original, out _);

        var reused = arena.Reserve("bar.wav", out var isNew);

        isNew.Should().BeTrue();
        reused.Index.Should().Be(original.Index);
        reused.Generation.Should().NotBe(original.Generation);

        var act = () => arena.GetState(original);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task FaultAllPending_FaultsOnlyLoadingSlots()
    {
        var arena = new AssetArena<string, Sound>();
        var loading = arena.Reserve("loading.wav", out _);
        var loaded = arena.Reserve("loaded.wav", out _);
        var sound = new Sound("pcm-bytes");
        arena.MarkLoaded(loaded, sound);

        arena.FaultAllPending(new ObjectDisposedException("teardown"));

        arena.GetState(loading).Should().Be(LoadState.Failed);
        var act = async () => await arena.WaitForLoadAsync(loading);
        await act.Should().ThrowAsync<ObjectDisposedException>();

        arena.GetState(loaded).Should().Be(LoadState.Loaded);
        arena.TryGet(loaded).Should().BeSameAs(sound);
    }

    [Fact]
    public void Reserve_ConcurrentSameKey_AllCallsDedupToOneSlot()
    {
        var arena = new AssetArena<string, Sound>();
        var handles = new Handle<Sound>[50];

        Parallel.For(0, 50, i => handles[i] = arena.Reserve("shared.wav", out _));

        handles.Should().AllSatisfy(h => h.Should().Be(handles[0]));
    }

    [Fact]
    public async Task WaitForLoadAsync_CalledAfterAlreadyLoaded_ReturnsCompletedTask()
    {
        var arena = new AssetArena<string, Sound>();
        var handle = arena.Reserve("foo.wav", out _);
        arena.MarkLoaded(handle, new Sound("pcm-bytes"));

        var task = arena.WaitForLoadAsync(handle);

        await task;
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task WaitForLoadAsync_CalledAfterAlreadyFailed_ReturnsFaultedTaskWithSameException()
    {
        var arena = new AssetArena<string, Sound>();
        var handle = arena.Reserve("foo.wav", out _);
        var exception = new InvalidOperationException("decode failed");
        arena.MarkFailed(handle, exception);

        var task = arena.WaitForLoadAsync(handle);

        var act = async () => await task;
        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(exception);
    }

    [Fact]
    public void WaitForLoadAsync_CalledTwiceBeforeResolution_ReturnsSameTask()
    {
        var arena = new AssetArena<string, Sound>();
        var handle = arena.Reserve("foo.wav", out _);

        var first = arena.WaitForLoadAsync(handle);
        var second = arena.WaitForLoadAsync(handle);

        second.Should().BeSameAs(first);
    }
}
