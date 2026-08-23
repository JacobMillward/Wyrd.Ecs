namespace Wyrd.Ecs.Tests;

file struct SlicePos : IComponent { public float X; }

public class ArchetypeChunkSlicingTests
{
    private static World BuildWorld(int entityCount)
    {
        var world = new World();
        for (var i = 0; i < entityCount; i++)
            world.Commands.AddComponent(world.Commands.CreateEntity(), new SlicePos { X = i });
        world.ApplyCommands();
        return world;
    }

    [Fact]
    public void CollectParallelChunks_SubThresholdArchetype_YieldsOneWholeRangeChunk()
    {
        var world = BuildWorld(ArchetypeChunks.ParallelSliceRows / 2);

        var chunks = new List<ArchetypeChunk>();
        ArchetypeQuery.Empty.Has<SlicePos>().Resolve(world).CollectParallelChunks(chunks);

        chunks.Should().HaveCount(1);
        chunks[0].Count.Should().Be(ArchetypeChunks.ParallelSliceRows / 2);
    }

    [Fact]
    public void SequentialResolution_KeepsWholeArchetypeRange_RegardlessOfSize()
    {
        var total = ArchetypeChunks.ParallelSliceRows * 2 + 123;
        var world = BuildWorld(total);

        var chunks = ArchetypeQuery.Empty.Has<SlicePos>().Resolve(world);

        chunks.Count.Should().Be(1);
        var chunk = chunks[0];
        chunk.Count.Should().Be(total);
        chunk.Entities.Length.Should().Be(total);
    }

    [Fact]
    public void CollectParallelChunks_SlicedRangesPartitionRowsExactlyOnce()
    {
        var total = ArchetypeChunks.ParallelSliceRows * 2 + 123;
        var world = BuildWorld(total);

        var chunks = new List<ArchetypeChunk>();
        ArchetypeQuery.Empty.Has<SlicePos>().Resolve(world).CollectParallelChunks(chunks);

        // Interior slices are fixed size; the last carries the remainder.
        for (var i = 0; i < chunks.Count - 1; i++)
            chunks[i].Count.Should().Be(ArchetypeChunks.ParallelSliceRows);
        chunks[^1].Count.Should().Be(123);
        chunks.Count.Should().Be(3);

        var seen = new HashSet<Entity>();
        foreach (var chunk in chunks)
        {
            chunk.Entities.Length.Should().Be(chunk.Count);
            foreach (var entity in chunk.Entities)
                seen.Add(entity).Should().BeTrue("each row must appear in exactly one slice");
        }
        seen.Should().HaveCount(total);
    }

    [Fact]
    public void Access_InsideSlices_AddressesRowsRelativeToSliceOffset()
    {
        var total = ArchetypeChunks.ParallelSliceRows * 2 + 123;
        var world = BuildWorld(total);

        var slices = new List<ArchetypeChunk>();
        ArchetypeQuery.Empty.Has<SlicePos>().Resolve(world).CollectParallelChunks(slices);

        // Write through slice-relative indices only: first and last row of each slice.
        foreach (var slice in slices)
        {
            slice.Access<Mut<SlicePos>>()[0].X = -1f;
            slice.Access<Mut<SlicePos>>()[slice.Count - 1] = new SlicePos { X = -2f };
        }

        // Read back over the whole archetype: exactly the slice boundary rows were written,
        // proving no slice addressed another slice's storage rows.
        var row = 0;
        foreach (var chunk in ArchetypeQuery.Empty.Has<SlicePos>().Resolve(world))
        {
            var positions = chunk.Access<Ref<SlicePos>>();
            for (var i = 0; i < positions.Length; i++, row++)
            {
                var isSliceBoundary =
                    row % ArchetypeChunks.ParallelSliceRows == 0 ||
                    row % ArchetypeChunks.ParallelSliceRows == ArchetypeChunks.ParallelSliceRows - 1 ||
                    row == total - 1;
                if (isSliceBoundary)
                {
                    (positions[i].X < 0).Should().BeTrue($"row {row} is a slice boundary row");
                }
                else
                {
                    positions[i].X.Should().Be(row, $"untouched row {row} keeps its seeded value");
                }
            }
        }
        row.Should().Be(total);
    }

    [Fact]
    public void CollectParallelChunks_SkipsEmptyArchetypes()
    {
        var world = BuildWorld(8);
        foreach (var entity in ArchetypeQuery.Empty.Has<SlicePos>().Resolve(world)[0].Entities)
            world.Commands.DestroyEntity(entity);
        world.ApplyCommands();

        // The archetype still matches but holds zero rows, like the enumerator's skip.
        var resolved = ArchetypeQuery.Empty.Has<SlicePos>().Resolve(world);
        resolved.Count.Should().BeGreaterThanOrEqualTo(1);

        var chunks = new List<ArchetypeChunk>();
        resolved.CollectParallelChunks(chunks);

        chunks.Should().BeEmpty();
    }
}
