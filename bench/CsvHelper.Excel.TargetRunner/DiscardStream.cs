using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel.TargetRunner;

/// <summary>
/// A stream that counts the bytes written to it and drops them, or keeps them in a buffer, as the benchmarks'
/// <c>CountingStream</c> does. It can't seek, as a response body can't, so the writer streams its package. It tells its
/// position (the bytes written), which .NET Framework's <see cref="System.IO.Compression.ZipArchive"/> reads of a stream
/// that can't seek; CsvHelper.Excel wraps such a stream there itself, so only the zip stage relies on it.
/// </summary>
/// <param name="kept">The buffer the bytes written are kept in, large enough for them all; or none, to drop them.</param>
internal sealed class DiscardStream(byte[]? kept = null) : Stream
{
	/// <summary>Gets the number of bytes written.</summary>
	public long Count { get; private set; }

	/// <inheritdoc/>
	public override bool CanRead => false;

	/// <inheritdoc/>
	public override bool CanSeek => false;

	/// <inheritdoc/>
	public override bool CanWrite => true;

	/// <inheritdoc/>
	public override long Length => throw new NotSupportedException();

	/// <inheritdoc/>
	public override long Position
	{
		get => Count;
		set => throw new NotSupportedException();
	}

	/// <inheritdoc/>
	public override void Flush()
	{
	}

	/// <inheritdoc/>
	public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void SetLength(long value) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count)
	{
		if (kept != null)
		{
			Buffer.BlockCopy(buffer, offset, kept, checked((int)Count), count);
		}

		Count += count;
	}

	/// <inheritdoc/>
	public override void WriteByte(byte value)
	{
		if (kept != null)
		{
			kept[Count] = value;
		}

		Count++;
	}

	/// <inheritdoc/>
	public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		Write(buffer, offset, count);

		return Task.CompletedTask;
	}

#if NET
	/// <inheritdoc/>
	public override void Write(ReadOnlySpan<byte> buffer)
	{
		if (kept != null)
		{
			buffer.CopyTo(kept.AsSpan(checked((int)Count)));
		}

		Count += buffer.Length;
	}

	/// <inheritdoc/>
	public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
	{
		Write(buffer.Span);

		return ValueTask.CompletedTask;
	}
#endif
}
