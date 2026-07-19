namespace Wyrd.Ecs;

/// <summary>
/// Maps component types to a stable wire discriminator plus serialize/deserialize
/// delegates — the extension point a pluggable persistence layer is built from.
/// "Save everything" is registering every component type; a narrower policy registers
/// only the types it cares about. The same mechanism serves both, and Wyrd itself has
/// no opinion on which a given consumer chooses.
/// </summary>
public sealed class SerializerRegistry
{
    private readonly Dictionary<string, IRegisteredComponentType> _byDiscriminator = new();
    private readonly Dictionary<int, IRegisteredComponentType> _byTypeIndex = new();

    /// <summary>
    /// Registers <typeparamref name="T"/> under <paramref name="discriminator"/> — a
    /// caller-chosen, stable identifier, never <see cref="Internal.TypeIndex{T}"/>.
    /// Throws if <paramref name="discriminator"/> is already registered.
    /// </summary>
    public void Register<T>(string discriminator, ComponentSerializer<T> serialize, ComponentDeserializer<T> deserialize) where T : struct, IComponent
    {
        if (_byDiscriminator.ContainsKey(discriminator))
            throw new ArgumentException($"Discriminator '{discriminator}' is already registered.", nameof(discriminator));

        var entry = new Internal.RegisteredComponentType<T>(discriminator, serialize, deserialize);
        _byDiscriminator[discriminator] = entry;
        _byTypeIndex[entry.TypeIndex] = entry;
    }

    /// <summary>Looks up a registration by its current-process <see cref="Internal.TypeIndex{T}"/> — used by <see cref="World.EnumerateAll"/> while walking type-erased storage.</summary>
    public bool TryGetByTypeIndex(int typeIndex, out IRegisteredComponentType registered)
    {
        if (_byTypeIndex.TryGetValue(typeIndex, out var found))
        {
            registered = found;
            return true;
        }

        registered = null!;
        return false;
    }

    /// <summary>Looks up a registration by its stable wire discriminator — used when deserializing saved or received data back into a <see cref="World"/>.</summary>
    public bool TryGetByDiscriminator(string discriminator, out IRegisteredComponentType registered)
    {
        if (_byDiscriminator.TryGetValue(discriminator, out var found))
        {
            registered = found;
            return true;
        }

        registered = null!;
        return false;
    }
}
