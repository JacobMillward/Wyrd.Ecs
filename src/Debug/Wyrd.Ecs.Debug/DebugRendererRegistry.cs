using Wyrd.Ecs.Debug.Abstractions;

namespace Wyrd.Ecs.Debug;

/// <summary>One registered custom renderer's describe/apply delegates, type-erased over <c>object</c>.</summary>
public sealed record DebugRendererRegistration(
    Func<object, InspectorField> Describe,
    Func<object, InspectorEdit, object> Apply);

/// <summary>
/// Type-erased renderer registrations, keyed by the same debug name
/// <see cref="Wyrd.Ecs.Internal.DebugNameRegistry"/> resolves for the type, populated by
/// <c>Wyrd.Ecs.Debug.Generators.DebugRendererRegistrationGenerator</c>'s module
/// initializer - no caller-invoked setup.
/// </summary>
public static class DebugRendererRegistry
{
    private static readonly Dictionary<string, DebugRendererRegistration> _byName = new();

    /// <summary>Registers describe/apply delegates for <paramref name="debugName"/>. Called by generated code; not meant for hand-written call sites.</summary>
    public static void Register(string debugName, Func<object, InspectorField> describe, Func<object, InspectorEdit, object> apply) =>
        _byName[debugName] = new DebugRendererRegistration(describe, apply);

    /// <summary>Looks up a registered renderer by debug name. False if the type has no <c>[DebugRenderer]</c>.</summary>
    public static bool TryGetRenderer(string debugName, out DebugRendererRegistration registration) =>
        _byName.TryGetValue(debugName, out registration!);
}
