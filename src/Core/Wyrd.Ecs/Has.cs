namespace Wyrd.Ecs;

/// <summary>Query filter marker: require the archetype to contain <typeparamref name="T"/>. Never yields an accessor — <typeparamref name="T"/>'s data is not read.</summary>
public readonly struct Has<T> where T : struct;
