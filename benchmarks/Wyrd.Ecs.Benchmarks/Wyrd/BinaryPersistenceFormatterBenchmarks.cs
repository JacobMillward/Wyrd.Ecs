using BenchmarkDotNet.Attributes;
using MemoryPack;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// Compares the generated-formatter path against the hand-written <c>[MemoryPackable]</c>
/// path, for two shapes: a blittable component (unmanaged fast path either way) and a
/// component with a managed <c>string</c> field (routed through a generated
/// <c>MemoryPackFormatter&lt;T&gt;</c> instead of MemoryPack's own generator). Both pairs
/// should land within noise of each other, since they resolve to the same low-level
/// <c>WriteValue</c>/<c>ReadValue</c> calls either way; worth measuring rather than assuming.
/// </summary>
[MemoryDiagnoser]
public partial class BinaryPersistenceFormatterBenchmarks
{
    public struct UnmanagedNoAttribute : IComponent
    {
        public float X;
        public float Y;
    }

    [MemoryPackable]
    public partial struct UnmanagedHandWritten : IComponent
    {
        public float X;
        public float Y;
    }

    public struct ManagedGenerated : IComponent
    {
        public string Name;
        public int Count;
    }

    [MemoryPackable]
    public partial struct ManagedHandWritten : IComponent
    {
        public string Name;
        public int Count;
    }

    private UnmanagedNoAttribute _unmanagedGenerated;
    private UnmanagedHandWritten _unmanagedHandWritten;
    private ManagedGenerated _managedGenerated;
    private ManagedHandWritten _managedHandWritten;

    private byte[] _unmanagedGeneratedBytes = null!;
    private byte[] _unmanagedHandWrittenBytes = null!;
    private byte[] _managedGeneratedBytes = null!;
    private byte[] _managedHandWrittenBytes = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _unmanagedGenerated = new UnmanagedNoAttribute { X = 1f, Y = 2f };
        _unmanagedHandWritten = new UnmanagedHandWritten { X = 1f, Y = 2f };
        _managedGenerated = new ManagedGenerated { Name = "benchmark-name", Count = 42 };
        _managedHandWritten = new ManagedHandWritten { Name = "benchmark-name", Count = 42 };

        _unmanagedGeneratedBytes = MemoryPackSerializer.Serialize(_unmanagedGenerated);
        _unmanagedHandWrittenBytes = MemoryPackSerializer.Serialize(_unmanagedHandWritten);
        _managedGeneratedBytes = MemoryPackSerializer.Serialize(_managedGenerated);
        _managedHandWrittenBytes = MemoryPackSerializer.Serialize(_managedHandWritten);
    }

    [Benchmark(Baseline = true)]
    public byte[] Serialize_Unmanaged_HandWrittenMemoryPackable() => MemoryPackSerializer.Serialize(_unmanagedHandWritten);

    [Benchmark]
    public byte[] Serialize_Unmanaged_NoAttribute() => MemoryPackSerializer.Serialize(_unmanagedGenerated);

    [Benchmark]
    public UnmanagedHandWritten Deserialize_Unmanaged_HandWrittenMemoryPackable() => MemoryPackSerializer.Deserialize<UnmanagedHandWritten>(_unmanagedHandWrittenBytes);

    [Benchmark]
    public UnmanagedNoAttribute Deserialize_Unmanaged_NoAttribute() => MemoryPackSerializer.Deserialize<UnmanagedNoAttribute>(_unmanagedGeneratedBytes);

    [Benchmark]
    public byte[] Serialize_Managed_HandWrittenMemoryPackable() => MemoryPackSerializer.Serialize(_managedHandWritten);

    [Benchmark]
    public byte[] Serialize_Managed_GeneratedFormatter() => MemoryPackSerializer.Serialize(_managedGenerated);

    [Benchmark]
    public ManagedHandWritten Deserialize_Managed_HandWrittenMemoryPackable() => MemoryPackSerializer.Deserialize<ManagedHandWritten>(_managedHandWrittenBytes);

    [Benchmark]
    public ManagedGenerated Deserialize_Managed_GeneratedFormatter() => MemoryPackSerializer.Deserialize<ManagedGenerated>(_managedGeneratedBytes);
}
