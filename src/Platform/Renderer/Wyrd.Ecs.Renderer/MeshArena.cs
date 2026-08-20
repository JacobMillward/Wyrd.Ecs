namespace Wyrd.Ecs.Renderer;

/// <summary>Dedup key for <see cref="MeshArena"/>: a source path plus which Assimp sub-mesh within it, since a multi-material file produces multiple distinct <see cref="Mesh"/> assets from one path.</summary>
internal readonly record struct MeshKey(string Path, int PartIndex);

/// <summary>
/// Path-keyed dedup and use-count arena for <see cref="Handle{T}"/>-based mesh loading, keyed
/// by <see cref="MeshKey"/> instead of a bare path. Same shape as <see cref="TextureArena"/>:
/// generation tracked separately per slot so a reused slot's stale handle compares unequal,
/// releasing owned by the caller via <c>readyForRelease</c> so this class stays ignorant of
/// frame-in-flight timing.
/// </summary>
internal sealed class MeshArena
{
    private sealed class Slot(MeshKey key)
    {
        public MeshKey Key = key;
        public LoadState State = LoadState.Loading;
        public Mesh? Mesh;
        public int UseCount = 1;
    }

    private readonly Lock _gate = new();
    private readonly List<Slot?> _slots = [];
    private readonly List<int> _generations = [];
    private readonly Dictionary<MeshKey, int> _keyToIndex = new();

    public Handle<Mesh> Reserve(MeshKey key)
    {
        lock (_gate)
        {
            if (_keyToIndex.TryGetValue(key, out var existingIndex))
            {
                _slots[existingIndex]!.UseCount++;
                return new Handle<Mesh>(existingIndex, _generations[existingIndex]);
            }

            var freeIndex = _slots.FindIndex(s => s is null);
            var slot = new Slot(key);
            if (freeIndex >= 0)
            {
                _slots[freeIndex] = slot;
                _keyToIndex[key] = freeIndex;
                return new Handle<Mesh>(freeIndex, _generations[freeIndex]);
            }

            _slots.Add(slot);
            _generations.Add(0);
            _keyToIndex[key] = _slots.Count - 1;
            return new Handle<Mesh>(_slots.Count - 1, 0);
        }
    }

    public void MarkLoaded(Handle<Mesh> handle, Mesh mesh)
    {
        lock (_gate)
        {
            var slot = GetSlotLocked(handle);
            slot.Mesh = mesh;
            slot.State = LoadState.Loaded;
        }
    }

    public void MarkFailed(Handle<Mesh> handle)
    {
        lock (_gate) { GetSlotLocked(handle).State = LoadState.Failed; }
    }

    public LoadState GetState(Handle<Mesh> handle)
    {
        lock (_gate) { return GetSlotLocked(handle).State; }
    }

    public Mesh? TryGetMesh(Handle<Mesh> handle)
    {
        lock (_gate) { return GetSlotLocked(handle).Mesh; }
    }

    public bool Unload(Handle<Mesh> handle, out Mesh? readyForRelease)
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

            readyForRelease = slot.Mesh;
            _keyToIndex.Remove(slot.Key);
            _generations[handle.Index]++;
            _slots[handle.Index] = null;
            return true;
        }
    }

    private Slot GetSlotLocked(Handle<Mesh> handle)
    {
        if (handle.Index >= _slots.Count || _slots[handle.Index] is not { } slot || _generations[handle.Index] != handle.Generation)
            throw new InvalidOperationException($"Handle {handle} does not refer to a live mesh (stale or already unloaded).");
        return slot;
    }
}
