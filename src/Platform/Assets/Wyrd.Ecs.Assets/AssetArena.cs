namespace Wyrd.Ecs.Assets;

/// <summary>
/// Key-keyed dedup, use-count, and generation-tracked arena backing <see cref="Handle{T}"/>-based
/// asset loading. Owns no decode/upload logic itself — callers drive <see cref="MarkLoaded"/>/
/// <see cref="MarkFailed"/> once their own (GPU/decoder/etc.) work completes. Thread-safe.
/// </summary>
public sealed class AssetArena<TKey, TAsset>
    where TKey : notnull
    where TAsset : class
{
    private sealed class Slot(TKey key)
    {
        public TKey Key = key;
        public LoadState State = LoadState.Loading;
        public TAsset? Asset;
        public Exception? Failure;
        public int UseCount = 1;
        public TaskCompletionSource? Completion;
    }

    private readonly Lock _gate = new();
    private readonly List<Slot?> _slots = [];
    private readonly List<int> _generations = [];
    private readonly Dictionary<TKey, int> _keyToIndex = new();

    /// <summary>
    /// Returns the existing handle for <paramref name="key"/> if one is already reserved
    /// (incrementing its use-count, <paramref name="isNew"/> <c>false</c>), otherwise allocates a
    /// fresh slot (<paramref name="isNew"/> <c>true</c>). Callers must skip their own decode/
    /// upload work when <paramref name="isNew"/> is <c>false</c> — the original reservation's load
    /// is already in flight or complete, and its <c>WaitForLoadAsync</c> task is shared by
    /// every handle returned for this key.
    /// </summary>
    public Handle<TAsset> Reserve(TKey key, out bool isNew)
    {
        lock (_gate)
        {
            if (_keyToIndex.TryGetValue(key, out var existingIndex))
            {
                _slots[existingIndex]!.UseCount++;
                isNew = false;
                return new Handle<TAsset>(existingIndex, _generations[existingIndex]);
            }

            isNew = true;
            var freeIndex = _slots.FindIndex(s => s is null);
            var slot = new Slot(key);
            if (freeIndex >= 0)
            {
                _slots[freeIndex] = slot;
                _keyToIndex[key] = freeIndex;
                return new Handle<TAsset>(freeIndex, _generations[freeIndex]);
            }

            _slots.Add(slot);
            _generations.Add(0);
            _keyToIndex[key] = _slots.Count - 1;
            return new Handle<TAsset>(_slots.Count - 1, 0);
        }
    }

    /// <summary>First-resolution-wins: a no-op if <paramref name="handle"/>'s slot is no longer <see cref="LoadState.Loading"/>.</summary>
    public void MarkLoaded(Handle<TAsset> handle, TAsset asset)
    {
        lock (_gate)
        {
            var slot = GetSlotLocked(handle);
            if (slot.State != LoadState.Loading) return;
            slot.Asset = asset;
            slot.State = LoadState.Loaded;
            slot.Completion?.TrySetResult();
        }
    }

    /// <summary>First-resolution-wins: a no-op if <paramref name="handle"/>'s slot is no longer <see cref="LoadState.Loading"/>.</summary>
    public void MarkFailed(Handle<TAsset> handle, Exception exception)
    {
        lock (_gate)
        {
            var slot = GetSlotLocked(handle);
            if (slot.State != LoadState.Loading) return;
            slot.State = LoadState.Failed;
            slot.Failure = exception;
            slot.Completion?.TrySetException(exception);
        }
    }

    /// <summary>The handle's current <see cref="LoadState"/>.</summary>
    public LoadState GetState(Handle<TAsset> handle)
    {
        lock (_gate) { return GetSlotLocked(handle).State; }
    }

    /// <summary>The resolved asset, or <c>null</c> if still <see cref="LoadState.Loading"/> or <see cref="LoadState.Failed"/>.</summary>
    public TAsset? TryGet(Handle<TAsset> handle)
    {
        lock (_gate) { return GetSlotLocked(handle).Asset; }
    }

    /// <summary>
    /// Task that completes (or faults with the exception passed to <see cref="MarkFailed"/>) once
    /// <paramref name="handle"/> resolves. The backing <see cref="TaskCompletionSource"/> is
    /// created lazily, here, rather than eagerly in <see cref="Reserve"/> — most loads are never
    /// awaited (callers poll <see cref="GetState"/>/<see cref="TryGet"/> instead), and an eager
    /// per-slot allocation measurably bloats <see cref="Slot"/>'s footprint under the scan-heavy
    /// access pattern a per-tick resolve call produces (many distinct slots touched every frame).
    /// If the slot already resolved before this is called, the returned task is already
    /// completed/faulted rather than left to hang — <see cref="Slot.Failure"/> exists precisely
    /// so a late call still has the original exception to fault with.
    /// </summary>
    public Task WaitForLoadAsync(Handle<TAsset> handle)
    {
        lock (_gate)
        {
            var slot = GetSlotLocked(handle);
            if (slot.Completion is { } existing) return existing.Task;

            var completion = new TaskCompletionSource();
            switch (slot.State)
            {
                case LoadState.Loaded:
                    completion.SetResult();
                    break;
                case LoadState.Failed:
                    completion.SetException(slot.Failure!);
                    break;
                default:
                    slot.Completion = completion;
                    break;
            }
            return completion.Task;
        }
    }

    /// <summary>
    /// Decrements the handle's use-count; once it reaches zero, removes the slot, bumps its
    /// generation (so any handle issued before this call now compares unequal to future <see
    /// cref="Reserve"/> calls reusing this index), and hands the caller the asset via <paramref
    /// name="readyForRelease"/>. The arena never disposes/releases the asset itself — only the
    /// caller knows how (e.g. GPU resource release timing tied to frames-in-flight).
    /// </summary>
    public bool Unload(Handle<TAsset> handle, out TAsset? readyForRelease)
    {
        lock (_gate)
        {
            var slot = GetSlotLocked(handle);
            slot.UseCount--;
            if (slot.UseCount > 0)
            {
                readyForRelease = null;
                return false;
            }

            readyForRelease = slot.Asset;
            _keyToIndex.Remove(slot.Key);
            _generations[handle.Index]++;
            _slots[handle.Index] = null;
            return true;
        }
    }

    /// <summary>
    /// Faults every slot still <see cref="LoadState.Loading"/> with <paramref name="exception"/>
    /// (via the same first-resolution-wins path as <see cref="MarkFailed"/> — already-resolved
    /// slots are untouched). For teardown: a load still in flight when its owner is destroyed can
    /// never resolve on its own, so without this an awaiting caller hangs forever instead of
    /// observing the teardown.
    /// </summary>
    public void FaultAllPending(Exception exception)
    {
        lock (_gate)
        {
            foreach (var slot in _slots)
            {
                if (slot is null || slot.State != LoadState.Loading) continue;
                slot.State = LoadState.Failed;
                slot.Failure = exception;
                slot.Completion?.TrySetException(exception);
            }
        }
    }

    private Slot GetSlotLocked(Handle<TAsset> handle)
    {
        if (handle.Index >= _slots.Count || _slots[handle.Index] is not { } slot || _generations[handle.Index] != handle.Generation)
            throw new InvalidOperationException($"Handle {handle} does not refer to a live asset (stale or already unloaded).");
        return slot;
    }
}
