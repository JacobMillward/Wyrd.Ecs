namespace Wyrd.Ecs.Persistence.Internal;

/// <summary>
/// A <see cref="FileStream"/>-backed write stream that buffers into a sibling
/// temporary file and only replaces the destination path on a normal <c>Dispose()</c>
/// — <see cref="File.Move(string, string, bool)"/> is atomic on the same volume on
/// every platform this targets, so a reader can never observe a half-written file.
/// <see cref="Abort"/> flips that default: the next <c>Dispose()</c> deletes the temp
/// file instead, leaving whatever was previously at the destination path untouched.
/// </summary>
internal sealed class AtomicFileWriteStream : Stream, ITransactionalWriteStream
{
    private readonly string _destinationPath;
    private readonly string _tempPath;
    private FileStream? _fileStream;
    private bool _aborted;

    public AtomicFileWriteStream(string destinationPath)
    {
        _destinationPath = destinationPath;
        _tempPath = destinationPath + $".tmp-{Guid.NewGuid():N}";
        _fileStream = File.Create(_tempPath);
    }

    public void Abort() => _aborted = true;

    protected override void Dispose(bool disposing)
    {
        if (disposing && _fileStream is not null)
        {
            _fileStream.Dispose();
            _fileStream = null;

            if (_aborted)
                File.Delete(_tempPath);
            else
                File.Move(_tempPath, _destinationPath, overwrite: true);
        }

        base.Dispose(disposing);
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => RequireOpen().Length;
    public override long Position
    {
        get => RequireOpen().Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _fileStream?.Flush();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => RequireOpen().Write(buffer, offset, count);

    private FileStream RequireOpen() => _fileStream ?? throw new ObjectDisposedException(nameof(AtomicFileWriteStream));
}
