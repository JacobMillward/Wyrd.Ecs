using System.Text;

namespace Wyrd.Ecs.Tests;

public class SerializerRegistryTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    private struct Velocity : IComponent
    {
        public float X;
    }

    private static ComponentSerializer<Position> SerializePosition => p => Encoding.UTF8.GetBytes(p.X.ToString());
    private static ComponentDeserializer<Position> DeserializePosition => bytes => new Position { X = float.Parse(Encoding.UTF8.GetString(bytes)) };

    [Fact]
    public void Register_ThenTryGetByDiscriminator_FindsItByTheDiscriminatorString()
    {
        var registry = new SerializerRegistry();

        registry.Register("Position", SerializePosition, DeserializePosition);

        registry.TryGetByDiscriminator("Position", out var registered).Should().BeTrue();
        registered.Discriminator.Should().Be("Position");
    }

    [Fact]
    public void Register_ThenTryGetByTypeIndex_FindsTheSameEntry()
    {
        var registry = new SerializerRegistry();

        registry.Register("Position", SerializePosition, DeserializePosition);

        registry.TryGetByTypeIndex(Wyrd.Ecs.Internal.TypeIndex<Position>.Value, out var registered).Should().BeTrue();
        registered.Discriminator.Should().Be("Position");
    }

    [Fact]
    public void TryGetByDiscriminator_ForAnUnregisteredDiscriminator_ReturnsFalse()
    {
        var registry = new SerializerRegistry();

        registry.TryGetByDiscriminator("Nonexistent", out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetByTypeIndex_ForAnUnregisteredType_ReturnsFalse()
    {
        var registry = new SerializerRegistry();

        registry.TryGetByTypeIndex(Wyrd.Ecs.Internal.TypeIndex<Velocity>.Value, out _).Should().BeFalse();
    }

    [Fact]
    public void Register_WithADuplicateDiscriminator_Throws()
    {
        var registry = new SerializerRegistry();
        registry.Register("Position", SerializePosition, DeserializePosition);

        var act = () => registry.Register("Position", (ComponentSerializer<Velocity>)(v => Encoding.UTF8.GetBytes(v.X.ToString())), bytes => new Velocity { X = float.Parse(Encoding.UTF8.GetString(bytes)) });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SerializeRow_RoundTripsThroughTheRegisteredDelegatesByDiscriminator()
    {
        var registry = new SerializerRegistry();
        registry.Register("Position", SerializePosition, DeserializePosition);
        registry.TryGetByDiscriminator("Position", out var registered);

        var items = new Position[] { new() { X = 1f }, new() { X = 42f }, new() { X = 3f } };
        var data = registered.SerializeRow(items, 1);

        registry.TryGetByDiscriminator("Position", out var forDeserialize);
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();
        forDeserialize.DeserializeInto(world, entity, data);

        world.GetComponent<Position>(entity).X.Should().Be(42f);
    }
}
