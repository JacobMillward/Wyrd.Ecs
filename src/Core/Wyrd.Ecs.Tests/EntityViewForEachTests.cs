using System.Collections.Concurrent;

namespace Wyrd.Ecs.Tests;

struct EntityViewPosition : IComponent { public float X; }
struct EntityViewMarker : ITag;

public class EntityViewForEachTests
{
    [Fact]
    public void ForEach_YieldsTheMatchedEntity()
    {
        var world = new World();
        Entity e1 = world.Commands.CreateEntity();
        world.Commands.AddComponent(e1, new EntityViewPosition { X = 1f });
        Entity e2 = world.Commands.CreateEntity();
        world.Commands.AddComponent(e2, new EntityViewPosition { X = 2f });
        world.ApplyCommands();

        var seen = new Dictionary<Entity, float>();
        world.Query().With<EntityViewPosition>()
            .ForEach((EntityView entity, in EntityViewPosition p) => seen[entity.Entity] = p.X);

        seen.Should().BeEquivalentTo(new Dictionary<Entity, float> { [e1] = 1f, [e2] = 2f });
    }

    [Fact]
    public void ForEach_AcrossMultipleArchetypes_EachEntityPairedWithItsOwnData()
    {
        var world = new World();
        Entity plain = world.Commands.CreateEntity();
        world.Commands.AddComponent(plain, new EntityViewPosition { X = 1f });

        Entity tagged = world.Commands.CreateEntity();
        world.Commands.AddComponent(tagged, new EntityViewPosition { X = 2f });
        world.Commands.AddTag<EntityViewMarker>(tagged);
        world.ApplyCommands();

        var seen = new Dictionary<Entity, float>();
        world.Query().With<EntityViewPosition>()
            .ForEach((EntityView entity, in EntityViewPosition p) => seen[entity.Entity] = p.X);

        seen.Should().BeEquivalentTo(new Dictionary<Entity, float> { [plain] = 1f, [tagged] = 2f });
    }

    [Fact]
    public void PredicateForEach_YieldsTheMatchedEntity_AndStopsWhenFalse()
    {
        var world = new World();
        Entity first = world.Commands.CreateEntity();
        world.Commands.AddComponent(first, new EntityViewPosition { X = 1f });
        Entity second = world.Commands.CreateEntity();
        world.Commands.AddComponent(second, new EntityViewPosition { X = 2f });
        world.ApplyCommands();

        var visited = new List<Entity>();
        world.Query().With<EntityViewPosition>().ForEach((EntityView entity, in EntityViewPosition p) =>
        {
            visited.Add(entity.Entity);
            return false;
        });

        visited.Should().ContainSingle().Which.Should().BeOneOf(first, second);
    }

    [Fact]
    public void ParallelForEach_AcrossMultipleSlicesOfOneArchetype_EachEntityPairedWithItsOwnData()
    {
        var world = new World();
        var entityCount = ArchetypeChunks.ParallelSliceRows * 2 + 6; // forces at least 3 parallel slices
        var expected = new ConcurrentDictionary<Entity, float>();
        for (var i = 0; i < entityCount; i++)
        {
            Entity entity = world.Commands.CreateEntity();
            world.Commands.AddComponent(entity, new EntityViewPosition { X = i });
            expected[entity] = i;
        }
        world.ApplyCommands();

        var observed = new ConcurrentDictionary<Entity, float>();
        world.Query().With<EntityViewPosition>()
            .ParallelForEach((EntityView entity, in EntityViewPosition p) => observed[entity.Entity] = p.X);

        observed.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void FilterOnlyQuery_SoloEntityViewLambda_VisitsEveryMatchingEntity()
    {
        var world = new World();
        Entity tagged = world.Commands.CreateEntity();
        world.Commands.AddTag<EntityViewMarker>(tagged);
        world.Commands.CreateEntity(); // untagged, must not match
        world.ApplyCommands();

        var visited = new List<Entity>();
        world.Query().Has<EntityViewMarker>().ForEach((EntityView entity) => visited.Add(entity.Entity));

        visited.Should().Equal(tagged);
    }

    [Fact]
    public void UniformOverload_WithEntityView_PassesStateAndEntityAndData()
    {
        var world = new World();
        Entity e = world.Commands.CreateEntity();
        world.Commands.AddComponent(e, new EntityViewPosition { X = 5f });
        world.ApplyCommands();

        var matches = new List<(Entity Entity, float X)>();
        world.Query().With<EntityViewPosition>()
            .ForEach(matches, (in List<(Entity Entity, float X)> m, EntityView entity, in EntityViewPosition p) => m.Add((entity.Entity, p.X)));

        matches.Should().Equal((e, 5f));
    }

    [Fact]
    public void ForEach_EntityViewAddTag_AppliesAfterCommandsFlush()
    {
        var world = new World();
        Entity e = world.Commands.CreateEntity();
        world.Commands.AddComponent(e, new EntityViewPosition { X = 1f });
        world.ApplyCommands();

        world.Query().With<EntityViewPosition>()
            .ForEach((EntityView entity, in EntityViewPosition p) => entity.AddTag<EntityViewMarker>());
        world.ApplyCommands();

        world.HasTag<EntityViewMarker>(e).Should().BeTrue();
    }

    [Fact]
    public void ParallelForEach_EntityViewDestroyEntity_AllQueuedDestroysApplyCleanly()
    {
        var world = new World();
        var entityCount = ArchetypeChunks.ParallelSliceRows * 2 + 6; // even, so exactly half the indices are even
        var entities = new List<Entity>();
        for (var i = 0; i < entityCount; i++)
        {
            Entity entity = world.Commands.CreateEntity();
            world.Commands.AddComponent(entity, new EntityViewPosition { X = i });
            entities.Add(entity);
        }
        world.ApplyCommands();

        world.Query().With<EntityViewPosition>()
            .ParallelForEach((EntityView entity, in EntityViewPosition p) =>
            {
                if (p.X % 2 == 0) entity.DestroyEntity();
            });
        world.ApplyCommands();

        var remaining = entities.Count(e => world.IsAlive(e));
        remaining.Should().Be(entityCount / 2);
    }
}
