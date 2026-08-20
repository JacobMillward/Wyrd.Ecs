using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Input;

/// <summary>
/// Registers an <see cref="IntentSystem{TAction}"/> on a <see cref="WorldBuilder"/>,
/// bound to the already-registered <see cref="PlatformSystem"/>. Requires
/// <c>AddPlatform</c> to have been called earlier in the same builder chain, same
/// requirement as <c>AddRenderer</c>. Registers via <c>AddSystemCore</c> directly, not the
/// generated <c>AddSystem&lt;T&gt;()</c> sugar <c>AddPlatform</c>/<c>AddRenderer</c> use -
/// that sugar needs <c>Wyrd.Ecs.Generators</c> referenced for *this* compilation, which
/// this package deliberately never does (see <see cref="IntentSystem{TAction}"/>'s own doc
/// comment for why a generic <c>EcsSystem</c> can't go through the generator at all).
/// Applies the <see cref="Phase.PreUpdate"/>/<see cref="PlatformSystem"/> scheduling edges
/// via <c>SystemRegistration.Phase()</c>/<c>.After&lt;T&gt;()</c> on the registration this
/// returns, rather than as a class attribute on <see cref="IntentSystem{TAction}"/> itself.
/// </summary>
public static class WorldBuilderInputExtensions
{
    extension<TAction>(WorldBuilder builder) where TAction : struct, Enum
    {
        /// <summary>Registers an <see cref="IntentSystem{TAction}"/> resolving <paramref name="bindings"/> every tick.</summary>
        public WorldBuilder AddInput(BindingTable<TAction> bindings)
        {
            builder.AddSystemCore(
                typeof(IntentSystem<TAction>),
                access: null,
                construct: w => new IntentSystem<TAction>(w, w.GetSystem<PlatformSystem>(), bindings),
                generatedBeforeTargets: [],
                generatedAfterTargets: [])
                .Phase(Phase.PreUpdate)
                .After<PlatformSystem>();
            return builder;
        }
    }
}
