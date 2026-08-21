using System.Diagnostics;

namespace Wyrd.Ecs;

/// <summary>
/// A transient, in-process working identifier for an entity: small, dense, and reused
/// after deletion. Valid only within the running process that created it. Never persist
/// or transmit this value; use <see cref="EntityId"/> for anything that must survive a
/// restart or cross a process boundary.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public readonly record struct Entity(int Id, int Generation)
{
    /// <summary>The value representing "no entity." <see cref="Id"/> 0 is never assigned to a live entity.</summary>
    public static readonly Entity Null = default;

    /// <summary>True when this value equals <see cref="Null"/>.</summary>
    public bool IsNull => Id == 0;

    /// <summary>The compact "{id}v{generation}" form, e.g. "3v1".</summary>
    public override string ToString() => $"{Id}v{Generation}";
}
