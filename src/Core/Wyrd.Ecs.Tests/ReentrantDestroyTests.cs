namespace Wyrd.Ecs.Tests;

struct RdtPosition : IComponent { public float X; }

/// <summary>
/// Guards against reentrant destruction: <see cref="World.DestroyEntity"/> notifies
/// observers and cascades dependent-relation subtrees BEFORE removing the entity from
/// the entity table, so anything destroying that same mid-flight entity again (an
/// observer, or a relation cycle reaching it through
/// <c>RelationBacklinks&lt;T&gt;.CascadeRemove</c>) re-enters while every liveness check
/// still passes. Without a guard that double-runs the cascade and double-retires the id:
/// the second table destroy removes whichever entity backfilled the row, and the id can
/// be handed out twice with the same generation.
/// </summary>
public class ReentrantDestroyTests
{
    private sealed class ReentrantDestroyer : IStructuralChangeObserver
    {
        private readonly World _world;
        private readonly Entity _victim;
        public int Notifications;

        internal ReentrantDestroyer(World world, Entity victim)
        {
            _world = world;
            _victim = victim;
        }

        public void OnEntityCreated(Entity entity) { }
        public void OnComponentAdded(Entity entity, int typeIndex) { }
        public void OnComponentRemoved(Entity entity, int typeIndex) { }
        public void OnTagAdded(Entity entity, int typeIndex) { }
        public void OnTagRemoved(Entity entity, int typeIndex) { }

        public void OnEntityDestroyed(Entity entity)
        {
            if (!entity.Equals(_victim)) return;
            Notifications++;
            // Synchronous reentry onto an entity still mid-destroy.
            _world.DestroyEntity(_victim);
        }
    }

    [Fact]
    public void ObserverReenteringDestroyMidFlight_NotifiesOnceAndLeavesNeighborsIntact()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new RdtPosition { X = 1f });
        Entity b = world.Commands.CreateEntity(new RdtPosition { X = 2f });
        Entity c = world.Commands.CreateEntity(new RdtPosition { X = 3f });
        world.ApplyCommands();

        var observer = new ReentrantDestroyer(world, a);
        using (world.ObserveStructuralChanges(observer))
        {
            world.Commands.DestroyEntity(a);
            world.ApplyCommands();
        }

        // Exactly one destroyed notification even though the observer reentered.
        observer.Notifications.Should().Be(1);

        // The double table-destroy must not have eaten whichever entity backfilled a's row.
        world.IsAlive(b).Should().BeTrue();
        world.GetComponent<RdtPosition>(b).X.Should().Be(2f);
        world.IsAlive(c).Should().BeTrue();
        world.GetComponent<RdtPosition>(c).X.Should().Be(3f);

        // a stays dead, exactly once-retired: no double free-list entry, so the next two
        // spawns recycle distinct ids rather than aliasing a's slot to two entities.
        world.IsAlive(a).Should().BeFalse();
        Entity recycledOne = world.Commands.CreateEntity(new RdtPosition { X = 10f });
        Entity recycledTwo = world.Commands.CreateEntity(new RdtPosition { X = 11f });
        world.ApplyCommands();
        recycledOne.Id.Should().NotBe(recycledTwo.Id);
    }

    [Fact]
    public void DeepDependentSubtree_StillDestroysEveryDescendantExactlyOnce()
    {
        var world = new World();
        Entity root = world.Commands.CreateEntity(new RdtPosition());
        Entity arm = world.Commands.CreateEntity(new RdtPosition());
        Entity hand = world.Commands.CreateEntity(new RdtPosition());
        world.ApplyCommands();

        world.Commands.AddRelation<Parent>(arm, root);
        world.Commands.AddRelation<Parent>(hand, arm);
        world.ApplyCommands();

        var notifiedRoot = 0;
        var notifiedArm = 0;
        var notifiedHand = 0;
        using (world.ObserveStructuralChanges(new CounterObserver(e =>
               {
                   if (e.Equals(root)) notifiedRoot++;
                   else if (e.Equals(arm)) notifiedArm++;
                   else if (e.Equals(hand)) notifiedHand++;
               })))
        {
            world.Commands.DestroyEntity(root);
            world.ApplyCommands();
        }

        notifiedRoot.Should().Be(1);
        notifiedArm.Should().Be(1);
        notifiedHand.Should().Be(1);
        world.IsAlive(root).Should().BeFalse();
        world.IsAlive(arm).Should().BeFalse();
        world.IsAlive(hand).Should().BeFalse();
    }

    [Fact]
    public void DependentCycle_EachEntityDestroyedExactlyOnce()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new RdtPosition());
        Entity b = world.Commands.CreateEntity(new RdtPosition());
        world.ApplyCommands();

        // A hierarchy cycle: a is a child of b AND b is a child of a. Destroying either
        // side cascades into the other while it is still mid-destroy.
        world.Commands.AddRelation<Parent>(a, b);
        world.Commands.AddRelation<Parent>(b, a);
        world.ApplyCommands();

        var notificationsA = 0;
        var notificationsB = 0;
        using (world.ObserveStructuralChanges(new CounterObserver(e =>
               {
                   if (e.Equals(a)) notificationsA++;
                   else if (e.Equals(b)) notificationsB++;
               })))
        {
            world.Commands.DestroyEntity(a);
            world.ApplyCommands();
        }

        notificationsA.Should().Be(1);
        notificationsB.Should().Be(1);
        world.IsAlive(a).Should().BeFalse();
        world.IsAlive(b).Should().BeFalse();
    }

    [Fact]
    public void ObserverThrowingMidDestroy_LeavesEntityAliveAndGuardStackClean()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new RdtPosition { X = 1f });
        var b = world.Commands.CreateEntity(new RdtPosition { X = 2f });
        world.ApplyCommands();

        using (world.ObserveStructuralChanges(new ThrowingObserver(when: e => e.Equals(a))))
        {
            var apply = () => { world.Commands.DestroyEntity(a); world.ApplyCommands(); };
            apply.Should().Throw<InvalidOperationException>("the observer's exception propagates to the Apply caller");
        }

        // The destroy never landed: a is still alive and queryable, and the guard stack
        // unwound, so a subsequent destroy of the same entity works normally.
        world.IsAlive(a).Should().BeTrue();
        world.GetComponent<RdtPosition>(a).X.Should().Be(1f);
        world.IsAlive(b).Should().BeTrue();

        world.Commands.DestroyEntity(a);
        world.ApplyCommands();
        world.IsAlive(a).Should().BeFalse();
        world.IsAlive(b).Should().BeTrue();
    }

    [Fact]
    public void SelfParentEdge_DestroyCompletesExactlyOnce()
    {
        var world = new World();
        Entity x = world.Commands.CreateEntity(new RdtPosition());
        world.ApplyCommands();
        world.Commands.AddRelation<Parent>(x, x);
        world.ApplyCommands();

        var notifications = 0;
        using (world.ObserveStructuralChanges(new CounterObserver(_ => notifications++)))
        {
            world.Commands.DestroyEntity(x);
            world.ApplyCommands();
        }

        // x's own backlink set contains x: destroying it reenters mid-destroy through the
        // dependent cascade with no second entity involved.
        notifications.Should().Be(1);
        world.IsAlive(x).Should().BeFalse();
    }

    private sealed class ThrowingObserver : IStructuralChangeObserver
    {
        private readonly Func<Entity, bool> _when;
        internal ThrowingObserver(Func<Entity, bool> when) => _when = when;

        public void OnEntityCreated(Entity entity) { }
        public void OnComponentAdded(Entity entity, int typeIndex) { }
        public void OnComponentRemoved(Entity entity, int typeIndex) { }
        public void OnTagAdded(Entity entity, int typeIndex) { }
        public void OnTagRemoved(Entity entity, int typeIndex) { }

        public void OnEntityDestroyed(Entity entity)
        {
            if (_when(entity)) throw new InvalidOperationException("observer boom");
        }
    }

    private sealed class CounterObserver : IStructuralChangeObserver
    {
        private readonly Action<Entity> _onDestroyed;
        internal CounterObserver(Action<Entity> onDestroyed) => _onDestroyed = onDestroyed;

        public void OnEntityCreated(Entity entity) { }
        public void OnComponentAdded(Entity entity, int typeIndex) { }
        public void OnComponentRemoved(Entity entity, int typeIndex) { }
        public void OnTagAdded(Entity entity, int typeIndex) { }
        public void OnTagRemoved(Entity entity, int typeIndex) { }
        public void OnEntityDestroyed(Entity entity) => _onDestroyed(entity);
    }
}
