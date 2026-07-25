namespace Wyrd.Ecs.SystemGenerators.Tests;

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

        public sealed class MovementSystem : EcsSystem
        {
            protected override void OnUpdate(World world, ulong tick) =>
                world.Query().With<Writes<Position>>().With<Reads<Velocity>>().ForEach(tick, (ulong t, ref Position p, in Velocity v) => { });
        }

        public sealed class MultiQuerySystem : EcsSystem
        {
            protected override void OnUpdate(World world, ulong tick)
            {
                world.Query().With<Writes<Health>>().ForEach(tick, (ulong t, ref Health h) => { });
                world.Query().With<Reads<Position>>().ForEach(tick, (ulong t, in Position p) => { });
            }
        }

        public static class Harness
        {
            public static (Type[] Keys, Type[] MovementReads, Type[] MovementWrites, Type[] MultiReads, Type[] MultiWrites) Run()
            {
                var keys = new List<Type>(GeneratedSystemAccess.Entries.Keys).ToArray();

                var movement = GeneratedSystemAccess.Entries[typeof(MovementSystem)];
                var multi = GeneratedSystemAccess.Entries[typeof(MultiQuerySystem)];

                return (keys,
                    new List<Type>(movement.Reads).ToArray(), new List<Type>(movement.Writes).ToArray(),
                    new List<Type>(multi.Reads).ToArray(), new List<Type>(multi.Writes).ToArray());
            }

            public static bool AdHocChain_GetsNoEntry()
            {
                var world = new World();
                world.Query().With<Reads<Position>>().ForEach(0, (int _, in Position p) => { });
                return GeneratedSystemAccess.Entries.Count == 2; // only MovementSystem and MultiQuerySystem -- this ad-hoc call adds nothing
            }
        }
        """;

    [Fact]
    public void OnUpdateChain_RegistersASystemAccessEntryKeyedByTheClass()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;
        var tuple = (System.Runtime.CompilerServices.ITuple)result;

        ((Type[])tuple[0]!).Should().HaveCount(2); // MovementSystem, MultiQuerySystem
        ((Type[])tuple[1]!).Should().BeEquivalentTo([assembly.GetType("Velocity")]);
        ((Type[])tuple[2]!).Should().BeEquivalentTo([assembly.GetType("Position")]);
    }

    [Fact]
    public void MultiQuerySystem_UnionsAccessAcrossBothChainsInOneOnUpdate()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;
        var tuple = (System.Runtime.CompilerServices.ITuple)result;

        ((Type[])tuple[3]!).Should().BeEquivalentTo([assembly.GetType("Position")]);
        ((Type[])tuple[4]!).Should().BeEquivalentTo([assembly.GetType("Health")]);
    }

    [Fact]
    public void AdHocChain_OutsideAnyEcsSystem_GetsNoEntry()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (bool)assembly.GetType("Harness")!.GetMethod("AdHocChain_GetsNoEntry")!.Invoke(null, null)!;

        result.Should().BeTrue();
    }
}
