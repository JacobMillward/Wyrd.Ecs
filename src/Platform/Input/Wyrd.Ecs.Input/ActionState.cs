using System.Numerics;

namespace Wyrd.Ecs.Input;

/// <summary>
/// One action's resolved state for the current tick. <see cref="Value"/> is a courtesy
/// for a digital (<c>Bind</c>-only) action, not authoritative - read
/// <see cref="IsHeld"/>/<see cref="JustPressed"/>/<see cref="JustReleased"/> for those.
/// </summary>
public readonly record struct ActionState(bool IsHeld, bool JustPressed, bool JustReleased, Vector2 Value);
