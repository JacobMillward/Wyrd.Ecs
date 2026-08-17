namespace Wyrd.Ecs.Persistence.Tests;

public class WorldPersistenceExtensionsTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"wyrd-persistence-worldextensions-{Guid.NewGuid():N}.bin");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void DefaultPersistenceStore_UnsetOnAFreshWorld_IsNull()
    {
        var world = new World();

        world.DefaultPersistenceStore.Should().BeNull();
    }

    [Fact]
    public void DefaultPersistenceStore_SetThenRead_ReturnsTheSameInstance()
    {
        var world = new World();
        var store = new FileStore(Path.GetTempFileName());

        world.DefaultPersistenceStore = store;

        world.DefaultPersistenceStore.Should().BeSameAs(store);
    }

    [Fact]
    public void DefaultPersistenceStore_IsIndependentPerWorldInstance()
    {
        var worldA = new World();
        var worldB = new World();
        var store = new FileStore(Path.GetTempFileName());

        worldA.DefaultPersistenceStore = store;

        worldB.DefaultPersistenceStore.Should().BeNull();
    }

    [Fact]
    public void DefaultPersistenceStore_SetThenAssignedNull_ClearsItBackToUnset()
    {
        var world = new World();
        var store = new FileStore(Path.GetTempFileName());
        world.DefaultPersistenceStore = store;

        world.DefaultPersistenceStore = null;

        world.DefaultPersistenceStore.Should().BeNull();
    }

    [Fact]
    public void CodecRegistry_UnsetOnAFreshWorld_IsNull()
    {
        var world = new World();

        world.CodecRegistry.Should().BeNull();
    }

    [Fact]
    public void CodecRegistry_SetThenRead_ReturnsTheSameInstance()
    {
        var world = new World();
        var registry = new CodecRegistry();

        world.CodecRegistry = registry;

        world.CodecRegistry.Should().BeSameAs(registry);
    }

    [Fact]
    public void CodecRegistry_IsIndependentPerWorldInstance()
    {
        var worldA = new World();
        var worldB = new World();
        var registry = new CodecRegistry();

        worldA.CodecRegistry = registry;

        worldB.CodecRegistry.Should().BeNull();
    }

    [Fact]
    public void CodecRegistry_SetThenAssignedNull_ClearsItBackToUnset()
    {
        var world = new World();
        var registry = new CodecRegistry();
        world.CodecRegistry = registry;

        world.CodecRegistry = null;

        world.CodecRegistry.Should().BeNull();
    }

    [Fact]
    public void SetCodecRegistry_AppliesOnceBuildRuns()
    {
        var registry = new CodecRegistry();
        var builder = new WorldBuilder().SetCodecRegistry(registry);

        var world = builder.Build();

        world.CodecRegistry.Should().BeSameAs(registry);
    }

    [Fact]
    public void SetPersistence_AppliesBothTheStoreAndRegistryOnceBuildRuns()
    {
        var store = new FileStore(_path);
        var registry = new CodecRegistry();
        var builder = new WorldBuilder().SetPersistence(store, registry);

        var world = builder.Build();

        world.DefaultPersistenceStore.Should().BeSameAs(store);
        world.CodecRegistry.Should().BeSameAs(registry);
    }

    private struct Position : IComponent
    {
        public float X;
    }

    private struct Velocity : IComponent
    {
        public float X;
    }

    private struct Enemy : ITag { }

    private static CodecRegistry BuildRegistry()
    {
        var registry = new CodecRegistry();
        registry.Register<Position>("Position",
            p => BitConverter.GetBytes(p.X),
            bytes => new Position { X = BitConverter.ToSingle(bytes) });
        registry.Register<Velocity>("Velocity",
            v => BitConverter.GetBytes(v.X),
            bytes => new Velocity { X = BitConverter.ToSingle(bytes) });
        return registry;
    }

    [Fact]
    public void Save_StampsTheCheckpointHeaderWithTheWorldsCurrentTick()
    {
        var source = new World();
        source.CodecRegistry = BuildRegistry();
        source.AdvanceTick();
        source.AdvanceTick();
        var store = new FileStore(_path);

        source.Save(store);

        using var stream = store.OpenCheckpointRead();
        var tick = Wyrd.Ecs.Persistence.Internal.CheckpointRecordIO.ReadHeader(stream);
        tick.Should().Be(source.CurrentTick);
    }

    [Fact]
    public void Save_ThenLoad_ReconstructsEquivalentEntities()
    {
        var registry = BuildRegistry();
        var source = new World();
        source.CodecRegistry = registry;
        Entity entity = source.Commands.CreateEntity(new Position { X = 1f });
        source.ApplyCommands();
        source.Commands.AddComponent(entity, new Velocity { X = 2f });
        source.ApplyCommands();
        var store = new FileStore(_path);

        source.Save(store);

        var target = new World();
        target.CodecRegistry = registry;
        target.Load(store);

        var found = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<Position>>().Access<Ref<Velocity>>().Resolve(target))
        {
            var positions = chunk.Access<Ref<Position>>();
            var velocities = chunk.Access<Ref<Velocity>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                positions[i].X.Should().Be(1f);
                velocities[i].X.Should().Be(2f);
                found = true;
            }
        }
        found.Should().BeTrue();
    }

    private struct Likes : IRelation
    {
        public float Weight;
    }

    private static CodecRegistry BuildRegistryWithRelation()
    {
        var registry = BuildRegistry();
        registry.RegisterRelation<Likes>("Likes",
            v => BitConverter.GetBytes(v.Weight),
            d => new Likes { Weight = BitConverter.ToSingle(d) });
        return registry;
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsARelationEdge()
    {
        var registry = BuildRegistryWithRelation();
        var source = new World();
        source.CodecRegistry = registry;
        Entity a = source.Commands.CreateEntity(new Position { X = 1f });
        Entity b = source.Commands.CreateEntity(new Position { X = 2f });
        source.ApplyCommands();
        source.Commands.AddRelation(a, b, new Likes { Weight = 3f });
        source.ApplyCommands();
        var store = new FileStore(_path);

        source.Save(store);

        var target = new World();
        target.CodecRegistry = registry;
        target.Load(store);

        Entity? loadedA = null;
        Entity? loadedB = null;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<Position>>().Resolve(target))
        {
            var positions = chunk.Access<Ref<Position>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (positions[i].X == 1f) loadedA = chunk.Entities[i];
                if (positions[i].X == 2f) loadedB = chunk.Entities[i];
            }
        }

        loadedA.Should().NotBeNull();
        loadedB.Should().NotBeNull();
        target.Targets<Likes>(loadedA!.Value).Should().ContainKey(loadedB!.Value)
            .WhoseValue.Weight.Should().Be(3f);
    }

    [Fact]
    public void Save_ThenLoad_DoesNotPersistRelationBacklinksDirectly_ItsRebuiltFromTheLinkRecord()
    {
        var registry = BuildRegistryWithRelation();
        var source = new World();
        source.CodecRegistry = registry;
        Entity a = source.Commands.CreateEntity();
        Entity b = source.Commands.CreateEntity();
        source.ApplyCommands();
        source.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        source.ApplyCommands();
        var store = new FileStore(_path);

        source.Save(store);

        var target = new World();
        target.CodecRegistry = registry;
        target.Load(store);

        var hasLinks = false;
        var hasBacklinks = false;
        foreach (var snapshot in target.EnumerateEntities(registry))
        {
            if (target.HasComponent<RelationLinks<Likes>>(snapshot.Entity)) hasLinks = true;
            if (target.Sources<Likes>(snapshot.Entity).Any()) hasBacklinks = true;
        }

        hasLinks.Should().BeTrue("the source side of the edge must exist after load");
        hasBacklinks.Should().BeTrue("AddRelation's apply-time op must have rebuilt the backlink as a side effect of replaying the link");
    }

    /// <summary>
    /// A relation record's target id may never appear as any other record's own entity
    /// id (here, because the file was hand-crafted and the id was never real).
    /// <c>Load</c> creates a fresh entity for it the same as any other first-seen id:
    /// not a corruption signal.
    /// </summary>
    [Fact]
    public void Save_ThenLoad_ARelationRecordsTargetNeverSeenElsewhere_StillGetsAFreshEntityAndTheEdge()
    {
        var registry = BuildRegistryWithRelation();
        var source = new World();
        source.CodecRegistry = registry;
        Entity a = source.Commands.CreateEntity();
        source.ApplyCommands();
        var store = new FileStore(_path);

        using (var stream = store.OpenCheckpointWrite())
        {
            Wyrd.Ecs.Persistence.Internal.CheckpointRecordIO.WriteHeader(stream, source.CurrentTick);
            var payload = BitConverter.GetBytes(1f);
            Wyrd.Ecs.Persistence.Internal.CheckpointRecordIO.WriteRelationRecord(
                stream, source.GetPermanentId(a), EntityId.NewId(), "Likes", null, payload);
        }

        var target = new World();
        target.CodecRegistry = registry;
        var act = () => target.Load(store);
        act.Should().NotThrow();

        Entity? loadedA = null;
        foreach (var snapshot in target.EnumerateEntities(registry))
            if (target.HasComponent<RelationLinks<Likes>>(snapshot.Entity)) loadedA = snapshot.Entity;

        loadedA.Should().NotBeNull();
        var likesTargets = target.Targets<Likes>(loadedA!.Value);
        likesTargets.Should().HaveCount(1);
        likesTargets.Values.Single().Weight.Should().Be(1f);
    }

    [Fact]
    public void Save_ThenLoad_PreservesMultipleEntitiesIndependently()
    {
        var registry = BuildRegistry();
        var source = new World();
        source.CodecRegistry = registry;
        source.Commands.CreateEntity(new Position { X = 10f });
        source.Commands.CreateEntity(new Position { X = 20f });
        source.ApplyCommands();
        var store = new FileStore(_path);

        source.Save(store);

        var target = new World();
        target.CodecRegistry = registry;
        target.Load(store);

        var values = new List<float>();
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<Position>>().Resolve(target))
        {
            var positions = chunk.Access<Ref<Position>>();
            for (var i = 0; i < chunk.Count; i++)
                values.Add(positions[i].X);
        }

        values.Should().BeEquivalentTo([10f, 20f]);
    }

    [Fact]
    public void Save_WithAPathArgument_ThenLoad_WithTheSamePathArgument_RoundTripsCorrectly()
    {
        var registry = BuildRegistry();
        var source = new World();
        source.CodecRegistry = registry;
        source.Commands.CreateEntity(new Position { X = 3f });
        source.ApplyCommands();

        source.Save(_path);

        var target = new World();
        target.CodecRegistry = registry;
        target.Load(_path);

        var found = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<Position>>().Resolve(target))
        {
            var positions = chunk.Access<Ref<Position>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                positions[i].X.Should().Be(3f);
                found = true;
            }
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void Load_ForADiscriminatorNotInTheLoadingRegistry_SkipsThatComponentWithoutError()
    {
        var saveRegistry = BuildRegistry();
        var source = new World();
        source.CodecRegistry = saveRegistry;
        source.Commands.CreateEntity(new Position { X = 1f });
        source.ApplyCommands();
        Entity entity = source.Commands.CreateEntity();
        source.ApplyCommands();
        source.Commands.AddComponent(entity, new Velocity { X = 2f });
        source.ApplyCommands();
        var store = new FileStore(_path);
        source.Save(store);

        var loadRegistry = new CodecRegistry();
        loadRegistry.Register<Position>("Position",
            p => BitConverter.GetBytes(p.X),
            bytes => new Position { X = BitConverter.ToSingle(bytes) });
        // Velocity deliberately not registered here.

        var target = new World();
        target.CodecRegistry = loadRegistry;
        var act = () => target.Load(store);

        act.Should().NotThrow();

        var positionCount = 0;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<Position>>().Resolve(target))
        {
            var positions = chunk.Access<Ref<Position>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                positions[i].X.Should().Be(1f);
                positionCount++;
            }
        }
        positionCount.Should().Be(1);
    }

    [Fact]
    public void Load_OnAnEmptyCheckpoint_LeavesTheWorldEmpty()
    {
        var registry = BuildRegistry();
        var source = new World();
        source.CodecRegistry = registry;
        var store = new FileStore(_path);
        source.Save(store);

        var target = new World();
        target.CodecRegistry = registry;
        target.Load(store);

        var count = 0;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<Position>>().Resolve(target)) count += chunk.Count;
        count.Should().Be(0);
    }

    [Fact]
    public void Save_WithNoStoreArgument_UsesTheWorldsDefaultPersistenceStore()
    {
        var registry = BuildRegistry();
        var source = new World();
        source.CodecRegistry = registry;
        source.Commands.CreateEntity(new Position { X = 7f });
        source.ApplyCommands();
        source.DefaultPersistenceStore = new FileStore(_path);

        source.Save();

        var target = new World();
        target.CodecRegistry = registry;
        target.DefaultPersistenceStore = new FileStore(_path);
        target.Load();

        var found = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<Position>>().Resolve(target))
        {
            var positions = chunk.Access<Ref<Position>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                positions[i].X.Should().Be(7f);
                found = true;
            }
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void Save_WithNoStoreArgumentAndNoDefaultConfigured_ThrowsAClearError()
    {
        var world = new World();
        world.CodecRegistry = BuildRegistry();

        var act = () => world.Save();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Load_WithNoStoreArgumentAndNoDefaultConfigured_ThrowsAClearError()
    {
        var world = new World();
        world.CodecRegistry = BuildRegistry();

        var act = () => world.Load();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Save_WithNoCodecRegistryConfigured_ThrowsAClearError()
    {
        var world = new World();
        world.DefaultPersistenceStore = new FileStore(_path);

        var act = () => world.Save();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Load_WithNoCodecRegistryConfigured_ThrowsAClearError()
    {
        var world = new World();
        world.DefaultPersistenceStore = new FileStore(_path);

        var act = () => world.Load();

        act.Should().Throw<InvalidOperationException>();
    }

    private struct PositionV1 : IComponent
    {
        public float X;
    }

    private struct PositionV2 : IComponent
    {
        public float X;
        public float Y;
    }

    private struct PositionV3 : IComponent
    {
        public float X;
        public float Y;
        public float Z;
    }

    private static byte[] EncodeV1(PositionV1 p) => BitConverter.GetBytes(p.X);
    private static PositionV1 DecodeV1(byte[] bytes) => new() { X = BitConverter.ToSingle(bytes) };

    private static byte[] EncodeV2(PositionV2 p)
    {
        var bytes = new byte[8];
        BitConverter.GetBytes(p.X).CopyTo(bytes, 0);
        BitConverter.GetBytes(p.Y).CopyTo(bytes, 4);
        return bytes;
    }

    private static PositionV2 DecodeV2(byte[] bytes) => new() { X = BitConverter.ToSingle(bytes, 0), Y = BitConverter.ToSingle(bytes, 4) };

    private static byte[] EncodeV3(PositionV3 p)
    {
        var bytes = new byte[12];
        BitConverter.GetBytes(p.X).CopyTo(bytes, 0);
        BitConverter.GetBytes(p.Y).CopyTo(bytes, 4);
        BitConverter.GetBytes(p.Z).CopyTo(bytes, 8);
        return bytes;
    }

    private static PositionV3 DecodeV3(byte[] bytes) => new() { X = BitConverter.ToSingle(bytes, 0), Y = BitConverter.ToSingle(bytes, 4), Z = BitConverter.ToSingle(bytes, 8) };

    [Fact]
    public void Save_ThenLoad_WithMatchingSchemaHashesOnBothSides_RoundTripsCorrectly()
    {
        var saveRegistry = new CodecRegistry();
        saveRegistry.Register<PositionV1>("Position", EncodeV1, DecodeV1, schemaHash: 100u);
        var source = new World();
        source.CodecRegistry = saveRegistry;
        source.Commands.CreateEntity(new PositionV1 { X = 5f });
        source.ApplyCommands();
        var store = new FileStore(_path);

        source.Save(store);

        var loadRegistry = new CodecRegistry();
        loadRegistry.Register<PositionV1>("Position", EncodeV1, DecodeV1, schemaHash: 100u);
        var target = new World();
        target.CodecRegistry = loadRegistry;
        target.Load(store);

        var found = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<PositionV1>>().Resolve(target))
        {
            var positions = chunk.Access<Ref<PositionV1>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                positions[i].X.Should().Be(5f);
                found = true;
            }
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void Load_WithAMismatchedSchemaHashAndNoRegisteredMigration_ThrowsNamingTheDiscriminatorAndHash()
    {
        var saveRegistry = new CodecRegistry();
        saveRegistry.Register<PositionV1>("Position", EncodeV1, DecodeV1, schemaHash: 100u);
        var source = new World();
        source.CodecRegistry = saveRegistry;
        source.Commands.CreateEntity(new PositionV1 { X = 5f });
        source.ApplyCommands();
        var store = new FileStore(_path);
        source.Save(store);

        var loadRegistry = new CodecRegistry();
        loadRegistry.Register<PositionV2>("Position", EncodeV2, DecodeV2, schemaHash: 200u);
        var target = new World();
        target.CodecRegistry = loadRegistry;

        var act = () => target.Load(store);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Position*")
            .WithMessage("*00000064*"); // 100 formatted as {hash:X8}, the format Migrate's error message uses
    }

    [Fact]
    public void Load_WithAMismatchedSchemaHashAndARegisteredMigration_AppliesItAndReconstructsCorrectly()
    {
        var saveRegistry = new CodecRegistry();
        saveRegistry.Register<PositionV1>("Position", EncodeV1, DecodeV1, schemaHash: 100u);
        var source = new World();
        source.CodecRegistry = saveRegistry;
        source.Commands.CreateEntity(new PositionV1 { X = 5f });
        source.ApplyCommands();
        var store = new FileStore(_path);
        source.Save(store);

        var loadRegistry = new CodecRegistry();
        loadRegistry.Register<PositionV2>("Position", EncodeV2, DecodeV2, schemaHash: 200u);
        loadRegistry.RegisterMigration("Position", fromSchemaHash: 100u, toSchemaHash: 200u,
            oldBytes => EncodeV2(new PositionV2 { X = DecodeV1(oldBytes).X, Y = 0f }));
        var target = new World();
        target.CodecRegistry = loadRegistry;

        target.Load(store);

        var found = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<PositionV2>>().Resolve(target))
        {
            var positions = chunk.Access<Ref<PositionV2>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                positions[i].X.Should().Be(5f);
                positions[i].Y.Should().Be(0f);
                found = true;
            }
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void Load_ChainingTwoRegisteredMigrationSteps_WalksBothToReachTheCurrentSchema()
    {
        var saveRegistry = new CodecRegistry();
        saveRegistry.Register<PositionV1>("Position", EncodeV1, DecodeV1, schemaHash: 100u);
        var source = new World();
        source.CodecRegistry = saveRegistry;
        source.Commands.CreateEntity(new PositionV1 { X = 5f });
        source.ApplyCommands();
        var store = new FileStore(_path);
        source.Save(store);

        var loadRegistry = new CodecRegistry();
        loadRegistry.Register<PositionV3>("Position", EncodeV3, DecodeV3, schemaHash: 300u);
        loadRegistry.RegisterMigration("Position", fromSchemaHash: 100u, toSchemaHash: 200u,
            oldBytes => EncodeV2(new PositionV2 { X = DecodeV1(oldBytes).X, Y = 0f }));
        loadRegistry.RegisterMigration("Position", fromSchemaHash: 200u, toSchemaHash: 300u,
            oldBytes => { var v2 = DecodeV2(oldBytes); return EncodeV3(new PositionV3 { X = v2.X, Y = v2.Y, Z = 0f }); });
        var target = new World();
        target.CodecRegistry = loadRegistry;

        target.Load(store);

        var found = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<PositionV3>>().Resolve(target))
        {
            var positions = chunk.Access<Ref<PositionV3>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                positions[i].X.Should().Be(5f);
                positions[i].Y.Should().Be(0f);
                positions[i].Z.Should().Be(0f);
                found = true;
            }
        }
        found.Should().BeTrue();
    }

    private struct LikesV1 : IRelation
    {
        public float Weight;
    }

    private struct LikesV2 : IRelation
    {
        public float Weight;
        public bool Mutual;
    }

    [Fact]
    public void Load_WithAMismatchedSchemaHashOnARelation_AppliesTheRegisteredMigrationAndReconstructsCorrectly()
    {
        var saveRegistry = BuildRegistry();
        saveRegistry.RegisterRelation<LikesV1>("Likes", v => BitConverter.GetBytes(v.Weight), d => new LikesV1 { Weight = BitConverter.ToSingle(d) }, schemaHash: 100u);
        var source = new World();
        source.CodecRegistry = saveRegistry;
        Entity a = source.Commands.CreateEntity(new Position { X = 1f });
        Entity b = source.Commands.CreateEntity(new Position { X = 2f });
        source.ApplyCommands();
        source.Commands.AddRelation(a, b, new LikesV1 { Weight = 5f });
        source.ApplyCommands();
        var store = new FileStore(_path);
        source.Save(store);

        var loadRegistry = BuildRegistry();
        loadRegistry.RegisterRelation<LikesV2>("Likes",
            v => BitConverter.GetBytes(v.Weight).Concat(BitConverter.GetBytes(v.Mutual)).ToArray(),
            d => new LikesV2 { Weight = BitConverter.ToSingle(d, 0), Mutual = BitConverter.ToBoolean(d, 4) },
            schemaHash: 200u);
        loadRegistry.RegisterMigration("Likes", fromSchemaHash: 100u, toSchemaHash: 200u,
            oldBytes => BitConverter.GetBytes(BitConverter.ToSingle(oldBytes)).Concat(BitConverter.GetBytes(false)).ToArray());
        var target = new World();
        target.CodecRegistry = loadRegistry;

        target.Load(store);

        Entity? loadedA = null;
        Entity? loadedB = null;
        foreach (var snapshot in target.EnumerateEntities(loadRegistry))
        {
            if (target.HasComponent<RelationLinks<LikesV2>>(snapshot.Entity)) loadedA = snapshot.Entity;
            if (target.Sources<LikesV2>(snapshot.Entity).Any()) loadedB = snapshot.Entity;
        }

        loadedA.Should().NotBeNull();
        loadedB.Should().NotBeNull();
        var likesTargets = target.Targets<LikesV2>(loadedA!.Value);
        likesTargets.Should().ContainKey(loadedB!.Value);
        likesTargets[loadedB.Value].Weight.Should().Be(5f);
        likesTargets[loadedB.Value].Mutual.Should().BeFalse();
    }

    [Fact]
    public void Load_WhenNoSchemaHashWasRegisteredAtSaveTime_NeverTriggersAMismatchCheck()
    {
        var saveRegistry = new CodecRegistry();
        saveRegistry.Register<PositionV1>("Position", EncodeV1, DecodeV1); // no schemaHash
        var source = new World();
        source.CodecRegistry = saveRegistry;
        source.Commands.CreateEntity(new PositionV1 { X = 5f });
        source.ApplyCommands();
        var store = new FileStore(_path);
        source.Save(store);

        var loadRegistry = new CodecRegistry();
        loadRegistry.Register<PositionV1>("Position", EncodeV1, DecodeV1, schemaHash: 999u);
        var target = new World();
        target.CodecRegistry = loadRegistry;

        var act = () => target.Load(store);

        act.Should().NotThrow();
        var found = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<PositionV1>>().Resolve(target))
        {
            var positions = chunk.Access<Ref<PositionV1>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                positions[i].X.Should().Be(5f);
                found = true;
            }
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void Load_WhenTheCurrentlyRegisteredTypeHasNoSchemaHash_NeverTriggersAMismatchCheck()
    {
        var saveRegistry = new CodecRegistry();
        saveRegistry.Register<PositionV1>("Position", EncodeV1, DecodeV1, schemaHash: 100u);
        var source = new World();
        source.CodecRegistry = saveRegistry;
        source.Commands.CreateEntity(new PositionV1 { X = 5f });
        source.ApplyCommands();
        var store = new FileStore(_path);
        source.Save(store);

        var loadRegistry = new CodecRegistry();
        loadRegistry.Register<PositionV1>("Position", EncodeV1, DecodeV1); // no schemaHash
        var target = new World();
        target.CodecRegistry = loadRegistry;

        var act = () => target.Load(store);

        act.Should().NotThrow();
        var found = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<PositionV1>>().Resolve(target))
        {
            var positions = chunk.Access<Ref<PositionV1>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                positions[i].X.Should().Be(5f);
                found = true;
            }
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void Save_WhenAnEncoderThrowsPartway_LeavesThePreviousCheckpointIntact()
    {
        var registry = BuildRegistry();
        var source = new World();
        source.CodecRegistry = registry;
        source.Commands.CreateEntity(new Position { X = 1f });
        source.ApplyCommands();
        var store = new FileStore(_path);
        source.Save(store);

        var throwingRegistry = new CodecRegistry();
        throwingRegistry.Register<Position>("Position",
            _ => throw new InvalidOperationException("encoder boom"),
            bytes => new Position { X = BitConverter.ToSingle(bytes) });
        var faultySource = new World();
        faultySource.CodecRegistry = throwingRegistry;
        faultySource.Commands.CreateEntity(new Position { X = 99f });
        faultySource.ApplyCommands();

        var act = () => faultySource.Save(store);
        act.Should().Throw<InvalidOperationException>().WithMessage("encoder boom");

        var target = new World();
        target.CodecRegistry = registry;
        target.Load(store);
        var found = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<Position>>().Resolve(target))
        {
            var positions = chunk.Access<Ref<Position>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                positions[i].X.Should().Be(1f);
                found = true;
            }
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void Load_OnAFileWithBadMagicBytes_ThrowsInvalidDataException()
    {
        File.WriteAllBytes(_path, [0x00, 0x00, 0x00, 0x00, 0x01, 0x00]);
        var target = new World();
        target.CodecRegistry = BuildRegistry();
        var store = new FileStore(_path);

        var act = () => target.Load(store);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void SaveThenLoad_PreservesWhichEntitiesHaveWhichTags()
    {
        var registry = new CodecRegistry();
        registry.RegisterTag<Enemy>("Enemy");
        var source = new World();
        source.CodecRegistry = registry;
        var entity = source.Commands.CreateEntity();
        source.Commands.AddTag<Enemy>(entity);
        source.ApplyCommands();
        var store = new FileStore(_path);

        source.Save(store);

        var target = new World();
        target.CodecRegistry = registry;
        target.Load(store);

        target.EnumerateAllTags(registry).Should().ContainSingle(t => t.Discriminator == "Enemy");
    }

    [Fact]
    public void SaveThenLoad_RenamedTag_OldDiscriminatorStillResolves()
    {
        var savingRegistry = new CodecRegistry();
        savingRegistry.RegisterTag<Enemy>("Old.Enemy");
        var source = new World();
        source.CodecRegistry = savingRegistry;
        var entity = source.Commands.CreateEntity();
        source.Commands.AddTag<Enemy>(entity);
        source.ApplyCommands();
        var store = new FileStore(_path);

        source.Save(store);

        var loadingRegistry = new CodecRegistry();
        loadingRegistry.RegisterTag<Enemy>("Enemy");
        loadingRegistry.RegisterAlias("Old.Enemy", "Enemy");
        var target = new World();
        target.CodecRegistry = loadingRegistry;
        target.Load(store);

        target.EnumerateAllTags(loadingRegistry).Should().ContainSingle(t => t.Discriminator == "Enemy");
    }

    [Fact]
    public void SetPersistence_CalledTwiceWithDifferentStores_KeepsEachStoresRegistryIndependent()
    {
        var pathA = Path.Combine(Path.GetTempPath(), $"wyrd-persistence-pair-a-{Guid.NewGuid():N}.bin");
        var pathB = Path.Combine(Path.GetTempPath(), $"wyrd-persistence-pair-b-{Guid.NewGuid():N}.bin");
        try
        {
            var registryA = new CodecRegistry();
            registryA.Register<Position>("Position",
                p => BitConverter.GetBytes(p.X),
                bytes => new Position { X = BitConverter.ToSingle(bytes) });
            var registryB = new CodecRegistry();
            registryB.Register<Velocity>("Velocity",
                v => BitConverter.GetBytes(v.X),
                bytes => new Velocity { X = BitConverter.ToSingle(bytes) });

            var world = new WorldBuilder()
                .SetPersistence(new FileStore(pathA), registryA)
                .SetPersistence(new FileStore(pathB), registryB)
                .Build();
            world.Commands.CreateEntity(new Position { X = 1f });
            world.Commands.CreateEntity(new Velocity { X = 2f });
            world.ApplyCommands();

            // Neither call passes a registry explicitly - each must resolve its own via
            // the pairing SetPersistence recorded, not whichever registry is "current."
            world.Save(pathA);
            world.Save(pathB);

            var targetA = new World();
            targetA.CodecRegistry = registryA;
            targetA.Load(pathA);
            var foundPosition = false;
            foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<Position>>().Resolve(targetA))
                foundPosition |= chunk.Count > 0;
            foundPosition.Should().BeTrue("pathA was saved with registryA, which knows about Position");

            var targetB = new World();
            targetB.CodecRegistry = registryB;
            targetB.Load(pathB);
            var foundVelocity = false;
            foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<Velocity>>().Resolve(targetB))
                foundVelocity |= chunk.Count > 0;
            foundVelocity.Should().BeTrue("pathB was saved with registryB, which knows about Velocity");
        }
        finally
        {
            if (File.Exists(pathA)) File.Delete(pathA);
            if (File.Exists(pathB)) File.Delete(pathB);
        }
    }

    [Fact]
    public void Save_WithAnExplicitRegistry_UsesItInsteadOfTheStoresPairedOrWorldsDefaultRegistry()
    {
        var pairedRegistry = new CodecRegistry();
        pairedRegistry.Register<Position>("Position",
            p => BitConverter.GetBytes(p.X),
            bytes => new Position { X = BitConverter.ToSingle(bytes) });
        var overrideRegistry = new CodecRegistry();
        overrideRegistry.Register<Velocity>("Velocity",
            v => BitConverter.GetBytes(v.X),
            bytes => new Velocity { X = BitConverter.ToSingle(bytes) });

        var world = new WorldBuilder().SetPersistence(new FileStore(_path), pairedRegistry).Build();
        world.Commands.CreateEntity(new Position { X = 1f });
        world.Commands.CreateEntity(new Velocity { X = 2f });
        world.ApplyCommands();

        world.Save(_path, overrideRegistry);

        var target = new World();
        target.CodecRegistry = overrideRegistry;
        target.Load(_path);
        var foundVelocity = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<Velocity>>().Resolve(target))
            foundVelocity |= chunk.Count > 0;
        foundVelocity.Should().BeTrue("the explicit registry argument, not the store's paired registry, should have been used");
    }

    [Fact]
    public void Load_WithAnExplicitRegistry_UsesItInsteadOfTheStoresPairedOrWorldsDefaultRegistry()
    {
        var registry = BuildRegistry();
        var source = new World();
        source.CodecRegistry = registry;
        source.Commands.CreateEntity(new Position { X = 4f });
        source.ApplyCommands();
        var store = new FileStore(_path);
        source.Save(store);

        var target = new World();
        target.Load(store, registry);

        var found = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<Position>>().Resolve(target))
        {
            var positions = chunk.Access<Ref<Position>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                positions[i].X.Should().Be(4f);
                found = true;
            }
        }
        found.Should().BeTrue();
    }
}
