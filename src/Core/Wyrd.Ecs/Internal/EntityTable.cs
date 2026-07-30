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
/// threads at once (via <see cref="CommandBuffer.CreateEntity()"/>, from several systems
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
    private EntityLocation[] _locations = new EntityLocation[4];
    private int[] _pending = Array.Empty<int>();
    private int _freeCursor;
    private bool[] _reserved = new bool[4];
    private int _nextId = 1; // Id 0 is reserved for Entity.Null.

    public EntityTable() { Array.Fill(_reserved, true); }

    /// <summary>The archetype+row currently backing entity id <paramref name="id"/>.</summary>
    internal ref EntityLocation this[int id] => ref _locations[id];

    internal EntityId PermanentId(int id) => _permanentIds[id];

    /// <summary>
    /// A direct array index, not a <c>HashSet&lt;int&gt;</c> — this is called on every
    /// single structural mutation (<see cref="CommandBuffer"/> checks it before almost every
    /// queued operation) plus every direct component read/write, so it's the hottest
    /// correctness check in the engine. A hash lookup here was real, avoidable overhead
    /// on that path; a bounds-checked array read costs a fraction of it.
    ///
    /// <para>
    /// <c>id &lt; _reserved.Length</c>, not <c>id &lt; _nextId</c> — a purely capacity
    /// bounds check, doubling as the guard against reading an index nothing has ever
    /// touched. Liveness itself comes entirely from <see cref="_reserved"/>: every slot
    /// defaults to <c>true</c> (not alive) the moment its capacity exists — either from
    /// <see cref="EntityTable"/>'s own construction or from <see cref="EnsureCapacity"/>
    /// growing into it — and <see cref="Place"/> is the only thing that ever clears one
    /// to <c>false</c>. That makes each id's liveness independent of every other id's,
    /// including a same-batch sibling that happens to share newly-grown capacity by
    /// coincidence (<see cref="Place"/>'s own doc explains why that matters).
    /// </para>
    /// </summary>
    internal bool IsAlive(int id, int generation) =>
        id > 0 && id < _reserved.Length && _generations[id] == generation && !_reserved[id];

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
    /// Bulk counterpart to <see cref="Reserve"/>: claims a contiguous range of
    /// <see cref="_freeCursor"/> cursor slots in one <see cref="Interlocked.Add(ref int, int)"/>
    /// instead of <c>destination.Length</c> separate <see cref="Interlocked.Decrement(ref int)"/> calls —
    /// the same lock-free, no-CAS-retry, order-independent scheme <see cref="Reserve"/>
    /// already documents for a single id, just claiming many slots at once. Produces
    /// exactly the id sequence <c>destination.Length</c> sequential <see cref="Reserve"/>
    /// calls would (verified directly against that arithmetic in
    /// <c>EntityTableTests.ReserveRange_ProducesTheSameIdsAsSequentialReserveCalls</c>).
    /// Used by batch entity creation so reserving a batch of N ids costs one atomic op,
    /// not N of them, keeping the whole batch-creation path O(1) per call regardless of N.
    /// </summary>
    internal void ReserveRange(Span<Entity> destination)
    {
        var count = destination.Length;
        var startCursor = Interlocked.Add(ref _freeCursor, -count); // == the cursor value after `count` sequential Decrements
        for (var i = 0; i < count; i++)
        {
            var cursor = startCursor + (count - 1 - i);
            if (cursor >= 0)
            {
                var id = _pending[cursor];
                _reserved[id] = true;
                destination[i] = new Entity(id, _generations[id]);
            }
            else
            {
                var id = _nextId - cursor - 1;
                destination[i] = new Entity(id, 0);
            }
        }
    }

    /// <summary>
    /// Places a previously-<see cref="Reserve"/>d entity into <paramref name="archetype"/>,
    /// making it <see cref="IsAlive"/> from this point on. Implemented as
    /// <see cref="Archetype.AddRow"/> (reserves this entity's row) followed by
    /// <see cref="PlaceAt"/> (the id-table bookkeeping) — split out so batch placement
    /// (<see cref="PlaceBatch"/>) can reuse the bookkeeping half against rows already
    /// bulk-reserved via <see cref="Archetype.AddRows"/>, without a second per-entity
    /// <c>AddRow</c> call. See <see cref="PlaceAt"/>'s own doc for the concurrency and
    /// liveness invariants this relies on.
    /// </summary>
    internal int Place(Entity entity, Archetype archetype)
    {
        var row = archetype.AddRow(entity);
        PlaceAt(entity, archetype, row);
        return row;
    }

    /// <summary>
    /// The id-table bookkeeping half of <see cref="Place"/>: grows backing array capacity
    /// for <paramref name="entity"/>'s id, assigns its permanent id, and clears
    /// <see cref="_reserved"/>'s bit for it — everything <see cref="Place"/> used to do
    /// except reserving the row itself, which the caller already did (via
    /// <see cref="Archetype.AddRow"/> for a single entity, or <see cref="Archetype.AddRows"/>
    /// for a batch). Only ever runs single-threaded, from <see cref="CommandBuffer.Apply"/>'s
    /// command loop — same as <see cref="Place"/> always has.
    ///
    /// <para>
    /// Clearing <see cref="_reserved"/>'s bit for this id is what actually makes it
    /// <see cref="IsAlive"/> — immediately, regardless of placement order — since a
    /// queued <c>AddComponent</c> for the same entity <c>CreateEntity</c> just reserved,
    /// in the same batch, checks <see cref="IsAlive"/> before that batch's
    /// <see cref="CommandBuffer.Apply"/> call returns, so it needs this entity already
    /// alive mid-batch, not just by the time the whole batch is done. <see cref="_nextId"/>
    /// still advances here too, for <see cref="Reserve"/>'s own bookkeeping (so the next
    /// batch never mints an id this one already used) — but that bump plays no part in
    /// this id's own liveness, or any other id's, unlike the scheme this replaced. Two
    /// concurrently-reserved new ids in the same batch can have their <see cref="Place"/>/
    /// <see cref="PlaceAt"/> calls run in either order (a race in which thread's
    /// <see cref="CommandBuffer.Enqueue"/> call wins the queue position first) —
    /// order-independent by construction: whichever runs first only ever clears its own
    /// id's bit, never a sibling's, so a lower, not-yet-placed sibling that happens to
    /// share newly-grown capacity (because <see cref="EnsureCapacity"/> just grew arrays
    /// to cover this higher id) still reads <c>true</c> — reserved, not alive — until its
    /// own <see cref="PlaceAt"/> call runs.
    /// </para>
    /// </summary>
    internal void PlaceAt(Entity entity, Archetype archetype, int row)
    {
        EnsureCapacity(entity.Id);
        if (entity.Id >= _nextId) _nextId = entity.Id + 1;
        _permanentIds[entity.Id] = EntityId.NewId();
        _reserved[entity.Id] = false;
        this[entity.Id] = new EntityLocation(archetype, row);
    }

    /// <summary>
    /// Bulk counterpart to <see cref="Place"/>: runs <see cref="PlaceAt"/>'s bookkeeping
    /// for every entity in <paramref name="entities"/> against the rows
    /// <see cref="Archetype.AddRows"/> already bulk-reserved for them, starting at
    /// <paramref name="startRow"/>. This loop is unavoidable — each id has its own
    /// generation/permanent-id/reserved-bit slot to update — but it's int/struct
    /// bookkeeping, not field-by-field component copying, so it doesn't undercut the
    /// point of batching.
    /// </summary>
    internal void PlaceBatch(ReadOnlySpan<Entity> entities, Archetype archetype, int startRow)
    {
        for (var i = 0; i < entities.Length; i++)
            PlaceAt(entities[i], archetype, startRow + i);
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
            this[moved.Id] = new EntityLocation(archetype, row);

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
    /// produce) has already run. Doesn't touch <see cref="_nextId"/> or
    /// <see cref="_reserved"/> — <see cref="Place"/> already updates both immediately,
    /// per entity, precisely so a same-batch <c>AddComponent</c> right after
    /// <c>CreateEntity</c> sees the entity alive without waiting for this reconciliation.
    /// </summary>
    internal void FlushReservations()
    {
        if (_freeCursor < 0) _freeCursor = 0;
    }

    /// <summary>
    /// Grows every parallel array to cover <paramref name="id"/>. <see cref="_reserved"/>
    /// needs one thing the others don't: <c>Array.Resize</c> zero-fills new slots to
    /// <c>false</c>, which for every other array is exactly "not yet meaningful, fine" —
    /// but for <see cref="_reserved"/>, <c>false</c> means "alive." Growing past some
    /// higher id (<paramref name="id"/> itself, mid-<see cref="Place"/>) would otherwise
    /// silently make a lower, not-yet-placed sibling's still-untouched slot read as alive
    /// the instant its capacity happens to exist — precisely the gap <see cref="IsAlive"/>'s
    /// own doc describes. Explicitly filling the newly-added region with <c>true</c>
    /// keeps every id "reserved" (not alive) by default until its own <see cref="Place"/>
    /// call says otherwise.
    /// </summary>
    private void EnsureCapacity(int id)
    {
        ArrayGrowth.EnsureCapacity(ref _generations, id + 1);
        ArrayGrowth.EnsureCapacity(ref _permanentIds, id + 1);
        ArrayGrowth.EnsureCapacity(ref _locations, id + 1);

        var previousLength = _reserved.Length;
        ArrayGrowth.EnsureCapacity(ref _reserved, id + 1);
        if (_reserved.Length > previousLength)
            Array.Fill(_reserved, true, previousLength, _reserved.Length - previousLength);
    }
}
