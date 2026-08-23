using BenchmarkDotNet.Attributes;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

public struct CbxPosition : IComponent { public float X, Y; }
public struct CbxVelocity : IComponent { public float X, Y; }
public struct CbxHealth : IComponent { public float Amount; }
public struct CbxMana : IComponent { public float Amount; }
public struct CbxFrozen : ITag { }
public struct CbxBurning : ITag { }
public struct CbxOwned : IRelation { }

// One-shot component types used only during setup, mirroring the many component types a
// long-lived world accumulates over its lifetime while any single frame's command batch
// touches just a few. Each leaves behind a per-type payload buffer for the buffer's lifetime.
public struct CbxWarm01 : IComponent { public float Value; }
public struct CbxWarm02 : IComponent { public float Value; }
public struct CbxWarm03 : IComponent { public float Value; }
public struct CbxWarm04 : IComponent { public float Value; }
public struct CbxWarm05 : IComponent { public float Value; }
public struct CbxWarm06 : IComponent { public float Value; }
public struct CbxWarm07 : IComponent { public float Value; }
public struct CbxWarm08 : IComponent { public float Value; }
public struct CbxWarm09 : IComponent { public float Value; }
public struct CbxWarm10 : IComponent { public float Value; }
public struct CbxWarm11 : IComponent { public float Value; }
public struct CbxWarm12 : IComponent { public float Value; }
public struct CbxWarm13 : IComponent { public float Value; }
public struct CbxWarm14 : IComponent { public float Value; }
public struct CbxWarm15 : IComponent { public float Value; }
public struct CbxWarm16 : IComponent { public float Value; }
public struct CbxWarm17 : IComponent { public float Value; }
public struct CbxWarm18 : IComponent { public float Value; }
public struct CbxWarm19 : IComponent { public float Value; }
public struct CbxWarm20 : IComponent { public float Value; }
public struct CbxWarm21 : IComponent { public float Value; }
public struct CbxWarm22 : IComponent { public float Value; }
public struct CbxWarm23 : IComponent { public float Value; }
public struct CbxWarm24 : IComponent { public float Value; }
public struct CbxWarm25 : IComponent { public float Value; }
public struct CbxWarm26 : IComponent { public float Value; }
public struct CbxWarm27 : IComponent { public float Value; }
public struct CbxWarm28 : IComponent { public float Value; }
public struct CbxWarm29 : IComponent { public float Value; }
public struct CbxWarm30 : IComponent { public float Value; }
public struct CbxWarm31 : IComponent { public float Value; }
public struct CbxWarm32 : IComponent { public float Value; }
public struct CbxWarm33 : IComponent { public float Value; }
public struct CbxWarm34 : IComponent { public float Value; }
public struct CbxWarm35 : IComponent { public float Value; }
public struct CbxWarm36 : IComponent { public float Value; }
public struct CbxWarm37 : IComponent { public float Value; }
public struct CbxWarm38 : IComponent { public float Value; }
public struct CbxWarm39 : IComponent { public float Value; }
public struct CbxWarm40 : IComponent { public float Value; }
public struct CbxWarm41 : IComponent { public float Value; }
public struct CbxWarm42 : IComponent { public float Value; }
public struct CbxWarm43 : IComponent { public float Value; }
public struct CbxWarm44 : IComponent { public float Value; }
public struct CbxWarm45 : IComponent { public float Value; }
public struct CbxWarm46 : IComponent { public float Value; }
public struct CbxWarm47 : IComponent { public float Value; }
public struct CbxWarm48 : IComponent { public float Value; }

/// <summary>
/// Measures a full enqueue-then-apply round trip for a realistic mixed command batch
/// (creates, destroys, component adds/removes, tag flips) against a pooled entity set,
/// across batch sizes. GlobalSetup creates per-type payload buffers for roughly fifty
/// component/relation types that the measured batches never touch - CbxMana, CbxOwned,
/// CbxBurning, the shared remove-relation target buffer, and forty-eight one-shot
/// component types - mirroring a long-lived world whose lifetime type set far exceeds any
/// single batch's, so cleanup work per apply is visible separately from replay work.
/// StructuralAndTagsOnly queues no payload-carrying commands at all, isolating that
/// cleanup cost. Population is held stable by pairing every create with a destroy. The
/// sink defeats dead-code elimination.
/// </summary>
[MemoryDiagnoser]
public class CommandBatchBenchmarks
{
    private const int PoolSize = 256;
    private const int PoolMask = PoolSize - 1;

    private World _world = null!;
    private Entity[] _pool = null!;
    private int _sink;

    [Params(10, 100, 10_000)]
    public int BatchSize;

    [GlobalSetup]
    public void Setup()
    {
        _world = new World();
        _pool = new Entity[PoolSize];

        var commands = _world.Commands;
        for (var i = 0; i < PoolSize; i++)
            _pool[i] = commands.CreateEntity(new CbxPosition { X = i, Y = i }, new CbxVelocity { X = i });
        _world.ApplyCommands();

        // Historical types: used once here, never in the measured batches, so their
        // per-type buffers persist for the buffer's lifetime like a real world's.
        for (var i = 0; i < PoolSize; i += 16)
        {
            commands.AddComponent<CbxMana>(_pool[i], new CbxMana { Amount = i });
            commands.AddTag<CbxBurning>(_pool[i]);
            if (i > 0)
                commands.AddRelation<CbxOwned>(_pool[i], _pool[i - 1]);
        }

        // One remove-relation so the shared RelationTargetBuffer is created too; the edge
        // added above is removed, leaving world state as it started.
        commands.RemoveRelation<CbxOwned>(_pool[16], _pool[15]);

        commands.AddComponent<CbxWarm01>(_pool[1], default);
        commands.AddComponent<CbxWarm02>(_pool[2], default);
        commands.AddComponent<CbxWarm03>(_pool[3], default);
        commands.AddComponent<CbxWarm04>(_pool[4], default);
        commands.AddComponent<CbxWarm05>(_pool[5], default);
        commands.AddComponent<CbxWarm06>(_pool[6], default);
        commands.AddComponent<CbxWarm07>(_pool[7], default);
        commands.AddComponent<CbxWarm08>(_pool[8], default);
        commands.AddComponent<CbxWarm09>(_pool[9], default);
        commands.AddComponent<CbxWarm10>(_pool[10], default);
        commands.AddComponent<CbxWarm11>(_pool[11], default);
        commands.AddComponent<CbxWarm12>(_pool[12], default);
        commands.AddComponent<CbxWarm13>(_pool[13], default);
        commands.AddComponent<CbxWarm14>(_pool[14], default);
        commands.AddComponent<CbxWarm15>(_pool[15], default);
        commands.AddComponent<CbxWarm16>(_pool[17], default);
        commands.AddComponent<CbxWarm18>(_pool[18], default);
        commands.AddComponent<CbxWarm19>(_pool[19], default);
        commands.AddComponent<CbxWarm20>(_pool[20], default);
        commands.AddComponent<CbxWarm21>(_pool[21], default);
        commands.AddComponent<CbxWarm22>(_pool[22], default);
        commands.AddComponent<CbxWarm23>(_pool[23], default);
        commands.AddComponent<CbxWarm24>(_pool[24], default);
        commands.AddComponent<CbxWarm25>(_pool[25], default);
        commands.AddComponent<CbxWarm26>(_pool[26], default);
        commands.AddComponent<CbxWarm27>(_pool[27], default);
        commands.AddComponent<CbxWarm28>(_pool[28], default);
        commands.AddComponent<CbxWarm29>(_pool[29], default);
        commands.AddComponent<CbxWarm30>(_pool[30], default);
        commands.AddComponent<CbxWarm31>(_pool[31], default);
        commands.AddComponent<CbxWarm32>(_pool[32], default);
        commands.AddComponent<CbxWarm33>(_pool[33], default);
        commands.AddComponent<CbxWarm34>(_pool[34], default);
        commands.AddComponent<CbxWarm35>(_pool[35], default);
        commands.AddComponent<CbxWarm36>(_pool[36], default);
        commands.AddComponent<CbxWarm37>(_pool[37], default);
        commands.AddComponent<CbxWarm38>(_pool[38], default);
        commands.AddComponent<CbxWarm39>(_pool[39], default);
        commands.AddComponent<CbxWarm40>(_pool[40], default);
        commands.AddComponent<CbxWarm41>(_pool[41], default);
        commands.AddComponent<CbxWarm42>(_pool[42], default);
        commands.AddComponent<CbxWarm43>(_pool[43], default);
        commands.AddComponent<CbxWarm44>(_pool[44], default);
        commands.AddComponent<CbxWarm45>(_pool[45], default);
        commands.AddComponent<CbxWarm46>(_pool[46], default);
        commands.AddComponent<CbxWarm47>(_pool[47], default);
        commands.AddComponent<CbxWarm48>(_pool[48], default);

        _world.ApplyCommands();
        _world.AdvanceTick();
    }

    /// <summary>Creates+destroys, three component types' adds/removes, and tag flips, in an 8-way rotation.</summary>
    [Benchmark]
    public void MixedCommands()
    {
        var commands = _world.Commands;
        for (var i = 0; i < BatchSize; i++)
        {
            switch (i & 7)
            {
                case 0:
                    var slot = i & PoolMask;
                    commands.DestroyEntity(_pool[slot]);
                    _pool[slot] = commands.CreateEntity(new CbxPosition { X = i }, new CbxVelocity { X = i });
                    break;
                case 1:
                    commands.AddComponent<CbxPosition>(_pool[i & PoolMask], new CbxPosition { X = i });
                    break;
                case 2:
                    commands.AddComponent<CbxVelocity>(_pool[i & PoolMask], new CbxVelocity { X = i });
                    break;
                case 3:
                    commands.AddComponent<CbxHealth>(_pool[(i + 1) & PoolMask], new CbxHealth { Amount = i });
                    break;
                case 4:
                    commands.RemoveComponent<CbxHealth>(_pool[(i + 1) & PoolMask]);
                    break;
                case 5:
                    commands.AddTag<CbxFrozen>(_pool[(i + 2) & PoolMask]);
                    break;
                case 6:
                    commands.RemoveTag<CbxFrozen>(_pool[(i + 2) & PoolMask]);
                    break;
                default:
                    break;
            }
        }

        _world.ApplyCommands();
        _sink++;
    }

    /// <summary>Same shape minus every payload-carrying op: placements and tags only, no per-type buffer is ever touched.</summary>
    [Benchmark]
    public void StructuralAndTagsOnly()
    {
        var commands = _world.Commands;
        for (var i = 0; i < BatchSize; i++)
        {
            switch (i % 3)
            {
                case 0:
                    var slot = (i * 5) & PoolMask;
                    commands.DestroyEntity(_pool[slot]);
                    _pool[slot] = commands.CreateEntity(new CbxPosition { X = i }, new CbxVelocity { X = i });
                    break;
                case 1:
                    commands.AddTag<CbxFrozen>(_pool[(i + 1) & PoolMask]);
                    break;
                default:
                    commands.RemoveTag<CbxFrozen>(_pool[(i + 2) & PoolMask]);
                    break;
            }
        }

        _world.ApplyCommands();
        _sink++;
    }
}
