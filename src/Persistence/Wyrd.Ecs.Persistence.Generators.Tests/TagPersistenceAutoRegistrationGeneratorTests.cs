namespace Wyrd.Ecs.Persistence.Generators.Tests;

public class TagPersistenceAutoRegistrationGeneratorTests
{
    [Fact]
    public void EveryTag_RegisteredByDefault()
    {
        const string source = "public struct Enemy : Wyrd.Ecs.ITag { }";

        var generated = GeneratorTestHost.Run(new TagPersistenceAutoRegistrationGenerator(), GeneratorTestHost.Compile(source))
            .Results[0].GeneratedSources.Single().SourceText.ToString();

        generated.Should().Contain("registry.RegisterTag<global::Enemy>(\"Enemy\");");
    }

    [Fact]
    public void PersistenceIgnore_ExcludesIt()
    {
        const string source = """
            [Wyrd.Ecs.Persistence.PersistenceIgnore]
            public struct Transient : Wyrd.Ecs.ITag { }
            """;

        var generated = GeneratorTestHost.Run(new TagPersistenceAutoRegistrationGenerator(), GeneratorTestHost.Compile(source))
            .Results[0].GeneratedSources.Single().SourceText.ToString();

        generated.Should().NotContain("Transient");
    }
}
