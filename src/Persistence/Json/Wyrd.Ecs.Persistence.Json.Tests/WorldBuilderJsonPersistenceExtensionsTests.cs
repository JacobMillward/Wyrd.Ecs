namespace Wyrd.Ecs.Persistence.Json.Tests;

public class WorldBuilderJsonPersistenceExtensionsTests : IDisposable
{
    private readonly string _path = Path.GetTempFileName();

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void AddJsonPersistence_WithAStoreAndARegistry_ConfiguresBoth()
    {
        var store = new FileStore(_path);
        var registry = new CodecRegistry();

        var world = new WorldBuilder().AddJsonPersistence(store, registry).Build();

        world.DefaultPersistenceStore.Should().BeSameAs(store);
        world.CodecRegistry.Should().BeSameAs(registry);
    }

    [Fact]
    public void AddJsonPersistence_WithAPathStringAndARegistry_CreatesAFileStoreAtThatPath()
    {
        var registry = new CodecRegistry();

        var world = new WorldBuilder().AddJsonPersistence(_path, registry).Build();

        world.DefaultPersistenceStore.Should().BeOfType<FileStore>().Which.Path.Should().Be(_path);
        world.CodecRegistry.Should().BeSameAs(registry);
    }
}
