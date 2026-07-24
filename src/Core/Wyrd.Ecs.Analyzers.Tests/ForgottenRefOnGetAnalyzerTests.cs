using Microsoft.CodeAnalysis.Testing;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Wyrd.Ecs.Analyzers.ForgottenRefOnGetAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Wyrd.Ecs.Analyzers.Tests;

public class ForgottenRefOnGetAnalyzerTests
{
    // A minimal stand-in for Wyrd.Ecs's real QueryRow<T0> shape. The analyzer matches
    // by namespace + name ("QueryRow"), not by referencing the real Wyrd.Ecs
    // assembly, so a genuinely ref-returning generic Get<T>() is enough in isolation.
    private const string FakeApi = @"
namespace Wyrd.Ecs
{
    public struct Position { public float X; }

    public readonly ref struct QueryRow<T0>
    {
        public ref T Get<T>() => throw new System.NotImplementedException();
    }
}
";

    [Fact]
    public async Task MissingRef_ReportsDiagnostic()
    {
        var test = FakeApi + @"
class C
{
    void M(Wyrd.Ecs.QueryRow<Wyrd.Ecs.Position> row)
    {
        {|#0:var position = row.Get<Wyrd.Ecs.Position>();|}
    }
}
";
        var expected = Verify.Diagnostic(ForgottenRefOnGetAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("position", "Position");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RefBound_NoDiagnostic()
    {
        var test = FakeApi + @"
class C
{
    void M(Wyrd.Ecs.QueryRow<Wyrd.Ecs.Position> row)
    {
        ref var position = ref row.Get<Wyrd.Ecs.Position>();
    }
}
";
        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RefReadonlyBound_NoDiagnostic()
    {
        var test = FakeApi + @"
class C
{
    void M(Wyrd.Ecs.QueryRow<Wyrd.Ecs.Position> row)
    {
        ref readonly var position = ref row.Get<Wyrd.Ecs.Position>();
    }
}
";
        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MissingRef_InAForLoopInitializer_ReportsDiagnosticAtTheDeclaratorItself()
    {
        var test = FakeApi + @"
class C
{
    void M(Wyrd.Ecs.QueryRow<Wyrd.Ecs.Position> row)
    {
        for ({|#0:var position = row.Get<Wyrd.Ecs.Position>()|}; ; )
        {
            break;
        }
    }
}
";
        var expected = Verify.Diagnostic(ForgottenRefOnGetAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("position", "Position");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task UnrelatedLocalDeclaration_NoDiagnostic()
    {
        var test = @"
class C
{
    void M()
    {
        var x = 5;
    }
}
";
        await Verify.VerifyAnalyzerAsync(test);
    }
}
