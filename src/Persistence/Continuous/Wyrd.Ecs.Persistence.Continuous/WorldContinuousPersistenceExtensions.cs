namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// Extension members wiring continuous persistence to a <see cref="WorldBuilder"/>/
/// <see cref="World"/>. A session is tied to the <see cref="World"/> instance it started
/// on and does not outlive it.
/// </summary>
public static class WorldContinuousPersistenceExtensions
{
    private static readonly Wyrd.Ecs.Persistence.Internal.WorldAttachedProperty<ContinuousSession> Sessions = new();

    extension(WorldBuilder builder)
    {
        /// <summary>
        /// Enables continuous persistence: writes an initial bootstrap checkpoint, then
        /// starts the WAL-writer and checkpoint-merge background threads. Requires
        /// <c>World.DefaultCodecRegistry</c> and <c>World.DefaultPersistenceStore</c>
        /// to already be configured earlier in the builder chain. Applied via
        /// <see cref="WorldBuilder.OnBuilt"/> once <see cref="WorldBuilder.Build"/> runs.
        /// Throws if continuous persistence is already enabled for this World.
        /// <paramref name="walStore"/> defaults to a <see cref="FileWalStore"/> colocated
        /// with the default persistence store when that's a <see cref="FileStore"/>;
        /// otherwise supply it explicitly.
        /// <paramref name="registerProcessExitSafetyNet"/> (default true) force-stops and
        /// merges this session if the process exits without <c>StopContinuousPersistence</c>
        /// being called first; it does not help a World abandoned mid-process while the
        /// game keeps running. Pass false to require <c>Stop</c> be called explicitly.
        /// </summary>
        public WorldBuilder EnableContinuousPersistence(
            IWalStore? walStore = null,
            WalOptions? options = null,
            Action<Exception>? onError = null,
            bool registerProcessExitSafetyNet = true)
        {
            builder.OnBuilt += world =>
            {
                if (Sessions.Get(world) is not null)
                    throw new InvalidOperationException(
                        "Continuous persistence is already enabled for this World. " +
                        "Call StopContinuousPersistence before enabling it again.");

                var registry = world.DefaultCodecRegistry
                    ?? throw new InvalidOperationException(
                        "No CodecRegistry was provided and none is configured via " +
                        "World.DefaultCodecRegistry (set directly, or via " +
                        "WorldBuilder.SetDefaultCodecRegistry, before " +
                        "EnableContinuousPersistence in the builder chain).");

                var checkpointStore = world.DefaultPersistenceStore
                    ?? throw new InvalidOperationException(
                        "No IPersistenceStore was provided and none is configured via " +
                        "World.DefaultPersistenceStore (set directly, or via " +
                        "WorldBuilder.SetDefaultPersistenceStore/AddBinaryPersistence, " +
                        "before EnableContinuousPersistence in the builder chain).");

                var resolvedWalStore = walStore ?? (checkpointStore is FileStore fileStore
                    ? new FileWalStore(fileStore.Path)
                    : throw new InvalidOperationException(
                        "No IWalStore was provided and none could be inferred: " +
                        "World.DefaultPersistenceStore isn't a FileStore. Pass walStore " +
                        "explicitly to EnableContinuousPersistence."));

                world.Save();
                // Advances past the bootstrap checkpoint's tick so later WAL merges
                // (filtering on tick > priorTick) don't exclude entities created
                // immediately after enabling persistence, before the caller's first
                // AdvanceTick.
                world.AdvanceTick();

                var capture = new ChangeCapture(world, registry);
                var walWorker = new Internal.ContinuousWalWorker(world, capture, checkpointStore, resolvedWalStore, options ?? WalOptions.Default, onError);
                walWorker.Start();

                Sessions.Set(world, new ContinuousSession(capture, walWorker));
                if (registerProcessExitSafetyNet)
                    Internal.ProcessExitSafetyNet.Register(world, world.StopContinuousPersistence);
            };
            return builder;
        }
    }

    extension(World world)
    {
        /// <summary>
        /// Stops continuous persistence: disposes the WAL-writer and checkpoint-merge
        /// threads, then stops change tracking. When <paramref name="mergeFinalCheckpoint"/>
        /// is true (the default), also folds everything left in the WAL into the
        /// checkpoint before returning, so <c>World.Load</c> alone reflects everything
        /// written before this call. Pass false for the fastest shutdown, leaving the WAL
        /// to be merged later. Throws if <c>EnableContinuousPersistence</c> was never
        /// called for this World.
        /// </summary>
        public void StopContinuousPersistence(bool mergeFinalCheckpoint = true)
        {
            if (Sessions.Get(world) is not { } session)
                throw new InvalidOperationException(
                    "Continuous persistence was never enabled for this World " +
                    "(WorldBuilder.EnableContinuousPersistence was never called).");

            Internal.ProcessExitSafetyNet.Unregister(world);
            session.WalWorker.Dispose();
            if (mergeFinalCheckpoint) session.WalWorker.MergeFinalCheckpoint();
            session.Capture.Dispose();
            Sessions.Set(world, null);
        }
    }

    private sealed record ContinuousSession(ChangeCapture Capture, Internal.ContinuousWalWorker WalWorker);
}
