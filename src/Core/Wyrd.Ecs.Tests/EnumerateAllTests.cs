using System.Text;

namespace Wyrd.Ecs.Tests;

public class EnumerateAllTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    private struct Velocity : IComponent
    {
        public float X;
    }

    private struct Marker : ITag;

    private static ComponentCodecRegistry BuildRegistry()
    {
        var registry = new ComponentCodecRegistry();
        registry.Register<Position>("Position",
            p => Encoding.UTF8.GetBytes(p.X.ToString()),
            bytes => new Position { X = float.Parse(Encoding.UTF8.GetString(bytes)) });
        registry.Register<Velocity>("Velocity",
            v => Encoding.UTF8.GetBytes(v.X.ToString()),
            bytes => new Velocity { X = float.Parse(Encoding.UTF8.GetString(bytes)) });
        return registry;
    }

    [Fact]
    public void EnumerateAll_YieldsOneEntryPerRegisteredComponentOnEveryEntity()
    {
        var world = new World();
        var registry = BuildRegistry();
        var onlyPosition = world.Commands.CreateEntity(new Position { X = 1f }).Entity;
        var both = world.Commands.CreateEntity(new Position { X = 2f }, new Velocity { X = 3f }).Entity;
        world.ApplyCommands();

        var results = world.EnumerateAll(registry).ToList();

        results.Should().HaveCount(3);
        results.Should().Contain(c => c.Entity == onlyPosition && c.Discriminator == "Position");
        results.Should().Contain(c => c.Entity == both && c.Discriminator == "Position");
        results.Should().Contain(c => c.Entity == both && c.Discriminator == "Velocity");
    }

    [Fact]
    public void EnumerateAll_SkipsComponentTypesNotRegistered()
    {
        var world = new World();
        var registry = new ComponentCodecRegistry();
        registry.Register<Position>("Position",
            p => Encoding.UTF8.GetBytes(p.X.ToString()),
            bytes => new Position { X = float.Parse(Encoding.UTF8.GetString(bytes)) });
        world.Commands.CreateEntity(new Position(), new Velocity()); // Velocity never registered
        world.ApplyCommands();

        var results = world.EnumerateAll(registry).ToList();

        results.Should().ContainSingle();
        results[0].Discriminator.Should().Be("Position");
    }

    [Fact]
    public void EnumerateAll_IgnoresTags_TheyHaveNoStorageToWalk()
    {
        var world = new World();
        var registry = BuildRegistry();
        var entity = world.Commands.CreateEntity(new Position()).Entity;
        world.Commands.AddTag<Marker>(entity);
        world.ApplyCommands();

        var results = world.EnumerateAll(registry).ToList();

        results.Should().ContainSingle();
        results[0].Discriminator.Should().Be("Position");
    }

    [Fact]
    public void EnumerateAll_ThenDeserializingEachResult_ReconstructsEquivalentEntities()
    {
        var source = new World();
        var registry = BuildRegistry();
        source.Commands.CreateEntity(new Position { X = 5f }, new Velocity { X = 6f });
        source.ApplyCommands();

        var snapshot = source.EnumerateAll(registry).ToList();

        var target = new World();
        var rebuilt = target.Commands.CreateEntity().Entity;
        target.ApplyCommands();
        foreach (var component in snapshot)
        {
            registry.TryGetByDiscriminator(component.Discriminator, out var registered).Should().BeTrue();
            registered.DecodeInto(target, rebuilt, component.Data);
        }
        target.ApplyCommands();

        target.GetComponent<Position>(rebuilt).X.Should().Be(5f);
        target.GetComponent<Velocity>(rebuilt).X.Should().Be(6f);
    }

    [Fact]
    public void EnumerateAll_OnAnEmptyWorld_YieldsNothing()
    {
        var world = new World();
        var registry = BuildRegistry();

        world.EnumerateAll(registry).Should().BeEmpty();
    }

    [Fact]
    public void EnumerateAll_CarriesEachComponentsRegisteredSchemaHashThrough()
    {
        var world = new World();
        var registry = new ComponentCodecRegistry();
        registry.Register<Position>("Position",
            p => Encoding.UTF8.GetBytes(p.X.ToString()),
            bytes => new Position { X = float.Parse(Encoding.UTF8.GetString(bytes)) },
            schemaHash: 999u);
        world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        var result = world.EnumerateAll(registry).Single();

        result.SchemaHash.Should().Be(999u);
    }
}
