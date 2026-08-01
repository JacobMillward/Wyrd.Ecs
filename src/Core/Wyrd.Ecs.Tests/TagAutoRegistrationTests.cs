namespace Wyrd.Ecs.Tests;

public struct Enemy : ITag { }
public struct Projectile : ITag { }

public class TagAutoRegistrationTests
{
    [Fact]
    public void RegisterAll_RegistersEveryTagInThisProject()
    {
        var registry = new ComponentCodecRegistry();

        Wyrd.Ecs.Generated.TagAutoRegistration.RegisterAll(registry);

        registry.TryGetTagByDiscriminator(nameof(Enemy), out _).Should().BeTrue();
        registry.TryGetTagByDiscriminator(nameof(Projectile), out _).Should().BeTrue();
        registry.TryGetTagByDiscriminator(nameof(Other.EnemyMarker), out _).Should().BeTrue();
    }

    [Fact]
    public void RegisterAll_ResolvesTheRegisteredTypeIndexBackToItsDiscriminator()
    {
        var registry = new ComponentCodecRegistry();

        Wyrd.Ecs.Generated.TagAutoRegistration.RegisterAll(registry);

        registry.TryGetTagByTypeIndex(Wyrd.Ecs.Internal.TypeIndex<Enemy>.Value, out var discriminator).Should().BeTrue();
        discriminator.Should().Be(nameof(Enemy));
    }
}
