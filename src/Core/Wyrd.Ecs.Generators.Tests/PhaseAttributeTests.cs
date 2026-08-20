namespace Wyrd.Ecs.Generators.Tests;

public class PhaseAttributeTests
{
    private const string PreUpdateHarness = """
        using System;
        using Wyrd.Ecs;

        [Phase(Phase.PreUpdate)]
        public sealed class PreUpdateSystem : EcsSystem
        {
            protected override void Execute(World world, Time time) { }
        }

        public static class Harness
        {
            public static (Type[] Before, Type[] After) Run()
            {
                var (before, after) = Wyrd.Ecs.Generated.SystemRegistry.Edges.TryGetValue(typeof(PreUpdateSystem), out var edges)
                    ? edges
                    : (Array.Empty<Type>(), Array.Empty<Type>());
                return ([.. before], [.. after]);
            }
        }
        """;

    [Fact]
    public void PreUpdateAttribute_ProducesABeforeEdgeToStartOfUpdatePhase()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(PreUpdateHarness));

        var (before, after) = ((Type[], Type[]))assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;

        before.Should().Contain(t => t.Name == "StartOfUpdatePhase");
        after.Should().BeEmpty();
    }

    private const string PostUpdateHarness = """
        using System;
        using Wyrd.Ecs;

        [Phase(Phase.PostUpdate)]
        public sealed class PostUpdateSystem : EcsSystem
        {
            protected override void Execute(World world, Time time) { }
        }

        public static class Harness
        {
            public static (Type[] Before, Type[] After) Run()
            {
                var (before, after) = Wyrd.Ecs.Generated.SystemRegistry.Edges.TryGetValue(typeof(PostUpdateSystem), out var edges)
                    ? edges
                    : (Array.Empty<Type>(), Array.Empty<Type>());
                return ([.. before], [.. after]);
            }
        }
        """;

    [Fact]
    public void PostUpdateAttribute_ProducesAnAfterEdgeToEndOfUpdatePhase()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(PostUpdateHarness));

        var (before, after) = ((Type[], Type[]))assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;

        after.Should().Contain(t => t.Name == "EndOfUpdatePhase");
        before.Should().BeEmpty();
    }

    private const string UpdateHarness = """
        using System;
        using Wyrd.Ecs;

        [Phase(Phase.Update)]
        public sealed class ExplicitUpdateSystem : EcsSystem
        {
            protected override void Execute(World world, Time time) { }
        }

        public static class Harness
        {
            public static bool HasAnyEdgeEntry() =>
                Wyrd.Ecs.Generated.SystemRegistry.Edges.ContainsKey(typeof(ExplicitUpdateSystem));
        }
        """;

    [Fact]
    public void ExplicitPhaseUpdateAttribute_IsAGenuineNoOp_NoRegistryEntryAtAll()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(UpdateHarness));

        var hasEntry = (bool)assembly.GetType("Harness")!.GetMethod("HasAnyEdgeEntry")!.Invoke(null, null)!;

        hasEntry.Should().BeFalse("Phase.Update must be indistinguishable from declaring no [Phase] attribute at all");
    }

    private const string ComposesHarness = """
        using System;
        using Wyrd.Ecs;

        public sealed class SomeConcreteSystem : EcsSystem
        {
            protected override void Execute(World world, Time time) { }
        }

        [Phase(Phase.PreUpdate)]
        [RunAfter(typeof(SomeConcreteSystem))]
        public sealed class PreUpdateAfterConcreteSystem : EcsSystem
        {
            protected override void Execute(World world, Time time) { }
        }

        public static class Harness
        {
            public static (Type[] Before, Type[] After) Run()
            {
                var (before, after) = Wyrd.Ecs.Generated.SystemRegistry.Edges.TryGetValue(typeof(PreUpdateAfterConcreteSystem), out var edges)
                    ? edges
                    : (Array.Empty<Type>(), Array.Empty<Type>());
                return ([.. before], [.. after]);
            }
        }
        """;

    [Fact]
    public void PhaseAttribute_ComposesWithAnExplicitRunAfterEdgeOnTheSameClass()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(ComposesHarness));

        var (before, after) = ((Type[], Type[]))assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;

        before.Should().Contain(t => t.Name == "StartOfUpdatePhase");
        after.Should().Contain(t => t.Name == "SomeConcreteSystem");
    }
}
