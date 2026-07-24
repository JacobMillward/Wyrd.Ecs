using Friflo.Engine.ECS;

namespace FrifloBenchmarks;

// Simulation-shaped data components, reused across every benchmark category.
public struct Position : IComponent { public float X, Y, Z; }
public struct Velocity : IComponent { public float X, Y, Z; }
public struct Health : IComponent { public float Current, Max; }

// A single larger (32-byte) component with no simulation meaning of its own — used
// wherever a benchmark needs a bulkier payload than Position/Velocity/Health: the
// 4th/5th component in Task 3's query-iteration arities, and the add/remove target
// in Task 4's archetype-move benchmark.
public struct BulkPayload : IComponent { public long A, B, C, D; }

// Small (4-byte), content-free components used only to reach an 8-component entity
// in Task 2's creation benchmark (Position/Velocity/Health/BulkPayload covers only
// 4) and a 5-component query in Task 3 — their values are never read.
public struct Padding1 : IComponent { public int Value; }
public struct Padding2 : IComponent { public int Value; }
public struct Padding3 : IComponent { public int Value; }
public struct Padding4 : IComponent { public int Value; }

// Zero-size tag, benchmarked directly in Task 4.
public struct Marker : ITag;

// Sixteen zero-size fragmentation tags. Task 3's fragmented-iteration benchmarks
// give every Nth entity Frag(N % 16) alongside its real query components, so
// entities matching a query are spread across up to 16 archetypes instead of 1.
public struct Frag0 : ITag;
public struct Frag1 : ITag;
public struct Frag2 : ITag;
public struct Frag3 : ITag;
public struct Frag4 : ITag;
public struct Frag5 : ITag;
public struct Frag6 : ITag;
public struct Frag7 : ITag;
public struct Frag8 : ITag;
public struct Frag9 : ITag;
public struct Frag10 : ITag;
public struct Frag11 : ITag;
public struct Frag12 : ITag;
public struct Frag13 : ITag;
public struct Frag14 : ITag;
public struct Frag15 : ITag;

// Generalizes Game.Simulation's MeshLink (src/Game.Simulation/Mesh/MeshLink.cs)
// into an engine-agnostic two-entity relation, benchmarked in Task 5.
public struct Link : ILinkRelation
{
    public Entity Target;
    public float Weight;

    public Entity GetRelationKey() => Target;
}

/// <summary>
/// Adds fragmentation tag <c>Frag(slot % 16)</c> to <paramref name="entity"/>. Shared
/// by every fragmented-layout benchmark setup in this project.
/// </summary>
public static class Fragmentation
{
    public const int SlotCount = 16;

    public static void AddFragTag(Entity entity, int slot)
    {
        switch (slot % SlotCount)
        {
            case 0: entity.AddTag<Frag0>(); break;
            case 1: entity.AddTag<Frag1>(); break;
            case 2: entity.AddTag<Frag2>(); break;
            case 3: entity.AddTag<Frag3>(); break;
            case 4: entity.AddTag<Frag4>(); break;
            case 5: entity.AddTag<Frag5>(); break;
            case 6: entity.AddTag<Frag6>(); break;
            case 7: entity.AddTag<Frag7>(); break;
            case 8: entity.AddTag<Frag8>(); break;
            case 9: entity.AddTag<Frag9>(); break;
            case 10: entity.AddTag<Frag10>(); break;
            case 11: entity.AddTag<Frag11>(); break;
            case 12: entity.AddTag<Frag12>(); break;
            case 13: entity.AddTag<Frag13>(); break;
            case 14: entity.AddTag<Frag14>(); break;
            case 15: entity.AddTag<Frag15>(); break;
        }
    }
}
