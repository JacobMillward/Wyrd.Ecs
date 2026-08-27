using Wyrd.Ecs;

namespace Wyrd.Ecs.PackagingTests;

public struct Score : IComponent { public int Value; }

public class AccessVariantCollisionTests
{
    [Fact]
    public void WriteThenRead_SameShape_DifferentAccess_ViaPackagedReference()
    {
        var world = new World();
        world.Commands.CreateEntity(new Score { Value = 5 });
        world.ApplyCommands();
        world.AdvanceTick();

        world.Query().With<Score>().ForEach(0, (in int _, ref Score s) => { s.Value += 10; });

        var observed = 0;
        world.Query().With<Score>().ForEach(0, (in int _, in Score s) => { observed = s.Value; });

        observed.Should().Be(15);
    }
}
