namespace Wyrd.Ecs.Tests;

public class EndToEndTests
{
    internal struct Energy : IComponent
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
            entities[i] = world.Commands.CreateEntity(new Energy { Current = 100f, DrainPerSecond = 10f });
        world.ApplyCommands();

        // Simulate one tick, reading and writing Energy directly through the chunk
        // tier — the "no chunk/archetype vocabulary needed" ergonomic tier this
        // comment used to reference (QueryRow's per-row Get<T>()) no longer exists;
        // see the design's "Why .Enumerate() was dropped entirely".
        foreach (var chunk in ArchetypeQuery.Empty.Access<Mut<Energy>>().Resolve(world))
        {
            var energy = chunk.Access<Mut<Energy>>();
            for (var i = 0; i < chunk.Count; i++)
                energy[i].Current -= energy[i].DrainPerSecond;
        }

        foreach (var entity in entities)
            world.GetComponent<Energy>(entity).Current.Should().Be(90f);

        // Structural change: tag anything fully drained (none yet, but exercises the
        // path), then remove one entity's Energy component and destroy another.
        foreach (var entity in entities)
            if (world.GetComponent<Energy>(entity).Current <= 0f)
                world.Commands.AddTag<Depleted>(entity);

        world.Commands.RemoveComponent<Energy>(entities[0]);
        world.Commands.DestroyEntity(entities[1]);
        world.ApplyCommands();

        world.HasComponent<Energy>(entities[0]).Should().BeFalse();
        world.IsAlive(entities[1]).Should().BeFalse();
        world.GetComponent<Energy>(entities[2]).Current.Should().Be(90f);
    }
}
