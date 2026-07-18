namespace Wyrd.Ecs;

/// <summary>
/// Growable, tick-stamped append log of entities marked dirty for one component type
/// within one archetype, plus that archetype's current row-to-entity mapping. Passed
/// to <see cref="IComponentAccessor{TSelf}.CreateChunk"/> so a tracked accessor
/// (<see cref="Mut{T}"/>) can append on an entity's first touch per tick. Capacity is
/// always ensured by the caller (<see cref="Internal.ComponentStorage{T}.GetDirtyLogForChunk"/>)
/// before a chunk is constructed — a growable append can't safely happen through a ref
/// struct mid-iteration, so growth is a pre-flight step, not something the accessor
/// itself ever does.
/// </summary>
public sealed class DirtyLog
{
    internal Entity[] ArchetypeEntities;
    internal DirtyEntry[] Entries;
    internal int Count;

    /// <summary>
    /// Index of the first live (untrimmed) entry. Entries before this are retired but
    /// not yet physically removed — <see cref="Internal.ComponentStorage{T}.TrimBefore"/>
    /// only ever advances this pointer, never shifts the array; the space it marks as
    /// garbage is reclaimed lazily, the next time the log needs to grow.
    /// </summary>
    internal int Head;

    internal DirtyLog(Entity[] archetypeEntities, DirtyEntry[] entries, int count)
    {
        ArchetypeEntities = archetypeEntities;
        Entries = entries;
        Count = count;
    }
}
