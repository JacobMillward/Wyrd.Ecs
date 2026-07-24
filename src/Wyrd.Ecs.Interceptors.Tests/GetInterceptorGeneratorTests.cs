namespace Wyrd.Ecs.Interceptors.Tests;

public class GetInterceptorGeneratorTests
{
    [Fact]
    public void PureReadThroughARowGet_EmitsAnInterceptor()
    {
        const string source = """
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public class Reader
            {
                public float Read(QueryRow<Position> row) => row.Get<Position>().X;
            }
            """;

        var result = GeneratorTestHost.Run(new GetInterceptorGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("namespace Wyrd.Ecs.Interceptors.Generated;");
        generated.Should().Contain("file static class Interceptors");
        generated.Should().Contain("[global::System.Runtime.CompilerServices.InterceptsLocationAttribute(");
        generated.Should().Contain("public static ref Position Intercepted1(this in Wyrd.Ecs.QueryRow<Position> self) => ref self.GetUnmarked<Position>();");
    }

    [Fact]
    public void WriteThroughARowGet_EmitsNoInterceptorForThatCall()
    {
        const string source = """
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public class Writer
            {
                public void Write(QueryRow<Position> row) => row.Get<Position>().X += 1f;
            }
            """;

        var result = GeneratorTestHost.Run(new GetInterceptorGenerator(), GeneratorTestHost.Compile(source));

        result.Results[0].GeneratedSources.Single().SourceText.ToString().Should().NotContain("Intercepted");
    }
}
