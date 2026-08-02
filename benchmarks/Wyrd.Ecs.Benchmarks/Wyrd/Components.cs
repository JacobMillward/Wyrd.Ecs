using Wyrd.Ecs;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

// Four more zero-meaning, content-free components alongside Comparison.Wyrd's Padding1-4,
// needed only to reach 12-component arity in HighArityQueryIterationBenchmarks. Values are
// never read.
public struct Padding5 : IComponent { public int Value; }
public struct Padding6 : IComponent { public int Value; }
public struct Padding7 : IComponent { public int Value; }
public struct Padding8 : IComponent { public int Value; }
