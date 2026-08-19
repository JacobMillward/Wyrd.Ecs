namespace Wyrd.Ecs.Renderer.Tests;

public class TextureArenaTests
{
    [Fact]
    public void Reserve_SamePathTwice_ReturnsSameHandle()
    {
        var arena = new TextureArena();

        var first = arena.Reserve("sprites/hero.png");
        var second = arena.Reserve("sprites/hero.png");

        first.Should().Be(second);
    }

    [Fact]
    public void Reserve_ThenMarkLoaded_StateBecomesLoaded()
    {
        var arena = new TextureArena();
        var handle = arena.Reserve("sprites/hero.png");

        arena.MarkLoaded(handle, new Texture(1, 32, 32));

        arena.GetState(handle).Should().Be(LoadState.Loaded);
    }

    [Fact]
    public void Unload_UseCountAboveZero_DoesNotReadyForRelease()
    {
        var arena = new TextureArena();
        var handle = arena.Reserve("sprites/hero.png");
        arena.Reserve("sprites/hero.png"); // use-count now 2
        arena.MarkLoaded(handle, new Texture(1, 32, 32));

        arena.Unload(handle, out var readyForRelease);

        readyForRelease.Should().BeNull();
    }

    [Fact]
    public void Unload_UseCountReachesZero_ReadyForRelease()
    {
        var arena = new TextureArena();
        var handle = arena.Reserve("sprites/hero.png");
        var texture = new Texture(1, 32, 32);
        arena.MarkLoaded(handle, texture);

        arena.Unload(handle, out var readyForRelease);

        readyForRelease.Should().BeSameAs(texture);
    }

    [Fact]
    public void Reserve_AfterFullUnload_ReusesSlotWithNewGeneration()
    {
        var arena = new TextureArena();
        var first = arena.Reserve("sprites/hero.png");
        arena.MarkLoaded(first, new Texture(1, 32, 32));
        arena.Unload(first, out _);

        var second = arena.Reserve("sprites/villain.png");

        second.Index.Should().Be(first.Index);
        second.Generation.Should().NotBe(first.Generation);
    }

    [Fact]
    public void GetState_StaleHandleAfterUnload_Throws()
    {
        var arena = new TextureArena();
        var handle = arena.Reserve("sprites/hero.png");
        arena.MarkLoaded(handle, new Texture(1, 32, 32));
        arena.Unload(handle, out _);

        Func<LoadState> act = () => arena.GetState(handle);

        act.Should().Throw<InvalidOperationException>();
    }
}
