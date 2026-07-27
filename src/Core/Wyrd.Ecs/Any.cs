namespace Wyrd.Ecs;

/// <summary>Query filter marker: require the archetype to contain at least one of <typeparamref name="T0"/>/<typeparamref name="T1"/>.</summary>
public readonly struct Any<T0, T1> where T0 : struct where T1 : struct;

/// <summary>Query filter marker: require the archetype to contain at least one of <typeparamref name="T0"/>/<typeparamref name="T1"/>/<typeparamref name="T2"/>.</summary>
public readonly struct Any<T0, T1, T2> where T0 : struct where T1 : struct where T2 : struct;

/// <summary>4-way <see cref="Any{T0, T1}"/>.</summary>
public readonly struct Any<T0, T1, T2, T3> where T0 : struct where T1 : struct where T2 : struct where T3 : struct;

/// <summary>5-way <see cref="Any{T0, T1}"/>.</summary>
public readonly struct Any<T0, T1, T2, T3, T4> where T0 : struct where T1 : struct where T2 : struct where T3 : struct where T4 : struct;

/// <summary>6-way <see cref="Any{T0, T1}"/>.</summary>
public readonly struct Any<T0, T1, T2, T3, T4, T5> where T0 : struct where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct;

/// <summary>7-way <see cref="Any{T0, T1}"/>.</summary>
public readonly struct Any<T0, T1, T2, T3, T4, T5, T6> where T0 : struct where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct where T6 : struct;

/// <summary>8-way <see cref="Any{T0, T1}"/>.</summary>
public readonly struct Any<T0, T1, T2, T3, T4, T5, T6, T7> where T0 : struct where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct where T6 : struct where T7 : struct;
