using System.Numerics;

namespace Wyrd.Ecs.Input;

/// <summary>
/// Per-tick resolved action state, published as a World resource by
/// <c>IntentSystem&lt;TAction&gt;</c>. Construct via <c>new()</c>, never
/// <c>default(IntentState&lt;TAction&gt;)</c>: the backing dictionary/lists are allocated
/// by this type's field initializers, which only run for an explicit <c>new()</c>.
/// </summary>
public struct IntentState<TAction> : IResource where TAction : struct, Enum
{
    internal readonly Dictionary<(TAction Action, ProfileId Profile), ActionState> States = [];

    /// <summary>Allocates the backing collections. Always use this, never <c>default</c>.</summary>
    public IntentState()
    {
    }

    /// <summary>The mouse's current position in window coordinates, shared across every profile.</summary>
    public Vector2 MousePosition { get; internal set; }

    /// <summary>The mouse's movement this tick, shared across every profile. Reset to zero every tick.</summary>
    public Vector2 MouseDelta { get; internal set; }

    /// <summary>The mouse wheel's movement this tick, shared across every profile. Reset to zero every tick.</summary>
    public Vector2 WheelDelta { get; internal set; }

    /// <summary>The current state of <paramref name="action"/> at <paramref name="profile"/>. Throws if never bound: use <see cref="TryGet"/> if that's expected.</summary>
    public ActionState this[TAction action, ProfileId profile = default] =>
        States.TryGetValue((action, profile), out var state)
            ? state
            : throw new InvalidOperationException($"No binding exists for action '{action}' at profile {profile.Value}. Call Bind()/BindAxis2D() for it first.");

    /// <summary>Same as the indexer, without throwing when the action was never bound.</summary>
    public bool TryGet(TAction action, out ActionState state, ProfileId profile = default) => States.TryGetValue((action, profile), out state);
}
