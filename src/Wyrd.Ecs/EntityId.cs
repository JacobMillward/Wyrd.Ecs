using System.Security.Cryptography;

namespace Wyrd.Ecs;

/// <summary>
/// A permanent, opaque entity identifier: a large random value with negligible
/// collision probability under decentralized, uncoordinated generation (the same
/// category of approach as a UUID). Carries no embedded meaning about where the
/// entity currently lives or which process created it — see the design's Identity
/// section. This is what persistence, relations, and any future cross-process
/// reference use; never the transient <see cref="Entity"/> working id.
/// </summary>
public readonly record struct EntityId(UInt128 Value)
{
    /// <summary>Generates a new, cryptographically random <see cref="EntityId"/>.</summary>
    public static EntityId NewId()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);

        var upper = BitConverter.ToUInt64(bytes[..8]);
        var lower = BitConverter.ToUInt64(bytes[8..]);
        return new EntityId(new UInt128(upper, lower));
    }
}
