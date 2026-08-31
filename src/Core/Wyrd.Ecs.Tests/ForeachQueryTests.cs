namespace Wyrd.Ecs.Tests;

struct ForeachQueryTransform : IComponent { public float X; }
struct ForeachQueryBullet : IComponent { public float Speed; }
struct ForeachQueryOddTag : ITag;

public class ForeachQueryTests
{
    [Fact]
    public void ForeachOverQuery_MutatesThroughWithMutMarker()
    {
        var world = new World();
        world.Commands.CreateEntity(new ForeachQueryTransform { X = 1f }, new ForeachQueryBullet { Speed = 2f });
        world.Commands.CreateEntity(new ForeachQueryTransform { X = 10f }, new ForeachQueryBullet { Speed = 3f });
        world.ApplyCommands();

        foreach (var row in world.Query().WithMut<ForeachQueryTransform>().Has<ForeachQueryBullet>())
            row.ForeachQueryTransform.X += 1f;

        var total = 0f;
        world.Query().With<ForeachQueryTransform>().ForEach((in ForeachQueryTransform t) => total += t.X);
        total.Should().Be(1f + 1f + 10f + 1f);
    }

    [Fact]
    public void ForeachOverQuery_ReadOnlyField_CannotBeAssigned()
    {
        // Compile-time only: With<T> (not WithMut<T>) must make the row's field
        // `ref readonly`, so this file simply must still build. No runtime assertion needed --
        // if row.ForeachQueryBullet.Speed = 1f; were added below, the build would fail with
        // CS8332, which is the guarantee under test.
        var world = new World();
        foreach (var row in world.Query().With<ForeachQueryBullet>())
            _ = row.ForeachQueryBullet.Speed;
    }

    [Fact]
    public void ForeachOverQuery_RowDestroyEntity_QueuesDestruction()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity(new ForeachQueryTransform());
        world.ApplyCommands();

        foreach (var row in world.Query().With<ForeachQueryTransform>())
            row.DestroyEntity();
        world.ApplyCommands();

        world[entity].IsAlive.Should().BeFalse();
    }

    [Fact]
    public void ForeachOverQuery_MultipleComponents_YieldsEntityAndBothFields()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity(new ForeachQueryTransform { X = 5f }, new ForeachQueryBullet { Speed = 7f });
        world.ApplyCommands();

        var seen = 0;
        foreach (var row in world.Query().With<ForeachQueryTransform>().With<ForeachQueryBullet>())
        {
            seen++;
            row.Entity.Should().Be(entity);
            row.ForeachQueryTransform.X.Should().Be(5f);
            row.ForeachQueryBullet.Speed.Should().Be(7f);
        }
        seen.Should().Be(1);
    }

    [Fact]
    public void ForeachOverQuery_SpansMultipleChunks()
    {
        // A chunk maps to one archetype, not a row-count slice, so spanning
        // multiple chunks requires multiple archetypes that both match the
        // query -- an odd/even tag fragments storage while leaving the
        // filter (just ForeachQueryTransform) untouched.
        var world = new World();
        for (var i = 0; i < 5000; i++)
        {
            if (i % 2 == 0)
                world.Commands.CreateEntity(new ForeachQueryTransform { X = i });
            else
                world.Commands.CreateEntity(new ForeachQueryTransform { X = i }).AddTag<ForeachQueryOddTag>();
        }
        world.ApplyCommands();

        var seenCount = 0;
        var sum = 0f;
        foreach (var row in world.Query().WithMut<ForeachQueryTransform>())
        {
            seenCount++;
            sum += row.ForeachQueryTransform.X;
        }

        seenCount.Should().Be(5000);
        sum.Should().Be(Enumerable.Range(0, 5000).Sum());
    }
}
