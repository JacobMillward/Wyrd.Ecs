using BenchmarkDotNet.Attributes;
using Wyrd.Ecs.Persistence.Continuous;
using Wyrd.Ecs.Persistence.Continuous.Internal;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// Isolated measurement of the WAL record write path
/// (<c>WalSegmentWriter.WriteRecords</c>), kept separate from
/// <see cref="ContinuousPersistenceTickBenchmarks"/> (which measures the sim-thread
/// capture cost) so a combined number can't hide a regression in either. Standing
/// regression guard for the pooled buffer's allocation cost.
/// </summary>
[MemoryDiagnoser]
public class WalRecordWriteBenchmarks
{
    [Params(100, 5_000)]
    public int RecordCount { get; set; }

    private string _directory = null!;
    private WalSegmentWriter _writer = null!;
    private List<CapturedWalEntry> _readyEntries = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"wyrd-benchmarks-wal-write-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        var walStore = new FileWalStore(Path.Combine(_directory, "world"));
        _writer = new WalSegmentWriter(walStore);
        _writer.EnsureSegmentOpen(1);

        _readyEntries = new List<CapturedWalEntry>(RecordCount);
        for (var i = 0; i < RecordCount; i++)
            _readyEntries.Add(new CapturedWalEntry(WalRecordKind.ComponentChanged, 1, EntityId.NewId(), "Position", 7u, BitConverter.GetBytes((float)i)));
    }

    [GlobalCleanup]
    public void GlobalCleanup() => Directory.Delete(_directory, recursive: true);

    [Benchmark]
    public void WriteRecords_AlreadyEncodedEntries()
    {
        _writer.WriteRecords(new DrainedChanges(_readyEntries, []));
        _writer.Flush();
    }
}
