namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// What kind of structural or value change one WAL record represents. Tag add/remove
/// are not represented: tags carry no data and are already skipped by
/// <see cref="World.EnumerateAll"/> on checkpoint.
/// </summary>
public enum WalRecordKind : byte
{
    /// <summary>A tracked component's value changed (via <c>ReadChanges&lt;T&gt;</c>).</summary>
    ComponentChanged = 0,

    /// <summary>An entity was created.</summary>
    EntityCreated = 1,

    /// <summary>An entity was destroyed, along with all of its components.</summary>
    EntityDestroyed = 2,

    /// <summary>A component was added to an already-existing entity.</summary>
    ComponentAdded = 3,

    /// <summary>A component was removed from an entity.</summary>
    ComponentRemoved = 4,

    /// <summary>A relation edge was added.</summary>
    RelationLinked = 5,

    /// <summary>A relation edge no longer exists.</summary>
    RelationUnlinked = 6,
}
