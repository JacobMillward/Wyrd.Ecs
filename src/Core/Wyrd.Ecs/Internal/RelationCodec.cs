namespace Wyrd.Ecs.Internal;

internal sealed class RelationCodec<T> : IRelationCodec where T : struct, IRelation
{
    private readonly RelationEncoder<T> _encode;
    private readonly RelationDecoder<T> _decode;

    public string Discriminator { get; }
    public int TypeIndex { get; }
    public uint? SchemaHash { get; }

    internal RelationCodec(string discriminator, RelationEncoder<T> encode, RelationDecoder<T> decode, uint? schemaHash)
    {
        Discriminator = discriminator;
        TypeIndex = Internal.TypeIndex<T>.Value;
        SchemaHash = schemaHash;
        _encode = encode;
        _decode = decode;
    }

    public byte[] EncodeValue(object value) => _encode((T)value);

    public IEnumerable<(Entity Target, byte[] Payload)> EncodeRow(Array rawItems, int row)
    {
        var links = ((RelationLinks<T>[])rawItems)[row];
        foreach (var (target, value) in links.Values)
            yield return (target, _encode(value));
    }

    public void DecodeInto(World world, Entity source, Entity target, byte[] data) =>
        world.Commands.AddRelation(source, target, _decode(data));
}
