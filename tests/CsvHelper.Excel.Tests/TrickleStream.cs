namespace CsvHelper.Excel.Tests;

/// <summary>
/// A read-only stream over bytes that returns at most <paramref name="chunk"/> bytes per read, so that readers see their
/// input split at every position. Its async reads yield first, so that they complete asynchronously.
/// </summary>
/// <param name="data">The bytes of the stream.</param>
/// <param name="chunk">The most bytes a read returns.</param>
internal sealed class TrickleStream(byte[] data, int chunk = 1) : Stream
{
	private int position;

	/// <summary>The number of bytes read from the stream so far.</summary>
	public int Delivered => position;

	/// <summary>The number of reads, sync and async.</summary>
	public int Reads { get; private set; }

	/// <summary>Whether the stream is disposed.</summary>
	public bool Disposed { get; private set; }

	/// <inheritdoc/>
	public override bool CanRead => true;

	/// <inheritdoc/>
	public override bool CanSeek => false;

	/// <inheritdoc/>
	public override bool CanWrite => false;

	/// <inheritdoc/>
	public override long Length => throw new NotSupportedException();

	/// <inheritdoc/>
	public override long Position
	{
		get => throw new NotSupportedException();
		set => throw new NotSupportedException();
	}

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count) => Take(buffer.AsSpan(offset, count));

	/// <inheritdoc/>
	public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		await Task.Yield();
		cancellationToken.ThrowIfCancellationRequested();

		return Take(buffer.AsSpan(offset, count));
	}

#if NET
	/// <inheritdoc/>
	public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
	{
		await Task.Yield();
		cancellationToken.ThrowIfCancellationRequested();

		return Take(buffer.Span);
	}
#endif

	/// <inheritdoc/>
	public override void Flush()
	{
	}

	/// <inheritdoc/>
	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void SetLength(long value) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		Disposed = true;
		base.Dispose(disposing);
	}

	/// <summary>Copies the next bytes into <paramref name="destination"/>.</summary>
	/// <param name="destination">Where the bytes go.</param>
	/// <returns>The number of bytes copied: 0 at the end.</returns>
	private int Take(Span<byte> destination)
	{
		Reads++;
		var count = Math.Min(Math.Min(destination.Length, chunk), data.Length - position);
		data.AsSpan(position, count).CopyTo(destination);
		position += count;

		return count;
	}
}
