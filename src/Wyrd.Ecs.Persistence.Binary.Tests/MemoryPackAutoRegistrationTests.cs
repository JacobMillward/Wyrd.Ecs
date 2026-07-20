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

public struct NotMemoryPackable : IComponent
{
    public float X;
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
        var registry = new ComponentCodecRegistry();

        MemoryPackAutoRegistration.RegisterAll(registry);

        registry.TryGetByDiscriminator(typeof(AutoPosition).FullName!, out _).Should().BeTrue();
        registry.TryGetByDiscriminator(typeof(AutoVelocity).FullName!, out _).Should().BeTrue();
    }

    [Fact]
    public void RegisterAll_DoesNotRegisterAComponentWithoutTheMemoryPackableAttribute()
    {
        var registry = new ComponentCodecRegistry();

        MemoryPackAutoRegistration.RegisterAll(registry);

        registry.TryGetByDiscriminator(typeof(NotMemoryPackable).FullName!, out _).Should().BeFalse();
    }

    [Fact]
    public void Save_ThenLoad_UsingOnlyAutoRegisteredMemoryPackTypes_RoundTripsCorrectly()
    {
        var registry = new ComponentCodecRegistry();
        MemoryPackAutoRegistration.RegisterAll(registry);

        var source = new World();
        source.Commands.CreateEntity(new AutoPosition { X = 1f, Y = 2f, Name = "hi" });
        source.ApplyCommands();
        var store = new FileStore(_path);

        WorldSnapshot.Save(source, registry, store);

        var target = new World();
        WorldSnapshot.Load(target, registry, store);

        var found = false;
        foreach (var row in target.Query<AutoPosition>())
        {
            row.Get<AutoPosition>().X.Should().Be(1f);
            row.Get<AutoPosition>().Y.Should().Be(2f);
            row.Get<AutoPosition>().Name.Should().Be("hi");
            found = true;
        }
        found.Should().BeTrue();
    }
}
