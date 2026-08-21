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
}
