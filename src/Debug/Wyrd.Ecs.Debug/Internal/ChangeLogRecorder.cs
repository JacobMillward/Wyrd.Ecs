namespace Wyrd.Ecs.Debug.Internal;

/// <summary>
/// Fixed-capacity ring buffer of <see cref="ChangeLogEntry"/>, newest first. Not the
/// hot-path-sensitive piece of this design (that's the separate, future
/// originating-system work), so a plain lock is the right amount of care here, not a
/// lock-free structure.
/// </summary>
internal sealed class ChangeLogRecorder(int capacity) : IStructuralChangeObserver
{
    private readonly LinkedList<ChangeLogEntry> _entries = new();
    private readonly Lock _lock = new();
    private int _tick;

    public IReadOnlyList<ChangeLogEntry> Entries
    {
        get { lock (_lock) return _entries.ToArray(); }
    }

    public void OnEntityCreated(Entity entity) => Record(ChangeKind.EntityCreated, entity, null);
    public void OnEntityDestroyed(Entity entity) => Record(ChangeKind.EntityDestroyed, entity, null);
    public void OnComponentAdded(Entity entity, int typeIndex) => Record(ChangeKind.ComponentAdded, entity, ResolveName(typeIndex));
    public void OnComponentRemoved(Entity entity, int typeIndex) => Record(ChangeKind.ComponentRemoved, entity, ResolveName(typeIndex));
    public void OnTagAdded(Entity entity, int typeIndex) => Record(ChangeKind.TagAdded, entity, ResolveName(typeIndex));
    public void OnTagRemoved(Entity entity, int typeIndex) => Record(ChangeKind.TagRemoved, entity, ResolveName(typeIndex));

    internal void AdvanceTick(int tick) => Volatile.Write(ref _tick, tick);

    private static string? ResolveName(int typeIndex) =>
        Wyrd.Ecs.Internal.DebugNameRegistry.TryGetName(typeIndex, out var name) ? name : null;

    private void Record(ChangeKind kind, Entity entity, string? componentName)
    {
        lock (_lock)
        {
            _entries.AddFirst(new ChangeLogEntry(Volatile.Read(ref _tick), kind, entity, componentName));
            if (_entries.Count > capacity) _entries.RemoveLast();
        }
    }
}
