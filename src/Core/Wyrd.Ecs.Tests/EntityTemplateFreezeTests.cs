namespace Wyrd.Ecs.Tests;

public class EntityTemplateFreezeTests
{
    private struct Position : IComponent { public float X; }
    private struct Flag : ITag;

    [Fact]
    public void AddComponent_AfterInstantiation_Throws()
    {
        var world = new World();
        var template = new EntityTemplate().AddComponent(new Position { X = 1f });
        world.Commands.CreateEntity(template);
        world.ApplyCommands();

        var act = () => template.AddComponent(new Position { X = 2f });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddTag_AfterInstantiation_Throws()
    {
        var world = new World();
        var template = new EntityTemplate().AddComponent(new Position { X = 1f });
        world.Commands.CreateEntity(template);
        world.ApplyCommands();

        var act = () => template.AddTag<Flag>();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddChild_AfterInstantiation_Throws()
    {
        var world = new World();
        var template = new EntityTemplate().AddComponent(new Position { X = 1f });
        world.Commands.CreateEntity(template);
        world.ApplyCommands();

        var act = () => template.AddChild(new EntityTemplate());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddParent_AfterInstantiation_Throws()
    {
        var world = new World();
        var template = new EntityTemplate().AddComponent(new Position { X = 1f });
        Entity parent = world.Commands.CreateEntity();
        world.Commands.CreateEntity(template);
        world.ApplyCommands();

        var act = () => template.AddParent(parent);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddComponent_BeforeInstantiation_StillWorks()
    {
        var template = new EntityTemplate();

        var act = () => template.AddComponent(new Position { X = 1f });

        act.Should().NotThrow();
    }

    /// <summary>
    /// Reads Signature/Setters on one thread while AddComponent runs on another. The only
    /// exception either thread should see is <see cref="InvalidOperationException"/> from
    /// <c>ThrowIfFrozen</c>, never a torn-dictionary symptom.
    /// </summary>
    [Fact]
    public void ConcurrentReadAndMutate_NeverThrowsAnythingOtherThanFrozen()
    {
        var template = new EntityTemplate().AddComponent(new Position { X = 1f });
        var otherExceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        using var stop = new CancellationTokenSource();

        var readerThread = new Thread(() =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    _ = template.Signature;
                    _ = template.Setters;
                }
            }
            catch (Exception ex)
            {
                otherExceptions.Add(ex);
            }
        });

        var writerThreads = Enumerable.Range(0, 4).Select(_ => new Thread(() =>
        {
            try
            {
                for (var i = 0; i < 2_000; i++)
                {
                    try
                    {
                        template.AddComponent(new Position { X = i });
                    }
                    catch (InvalidOperationException)
                    {
                        // Expected once the reader thread has frozen the template.
                    }
                }
            }
            catch (Exception ex)
            {
                otherExceptions.Add(ex);
            }
        })).ToArray();

        readerThread.Start();
        foreach (var thread in writerThreads) thread.Start();
        foreach (var thread in writerThreads) thread.Join();
        stop.Cancel();
        readerThread.Join();

        otherExceptions.Should().BeEmpty();

        // By now the reader thread has frozen the template: confirm that deterministically.
        var act = () => template.AddComponent(new Position { X = 0 });
        act.Should().Throw<InvalidOperationException>();
    }
}
