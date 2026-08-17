namespace Wyrd.Ecs;

public sealed partial class World
{
    private object?[] _resources = [];

    /// <summary>
    /// Registers <paramref name="instance"/> as the <typeparamref name="T"/> resource.
    /// Throws if one is already registered; call <see cref="RemoveResource{T}"/> first to
    /// replace it.
    /// </summary>
    public World AddResource<T>(T instance) where T : struct, IResource
    {
        SetResourceCore(instance);
        return this;
    }

    /// <summary>Same as <see cref="AddResource{T}(T)"/>, but builds the value from a factory that receives this <see cref="World"/>.</summary>
    public World AddResource<T>(Func<World, T> factory) where T : struct, IResource
    {
        SetResourceCore(factory(this));
        return this;
    }

    private void SetResourceCore<T>(T value) where T : struct, IResource
    {
        var index = Internal.TypeIndex<T>.Value;
        if (index >= _resources.Length)
            Array.Resize(ref _resources, index + 1);
        if (_resources[index] is not null)
            throw new InvalidOperationException(
                $"A resource of type '{typeof(T)}' is already registered. Call RemoveResource<T>() first to replace it.");
        _resources[index] = new[] { value };
    }

    /// <summary>Removes the registered <typeparamref name="T"/> resource, if any. Returns whether one was removed.</summary>
    public bool RemoveResource<T>() where T : struct, IResource
    {
        var index = Internal.TypeIndex<T>.Value;
        if (index >= _resources.Length || _resources[index] is null) return false;
        _resources[index] = null;
        return true;
    }

    /// <summary>A copy of the registered <typeparamref name="T"/> resource. Throws if none is registered; use <see cref="TryGetResource{T}"/> if that's expected.</summary>
    public T GetResource<T>() where T : struct, IResource => GetResourceRef<T>();

    /// <summary>A mutable reference into the registered <typeparamref name="T"/> resource's storage. Throws if none is registered.</summary>
    public ref T GetResourceRef<T>() where T : struct, IResource
    {
        var index = Internal.TypeIndex<T>.Value;
        if (index >= _resources.Length || _resources[index] is not T[] array)
            throw new InvalidOperationException($"No resource of type {typeof(T)} is registered.");
        return ref array[0];
    }

    /// <summary>Same as <see cref="GetResource{T}"/>, without throwing when nothing is registered.</summary>
    public bool TryGetResource<T>(out T value) where T : struct, IResource
    {
        var index = Internal.TypeIndex<T>.Value;
        if (index < _resources.Length && _resources[index] is T[] array)
        {
            value = array[0];
            return true;
        }
        value = default;
        return false;
    }
}
