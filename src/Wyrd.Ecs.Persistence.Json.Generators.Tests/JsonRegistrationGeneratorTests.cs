using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Wyrd.Ecs.Persistence.Json.Generators.Tests;

public class JsonRegistrationGeneratorTests
{
    [Fact]
    public void NoComponents_EmitsAnEmptyRegisterAll()
    {
        const string source = """
            namespace Test;
            public struct NotAComponent { public float X; }
            """;

        var result = GeneratorTestHost.Run(new JsonRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("public static void RegisterAll(global::Wyrd.Ecs.ComponentCodecRegistry registry)");
        generated.Should().NotContain("registry.Register<");
    }

    [Fact]
    public void AComponentWithNoAttributes_IsRegistered()
    {
        const string source = """
            using Wyrd.Ecs;
            namespace Test;
            public struct Position : IComponent { public float X; }
            """;

        var result = GeneratorTestHost.Run(new JsonRegistrationGenerator(), GeneratorTestHost.Compile(source, assemblyName: "MyAssembly"));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("registry.Register<global::Test.Position>(\"Test.Position\",");
        generated.Should().Contain("global::MyAssemblyJsonPersistenceContext.Default.Test_Position");
    }

    [Fact]
    public void AComponentMarkedJsonPersistenceIgnore_IsNotRegistered()
    {
        const string source = """
            using Wyrd.Ecs;
            using Wyrd.Ecs.Persistence.Json;
            namespace Test;
            [JsonPersistenceIgnore]
            public struct Secret : IComponent { public string Value; }
            """;

        var result = GeneratorTestHost.Run(new JsonRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().NotContain("registry.Register<");
    }

    [Fact]
    public void MultipleComponents_RegistersEachOnce()
    {
        const string source = """
            using Wyrd.Ecs;
            namespace Test;
            public struct Position : IComponent { public float X; }
            public struct Velocity : IComponent { public float X; }
            """;

        var result = GeneratorTestHost.Run(new JsonRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("registry.Register<global::Test.Position>(\"Test.Position\",");
        generated.Should().Contain("registry.Register<global::Test.Velocity>(\"Test.Velocity\",");
    }

    [Fact]
    public void AlwaysEmitsBothAddJsonPersistenceOverloads()
    {
        const string source = """
            namespace Test;
            public struct NotAComponent { public float X; }
            """;

        var result = GeneratorTestHost.Run(new JsonRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("public global::Wyrd.Ecs.WorldBuilder AddJsonPersistence(global::Wyrd.Ecs.Persistence.IPersistenceStore store)");
        generated.Should().Contain("public global::Wyrd.Ecs.WorldBuilder AddJsonPersistence(string path)");
    }

    [Fact]
    public void EditingAnUnrelatedMethodBody_LeavesTheCandidateStepUnchanged()
    {
        const string sourceV1 = """
            using Wyrd.Ecs;
            namespace Test;
            public struct Position : IComponent { public float X; }
            public static class Unrelated { public static int Compute() => 1; }
            """;

        const string sourceV2 = """
            using Wyrd.Ecs;
            namespace Test;
            public struct Position : IComponent { public float X; }
            public static class Unrelated { public static int Compute() => 2; }
            """;

        var generator = new JsonRegistrationGenerator().AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            driverOptions: new GeneratorDriverOptions(trackIncrementalGeneratorSteps: true));

        var compilationV1 = GeneratorTestHost.Compile(sourceV1);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilationV1, out _, out _);

        var originalTree = compilationV1.SyntaxTrees.Single();
        var editedTree = originalTree.WithChangedText(SourceText.From(sourceV2));
        var compilationV2 = compilationV1.ReplaceSyntaxTree(originalTree, editedTree);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilationV2, out _, out _);

        var steps = driver.GetRunResult().Results[0].TrackedSteps["JsonRegisteredComponentInfo"];
        steps.Should().ContainSingle();
        steps[0].Outputs.Should().Contain(o =>
            o.Reason == IncrementalStepRunReason.Cached || o.Reason == IncrementalStepRunReason.Unchanged);
    }
}
