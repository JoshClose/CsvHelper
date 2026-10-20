using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper;

/// <summary>A read-only stream that returns some already-read bytes before continuing with another stream.</summary>
/// <param name="prefix">The bytes already read from <paramref name="inner"/>, returned first.</param>
/// <param name="inner">The stream that the rest of the bytes come from.</param>
/// <param name="ownsInner">Whether disposing this stream disposes <paramref name="inner"/>.</param>
internal sealed class ReplayStream(byte[] prefix, Stream inner, bool ownsInner) : Stream
{
	private int prefixPos;

	/// <summary>Always <see langword="true"/>.</summary>
	public override bool CanRead => true;

	/// <summary>Always <see langword="false"/>: the stream cannot seek.</summary>
	public override bool CanSeek => false;

	/// <summary>Always <see langword="false"/>: the stream is read-only.</summary>
	public override bool CanWrite => false;

	/// <summary>Not supported: the stream cannot seek.</summary>
	/// <exception cref="NotSupportedException">Always.</exception>
	public override long Length => throw new NotSupportedException();

	/// <summary>Not supported: the stream cannot seek.</summary>
	/// <exception cref="NotSupportedException">Always.</exception>
	public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

#if NET
	/// <summary>Reads the replayed bytes first, then bytes of the inner stream. See <see cref="Read(Span{byte})"/>.</summary>
	/// <param name="buffer">The array to read into.</param>
	/// <param name="offset">The position in <paramref name="buffer"/> to read to.</param>
	/// <param name="count">The largest number of bytes to read.</param>
	/// <returns>The number of bytes read; 0 at the end of the inner stream.</returns>
	public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

	/// <summary>
	/// Reads the replayed bytes first, then bytes of the inner stream. A read returns bytes of one or the other,
	/// never both.
	/// </summary>
	/// <param name="buffer">The span to read into.</param>
	/// <returns>The number of bytes read; 0 at the end of the inner stream.</returns>
	public override int Read(Span<byte> buffer)
	{
		if (prefixPos < prefix.Length)
		{
			var n = Math.Min(buffer.Length, prefix.Length - prefixPos);
			prefix.AsSpan(prefixPos, n).CopyTo(buffer);
			prefixPos += n;

			return n;
		}

		return inner.Read(buffer);
	}

	/// <summary>
	/// Reads the replayed bytes first (synchronously), then bytes of the inner stream. A read returns bytes of one or
	/// the other, never both.
	/// </summary>
	/// <param name="buffer">The memory to read into.</param>
	/// <param name="cancellationToken">Cancels the read from the inner stream.</param>
	/// <returns>The number of bytes read; 0 at the end of the inner stream.</returns>
	public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
	{
		if (prefixPos < prefix.Length) return new ValueTask<int>(Read(buffer.Span));

		return inner.ReadAsync(buffer, cancellationToken);
	}

	/// <summary>Reads the replayed bytes first, then bytes of the inner stream. See <see cref="ReadAsync(Memory{byte}, CancellationToken)"/>.</summary>
	/// <param name="buffer">The array to read into.</param>
	/// <param name="offset">The position in <paramref name="buffer"/> to read to.</param>
	/// <param name="count">The largest number of bytes to read.</param>
	/// <param name="cancellationToken">Cancels the read from the inner stream.</param>
	/// <returns>The number of bytes read; 0 at the end of the inner stream.</returns>
	public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
		ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
#else
	/// <summary>
	/// Reads the replayed bytes first, then bytes of the inner stream. A read returns bytes of one or the other,
	/// never both.
	/// </summary>
	/// <param name="buffer">The array to read into.</param>
	/// <param name="offset">The position in <paramref name="buffer"/> to read to.</param>
	/// <param name="count">The largest number of bytes to read.</param>
	/// <returns>The number of bytes read; 0 at the end of the inner stream.</returns>
	public override int Read(byte[] buffer, int offset, int count)
	{
		if (prefixPos < prefix.Length)
		{
			var n = Math.Min(count, prefix.Length - prefixPos);
			Buffer.BlockCopy(prefix, prefixPos, buffer, offset, n);
			prefixPos += n;

			return n;
		}

		return inner.Read(buffer, offset, count);
	}

	/// <summary>
	/// Reads the replayed bytes first (synchronously), then bytes of the inner stream. A read returns bytes of one or
	/// the other, never both.
	/// </summary>
	/// <param name="buffer">The array to read into.</param>
	/// <param name="offset">The position in <paramref name="buffer"/> to read to.</param>
	/// <param name="count">The largest number of bytes to read.</param>
	/// <param name="cancellationToken">Cancels the read from the inner stream.</param>
	/// <returns>The number of bytes read; 0 at the end of the inner stream.</returns>
	public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		if (prefixPos < prefix.Length) return Task.FromResult(Read(buffer, offset, count));

		return inner.ReadAsync(buffer, offset, count, cancellationToken);
	}
#endif

	/// <summary>Does nothing: the stream is read-only.</summary>
	public override void Flush() { }

	/// <summary>Not supported: the stream cannot seek.</summary>
	/// <param name="offset">Not used.</param>
	/// <param name="origin">Not used.</param>
	/// <returns>Never returns.</returns>
	/// <exception cref="NotSupportedException">Always.</exception>
	public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

	/// <summary>Not supported: the stream is read-only.</summary>
	/// <param name="value">Not used.</param>
	/// <exception cref="NotSupportedException">Always.</exception>
	public override void SetLength(long value) => throw new NotSupportedException();

	/// <summary>Not supported: the stream is read-only.</summary>
	/// <param name="buffer">Not used.</param>
	/// <param name="offset">Not used.</param>
	/// <param name="count">Not used.</param>
	/// <exception cref="NotSupportedException">Always.</exception>
	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

	/// <summary>Disposes the inner stream if this stream owns it.</summary>
	/// <param name="disposing">Whether the call comes from <see cref="Stream.Dispose()"/> rather than a finalizer.</param>
	protected override void Dispose(bool disposing)
	{
		if (disposing && ownsInner) inner.Dispose();
		base.Dispose(disposing);
	}
}
