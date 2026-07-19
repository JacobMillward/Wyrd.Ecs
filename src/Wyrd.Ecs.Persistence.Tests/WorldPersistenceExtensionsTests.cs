namespace Wyrd.Ecs.Persistence.Tests;

public class WorldPersistenceExtensionsTests
{
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
}
