namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// Configuration for <c>WorldBuilder.EnableContinuousPersistence</c> — the checkpoint
/// and WAL storage backends continuous persistence writes to, plus its durability/
/// checkpoint cadence tunables.
/// </summary>
public sealed class ContinuousOptions
{
    /// <summary>Where checkpoints are written — also where the initial bootstrap checkpoint lands.</summary>
    public required IPersistenceStore CheckpointStore { get; init; }

    /// <summary>Where WAL segments are written.</summary>
    public required IWalStore WalStore { get; init; }

    /// <summary>Durability/checkpoint cadence tunables. Defaults to <see cref="WalOptions.Default"/>.</summary>
    public WalOptions Options { get; init; } = WalOptions.Default;

    /// <summary>Reports a background thread's I/O failure instead of letting it propagate silently. Optional.</summary>
    public Action<Exception>? OnError { get; init; }
}
