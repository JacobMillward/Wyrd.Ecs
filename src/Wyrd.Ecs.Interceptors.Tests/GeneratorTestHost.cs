using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Wyrd.Ecs.Interceptors.Tests;

internal static class GeneratorTestHost
{
    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(IComponent).Assembly.Location))
            .ToArray();

    public static CSharpCompilation Compile(params string[] sources) =>
        CSharpCompilation.Create(
            assemblyName: "InterceptorsTestAssembly",
            syntaxTrees: sources.Select(s => CSharpSyntaxTree.ParseText(s, new CSharpParseOptions(LanguageVersion.Preview))),
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

    public static GeneratorDriverRunResult Run(IIncrementalGenerator generator, Compilation compilation, bool trackSteps = false)
    {
        // Generated sources are parsed with the driver's own parseOptions, which must match the
        // input compilation's LanguageVersion.Preview or CSharpCompilation throws "Inconsistent
        // language versions" the moment a generated tree is added.
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview),
            driverOptions: new GeneratorDriverOptions(trackIncrementalGeneratorSteps: trackSteps));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        return driver.GetRunResult();
    }
}
