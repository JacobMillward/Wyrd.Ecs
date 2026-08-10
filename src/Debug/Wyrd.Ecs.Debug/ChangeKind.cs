namespace Wyrd.Ecs.Debug;

/// <summary>The kind of structural change a <see cref="ChangeLogEntry"/> records.</summary>
public enum ChangeKind
{
    /// <summary>An entity was created.</summary>
    EntityCreated,

    /// <summary>An entity was destroyed.</summary>
    EntityDestroyed,

    /// <summary>A component was added to an existing entity.</summary>
    ComponentAdded,

    /// <summary>A component was removed from an entity.</summary>
    ComponentRemoved,

    /// <summary>A tag was added to an entity.</summary>
    TagAdded,

    /// <summary>A tag was removed from an entity.</summary>
    TagRemoved,
}
