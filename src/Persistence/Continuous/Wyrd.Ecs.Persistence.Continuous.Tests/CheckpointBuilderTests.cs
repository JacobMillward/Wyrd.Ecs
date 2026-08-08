namespace Wyrd.Ecs.Persistence.Continuous.Tests;

public class CheckpointBuilderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"wyrd-continuous-checkpointbuilder-{Guid.NewGuid():N}");
    private IPersistenceStore CheckpointStore => new FileStore(Path.Combine(_directory, "world.checkpoint"));
    private IWalStore WalStore => new FileWalStore(Path.Combine(_directory, "world"));

    public CheckpointBuilderTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private static void WriteSegment(IWalStore walStore, int startTick, params (WalRecordKind Kind, int Tick, EntityId EntityId, string Discriminator, uint? SchemaHash, byte[] Payload)[] records)
    {
        using var stream = walStore.OpenSegmentAppend(startTick);
        Internal.WalSegmentIO.WriteHeader(stream);
        foreach (var record in records)
            Internal.WalSegmentIO.WriteRecord(stream, record.Kind, record.Tick, record.EntityId, record.Discriminator, record.SchemaHash, record.Payload);
    }

    private static void WriteRelationSegment(IWalStore walStore, int startTick, params (WalRecordKind Kind, int Tick, EntityId SourceId, EntityId TargetId, string Discriminator, uint? SchemaHash, byte[] Payload)[] records)
    {
        using var stream = walStore.OpenSegmentAppend(startTick);
        Internal.WalSegmentIO.WriteHeader(stream);
        foreach (var record in records)
            Internal.WalSegmentIO.WriteRelationRecord(stream, record.Kind, record.Tick, record.SourceId, record.TargetId, record.Discriminator, record.SchemaHash, record.Payload);
    }

    [Fact]
    public void Build_WithNoPriorCheckpoint_CreatesOneFromWalRecordsAlone()
    {
        var checkpointStore = CheckpointStore;
        var walStore = WalStore;
        var entity = EntityId.NewId();
        WriteSegment(walStore, startTick: 1, (WalRecordKind.ComponentChanged, 1, entity, "Position", 42u, [1, 2, 3]));

        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 1);

        var (tick, entries, _, _) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        tick.Should().Be(1);
        entries.Should().ContainKey((entity, "Position"));
        entries[(entity, "Position")].SchemaHash.Should().Be(42u);
        entries[(entity, "Position")].Payload.Should().Equal(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void Build_OverwritesAPriorCheckpointEntryWithANewerWalRecord()
    {
        var checkpointStore = CheckpointStore;
        var walStore = WalStore;
        var entity = EntityId.NewId();
        WriteSegment(walStore, startTick: 1, (WalRecordKind.ComponentChanged, 1, entity, "Position", null, [1]));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 1);
        walStore.DeleteSegment(1);

        WriteSegment(walStore, startTick: 2, (WalRecordKind.ComponentChanged, 2, entity, "Position", null, [9, 9]));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 2);

        var (tick, entries, _, _) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        tick.Should().Be(2);
        entries[(entity, "Position")].Payload.Should().Equal(new byte[] { 9, 9 });
    }

    [Fact]
    public void Build_PreservesAPriorCheckpointEntryNoWalRecordTouches()
    {
        var checkpointStore = CheckpointStore;
        var walStore = WalStore;
        var untouched = EntityId.NewId();
        var touched = EntityId.NewId();
        WriteSegment(walStore, startTick: 1,
            (WalRecordKind.ComponentChanged, 1, untouched, "Position", null, [1]),
            (WalRecordKind.ComponentChanged, 1, touched, "Velocity", null, [2]));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 1);
        walStore.DeleteSegment(1);

        WriteSegment(walStore, startTick: 2, (WalRecordKind.ComponentChanged, 2, touched, "Velocity", null, [9]));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 2);

        var (_, entries, _, _) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        entries.Should().ContainKey((untouched, "Position"));
        entries[(untouched, "Position")].Payload.Should().Equal(new byte[] { 1 });
    }

    [Fact]
    public void Build_ComponentRemoved_DeletesJustThatComponentFromTheCheckpoint()
    {
        var checkpointStore = CheckpointStore;
        var walStore = WalStore;
        var entity = EntityId.NewId();
        WriteSegment(walStore, startTick: 1,
            (WalRecordKind.ComponentChanged, 1, entity, "Position", null, [1]),
            (WalRecordKind.ComponentChanged, 1, entity, "Velocity", null, [2]));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 1);
        walStore.DeleteSegment(1);

        WriteSegment(walStore, startTick: 2, (WalRecordKind.ComponentRemoved, 2, entity, "Velocity", null, []));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 2);

        var (_, entries, _, _) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        entries.Should().ContainKey((entity, "Position"));
        entries.Should().NotContainKey((entity, "Velocity"));
    }

    [Fact]
    public void Build_EntityDestroyed_DeletesEveryComponentOfThatEntity()
    {
        var checkpointStore = CheckpointStore;
        var walStore = WalStore;
        var destroyed = EntityId.NewId();
        var survivor = EntityId.NewId();
        WriteSegment(walStore, startTick: 1,
            (WalRecordKind.ComponentChanged, 1, destroyed, "Position", null, [1]),
            (WalRecordKind.ComponentChanged, 1, destroyed, "Velocity", null, [2]),
            (WalRecordKind.ComponentChanged, 1, survivor, "Position", null, [3]));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 1);
        walStore.DeleteSegment(1);

        WriteSegment(walStore, startTick: 2, (WalRecordKind.EntityDestroyed, 2, destroyed, "", null, []));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 2);

        var (_, entries, _, _) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        entries.Should().NotContainKey((destroyed, "Position"));
        entries.Should().NotContainKey((destroyed, "Velocity"));
        entries.Should().ContainKey((survivor, "Position"));
    }

    [Fact]
    public void Build_EntityCreated_ContributesNoDataOnItsOwn()
    {
        var checkpointStore = CheckpointStore;
        var walStore = WalStore;
        var entity = EntityId.NewId();
        WriteSegment(walStore, startTick: 1, (WalRecordKind.EntityCreated, 1, entity, "", null, []));

        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 1);

        var (tick, entries, _, _) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        tick.Should().Be(1);
        entries.Should().BeEmpty();
    }

    [Fact]
    public void Build_IgnoresWalRecordsAtOrBeforeThePriorCheckpointsTick()
    {
        var checkpointStore = CheckpointStore;
        var walStore = WalStore;
        var entity = EntityId.NewId();
        WriteSegment(walStore, startTick: 1, (WalRecordKind.ComponentChanged, 1, entity, "Position", null, [1]));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 1);
        walStore.DeleteSegment(1);

        // A record at the already-covered tick, mixed into a new segment alongside a genuinely new one.
        WriteSegment(walStore, startTick: 1,
            (WalRecordKind.ComponentChanged, 1, entity, "Position", null, [255]), // stale, tick <= priorTick
            (WalRecordKind.ComponentChanged, 2, entity, "Position", null, [9]));  // genuinely new

        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 2);

        var (_, entries, _, _) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        entries[(entity, "Position")].Payload.Should().Equal(new byte[] { 9 });
    }

    [Fact]
    public void Build_IgnoresWalRecordsAfterTheTargetTick()
    {
        var checkpointStore = CheckpointStore;
        var walStore = WalStore;
        var entity = EntityId.NewId();
        WriteSegment(walStore, startTick: 1,
            (WalRecordKind.ComponentChanged, 1, entity, "Position", null, [1]),
            (WalRecordKind.ComponentChanged, 2, entity, "Position", null, [99])); // beyond targetTick

        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 1);

        var (tick, entries, _, _) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        tick.Should().Be(1);
        entries[(entity, "Position")].Payload.Should().Equal(new byte[] { 1 });
    }

    [Fact]
    public void Build_ReadsMultipleSegmentsInStartTickOrder()
    {
        var checkpointStore = CheckpointStore;
        var walStore = WalStore;
        var entity = EntityId.NewId();
        WriteSegment(walStore, startTick: 1, (WalRecordKind.ComponentChanged, 1, entity, "Position", null, [1]));
        WriteSegment(walStore, startTick: 2, (WalRecordKind.ComponentChanged, 2, entity, "Position", null, [2]));

        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 2);

        var (tick, entries, _, _) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        tick.Should().Be(2);
        entries[(entity, "Position")].Payload.Should().Equal(new byte[] { 2 });
    }

    [Fact]
    public void Build_TagAdded_SurvivesAMergeWithNoFurtherWalActivity()
    {
        var checkpointStore = CheckpointStore;
        var walStore = WalStore;
        var entity = EntityId.NewId();
        WriteSegment(walStore, startTick: 1, (WalRecordKind.TagAdded, 1, entity, "Enemy", null, []));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 1);
        walStore.DeleteSegment(1);

        WriteSegment(walStore, startTick: 2, (WalRecordKind.ComponentChanged, 2, EntityId.NewId(), "Position", null, [9]));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 2);

        var (_, _, _, tagEntries) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        tagEntries.Should().Contain((entity, "Enemy"));
    }

    [Fact]
    public void Build_TagAddedThenRemovedAcrossSegments_FinalCheckpointReflectsRemoval()
    {
        var checkpointStore = CheckpointStore;
        var walStore = WalStore;
        var entity = EntityId.NewId();
        WriteSegment(walStore, startTick: 1, (WalRecordKind.TagAdded, 1, entity, "Enemy", null, []));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 1);
        walStore.DeleteSegment(1);

        WriteSegment(walStore, startTick: 2, (WalRecordKind.TagRemoved, 2, entity, "Enemy", null, []));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 2);

        var (_, _, _, tagEntries) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        tagEntries.Should().NotContain((entity, "Enemy"));
    }

    [Fact]
    public void Build_EntityDestroyed_RemovesItsTags()
    {
        var checkpointStore = CheckpointStore;
        var walStore = WalStore;
        var destroyed = EntityId.NewId();
        WriteSegment(walStore, startTick: 1, (WalRecordKind.TagAdded, 1, destroyed, "Enemy", null, []));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 1);
        walStore.DeleteSegment(1);

        WriteSegment(walStore, startTick: 2, (WalRecordKind.EntityDestroyed, 2, destroyed, "", null, []));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 2);

        var (_, _, _, tagEntries) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        tagEntries.Should().NotContain((destroyed, "Enemy"));
    }

    private struct Likes : IRelation
    {
        public float Weight;
    }

    [Fact]
    public void Build_RelationLinked_SurvivesAMergeWithNoFurtherWalActivity()
    {
        var checkpointStore = CheckpointStore;
        var walStore = WalStore;
        var source = EntityId.NewId();
        var target = EntityId.NewId();
        WriteRelationSegment(walStore, startTick: 1, (WalRecordKind.RelationLinked, 1, source, target, "Likes", 42u, [1, 2, 3]));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 1);
        walStore.DeleteSegment(1);

        WriteSegment(walStore, startTick: 2, (WalRecordKind.ComponentChanged, 2, EntityId.NewId(), "Position", null, [9]));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 2);

        var (_, _, relationEntries, _) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        relationEntries.Should().ContainKey((source, target, "Likes"));
        relationEntries[(source, target, "Likes")].SchemaHash.Should().Be(42u);
        relationEntries[(source, target, "Likes")].Payload.Should().Equal(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void Build_RelationUnlinked_DeletesTheEntry()
    {
        var checkpointStore = CheckpointStore;
        var walStore = WalStore;
        var source = EntityId.NewId();
        var target = EntityId.NewId();
        WriteRelationSegment(walStore, startTick: 1, (WalRecordKind.RelationLinked, 1, source, target, "Likes", null, [1]));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 1);
        walStore.DeleteSegment(1);

        WriteRelationSegment(walStore, startTick: 2, (WalRecordKind.RelationUnlinked, 2, source, target, "Likes", null, []));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 2);

        var (_, _, relationEntries, _) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        relationEntries.Should().NotContainKey((source, target, "Likes"));
    }

    [Fact]
    public void Build_EntityDestroyed_RemovesEveryRelationEdgeWhereItWasTheSource()
    {
        var checkpointStore = CheckpointStore;
        var walStore = WalStore;
        var destroyed = EntityId.NewId();
        var otherTarget = EntityId.NewId();
        WriteRelationSegment(walStore, startTick: 1, (WalRecordKind.RelationLinked, 1, destroyed, otherTarget, "Likes", null, [1]));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 1);
        walStore.DeleteSegment(1);

        WriteSegment(walStore, startTick: 2, (WalRecordKind.EntityDestroyed, 2, destroyed, "", null, []));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 2);

        var (_, _, relationEntries, _) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        relationEntries.Should().NotContainKey((destroyed, otherTarget, "Likes"));
    }

    [Fact]
    public void Build_EntityDestroyed_RemovesEveryRelationEdgeWhereItWasTheTarget()
    {
        var checkpointStore = CheckpointStore;
        var walStore = WalStore;
        var otherSource = EntityId.NewId();
        var destroyed = EntityId.NewId();
        WriteRelationSegment(walStore, startTick: 1, (WalRecordKind.RelationLinked, 1, otherSource, destroyed, "Likes", null, [1]));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 1);
        walStore.DeleteSegment(1);

        WriteSegment(walStore, startTick: 2, (WalRecordKind.EntityDestroyed, 2, destroyed, "", null, []));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 2);

        var (_, _, relationEntries, _) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        relationEntries.Should().NotContainKey((otherSource, destroyed, "Likes"));
    }

    [Fact]
    public void Build_APriorCheckpointsOwnRelationRecord_SurvivesAMergeWithNoWalActivityTouchingIt()
    {
        // Regression: a checkpoint can already contain RelationEdge records written
        // directly by World.Save, not just ones merged in from WAL activity. ReadCheckpoint
        // must read that kind correctly, not misread it as a Component record.
        var checkpointStore = CheckpointStore;
        var registry = new CodecRegistry();
        registry.RegisterRelation<Likes>("Likes", v => BitConverter.GetBytes(v.Weight), d => new Likes { Weight = BitConverter.ToSingle(d) });
        var world = new World();
        world.CodecRegistry = registry;
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 7f });
        world.ApplyCommands();
        world.Save(checkpointStore);
        var sourceId = world.GetPermanentId(a);
        var targetId = world.GetPermanentId(b);

        var walStore = WalStore;
        WriteSegment(walStore, startTick: world.CurrentTick + 1, (WalRecordKind.ComponentChanged, world.CurrentTick + 1, EntityId.NewId(), "Unrelated", null, [1]));
        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: world.CurrentTick + 1);

        var (_, _, relationEntries, _) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        relationEntries.Should().ContainKey((sourceId, targetId, "Likes"));
        BitConverter.ToSingle(relationEntries[(sourceId, targetId, "Likes")].Payload).Should().Be(7f);
    }
}
