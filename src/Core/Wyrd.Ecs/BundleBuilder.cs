namespace Wyrd.Ecs;

/// <summary>
/// A chainable wrapper around an <see cref="IComponentSink"/>, for writing an
/// <see cref="IComponentBundle.ApplyTo{TSink}"/> implementation as one expression instead of
/// several statements. <c>Add</c> returns itself by value, the same convention
/// <see cref="EntityView.AddComponent{T}"/> already uses, not a mutable running total.
/// </summary>
public readonly ref struct BundleBuilder<TSink> where TSink : IComponentSink, allows ref struct
{
    private readonly TSink _sink;

    /// <summary>Wraps <paramref name="sink"/> for chaining.</summary>
    public BundleBuilder(TSink sink) => _sink = sink;

    /// <summary>Adds <paramref name="value"/> to the wrapped sink, returns this builder for further chaining.</summary>
    public BundleBuilder<TSink> Add<T>(T value) where T : struct, IComponent
    {
        _sink.AddComponent(value);
        return this;
    }
}
