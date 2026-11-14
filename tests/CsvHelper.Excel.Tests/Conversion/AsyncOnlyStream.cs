namespace CsvHelper.Excel.Tests.Conversion;

/// <summary>A stream that can only be read asynchronously, as some network streams are.</summary>
internal sealed class AsyncOnlyStream(byte[] bytes) : Stream
{
	private readonly MemoryStream inner = new(bytes, false);

	public override bool CanRead => true;

	public override bool CanSeek => false;

	public override bool CanWrite => false;

	public override long Length => throw new NotSupportedException();

	public override long Position
	{
		get => throw new NotSupportedException();
		set => throw new NotSupportedException();
	}

	public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("The stream can only be read asynchronously.");

	public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Task.FromResult(inner.Read(buffer, offset, count));

#if NET
	public override int Read(Span<byte> buffer) => throw new NotSupportedException("The stream can only be read asynchronously.");

	public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => new(inner.Read(buffer.Span));
#endif

	public override void Flush()
	{
	}

	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

	public override void SetLength(long value) => throw new NotSupportedException();

	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

