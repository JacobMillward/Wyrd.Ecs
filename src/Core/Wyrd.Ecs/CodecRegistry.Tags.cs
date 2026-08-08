namespace Wyrd.Ecs;

public sealed partial class CodecRegistry
{
    private readonly Dictionary<string, ITagBinder> _tagsByDiscriminator = new();
    private readonly Dictionary<int, ITagBinder> _tagsByTypeIndex = new();

    /// <summary>
    /// Registers <typeparamref name="T"/> (a tag: no data, no schema) under
    /// <paramref name="discriminator"/> for real persistence - checkpoint Save/Load and
    /// continuous/WAL both consult this. A tag discriminator may collide with a component
    /// discriminator (separate dictionary); two tags may not collide with each other.
    /// Participates in the shared alias table: <see cref="RegisterAlias"/>/
    /// <see cref="RenamedFromAttribute"/> work for a tag exactly like they do for a
    /// component.
    /// </summary>
    public void RegisterTag<T>(string discriminator) where T : struct, ITag
    {
        if (_tagsByDiscriminator.ContainsKey(discriminator))
            throw new ArgumentException($"Discriminator '{discriminator}' is already registered.", nameof(discriminator));

        var typeIndex = Internal.TypeIndex<T>.Value;
        if (_tagsByTypeIndex.TryGetValue(typeIndex, out var existing))
            throw new ArgumentException($"Type '{typeof(T)}' is already registered under discriminator '{existing.Discriminator}'.");

        var binder = new Internal.TagBinder<T>(discriminator);
        _tagsByDiscriminator[discriminator] = binder;
        _tagsByTypeIndex[typeIndex] = binder;
    }

    /// <summary>Looks up a registered tag by its current-process <see cref="Internal.TypeIndex{T}"/>.</summary>
    public bool TryGetTagByTypeIndex(int typeIndex, out ITagBinder registered)
    {
        if (_tagsByTypeIndex.TryGetValue(typeIndex, out var found))
        {
            registered = found;
            return true;
        }

        registered = null!;
        return false;
    }

    /// <summary>Looks up a registered tag by its stable wire discriminator, falling back through the alias chain (<see cref="RegisterAlias"/>) if there's no direct hit.</summary>
    public bool TryGetTagByDiscriminator(string discriminator, out ITagBinder registered)
    {
        var visited = new HashSet<string>();
        while (true)
        {
            if (_tagsByDiscriminator.TryGetValue(discriminator, out var found))
            {
                registered = found;
                return true;
            }

            if (!_aliases.TryGetValue(discriminator, out var next) || !visited.Add(discriminator))
            {
                registered = null!;
                return false;
            }
            discriminator = next;
        }
    }
}
