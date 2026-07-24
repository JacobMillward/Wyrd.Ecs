namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// One tick-cycle's worth of captured changes, drained from <see cref="ChangeCapture"/>:
/// structural events already resolved to bytes (<see cref="Ready"/>) and value changes
/// still awaiting their encode call (<see cref="Pending"/>). Both lists are only safe to
/// read until the next <see cref="ChangeCapture.SwapBuffers"/> call, same contract the
/// single list they replace already had.
/// </summary>
internal readonly record struct DrainedChanges(List<CapturedWalEntry> Ready, List<PendingValueChange> Pending);
