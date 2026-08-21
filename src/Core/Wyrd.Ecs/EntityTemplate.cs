namespace Wyrd.Ecs;

/// <summary>
/// One setter for one component type on an <see cref="EntityTemplate"/>, built once at
/// template-definition time so the component's value is captured unboxed in the closure.
/// Always writes via <see cref="Internal.ComponentStorage{T}.Fill"/>, even for a single
/// entity, so <see cref="CommandBuffer.CreateEntity(EntityTemplate)"/> and its batch
/// counterpart share one code path.
/// </summary>
internal delegate void TemplateComponentSetter(World world, Internal.Archetype archetype, int startRow, int count);

/// <summary>
/// A reusable definition of an entity's starting components, tags, and children. Use it
/// directly (<c>new EntityTemplate().AddComponent(...)</c>) for runtime-composed shapes, or
/// subclass it for named, hand-authored prefabs. Frozen after first instantiation: mutating
/// a shared or reused template afterward throws instead of silently corrupting it.
/// </summary>
public class EntityTemplate : IComponentSink
{
    private readonly Lock _gate = new();
    private Internal.TypeBitSet _signature = Internal.TypeBitSet.Empty;
    private readonly Dictionary<int, TemplateComponentSetter> _settersByType = new();
    private TemplateComponentSetter[]? _cachedSetters;
    private bool _frozen;

    /// <summary>The archetype signature every instance of this template lands in (components + tags). Computed incrementally as <see cref="AddComponent{T}"/>/<see cref="AddTag{T}"/> are called. Reading this freezes the template, see <see cref="ThrowIfFrozen"/>.</summary>
    internal Internal.TypeBitSet Signature
    {
        get { lock (_gate) { _frozen = true; return _signature; } }
    }

    /// <summary>
    /// Every component setter on this template, backed by <see cref="_settersByType"/> (keyed
    /// by type so a repeated <see cref="AddComponent{T}"/> call for the same component type
    /// replaces rather than duplicates). Cached after the first read and invalidated only by
    /// a further <see cref="AddComponent{T}"/> call. Reading this freezes the template; see
    /// <see cref="ThrowIfFrozen"/>.
    /// </summary>
    internal TemplateComponentSetter[] Setters
    {
        get { lock (_gate) { _frozen = true; return _cachedSetters ??= [.. _settersByType.Values]; } }
    }

    /// <summary>
    /// Throws if this template has already been read for instantiation (via
    /// <see cref="Signature"/> or <see cref="Setters"/>). Caller must already hold
    /// <see cref="_gate"/>: every mutator below takes the same lock <see cref="Signature"/>/
    /// <see cref="Setters"/> freeze under, so a mutation can never interleave with an
    /// in-progress freeze-and-read on another thread.
    /// </summary>
    private void ThrowIfFrozen()
    {
        if (_frozen)
            throw new InvalidOperationException("This EntityTemplate has already been instantiated and can no longer be modified.");
    }

    /// <summary>
    /// Adds <paramref name="value"/> as this template's <typeparamref name="T"/>. Calling
    /// this twice for the same <typeparamref name="T"/> replaces the earlier value: last
    /// call wins.
    /// </summary>
    public EntityTemplate AddComponent<T>(T value) where T : struct, IComponent
    {
        lock (_gate)
        {
            ThrowIfFrozen();
            var typeIndex = Internal.TypeIndex<T>.Value;
            _signature = _signature.With(typeIndex);
            _settersByType[typeIndex] = MakeSetter(value);
            _cachedSetters = null;
        }
        return this;
    }

    /// <summary>
    /// Adds <see cref="Transform"/>, and, unless <paramref name="isStatic"/>, a matching
    /// <see cref="PreviousTransform"/> (equal to <paramref name="value"/>) too, mirroring
    /// <see cref="EntityView.AddTransform(Transform, bool)"/>. <paramref name="isStatic"/>
    /// entities never match <see cref="TransformSnapshotSystem"/>'s query (it requires both
    /// components), so they skip the per-tick snapshot copy entirely, the right choice for
    /// anything placed once and never moved again (level geometry, background art).
    /// </summary>
    public EntityTemplate AddTransform(Transform value, bool isStatic = false)
    {
        AddComponent(value);
        if (!isStatic)
            AddComponent(new PreviousTransform { Position = value.Position, Rotation = value.Rotation, Scale = value.Scale });
        return this;
    }

    /// <inheritdoc/>
    void IComponentSink.AddComponent<T>(T value) => AddComponent(value);

    /// <summary>Adds every component in <paramref name="bundle"/> to this template. See <see cref="IComponentBundle"/>.</summary>
    public EntityTemplate Add<TBundle>(TBundle bundle) where TBundle : IComponentBundle
    {
        bundle.ApplyTo(this);
        return this;
    }

    /// <summary>
    /// Adds tag <typeparamref name="T"/> to this template. Unlike <see cref="AddComponent{T}"/>,
    /// this only ORs <typeparamref name="T"/>'s type index into <see cref="Signature"/>: a tag
    /// contributes no storage, so there's no setter to build.
    /// </summary>
    public EntityTemplate AddTag<T>() where T : struct, ITag
    {
        lock (_gate)
        {
            ThrowIfFrozen();
            _signature = _signature.With(Internal.TypeIndex<T>.Value);
        }
        return this;
    }

    private readonly List<EntityTemplate> _children = new();

    /// <summary>Every child template attached via <see cref="AddChild"/>, in call order.</summary>
    internal IReadOnlyList<EntityTemplate> Children => _children;

    /// <summary>
    /// Attaches <paramref name="child"/> as a child of this template: instantiating this
    /// template also instantiates <paramref name="child"/> (and its own children,
    /// recursively), connecting each to its parent via the <see cref="Parent"/> relation
    /// in one archetype move per node. <paramref name="child"/> is reusable from multiple
    /// parents; each instantiation creates its own independent set of entities. A cycle in
    /// the child graph is structurally impossible, since <paramref name="child"/> must
    /// already exist before it can be passed here.
    /// </summary>
    public EntityTemplate AddChild(EntityTemplate child)
    {
        lock (_gate)
        {
            ThrowIfFrozen();
            _children.Add(child);
        }
        return this;
    }

    /// <summary>The entity this template's root should be parented to on instantiation, via <see cref="AddParent"/>. <c>null</c> if not set.</summary>
    internal Entity? ExplicitParent { get; private set; }

    /// <summary>
    /// When this template is instantiated as the root of a
    /// <see cref="CommandBuffer.CreateEntity(EntityTemplate)"/> call, attaches it to
    /// <paramref name="parent"/>, an already-existing entity, not one created as part of
    /// this template's own tree. Whether the edge is included is decided synchronously,
    /// right now (via <c>World.IsAlive</c>), not deferred to
    /// <see cref="World.ApplyCommands()"/> like every other queued operation's
    /// target-aliveness check, so it doesn't protect against <paramref name="parent"/> being
    /// destroyed by an earlier queued command before this template's placement applies.
    /// Throws at instantiate time if this template is also reached as someone's
    /// <see cref="AddChild"/> in the same tree (two parents for one node).
    /// </summary>
    public EntityTemplate AddParent(Entity parent)
    {
        lock (_gate)
        {
            ThrowIfFrozen();
            ExplicitParent = parent;
        }
        return this;
    }

    private static TemplateComponentSetter MakeSetter<T>(T value) where T : struct, IComponent =>
        (world, archetype, startRow, count) =>
        {
            var storage = archetype.GetOrCreateStorage<T>();
            storage.Fill(startRow, count, value);
            world.MarkDirtyRangeIfTracked(storage, startRow, count);
        };
}
