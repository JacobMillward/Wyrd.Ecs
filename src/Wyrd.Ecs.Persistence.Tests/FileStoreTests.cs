namespace Wyrd.Ecs.Persistence.Tests;

public class LocalFilePersistenceStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"wyrd-persistence-test-{Guid.NewGuid():N}.bin");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void OpenCheckpointWrite_ThenOpenCheckpointRead_RoundTripsBytes()
    {
        var store = new LocalFilePersistenceStore(_path);
        var written = new byte[] { 1, 2, 3, 4, 5 };

        using (var writeStream = store.OpenCheckpointWrite())
            writeStream.Write(written);

        using var readStream = store.OpenCheckpointRead();
        var read = new byte[written.Length];
        readStream.ReadExactly(read);

        read.Should().Equal(written);
    }

    [Fact]
    public void OpenCheckpointWrite_CalledTwice_OverwritesThePreviousCheckpoint()
    {
        var store = new LocalFilePersistenceStore(_path);

        using (var first = store.OpenCheckpointWrite())
            first.Write(new byte[] { 1, 2, 3 });

        using (var second = store.OpenCheckpointWrite())
            second.Write(new byte[] { 9, 9 });

        using var readStream = store.OpenCheckpointRead();
        var read = new byte[2];
        readStream.ReadExactly(read);
        read.Should().Equal(new byte[] { 9, 9 });
        readStream.Length.Should().Be(2);
    }
}
