using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Wyrd.Ecs.Persistence.Json.Generators.Tests;

internal static class GeneratorTestHost
{
    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(IComponent).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(typeof(JsonPersistenceIgnoreAttribute).Assembly.Location))
            .ToArray();

    public static CSharpCompilation Compile(string source, string assemblyName = "PersistenceJsonGeneratorsTestAssembly") =>
        CSharpCompilation.Create(
            assemblyName: assemblyName,
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
