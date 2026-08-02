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
/// callback overload. Frozen after first instantiation — see <see cref="ThrowIfFrozen"/> —
/// so mutating a shared/reused template after (or concurrently with) instantiating it
/// throws instead of silently corrupting <see cref="_settersByType"/>.
/// </summary>
public class EntityTemplate
{
    private readonly Lock _gate = new();
    private Internal.ArchetypeSignature _signature = Internal.ArchetypeSignature.Empty;
    private readonly Dictionary<int, TemplateComponentSetter> _settersByType = new();
    private TemplateComponentSetter[]? _cachedSetters;
    private bool _frozen;

    /// <summary>The archetype signature every instance of this template lands in (components + tags). Computed incrementally as <see cref="AddComponent{T}"/>/<see cref="AddTag{T}"/> are called. Reading this freezes the template — see <see cref="ThrowIfFrozen"/>.</summary>
    internal Internal.ArchetypeSignature Signature
    {
        get { lock (_gate) { _frozen = true; return _signature; } }
    }

    /// <summary>
    /// Every component setter on this template. Backed by <see cref="_settersByType"/> (keyed
    /// by type so a repeated <see cref="AddComponent{T}"/> call for the same component type
    /// replaces rather than duplicates), but exposed as a concrete array — not
    /// <c>IReadOnlyCollection&lt;T&gt;</c> — cached after the first read and invalidated only
    /// by a further <see cref="AddComponent{T}"/> call, so a reused template still pays the
    /// materialization cost once, not per instantiate. The concrete array type matters, not
    /// just the caching: an interface-typed <c>foreach</c> (as this used to be) forces a
    /// boxed, virtually-dispatched enumerator on every hot-path instantiate call; iterating a
    /// <c>TemplateComponentSetter[]</c> directly doesn't. Reading this freezes the template —
    /// see <see cref="ThrowIfFrozen"/>.
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
    /// in-progress freeze-and-read on another thread — this is a real concurrency guard,
    /// not just a same-thread sequencing check, since template instantiation
    /// (<see cref="CommandBuffer.CreateEntity(EntityTemplate)"/>) is itself documented safe
    /// to call from several threads at once.
    /// </summary>
    private void ThrowIfFrozen()
    {
        if (_frozen)
            throw new InvalidOperationException("This EntityTemplate has already been instantiated and can no longer be modified.");
    }

    /// <summary>
    /// Adds <paramref name="value"/> as this template's <typeparamref name="T"/>. Calling
    /// this twice for the same <typeparamref name="T"/> on one template replaces the
    /// earlier value — last call wins, matching <see cref="CommandBuffer.AddComponent{T}(Entity, T)"/>'s
    /// already-documented stance for live entities.
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
    /// Adds tag <typeparamref name="T"/> to this template. Unlike <see cref="AddComponent{T}"/>,
    /// this only ORs <typeparamref name="T"/>'s type index into <see cref="Signature"/> — a
    /// tag contributes no storage (<c>Archetype.Signature</c>'s own doc: "tags contribute
    /// only to Signature, never get a storage entry"), so there's no setter to build. Its
    /// entire instantiation cost is already paid by build time.
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
    /// recursively) and connects each to its parent via the <see cref="Parent"/> relation
    /// — one archetype move per node, no matter the tree's depth. <paramref name="child"/>
    /// is a value, reusable from multiple parents (each instantiation creates its own,
    /// independent set of entities). Because <paramref name="child"/> must already exist as
    /// a constructed <see cref="EntityTemplate"/> before it can be passed here, a cycle in
    /// the child graph is structurally impossible.
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
    /// When this template is instantiated as the root of a <see cref="CommandBuffer.CreateEntity(EntityTemplate)"/>
    /// call, attaches it to <paramref name="parent"/> — an already-existing entity, not one
    /// created as part of this template's own tree. Different cost profile from
    /// <see cref="AddChild"/>: the new entity's own side is still one move (placed directly
    /// with <see cref="RelationLinks{T}"/> pre-populated), but <paramref name="parent"/>
    /// already has its own archetype, so its <see cref="RelationBacklinks{T}"/> update pays
    /// the same one-move cost <see cref="CommandBuffer.AddRelation{T}(Entity, Entity, T)"/>
    /// already pays today. Whether the edge is included at all is decided synchronously,
    /// right now (via <c>World.IsAlive</c>), not deferred to <see cref="World.ApplyCommands()"/>
    /// like every other queued operation's target-aliveness check — a consequence of
    /// direct-to-final-archetype placement needing its target signature fixed before any
    /// command in the batch runs. This doesn't protect against <paramref name="parent"/>
    /// being destroyed by an earlier queued command in the same buffer before this
    /// template's placement actually applies — the same class of limitation
    /// <see cref="CommandBuffer.Apply"/>'s own doc comment already describes for ordering in
    /// general. Throws at instantiate time if this template is also reached as someone's
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
