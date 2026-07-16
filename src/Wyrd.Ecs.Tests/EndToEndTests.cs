namespace Wyrd.Ecs.Tests;

public class EndToEndTests
{
    private struct Energy : IComponent
    {
        public float Current;
        public float DrainPerSecond;
    }

    private struct Depleted : ITag;

    [Fact]
    public void CreateEntitiesAddComponentsQueryAndDestroy_WorksEndToEnd()
    {
        var world = new World();

        var entities = new Entity[10];
        for (var i = 0; i < entities.Length; i++)
        {
            entities[i] = world.CreateEntity();
            world.AddComponent<Energy>(entities[i]) = new Energy { Current = 100f, DrainPerSecond = 10f };
        }

        // Simulate one tick over the hidden-chunk tier — the intended "no chunk/
        // archetype vocabulary" onramp.
        foreach (var energy in world.Query<Mut<Energy>>())
            energy[0].Current -= energy[0].DrainPerSecond;

        foreach (var entity in entities)
            world.GetComponent<Energy>(entity).Current.Should().Be(90f);

        // Structural change: tag anything fully drained (none yet, but exercises the
        // path), then remove one entity's Energy component and destroy another.
        foreach (var entity in entities)
            if (world.GetComponent<Energy>(entity).Current <= 0f)
                world.AddTag<Depleted>(entity);

        world.RemoveComponent<Energy>(entities[0]);
        world.DestroyEntity(entities[1]);

        world.HasComponent<Energy>(entities[0]).Should().BeFalse();
        world.IsAlive(entities[1]).Should().BeFalse();
        world.GetComponent<Energy>(entities[2]).Current.Should().Be(90f);
    }
}
