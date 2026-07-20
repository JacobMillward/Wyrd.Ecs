using System.Text;

namespace Wyrd.Ecs.Tests;

public class ComponentCodecChangeTrackingTests
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
    public void EncodeChanges_WithNoTrackingEnabled_ReturnsNothing()
    {
        var world = new World();
        var registry = BuildRegistry();
        world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();
        registry.TryGetByDiscriminator("Position", out var codec);

        var changes = codec.EncodeChanges(world, sinceTick: 0);

        changes.Should().BeEmpty();
    }

    [Fact]
    public void EnableChangeTracking_ThenEncodeChanges_ReturnsTheChangedValueEncoded()
    {
        var world = new World();
        var registry = BuildRegistry(schemaHash: 7u);
        registry.TryGetByDiscriminator("Position", out var codec);
        using var tracking = codec.EnableChangeTracking(world);

        var entity = world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();

        var changes = codec.EncodeChanges(world, sinceTick: 0);

        changes.Should().ContainSingle();
        var change = changes[0];
        change.Entity.Should().Be(entity);
        change.Discriminator.Should().Be("Position");
        change.SchemaHash.Should().Be(7u);
        Encoding.UTF8.GetString(change.Data).Should().Be("5");
    }

    [Fact]
    public void EncodeChanges_OnlyReturnsChangesAfterTheGivenSinceTick()
    {
        var world = new World();
        var registry = BuildRegistry();
        registry.TryGetByDiscriminator("Position", out var codec);
        using var tracking = codec.EnableChangeTracking(world);

        world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();
        var watermark = world.CurrentTick;
        world.AdvanceTick();

        var second = world.Commands.CreateEntity(new Position { X = 2f });
        world.ApplyCommands();

        var changes = codec.EncodeChanges(world, sinceTick: watermark);

        changes.Should().ContainSingle();
        changes[0].Entity.Should().Be(second);
    }

    [Fact]
    public void EnableChangeTracking_DisposingTheHandle_StopsFurtherTracking()
    {
        var world = new World();
        var registry = BuildRegistry();
        registry.TryGetByDiscriminator("Position", out var codec);
        var tracking = codec.EnableChangeTracking(world);
        tracking.Dispose();

        world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        var changes = codec.EncodeChanges(world, sinceTick: 0);

        changes.Should().BeEmpty();
    }
}
