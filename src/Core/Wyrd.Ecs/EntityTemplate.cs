namespace Wyrd.Ecs;

/// <summary>
/// One setter for one component type on an <see cref="EntityTemplate"/>, built once at
/// template-definition time via a generic factory (<see cref="EntityTemplate.AddComponent{T}"/>)
/// so the component's value is captured unboxed in the closure, not boxed into
/// <see cref="object"/>. Always writes via <see cref="Internal.ComponentStorage{T}.Fill"/>,
/// even for a single entity (<c>count == 1</c>): this lets the identical setter serve both
/// <see cref="CommandBuffer.CreateEntity(EntityTemplate)"/> and its batch counterpart with no
/// separate single-entity code path.
/// </summary>
internal delegate void TemplateComponentSetter(World world, Internal.Archetype archetype, int startRow, int count);

/// <summary>
/// A reusable, hand-authorable (or runtime/mod-composed) definition of an entity's starting
/// components/tags/children. Not sealed and constructed via a public parameterless
/// constructor so it can be used two ways with the exact same mechanism: directly
/// (<c>new EntityTemplate().AddComponent(...)</c>) for runtime-composed shapes, or
/// subclassed with a constructor calling the same members, for named, hand-authored
/// prefabs. See the design doc's section A for why there's no separate builder type and no
/// callback overload. Conventionally treated as read-only once used to instantiate — not
/// runtime-enforced, the same trust-the-caller stance <see cref="RelationLinks{T}"/>'s own
/// doc comment takes for its invariants.
/// </summary>
public class EntityTemplate
{
    private Internal.ArchetypeSignature _signature = Internal.ArchetypeSignature.Empty;
    private readonly Dictionary<int, TemplateComponentSetter> _setters = new();

    /// <summary>The archetype signature every instance of this template lands in (components + tags). Computed incrementally as <see cref="AddComponent{T}"/>/<see cref="AddTag{T}"/> are called.</summary>
    internal Internal.ArchetypeSignature Signature => _signature;

    /// <summary>
    /// Every component setter on this template, keyed internally by type so a repeated
    /// <see cref="AddComponent{T}"/> call for the same component type replaces rather than
    /// duplicates. Returns the dictionary's live <c>Values</c> view, not a copy — matches
    /// <see cref="ComponentCodecRegistry.All"/>'s existing pattern for the same reason:
    /// repeated reads (once per instantiate call) cost nothing extra.
    /// </summary>
    internal IReadOnlyCollection<TemplateComponentSetter> Setters => _setters.Values;

    /// <summary>
    /// Adds <paramref name="value"/> as this template's <typeparamref name="T"/>. Calling
    /// this twice for the same <typeparamref name="T"/> on one template replaces the
    /// earlier value — last call wins, matching <see cref="CommandBuffer.AddComponent{T}(Entity, T)"/>'s
    /// already-documented stance for live entities.
    /// </summary>
    public EntityTemplate AddComponent<T>(T value) where T : struct, IComponent
    {
        var typeIndex = Internal.TypeIndex<T>.Value;
        _signature = _signature.With(typeIndex);
        _setters[typeIndex] = MakeSetter(value);
        return this;
    }

    /// <summary>
    /// Adds tag <typeparamref name="T"/> to this template. Unlike <see cref="AddComponent{T}"/>,
    /// this only ORs <typeparamref name="T"/>'s type index into <see cref="Signature"/> — a
    /// tag contributes no storage (<c>Archetype.Signature</c>'s own doc: "tags contribute
    /// only to Signature, never get a storage entry"), so there's no setter to build. Its
    /// entire instantiation cost is already paid by build time.
    /// </summary>
    public EntityTemplate AddTag<T>() where T : struct, ITag
    {
        _signature = _signature.With(Internal.TypeIndex<T>.Value);
        return this;
    }

    private static TemplateComponentSetter MakeSetter<T>(T value) where T : struct, IComponent =>
        (world, archetype, startRow, count) =>
        {
            var storage = archetype.GetOrCreateStorage<T>();
            storage.Fill(startRow, count, value);
            if (world.IsTracked(Internal.TypeIndex<T>.Value))
                storage.MarkDirtyRange(startRow, count, world.CurrentTick);
        };
}
