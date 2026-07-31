using Wyrd.Ecs;

namespace Comparison.Wyrd;

public struct Position : IComponent { public float X, Y, Z; }
public struct Velocity : IComponent { public float X, Y, Z; }
public struct Health : IComponent { public float Current, Max; }
public struct BulkPayload : IComponent { public long A, B, C, D; }

public struct Link : IRelation { public float Weight; }

public struct Padding1 : IComponent { public int Value; }
public struct Padding2 : IComponent { public int Value; }
public struct Padding3 : IComponent { public int Value; }
public struct Padding4 : IComponent { public int Value; }

public struct Marker : ITag;

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

/// <summary>Adds fragmentation tag <c>Frag(slot % 16)</c> to <paramref name="entity"/>.</summary>
public static class Fragmentation
{
    public const int SlotCount = 16;

    public static void AddFragTag(World world, Entity entity, int slot)
    {
        switch (slot % SlotCount)
        {
            case 0: world.Commands.AddTag<Frag0>(entity); break;
            case 1: world.Commands.AddTag<Frag1>(entity); break;
            case 2: world.Commands.AddTag<Frag2>(entity); break;
            case 3: world.Commands.AddTag<Frag3>(entity); break;
            case 4: world.Commands.AddTag<Frag4>(entity); break;
            case 5: world.Commands.AddTag<Frag5>(entity); break;
            case 6: world.Commands.AddTag<Frag6>(entity); break;
            case 7: world.Commands.AddTag<Frag7>(entity); break;
            case 8: world.Commands.AddTag<Frag8>(entity); break;
            case 9: world.Commands.AddTag<Frag9>(entity); break;
            case 10: world.Commands.AddTag<Frag10>(entity); break;
            case 11: world.Commands.AddTag<Frag11>(entity); break;
            case 12: world.Commands.AddTag<Frag12>(entity); break;
            case 13: world.Commands.AddTag<Frag13>(entity); break;
            case 14: world.Commands.AddTag<Frag14>(entity); break;
            case 15: world.Commands.AddTag<Frag15>(entity); break;
        }
    }
}
