using Wyrd.Ecs;
using Wyrd.Ecs.Persistence;
using Wyrd.Ecs.Persistence.Binary;

var path = Path.Combine(Path.GetTempPath(), $"wyrd-aot-smoke-{Guid.NewGuid():N}.bin");
try
{
    var world = new WorldBuilder().AddBinaryPersistence(path).Build();
    world.Commands.CreateEntity(new Unmanaged { X = 1f, Y = 2f });
    world.Commands.CreateEntity(new WithString { Name = "hello", Count = 42 });
    world.Commands.CreateEntity(new WithNested { Label = new Label { Text = "nested" } });
    world.Commands.CreateEntity(new WithCollection { Tags = new[] { "a", "b", "c" } });
    world.ApplyCommands();
    world.Save();

    var loaded = new WorldBuilder().AddBinaryPersistence(path).Build();
    loaded.Load();

    var ok = true;
    var foundUnmanaged = false;
    foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<Unmanaged>>().Resolve(loaded))
    {
        var values = chunk.Access<Ref<Unmanaged>>();
        for (var i = 0; i < chunk.Count; i++)
        {
            ok &= values[i].X == 1f && values[i].Y == 2f;
            foundUnmanaged = true;
        }
    }

    var foundString = false;
    foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<WithString>>().Resolve(loaded))
    {
        var values = chunk.Access<Ref<WithString>>();
        for (var i = 0; i < chunk.Count; i++)
        {
            ok &= values[i].Name == "hello" && values[i].Count == 42;
            foundString = true;
        }
    }

    var foundNested = false;
    foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<WithNested>>().Resolve(loaded))
    {
        var values = chunk.Access<Ref<WithNested>>();
        for (var i = 0; i < chunk.Count; i++)
        {
            ok &= values[i].Label.Text == "nested";
            foundNested = true;
        }
    }

    var foundCollection = false;
    foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<WithCollection>>().Resolve(loaded))
    {
        var values = chunk.Access<Ref<WithCollection>>();
        for (var i = 0; i < chunk.Count; i++)
        {
            ok &= values[i].Tags is ["a", "b", "c"];
            foundCollection = true;
        }
    }

    if (!ok || !foundUnmanaged || !foundString || !foundNested || !foundCollection)
    {
        Console.Error.WriteLine("FAIL: NativeAOT binary persistence round trip did not match");
        return 1;
    }

    Console.WriteLine("OK: NativeAOT binary persistence round trip succeeded");
    return 0;
}
finally
{
    if (File.Exists(path)) File.Delete(path);
}

public struct Unmanaged : IComponent
{
    public float X;
    public float Y;
}

public struct WithString : IComponent
{
    public string Name;
    public int Count;
}

public struct Label
{
    public string Text;
}

public struct WithNested : IComponent
{
    public Label Label;
}

public struct WithCollection : IComponent
{
    public string[] Tags;
}
