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
    /// One thread repeatedly reads <c>Signature</c>/<c>Setters</c> (what
    /// <see cref="CommandBuffer.CreateEntity(EntityTemplate)"/> does internally, and is
    /// itself documented safe to call from several threads at once) while another
    /// repeatedly calls <see cref="EntityTemplate.AddComponent{T}"/> — the actual
    /// concurrent race the freeze guard's own doc comment claims to close, not just the
    /// sequential instantiate-then-mutate case the other tests in this file cover. Both
    /// operations share one lock, so the only exception either thread should ever
    /// observe is <see cref="InvalidOperationException"/> from
    /// <c>ThrowIfFrozen</c> — never a torn-dictionary symptom
    /// (<see cref="IndexOutOfRangeException"/>, a "Collection was modified"
    /// <see cref="InvalidOperationException"/> from enumerating <c>_settersByType</c>
    /// mid-mutation, etc.) from the freeze check and the mutation it guards racing
    /// unsynchronized.
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

        // By now the reader thread has frozen the template — confirm that deterministically.
        var act = () => template.AddComponent(new Position { X = 0 });
        act.Should().Throw<InvalidOperationException>();
    }
}
