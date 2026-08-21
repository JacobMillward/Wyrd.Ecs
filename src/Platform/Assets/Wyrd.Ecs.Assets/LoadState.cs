namespace Wyrd.Ecs.Assets;

/// <summary>A <see cref="Handle{T}"/>'s current resolution state.</summary>
public enum LoadState
{
    /// <summary>Not yet resolved. Callers typically show a placeholder.</summary>
    Loading,

    /// <summary>Resolved to a real asset.</summary>
    Loaded,

    /// <summary>Loading failed.</summary>
    Failed,
}
