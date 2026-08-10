using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Wyrd.Ecs.Debug.Generators;

namespace Wyrd.Ecs.Debug.Generators.Tests;

public class WithDebugServerGeneratorTests
{
    [Fact]
    public void AlwaysEmitsTheOverload_RegardlessOfConsumerSource()
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "AnyConsumer",
            syntaxTrees: [CSharpSyntaxTree.ParseText("namespace Test; public struct Unrelated { }")],
            references: [],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new WithDebugServerGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var result = driver.GetRunResult();

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("public static global::Wyrd.Ecs.Debug.DebugServer WithDebugServer(this global::Wyrd.Ecs.World world, int port = 5299)");
        generated.Should().Contain("global::Wyrd.Ecs.Persistence.Json.JsonAutoRegistration.RegisterAllIncludingIgnored(registry);");
    }
}
