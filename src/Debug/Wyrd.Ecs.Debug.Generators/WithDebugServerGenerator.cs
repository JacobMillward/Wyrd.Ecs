using Microsoft.CodeAnalysis;

namespace Wyrd.Ecs.Debug.Generators;

/// <summary>
/// Emits <c>World.WithDebugServer(int port = 5299)</c> into every project that
/// references <c>Wyrd.Ecs.Debug</c>: unconditional, no scanning needed. Unlike
/// <see cref="Wyrd.Ecs.Persistence.Json.Generators.JsonRegistrationGenerator"/>, there's
/// no per-consumer content to discover, just one fixed overload to add.
/// References <c>Wyrd.Ecs.Persistence.Json.JsonAutoRegistration.RegisterAllIncludingIgnored</c>
/// and <c>World.WithDebugServer(CodecRegistry, DebugServerOptions?)</c> by name only,
/// never by seeing their generated/compiled syntax. Same by-convention technique
/// <c>JsonRegistrationGenerator</c> already documents for referencing
/// <c>JsonContextEmitTask</c>'s output, needed for the same reason: one Roslyn generator
/// cannot see another generator's output within the same compilation
/// (dotnet/roslyn#77560), but a blind reference by name still compiles once every
/// generator's output is present together in the final pass.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class WithDebugServerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx => ctx.AddSource("GeneratedWorldExtensions.g.cs", Source));
    }

    private const string Source = """
        namespace Wyrd.Ecs.Debug;

        public static class GeneratedWorldExtensions
        {
            public static global::Wyrd.Ecs.Debug.DebugServer WithDebugServer(this global::Wyrd.Ecs.World world, int port = 5299)
            {
                var registry = new global::Wyrd.Ecs.CodecRegistry();
                global::Wyrd.Ecs.Persistence.Json.JsonAutoRegistration.RegisterAllIncludingIgnored(registry);
                return world.WithDebugServer(registry, new global::Wyrd.Ecs.Debug.DebugServerOptions(Port: port));
            }
        }
        """;
}
