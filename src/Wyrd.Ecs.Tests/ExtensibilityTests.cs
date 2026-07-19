using Wyrd.Ecs.Tests.Fixtures;

namespace Wyrd.Ecs.Tests;

public class ExtensibilityTests
{
    [Fact]
    public void NewComponentType_AddedAsOnlyAFile_IsStoredQueriedAndDirtyTracked()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new Wobble { Intensity = 5 });
        world.ApplyCommands();

        world.HasComponent<Wobble>(entity).Should().BeTrue();
        world.GetComponent<Wobble>(entity).Intensity.Should().Be(5);

        var seen = new List<int>();
        world.Query<Mut<Wobble>>(chunk =>
        {
            for (var i = 0; i < chunk.Length; i++)
                seen.Add(chunk[i].Intensity);
        });

        seen.Should().Equal(5);
    }
}
