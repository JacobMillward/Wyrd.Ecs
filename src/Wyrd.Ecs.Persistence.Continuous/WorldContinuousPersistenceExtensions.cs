using System.Runtime.CompilerServices;

namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// Extension members wiring continuous persistence to a <see cref="WorldBuilder"/>/
/// <see cref="World"/>, neither of which can gain new fields from another assembly.
/// Backed by a <see cref="ConditionalWeakTable{TKey,TValue}"/> keyed on the
/// <see cref="World"/> instance, matching <c>WorldPersistenceExtensions</c>'s own
/// pattern, so a running session doesn't outlive the World that started it.
/// </summary>
public static class WorldContinuousPersistenceExtensions
{
    private static readonly ConditionalWeakTable<World, ContinuousSession> Sessions = new();

    extension(WorldBuilder builder)
    {
        /// <summary>
        /// Enables continuous persistence: pulls <c>World.DefaultComponentCodecRegistry</c>
        /// (must already be configured — set directly, or via
        /// <c>WorldBuilder.SetDefaultComponentCodecRegistry</c>, before this call in the
        /// builder chain), writes an initial <see cref="WorldSnapshot.Save"/>
        /// bootstrap checkpoint so a valid baseline always exists (harmless and cheap for
        /// a freshly-built World; correctly captures whatever already exists for a
        /// mid-session enable), then starts the WAL-writer and checkpoint-merge
        /// background threads. Applied via <see cref="WorldBuilder.OnBuilt"/> once
        /// <see cref="WorldBuilder.Build"/> runs.
        /// </summary>
        public WorldBuilder EnableContinuousPersistence(ContinuousOptions options)
        {
            builder.OnBuilt += world =>
            {
                var registry = world.DefaultComponentCodecRegistry
                    ?? throw new InvalidOperationException(
                        "No ComponentCodecRegistry was provided and none is configured via " +
                        "World.DefaultComponentCodecRegistry (set directly, or via " +
                        "WorldBuilder.SetDefaultComponentCodecRegistry, before " +
                        "EnableContinuousPersistence in the builder chain).");

                WorldSnapshot.Save(world, registry, options.CheckpointStore);
                // Seals the bootstrap checkpoint's tick boundary. WorldSnapshot.Save
                // stamps the checkpoint with world.CurrentTick as it is at this exact
                // instant — but if the consumer's very next action is creating initial
                // entities (the ordinary "populate the world, then start ticking"
                // pattern, before ever calling AdvanceTick themselves), those creations
                // would be stamped with that same tick number. A later merge's
                // tick > priorTick filter would then exclude them forever, since they'd
                // share a tick with a checkpoint that (by construction) already claims
                // to cover it. Advancing once here, before ChangeCapture or the WAL
                // worker exist, guarantees nothing captured afterward can ever collide
                // with the bootstrap snapshot's own tick.
                world.AdvanceTick();

                var capture = new ChangeCapture(world, registry);
                var walWorker = new Internal.ContinuousWalWorker(world, capture, options.CheckpointStore, options.WalStore, options.Options, options.OnError);
                walWorker.Start();

                Sessions.AddOrUpdate(world, new ContinuousSession(capture, walWorker));
            };
            return builder;
        }
    }

    extension(World world)
    {
        /// <summary>
        /// Stops continuous persistence: disposes the WAL-writer/checkpoint-merge
        /// threads (the WAL-writer finishes draining and fsyncs one last time; the
        /// checkpoint-merge thread finishes any in-flight merge rather than aborting
        /// it, but performs no forced final merge), then stops change tracking. Throws
        /// if <c>EnableContinuousPersistence</c> was never called for this World.
        /// </summary>
        public void StopContinuousPersistence()
        {
            if (!Sessions.TryGetValue(world, out var session))
                throw new InvalidOperationException(
                    "Continuous persistence was never enabled for this World " +
                    "(WorldBuilder.EnableContinuousPersistence was never called).");

            session.WalWorker.Dispose();
            session.Capture.Dispose();
            Sessions.Remove(world);
        }
    }

    private sealed record ContinuousSession(ChangeCapture Capture, Internal.ContinuousWalWorker WalWorker);
}
