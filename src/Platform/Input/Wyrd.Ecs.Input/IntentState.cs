using System.Numerics;

namespace Wyrd.Ecs.Input;

/// <summary>
/// Per-tick resolved action state, published as a World resource by
/// <c>IntentSystem&lt;TAction&gt;</c>. Construct via <c>new()</c>, never
/// <c>default(IntentState&lt;TAction&gt;)</c> - the backing dictionary/lists are allocated
/// by this type's field initializers, which only run for an explicit <c>new()</c>.
/// </summary>
public struct IntentState<TAction> : IResource where TAction : struct, Enum
{
    internal readonly Dictionary<(TAction Action, int Seat), ActionState> States = [];
    internal readonly List<uint> ConnectedThisTickList = [];
    internal readonly List<uint> DisconnectedThisTickList = [];

    /// <summary>Allocates the backing collections - always use this, never <c>default</c>.</summary>
    public IntentState()
    {
    }

    /// <summary>The mouse's current position in window coordinates, shared across every seat.</summary>
    public Vector2 MousePosition { get; internal set; }

    /// <summary>The mouse's movement this tick, shared across every seat. Reset to zero every tick.</summary>
    public Vector2 MouseDelta { get; internal set; }

    /// <summary>The mouse wheel's movement this tick, shared across every seat. Reset to zero every tick.</summary>
    public Vector2 WheelDelta { get; internal set; }

    /// <summary>Device ids that connected this tick (keyboards/mice). Reset every tick.</summary>
    public IReadOnlyList<uint> DevicesConnectedThisTick => ConnectedThisTickList;

    /// <summary>Device ids that disconnected this tick (keyboards/mice). Reset every tick.</summary>
    public IReadOnlyList<uint> DevicesDisconnectedThisTick => DisconnectedThisTickList;

    /// <summary>The current state of <paramref name="action"/> at <paramref name="seat"/>. Throws if never bound - use <see cref="TryGet"/> if that's expected.</summary>
    public ActionState this[TAction action, int seat = 0] =>
        States.TryGetValue((action, seat), out var state)
            ? state
            : throw new InvalidOperationException($"No binding exists for action '{action}' at seat {seat}. Call Bind()/BindAxis2D() for it first.");

    /// <summary>Same as the indexer, without throwing when the action was never bound.</summary>
    public bool TryGet(TAction action, out ActionState state, int seat = 0) => States.TryGetValue((action, seat), out state);
}
