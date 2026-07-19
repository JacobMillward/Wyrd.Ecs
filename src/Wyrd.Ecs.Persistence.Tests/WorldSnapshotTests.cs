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

    private static SerializerRegistry BuildRegistry()
    {
        var registry = new SerializerRegistry();
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
        var store = new LocalFilePersistenceStore(_path);

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
        var store = new LocalFilePersistenceStore(_path);

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
        var store = new LocalFilePersistenceStore(_path);
        WorldSnapshot.Save(source, saveRegistry, store);

        var loadRegistry = new SerializerRegistry();
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
        var store = new LocalFilePersistenceStore(_path);
        WorldSnapshot.Save(source, registry, store);

        var target = new World();
        WorldSnapshot.Load(target, registry, store);

        var count = 0;
        foreach (var _ in target.Query<Position>()) count++;
        count.Should().Be(0);
    }
}
