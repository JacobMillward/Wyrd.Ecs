namespace Comparison.Fennecs;

public struct Position { public float X, Y, Z; }
public struct Velocity { public float X, Y, Z; }
public struct Health { public float Current, Max; }
public struct BulkPayload { public long A, B, C, D; }

public struct Padding1 { public int Value; }
public struct Padding2 { public int Value; }
public struct Padding3 { public int Value; }
public struct Padding4 { public int Value; }

public struct Marker;

public struct Frag0;
public struct Frag1;
public struct Frag2;
public struct Frag3;
public struct Frag4;
public struct Frag5;
public struct Frag6;
public struct Frag7;
public struct Frag8;
public struct Frag9;
public struct Frag10;
public struct Frag11;
public struct Frag12;
public struct Frag13;
public struct Frag14;
public struct Frag15;

// fennecs relations key the target entity out-of-band (Entity.Add<R>(value, target)) rather
// than embedding it in the component, unlike Friflo's ILinkRelation. This Link carries no
// Target field.
public struct Link { public float Weight; }

public static class Fragmentation
{
    public const int SlotCount = 16;

    public static void AddFragTag(global::fennecs.Entity entity, int slot)
    {
        switch (slot % SlotCount)
        {
            case 0: entity.Add(new Frag0()); break;
            case 1: entity.Add(new Frag1()); break;
            case 2: entity.Add(new Frag2()); break;
            case 3: entity.Add(new Frag3()); break;
            case 4: entity.Add(new Frag4()); break;
            case 5: entity.Add(new Frag5()); break;
            case 6: entity.Add(new Frag6()); break;
            case 7: entity.Add(new Frag7()); break;
            case 8: entity.Add(new Frag8()); break;
            case 9: entity.Add(new Frag9()); break;
            case 10: entity.Add(new Frag10()); break;
            case 11: entity.Add(new Frag11()); break;
            case 12: entity.Add(new Frag12()); break;
            case 13: entity.Add(new Frag13()); break;
            case 14: entity.Add(new Frag14()); break;
            case 15: entity.Add(new Frag15()); break;
        }
    }
}
