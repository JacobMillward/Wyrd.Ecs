namespace Wyrd.Ecs.Persistence.Binary.Tests;

public class WorldBuilderBinaryPersistenceExtensionsTests
{
    [Fact]
    public void AddBinaryPersistence_ConfiguresTheWorldsDefaultPersistenceStore()
    {
        var store = new FileStore(Path.GetTempFileName());

        var world = new WorldBuilder().AddBinaryPersistence(store).Build();

        world.DefaultPersistenceStore.Should().BeSameAs(store);
    }
}
