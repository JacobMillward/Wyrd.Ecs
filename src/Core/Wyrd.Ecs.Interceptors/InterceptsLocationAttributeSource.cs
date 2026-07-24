using Microsoft.CodeAnalysis;

namespace Wyrd.Ecs.Interceptors;

/// <summary>
/// The .NET SDK does not ship <c>System.Runtime.CompilerServices.InterceptsLocationAttribute</c>
/// in corelib, so every generator that emits interceptors has to supply its own
/// definition. This is that definition, emitted once per consuming compilation.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class InterceptsLocationAttributeSource : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
            "InterceptsLocationAttribute.g.cs",
            """
            namespace System.Runtime.CompilerServices
            {
                [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
                internal sealed class InterceptsLocationAttribute : Attribute
                {
                    public InterceptsLocationAttribute(int version, string data) { }
                }
            }
            """));
    }
}
