using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Input;

/// <summary>
/// Registers an <see cref="IntentSystem{TAction}"/> and an
/// <see cref="IntentTickResetSystem{TAction}"/> on a <see cref="WorldBuilder"/>, bound to the
/// registered <see cref="PlatformSystem"/>. Order-independent: <c>AddWindow</c> can be called
/// before or after this method in the same chain. Registers via <c>AddSystemCore</c> directly,
/// not the generated <c>AddSystem&lt;T&gt;()</c> sugar, since that sugar needs
/// <c>Wyrd.Ecs.Generators</c> referenced for *this* compilation, which this package
/// deliberately never does (see <see cref="IntentSystem{TAction}"/>'s own doc comment for why
/// a generic <c>EcsSystem</c> can't go through the generator at all). Applies the
/// <see cref="Phase.PreUpdate"/>/<see cref="PlatformSystem"/> scheduling edges via
/// <c>SystemRegistration.Phase()</c>/<c>.After&lt;T&gt;()</c> on the registration this
/// returns, rather than as a class attribute on <see cref="IntentSystem{TAction}"/> itself:
/// those are *execution*-order edges, separate from the *construction*-order dependency
/// declared below, which <see cref="WorldBuilder.Build"/> resolves before either system is
/// constructed.
/// </summary>
public static class WorldBuilderInputExtensions
{
    extension<TAction>(WorldBuilder builder) where TAction : struct, Enum
    {
        /// <summary>Registers an <see cref="IntentSystem{TAction}"/> resolving <paramref name="bindings"/> every tick, plus its <see cref="IntentTickResetSystem{TAction}"/>.</summary>
        public WorldBuilder AddInput(BindingTable<TAction> bindings)
        {
            builder.AddSystemCore(
                typeof(IntentSystem<TAction>),
                access: null,
                construct: w => new IntentSystem<TAction>(w, w.GetSystem<PlatformSystem>(), bindings),
                generatedBeforeTargets: [],
                generatedAfterTargets: [],
                constructionDependencies: [typeof(PlatformSystem)])
                .Phase(Phase.PreUpdate)
                .After<PlatformSystem>();
            builder.AddSystemCore(
                typeof(IntentTickResetSystem<TAction>),
                access: null,
                construct: _ => new IntentTickResetSystem<TAction>(),
                generatedBeforeTargets: [],
                generatedAfterTargets: [],
                cadence: SystemCadence.Fixed,
                constructionDependencies: [typeof(IntentSystem<TAction>)])
                .Phase(Phase.PostUpdate);
            return builder;
        }
    }
}
