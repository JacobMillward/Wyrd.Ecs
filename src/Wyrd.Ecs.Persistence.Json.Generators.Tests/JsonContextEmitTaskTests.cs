using Microsoft.Build.Utilities;

namespace Wyrd.Ecs.Persistence.Json.Generators.Tests;

public class JsonContextEmitTaskTests : IDisposable
{
    private static readonly string[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Append(typeof(IComponent).Assembly.Location)
            .Append(typeof(JsonPersistenceIgnoreAttribute).Assembly.Location)
            .ToArray();

    private readonly string _sourceDir = Path.Combine(Path.GetTempPath(), $"wyrd-json-emit-{Guid.NewGuid():N}");
    private readonly string _outputPath;

    public JsonContextEmitTaskTests()
    {
        Directory.CreateDirectory(_sourceDir);
        _outputPath = Path.Combine(_sourceDir, "Output.g.cs");
    }

    public void Dispose()
    {
        if (Directory.Exists(_sourceDir)) Directory.Delete(_sourceDir, recursive: true);
    }

    private string WriteSource(string fileName, string content)
    {
        var path = Path.Combine(_sourceDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static TaskItem[] ToItems(IEnumerable<string> paths) =>
        paths.Select(p => new TaskItem(p)).ToArray();

    private bool RunTask(IEnumerable<string> sourcePaths, out string output)
    {
        var task = new JsonContextEmitTask
        {
            Compile = ToItems(sourcePaths),
            References = ToItems(References),
            AssemblyName = "TestAssembly",
            OutputPath = _outputPath,
        };

        var result = task.Execute();
        output = File.Exists(_outputPath) ? File.ReadAllText(_outputPath) : "";
        return result;
    }

    [Fact]
    public void Execute_FindsAStructImplementingIComponent()
    {
        var path = WriteSource("Position.cs", """
            using Wyrd.Ecs;
            public struct Position : IComponent { public float X; }
            """);

        RunTask([path], out var output).Should().BeTrue();

        output.Should().Contain("typeof(global::Position)");
    }

    [Fact]
    public void Execute_SkipsAStructNotImplementingIComponent()
    {
        var path = WriteSource("NotAComponent.cs", """
            public struct NotAComponent { public float X; }
            """);

        RunTask([path], out var output);

        output.Should().NotContain("NotAComponent");
    }

    [Fact]
    public void Execute_SkipsAComponentMarkedJsonPersistenceIgnore()
    {
        var path = WriteSource("Secret.cs", """
            using Wyrd.Ecs;
            using Wyrd.Ecs.Persistence.Json;
            [JsonPersistenceIgnore]
            public struct Secret : IComponent { public string Value; }
            """);

        RunTask([path], out var output);

        output.Should().NotContain("Secret");
    }

    [Fact]
    public void Execute_EmitsIncludeFieldsOption()
    {
        var path = WriteSource("Position.cs", """
            using Wyrd.Ecs;
            public struct Position : IComponent { public float X; }
            """);

        RunTask([path], out var output);

        output.Should().Contain("[JsonSourceGenerationOptions(IncludeFields = true)]");
    }

    [Fact]
    public void Execute_UsesTheAssemblyNameToDeriveTheContextClassName()
    {
        RunTask([], out var output);

        output.Should().Contain("public partial class TestAssemblyJsonPersistenceContext : JsonSerializerContext");
    }

    [Fact]
    public void Execute_GivesTwoSameSimpleNameTypesDistinctTypeInfoPropertyNames()
    {
        var first = WriteSource("Position1.cs", """
            using Wyrd.Ecs;
            namespace Foo { public struct Position : IComponent { public float X; } }
            """);
        var second = WriteSource("Position2.cs", """
            using Wyrd.Ecs;
            namespace Bar { public struct Position : IComponent { public float X; } }
            """);

        RunTask([first, second], out var output);

        output.Should().Contain("TypeInfoPropertyName = \"Foo_Position\"");
        output.Should().Contain("TypeInfoPropertyName = \"Bar_Position\"");
    }
}
