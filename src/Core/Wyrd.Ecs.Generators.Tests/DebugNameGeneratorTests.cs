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
    public void TwoTypesSharingASimpleNameInDifferentNamespaces_BothRegister_NoThrow()
    {
        const string source = """
            using Wyrd.Ecs;
            namespace A { public struct Enemy : ITag { } }
            namespace B { public struct Enemy : ITag { } }
            """;

        var result = GeneratorTestHost.Run(new DebugNameGenerator(), GeneratorTestHost.Compile(source));

        result.Diagnostics.Should().BeEmpty();
        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("global::A.Enemy>(\"Enemy\");");
        generated.Should().Contain("global::B.Enemy>(\"Enemy\");");
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
}
