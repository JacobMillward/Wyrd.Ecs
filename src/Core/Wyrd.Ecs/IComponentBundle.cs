namespace Wyrd.Ecs;

/// <summary>
/// A fixed group of components applied to one entity together, behind one named,
/// constructible value instead of several individual <c>AddComponent</c> calls. Implement
/// <see cref="ApplyTo{TSink}"/> using <see cref="BundleBuilder{TSink}"/> for the chainable,
/// zero-boxing shape every built-in bundle uses. Works identically against
/// <see cref="EntityView.Add{TBundle}"/> and <see cref="EntityTemplate.Add{TBundle}"/>.
/// </summary>
public interface IComponentBundle
{
    /// <summary>Adds this bundle's components to <paramref name="sink"/>.</summary>
    void ApplyTo<TSink>(TSink sink) where TSink : IComponentSink, allows ref struct;
}
