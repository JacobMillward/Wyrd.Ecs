using System.Text;

namespace Wyrd.Ecs.Persistence.Continuous.Tests;

public class ChangeCaptureTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    private static ComponentCodecRegistry BuildRegistry(uint? schemaHash = null)
    {
        var registry = new ComponentCodecRegistry();
        registry.Register<Position>("Position",
            p => Encoding.UTF8.GetBytes(p.X.ToString()),
            bytes => new Position { X = float.Parse(Encoding.UTF8.GetString(bytes)) },
            schemaHash);
        return registry;
    }

    [Fact]
    public void ComponentValueChange_AppearsInTheSwappedBufferAfterATick()
    {
        var world = new World();
        var registry = BuildRegistry(schemaHash: 7u);
        using var capture = new ChangeCapture(world, registry);

        var entity = world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();
        world.AdvanceTick();

        var drained = capture.SwapBuffers();

        var entry = drained.Should().ContainSingle(e => e.Kind == WalRecordKind.ComponentChanged).Which;
        entry.EntityId.Should().Be(world.GetPermanentId(entity));
        entry.Discriminator.Should().Be("Position");
        entry.SchemaHash.Should().Be(7u);
        Encoding.UTF8.GetString(entry.Payload).Should().Be("5");
    }

    [Fact]
    public void EntityDestroyed_AppearsInTheBuffer()
    {
        var world = new World();
        var registry = BuildRegistry();
        var entity = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();
        using var capture = new ChangeCapture(world, registry);
        var permanentId = world.GetPermanentId(entity);

        world.Commands.DestroyEntity(entity);
        world.ApplyCommands();

        var drained = capture.SwapBuffers();

        drained.Should().Contain(e => e.Kind == WalRecordKind.EntityDestroyed && e.EntityId == permanentId);
    }

    [Fact]
    public void ComponentAdd_ProducesNoDirectEntry_ButItsValueAppearsViaTheNextTicksScan()
    {
        var world = new World();
        var registry = BuildRegistry();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();
        using var capture = new ChangeCapture(world, registry);

        world.Commands.AddComponent(entity, new Position { X = 3f });
        world.ApplyCommands();
        var beforeTick = capture.SwapBuffers();
        beforeTick.Should().NotContain(e => e.Kind == WalRecordKind.ComponentChanged);

        world.AdvanceTick();
        var afterTick = capture.SwapBuffers();

        afterTick.Should().ContainSingle(e => e.Kind == WalRecordKind.ComponentChanged);
    }

    [Fact]
    public void SwapBuffers_CalledTwiceWithNoActivityBetween_ReturnsAnEmptyListTheSecondTime()
    {
        var world = new World();
        var registry = BuildRegistry();
        using var capture = new ChangeCapture(world, registry);
        world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();
        world.AdvanceTick();
        capture.SwapBuffers();

        world.AdvanceTick();
        var second = capture.SwapBuffers();

        second.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_StopsFurtherCapture()
    {
        var world = new World();
        var registry = BuildRegistry();
        var capture = new ChangeCapture(world, registry);
        capture.Dispose();

        world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();
        world.AdvanceTick();

        var drained = capture.SwapBuffers();
        drained.Should().BeEmpty();
    }
}
