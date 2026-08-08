using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Wyrd.Ecs.Persistence.Binary.Generators.Tests;

public class MemoryPackRegistrationGeneratorTests
{
    [Fact]
    public void NoComponents_EmitsAnEmptyRegisterAll()
    {
        const string source = """
            using Wyrd.Ecs;

            public static class NotAComponentAtAll { }
            """;

        var result = GeneratorTestHost.Run(new MemoryPackRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("public static void RegisterAll(global::Wyrd.Ecs.CodecRegistry registry)");
        generated.Should().NotContain("registry.Register<");
    }

    [Fact]
    public void UnmanagedComponentWithNoAttribute_RegistersIt()
    {
        const string source = """
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; public float Y; }
            """;

        var result = GeneratorTestHost.Run(new MemoryPackRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("registry.Register<global::Position>(\"Position\",");
        generated.Should().Contain("global::MemoryPack.MemoryPackSerializer.Serialize(v)");
    }

    [Fact]
    public void ComponentMarkedPersistenceIgnore_IsNotRegistered()
    {
        const string source = """
            using Wyrd.Ecs;
            using Wyrd.Ecs.Persistence;

            [PersistenceIgnore]
            public struct Position : IComponent { public float X; }
            """;

        var result = GeneratorTestHost.Run(new MemoryPackRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().NotContain("registry.Register<");
    }

    [Fact]
    public void ComponentWithStringFieldAndNoAttribute_GeneratesAFormatterAndRegistersIt()
    {
        const string source = """
            using Wyrd.Ecs;

            public struct Named : IComponent { public string Value; public int Count; }
            """;

        var result = GeneratorTestHost.Run(new MemoryPackRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("registry.Register<global::Named>(\"Named\",");
        generated.Should().Contain(": global::MemoryPack.MemoryPackFormatter<global::Named>");
        generated.Should().Contain("global::MemoryPack.MemoryPackFormatterProvider.Register(");
        generated.Should().Contain("[System.Runtime.CompilerServices.ModuleInitializer]");
    }

    [Fact]
    public void ComponentWithNestedPlainStructField_GeneratesAFormatterForBothTypesExactlyOnce()
    {
        const string source = """
            using Wyrd.Ecs;

            public struct Label { public string Text; }
            public struct Named : IComponent { public Label Label; }
            """;

        var result = GeneratorTestHost.Run(new MemoryPackRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain(": global::MemoryPack.MemoryPackFormatter<global::Named>");
        generated.Should().Contain(": global::MemoryPack.MemoryPackFormatter<global::Label>");
        System.Text.RegularExpressions.Regex.Matches(generated, "MemoryPackFormatter<global::Label>").Count.Should().Be(1);
    }

    [Fact]
    public void ComponentWithArrayAndListFields_GeneratesOneFormatterForTheComponentOnly()
    {
        const string source = """
            using System.Collections.Generic;
            using Wyrd.Ecs;

            public struct Inventory : IComponent { public string[] Tags; public List<int> Counts; }
            """;

        var result = GeneratorTestHost.Run(new MemoryPackRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain(": global::MemoryPack.MemoryPackFormatter<global::Inventory>");
        generated.Should().NotContain("MemoryPackFormatter<global::System.String[]");
        generated.Should().NotContain("MemoryPackFormatter<global::System.Collections.Generic.List<global::System.Int32>>");
    }

    [Fact]
    public void ComponentWithInterfaceTypedField_ReportsWYRD006_DoesNotEmitAFormatter()
    {
        const string source = """
            using Wyrd.Ecs;

            public interface IHandler { }

            public struct WithHandler : IComponent { public IHandler Handler; }
            """;

        var result = GeneratorTestHost.Run(new MemoryPackRegistrationGenerator(), GeneratorTestHost.Compile(source));

        result.Diagnostics.Should().ContainSingle(d => d.Id == "WYRD006");
        result.Diagnostics.Single(d => d.Id == "WYRD006").GetMessage().Should()
            .Contain("WithHandler.Handler").And.Contain("IHandler");

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().NotContain("registry.Register<global::WithHandler>");
        generated.Should().NotContain("MemoryPackFormatter<global::WithHandler>");
    }

    [Fact]
    public void MemoryPackableComponent_RegistersIt()
    {
        const string source = """
            using MemoryPack;
            using Wyrd.Ecs;

            [MemoryPackable]
            public partial struct Position : IComponent { public float X; }
            """;

        var result = GeneratorTestHost.Run(new MemoryPackRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("registry.Register<global::Position>(\"Position\",");
        generated.Should().Contain("global::MemoryPack.MemoryPackSerializer.Serialize(v)");
        generated.Should().Contain("global::MemoryPack.MemoryPackSerializer.Deserialize<global::Position>(bytes)");
    }

    [Fact]
    public void NonComponentStructWithMemoryPackable_IsNotRegistered()
    {
        const string source = """
            using MemoryPack;
            using Wyrd.Ecs;

            [MemoryPackable]
            public partial struct NotAComponent { public float X; }
            """;

        var result = GeneratorTestHost.Run(new MemoryPackRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().NotContain("registry.Register<");
    }

    [Fact]
    public void MultipleComponents_RegistersEachOnce()
    {
        const string source = """
            using MemoryPack;
            using Wyrd.Ecs;

            [MemoryPackable]
            public partial struct Position : IComponent { public float X; }

            [MemoryPackable]
            public partial struct Velocity : IComponent { public float X; }
            """;

        var result = GeneratorTestHost.Run(new MemoryPackRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("registry.Register<global::Position>(\"Position\",");
        generated.Should().Contain("registry.Register<global::Velocity>(\"Velocity\",");
    }

    [Fact]
    public void AlwaysEmitsBothAddBinaryPersistenceOverloads()
    {
        const string source = """
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }
            """;

        var result = GeneratorTestHost.Run(new MemoryPackRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("public global::Wyrd.Ecs.WorldBuilder AddBinaryPersistence(global::Wyrd.Ecs.Persistence.IPersistenceStore store)");
        generated.Should().Contain("public global::Wyrd.Ecs.WorldBuilder AddBinaryPersistence(string path)");
    }

    [Fact]
    public void StableName_OverridesTheDefaultDiscriminator()
    {
        const string source = """
            using MemoryPack;
            using Wyrd.Ecs;

            [MemoryPackable]
            [StableName("Enemy")]
            public partial struct OldEnemyTypeName : IComponent { }
            """;

        var result = GeneratorTestHost.Run(new MemoryPackRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("registry.Register<global::OldEnemyTypeName>(\"Enemy\",");
        generated.Should().NotContain("\"OldEnemyTypeName\"");
    }

    [Fact]
    public void RenamedFrom_EmitsRegisterAlias()
    {
        const string source = """
            using MemoryPack;
            using Wyrd.Ecs;

            [MemoryPackable]
            [RenamedFrom("Old.A")]
            [RenamedFrom("Old.B")]
            public partial struct Enemy : IComponent { }
            """;

        var result = GeneratorTestHost.Run(new MemoryPackRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("registry.RegisterAlias(\"Old.A\", \"Enemy\");");
        generated.Should().Contain("registry.RegisterAlias(\"Old.B\", \"Enemy\");");
    }

    [Fact]
    public void EditingAnUnrelatedMethodBody_LeavesTheCandidateStepUnchanged()
    {
        const string sourceV1 = """
            using MemoryPack;
            using Wyrd.Ecs;

            [MemoryPackable]
            public partial struct Position : IComponent { public float X; }

            public static class Unrelated
            {
                public static int Compute() => 1;
            }
            """;

        const string sourceV2 = """
            using MemoryPack;
            using Wyrd.Ecs;

            [MemoryPackable]
            public partial struct Position : IComponent { public float X; }

            public static class Unrelated
            {
                public static int Compute() => 2;
            }
            """;

        var generator = new MemoryPackRegistrationGenerator().AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            driverOptions: new GeneratorDriverOptions(trackIncrementalGeneratorSteps: true));

        var compilationV1 = GeneratorTestHost.Compile(sourceV1);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilationV1, out _, out _);

        var originalTree = compilationV1.SyntaxTrees.Single();
        var editedTree = originalTree.WithChangedText(Microsoft.CodeAnalysis.Text.SourceText.From(sourceV2));
        var compilationV2 = compilationV1.ReplaceSyntaxTree(originalTree, editedTree);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilationV2, out _, out _);

        var steps = driver.GetRunResult().Results[0].TrackedSteps["RegisteredComponentInfo"];
        steps.Should().ContainSingle();
        steps[0].Outputs.Should().Contain(o =>
            o.Reason == IncrementalStepRunReason.Cached || o.Reason == IncrementalStepRunReason.Unchanged);
    }
}
