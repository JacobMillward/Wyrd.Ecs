namespace Wyrd.Ecs.Persistence.Tests;

public class FileStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"wyrd-persistence-test-{Guid.NewGuid():N}.bin");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void OpenCheckpointWrite_ThenOpenCheckpointRead_RoundTripsBytes()
    {
        var store = new FileStore(_path);
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
        var store = new FileStore(_path);

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

    [Fact]
    public void OpenCheckpointWrite_WhenAbortedBeforeDispose_LeavesThePreviousCheckpointUntouched()
    {
        var store = new FileStore(_path);
        using (var first = store.OpenCheckpointWrite())
            first.Write(new byte[] { 1, 2, 3 });

        using (var second = store.OpenCheckpointWrite())
        {
            second.Write(new byte[] { 9, 9 });
            ((ITransactionalWriteStream)second).Abort();
        }

        using var readStream = store.OpenCheckpointRead();
        var read = new byte[3];
        readStream.ReadExactly(read);
        read.Should().Equal(new byte[] { 1, 2, 3 });
        readStream.Length.Should().Be(3);
    }

    [Fact]
    public void OpenCheckpointWrite_WhenAbortedBeforeDispose_AndNoPreviousCheckpointExisted_LeavesNoFileBehind()
    {
        var store = new FileStore(_path);

        using (var stream = store.OpenCheckpointWrite())
        {
            stream.Write(new byte[] { 9, 9 });
            ((ITransactionalWriteStream)stream).Abort();
        }

        File.Exists(_path).Should().BeFalse();
    }

    [Fact]
    public void OpenCheckpointWrite_LeavesNoTempFileBehindOnCommitOrAbort()
    {
        var store = new FileStore(_path);
        var directory = Path.GetDirectoryName(_path)!;
        var tempFilePattern = Path.GetFileName(_path) + ".tmp-*";

        using (var stream = store.OpenCheckpointWrite())
            stream.Write(new byte[] { 1 });
        Directory.GetFiles(directory, tempFilePattern).Should().BeEmpty();

        using (var stream = store.OpenCheckpointWrite())
        {
            stream.Write(new byte[] { 2 });
            ((ITransactionalWriteStream)stream).Abort();
        }
        Directory.GetFiles(directory, tempFilePattern).Should().BeEmpty();
    }

    [Fact]
    public void Path_ReturnsTheConstructorArgument()
    {
        var store = new FileStore(_path);

        store.Path.Should().Be(_path);
    }
}
