namespace Wyrd.Ecs.Persistence;

/// <summary>
/// Implemented by a checkpoint-write <c>Stream</c> whose destination is only ever
/// touched atomically. The default — disposing the stream normally — commits whatever
/// was written; <see cref="Abort"/> tells a subsequent <c>Dispose()</c> to discard it
/// instead, so a write that fails partway through (<see cref="WorldSnapshot.Save"/>
/// calls this from its own catch block) can never leave a truncated file where the
/// previous good checkpoint used to be. A storage backend with nothing to make atomic
/// simply doesn't implement this — callers probe for it via <c>is</c>, the same
/// pattern <c>Stream.CanSeek</c>-style capability checks use.
/// </summary>
public interface ITransactionalWriteStream
{
    /// <summary>Marks this stream so the next <c>Dispose()</c> discards what was written instead of committing it.</summary>
    void Abort();
}
