namespace Wyrd.Ecs.Persistence.Tests;

public class WorldSnapshotTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"wyrd-persistence-worldsnapshot-{Guid.NewGuid():N}.bin");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private struct Position : IComponent
    {
        public float X;
    }

    private struct Velocity : IComponent
    {
        public float X;
    }

    private static ComponentCodecRegistry BuildRegistry()
    {
        var registry = new ComponentCodecRegistry();
        registry.Register<Position>("Position",
            p => BitConverter.GetBytes(p.X),
            bytes => new Position { X = BitConverter.ToSingle(bytes) });
        registry.Register<Velocity>("Velocity",
            v => BitConverter.GetBytes(v.X),
            bytes => new Velocity { X = BitConverter.ToSingle(bytes) });
        return registry;
    }

    [Fact]
    public void Save_ThenLoad_ReconstructsEquivalentEntities()
    {
        var registry = BuildRegistry();
        var source = new World();
        var entity = source.Commands.CreateEntity(new Position { X = 1f });
        source.ApplyCommands();
        source.Commands.AddComponent(entity, new Velocity { X = 2f });
        source.ApplyCommands();
        var store = new FileStore(_path);

        WorldSnapshot.Save(source, registry, store);

        var target = new World();
        WorldSnapshot.Load(target, registry, store);

        var found = false;
        foreach (var row in target.Query<Position, Velocity>())
        {
            row.Get<Position>().X.Should().Be(1f);
            row.Get<Velocity>().X.Should().Be(2f);
            found = true;
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void Save_ThenLoad_PreservesMultipleEntitiesIndependently()
    {
        var registry = BuildRegistry();
        var source = new World();
        source.Commands.CreateEntity(new Position { X = 10f });
        source.Commands.CreateEntity(new Position { X = 20f });
        source.ApplyCommands();
        var store = new FileStore(_path);

        WorldSnapshot.Save(source, registry, store);

        var target = new World();
        WorldSnapshot.Load(target, registry, store);

        var values = new List<float>();
        foreach (var row in target.Query<Position>())
            values.Add(row.Get<Position>().X);

        values.Should().BeEquivalentTo([10f, 20f]);
    }

    [Fact]
    public void Load_ForADiscriminatorNotInTheLoadingRegistry_SkipsThatComponentWithoutError()
    {
        var saveRegistry = BuildRegistry();
        var source = new World();
        source.Commands.CreateEntity(new Position { X = 1f });
        source.ApplyCommands();
        var entity = source.Commands.CreateEntity();
        source.ApplyCommands();
        source.Commands.AddComponent(entity, new Velocity { X = 2f });
        source.ApplyCommands();
        var store = new FileStore(_path);
        WorldSnapshot.Save(source, saveRegistry, store);

        var loadRegistry = new ComponentCodecRegistry();
        loadRegistry.Register<Position>("Position",
            p => BitConverter.GetBytes(p.X),
            bytes => new Position { X = BitConverter.ToSingle(bytes) });
        // Velocity deliberately not registered here.

        var target = new World();
        var act = () => WorldSnapshot.Load(target, loadRegistry, store);

        act.Should().NotThrow();

        var positionCount = 0;
        foreach (var row in target.Query<Position>())
        {
            row.Get<Position>().X.Should().Be(1f);
            positionCount++;
        }
        positionCount.Should().Be(1);
    }

    [Fact]
    public void Load_OnAnEmptyCheckpoint_LeavesTheWorldEmpty()
    {
        var registry = BuildRegistry();
        var source = new World();
        var store = new FileStore(_path);
        WorldSnapshot.Save(source, registry, store);

        var target = new World();
        WorldSnapshot.Load(target, registry, store);

        var count = 0;
        foreach (var _ in target.Query<Position>()) count++;
        count.Should().Be(0);
    }

    [Fact]
    public void Save_WithNoStoreArgument_UsesTheWorldsDefaultPersistenceStore()
    {
        var registry = BuildRegistry();
        var source = new World();
        source.Commands.CreateEntity(new Position { X = 7f });
        source.ApplyCommands();
        source.DefaultPersistenceStore = new FileStore(_path);

        WorldSnapshot.Save(source, registry);

        var target = new World();
        target.DefaultPersistenceStore = new FileStore(_path);
        WorldSnapshot.Load(target, registry);

        var found = false;
        foreach (var row in target.Query<Position>())
        {
            row.Get<Position>().X.Should().Be(7f);
            found = true;
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void Save_WithNoStoreArgumentAndNoDefaultConfigured_ThrowsAClearError()
    {
        var registry = BuildRegistry();
        var world = new World();

        var act = () => WorldSnapshot.Save(world, registry);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Load_WithNoStoreArgumentAndNoDefaultConfigured_ThrowsAClearError()
    {
        var registry = BuildRegistry();
        var world = new World();

        var act = () => WorldSnapshot.Load(world, registry);

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
        var saveRegistry = new ComponentCodecRegistry();
        saveRegistry.Register<PositionV1>("Position", EncodeV1, DecodeV1, schemaHash: 100u);
        var source = new World();
        source.Commands.CreateEntity(new PositionV1 { X = 5f });
        source.ApplyCommands();
        var store = new FileStore(_path);

        WorldSnapshot.Save(source, saveRegistry, store);

        var loadRegistry = new ComponentCodecRegistry();
        loadRegistry.Register<PositionV1>("Position", EncodeV1, DecodeV1, schemaHash: 100u);
        var target = new World();
        WorldSnapshot.Load(target, loadRegistry, store);

        var found = false;
        foreach (var row in target.Query<PositionV1>())
        {
            row.Get<PositionV1>().X.Should().Be(5f);
            found = true;
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void Load_WithAMismatchedSchemaHashAndNoRegisteredMigration_ThrowsNamingTheDiscriminatorAndHash()
    {
        var saveRegistry = new ComponentCodecRegistry();
        saveRegistry.Register<PositionV1>("Position", EncodeV1, DecodeV1, schemaHash: 100u);
        var source = new World();
        source.Commands.CreateEntity(new PositionV1 { X = 5f });
        source.ApplyCommands();
        var store = new FileStore(_path);
        WorldSnapshot.Save(source, saveRegistry, store);

        var loadRegistry = new ComponentCodecRegistry();
        loadRegistry.Register<PositionV2>("Position", EncodeV2, DecodeV2, schemaHash: 200u);
        var target = new World();

        var act = () => WorldSnapshot.Load(target, loadRegistry, store);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Position*")
            .WithMessage("*00000064*"); // 100 formatted as {hash:X8}, the format Migrate's error message uses
    }

    [Fact]
    public void Load_WithAMismatchedSchemaHashAndARegisteredMigration_AppliesItAndReconstructsCorrectly()
    {
        var saveRegistry = new ComponentCodecRegistry();
        saveRegistry.Register<PositionV1>("Position", EncodeV1, DecodeV1, schemaHash: 100u);
        var source = new World();
        source.Commands.CreateEntity(new PositionV1 { X = 5f });
        source.ApplyCommands();
        var store = new FileStore(_path);
        WorldSnapshot.Save(source, saveRegistry, store);

        var loadRegistry = new ComponentCodecRegistry();
        loadRegistry.Register<PositionV2>("Position", EncodeV2, DecodeV2, schemaHash: 200u);
        loadRegistry.RegisterMigration("Position", fromSchemaHash: 100u, toSchemaHash: 200u,
            oldBytes => EncodeV2(new PositionV2 { X = DecodeV1(oldBytes).X, Y = 0f }));
        var target = new World();

        WorldSnapshot.Load(target, loadRegistry, store);

        var found = false;
        foreach (var row in target.Query<PositionV2>())
        {
            row.Get<PositionV2>().X.Should().Be(5f);
            row.Get<PositionV2>().Y.Should().Be(0f);
            found = true;
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void Load_ChainingTwoRegisteredMigrationSteps_WalksBothToReachTheCurrentSchema()
    {
        var saveRegistry = new ComponentCodecRegistry();
        saveRegistry.Register<PositionV1>("Position", EncodeV1, DecodeV1, schemaHash: 100u);
        var source = new World();
        source.Commands.CreateEntity(new PositionV1 { X = 5f });
        source.ApplyCommands();
        var store = new FileStore(_path);
        WorldSnapshot.Save(source, saveRegistry, store);

        var loadRegistry = new ComponentCodecRegistry();
        loadRegistry.Register<PositionV3>("Position", EncodeV3, DecodeV3, schemaHash: 300u);
        loadRegistry.RegisterMigration("Position", fromSchemaHash: 100u, toSchemaHash: 200u,
            oldBytes => EncodeV2(new PositionV2 { X = DecodeV1(oldBytes).X, Y = 0f }));
        loadRegistry.RegisterMigration("Position", fromSchemaHash: 200u, toSchemaHash: 300u,
            oldBytes => { var v2 = DecodeV2(oldBytes); return EncodeV3(new PositionV3 { X = v2.X, Y = v2.Y, Z = 0f }); });
        var target = new World();

        WorldSnapshot.Load(target, loadRegistry, store);

        var found = false;
        foreach (var row in target.Query<PositionV3>())
        {
            row.Get<PositionV3>().X.Should().Be(5f);
            row.Get<PositionV3>().Y.Should().Be(0f);
            row.Get<PositionV3>().Z.Should().Be(0f);
            found = true;
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void Load_WhenNoSchemaHashWasRegisteredAtSaveTime_NeverTriggersAMismatchCheck()
    {
        var saveRegistry = new ComponentCodecRegistry();
        saveRegistry.Register<PositionV1>("Position", EncodeV1, DecodeV1); // no schemaHash
        var source = new World();
        source.Commands.CreateEntity(new PositionV1 { X = 5f });
        source.ApplyCommands();
        var store = new FileStore(_path);
        WorldSnapshot.Save(source, saveRegistry, store);

        var loadRegistry = new ComponentCodecRegistry();
        loadRegistry.Register<PositionV1>("Position", EncodeV1, DecodeV1, schemaHash: 999u);
        var target = new World();

        var act = () => WorldSnapshot.Load(target, loadRegistry, store);

        act.Should().NotThrow();
        var found = false;
        foreach (var row in target.Query<PositionV1>())
        {
            row.Get<PositionV1>().X.Should().Be(5f);
            found = true;
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void Load_WhenTheCurrentlyRegisteredTypeHasNoSchemaHash_NeverTriggersAMismatchCheck()
    {
        var saveRegistry = new ComponentCodecRegistry();
        saveRegistry.Register<PositionV1>("Position", EncodeV1, DecodeV1, schemaHash: 100u);
        var source = new World();
        source.Commands.CreateEntity(new PositionV1 { X = 5f });
        source.ApplyCommands();
        var store = new FileStore(_path);
        WorldSnapshot.Save(source, saveRegistry, store);

        var loadRegistry = new ComponentCodecRegistry();
        loadRegistry.Register<PositionV1>("Position", EncodeV1, DecodeV1); // no schemaHash
        var target = new World();

        var act = () => WorldSnapshot.Load(target, loadRegistry, store);

        act.Should().NotThrow();
        var found = false;
        foreach (var row in target.Query<PositionV1>())
        {
            row.Get<PositionV1>().X.Should().Be(5f);
            found = true;
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void Load_OnAFileWithBadMagicBytes_ThrowsInvalidDataException()
    {
        File.WriteAllBytes(_path, [0x00, 0x00, 0x00, 0x00, 0x01, 0x00]);
        var registry = BuildRegistry();
        var target = new World();
        var store = new FileStore(_path);

        var act = () => WorldSnapshot.Load(target, registry, store);

        act.Should().Throw<InvalidDataException>();
    }
}
