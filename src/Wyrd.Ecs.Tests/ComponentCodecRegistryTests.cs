using System.Text;

namespace Wyrd.Ecs.Tests;

public class ComponentCodecRegistryTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    private struct Velocity : IComponent
    {
        public float X;
    }

    private static ComponentEncoder<Position> SerializePosition => p => Encoding.UTF8.GetBytes(p.X.ToString());
    private static ComponentDecoder<Position> DeserializePosition => bytes => new Position { X = float.Parse(Encoding.UTF8.GetString(bytes)) };

    [Fact]
    public void Register_ThenTryGetByDiscriminator_FindsItByTheDiscriminatorString()
    {
        var registry = new ComponentCodecRegistry();

        registry.Register("Position", SerializePosition, DeserializePosition);

        registry.TryGetByDiscriminator("Position", out var registered).Should().BeTrue();
        registered.Discriminator.Should().Be("Position");
    }

    [Fact]
    public void Register_ThenTryGetByTypeIndex_FindsTheSameEntry()
    {
        var registry = new ComponentCodecRegistry();

        registry.Register("Position", SerializePosition, DeserializePosition);

        registry.TryGetByTypeIndex(Wyrd.Ecs.Internal.TypeIndex<Position>.Value, out var registered).Should().BeTrue();
        registered.Discriminator.Should().Be("Position");
    }

    [Fact]
    public void TryGetByDiscriminator_ForAnUnregisteredDiscriminator_ReturnsFalse()
    {
        var registry = new ComponentCodecRegistry();

        registry.TryGetByDiscriminator("Nonexistent", out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetByTypeIndex_ForAnUnregisteredType_ReturnsFalse()
    {
        var registry = new ComponentCodecRegistry();

        registry.TryGetByTypeIndex(Wyrd.Ecs.Internal.TypeIndex<Velocity>.Value, out _).Should().BeFalse();
    }

    [Fact]
    public void Register_WithADuplicateDiscriminator_Throws()
    {
        var registry = new ComponentCodecRegistry();
        registry.Register("Position", SerializePosition, DeserializePosition);

        var act = () => registry.Register("Position", (ComponentEncoder<Velocity>)(v => Encoding.UTF8.GetBytes(v.X.ToString())), bytes => new Velocity { X = float.Parse(Encoding.UTF8.GetString(bytes)) });

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_TheSameTypeTwiceUnderDifferentDiscriminators_Throws()
    {
        var registry = new ComponentCodecRegistry();
        registry.Register("Position", SerializePosition, DeserializePosition);

        var act = () => registry.Register("Position_V2", SerializePosition, DeserializePosition);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EncodeRow_RoundTripsThroughTheRegisteredDelegatesByDiscriminator()
    {
        var registry = new ComponentCodecRegistry();
        registry.Register("Position", SerializePosition, DeserializePosition);
        registry.TryGetByDiscriminator("Position", out var registered);

        var items = new Position[] { new() { X = 1f }, new() { X = 42f }, new() { X = 3f } };
        var data = registered.EncodeRow(items, 1);

        registry.TryGetByDiscriminator("Position", out var forDeserialize);
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();
        forDeserialize.DecodeInto(world, entity, data);
        world.ApplyCommands();

        world.GetComponent<Position>(entity).X.Should().Be(42f);
    }

    [Fact]
    public void Register_WithASchemaHash_MakesItAvailableOnTheRegisteredEntry()
    {
        var registry = new ComponentCodecRegistry();

        registry.Register("Position", SerializePosition, DeserializePosition, schemaHash: 12345u);

        registry.TryGetByDiscriminator("Position", out var registered).Should().BeTrue();
        registered.SchemaHash.Should().Be(12345u);
    }

    [Fact]
    public void Register_WithNoSchemaHash_LeavesItNull()
    {
        var registry = new ComponentCodecRegistry();

        registry.Register("Position", SerializePosition, DeserializePosition);

        registry.TryGetByDiscriminator("Position", out var registered).Should().BeTrue();
        registered.SchemaHash.Should().BeNull();
    }
}
