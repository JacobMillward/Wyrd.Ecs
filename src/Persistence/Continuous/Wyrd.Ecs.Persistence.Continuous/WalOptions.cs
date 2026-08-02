namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>Tunables for how often continuous persistence flushes to disk and checkpoints.</summary>
public sealed class WalOptions
{
    /// <summary>
    /// How often the WAL-writer thread flushes to disk. Bounds how much recent activity a
    /// crash can lose, at the cost of more I/O the shorter it is.
    /// </summary>
    public TimeSpan FsyncInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How often the checkpoint-merge thread takes a new checkpoint, at minimum (also
    /// fires early if <see cref="CheckpointThresholdBytes"/> is reached first). Bounds
    /// how much a restart has to catch up on.
    /// </summary>
    public TimeSpan CheckpointInterval { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The WAL size, in bytes, that forces an early checkpoint even before
    /// <see cref="CheckpointInterval"/> elapses, to catch a burst of activity before
    /// segment count or restart catch-up time gets out of hand.
    /// </summary>
    public long CheckpointThresholdBytes { get; init; } = 64 * 1024 * 1024;

    /// <summary>The default tunables: a 1 second fsync interval, a 60 second checkpoint interval, and a 64 MB checkpoint size threshold.</summary>
    public static WalOptions Default { get; } = new();
}
