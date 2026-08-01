namespace Wyrd.Ecs.Tests;

public class EntityTemplateTests
{
    // Matches the existing per-test-class nested component convention (see e.g.
    // BatchEntityCreationTests.cs) rather than a shared cross-file component type.
    private struct Position : IComponent
    {
        public float X;
        public float Y;
    }

    private struct Health : IComponent
    {
        public int Value;
    }

    [Fact]
    public void AddComponent_AccumulatesSignatureBits()
    {
        var template = new EntityTemplate()
            .AddComponent(new Position { X = 1, Y = 2 })
            .AddComponent(new Health { Value = 100 });

        template.Signature.Contains(Wyrd.Ecs.Internal.TypeIndex<Position>.Value).Should().BeTrue();
        template.Signature.Contains(Wyrd.Ecs.Internal.TypeIndex<Health>.Value).Should().BeTrue();
    }

    [Fact]
    public void AddComponent_CalledTwiceForSameType_LastValueWins()
    {
        var template = new EntityTemplate()
            .AddComponent(new Health { Value = 1 })
            .AddComponent(new Health { Value = 2 });

        template.Setters.Should().HaveCount(1);
    }
}
