using System.Text;
using Wyrd.Ecs.Persistence.Continuous.Internal;

namespace Wyrd.Ecs.Persistence.Continuous.Tests;

public class StructuralChangeCaptureTests
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
    public void OnEntityCreated_CapturesAnEntityCreatedEntryWithNoPayload()
    {
        var world = new World();
        var registry = BuildRegistry();
        var captured = new List<CapturedWalEntry>();
        var observer = new StructuralChangeCapture(world, registry, captured.Add);
        using var subscription = world.ObserveStructuralChanges(observer);

        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        captured.Should().ContainSingle();
        captured[0].Kind.Should().Be(WalRecordKind.EntityCreated);
        captured[0].EntityId.Should().Be(world.GetPermanentId(entity));
        captured[0].Payload.Should().BeEmpty();
    }

    [Fact]
    public void OnEntityDestroyed_CapturesAnEntityDestroyedEntryWithTheEntitysPermanentId()
    {
        var world = new World();
        var registry = BuildRegistry();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();
        var permanentId = world.GetPermanentId(entity);
        var captured = new List<CapturedWalEntry>();
        var observer = new StructuralChangeCapture(world, registry, captured.Add);
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.DestroyEntity(entity);
        world.ApplyCommands();

        captured.Should().ContainSingle();
        captured[0].Kind.Should().Be(WalRecordKind.EntityDestroyed);
        captured[0].EntityId.Should().Be(permanentId);
    }

    [Fact]
    public void OnComponentRemoved_ForARegisteredType_CapturesItsDiscriminatorAndSchemaHash()
    {
        var world = new World();
        var registry = BuildRegistry(schemaHash: 7u);
        Entity entity = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();
        var captured = new List<CapturedWalEntry>();
        var observer = new StructuralChangeCapture(world, registry, captured.Add);
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.RemoveComponent<Position>(entity);
        world.ApplyCommands();

        captured.Should().ContainSingle();
        captured[0].Kind.Should().Be(WalRecordKind.ComponentRemoved);
        captured[0].Discriminator.Should().Be("Position");
        captured[0].SchemaHash.Should().Be(7u);
    }

    [Fact]
    public void OnComponentRemoved_ForAnUnregisteredType_CapturesNothing()
    {
        var world = new World();
        var registry = new ComponentCodecRegistry();
        Entity entity = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();
        var captured = new List<CapturedWalEntry>();
        var observer = new StructuralChangeCapture(world, registry, captured.Add);
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.RemoveComponent<Position>(entity);
        world.ApplyCommands();

        captured.Should().BeEmpty();
    }

    [Fact]
    public void OnComponentAdded_CapturesNothing()
    {
        var world = new World();
        var registry = BuildRegistry();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();
        var captured = new List<CapturedWalEntry>();
        var observer = new StructuralChangeCapture(world, registry, captured.Add);
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.AddComponent(entity, new Position { X = 1f });
        world.ApplyCommands();

        captured.Should().BeEmpty();
    }
}
