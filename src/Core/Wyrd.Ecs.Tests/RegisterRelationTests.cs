namespace Wyrd.Ecs.Tests;

public class RegisterRelationTests
{
    private struct Likes : IRelation
    {
        public float Weight;
    }

    private struct Follows : IRelation;

    private struct Position : IComponent;

    [Fact]
    public void RegisterRelation_IsFindableByTypeIndexAndDiscriminator()
    {
        var registry = new ComponentCodecRegistry();

        registry.RegisterRelation<Likes>("likes", v => BitConverter.GetBytes(v.Weight), d => new Likes { Weight = BitConverter.ToSingle(d) });

        registry.TryGetRelationByTypeIndex(Wyrd.Ecs.Internal.TypeIndex<Likes>.Value, out var byType).Should().BeTrue();
        byType.Discriminator.Should().Be("likes");

        registry.TryGetRelationByDiscriminator("likes", out var byDiscriminator).Should().BeTrue();
        byDiscriminator.TypeIndex.Should().Be(Wyrd.Ecs.Internal.TypeIndex<Likes>.Value);
    }

    [Fact]
    public void RegisterRelation_EncodeThenDecode_RoundTrips()
    {
        var registry = new ComponentCodecRegistry();
        registry.RegisterRelation<Likes>("likes", v => BitConverter.GetBytes(v.Weight), d => new Likes { Weight = BitConverter.ToSingle(d) });
        registry.TryGetRelationByDiscriminator("likes", out var codec);

        var bytes = codec.EncodeValue(new Likes { Weight = 2.5f });

        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.ApplyCommands();

        codec.DecodeInto(world, a, b, bytes);
        world.ApplyCommands();

        world.Targets<Likes>(a)[b].Weight.Should().Be(2.5f);
    }

    [Fact]
    public void RegisterRelation_DuplicateDiscriminator_Throws()
    {
        var registry = new ComponentCodecRegistry();
        registry.RegisterRelation<Likes>("rel", _ => [], _ => default);

        var act = () => registry.RegisterRelation<Follows>("rel", _ => [], _ => default);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterRelation_SameTypeTwiceUnderDifferentDiscriminators_Throws()
    {
        var registry = new ComponentCodecRegistry();
        registry.RegisterRelation<Likes>("likes-a", _ => [], _ => default);

        var act = () => registry.RegisterRelation<Likes>("likes-b", _ => [], _ => default);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterRelation_DiscriminatorAlreadyUsedByAComponent_Throws()
    {
        var registry = new ComponentCodecRegistry();
        registry.Register<Position>("shared", _ => [], _ => default);

        var act = () => registry.RegisterRelation<Likes>("shared", _ => [], _ => default);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Register_DiscriminatorAlreadyUsedByARelation_Throws()
    {
        var registry = new ComponentCodecRegistry();
        registry.RegisterRelation<Likes>("shared", _ => [], _ => default);

        var act = () => registry.Register<Position>("shared", _ => [], _ => default);

        act.Should().Throw<ArgumentException>();
    }
}
