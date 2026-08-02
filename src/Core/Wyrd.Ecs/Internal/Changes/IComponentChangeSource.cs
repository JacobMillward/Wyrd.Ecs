namespace Wyrd.Ecs.Internal;

/// <summary>
/// The change-tracking half of a registered component's runtime view, split out of
/// <see cref="IComponentCodec"/> (which stays pure serialization) since nothing outside
/// <see cref="ChangeFeedHub"/> needs it. Implemented by <see cref="ComponentCodec{T}"/>
/// alongside the public <see cref="IComponentCodec"/> interface on the same instance;
/// <see cref="ChangeFeedHub"/>, in the same assembly, casts down to this when it needs
/// tracking.
/// </summary>
internal interface IComponentChangeSource
{
    /// <summary>Turns change tracking on for this registration's concrete component type via <see cref="World.TrackChanges{T}"/>, without the caller needing to know that type. Dispose the returned handle to turn tracking back off, same contract as <see cref="World.TrackChanges{T}"/> itself.</summary>
    IDisposable EnableChangeTracking(World world);

    /// <summary>
    /// Scans for every change to this registration's concrete component type since
    /// <paramref name="sinceTick"/>, the same scan a typed <see cref="World.ReadChanges{T}"/>
    /// call would use, each value returned boxed. Only observes anything once
    /// <see cref="EnableChangeTracking"/> has been called for this type.
    /// </summary>
    List<RawChange> ReadRawChanges(World world, int sinceTick);
}
