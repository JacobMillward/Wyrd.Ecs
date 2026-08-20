using SDL3;
using Wyrd.Ecs.Persistence;

namespace Wyrd.Ecs.Input.Tests;

public class BindingTablePersistenceTests
{
    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"wyrd-input-test-{Guid.NewGuid():N}.json");

    [Fact]
    public void LoadOverrides_WithNoFileYet_LeavesCodeDefaultsInPlace()
    {
        var table = new BindingTable<TestAction>();
        table.Bind(TestAction.Jump, SDL.Scancode.Space);

        table.LoadOverrides(TempPath()); // never written to - FileNotFoundException swallowed

        table.KeysFor(0, TestAction.Jump).Should().BeEquivalentTo([SDL.Scancode.Space]);
    }

    [Fact]
    public void SaveThenLoadOverrides_RoundTripsAKeyBinding()
    {
        var path = TempPath();
        try
        {
            var saved = new BindingTable<TestAction>();
            saved.Bind(TestAction.Jump, SDL.Scancode.Return);
            saved.SaveOverrides(path);

            var loaded = new BindingTable<TestAction>();
            loaded.Bind(TestAction.Jump, SDL.Scancode.Space); // code default, must be replaced not merged
            loaded.LoadOverrides(path);

            loaded.KeysFor(0, TestAction.Jump).Should().BeEquivalentTo([SDL.Scancode.Return]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void LoadOverrides_OnlyReplacesActionsTheFileActuallyContains()
    {
        var path = TempPath();
        try
        {
            var saved = new BindingTable<TestAction>();
            saved.Bind(TestAction.Jump, SDL.Scancode.Return);
            saved.SaveOverrides(path);

            var loaded = new BindingTable<TestAction>();
            loaded.Bind(TestAction.Jump, SDL.Scancode.Space);
            loaded.BindAxis2D(TestAction.Move, SDL.Scancode.W, SDL.Scancode.S, SDL.Scancode.A, SDL.Scancode.D);
            loaded.LoadOverrides(path);

            loaded.AxisFor(0, TestAction.Move).Should().Be((SDL.Scancode.W, SDL.Scancode.S, SDL.Scancode.A, SDL.Scancode.D));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SavedFile_IsHumanEditableJsonKeyedByEnumName()
    {
        var path = TempPath();
        try
        {
            var table = new BindingTable<TestAction>();
            table.Bind(TestAction.Jump, SDL.Scancode.Space);
            table.SaveOverrides(path);

            var json = File.ReadAllText(path);

            json.Should().Contain("Jump").And.Contain("Space");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
