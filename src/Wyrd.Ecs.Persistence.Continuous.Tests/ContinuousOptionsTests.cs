namespace Wyrd.Ecs.Persistence.Continuous.Tests;

public class ContinuousOptionsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"wyrd-continuous-options-{Guid.NewGuid():N}");

    public ContinuousOptionsTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Options_DefaultsToWalOptionsDefault()
    {
        var options = new ContinuousOptions
        {
            CheckpointStore = new FileStore(Path.Combine(_directory, "world.checkpoint")),
            WalStore = new FileWalStore(Path.Combine(_directory, "world")),
        };

        options.Options.Should().BeSameAs(WalOptions.Default);
    }

    [Fact]
    public void OnError_DefaultsToNull()
    {
        var options = new ContinuousOptions
        {
            CheckpointStore = new FileStore(Path.Combine(_directory, "world.checkpoint")),
            WalStore = new FileWalStore(Path.Combine(_directory, "world")),
        };

        options.OnError.Should().BeNull();
    }

    [Fact]
    public void EveryPropertyCanBeOverridden()
    {
        var checkpointStore = new FileStore(Path.Combine(_directory, "world.checkpoint"));
        var walStore = new FileWalStore(Path.Combine(_directory, "world"));
        var walOptions = new WalOptions { FsyncInterval = TimeSpan.FromMilliseconds(1) };
        Action<Exception> onError = _ => { };

        var options = new ContinuousOptions
        {
            CheckpointStore = checkpointStore,
            WalStore = walStore,
            Options = walOptions,
            OnError = onError,
        };

        options.CheckpointStore.Should().BeSameAs(checkpointStore);
        options.WalStore.Should().BeSameAs(walStore);
        options.Options.Should().BeSameAs(walOptions);
        options.OnError.Should().BeSameAs(onError);
    }
}
