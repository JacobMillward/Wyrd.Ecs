using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests;

public struct Enemy : ITag { }
public struct Projectile : ITag { }

public class DebugNamesIntegrationTests
{
    [Fact]
    public void EveryTagInThisProject_IsRegisteredAutomatically_NoCallNeeded()
    {
        // No RegisterAll call anywhere in this test: the generated module initializer
        // already ran at assembly load. This is the behavior the zero-setup design exists
        // to prove.
        DebugNameRegistry.TryGetName(TypeIndex<Enemy>.Value, out var enemyName).Should().BeTrue();
        enemyName.Should().Be(nameof(Enemy));

        DebugNameRegistry.TryGetName(TypeIndex<Projectile>.Value, out var projectileName).Should().BeTrue();
        projectileName.Should().Be(nameof(Projectile));

        DebugNameRegistry.TryGetName(TypeIndex<Other.EnemyMarker>.Value, out var otherName).Should().BeTrue();
        otherName.Should().Be(nameof(Other.EnemyMarker));
    }
}
