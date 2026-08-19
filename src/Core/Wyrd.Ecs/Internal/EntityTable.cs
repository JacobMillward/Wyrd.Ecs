using System.Threading;

namespace Wyrd.Ecs.Internal;

/// <summary>
/// The world's entity identity table: generation-checked ids for liveness, permanent
/// opaque ids (see <see cref="EntityId"/>), and each live entity's current archetype and
/// row. Owns id allocation and recycling. A mutable struct, embedded directly in
/// <see cref="World"/> rather than a class, so hot-path location lookups avoid an extra
/// heap indirection.
///
/// <para>
/// <see cref="Reserve"/> is the only method safe to call concurrently from several threads
/// at once. It mirrors Bevy's <c>Entities::reserve_entity</c>: an atomic cursor
/// (<see cref="_freeCursor"/>) into the recycled-id list (<see cref="_pending"/>), falling
/// back to minting a new id once the cursor goes negative, lock-free, with no two callers
/// ever handed the same id. Every other method assumes single-threaded access, at the join
/// point after a stage's systems have all returned.
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
    /// A direct array index, not a <c>HashSet&lt;int&gt;</c>, since this runs on every
    /// structural mutation and every direct component read/write, the hottest correctness
    /// check in the engine.
    ///
    /// <para>
    /// Liveness comes entirely from <see cref="_reserved"/>: every slot defaults to
    /// <c>true</c> (not alive) the moment its capacity exists, and <see cref="Place"/> is
    /// the only thing that ever clears one to <c>false</c>. That keeps each id's liveness
    /// independent of a same-batch sibling that happens to share newly-grown capacity.
    /// </para>
    /// </summary>
    internal bool IsAlive(int id, int generation) =>
        id > 0 && id < _reserved.Length && _generations[id] == generation && !_reserved[id];

    /// <summary>
    /// Reserves a fresh entity id without placing it into any archetype: not
    /// <see cref="IsAlive"/> until <see cref="Place"/> runs. Lets a caller hand back a
    /// usable <see cref="Entity"/> immediately for chaining further deferred commands,
    /// with actual archetype placement happening later, at apply time. Safe to call
    /// concurrently from several threads; see the class doc for the scheme.
    /// </summary>
    internal Entity Reserve()
    {
        var cursor = Interlocked.Decrement(ref _freeCursor);
        if (cursor >= 0)
        {
            // Reuse a recycled id. _pending/_generations are read-only from here: both
            // are only ever written back on the single-threaded side of the join point
            // (Retire/FlushReservations), never concurrently with a Reserve call.
            var id = _pending[cursor];
            _reserved[id] = true;
            return new Entity(id, _generations[id]);
        }
        else
        {
            // No recycled id available: mint a new id. _nextId only ever changes
            // single-threaded (in Place), so every concurrent caller here reads the
            // same stable value and lands on a distinct, increasing id with no CAS
            // retry needed. Generation is always 0 for a never-before-used id; capacity
            // is grown later, in Place.
            var id = _nextId - cursor - 1;
            return new Entity(id, 0);
        }
    }

    /// <summary>
    /// Bulk counterpart to <see cref="Reserve"/>: claims a contiguous range of
    /// <see cref="_freeCursor"/> cursor slots in one <see cref="Interlocked.Add(ref int, int)"/>
    /// instead of <c>destination.Length</c> separate decrements, using the same lock-free
    /// scheme. Produces exactly the id sequence that many sequential <see cref="Reserve"/>
    /// calls would, at O(1) per call regardless of count.
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
    /// <see cref="Archetype.AddRow"/> followed by <see cref="PlaceAt"/>'s id-table
    /// bookkeeping, split out so <see cref="PlaceBatch"/> can reuse the bookkeeping half
    /// against rows already bulk-reserved via <see cref="Archetype.AddRows"/>.
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
    /// <see cref="_reserved"/>'s bit for it. The row itself is reserved separately, by the
    /// caller. Only ever runs single-threaded, from <see cref="CommandBuffer.Apply"/>'s
    /// command loop.
    ///
    /// <para>
    /// Clearing <see cref="_reserved"/>'s bit is what makes this id <see cref="IsAlive"/>,
    /// immediately, which a same-batch queued <c>AddComponent</c> right after
    /// <c>CreateEntity</c> relies on. <see cref="_nextId"/> also advances here so a later
    /// batch never mints an id this one already used. Two ids placed in either order within
    /// the same batch stay independent: each call only ever clears its own id's bit, so a
    /// not-yet-placed sibling that shares newly-grown capacity still reads reserved until
    /// its own <see cref="PlaceAt"/> call runs.
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
    /// for every entity in <paramref name="entities"/> against rows already bulk-reserved
    /// via <see cref="Archetype.AddRows"/>, starting at <paramref name="startRow"/>. The
    /// per-entity loop is unavoidable but is only int/struct bookkeeping, not component
    /// copying.
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
    /// report dead. Only ever called single-threaded (from <see cref="Destroy"/>), so
    /// it's safe to read <see cref="_freeCursor"/> directly here, without
    /// <c>Interlocked</c>, even though <see cref="Reserve"/> also touches it. Writes
    /// <paramref name="id"/> at whatever index <see cref="_freeCursor"/> names as the
    /// first no-longer-needed slot (clamped to 0 if negative), then bumps
    /// <see cref="_freeCursor"/> to one past it, exactly where the next
    /// <see cref="Reserve"/> call should find this id.
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
    /// Clamps a negative <see cref="_freeCursor"/> back to zero after a batch fully
    /// consumed the recycled pool and minted new ids beyond it, so the next batch of
    /// concurrent <see cref="Reserve"/> calls starts clean instead of digging a deeper
    /// hole (which would skip ids and never rediscover entries <see cref="Retire"/> adds
    /// later). A non-negative cursor already reflects available entries correctly, so
    /// there's nothing to do in that case. Called once per <see cref="World.ApplyCommands()"/>,
    /// after the whole command queue has run. Doesn't touch <see cref="_nextId"/> or
    /// <see cref="_reserved"/>: <see cref="Place"/> already updates both immediately per
    /// entity.
    /// </summary>
    internal void FlushReservations()
    {
        if (_freeCursor < 0) _freeCursor = 0;
    }

    /// <summary>
    /// Grows every parallel array to cover <paramref name="id"/>. <see cref="_reserved"/>
    /// needs special handling: <c>Array.Resize</c> zero-fills new slots to <c>false</c>,
    /// which means "alive" for this array. Left alone, growing past a higher id mid-
    /// <see cref="Place"/> would make a lower, not-yet-placed sibling's untouched slot
    /// read as alive the instant its capacity exists. Explicitly filling the newly-added
    /// region with <c>true</c> keeps every id reserved (not alive) by default until its
    /// own <see cref="Place"/> call says otherwise.
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
