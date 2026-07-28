using BenchmarkDotNet.Attributes;
using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

[MemoryDiagnoser]
public class ArchetypeSignatureBenchmarks
{
    [Benchmark]
    public bool With_FourBits() =>
        ArchetypeSignature.Empty.With(1).With(2).With(3).With(4).Contains(4);

    [Benchmark]
    public bool With_ThenIntersects()
    {
        var a = ArchetypeSignature.Empty.With(1).With(2);
        var b = ArchetypeSignature.Empty.With(2).With(3);
        return a.Intersects(b);
    }
}
