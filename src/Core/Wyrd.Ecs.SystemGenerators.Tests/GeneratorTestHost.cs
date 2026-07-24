using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Wyrd.Ecs.SystemGenerators.Tests;

/// <summary>
/// Builds a compilation referencing every trusted platform assembly plus the real
/// <c>Wyrd.Ecs.dll</c>, so a generator's semantic-model-based predicates (matching
/// against <c>Wyrd.Ecs.QuerySystem&lt;...&gt;</c> by symbol, not by source text) see
/// the genuine types instead of a hand-rolled stand-in.
/// </summary>
internal static class GeneratorTestHost
{
    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(IComponent).Assembly.Location))
            .ToArray();

    public static CSharpCompilation Compile(string source) =>
        CSharpCompilation.Create(
            assemblyName: "SystemGeneratorsTestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    public static GeneratorDriverRunResult Run(IIncrementalGenerator generator, Compilation compilation, bool trackSteps = false)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(trackIncrementalGeneratorSteps: trackSteps));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        return driver.GetRunResult();
    }
}
