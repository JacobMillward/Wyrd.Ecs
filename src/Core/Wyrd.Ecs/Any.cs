namespace Wyrd.Ecs;

/// <summary>Query filter marker: require the archetype to contain at least one of <typeparamref name="T0"/>/<typeparamref name="T1"/>.</summary>
public readonly struct Any<T0, T1> where T0 : struct where T1 : struct;
