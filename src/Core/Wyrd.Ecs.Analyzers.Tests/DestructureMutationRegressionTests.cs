using Microsoft.CodeAnalysis.Testing;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Wyrd.Ecs.Analyzers.ForgottenRefOnGetAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Wyrd.Ecs.Analyzers.Tests;

/// <summary>
/// Not a custom-analyzer test — a regression proof that the design spec's second
/// analyzer requirement ("mutation through a destructured element... cannot be
/// allowed to compile silently") is already met natively by the C# compiler for the
/// <c>foreach (var (a, b) in ...)</c> shape, so no custom <c>Wyrd.Ecs.Analyzers</c>
/// rule is needed for it. See the plan's Task 4 for the full reasoning.
/// </summary>
public class DestructureMutationRegressionTests
{
    // A minimal stand-in for Wyrd.Ecs's real Query<T0,T1>/QueryRow<T0,T1> shape —
    // the native CS1654/CS1656 protection tested here applies to any deconstructing
    // foreach, not something specific to the real Wyrd.Ecs assembly.
    private const string FakeApi = @"
namespace Wyrd.Ecs
{
    public struct Position { public float X; }
    public struct Velocity { public float X; }

    public readonly ref struct QueryRow<T0, T1>
    {
        public void Deconstruct(out T0 component0, out T1 component1)
        {
            component0 = default!;
            component1 = default!;
        }
    }

    public readonly ref struct Query<T0, T1>
    {
        public Enumerator GetEnumerator() => default;

        public ref struct Enumerator
        {
            public QueryRow<T0, T1> Current => default;
            public bool MoveNext() => false;
        }
    }
}
";

    [Fact]
    public async Task WriteThroughDestructuredFieldMember_IsANativeCompileError()
    {
        var test = FakeApi + @"
class C
{
    void M(Wyrd.Ecs.Query<Wyrd.Ecs.Position, Wyrd.Ecs.Velocity> query)
    {
        foreach (var (position, velocity) in query)
        {
            {|#0:position.X|} += 1f;
        }
    }
}
";
        var expected = DiagnosticResult.CompilerError("CS1654")
            .WithLocation(0)
            .WithArguments("position", "foreach iteration variable");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task DirectAssignmentToDestructuredLocal_IsANativeCompileError()
    {
        var test = FakeApi + @"
class C
{
    void M(Wyrd.Ecs.Query<Wyrd.Ecs.Position, Wyrd.Ecs.Velocity> query)
    {
        foreach (var (position, velocity) in query)
        {
            {|#0:position|} = default;
        }
    }
}
";
        var expected = DiagnosticResult.CompilerError("CS1656")
            .WithLocation(0)
            .WithArguments("position", "foreach iteration variable");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ReadOnlyUse_NoDiagnostic()
    {
        var test = FakeApi + @"
class C
{
    void M(Wyrd.Ecs.Query<Wyrd.Ecs.Position, Wyrd.Ecs.Velocity> query)
    {
        var total = 0f;
        foreach (var (position, velocity) in query)
        {
            total += position.X + velocity.X;
        }
    }
}
";
        await Verify.VerifyAnalyzerAsync(test);
    }
}
