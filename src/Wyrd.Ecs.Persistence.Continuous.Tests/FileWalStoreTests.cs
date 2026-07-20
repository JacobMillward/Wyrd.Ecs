namespace Wyrd.Ecs.Persistence.Continuous.Tests;

public class FileWalStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"wyrd-continuous-test-{Guid.NewGuid():N}");
    private string BasePath => Path.Combine(_directory, "world");

    public FileWalStoreTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void OpenSegmentAppend_ThenOpenSegmentRead_RoundTripsBytes()
    {
        var store = new FileWalStore(BasePath);
        var written = new byte[] { 1, 2, 3, 4, 5 };

        using (var writeStream = store.OpenSegmentAppend(startTick: 1))
            writeStream.Write(written);

        using var readStream = store.OpenSegmentRead(startTick: 1);
        var read = new byte[written.Length];
        readStream.ReadExactly(read);

        read.Should().Equal(written);
    }

    [Fact]
    public void OpenSegmentAppend_CalledTwiceForTheSameStartTick_ThrowsWithoutOverwriting()
    {
        var store = new FileWalStore(BasePath);
        using (var first = store.OpenSegmentAppend(startTick: 1))
            first.Write(new byte[] { 1, 2, 3 });

        var act = () => store.OpenSegmentAppend(startTick: 1);

        act.Should().Throw<IOException>();

        using var readStream = store.OpenSegmentRead(startTick: 1);
        var read = new byte[3];
        readStream.ReadExactly(read);
        read.Should().Equal(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void ListSegmentStartTicks_ReturnsEveryCreatedSegmentInAscendingOrder()
    {
        var store = new FileWalStore(BasePath);
        store.OpenSegmentAppend(startTick: 50).Dispose();
        store.OpenSegmentAppend(startTick: 1).Dispose();
        store.OpenSegmentAppend(startTick: 20).Dispose();

        store.ListSegmentStartTicks().Should().Equal([1, 20, 50]);
    }

    [Fact]
    public void ListSegmentStartTicks_OnAFreshStore_ReturnsEmpty()
    {
        var store = new FileWalStore(BasePath);

        store.ListSegmentStartTicks().Should().BeEmpty();
    }

    [Fact]
    public void DeleteSegment_RemovesItFromListSegmentStartTicks()
    {
        var store = new FileWalStore(BasePath);
        store.OpenSegmentAppend(startTick: 1).Dispose();
        store.OpenSegmentAppend(startTick: 2).Dispose();

        store.DeleteSegment(startTick: 1);

        store.ListSegmentStartTicks().Should().Equal([2]);
    }

    [Fact]
    public void OpenSegmentRead_ForANonexistentSegment_Throws()
    {
        var store = new FileWalStore(BasePath);

        var act = () => store.OpenSegmentRead(startTick: 999);

        act.Should().Throw<FileNotFoundException>();
    }
}
