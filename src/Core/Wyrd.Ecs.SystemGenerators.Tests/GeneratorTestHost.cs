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

    /// <summary>
    /// Runs <paramref name="generator"/> against <paramref name="compilation"/>, emits
    /// the result (original + generated sources) to an in-memory assembly, and loads
    /// it — for tests that need to actually execute generated code against a real
    /// <c>World</c>, not just inspect generated source text.
    /// </summary>
    public static System.Reflection.Assembly CompileAndLoad(IIncrementalGenerator generator, Compilation compilation)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators: [generator.AsSourceGenerator()]);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out var diagnostics);

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
            throw new InvalidOperationException("Generator reported errors:\n" + string.Join("\n", errors));

        using var stream = new MemoryStream();
        var result = updatedCompilation.Emit(stream);
        if (!result.Success)
        {
            var compileErrors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
            throw new InvalidOperationException("Emit failed:\n" + string.Join("\n", compileErrors));
        }

        stream.Seek(0, SeekOrigin.Begin);
        return System.Reflection.Assembly.Load(stream.ToArray());
    }
}
