using Friflo.Engine.ECS;

namespace Comparison.Friflo;

public struct Position : IComponent { public float X, Y, Z; }
public struct Velocity : IComponent { public float X, Y, Z; }
public struct Health : IComponent { public float Current, Max; }
public struct BulkPayload : IComponent { public long A, B, C, D; }

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

public struct Link : ILinkRelation
{
    public Entity Target;
    public float Weight;

    public Entity GetRelationKey() => Target;
}

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
