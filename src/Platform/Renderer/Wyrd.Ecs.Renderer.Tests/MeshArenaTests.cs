namespace Wyrd.Ecs.Renderer.Tests;

public class MeshArenaTests
{
    [Fact]
    public void Reserve_SameKeyTwice_ReturnsSameHandle()
    {
        var arena = new MeshArena();

        var first = arena.Reserve(new MeshKey("models/cube.obj", 0));
        var second = arena.Reserve(new MeshKey("models/cube.obj", 0));

        first.Should().Be(second);
    }

    [Fact]
    public void Reserve_SamePathDifferentPartIndex_ReturnsDifferentHandles()
    {
        var arena = new MeshArena();

        var first = arena.Reserve(new MeshKey("models/cube.obj", 0));
        var second = arena.Reserve(new MeshKey("models/cube.obj", 1));

        first.Should().NotBe(second);
    }

    [Fact]
    public void Reserve_ThenMarkLoaded_StateBecomesLoaded()
    {
        var arena = new MeshArena();
        var handle = arena.Reserve(new MeshKey("models/cube.obj", 0));

        arena.MarkLoaded(handle, new Mesh(1, 2, 6, default));

        arena.GetState(handle).Should().Be(LoadState.Loaded);
    }

    [Fact]
    public void Unload_UseCountReachesZero_ReadyForRelease()
    {
        var arena = new MeshArena();
        var handle = arena.Reserve(new MeshKey("models/cube.obj", 0));
        var mesh = new Mesh(1, 2, 6, default);
        arena.MarkLoaded(handle, mesh);

        arena.Unload(handle, out var readyForRelease);

        readyForRelease.Should().BeSameAs(mesh);
    }

    [Fact]
    public void Reserve_AfterFullUnload_ReusesSlotWithNewGeneration()
    {
        var arena = new MeshArena();
        var first = arena.Reserve(new MeshKey("models/cube.obj", 0));
        arena.MarkLoaded(first, new Mesh(1, 2, 6, default));
        arena.Unload(first, out _);

        var second = arena.Reserve(new MeshKey("models/villain.obj", 0));

        second.Index.Should().Be(first.Index);
        second.Generation.Should().NotBe(first.Generation);
    }

    [Fact]
    public void GetState_StaleHandleAfterUnload_Throws()
    {
        var arena = new MeshArena();
        var handle = arena.Reserve(new MeshKey("models/cube.obj", 0));
        arena.MarkLoaded(handle, new Mesh(1, 2, 6, default));
        arena.Unload(handle, out _);

        Func<LoadState> act = () => arena.GetState(handle);

        act.Should().Throw<InvalidOperationException>();
    }
}
