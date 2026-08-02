namespace Wyrd.Ecs.Persistence;

/// <summary>
/// Implemented by a checkpoint-write <c>Stream</c> whose destination file is only ever
/// updated atomically. Disposing normally commits what was written; <see cref="Abort"/>
/// tells the next <c>Dispose()</c> to discard it instead, so a save that fails partway
/// through never overwrites a good checkpoint with a truncated one.
/// </summary>
public interface ITransactionalWriteStream
{
    /// <summary>Marks this stream so the next <c>Dispose()</c> discards what was written instead of committing it.</summary>
    void Abort();
}
