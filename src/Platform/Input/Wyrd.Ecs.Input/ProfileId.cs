namespace Wyrd.Ecs.Input;

/// <summary>
/// Identifies an input profile (e.g. player 1 vs player 2, or a keyboard-and-mouse profile
/// vs a gamepad profile) within a <see cref="BindingTable{TAction}"/>. Wraps the raw
/// <see langword="int"/> so <c>Dictionary</c> keys and method signatures document what the
/// number means instead of exposing a bare index. The default value, <c>default(ProfileId)</c>,
/// is profile 0.
/// </summary>
public readonly record struct ProfileId(int Value);
