using System;
using System.Buffers;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// The stream the serial read of a sheet goes on with when <see cref="SegmentPipeline"/> stops part way: the part's bytes from
/// a segment's start (<see cref="Offset"/>) on, in order. First the bytes the pipeline inflated from there (the segments,
/// and those its inflating thread read past its last cut), then the part's stream; or, where the inflating thread's read
/// failed, that read's exception. A read-only, forward-only stream, which <see cref="XmlWindow"/> reads after
/// <c>XmlWindow.Restart</c>.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline read its bytes uncounted (<see cref="LimitedReadStream.ReadUncounted"/>), so each read here charges the
/// bytes it returns (<see cref="LimitedReadStream.Charge"/>, which throws past the limit, after the read, as the part's own
/// reads do), but for those the serial window had counted already before the pipeline began. The part's stream counts its
/// own. Disposing charges the inflated bytes no read reached (<see cref="LimitedReadStream.TryCharge"/>), so each inflated
/// byte is counted once.
/// </para>
/// <para>
/// It owns the part's stream and the buffers of its pieces: each buffer goes back to the pool once read, and the rest when
/// the stream is disposed, which disposes the part's stream too.
/// </para>
/// </remarks>
internal sealed class SegmentStream : Stream
{
	private readonly LimitedReadStream part;
	private readonly ArrayPool<byte> pool;
	private readonly Piece[] pieces;
	private readonly long offset;
	private readonly ExceptionDispatchInfo? error;
	private readonly bool partEnded;
	private int index;
	private int position;
	private bool disposed;

	/// <summary>Creates the stream.</summary>
	/// <param name="part">The part's stream, read on after the pieces, which this stream owns.</param>
	/// <param name="pool">The pool the pieces' buffers go back to.</param>
	/// <param name="pieces">The bytes inflated from <paramref name="offset"/> on, in order, which this stream owns.</param>
	/// <param name="offset">The part's offset of the first piece's first byte.</param>
	/// <param name="error">The exception of the read that failed after the pieces, or <see langword="null"/>.</param>
	/// <param name="partEnded">Whether the part's stream ended after the pieces: nothing follows them.</param>
	public SegmentStream(LimitedReadStream part, ArrayPool<byte> pool, Piece[] pieces, long offset, ExceptionDispatchInfo? error, bool partEnded)
	{
		this.part = part;
		this.pool = pool;
		this.pieces = pieces;
		this.offset = offset;
		this.error = error;
		this.partEnded = partEnded;
	}

	/// <summary>The part's offset of the stream's first byte: where the serial read resumes (<c>SheetScanner.ResumeAt</c>).</summary>
	public long Offset => offset;

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
	public override int Read(byte[] buffer, int offset, int count)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		if (TryTake(buffer.AsSpan(offset, count), out var taken))
		{
			return taken;
		}

		error?.Throw();

		return partEnded ? 0 : part.Read(buffer, offset, count);
	}

	/// <inheritdoc/>
	public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ObjectDisposedException.ThrowIf(disposed, this);
		if (TryTake(buffer.AsSpan(offset, count), out var taken))
		{
			return taken;
		}

		error?.Throw();

		return partEnded ? 0 : await part.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
	}

#if NET || NETSTANDARD2_1
	/// <inheritdoc/>
	public override int Read(Span<byte> buffer)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		if (TryTake(buffer, out var taken))
		{
			return taken;
		}

		error?.Throw();

		return partEnded ? 0 : part.Read(buffer);
	}

	/// <inheritdoc/>
	public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		ObjectDisposedException.ThrowIf(disposed, this);
		if (TryTake(buffer.Span, out var taken))
		{
			return taken;
		}

		error?.Throw();

		return partEnded ? 0 : await part.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
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
		if (disposing && Release())
		{
			part.Dispose();
		}

		base.Dispose(disposing);
	}

#if NET || NETSTANDARD2_1
	/// <inheritdoc/>
	public override async ValueTask DisposeAsync()
	{
		if (Release())
		{
			await part.DisposeAsync().ConfigureAwait(false);
		}

		await base.DisposeAsync().ConfigureAwait(false);
	}
#endif

	/// <summary>Copies the next bytes of the pieces, and charges those not counted already.</summary>
	/// <param name="destination">Where the bytes go.</param>
	/// <param name="taken">The number of bytes copied.</param>
	/// <returns>Whether a piece had bytes to copy, or <paramref name="destination"/> is empty; otherwise the pieces are all read.</returns>
	/// <exception cref="InvalidDataException">The bytes take the workbook past its limit; they are copied, and read.</exception>
	private bool TryTake(Span<byte> destination, out int taken)
	{
		if (destination.IsEmpty)
		{
			taken = 0;

			return true;
		}

		while (index < pieces.Length)
		{
			var piece = pieces[index];
			if (position < piece.Length)
			{
				var start = position;
				taken = Math.Min(destination.Length, piece.Length - start);
				piece.Buffer.AsSpan(start, taken).CopyTo(destination);
				position += taken;
				if (position == piece.Length)
				{
					NextPiece();
				}

				// The bytes before Counted were counted by the serial window, before the pipeline began.
				var uncounted = start + taken - Math.Max(start, piece.Counted);
				if (uncounted > 0)
				{
					part.Charge(uncounted);
				}

				return true;
			}

			NextPiece();
		}

		taken = 0;

		return false;
	}

	/// <summary>Returns the current piece's buffer, which is all read, and moves to the next piece.</summary>
	private void NextPiece()
	{
		var buffer = pieces[index].Buffer;
		pieces[index] = default;
		index++;
		position = 0;
		if (buffer is { Length: > 0 })
		{
			pool.Return(buffer);
		}
	}

	/// <summary>Marks the stream disposed, charges the inflated bytes no read reached, and returns the pieces' buffers.</summary>
	/// <returns>Whether the stream was not disposed already.</returns>
	private bool Release()
	{
		if (disposed)
		{
			return false;
		}

		disposed = true;
		var unread = 0L;
		for (var i = index; i < pieces.Length; i++)
		{
			var piece = pieces[i];
			var from = Math.Max(i == index ? position : 0, piece.Counted);
			unread += Math.Max(piece.Length - from, 0);
			pieces[i] = default;
			if (piece.Buffer is { Length: > 0 })
			{
				pool.Return(piece.Buffer);
			}
		}

		if (unread > 0)
		{
			part.TryCharge(unread);
		}

		return true;
	}

	/// <summary>Bytes the pipeline inflated, in a buffer of its pool.</summary>
	/// <param name="buffer">The buffer, which the stream returns to the pool.</param>
	/// <param name="length">The number of bytes in it.</param>
	/// <param name="counted">The bytes at its start that the serial window counted already: those it carried into the pipeline.</param>
	internal readonly struct Piece(byte[] buffer, int length, int counted)
	{
		/// <summary>The buffer, which the stream returns to the pool.</summary>
		public byte[] Buffer => buffer;

		/// <summary>The number of bytes in it.</summary>
		public int Length => length;

		/// <summary>The bytes at its start that the serial window counted already.</summary>
		public int Counted => counted;
	}
}
