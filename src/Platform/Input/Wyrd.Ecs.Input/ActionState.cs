using System.Numerics;

namespace Wyrd.Ecs.Input;

/// <summary>
/// One action's resolved state for the current tick. <see cref="Value"/> is a courtesy
/// for a digital (<c>Bind</c>-only) action, not authoritative - read
/// <see cref="IsHeld"/>/<see cref="JustPressed"/>/<see cref="JustReleased"/> for those.
/// <see cref="JustPressed"/>/<see cref="JustReleased"/> are only safe to read from a
/// <see cref="SystemCadence.Variable"/> system - they're recomputed fresh every real
/// <c>World.Update()</c> call by <see cref="IntentSystem{TAction}"/>, so a
/// <see cref="SystemCadence.Fixed"/> reader can miss or double-count an edge depending on
/// how the fixed-step accumulator lines up with real calls that tick. A
/// <see cref="SystemCadence.Fixed"/> system needing edge-triggered input should read
/// <see cref="TickJustPressed"/>/<see cref="TickJustReleased"/> instead: accumulated across
/// however many real calls occur with no fixed step, and cleared exactly once per fixed
/// step by <c>IntentTickResetSystem&lt;TAction&gt;</c>.
/// </summary>
public readonly record struct ActionState(
    bool IsHeld, bool JustPressed, bool JustReleased, Vector2 Value,
    bool TickJustPressed = false, bool TickJustReleased = false);
