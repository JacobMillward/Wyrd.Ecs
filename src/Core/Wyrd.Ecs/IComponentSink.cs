namespace Wyrd.Ecs;

/// <summary>
/// Something an <see cref="IComponentBundle"/> can add components to: implemented by
/// <see cref="EntityView"/> and <see cref="EntityTemplate"/>, letting one bundle
/// implementation work against either without boxing (dispatched via a generic method
/// constrained <c>allows ref struct</c>, since <see cref="EntityView"/> is a ref struct).
/// </summary>
public interface IComponentSink
{
    /// <summary>Adds <paramref name="value"/> as this sink's <typeparamref name="T"/>.</summary>
    void AddComponent<T>(T value) where T : struct, IComponent;
}
