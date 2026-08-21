namespace Wyrd.Ecs.Generators.Tests;

public class DebugNameGeneratorTests
{
    [Fact]
    public void ComponentTagAndRelation_AllGetRegistered()
    {
        const string source = """
            using Wyrd.Ecs;
            namespace Test;
            public struct Position : IComponent { public float X; }
            public struct Enemy : ITag { }
            public struct Likes : IRelation { }
            """;

        var result = GeneratorTestHost.Run(new DebugNameGenerator(), GeneratorTestHost.Compile(source));
        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();

        generated.Should().Contain("[System.Runtime.CompilerServices.ModuleInitializer]");
        generated.Should().Contain("Wyrd.Ecs.Internal.DebugNameRegistry.Register<global::Test.Position>(\"Position\");");
        generated.Should().Contain("Wyrd.Ecs.Internal.DebugNameRegistry.Register<global::Test.Enemy>(\"Enemy\");");
        generated.Should().Contain("Wyrd.Ecs.Internal.DebugNameRegistry.Register<global::Test.Likes>(\"Likes\");");
    }

    [Fact]
    public void TwoTypesSharingASimpleNameInDifferentNamespaces_BothGetNamespaceQualified()
    {
        const string source = """
            using Wyrd.Ecs;
            namespace A { public struct Enemy : ITag { } }
            namespace B { public struct Enemy : ITag { } }
            """;

        var result = GeneratorTestHost.Run(new DebugNameGenerator(), GeneratorTestHost.Compile(source));

        result.Diagnostics.Should().BeEmpty();
        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("global::A.Enemy>(\"A.Enemy\");");
        generated.Should().Contain("global::B.Enemy>(\"B.Enemy\");");
    }

    [Fact]
    public void TwoNestedTypesSharingASimpleName_BothGetContainingTypeQualified()
    {
        const string source = """
            using Wyrd.Ecs;
            namespace Test;
            public class Outer1 { public struct Health : IComponent { public int Current; } }
            public class Outer2 { public struct Health : IComponent { public int Current; } }
            """;

        var result = GeneratorTestHost.Run(new DebugNameGenerator(), GeneratorTestHost.Compile(source));

        result.Diagnostics.Should().BeEmpty();
        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("global::Test.Outer1.Health>(\"Outer1.Health\");");
        generated.Should().Contain("global::Test.Outer2.Health>(\"Outer2.Health\");");
    }

    [Fact]
    public void AUniquelyNamedType_RegistersUnderItsBareName()
    {
        const string source = """
            using Wyrd.Ecs;
            namespace Test;
            public struct OnlyOne : ITag { }
            """;

        var result = GeneratorTestHost.Run(new DebugNameGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("global::Test.OnlyOne>(\"OnlyOne\");");
    }

    [Fact]
    public void APrivateNestedType_IsNotRegistered()
    {
        const string source = """
            using Wyrd.Ecs;
            namespace Test;
            public class Container
            {
                private struct Hidden : ITag { }
            }
            """;

        var result = GeneratorTestHost.Run(new DebugNameGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().NotContain("Hidden");
    }

    [Fact]
    public void AFileLocalType_IsNotRegistered()
    {
        const string source = """
            using Wyrd.Ecs;
            namespace Test;
            file struct Hidden : ITag { }
            """;

        var result = GeneratorTestHost.Run(new DebugNameGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().NotContain("Hidden");
    }

    [Fact]
    public void ANonMatchingStruct_IsNotRegistered()
    {
        const string source = """
            namespace Test;
            public struct NotTracked { public float X; }
            """;

        var result = GeneratorTestHost.Run(new DebugNameGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().NotContain("NotTracked");
    }

    [Fact]
    public void AStructWithSystemManaged_AlsoEmitsASystemManagedRegistryCall()
    {
        const string source = """
            using Wyrd.Ecs;
            namespace Test;
            [SystemManaged]
            public struct Internal : IComponent { public int X; }
            """;

        var result = GeneratorTestHost.Run(new DebugNameGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("Wyrd.Ecs.Internal.SystemManagedRegistry.Register(\"Internal\");");
    }

    [Fact]
    public void AStructWithoutSystemManaged_EmitsNoSystemManagedRegistryCallForIt()
    {
        const string source = """
            using Wyrd.Ecs;
            namespace Test;
            public struct Ordinary : IComponent { public int X; }
            """;

        var result = GeneratorTestHost.Run(new DebugNameGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().NotContain("SystemManagedRegistry.Register(\"Ordinary\")");
    }
}
