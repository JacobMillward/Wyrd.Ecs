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

    private struct Enemy : ITag { }
    private struct Projectile : ITag { }

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
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();
        forDeserialize.DecodeInto(world, entity, data);
        world.ApplyCommands();

        world.GetComponent<Position>(entity).X.Should().Be(42f);
    }

    [Fact]
    public void All_OnAFreshRegistry_IsEmpty()
    {
        var registry = new ComponentCodecRegistry();

        registry.All.Should().BeEmpty();
    }

    [Fact]
    public void All_AfterRegisteringTwoTypes_ContainsBoth()
    {
        var registry = new ComponentCodecRegistry();
        registry.Register("Position", SerializePosition, DeserializePosition);
        registry.Register<Velocity>("Velocity",
            v => Encoding.UTF8.GetBytes(v.X.ToString()),
            bytes => new Velocity { X = float.Parse(Encoding.UTF8.GetString(bytes)) });

        registry.All.Select(c => c.Discriminator).Should().BeEquivalentTo(["Position", "Velocity"]);
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

    [Fact]
    public void Migrate_WithASingleRegisteredStep_TransformsTheBytesToTheCurrentSchema()
    {
        var registry = new ComponentCodecRegistry();
        registry.Register("Position", SerializePosition, DeserializePosition, schemaHash: 2u);
        registry.RegisterMigration("Position", fromSchemaHash: 1u, toSchemaHash: 2u, oldBytes => Encoding.UTF8.GetBytes("migrated"));

        var result = registry.Migrate("Position", fromSchemaHash: 1u, [1, 2, 3]);

        Encoding.UTF8.GetString(result).Should().Be("migrated");
    }

    [Fact]
    public void Migrate_WalkingTwoChainedSteps_ReachesTheCurrentSchema()
    {
        var registry = new ComponentCodecRegistry();
        registry.Register("Position", SerializePosition, DeserializePosition, schemaHash: 3u);
        registry.RegisterMigration("Position", fromSchemaHash: 1u, toSchemaHash: 2u, oldBytes => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(oldBytes) + "-step1"));
        registry.RegisterMigration("Position", fromSchemaHash: 2u, toSchemaHash: 3u, oldBytes => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(oldBytes) + "-step2"));

        var result = registry.Migrate("Position", fromSchemaHash: 1u, Encoding.UTF8.GetBytes("start"));

        Encoding.UTF8.GetString(result).Should().Be("start-step1-step2");
    }

    [Fact]
    public void Migrate_WithNoRegisteredStepFromTheGivenHash_ThrowsNamingTheDiscriminatorAndHash()
    {
        var registry = new ComponentCodecRegistry();
        registry.Register("Position", SerializePosition, DeserializePosition, schemaHash: 2u);

        var act = () => registry.Migrate("Position", fromSchemaHash: 1u, [1, 2, 3]);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Position*")
            .WithMessage("*1*");
    }

    [Fact]
    public void RegisterMigration_WithADuplicateFromHashForTheSameDiscriminator_Throws()
    {
        var registry = new ComponentCodecRegistry();
        registry.RegisterMigration("Position", fromSchemaHash: 1u, toSchemaHash: 2u, oldBytes => oldBytes);

        var act = () => registry.RegisterMigration("Position", fromSchemaHash: 1u, toSchemaHash: 3u, oldBytes => oldBytes);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Migrate_ForAnUnregisteredDiscriminator_Throws()
    {
        var registry = new ComponentCodecRegistry();

        var act = () => registry.Migrate("Nonexistent", fromSchemaHash: 1u, [1, 2, 3]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Migrate_WithAChainThatCyclesWithoutEverReachingTheCurrentSchema_ThrowsInsteadOfLoopingForever()
    {
        var registry = new ComponentCodecRegistry();
        registry.Register("Position", SerializePosition, DeserializePosition, schemaHash: 99u);
        registry.RegisterMigration("Position", fromSchemaHash: 1u, toSchemaHash: 2u, oldBytes => oldBytes);
        registry.RegisterMigration("Position", fromSchemaHash: 2u, toSchemaHash: 1u, oldBytes => oldBytes);

        var task = Task.Run(() => registry.Migrate("Position", fromSchemaHash: 1u, [1, 2, 3]));
        var act = async () => await task.WaitAsync(TimeSpan.FromSeconds(2));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Position*");
    }

    [Fact]
    public void RegisterTag_ThenTryGetTagByDiscriminator_FindsIt()
    {
        var registry = new ComponentCodecRegistry();

        registry.RegisterTag<Enemy>("Enemy");

        registry.TryGetTagByDiscriminator("Enemy", out var typeIndex).Should().BeTrue();
        typeIndex.Should().Be(Wyrd.Ecs.Internal.TypeIndex<Enemy>.Value);
    }

    [Fact]
    public void RegisterTag_ThenTryGetTagByTypeIndex_FindsTheSameDiscriminator()
    {
        var registry = new ComponentCodecRegistry();

        registry.RegisterTag<Enemy>("Enemy");

        registry.TryGetTagByTypeIndex(Wyrd.Ecs.Internal.TypeIndex<Enemy>.Value, out var discriminator).Should().BeTrue();
        discriminator.Should().Be("Enemy");
    }

    [Fact]
    public void TryGetTagByDiscriminator_ForAnUnregisteredDiscriminator_ReturnsFalse()
    {
        var registry = new ComponentCodecRegistry();

        registry.TryGetTagByDiscriminator("Nonexistent", out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetTagByTypeIndex_ForAnUnregisteredType_ReturnsFalse()
    {
        var registry = new ComponentCodecRegistry();

        registry.TryGetTagByTypeIndex(Wyrd.Ecs.Internal.TypeIndex<Projectile>.Value, out _).Should().BeFalse();
    }

    [Fact]
    public void RegisterTag_WithADuplicateDiscriminator_Throws()
    {
        var registry = new ComponentCodecRegistry();
        registry.RegisterTag<Enemy>("Enemy");

        var act = () => registry.RegisterTag<Projectile>("Enemy");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterTag_TheSameTypeTwiceUnderDifferentDiscriminators_Throws()
    {
        var registry = new ComponentCodecRegistry();
        registry.RegisterTag<Enemy>("Enemy");

        var act = () => registry.RegisterTag<Enemy>("Enemy_V2");

        act.Should().Throw<ArgumentException>();
    }
}
