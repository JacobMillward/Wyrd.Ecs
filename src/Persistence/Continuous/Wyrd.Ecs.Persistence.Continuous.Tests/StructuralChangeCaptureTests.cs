using System.Text;
using Wyrd.Ecs.Persistence.Continuous.Internal;

namespace Wyrd.Ecs.Persistence.Continuous.Tests;

public class StructuralChangeCaptureTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    private static CodecRegistry BuildRegistry(uint? schemaHash = null)
    {
        var registry = new CodecRegistry();
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
        var registry = new CodecRegistry();
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

    private struct Likes : IRelation
    {
        public float Weight;
    }

    private static CodecRegistry BuildRegistryWithRelation()
    {
        var registry = BuildRegistry();
        registry.RegisterRelation<Likes>("Likes",
            v => BitConverter.GetBytes(v.Weight),
            d => new Likes { Weight = BitConverter.ToSingle(d) });
        return registry;
    }

    [Fact]
    public void OnRelationLinked_CapturesTheEdgesCurrentPayload()
    {
        var world = new World();
        var registry = BuildRegistryWithRelation();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.ApplyCommands();
        var captured = new List<CapturedWalEntry>();
        var observer = new StructuralChangeCapture(world, registry, captured.Add);
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.AddRelation(a, b, new Likes { Weight = 4f });
        world.ApplyCommands();

        captured.Should().ContainSingle();
        captured[0].Kind.Should().Be(WalRecordKind.RelationLinked);
        captured[0].EntityId.Should().Be(world.GetPermanentId(a));
        captured[0].TargetId.Should().Be(world.GetPermanentId(b));
        captured[0].Discriminator.Should().Be("Likes");
        BitConverter.ToSingle(captured[0].Payload).Should().Be(4f);
    }

    [Fact]
    public void OnRelationUnlinked_CapturesNoPayload()
    {
        var world = new World();
        var registry = BuildRegistryWithRelation();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();
        var captured = new List<CapturedWalEntry>();
        var observer = new StructuralChangeCapture(world, registry, captured.Add);
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.RemoveRelation<Likes>(a, b);
        world.ApplyCommands();

        captured.Should().ContainSingle();
        captured[0].Kind.Should().Be(WalRecordKind.RelationUnlinked);
        captured[0].EntityId.Should().Be(world.GetPermanentId(a));
        captured[0].TargetId.Should().Be(world.GetPermanentId(b));
        captured[0].Payload.Should().BeEmpty();
    }

    private struct Enemy : ITag;

    [Fact]
    public void OnTagAdded_ForARegisteredTag_CapturesItsDiscriminator()
    {
        var world = new World();
        var registry = BuildRegistry();
        registry.RegisterTag<Enemy>("Enemy");
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();
        var captured = new List<CapturedWalEntry>();
        var observer = new StructuralChangeCapture(world, registry, captured.Add);
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.AddTag<Enemy>(entity);
        world.ApplyCommands();

        captured.Should().ContainSingle();
        captured[0].Kind.Should().Be(WalRecordKind.TagAdded);
        captured[0].EntityId.Should().Be(world.GetPermanentId(entity));
        captured[0].Discriminator.Should().Be("Enemy");
    }

    [Fact]
    public void OnTagAdded_ForAnUnregisteredTag_CapturesNothing()
    {
        var world = new World();
        var registry = BuildRegistry(); // Enemy deliberately not registered
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();
        var captured = new List<CapturedWalEntry>();
        var observer = new StructuralChangeCapture(world, registry, captured.Add);
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.AddTag<Enemy>(entity);
        world.ApplyCommands();

        captured.Should().BeEmpty();
    }

    [Fact]
    public void OnTagRemoved_ForARegisteredTag_CapturesItsDiscriminator()
    {
        var world = new World();
        var registry = BuildRegistry();
        registry.RegisterTag<Enemy>("Enemy");
        Entity entity = world.Commands.CreateEntity();
        world.Commands.AddTag<Enemy>(entity);
        world.ApplyCommands();
        var captured = new List<CapturedWalEntry>();
        var observer = new StructuralChangeCapture(world, registry, captured.Add);
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.RemoveTag<Enemy>(entity);
        world.ApplyCommands();

        captured.Should().ContainSingle();
        captured[0].Kind.Should().Be(WalRecordKind.TagRemoved);
        captured[0].EntityId.Should().Be(world.GetPermanentId(entity));
        captured[0].Discriminator.Should().Be("Enemy");
    }

    [Fact]
    public void OnRelationLinked_ForAnUnregisteredRelationType_CapturesNothing()
    {
        var world = new World();
        var registry = BuildRegistry(); // Likes deliberately not registered
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.ApplyCommands();
        var captured = new List<CapturedWalEntry>();
        var observer = new StructuralChangeCapture(world, registry, captured.Add);
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        captured.Should().BeEmpty();
    }
}
