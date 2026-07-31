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
    public void ComponentValueChange_AppearsInThePendingListAfterATick_UnencodedAndCorrect()
    {
        var world = new World();
        var registry = BuildRegistry(schemaHash: 7u);
        using var capture = new ChangeCapture(world, registry);

        Entity entity = world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();
        world.AdvanceTick();

        var drained = capture.SwapBuffers();

        var pending = drained.Pending.Should().ContainSingle().Which;
        pending.EntityId.Should().Be(world.GetPermanentId(entity));
        pending.Codec.Discriminator.Should().Be("Position");
        pending.Codec.SchemaHash.Should().Be(7u);
        pending.Value.Should().BeOfType<Position>().Which.X.Should().Be(5f);
        drained.Ready.Should().NotContain(e => e.Kind == WalRecordKind.ComponentChanged);
    }

    [Fact]
    public void EntityDestroyed_AppearsInTheReadyList()
    {
        var world = new World();
        var registry = BuildRegistry();
        Entity entity = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();
        using var capture = new ChangeCapture(world, registry);
        var permanentId = world.GetPermanentId(entity);

        world.Commands.DestroyEntity(entity);
        world.ApplyCommands();

        var drained = capture.SwapBuffers();

        drained.Ready.Should().Contain(e => e.Kind == WalRecordKind.EntityDestroyed && e.EntityId == permanentId);
    }

    [Fact]
    public void ComponentAdd_ProducesNoDirectEntry_ButItsValueAppearsViaTheNextTicksScan()
    {
        var world = new World();
        var registry = BuildRegistry();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();
        using var capture = new ChangeCapture(world, registry);

        world.Commands.AddComponent(entity, new Position { X = 3f });
        world.ApplyCommands();
        var beforeTick = capture.SwapBuffers();
        beforeTick.Pending.Should().BeEmpty();

        world.AdvanceTick();
        var afterTick = capture.SwapBuffers();

        afterTick.Pending.Should().ContainSingle();
    }

    [Fact]
    public void SwapBuffers_CalledTwiceWithNoActivityBetween_ReturnsEmptyListsTheSecondTime()
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

        second.Ready.Should().BeEmpty();
        second.Pending.Should().BeEmpty();
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
        drained.Ready.Should().BeEmpty();
        drained.Pending.Should().BeEmpty();
    }
}
