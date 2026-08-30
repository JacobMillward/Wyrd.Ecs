namespace Wyrd.Ecs.Input;

/// <summary>
/// Clears <see cref="ActionState.TickJustPressed"/>/<see cref="ActionState.TickJustReleased"/>
/// on every entry in <see cref="IntentState{TAction}"/>, once per fixed step. Registered
/// <see cref="SystemCadence.Fixed"/> + <see cref="Phase.PostUpdate"/> by
/// <c>WorldBuilderInputExtensions.AddInput</c>. <see cref="Phase.PostUpdate"/>'s "runs after
/// every Update/PreUpdate system, with no edge needed from anything else" guarantee means
/// this always runs after every other <see cref="SystemCadence.Fixed"/> gameplay system
/// within the same fixed-step iteration, so a catch-up burst's second and later substeps see
/// an already-cleared pair and never double-count an edge the first substep already consumed.
/// </summary>
public sealed class IntentTickResetSystem<TAction> : EcsSystem where TAction : struct, Enum
{
    private readonly List<(TAction Action, ProfileId Profile)> _keys = [];

    /// <inheritdoc/>
    protected override void Execute(World world, Time time)
    {
        ref var state = ref world.GetResourceRef<IntentState<TAction>>();
        _keys.Clear();
        _keys.AddRange(state.States.Keys);
        foreach (var key in _keys)
            state.States[key] = state.States[key] with { TickJustPressed = false, TickJustReleased = false };
    }
}
