namespace Wyrd.Ecs.Generators.Tests;

/// <summary>
/// Regression test for <c>QueryChainEmitter.ParamName</c>: deriving a lambda parameter
/// name from a generic component type's fully-qualified name used to find the *last* '.'
/// anywhere in the string, which can land inside a generic argument's own namespace --
/// producing a garbage identifier that includes the argument's closing '&gt;'. Exercises
/// this directly with a small generic wrapper component, independent of any specific
/// feature that happens to use one.
/// </summary>
public class QueryChainGeneratorGenericComponentTests
{
    private const string Harness = """
        using Wyrd.Ecs;

        namespace Some.Nested.Namespace
        {
            public struct Payload : IComponent { public float Value; }
        }

        public struct Box<T> : IComponent where T : struct, IComponent
        {
            public T Inner;
        }

        public static class Harness
        {
            public static float Run()
            {
                var world = new World();
                world.Commands.CreateEntity(new Box<Some.Nested.Namespace.Payload> { Inner = new Some.Nested.Namespace.Payload { Value = 7f } });
                world.ApplyCommands();

                var total = 0f;
                world.Query().With<Box<Some.Nested.Namespace.Payload>>()
                    .ForEach(0, (in int _, in Box<Some.Nested.Namespace.Payload> box) => total = box.Inner.Value);

                return total;
            }
        }
        """;

    [Fact]
    public void ForEach_WithAGenericComponentTypeWhoseArgumentIsNamespaceQualified_CompilesAndExecutesCorrectly()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (float)assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;

        result.Should().Be(7f);
    }
}
