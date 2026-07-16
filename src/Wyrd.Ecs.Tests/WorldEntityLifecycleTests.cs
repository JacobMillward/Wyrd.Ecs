namespace Wyrd.Ecs.Tests;

public class WorldEntityLifecycleTests
{
    [Fact]
    public void CreateEntity_ReturnsANonNullEntity()
    {
        var world = new World();

        var entity = world.CreateEntity();

        entity.IsNull.Should().BeFalse();
    }

    [Fact]
    public void CreateEntity_IsAlive()
    {
        var world = new World();

        var entity = world.CreateEntity();

        world.IsAlive(entity).Should().BeTrue();
    }

    [Fact]
    public void CreateEntity_TwiceInARow_ReturnsDistinctEntities()
    {
        var world = new World();

        var a = world.CreateEntity();
        var b = world.CreateEntity();

        a.Should().NotBe(b);
    }

    [Fact]
    public void CreateEntity_AssignsAUniquePermanentId()
    {
        var world = new World();

        var a = world.CreateEntity();
        var b = world.CreateEntity();

        world.GetPermanentId(a).Should().NotBe(world.GetPermanentId(b));
    }

    [Fact]
    public void DestroyEntity_IsNoLongerAlive()
    {
        var world = new World();
        var entity = world.CreateEntity();

        world.DestroyEntity(entity);

        world.IsAlive(entity).Should().BeFalse();
    }

    [Fact]
    public void DestroyEntity_ReusedIdGetsANewGeneration_OldHandleStaysDead()
    {
        var world = new World();
        var first = world.CreateEntity();
        world.DestroyEntity(first);

        var second = world.CreateEntity();

        second.Id.Should().Be(first.Id);
        second.Generation.Should().NotBe(first.Generation);
        world.IsAlive(first).Should().BeFalse();
        world.IsAlive(second).Should().BeTrue();
    }

    [Fact]
    public void DestroyEntity_MiddleOfMany_KeepsOthersAlive()
    {
        var world = new World();
        var a = world.CreateEntity();
        var b = world.CreateEntity();
        var c = world.CreateEntity();

        world.DestroyEntity(b);

        world.IsAlive(a).Should().BeTrue();
        world.IsAlive(c).Should().BeTrue();
        world.IsAlive(b).Should().BeFalse();
    }

    [Fact]
    public void IsAlive_NullEntity_IsFalse()
    {
        var world = new World();

        world.IsAlive(Entity.Null).Should().BeFalse();
    }

    [Fact]
    public void IsAlive_NeverCreatedEntity_IsFalse()
    {
        var world = new World();

        world.IsAlive(new Entity(9999, 0)).Should().BeFalse();
    }

    [Fact]
    public void DestroyEntity_NotAlive_Throws()
    {
        var world = new World();

        var act = () => world.DestroyEntity(new Entity(1, 0));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ManyCreatesAndDestroys_NeverProducesADuplicateLiveEntity()
    {
        var world = new World();
        var live = new HashSet<Entity>();

        var random = new Random(1234);
        for (var i = 0; i < 5_000; i++)
        {
            if (live.Count > 0 && random.Next(2) == 0)
            {
                var victim = live.First();
                world.DestroyEntity(victim);
                live.Remove(victim);
            }
            else
            {
                var created = world.CreateEntity();
                live.Add(created).Should().BeTrue();
            }
        }

        foreach (var entity in live)
            world.IsAlive(entity).Should().BeTrue();
    }
}
