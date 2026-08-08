namespace Wyrd.Ecs.Internal;

internal sealed class TagBinder<T>(string discriminator) : ITagBinder where T : struct, ITag
{
    public string Discriminator { get; } = discriminator;
    public void Bind(CommandBuffer commands, Entity entity) => commands.AddTag<T>(entity);
}
