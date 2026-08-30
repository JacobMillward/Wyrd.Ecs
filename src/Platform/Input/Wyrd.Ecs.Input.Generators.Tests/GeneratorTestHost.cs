using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Wyrd.Ecs.Input.Generators.Tests;

/// <summary>
/// Builds a compilation referencing every trusted platform assembly plus the real
/// <c>Wyrd.Ecs.dll</c> and <c>Wyrd.Ecs.Input.dll</c>, so the analyzer's semantic-model-based
/// checks (matching <c>Wyrd.Ecs.Input.ActionState</c>/<c>Wyrd.Ecs.FixedTimestepAttribute</c>
/// by symbol) see the genuine types. Mirrors <c>Wyrd.Ecs.Generators.Tests.GeneratorTestHost</c>.
/// </summary>
internal static class GeneratorTestHost
{
    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(IComponent).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(typeof(ActionState).Assembly.Location))
            .ToArray();

    public static CSharpCompilation Compile(string source) =>
        CSharpCompilation.Create(
            assemblyName: "InputGeneratorsTestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, path: "Test.cs")],
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
}
