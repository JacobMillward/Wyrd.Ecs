namespace Wyrd.Ecs.Tests;

public class EnumerateRelationsTests
{
    private struct Likes : IRelation
    {
        public float Weight;
    }

    private struct Follows : IRelation;

    [Fact]
    public void EnumerateRelations_YieldsOneEncodedRelationPerEdge()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 2.5f });
        world.ApplyCommands();

        var registry = new ComponentCodecRegistry();
        registry.RegisterRelation<Likes>("likes",
            v => BitConverter.GetBytes(v.Weight),
            d => new Likes { Weight = BitConverter.ToSingle(d) },
            schemaHash: 9u);

        var relations = world.EnumerateRelations(registry).ToList();

        relations.Should().ContainSingle();
        relations[0].Source.Should().Be(a);
        relations[0].Target.Should().Be(b);
        relations[0].Discriminator.Should().Be("likes");
        relations[0].SchemaHash.Should().Be(9u);
        BitConverter.ToSingle(relations[0].Data).Should().Be(2.5f);
    }

    [Fact]
    public void EnumerateRelations_MultipleTargetsOnOneSource_YieldsOnePerTarget()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        Entity c = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.Commands.AddRelation(a, c, new Likes { Weight = 2f });
        world.ApplyCommands();

        var registry = new ComponentCodecRegistry();
        registry.RegisterRelation<Likes>("likes", v => BitConverter.GetBytes(v.Weight), d => new Likes { Weight = BitConverter.ToSingle(d) });

        var relations = world.EnumerateRelations(registry).ToList();

        relations.Should().HaveCount(2);
        relations.Should().ContainSingle(r => r.Target == b);
        relations.Should().ContainSingle(r => r.Target == c);
    }

    [Fact]
    public void EnumerateRelations_UnregisteredRelationType_IsSkipped()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.Commands.AddRelation<Follows>(a, b);
        world.ApplyCommands();

        var registry = new ComponentCodecRegistry();

        world.EnumerateRelations(registry).Should().BeEmpty();
    }

    [Fact]
    public void EnumerateRelations_NeverYieldsForRelationBacklinks()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        var registry = new ComponentCodecRegistry();
        registry.RegisterRelation<Likes>("likes", v => BitConverter.GetBytes(v.Weight), d => new Likes { Weight = BitConverter.ToSingle(d) });

        world.EnumerateRelations(registry).Should().ContainSingle();
    }
}
