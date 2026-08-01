namespace Wyrd.Ecs.Generators.Tests;

public class TagAutoRegistrationGeneratorTests
{
    [Fact]
    public void NoTags_EmitsAnEmptyRegisterAll()
    {
        const string source = """
            namespace Test;
            public struct NotATag { public float X; }
            """;

        var result = GeneratorTestHost.Run(new TagAutoRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("public static void RegisterAll(global::Wyrd.Ecs.ComponentCodecRegistry registry)");
        generated.Should().NotContain("registry.RegisterTag<");
    }

    [Fact]
    public void ATag_IsRegisteredUnderItsSimpleName()
    {
        const string source = """
            using Wyrd.Ecs;
            namespace Test;
            public struct Enemy : ITag { }
            """;

        var result = GeneratorTestHost.Run(new TagAutoRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("registry.RegisterTag<global::Test.Enemy>(\"Enemy\");");
    }

    [Fact]
    public void ANonTagStruct_IsNotRegistered()
    {
        const string source = """
            using Wyrd.Ecs;
            namespace Test;
            public struct Position : IComponent { public float X; }
            """;

        var result = GeneratorTestHost.Run(new TagAutoRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().NotContain("registry.RegisterTag<");
    }

    [Fact]
    public void MultipleTags_RegistersEachOnce()
    {
        const string source = """
            using Wyrd.Ecs;
            namespace Test;
            public struct Enemy : ITag { }
            public struct Projectile : ITag { }
            """;

        var result = GeneratorTestHost.Run(new TagAutoRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("registry.RegisterTag<global::Test.Enemy>(\"Enemy\");");
        generated.Should().Contain("registry.RegisterTag<global::Test.Projectile>(\"Projectile\");");
    }

    [Fact]
    public void APrivateNestedTagStruct_IsNotRegistered()
    {
        // A generated public RegisterAll could never legally reference a private nested
        // type -- discovered via a real CS0122 build failure when this generator ran over
        // Wyrd.Ecs.Tests itself, which has several private ITag structs scattered across
        // unrelated test files for unrelated purposes.
        const string source = """
            using Wyrd.Ecs;
            namespace Test;
            public class Container
            {
                private struct Hidden : ITag { }
            }
            """;

        var result = GeneratorTestHost.Run(new TagAutoRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().NotContain("registry.RegisterTag<");
    }

    [Fact]
    public void AFileLocalTagStruct_IsNotRegistered()
    {
        const string source = """
            using Wyrd.Ecs;
            namespace Test;
            file struct Hidden : ITag { }
            """;

        var result = GeneratorTestHost.Run(new TagAutoRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().NotContain("registry.RegisterTag<");
    }

    [Fact]
    public void RegisterAll_WithTwoSameSimpleNameTagsInDifferentNamespaces_ThrowsWhenActuallyRun()
    {
        const string source = """
            using Wyrd.Ecs;

            namespace Test { public struct Enemy : ITag { } }
            namespace Other { public struct Enemy : ITag { } }

            public static class Harness
            {
                public static bool RegisterAllThrows()
                {
                    var registry = new global::Wyrd.Ecs.ComponentCodecRegistry();
                    try
                    {
                        global::Wyrd.Ecs.Generated.TagAutoRegistration.RegisterAll(registry);
                        return false;
                    }
                    catch (System.ArgumentException)
                    {
                        return true;
                    }
                }
            }
            """;

        var assembly = GeneratorTestHost.CompileAndLoad(new TagAutoRegistrationGenerator(), GeneratorTestHost.Compile(source));

        var threw = (bool)assembly.GetType("Harness")!.GetMethod("RegisterAllThrows")!.Invoke(null, null)!;

        threw.Should().BeTrue();
    }
}
