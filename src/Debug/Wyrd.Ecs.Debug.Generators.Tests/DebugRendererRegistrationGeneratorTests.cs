using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Wyrd.Ecs.Debug.Generators;

namespace Wyrd.Ecs.Debug.Generators.Tests;

public class DebugRendererRegistrationGeneratorTests
{
    private const string ComponentIface = "namespace Wyrd.Ecs { public interface IComponent { } }";
    private const string AbstractionsSource = """
        namespace Wyrd.Ecs.Debug.Abstractions
        {
            public abstract class InspectorField { }
            public struct InspectorEdit { }
            public interface IComponentInspectorRenderer<T> { }
            [System.AttributeUsage(System.AttributeTargets.Struct)]
            public sealed class DebugRendererAttribute : System.Attribute
            {
                public DebugRendererAttribute(System.Type rendererType) { }
            }
        }
        """;

    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();

    private static string RunGenerator(string consumerSource)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "Consumer",
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(ComponentIface),
                CSharpSyntaxTree.ParseText(AbstractionsSource),
                CSharpSyntaxTree.ParseText(consumerSource),
            ],
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new DebugRendererRegistrationGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        return driver.GetRunResult().Results[0].GeneratedSources.Single().SourceText.ToString();
    }

    [Fact]
    public void AStructWithDebugRenderer_EmitsARegistrationCallWithDescribeAndApply()
    {
        const string source = """
            using Wyrd.Ecs.Debug.Abstractions;

            namespace Test;

            public sealed class HealthRenderer : IComponentInspectorRenderer<Health> { }

            [DebugRenderer(typeof(HealthRenderer))]
            public struct Health : Wyrd.Ecs.IComponent { public int Current; }
            """;

        var generated = RunGenerator(source);

        generated.Should().Contain("global::Wyrd.Ecs.Debug.DebugRendererRegistry.Register");
        generated.Should().Contain("global::Test.Health");
        generated.Should().Contain("global::Test.HealthRenderer");
        generated.Should().Contain(".Describe(");
        generated.Should().Contain(".Apply(");
    }

    [Fact]
    public void AStructWithNoDebugRenderer_EmitsNoRegistrationForIt()
    {
        const string source = "public struct Plain : Wyrd.Ecs.IComponent { public int X; }";

        var generated = RunGenerator(source);

        generated.Should().NotContain("Plain");
    }
}
