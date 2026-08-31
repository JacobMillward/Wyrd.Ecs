namespace Wyrd.Ecs.Generators.Tests;

public class QueryChainGeneratorSystemAccessTests
{
    private const string Harness = """
        using System;
        using System.Collections.Generic;
        using Wyrd.Ecs;
        using Wyrd.Ecs.Generated;

        public struct Position : IComponent { public float X; }
        public struct Velocity : IComponent { public float X; }
        public struct Health : IComponent { public float Current; }
        public struct Score : IResource { public int Value; }

        public sealed class MovementSystem : EcsSystem
        {
            protected override void Execute(World world, Time time) =>
                world.Query().With<Position>().With<Velocity>().ForEach(time, (in Time t, ref Position p, in Velocity v) => { });
        }

        public sealed class MultiQuerySystem : EcsSystem
        {
            protected override void Execute(World world, Time time)
            {
                world.Query().With<Health>().ForEach(time, (in Time t, ref Health h) => { });
                world.Query().With<Position>().ForEach(time, (in Time t, in Position p) => { });
            }
        }

        public sealed class OtherSystem : EcsSystem
        {
            protected override void Execute(World world, Time time) { }
        }

        [RunBefore(typeof(OtherSystem))]
        [RunAfter(typeof(OtherSystem))]
        public sealed class DecoratedSystem : EcsSystem
        {
            protected override void Execute(World world, Time time) { }
        }

        public sealed class WorldCtorSystem : EcsSystem
        {
            public readonly World ReceivedWorld;
            public WorldCtorSystem(World world) => ReceivedWorld = world;
            protected override void Execute(World world, Time time) { }
        }

        public sealed class UnconstructableSystem : EcsSystem
        {
            public UnconstructableSystem(int amount) { }
            protected override void Execute(World world, Time time) { }
        }

        [FixedTimestep]
        public sealed class FixedCadenceSystem : EcsSystem
        {
            protected override void Execute(World world, Time time) { }
        }

        public sealed class HelperMethodSystem : EcsSystem
        {
            protected override void Execute(World world, Time time) => Step(world, time);
            private void Step(World world, Time time) =>
                world.Query().With<Health>().ForEach(time, (in Time t, ref Health h) => { });
        }

        public sealed partial class DeclaredResourceSystem : EcsSystem
        {
            [Resource] public partial Score Score { get; set; }
            protected override void Execute(World world, Time time) { }
        }

        public sealed class AdHocResourceSystem : EcsSystem
        {
            protected override void Execute(World world, Time time) => world.GetResourceRef<Score>().Value = 1;
        }

        public sealed class AdHocReadOnlyResourceSystem : EcsSystem
        {
            protected override void Execute(World world, Time time)
            {
                var score = world.GetResource<Score>();
            }
        }

        // Position is also used via MultiQuerySystem's .ForEach() above: this shares that
        // shape's backend rather than needing its own, exercising the guard in
        // EmitTrySingleOverloads that skips re-emitting an already-covered backend.
        public sealed class TrySinglePositionSystem : EcsSystem
        {
            protected override void Execute(World world, Time time)
            {
                if (world.Query().With<Position>().TrySingle(out var position)) { }
            }
        }

        public static class Harness
        {
            public static Type[] HelperMethodWrites() =>
                new List<Type>(SystemRegistry.Access[typeof(HelperMethodSystem)].Writes).ToArray();

            public static Type[] TrySinglePositionReads() =>
                new List<Type>(SystemRegistry.Access[typeof(TrySinglePositionSystem)].Reads).ToArray();

            public static Type[] DeclaredResourceWrites() =>
                new List<Type>(SystemRegistry.Access[typeof(DeclaredResourceSystem)].Writes).ToArray();

            public static Type[] AdHocResourceWrites() =>
                new List<Type>(SystemRegistry.Access[typeof(AdHocResourceSystem)].Writes).ToArray();

            public static Type[] AdHocReadOnlyResourceReads() =>
                new List<Type>(SystemRegistry.Access[typeof(AdHocReadOnlyResourceSystem)].Reads).ToArray();

            public static (Type[] Keys, Type[] MovementReads, Type[] MovementWrites, Type[] MultiReads, Type[] MultiWrites) Run()
            {
                var keys = new List<Type>(SystemRegistry.Access.Keys).ToArray();

                var movement = SystemRegistry.Access[typeof(MovementSystem)];
                var multi = SystemRegistry.Access[typeof(MultiQuerySystem)];

                return (keys,
                    new List<Type>(movement.Reads).ToArray(), new List<Type>(movement.Writes).ToArray(),
                    new List<Type>(multi.Reads).ToArray(), new List<Type>(multi.Writes).ToArray());
            }

            public static bool AdHocChain_GetsNoEntry()
            {
                var world = new World();
                world.Query().With<Position>().ForEach(0, (in int _, in Position p) => { });
                return SystemRegistry.Access.Count == 7; // MovementSystem, MultiQuerySystem, HelperMethodSystem, DeclaredResourceSystem, AdHocResourceSystem, AdHocReadOnlyResourceSystem, TrySinglePositionSystem: this ad-hoc call adds nothing
            }

            public static (Type[] Before, Type[] After) Edges()
            {
                var edges = SystemRegistry.Edges[typeof(DecoratedSystem)];
                return (new List<Type>(edges.Before).ToArray(), new List<Type>(edges.After).ToArray());
            }

            public static (bool ParameterlessConstructed, bool WorldCtorConstructed, bool WorldCtorReceivedTheSameWorld) Construct()
            {
                var world = new World();

                var parameterless = SystemRegistry.Construct[typeof(MovementSystem)](world);
                var worldCtor = (WorldCtorSystem)SystemRegistry.Construct[typeof(WorldCtorSystem)](world);

                return (parameterless is MovementSystem, worldCtor is WorldCtorSystem, ReferenceEquals(worldCtor.ReceivedWorld, world));
            }

            public static bool UnconstructableSystem_GetsNoConstructEntry() =>
                !SystemRegistry.Construct.ContainsKey(typeof(UnconstructableSystem));

            public static (bool FixedHasEntry, SystemCadence FixedValue, bool VariableHasEntry) Cadence()
            {
                var fixedHas = SystemRegistry.Cadence.TryGetValue(typeof(FixedCadenceSystem), out var fixedValue);
                var variableHas = SystemRegistry.Cadence.ContainsKey(typeof(OtherSystem));
                return (fixedHas, fixedValue, variableHas);
            }
        }
        """;

    [Fact]
    public void ExecuteChain_RegistersASystemAccessEntryKeyedByTheClass()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;
        var tuple = (System.Runtime.CompilerServices.ITuple)result;

        ((Type[])tuple[0]!).Should().HaveCount(7, "MovementSystem, MultiQuerySystem, HelperMethodSystem, DeclaredResourceSystem, AdHocResourceSystem, AdHocReadOnlyResourceSystem, TrySinglePositionSystem");
        ((Type[])tuple[1]!).Should().BeEquivalentTo([assembly.GetType("Velocity")]);
        ((Type[])tuple[2]!).Should().BeEquivalentTo([assembly.GetType("Position")]);
    }

    [Fact]
    public void MultiQuerySystem_UnionsAccessAcrossBothChainsInOneExecute()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;
        var tuple = (System.Runtime.CompilerServices.ITuple)result;

        ((Type[])tuple[3]!).Should().BeEquivalentTo([assembly.GetType("Position")]);
        ((Type[])tuple[4]!).Should().BeEquivalentTo([assembly.GetType("Health")]);
    }

    [Fact]
    public void ForEachCallInPrivateHelperMethod_IsAttributedToTheSystem()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (Type[])assembly.GetType("Harness")!.GetMethod("HelperMethodWrites")!.Invoke(null, null)!;

        result.Should().BeEquivalentTo([assembly.GetType("Health")], "the .ForEach() call is inside Step(), called from Execute, not directly inside Execute itself");
    }

    [Fact]
    public void ResourcePartialProperty_IsTrackedAsWriteAccess()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (Type[])assembly.GetType("Harness")!.GetMethod("DeclaredResourceWrites")!.Invoke(null, null)!;

        result.Should().BeEquivalentTo([assembly.GetType("Score")], "the property has a public setter, same rule as [Resource] on QuerySystem");
    }

    [Fact]
    public void GetResourceRefCall_IsTrackedAsWriteAccess()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (Type[])assembly.GetType("Harness")!.GetMethod("AdHocResourceWrites")!.Invoke(null, null)!;

        result.Should().BeEquivalentTo([assembly.GetType("Score")]);
    }

    [Fact]
    public void GetResourceCall_IsTrackedAsReadAccess()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (Type[])assembly.GetType("Harness")!.GetMethod("AdHocReadOnlyResourceReads")!.Invoke(null, null)!;

        result.Should().BeEquivalentTo([assembly.GetType("Score")]);
    }

    [Fact]
    public void TrySingleCall_IsTrackedAsReadAccess_AndSharesAnExistingBackend()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (Type[])assembly.GetType("Harness")!.GetMethod("TrySinglePositionReads")!.Invoke(null, null)!;

        result.Should().BeEquivalentTo([assembly.GetType("Position")], "TrySingle can only ever resolve to Reads, since there's no lambda to read ref/in from");
    }

    [Fact]
    public void AdHocChain_OutsideAnyEcsSystem_GetsNoEntry()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (bool)assembly.GetType("Harness")!.GetMethod("AdHocChain_GetsNoEntry")!.Invoke(null, null)!;

        result.Should().BeTrue();
    }

    [Fact]
    public void RunBeforeAndRunAfterAttributes_AreCapturedIntoSystemRegistryEdges()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = assembly.GetType("Harness")!.GetMethod("Edges")!.Invoke(null, null)!;
        var tuple = (System.Runtime.CompilerServices.ITuple)result;

        ((Type[])tuple[0]!).Should().BeEquivalentTo([assembly.GetType("OtherSystem")]);
        ((Type[])tuple[1]!).Should().BeEquivalentTo([assembly.GetType("OtherSystem")]);
    }

    [Fact]
    public void ConstructorShapes_EmitMatchingFactoriesIntoSystemRegistryConstruct()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = assembly.GetType("Harness")!.GetMethod("Construct")!.Invoke(null, null)!;
        var tuple = (System.Runtime.CompilerServices.ITuple)result;

        ((bool)tuple[0]!).Should().BeTrue("MovementSystem has no explicit ctor, so a public parameterless one is synthesized and used");
        ((bool)tuple[1]!).Should().BeTrue("WorldCtorSystem's ctor(World) is detected and used");
        ((bool)tuple[2]!).Should().BeTrue("the exact World instance passed to the factory must reach the constructor");
    }

    [Fact]
    public void UnconstructableSystem_GetsNoConstructEntry()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (bool)assembly.GetType("Harness")!.GetMethod("UnconstructableSystem_GetsNoConstructEntry")!.Invoke(null, null)!;

        result.Should().BeTrue("a ctor(int) is neither ctor(World) nor parameterless, so no factory can be emitted for it - silently skipped, not diagnosed, since nothing here says whether it's ever used via bare AddSystem<T>()");
    }

    [Fact]
    public void FixedTimestepAttribute_IsCapturedIntoSystemRegistryCadence()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = assembly.GetType("Harness")!.GetMethod("Cadence")!.Invoke(null, null)!;
        var tuple = (System.Runtime.CompilerServices.ITuple)result;

        ((bool)tuple[0]!).Should().BeTrue("FixedCadenceSystem carries [FixedTimestep]");
        tuple[1]!.ToString().Should().Be("Fixed");
        ((bool)tuple[2]!).Should().BeFalse("OtherSystem carries no [FixedTimestep], so it gets no Cadence entry at all: Variable is the TryGetValue-fallback default, not an explicit entry");
    }
}
