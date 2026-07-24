namespace Wyrd.Ecs;

/// <summary>Query filter marker: require the archetype to NOT contain <typeparamref name="T"/>.</summary>
public readonly struct Without<T> where T : struct;
