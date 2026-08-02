using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Wyrd.Ecs.Generators.Tests;

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

    public static CSharpCompilation Compile(string source) => Compile([source]);

    /// <summary>
    /// Use this instead of <see cref="Compile(string)"/> when a test needs multiple source
    /// files: each string becomes its own syntax tree with a distinct <c>path</c>, since
    /// without one, <c>IsFileLocal</c> can't tell same-named `file`-scoped types apart
    /// across trees (they'd otherwise share the same blank path).
    /// </summary>
    public static CSharpCompilation Compile(params string[] sources) =>
        CSharpCompilation.Create(
            assemblyName: "GeneratorsTestAssembly",
            syntaxTrees: sources.Select((s, i) => CSharpSyntaxTree.ParseText(s, path: $"File{i}.cs")),
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
    /// the result (original + generated sources) to an in-memory assembly, and loads it.
    /// Use this instead of <see cref="Run"/> when a test needs to actually execute
    /// generated code against a real <c>World</c>, not just inspect generated source text.
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

    /// <summary>
    /// Wraps <paramref name="source"/> in a real workspace <see cref="Document"/>. Needed
    /// by anything driving a <c>CodeFixProvider</c> directly, since <see cref="Compile"/>'s
    /// plain <see cref="CSharpCompilation"/> has no <see cref="Document"/>/
    /// <see cref="Solution"/> wrapper a codefix can edit.
    /// </summary>
    public static Document CreateDocument(string source)
    {
        using var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var solution = workspace.CurrentSolution
            .AddProject(projectId, "GeneratorsTestAssembly", "GeneratorsTestAssembly", LanguageNames.CSharp)
            .AddMetadataReferences(projectId, References)
            .AddDocument(documentId, "Test.cs", SourceText.From(source));

        return solution.GetDocument(documentId)!;
    }
}
