using Microsoft.CodeAnalysis.Testing;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Wyrd.Ecs.Analyzers.MutEntityQueryRefForEachAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Wyrd.Ecs.Analyzers.Tests;

public class MutEntityQueryRefForEachAnalyzerTests
{
    // A minimal stand-in for Wyrd.Ecs's real MutEntityQuery<T> shape. The analyzer
    // matches by namespace + name + arity, not by referencing the real Wyrd.Ecs
    // assembly, so this fake — with a genuinely ref-returning Current, matching the
    // real type — is enough to exercise it in isolation.
    private const string FakeApi = @"
namespace Wyrd.Ecs
{
    public struct Position { public float X; }

    public ref struct MutEntityQuery<T>
    {
        public Enumerator GetEnumerator() => default;

        public ref struct Enumerator
        {
            // Array-backed, not a plain instance field — a ref struct can't
            // ref-return its own value field (CS8170), same reason the real
            // MutEntityQuery<T> ref-returns into a Span over external storage.
            private T[] _value;
            public ref T Current => ref _value[0];
            public bool MoveNext() => false;
        }
    }
}
";

    [Fact]
    public async Task MissingRef_ReportsDiagnostic()
    {
        var test = FakeApi + @"
class C
{
    void M(Wyrd.Ecs.MutEntityQuery<Wyrd.Ecs.Position> query)
    {
        {|#0:foreach (var position in query)
        {
        }|}
    }
}
";
        var expected = Verify.Diagnostic(MutEntityQueryRefForEachAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Position");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RefBound_NoDiagnostic()
    {
        var test = FakeApi + @"
class C
{
    void M(Wyrd.Ecs.MutEntityQuery<Wyrd.Ecs.Position> query)
    {
        foreach (ref var position in query)
        {
        }
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
    void M(Wyrd.Ecs.MutEntityQuery<Wyrd.Ecs.Position> query)
    {
        foreach (ref readonly var position in query)
        {
        }
    }
}
";
        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task UnrelatedForEach_NoDiagnostic()
    {
        var test = @"
using System.Collections.Generic;

class C
{
    void M(List<int> items)
    {
        foreach (var item in items)
        {
        }
    }
}
";
        await Verify.VerifyAnalyzerAsync(test);
    }
}
