namespace Wyrd.Ecs;

/// <summary>
/// A permanent, opaque entity identifier: a large random value with negligible
/// collision probability under decentralized, uncoordinated generation (the same
/// category of approach as a UUID), and a good statistical distribution for use as
/// a consistent-hashing key. Carries no embedded meaning about where the entity
/// currently lives or which process created it — see the design's Identity section.
/// This is what persistence, relations, and any future cross-process reference use;
/// never the transient <see cref="Entity"/> working id.
/// </summary>
public readonly record struct EntityId(UInt128 Value)
{
    /// <summary>Generates a new random <see cref="EntityId"/>.</summary>
    public static EntityId NewId()
    {
        var upper = (ulong)Random.Shared.NextInt64();
        var lower = (ulong)Random.Shared.NextInt64();
        return new EntityId(new UInt128(upper, lower));
    }
}
