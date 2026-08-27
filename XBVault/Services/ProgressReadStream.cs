#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace XBVault.Services;

/// <summary>
/// A read-only stream wrapper that reports how far its inner stream has been
/// consumed. Wraps a <see cref="FileStream"/> inside an
/// <see cref="System.Net.Http.StreamContent"/> so upload progress can be shown
/// while bytes actually flow, instead of jumping 0-100% once the upload ends.
/// </summary>
public sealed class ProgressReadStream : Stream
{
    private readonly Stream _inner;
    private readonly long _total;
    private readonly IProgress<double>? _progress;

    public ProgressReadStream(Stream inner, IProgress<double>? progress)
    {
        _inner = inner;
        _progress = progress;
        _total = inner.CanSeek && inner.Length > 0 ? inner.Length : 0;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _inner.Length;
    public override long Position { get => _inner.Position; set => _inner.Position = value; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        ReportProgress();
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken);
        ReportProgress();
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    private void ReportProgress()
    {
        if (_progress is null || _total <= 0)
            return;
        var fraction = Math.Min(1.0, (double)_inner.Position / _total);
        _progress.Report(fraction);
    }

    public override void Flush() => _inner.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();
        base.Dispose(disposing);
    }
}