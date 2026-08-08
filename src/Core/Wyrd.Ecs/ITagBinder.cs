namespace Wyrd.Ecs;

/// <summary>
/// Closes a registered tag type over a runtime discriminator lookup, the same shape
/// <see cref="IComponentCodec"/> uses for components - <see cref="Bind"/> turns "add this
/// tag" into a compile-time-typed <see cref="CommandBuffer"/> call with no reflection.
/// </summary>
public interface ITagBinder
{
    /// <summary>The stable wire discriminator this tag was registered under.</summary>
    string Discriminator { get; }

    /// <summary>Adds the tag this binder was closed over to <paramref name="entity"/>.</summary>
    void Bind(CommandBuffer commands, Entity entity);
}
