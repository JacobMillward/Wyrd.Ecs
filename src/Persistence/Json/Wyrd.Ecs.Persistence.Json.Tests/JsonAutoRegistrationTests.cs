namespace Wyrd.Ecs.Persistence.Json.Tests;

public struct AutoPosition : IComponent
{
    public float X;
    public float Y;
    public string Name;
}

public struct AutoVelocity : IComponent
{
    public float X;
}

[JsonPersistenceIgnore]
public struct Ignored : IComponent
{
    public float X;
}

public class JsonAutoRegistrationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"wyrd-persistence-json-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void RegisterAll_RegistersEveryComponent()
    {
        var registry = new CodecRegistry();

        JsonAutoRegistration.RegisterAll(registry);

        registry.TryGetByDiscriminator(typeof(AutoPosition).FullName!, out _).Should().BeTrue();
        registry.TryGetByDiscriminator(typeof(AutoVelocity).FullName!, out _).Should().BeTrue();
    }

    [Fact]
    public void RegisterAll_DoesNotRegisterAComponentMarkedJsonPersistenceIgnore()
    {
        var registry = new CodecRegistry();

        JsonAutoRegistration.RegisterAll(registry);

        registry.TryGetByDiscriminator(typeof(Ignored).FullName!, out _).Should().BeFalse();
    }

    [Fact]
    public void RegisterAll_RegistersTwoSameSimpleNameTypesFromDifferentNamespacesIndependently()
    {
        var registry = new CodecRegistry();
        JsonAutoRegistration.RegisterAll(registry);

        var source = new World();
        source.DefaultCodecRegistry = registry;
        source.Commands.CreateEntity(new AutoPosition { X = 1f, Y = 2f, Name = "top" });
        source.Commands.CreateEntity(new Other.AutoPosition { Layer = 5 });
        source.ApplyCommands();
        var store = new FileStore(_path);

        source.Save(store);

        var target = new World();
        target.DefaultCodecRegistry = registry;
        target.Load(store);

        var foundTop = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<AutoPosition>>().Resolve(target))
        {
            var values = chunk.Access<Ref<AutoPosition>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                values[i].Name.Should().Be("top");
                foundTop = true;
            }
        }

        var foundOther = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<Other.AutoPosition>>().Resolve(target))
        {
            var values = chunk.Access<Ref<Other.AutoPosition>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                values[i].Layer.Should().Be(5);
                foundOther = true;
            }
        }

        foundTop.Should().BeTrue();
        foundOther.Should().BeTrue();
    }

    [Fact]
    public void Save_ThenLoad_UsingOnlyAutoRegisteredJsonTypes_RoundTripsCorrectly()
    {
        var registry = new CodecRegistry();
        JsonAutoRegistration.RegisterAll(registry);

        var source = new World();
        source.DefaultCodecRegistry = registry;
        source.Commands.CreateEntity(new AutoPosition { X = 1f, Y = 2f, Name = "hi" });
        source.ApplyCommands();
        var store = new FileStore(_path);

        source.Save(store);

        var target = new World();
        target.DefaultCodecRegistry = registry;
        target.Load(store);

        var found = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<AutoPosition>>().Resolve(target))
        {
            var positions = chunk.Access<Ref<AutoPosition>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                positions[i].X.Should().Be(1f);
                positions[i].Y.Should().Be(2f);
                positions[i].Name.Should().Be("hi");
                found = true;
            }
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void GeneratedAddJsonPersistence_WithAStore_AutoRegistersAndConfiguresTheStore()
    {
        var store = new FileStore(_path);

        var world = new WorldBuilder().AddJsonPersistence(store).Build();

        world.DefaultPersistenceStore.Should().BeSameAs(store);
        world.DefaultCodecRegistry!.TryGetByDiscriminator(typeof(AutoPosition).FullName!, out _).Should().BeTrue();
    }

    [Fact]
    public void GeneratedAddJsonPersistence_WithAPathString_AutoRegistersAndCreatesAFileStore()
    {
        var world = new WorldBuilder().AddJsonPersistence(_path).Build();

        world.DefaultPersistenceStore.Should().BeOfType<FileStore>().Which.Path.Should().Be(_path);
        world.DefaultCodecRegistry!.TryGetByDiscriminator(typeof(AutoPosition).FullName!, out _).Should().BeTrue();
    }
}
