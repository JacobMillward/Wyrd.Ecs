using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Persistence.Json.Generators;

/// <summary>
/// MSBuild task, wired to run <c>BeforeTargets="CoreCompile"</c> by
/// <c>build/Wyrd.Ecs.Persistence.Json.Generators.targets</c>. Builds an ad hoc
/// <see cref="CSharpCompilation"/> from the consuming project's own <c>@(Compile)</c>
/// items, scans it for every <c>Wyrd.Ecs.IComponent</c> implementer, including types
/// marked <see cref="Wyrd.Ecs.Persistence.PersistenceIgnoreAttribute"/> (whether a type can
/// be serialized and whether it's wired into a given CodecRegistry are different questions;
/// only <see cref="JsonRegistrationGenerator"/> enforces the ignore semantics), and
/// materializes a <c>[JsonSerializable]</c>-decorated <c>JsonSerializerContext</c>
/// partial class to disk before <c>CoreCompile</c> runs, so System.Text.Json's own source
/// generator processes it like any hand-written source. This materialize-then-let-STJ-run
/// step exists because one Roslyn generator cannot see another generator's output within
/// the same compilation (dotnet/roslyn#77560): an ordinary <c>IIncrementalGenerator</c>
/// can't populate <c>[JsonSerializable]</c> on STJ's own context class the way
/// <see cref="JsonRegistrationGenerator"/> populates <c>CodecRegistry</c>.
/// </summary>
// RS1035 bans file IO for analyzer assemblies. This class is an MSBuild Task, not an
// analyzer, and file IO is its entire job: the ban doesn't apply, it just can't tell
// the two roles apart within one assembly.
#pragma warning disable RS1035
public sealed class JsonContextEmitTask : Microsoft.Build.Utilities.Task
{
    [Required] public ITaskItem[] Compile { get; set; } = [];
    [Required] public ITaskItem[] References { get; set; } = [];
    [Required] public string AssemblyName { get; set; } = "";
    [Required] public string OutputPath { get; set; } = "";

    public override bool Execute()
    {
        var trees = Compile
            .Select(item => item.ItemSpec)
            .Where(File.Exists)
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path))
            .ToList();

        var references = References
            .Select(item => item.ItemSpec)
            .Where(File.Exists)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        var compilation = CSharpCompilation.Create(
            assemblyName: "WyrdJsonPersistenceScan",
            syntaxTrees: trees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var componentTypeNames = new List<string>();
        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var structDeclaration in tree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(structDeclaration) is not INamedTypeSymbol symbol) continue;
                if (!symbol.AllInterfaces.Any(i => i.ToDisplayString() == "Wyrd.Ecs.IComponent")) continue;

                componentTypeNames.Add(symbol.ToDisplayString());
            }
        }

        // A JsonSerializerContext with no [JsonSerializable] entries never gets its
        // abstract members completed by System.Text.Json's own generator, so it fails to
        // compile - and JsonRegistrationGenerator's RegisterAll never references this
        // class when there's nothing to register anyway. Skip emitting it entirely rather
        // than write something guaranteed uncompilable, and remove a stale one from an
        // earlier build where components existed but don't anymore.
        if (componentTypeNames.Count == 0)
        {
            if (File.Exists(OutputPath)) File.Delete(OutputPath);
            return true;
        }

        var contextClassName = ConsumerContextNaming.ContextClassName(AssemblyName);

        var sb = new StringBuilder();
        sb.AppendLine("using System.Text.Json.Serialization;");
        foreach (var name in componentTypeNames)
        {
            var propertyName = ConsumerContextNaming.TypeInfoPropertyName(name);
            sb.AppendLine($"[JsonSerializable(typeof(global::{name}), TypeInfoPropertyName = \"{propertyName}\")]");
        }
        sb.AppendLine("[JsonSourceGenerationOptions(IncludeFields = true)]");
        sb.AppendLine($"public partial class {contextClassName} : JsonSerializerContext {{ }}");

        var directory = Path.GetDirectoryName(OutputPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(OutputPath, sb.ToString());

        return true;
    }
}
#pragma warning restore RS1035
