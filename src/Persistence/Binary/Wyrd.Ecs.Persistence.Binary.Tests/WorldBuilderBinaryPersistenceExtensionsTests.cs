namespace Wyrd.Ecs.Persistence.Binary.Tests;

public class WorldBuilderBinaryPersistenceExtensionsTests : IDisposable
{
    private readonly string _path = Path.GetTempFileName();

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void AddBinaryPersistence_WithAStoreAndARegistry_ConfiguresBoth()
    {
        var store = new FileStore(_path);
        var registry = new CodecRegistry();

        var world = new WorldBuilder().AddBinaryPersistence(store, registry).Build();

        world.DefaultPersistenceStore.Should().BeSameAs(store);
        world.CodecRegistry.Should().BeSameAs(registry);
    }

    [Fact]
    public void AddBinaryPersistence_WithAPathStringAndARegistry_CreatesAFileStoreAtThatPath()
    {
        var registry = new CodecRegistry();

        var world = new WorldBuilder().AddBinaryPersistence(_path, registry).Build();

        world.DefaultPersistenceStore.Should().BeOfType<FileStore>().Which.Path.Should().Be(_path);
        world.CodecRegistry.Should().BeSameAs(registry);
    }
}
