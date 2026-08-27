using System.Threading;
using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests;

struct QccComp0 : IComponent;
struct QccComp1 : IComponent;
struct QccComp2 : IComponent;
struct QccComp3 : IComponent;
struct QccComp4 : IComponent;
struct QccComp5 : IComponent;
struct QccComp6 : IComponent;
struct QccComp7 : IComponent;

/// <summary>
/// Parallel stages resolve the archetype-set caches concurrently when systems with different
/// signatures run in one stage. These tests coordinate threads with a barrier so every resolver
/// misses the cold caches simultaneously - the exact interleaving that corrupts an
/// unsynchronized Dictionary. Bounded rounds, no timing waits.
/// </summary>
public class QueryCacheConcurrencyTests
{
    private const int SignatureCount = 8;
    private const int ThreadsPerSignature = 2;
    private const int Rounds = 256;
    private const int ThreadCount = SignatureCount * ThreadsPerSignature;

    [Fact]
    public void ParallelColdCacheResolution_UnfilteredCacheSurvivesSimultaneousMisses()
    {
        var world = CreateWorldWithOneEntityPerSignature();

        RunConcurrentResolutions(world, expectFiltered: false);
    }

    [Fact]
    public void ParallelColdCacheResolution_FilteredCacheSurvivesSimultaneousMisses()
    {
        var world = CreateWorldWithOneEntityPerSignature();

        RunConcurrentResolutions(world, expectFiltered: true);
    }

    private static World CreateWorldWithOneEntityPerSignature()
    {
        var world = new World();
        for (var i = 0; i < SignatureCount; i++)
            SpawnSingle(world, i);
        world.ApplyCommands();
        // No query resolved yet: every signature below is a guaranteed cold-cache miss.
        return world;
    }

    private static void SpawnSingle(World world, int index)
    {
        switch (index)
        {
            case 0: world.Commands.CreateEntity(new QccComp0()); break;
            case 1: world.Commands.CreateEntity(new QccComp1()); break;
            case 2: world.Commands.CreateEntity(new QccComp2()); break;
            case 3: world.Commands.CreateEntity(new QccComp3()); break;
            case 4: world.Commands.CreateEntity(new QccComp4()); break;
            case 5: world.Commands.CreateEntity(new QccComp5()); break;
            case 6: world.Commands.CreateEntity(new QccComp6()); break;
            default: world.Commands.CreateEntity(new QccComp7()); break;
        }
    }

    private static TypeBitSet RequiredFor(int signatureIndex)
    {
        switch (signatureIndex)
        {
            case 0: return TypeBitSet.Empty.With(TypeIndex<QccComp0>.Value);
            case 1: return TypeBitSet.Empty.With(TypeIndex<QccComp1>.Value);
            case 2: return TypeBitSet.Empty.With(TypeIndex<QccComp2>.Value);
            case 3: return TypeBitSet.Empty.With(TypeIndex<QccComp3>.Value);
            case 4: return TypeBitSet.Empty.With(TypeIndex<QccComp4>.Value);
            case 5: return TypeBitSet.Empty.With(TypeIndex<QccComp5>.Value);
            case 6: return TypeBitSet.Empty.With(TypeIndex<QccComp6>.Value);
            default: return TypeBitSet.Empty.With(TypeIndex<QccComp7>.Value);
        }
    }

    // Pairs each keep-signature with a distinct skip-type so the filtered keys stay distinct
    // from one another and from the unfiltered cache's keys.
    private static ArchetypeFilter FilterFor(int signatureIndex)
    {
        switch (signatureIndex)
        {
            case 0: return ArchetypeFilter.Empty.Without<QccComp1>();
            case 1: return ArchetypeFilter.Empty.Without<QccComp2>();
            case 2: return ArchetypeFilter.Empty.Without<QccComp3>();
            case 3: return ArchetypeFilter.Empty.Without<QccComp4>();
            case 4: return ArchetypeFilter.Empty.Without<QccComp5>();
            case 5: return ArchetypeFilter.Empty.Without<QccComp6>();
            case 6: return ArchetypeFilter.Empty.Without<QccComp7>();
            default: return ArchetypeFilter.Empty.Without<QccComp0>();
        }
    }

    [Fact]
    public void ParallelColdCacheResolution_CombinedChainPairCacheSurvivesSimultaneousMisses()
    {
        var world = CreateWorldWithOneEntityPerSignature();
        var exceptions = new Exception?[ThreadCount];
        var wrongResults = new int[ThreadCount];
        var barrier = new Barrier(ThreadCount);
        var threads = new Thread[ThreadCount];

        for (var t = 0; t < ThreadCount; t++)
        {
            var slot = t;
            threads[t] = new Thread(() =>
            {
                try
                {
                    var signature = slot % SignatureCount;
                    var baseFilter = BaseFilterFor(signature);
                    var userFilter = FilterFor(signature);
                    barrier.SignalAndWait();

                    for (var round = 0; round < Rounds; round++)
                    {
                        // The keep-type's entity never carries the skip-type, so exactly one
                        // archetype satisfies the pair.
                        var matches = world.GetMatchingArchetypes(baseFilter, userFilter);
                        if (matches.Length != 1)
                            Interlocked.Increment(ref wrongResults[slot]);
                    }
                }
                catch (Exception ex)
                {
                    exceptions[slot] = ex;
                }
            });
        }

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();

        var failures = exceptions.OfType<Exception>().ToList();
        failures.Should().BeEmpty(
            $"every worker completed its bounded rounds without exception{Describe(failures)}");
        wrongResults.Should().AllBeEquivalentTo(0,
            "every pair resolution returns exactly the one matching archetype");
    }

    private static ArchetypeFilter BaseFilterFor(int signatureIndex)
    {
        switch (signatureIndex)
        {
            case 0: return ArchetypeFilter.Empty.Has<QccComp0>();
            case 1: return ArchetypeFilter.Empty.Has<QccComp1>();
            case 2: return ArchetypeFilter.Empty.Has<QccComp2>();
            case 3: return ArchetypeFilter.Empty.Has<QccComp3>();
            case 4: return ArchetypeFilter.Empty.Has<QccComp4>();
            case 5: return ArchetypeFilter.Empty.Has<QccComp5>();
            case 6: return ArchetypeFilter.Empty.Has<QccComp6>();
            default: return ArchetypeFilter.Empty.Has<QccComp7>();
        }
    }

    /// <summary>
    /// ThreadsPerSignature workers share each signature while SignatureCount distinct keys are
    /// inserted concurrently - exercising both same-key duplicate writes and cross-key resize
    /// races against whichever cache the caller selects.
    /// </summary>
    private static void RunConcurrentResolutions(World world, bool expectFiltered)
    {
        var exceptions = new Exception?[ThreadCount];
        var wrongResults = new int[ThreadCount];
        var barrier = new Barrier(ThreadCount);
        var threads = new Thread[ThreadCount];

        for (var t = 0; t < ThreadCount; t++)
        {
            var slot = t;
            threads[t] = new Thread(() =>
            {
                try
                {
                    var required = RequiredFor(slot % SignatureCount);
                    var filter = FilterFor(slot % SignatureCount);
                    barrier.SignalAndWait();

                    for (var round = 0; round < Rounds; round++)
                    {
                        var matches = expectFiltered && slot % 2 == 0
                            ? world.GetMatchingArchetypes(required, filter)
                            : world.GetMatchingArchetypes(required);
                        if (matches.Length != 1)
                            Interlocked.Increment(ref wrongResults[slot]);
                    }
                }
                catch (Exception ex)
                {
                    exceptions[slot] = ex;
                }
            });
        }

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();

        var failures = exceptions.OfType<Exception>().ToList();
        failures.Should().BeEmpty(
            $"every worker completed its bounded rounds without exception{Describe(failures)}");
        wrongResults.Should().AllBeEquivalentTo(0,
            "every resolution returns exactly the one matching archetype");
    }

    private static string Describe(List<Exception> failures)
        => failures.Count == 0 ? "" : $"; first failure: {failures[0].GetType().Name}: {failures[0].Message}";
}
