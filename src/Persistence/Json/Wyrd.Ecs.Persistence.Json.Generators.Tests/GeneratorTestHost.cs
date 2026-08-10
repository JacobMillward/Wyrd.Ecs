using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wyrd.Ecs.Persistence.Json.Generators.Tests;

internal static class GeneratorTestHost
{
    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(IComponent).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(typeof(Wyrd.Ecs.Persistence.PersistenceIgnoreAttribute).Assembly.Location))
            .ToArray();

    public static CSharpCompilation Compile(string source, string assemblyName = "PersistenceJsonGeneratorsTestAssembly") =>
        CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    public static GeneratorDriverRunResult Run(
        IIncrementalGenerator generator,
        Compilation compilation,
        IReadOnlyDictionary<string, string>? globalOptions = null,
        bool trackSteps = false)
    {
        AnalyzerConfigOptionsProvider? optionsProvider = globalOptions is { Count: > 0 }
            ? new TestAnalyzerConfigOptionsProvider(new TestAnalyzerConfigOptions(globalOptions))
            : null;

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            optionsProvider: optionsProvider,
            driverOptions: new GeneratorDriverOptions(trackIncrementalGeneratorSteps: trackSteps));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        return driver.GetRunResult();
    }

    // MSBuild CompilerVisibleProperty items surface to a generator as build_property.<Name>
    // keys on AnalyzerConfigOptionsProvider.GlobalOptions. These two classes are the minimal
    // concrete implementation the real Roslyn/MSBuild pipeline provides at compile time.
    private sealed class TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            if (values.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }
            value = "";
            return false;
        }
    }

    private sealed class TestAnalyzerConfigOptionsProvider(AnalyzerConfigOptions globalOptions) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions => globalOptions;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => globalOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => globalOptions;
    }
}
