using System.Text;

namespace Wyrd.Ecs.Tests;

public class ComponentCodecChangeTrackingTests
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
    public void ReadRawChanges_WithNoTrackingEnabled_ReturnsNothing()
    {
        var world = new World();
        var registry = BuildRegistry();
        world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();
        registry.TryGetByDiscriminator("Position", out var codec);
        var source = (Wyrd.Ecs.Internal.IComponentChangeSource)codec;

        var changes = source.ReadRawChanges(world, sinceTick: 0);

        changes.Should().BeEmpty();
    }

    [Fact]
    public void EnableChangeTracking_ThenReadRawChanges_ReturnsTheBoxedValueUnencoded()
    {
        var world = new World();
        var registry = BuildRegistry(schemaHash: 7u);
        registry.TryGetByDiscriminator("Position", out var codec);
        var source = (Wyrd.Ecs.Internal.IComponentChangeSource)codec;
        using var tracking = source.EnableChangeTracking(world);

        Entity entity = world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();

        var changes = source.ReadRawChanges(world, sinceTick: 0);

        changes.Should().ContainSingle();
        var change = changes[0];
        change.Entity.Should().Be(entity);
        change.Value.Should().BeOfType<Position>().Which.X.Should().Be(5f);
    }

    [Fact]
    public void ReadRawChanges_OnlyReturnsChangesAfterTheGivenSinceTick()
    {
        var world = new World();
        var registry = BuildRegistry();
        registry.TryGetByDiscriminator("Position", out var codec);
        var source = (Wyrd.Ecs.Internal.IComponentChangeSource)codec;
        using var tracking = source.EnableChangeTracking(world);

        world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();
        var watermark = world.CurrentTick;
        world.AdvanceTick();

        Entity second = world.Commands.CreateEntity(new Position { X = 2f });
        world.ApplyCommands();

        var changes = source.ReadRawChanges(world, sinceTick: watermark);

        changes.Should().ContainSingle();
        changes[0].Entity.Should().Be(second);
    }

    [Fact]
    public void EnableChangeTracking_DisposingTheHandle_StopsFurtherTracking()
    {
        var world = new World();
        var registry = BuildRegistry();
        registry.TryGetByDiscriminator("Position", out var codec);
        var source = (Wyrd.Ecs.Internal.IComponentChangeSource)codec;
        var tracking = source.EnableChangeTracking(world);
        tracking.Dispose();

        world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        var changes = source.ReadRawChanges(world, sinceTick: 0);

        changes.Should().BeEmpty();
    }

    [Fact]
    public void EncodeValue_EncodesABoxedValueTheSameWayReadRawChangesWouldReportIt()
    {
        var world = new World();
        var registry = BuildRegistry();
        registry.TryGetByDiscriminator("Position", out var codec);
        var source = (Wyrd.Ecs.Internal.IComponentChangeSource)codec;
        using var tracking = source.EnableChangeTracking(world);
        world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();
        var raw = source.ReadRawChanges(world, sinceTick: 0)[0];

        var encoded = codec.EncodeValue(raw.Value);

        Encoding.UTF8.GetString(encoded).Should().Be("5");
    }

    [Fact]
    public void DecodeValue_DecodesBytesTheSameWayEncodeValueProducedThem()
    {
        var registry = BuildRegistry();
        registry.TryGetByDiscriminator("Position", out var codec);
        var encoded = codec.EncodeValue(new Position { X = 9f });

        var decoded = codec.DecodeValue(encoded);

        decoded.Should().BeOfType<Position>().Which.X.Should().Be(9f);
    }
}
