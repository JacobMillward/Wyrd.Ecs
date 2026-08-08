using MemoryPack;

namespace Wyrd.Ecs.Persistence.Binary.Tests;

[MemoryPackable]
public partial struct AutoPosition : IComponent
{
    public float X;
    public float Y;
    public string Name;
}

[MemoryPackable]
public partial struct AutoVelocity : IComponent
{
    public float X;
}

public struct UnmanagedNoAttribute : IComponent
{
    public float X;
    public float Y;
}

[PersistenceIgnore]
public struct Ignored : IComponent
{
    public float X;
}

public struct Label
{
    public string Text;
}

public struct WithStringField : IComponent
{
    public string Name;
    public int Count;
}

public struct WithNestedField : IComponent
{
    public Label Label;
}

public struct WithCollectionFields : IComponent
{
    public string[] Tags;
    public System.Collections.Generic.List<int> Counts;
}

public class MemoryPackAutoRegistrationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"wyrd-persistence-memorypack-{Guid.NewGuid():N}.bin");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void RegisterAll_RegistersEveryMemoryPackableComponent()
    {
        var registry = new CodecRegistry();

        MemoryPackAutoRegistration.RegisterAll(registry);

        registry.TryGetByDiscriminator(typeof(AutoPosition).FullName!, out _).Should().BeTrue();
        registry.TryGetByDiscriminator(typeof(AutoVelocity).FullName!, out _).Should().BeTrue();
    }

    [Fact]
    public void RegisterAll_RegistersAnUnmanagedComponentWithNoAttribute()
    {
        var registry = new CodecRegistry();

        MemoryPackAutoRegistration.RegisterAll(registry);

        registry.TryGetByDiscriminator(typeof(UnmanagedNoAttribute).FullName!, out _).Should().BeTrue();
    }

    [Fact]
    public void RegisterAll_DoesNotRegisterAComponentMarkedPersistenceIgnore()
    {
        var registry = new CodecRegistry();

        MemoryPackAutoRegistration.RegisterAll(registry);

        registry.TryGetByDiscriminator(typeof(Ignored).FullName!, out _).Should().BeFalse();
    }

    [Fact]
    public void Save_ThenLoad_UsingOnlyAutoRegisteredMemoryPackTypes_RoundTripsCorrectly()
    {
        var registry = new CodecRegistry();
        MemoryPackAutoRegistration.RegisterAll(registry);

        var source = new World();
        source.CodecRegistry = registry;
        source.Commands.CreateEntity(new AutoPosition { X = 1f, Y = 2f, Name = "hi" });
        source.ApplyCommands();
        var store = new FileStore(_path);

        source.Save(store);

        var target = new World();
        target.CodecRegistry = registry;
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
    public void Save_ThenLoad_ComponentWithStringFieldAndNoAttribute_RoundTripsCorrectly()
    {
        var registry = new CodecRegistry();
        MemoryPackAutoRegistration.RegisterAll(registry);

        var source = new World();
        source.CodecRegistry = registry;
        source.Commands.CreateEntity(new WithStringField { Name = "hello", Count = 42 });
        source.ApplyCommands();
        var store = new FileStore(_path);
        source.Save(store);

        var target = new World();
        target.CodecRegistry = registry;
        target.Load(store);

        var found = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<WithStringField>>().Resolve(target))
        {
            var values = chunk.Access<Ref<WithStringField>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                values[i].Name.Should().Be("hello");
                values[i].Count.Should().Be(42);
                found = true;
            }
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void Save_ThenLoad_ComponentWithNestedPlainStructField_RoundTripsCorrectly()
    {
        var registry = new CodecRegistry();
        MemoryPackAutoRegistration.RegisterAll(registry);

        var source = new World();
        source.CodecRegistry = registry;
        source.Commands.CreateEntity(new WithNestedField { Label = new Label { Text = "nested" } });
        source.ApplyCommands();
        var store = new FileStore(_path);
        source.Save(store);

        var target = new World();
        target.CodecRegistry = registry;
        target.Load(store);

        var found = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<WithNestedField>>().Resolve(target))
        {
            var values = chunk.Access<Ref<WithNestedField>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                values[i].Label.Text.Should().Be("nested");
                found = true;
            }
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void Save_ThenLoad_ComponentWithArrayAndListFields_RoundTripsCorrectly()
    {
        var registry = new CodecRegistry();
        MemoryPackAutoRegistration.RegisterAll(registry);

        var source = new World();
        source.CodecRegistry = registry;
        source.Commands.CreateEntity(new WithCollectionFields
        {
            Tags = new[] { "a", "b" },
            Counts = new System.Collections.Generic.List<int> { 1, 2, 3 }
        });
        source.ApplyCommands();
        var store = new FileStore(_path);
        source.Save(store);

        var target = new World();
        target.CodecRegistry = registry;
        target.Load(store);

        var found = false;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<WithCollectionFields>>().Resolve(target))
        {
            var values = chunk.Access<Ref<WithCollectionFields>>();
            for (var i = 0; i < chunk.Count; i++)
            {
                values[i].Tags.Should().Equal("a", "b");
                values[i].Counts.Should().Equal(1, 2, 3);
                found = true;
            }
        }
        found.Should().BeTrue();
    }

    [Fact]
    public void GeneratedAddBinaryPersistence_WithAStore_AutoRegistersAndConfiguresTheStore()
    {
        var store = new FileStore(_path);

        var world = new WorldBuilder().AddBinaryPersistence(store).Build();

        world.DefaultPersistenceStore.Should().BeSameAs(store);
        world.CodecRegistry!.TryGetByDiscriminator(typeof(AutoPosition).FullName!, out _).Should().BeTrue();
    }

    [Fact]
    public void GeneratedAddBinaryPersistence_WithAPathString_AutoRegistersAndCreatesAFileStore()
    {
        var world = new WorldBuilder().AddBinaryPersistence(_path).Build();

        world.DefaultPersistenceStore.Should().BeOfType<FileStore>().Which.Path.Should().Be(_path);
        world.CodecRegistry!.TryGetByDiscriminator(typeof(AutoPosition).FullName!, out _).Should().BeTrue();
    }
}
