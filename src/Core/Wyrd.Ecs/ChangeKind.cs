namespace Wyrd.Ecs;

/// <summary>What a <see cref="ChangeEntry"/> represents, stamped on <see cref="ChangeEntry.Kind"/>.</summary>
[Flags]
public enum ChangeKind : ushort
{
    /// <summary>A tracked component's value changed.</summary>
    ValueChanged = 1,

    /// <summary>An entity was created.</summary>
    EntityCreated = 2,

    /// <summary>An entity was destroyed.</summary>
    EntityDestroyed = 4,

    /// <summary>A component was added to an already-existing entity.</summary>
    ComponentAdded = 8,

    /// <summary>A component was removed from an entity.</summary>
    ComponentRemoved = 16,

    /// <summary>A relation edge was added.</summary>
    RelationLinked = 32,

    /// <summary>A relation edge no longer exists.</summary>
    RelationUnlinked = 64,

    /// <summary>A tag was added to an entity.</summary>
    TagAdded = 128,

    /// <summary>A tag was removed from an entity.</summary>
    TagRemoved = 256,
}
