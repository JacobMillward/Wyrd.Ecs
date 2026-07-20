namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// Tunables for continuous persistence's durability/checkpoint cadence — matching the
/// <see cref="WorldBuilder.WithArchetypeCapacity"/> precedent of an exposed, overridable
/// setting with a sensible default rather than a hardcoded constant, since a hosted
/// server and a local single-player save have different risk tolerance and this package
/// doesn't get to assume which one a given consumer is.
/// </summary>
public sealed class WalOptions
{
    /// <summary>
    /// How often the WAL-writer thread fsyncs its appended records. Bounds crash-loss to
    /// about this much unfsynced writing time, without paying fsync-per-record
    /// throughput cost.
    /// </summary>
    public TimeSpan FsyncInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How often the checkpoint-merge thread takes a new checkpoint, at minimum — a
    /// checkpoint also fires early if <see cref="CheckpointThresholdBytes"/> is reached
    /// first. Bounds how much WAL a restart might have to replay under normal churn.
    /// </summary>
    public TimeSpan CheckpointInterval { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The WAL size, in bytes, that forces an early checkpoint even before
    /// <see cref="CheckpointInterval"/> elapses — catches a churn spike before segment
    /// count or replay cost gets out of hand.
    /// </summary>
    public long CheckpointThresholdBytes { get; init; } = 64 * 1024 * 1024;

    /// <summary>The default tunables: a 1 second fsync interval, a 60 second checkpoint interval, and a 64 MB checkpoint size threshold.</summary>
    public static WalOptions Default { get; } = new();
}
