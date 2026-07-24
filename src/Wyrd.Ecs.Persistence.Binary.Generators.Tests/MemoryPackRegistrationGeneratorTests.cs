namespace Wyrd.Ecs.Persistence.Binary.Generators.Tests;

public class MemoryPackRegistrationGeneratorTests
{
    [Fact]
    public void NoMemoryPackableComponents_EmitsAnEmptyRegisterAll()
    {
        const string source = """
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }
            """;

        var result = GeneratorTestHost.Run(new MemoryPackRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("public static void RegisterAll(global::Wyrd.Ecs.ComponentCodecRegistry registry)");
        generated.Should().NotContain("registry.Register<");
    }

    [Fact]
    public void MemoryPackableComponent_RegistersIt()
    {
        const string source = """
            using MemoryPack;
            using Wyrd.Ecs;

            [MemoryPackable]
            public partial struct Position : IComponent { public float X; }
            """;

        var result = GeneratorTestHost.Run(new MemoryPackRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("registry.Register<global::Position>(\"Position\",");
        generated.Should().Contain("global::MemoryPack.MemoryPackSerializer.Serialize(v)");
        generated.Should().Contain("global::MemoryPack.MemoryPackSerializer.Deserialize<global::Position>(bytes)");
    }

    [Fact]
    public void NonComponentStructWithMemoryPackable_IsNotRegistered()
    {
        const string source = """
            using MemoryPack;
            using Wyrd.Ecs;

            [MemoryPackable]
            public partial struct NotAComponent { public float X; }
            """;

        var result = GeneratorTestHost.Run(new MemoryPackRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().NotContain("registry.Register<");
    }

    [Fact]
    public void MultipleComponents_RegistersEachOnce()
    {
        const string source = """
            using MemoryPack;
            using Wyrd.Ecs;

            [MemoryPackable]
            public partial struct Position : IComponent { public float X; }

            [MemoryPackable]
            public partial struct Velocity : IComponent { public float X; }
            """;

        var result = GeneratorTestHost.Run(new MemoryPackRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("registry.Register<global::Position>(\"Position\",");
        generated.Should().Contain("registry.Register<global::Velocity>(\"Velocity\",");
    }

    [Fact]
    public void AlwaysEmitsBothAddBinaryPersistenceOverloads()
    {
        const string source = """
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }
            """;

        var result = GeneratorTestHost.Run(new MemoryPackRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("public global::Wyrd.Ecs.WorldBuilder AddBinaryPersistence(global::Wyrd.Ecs.Persistence.IPersistenceStore store)");
        generated.Should().Contain("public global::Wyrd.Ecs.WorldBuilder AddBinaryPersistence(string path)");
    }
}
