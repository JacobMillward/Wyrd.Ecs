namespace Wyrd.Ecs;

/// <summary>What a <see cref="ChangeEntry"/> represents.</summary>
public enum ChangeKind : byte
{
    /// <summary>A tracked component's value changed.</summary>
    ValueChanged = 0,

    /// <summary>An entity was created.</summary>
    EntityCreated = 1,

    /// <summary>An entity was destroyed.</summary>
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
