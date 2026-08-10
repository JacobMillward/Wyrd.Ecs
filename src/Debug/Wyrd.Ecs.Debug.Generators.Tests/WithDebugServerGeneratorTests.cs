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
        generated.Should().Contain("public static global::Wyrd.Ecs.Debug.DebugServer CreateDebugServer(this global::Wyrd.Ecs.World world, global::Wyrd.Ecs.Debug.DebugServerOptions? options = null)");

        // Sliced, not whole-file Contains: RegisterAllIncludingIgnored appears once per
        // method, and each occurrence needs to be inside that method's own body to prove
        // both methods independently auto-wire a registry, not just one of them.
        var createStart = generated.IndexOf("CreateDebugServer");
        var withStart = generated.IndexOf("WithDebugServer(this");
        createStart.Should().BeGreaterThan(-1);
        withStart.Should().BeGreaterThan(-1);
        generated.Substring(createStart, withStart - createStart).Should().Contain("RegisterAllIncludingIgnored(registry);");
        generated.Substring(withStart).Should().Contain("RegisterAllIncludingIgnored(registry);");
    }
}
