using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests;

public class CodecRegistryTagTests
{
    private struct Enemy : ITag { }
    private struct OtherTag : ITag { }
    private struct Position : IComponent { }

    [Fact]
    public void RegisterTag_ThenLookupByTypeIndexAndDiscriminator_BothResolve()
    {
        var registry = new CodecRegistry();
        registry.RegisterTag<Enemy>("Enemy");

        registry.TryGetTagByTypeIndex(TypeIndex<Enemy>.Value, out var byIndex).Should().BeTrue();
        byIndex.Discriminator.Should().Be("Enemy");

        registry.TryGetTagByDiscriminator("Enemy", out var byName).Should().BeTrue();
        byName.Discriminator.Should().Be("Enemy");
    }

    [Fact]
    public void RegisterTag_DuplicateDiscriminator_Throws()
    {
        var registry = new CodecRegistry();
        registry.RegisterTag<Enemy>("Enemy");

        var act = () => registry.RegisterTag<OtherTag>("Enemy");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterTag_MayCollideWithAComponentDiscriminator()
    {
        var registry = new CodecRegistry();
        registry.Register<Position>("Shared", p => [], bytes => default);

        var act = () => registry.RegisterTag<Enemy>("Shared");

        act.Should().NotThrow();
    }

    [Fact]
    public void RegisterTag_ViaAlias_OldDiscriminatorResolves()
    {
        var registry = new CodecRegistry();
        registry.RegisterTag<Enemy>("Enemy");
        registry.RegisterAlias("Old.Enemy", "Enemy");

        registry.TryGetTagByDiscriminator("Old.Enemy", out var binder).Should().BeTrue();
        binder.Discriminator.Should().Be("Enemy");
    }
}
