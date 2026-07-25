using System.Threading;

namespace Wyrd.Ecs.Internal;

/// <summary>
/// The world's entity identity table: generation-checked ids for liveness, permanent
/// opaque ids (see <see cref="EntityId"/>), and each live entity's current archetype
/// and row. Owns id allocation and recycling. A mutable struct, embedded directly in
/// <see cref="World"/> rather than a class, so hot-path location lookups don't pay for
/// an extra heap indirection to reach it — it's never referenced from anywhere but its
/// owning <see cref="World"/>, so it doesn't need reference semantics.
///
/// <para>
/// <see cref="Reserve"/> is the one method here callable concurrently from several
/// threads at once (via <see cref="CommandBuffer.CreateEntity"/>, from several systems
/// in the same <c>ScheduledExecutor</c> stage) — every other method on this type still
/// assumes single-threaded, pre-/post-stage access. Rather than a lock, it mirrors
/// Bevy's <c>Entities::reserve_entity</c>: an atomic cursor (<see cref="_freeCursor"/>)
/// into the recycled-id list (<see cref="_pending"/>), falling back to minting a
/// brand-new id (computed from <see cref="_nextId"/>'s value at the start of this
/// reservation batch) once the cursor goes negative — no CAS retry loop, no lock, and
/// no two concurrent callers can ever be handed the same id. <see cref="_pending"/>
/// (via <see cref="Retire"/>) and <see cref="_nextId"/>/<see cref="_freeCursor"/> (via
/// <see cref="Place"/>/<see cref="FlushReservations"/> respectively) are still only
/// ever mutated single-threaded — this works because those three only ever run at the
/// join point after a stage's systems have all returned, never concurrently with a
/// <see cref="Reserve"/> call from that same stage.
/// </para>
/// </summary>
internal struct EntityTable
{
    private EntityId[] _permanentIds = new EntityId[4];
    private int[] _generations = new int[4];
    private (Archetype Archetype, int Row)[] _locations = new (Archetype, int)[4];
    private int[] _pending = Array.Empty<int>();
    private int _freeCursor;
    private bool[] _reserved = new bool[4];
    private int _nextId = 1; // Id 0 is reserved for Entity.Null.

    public EntityTable() { }

    /// <summary>The archetype+row currently backing entity id <paramref name="id"/>.</summary>
    internal ref (Archetype Archetype, int Row) this[int id] => ref _locations[id];

    internal EntityId PermanentId(int id) => _permanentIds[id];

    /// <summary>
    /// A direct array index, not a <c>HashSet&lt;int&gt;</c> — this is called on every
    /// single structural mutation (<see cref="CommandBuffer"/> checks it before almost every
    /// queued operation) plus every direct component read/write, so it's the hottest
    /// correctness check in the engine. A hash lookup here was real, avoidable overhead
    /// on that path; a bounds-checked array read costs a fraction of it.
    /// </summary>
    internal bool IsAlive(int id, int generation) =>
        id > 0 && id < _nextId && _generations[id] == generation && !_reserved[id];

    /// <summary>
    /// Reserves a fresh entity id without placing it into any archetype — not
    /// <see cref="IsAlive"/> until <see cref="Place"/> runs. Lets a caller (currently
    /// only <see cref="CommandBuffer"/>) hand back a real, usable <see cref="Entity"/>
    /// immediately for chaining further deferred commands against it, while the actual
    /// archetype placement happens later, at apply time. Safe to call concurrently
    /// from several threads at once — see the class doc for the lock-free scheme.
    /// </summary>
    internal Entity Reserve()
    {
        var cursor = Interlocked.Decrement(ref _freeCursor);
        if (cursor >= 0)
        {
            // Reuse a recycled id. _pending/_generations are read-only from here --
            // both are only ever written back on the single-threaded side of the join
            // point (Retire/FlushReservations), never concurrently with a Reserve call.
            var id = _pending[cursor];
            _reserved[id] = true;
            return new Entity(id, _generations[id]);
        }
        else
        {
            // No recycled id available (or the recycled pool is exhausted mid-batch):
            // mint a brand-new id. _nextId only ever changes single-threaded (in
            // Place), never concurrently with this branch, so every concurrent caller
            // in this batch reads the same stable value and lands on a distinct,
            // increasing id with no CAS retry needed. A never-before-used id's
            // generation is always 0; array capacity for it is grown later, in Place
            // -- see Place's own doc for why _reserved doesn't need touching here.
            var id = _nextId - cursor - 1;
            return new Entity(id, 0);
        }
    }

    /// <summary>
    /// Places a previously-<see cref="Reserve"/>d entity into <paramref name="archetype"/>,
    /// making it <see cref="IsAlive"/> from this point on. Grows backing array capacity
    /// for <paramref name="entity"/>'s id here (not in <see cref="Reserve"/>), and
    /// assigns its permanent id here too — safe because <see cref="Place"/>, unlike
    /// <see cref="Reserve"/>, only ever runs single-threaded, from
    /// <see cref="CommandBuffer.Apply"/>'s command loop.
    ///
    /// <para>
    /// Bumps <see cref="_nextId"/> immediately, per entity, for a brand-new (never
    /// recycled) id — deliberately not deferred to <see cref="FlushReservations"/>,
    /// which only runs once <see cref="CommandBuffer.Apply"/>'s whole queue has
    /// finished: a queued <c>AddComponent</c> for the same entity <c>CreateEntity</c>
    /// just reserved, in the same batch, checks <see cref="IsAlive"/> before that
    /// batch's <see cref="CommandBuffer.Apply"/> call returns, so it needs this entity
    /// already alive mid-batch, not just by the time the whole batch is done. Not a
    /// plain assignment, since two concurrently-reserved new ids in the same batch can
    /// have their <see cref="Place"/> calls run in either order (a race in which
    /// thread's <see cref="CommandBuffer.Enqueue"/> call wins the queue position first)
    /// — order-independent by construction, so it doesn't matter which lands first.
    /// </para>
    ///
    /// <para>
    /// Known narrow gap, not fixed here: if entity A (id 5) and B (id 6) are both
    /// reserved as brand-new ids in the same batch, and B's <see cref="Place"/> happens
    /// to run before A's (per the ordering note above), <see cref="_nextId"/> jumps to
    /// cover both the moment B is placed — so <see cref="IsAlive"/> would report A
    /// (not yet placed) as alive too, for that brief window, to anything that checks
    /// it from inside an <see cref="IStructuralChangeObserver"/> callback fired
    /// synchronously by B's own placement. No current caller does this (checking a
    /// *different*, not-yet-processed queued entity's liveness from inside a
    /// same-batch structural-change callback); worth fixing properly — a two-phase
    /// placement that defers every new id's <see cref="_nextId"/> visibility until
    /// the whole batch's new ids are all placed — if a future caller ever needs it.
    /// </para>
    /// </summary>
    internal int Place(Entity entity, Archetype archetype)
    {
        EnsureCapacity(entity.Id);
        if (entity.Id >= _nextId) _nextId = entity.Id + 1;
        _permanentIds[entity.Id] = EntityId.NewId();
        _reserved[entity.Id] = false;
        var row = archetype.AddRow(entity);
        this[entity.Id] = (archetype, row);
        return row;
    }

    /// <summary>
    /// Removes entity <paramref name="id"/> from its current archetype, keeping the
    /// location table consistent for whichever entity backfilled its row, and retires
    /// the id for reuse.
    /// </summary>
    internal void Destroy(int id)
    {
        var (archetype, row) = this[id];
        var moved = archetype.RemoveRow(row);
        if (!moved.IsNull)
            this[moved.Id] = (archetype, row);

        Retire(id);
    }

    /// <summary>
    /// Retires <paramref name="id"/> for reuse, bumping its generation so stale handles
    /// report dead. Only ever called single-threaded (from <see cref="Destroy"/>, from
    /// <see cref="CommandBuffer.Apply"/>'s command loop), so it's safe to read
    /// <see cref="_freeCursor"/> directly here (not via <c>Interlocked</c>) even though
    /// <see cref="Reserve"/> also touches it — the two never run concurrently. Writes at
    /// whatever index <see cref="_freeCursor"/> currently names as "first no-longer-needed
    /// slot": if it's still non-negative, that's the first entry <em>past</em> however many
    /// of <see cref="_pending"/>'s existing entries remain genuinely available (everything
    /// at or after that index is stale — already consumed by an earlier <see cref="Reserve"/>
    /// this same stage, safe to overwrite); if it went negative (the whole recycled pool was
    /// consumed, plus some brand-new ids minted beyond it), there's nothing left to preserve
    /// at all, so this writes at index 0. Either way this is also exactly where the next
    /// <see cref="Reserve"/> call should find this id, which is why bumping
    /// <see cref="_freeCursor"/> to one past it here is correct without any separate count
    /// to reconcile.
    /// </summary>
    private void Retire(int id)
    {
        _generations[id]++;
        var firstStaleSlot = _freeCursor < 0 ? 0 : _freeCursor;
        ArrayGrowth.EnsureCapacity(ref _pending, firstStaleSlot + 1);
        _pending[firstStaleSlot] = id;
        _freeCursor = firstStaleSlot + 1;
    }

    /// <summary>
    /// Clamps a negative <see cref="_freeCursor"/> (this batch's recycled pool was fully
    /// consumed, plus some brand-new ids minted beyond it) back to zero, so the next
    /// batch of concurrent <see cref="Reserve"/> calls starts from a clean "nothing
    /// available yet" state instead of continuing to dig a deeper hole from wherever
    /// this batch's cursor excursion left off (which would both skip ids in the
    /// brand-new-id formula and never rediscover entries <see cref="Retire"/> adds
    /// later). A non-negative <see cref="_freeCursor"/> already correctly reflects how
    /// many entries are available — nothing to do in that case. Called once per
    /// <see cref="World.ApplyCommands()"/>, after <see cref="CommandBuffer.Apply"/>'s
    /// whole queue (hence every <see cref="Place"/>/<see cref="Retire"/> call it could
    /// produce) has already run. Doesn't touch <see cref="_nextId"/> — <see cref="Place"/>
    /// already bumps that immediately, per entity, precisely so a same-batch
    /// <c>AddComponent</c> right after <c>CreateEntity</c> sees the entity alive without
    /// waiting for this reconciliation.
    /// </summary>
    internal void FlushReservations()
    {
        if (_freeCursor < 0) _freeCursor = 0;
    }

    private void EnsureCapacity(int id)
    {
        ArrayGrowth.EnsureCapacity(ref _generations, id + 1);
        ArrayGrowth.EnsureCapacity(ref _permanentIds, id + 1);
        ArrayGrowth.EnsureCapacity(ref _locations, id + 1);
        ArrayGrowth.EnsureCapacity(ref _reserved, id + 1);
    }
}
