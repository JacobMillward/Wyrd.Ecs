using System.Reflection;

namespace Wyrd.Ecs.Generators.Tests;

public class QueryChainGeneratorEntityViewTests
{
    private const string DualFamilySource = """
        using Wyrd.Ecs;

        public struct Score : IComponent { public int Value; }

        public static class Harness
        {
            public static (int PlainSum, int EntitySum, int EntityCount) Run()
            {
                var world = new World();
                world.Commands.CreateEntity(new Score { Value = 1 });
                world.Commands.CreateEntity(new Score { Value = 2 });
                world.ApplyCommands();

                var plainSum = 0;
                world.Query().With<Score>().ForEach(0, (in int _, in Score s) => plainSum += s.Value);

                var entitySum = 0;
                var entityCount = 0;
                world.Query().With<Score>().ForEach(0, (in int _, EntityView entity, in Score s) =>
                {
                    entitySum += s.Value;
                    entityCount++;
                });

                return (plainSum, entitySum, entityCount);
            }
        }
        """;

    [Fact]
    public void ShapeUsedBothWithAndWithoutEntityView_GeneratesWithoutCrashing_BothOverloadsWork()
    {
        // A shape used both ways gives two canonical entries sharing one ExactShapeTypeName
        // (one per family). CompileAndLoad runs the generator for real, so if
        // EmitInterceptorsAndTargets's canonicalByExactShapeTypeName dictionary is still
        // keyed by ExactShapeTypeName alone, this throws ArgumentException here.
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(DualFamilySource));
        var method = assembly.GetType("Harness")!.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;

        var (plainSum, entitySum, entityCount) = ((int, int, int))method.Invoke(null, null)!;

        plainSum.Should().Be(3);
        entitySum.Should().Be(3);
        entityCount.Should().Be(2);
    }

    [Fact]
    public void ShapeUsedBothWithAndWithoutEntityView_EmitsTwoDistinctForEachOverloadFiles()
    {
        var result = GeneratorTestHost.Run(new QueryChainGenerator(), GeneratorTestHost.Compile(DualFamilySource));

        var forEachFiles = result.Results[0].GeneratedSources
            .Where(s => s.HintName.StartsWith("QueryChainForEach."))
            .Select(s => s.HintName)
            .ToList();

        forEachFiles.Should().HaveCount(2, "the entity-inclusive and non-entity variants of the same Score shape must not share one generated overload file");
    }

    [Fact]
    public void EntityInclusiveFamily_ConflictingRefInAccess_ReadObservesWrite_ViaInterceptor()
    {
        var compilation = GeneratorTestHost.Compile("""
            using Wyrd.Ecs;

            public struct Score : IComponent { public int Value; }

            public static class Harness
            {
                public static int Run()
                {
                    var world = new World();
                    world.Commands.CreateEntity(new Score { Value = 5 });
                    world.ApplyCommands();
                    world.AdvanceTick();

                    world.Query().With<Score>().ForEach(0, (in int _, EntityView entity, ref Score s) => { s.Value += 10; });

                    var observed = 0;
                    world.Query().With<Score>().ForEach(0, (in int _, EntityView entity, in Score s) => { observed = s.Value; });
                    return observed;
                }
            }
            """);

        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), compilation);
        var method = assembly.GetType("Harness")!.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;

        var result = (int)method.Invoke(null, null)!;

        result.Should().Be(15);
    }

    [Fact]
    public void QuerySystemWithEntityView_DoesNotSpawnASeparateEntityInclusiveCanonicalFamily()
    {
        // EntityViewSystem's Update declares EntityView -- QuerySystemUpdateShape already
        // recognizes that today, unrelated to this feature. What this test actually
        // guards: DeduplicateShapes must contribute IncludesEntityView=false for every
        // QuerySystemCandidate regardless of its own HasEntityViewParameter, since
        // QuerySystem's EntityView handling (AppendEntityViewExecute) never routes through
        // the terminal-class family this grouping controls. A Ref<Score>/Mut<Score> value
        // read is not a useful assertion here -- both are views over the same physical
        // storage, so a misgrouping bug would not show up as a wrong number. It shows up
        // structurally instead: a spurious second canonical family (and thus a second
        // QueryChainForEach file) for a shape with only one real chain-side family.
        var compilation = GeneratorTestHost.Compile("""
            using Wyrd.Ecs;

            public struct Score : IComponent { public int Value; }

            public sealed partial class EntityViewSystem : QuerySystem
            {
                protected override IQuery DefineQuery(Query query) => query.With<Score>();
                public void Update(Time time, EntityView entity, ref Score score) => score.Value += 1;
            }

            public static class SiteA
            {
                public static void M(World world) =>
                    world.Query().With<Score>().ForEach(0, (in int _, in Score s) => { });
            }
            """);

        var result = GeneratorTestHost.Run(new QueryChainGenerator(), compilation);

        var forEachFiles = result.Results[0].GeneratedSources
            .Where(s => s.HintName.StartsWith("QueryChainForEach."))
            .Select(s => s.HintName)
            .ToList();

        forEachFiles.Should().ContainSingle("the QuerySystem's own EntityView declaration must not be mistaken for a chain-side entity-inclusive variant");
    }
}
