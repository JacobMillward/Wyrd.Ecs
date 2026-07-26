using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Wyrd.Ecs.Benchmarks.Comparison;

/// <summary>
/// Builds and disposes every <see cref="ContextAttribute"/>-tagged private field on a
/// benchmark instance uniformly, so each backend's per-scenario state (a <c>WyrdContext</c>,
/// <c>FrifloContext</c>, <c>FennecsContext</c>, etc.) is constructed from the same shared
/// arguments the scenario's <c>[Params]</c> declare — the mechanism that makes it
/// structurally impossible for one backend to run a different <c>EntityCount</c>/arity than
/// another. Only ever called from <c>[GlobalSetup]</c>/<c>[IterationSetup]</c>/
/// <c>[GlobalCleanup]</c>/<c>[IterationCleanup]</c> — never from a <c>[Benchmark]</c> method.
/// </summary>
internal static class BenchmarkOperations
{
    private static IEnumerable<FieldInfo> ContextFields<T>() =>
        typeof(T)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(field => field.GetCustomAttribute<ContextAttribute>() is not null);

    public static void SetupContexts<T>(T benchmark, params object[] args)
    {
        foreach (var field in ContextFields<T>())
            field.SetValue(benchmark, Activator.CreateInstance(field.FieldType, args));
    }

    public static void CleanupContexts<T>(T benchmark)
    {
        foreach (var field in ContextFields<T>())
        {
            if (field.GetValue(benchmark) is IDisposable disposable)
                disposable.Dispose();
            field.SetValue(benchmark, null);
        }
    }
}
