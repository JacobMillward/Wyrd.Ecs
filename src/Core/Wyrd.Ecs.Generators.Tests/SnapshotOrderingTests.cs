namespace Wyrd.Ecs.Generators.Tests;

public class SnapshotOrderingTests
{
    private const string Harness = """
        using System;
        using System.Collections.Generic;
        using Wyrd.Ecs;

        public static class Log
        {
            public static readonly List<string> Order = new();
        }

        // Tracked is the tagged component (mirrors Transform); TrackedSnapshot is what
        // SnapshotSystem writes instead (mirrors PreviousTransform). SnapshotSystem must
        // never itself write the tagged component, or the merge logic would inject a
        // self-referential edge onto its own target.
        [RequiresSnapshotBefore(typeof(SnapshotSystem))]
        public struct Tracked : IComponent { public int Value; }

        public struct TrackedSnapshot : IComponent { public int Value; }

        [FixedTimestep]
        public sealed partial class SnapshotSystem : QuerySystem
        {
            protected override IQuery DefineQuery(Query query) => query.With<Tracked>().With<TrackedSnapshot>();
            public void Update(Time time, in Tracked tracked, ref TrackedSnapshot snapshot) => Log.Order.Add("Snapshot");
        }

        [FixedTimestep]
        public sealed partial class WriterSystem : QuerySystem
        {
            protected override IQuery DefineQuery(Query query) => query.With<Tracked>();
            public void Update(Time time, ref Tracked tracked) => Log.Order.Add("Writer");
        }

        public static class Harness
        {
            public static List<string> Run()
            {
                // WriterSystem registered before SnapshotSystem: registration-order
                // tie-break alone would log Writer first. This only logs Snapshot first
                // if the auto-injected edge actually overrides that tie-break.
                var world = new WorldBuilder()
                    .WithFixedTimestep(TimeSpan.FromSeconds(1))
                    .AddSystem<WriterSystem>()
                    .AddSystem<SnapshotSystem>()
                    .Build();
                world.Commands.CreateEntity(new Tracked { Value = 0 }, new TrackedSnapshot { Value = 0 });
                world.ApplyCommands();
                world.Update(TimeSpan.FromSeconds(1));
                return Log.Order;
            }
        }
        """;

    [Fact]
    public void FixedTimestepSystem_WritingASnapshotTaggedComponent_AutomaticallyRunsAfterItsTarget()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (List<string>)assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;

        // Both write Tracked, so the conflict rule alone keeps them in different stages,
        // since the only question is direction. Verified empirically that WriterSystem's
        // earlier registration wins the tie-break on its own (Writer logs first with no
        // ordering edge at all), so this only passes if the auto-injected edge actually
        // overrides that tie-break.
        result.Should().Equal("Snapshot", "Writer");
    }
}
