namespace Wyrd.Ecs.Renderer;

/// <summary>A <see cref="Handle{T}"/>'s current resolution state.</summary>
internal enum LoadState
{
    /// <summary>Decode/upload not yet complete. Draw the placeholder.</summary>
    Loading,

    /// <summary>Resolved to a real GPU texture.</summary>
    Loaded,

    /// <summary>Decode or upload failed. Draw the placeholder.</summary>
    Failed,
}

/// <summary>
/// Path-keyed dedup + use-count arena for <see cref="Handle{T}"/>-based texture loading. Owns
/// no GPU resources itself: <see cref="RendererSystem"/> owns the actual
/// <c>SDL_ReleaseGPUTexture</c> call, via the <see cref="Texture"/> handed back by
/// <see cref="Unload"/>'s <c>readyForRelease</c> out-param once use-count hits zero, so the
/// caller can route it through <c>DeferredDestroyQueue</c> rather than this class needing to
/// know about frame-in-flight timing. A slot's generation is tracked separately from the slot
/// object itself (in <see cref="_generations"/>, indexed by slot index, never cleared) so a
/// freed-and-reused slot keeps counting up rather than resetting. A stale <see cref="Handle{T}"/>
/// from before the reuse must compare unequal to the new one at the same index.
/// </summary>
internal sealed class TextureArena
{
    private sealed class Slot(string path)
    {
        public string Path = path;
        public LoadState State = LoadState.Loading;
        public Texture? Texture;
        public int UseCount = 1;
    }

    private readonly Lock _gate = new();
    private readonly List<Slot?> _slots = [];
    private readonly List<int> _generations = [];
    private readonly Dictionary<string, int> _pathToIndex = new();

    public Handle<Texture> Reserve(string path)
    {
        lock (_gate)
        {
            if (_pathToIndex.TryGetValue(path, out var existingIndex))
            {
                _slots[existingIndex]!.UseCount++;
                return new Handle<Texture>(existingIndex, _generations[existingIndex]);
            }

            var freeIndex = _slots.FindIndex(s => s is null);
            var slot = new Slot(path);
            if (freeIndex >= 0)
            {
                _slots[freeIndex] = slot;
                _pathToIndex[path] = freeIndex;
                return new Handle<Texture>(freeIndex, _generations[freeIndex]);
            }

            _slots.Add(slot);
            _generations.Add(0);
            _pathToIndex[path] = _slots.Count - 1;
            return new Handle<Texture>(_slots.Count - 1, 0);
        }
    }

    public void MarkLoaded(Handle<Texture> handle, Texture texture)
    {
        lock (_gate)
        {
            var slot = GetSlotLocked(handle);
            slot.Texture = texture;
            slot.State = LoadState.Loaded;
        }
    }

    public void MarkFailed(Handle<Texture> handle)
    {
        lock (_gate) { GetSlotLocked(handle).State = LoadState.Failed; }
    }

    public LoadState GetState(Handle<Texture> handle)
    {
        lock (_gate) { return GetSlotLocked(handle).State; }
    }

    public Texture? TryGetTexture(Handle<Texture> handle)
    {
        lock (_gate) { return GetSlotLocked(handle).Texture; }
    }

    public bool Unload(Handle<Texture> handle, out Texture? readyForRelease)
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

            readyForRelease = slot.Texture;
            _pathToIndex.Remove(slot.Path);
            _generations[handle.Index]++;
            _slots[handle.Index] = null;
            return true;
        }
    }

    private Slot GetSlotLocked(Handle<Texture> handle)
    {
        if (handle.Index >= _slots.Count || _slots[handle.Index] is not { } slot || _generations[handle.Index] != handle.Generation)
            throw new InvalidOperationException($"Handle {handle} does not refer to a live texture (stale or already unloaded).");
        return slot;
    }
}
