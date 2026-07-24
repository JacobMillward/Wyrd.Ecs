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

    [Fact]
    public void Build_WithNoPriorCheckpoint_CreatesOneFromWalRecordsAlone()
    {
        var checkpointStore = CheckpointStore;
        var walStore = WalStore;
        var entity = EntityId.NewId();
        WriteSegment(walStore, startTick: 1, (WalRecordKind.ComponentChanged, 1, entity, "Position", 42u, [1, 2, 3]));

        CheckpointBuilder.Build(checkpointStore, walStore, targetTick: 1);

        var (tick, entries) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
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

        var (tick, entries) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
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

        var (_, entries) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
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

        var (_, entries) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
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

        var (_, entries) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
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

        var (tick, entries) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
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

        var (_, entries) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
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

        var (tick, entries) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
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

        var (tick, entries) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        tick.Should().Be(2);
        entries[(entity, "Position")].Payload.Should().Equal(new byte[] { 2 });
    }
}
