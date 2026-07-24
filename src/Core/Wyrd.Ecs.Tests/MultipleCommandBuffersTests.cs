namespace Wyrd.Ecs.Tests;

public class MultipleCommandBuffersTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    [Fact]
    public void CreateCommands_ReturnsABufferIndependentOfTheBuiltInOne()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        var extra = world.CreateCommands();
        extra.AddComponent(entity, new Position { X = 3f });

        world.ApplyCommands();

        world.HasComponent<Position>(entity).Should().BeFalse();
    }

    [Fact]
    public void ApplyCommands_WithASpecificBuffer_AppliesOnlyThatBuffersQueuedCommands()
    {
        var world = new World();
        var a = world.Commands.CreateEntity();
        var b = world.Commands.CreateEntity();
        world.ApplyCommands();

        var bufferForA = world.CreateCommands();
        var bufferForB = world.CreateCommands();
        bufferForA.AddComponent(a, new Position { X = 1f });
        bufferForB.AddComponent(b, new Position { X = 2f });

        world.ApplyCommands(bufferForA);

        world.HasComponent<Position>(a).Should().BeTrue();
        world.GetComponent<Position>(a).X.Should().Be(1f);
        world.HasComponent<Position>(b).Should().BeFalse();
    }

    [Fact]
    public void ApplyCommands_PreservesAnExtraBuffersOwnQueuedOrder()
    {
        var world = new World();
        var buffer = world.CreateCommands();

        var entity = buffer.CreateEntity();
        buffer.AddComponent(entity, new Position { X = 9f });

        world.ApplyCommands(buffer);

        world.HasComponent<Position>(entity).Should().BeTrue();
        world.GetComponent<Position>(entity).X.Should().Be(9f);
    }

    [Fact]
    public void ApplyCommands_GivenABufferFromADifferentWorld_Throws()
    {
        var worldA = new World();
        var worldB = new World();
        var bufferFromA = worldA.CreateCommands();

        var act = () => worldB.ApplyCommands(bufferFromA);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MultipleBuffers_CanBeAppliedInAnyCallerChosenOrder_NotCreationOrder()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        var setsToOne = world.CreateCommands();
        var setsToTwo = world.CreateCommands();
        setsToOne.AddComponent(entity, new Position { X = 1f });
        setsToTwo.AddComponent(entity, new Position { X = 2f });

        // Applied in the reverse of creation order — the caller decides, Commands doesn't.
        world.ApplyCommands(setsToTwo);

        world.GetComponent<Position>(entity).X.Should().Be(2f);
    }
}
