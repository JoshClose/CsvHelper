namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// A stream that a package is written to, over memory, for the zip writer's tests: it records each write's size, whether
/// it was asynchronous, whether its bytes were a watched array's own and where it started, and each seek; can refuse
/// synchronous or asynchronous calls; can fail at its nth write (with its own exception, or as canceled), its nth seek,
/// its flush or its dispose; and can cancel a token as its nth write starts. It seeks or not, as made.
/// </summary>
/// <param name="seekable">Whether it seeks.</param>
internal sealed class ZipTestStream(bool seekable = true) : Stream
{
	/// <summary>The bytes written.</summary>
	private readonly MemoryStream inner = new();

	/// <summary>Whether synchronous writes and flushes throw, as some network streams' do.</summary>
	public bool ForbidSync { get; init; }

	/// <summary>Whether asynchronous writes and flushes throw, so that a synchronous call's are checked to be synchronous.</summary>
	public bool ForbidAsync { get; init; }

	/// <summary>
	/// Whether asynchronous writes and flushes yield before they run, so that they complete asynchronously; tests of many
	/// small writes turn it off.
	/// </summary>
	public bool Yields { get; init; } = true;

	/// <summary>The source that is canceled as the write <see cref="CancelAt"/> starts.</summary>
	public CancellationTokenSource? Canceled { get; init; }

	/// <summary>
	/// The asynchronous write, from 1 as <see cref="Attempts"/> counts them, as which <see cref="Canceled"/> is canceled:
	/// that write then throws if its token is that source's, before it counts as tried.
	/// </summary>
	public int CancelAt { get; init; } = int.MaxValue;

	/// <summary>The array whose own bytes each write is checked for: not a copy of them, but the array itself.</summary>
	public byte[]? Watched { get; init; }

	/// <summary>The write that throws <see cref="Failure"/>, from 1; it and every write after it throw.</summary>
	public int FailAt { get; init; } = int.MaxValue;

	/// <summary>
	/// Whether the writes from <see cref="FailAt"/> on throw <see cref="Canceling"/> rather than <see cref="Failure"/>,
	/// as a stream canceled by its own means does, whatever the write's token.
	/// </summary>
	public bool FailCanceled { get; init; }

	/// <summary>The seek that throws <see cref="Failure"/>, from 1 as <see cref="Seeks"/> counts them; it and every seek after it throw.</summary>
	public int FailSeekAt { get; init; } = int.MaxValue;

	/// <summary>Whether a flush throws <see cref="Failure"/>.</summary>
	public bool FailFlush { get; init; }

	/// <summary>Whether disposing throws <see cref="Failure"/>.</summary>
	public bool FailDispose { get; init; }

	/// <summary>The exception the stream throws when it fails.</summary>
	public IOException Failure { get; } = new("The test stream failed.");

	/// <summary>The exception the stream throws when its write is canceled (<see cref="FailCanceled"/>).</summary>
	public OperationCanceledException Canceling { get; } = new("The test stream's write was canceled.");

	/// <summary>
	/// Each write that reached the stream, in order: its size, whether it was asynchronous, and whether its bytes were
	/// <see cref="Watched"/>'s own.
	/// </summary>
	public List<(int Size, bool Async, bool Watched)> Writes { get; } = [];

	/// <summary>Where each write of <see cref="Writes"/> started: the stream's position, in order.</summary>
	public List<long> Starts { get; } = [];

	/// <summary>Each seek tried, those that threw included: the position sought, from the stream's start.</summary>
	public List<long> Seeks { get; } = [];

	/// <summary>The writes tried, those that threw included.</summary>
	public int Attempts { get; private set; }

	/// <summary>The synchronous flushes.</summary>
	public int Flushes { get; private set; }

	/// <summary>The asynchronous flushes.</summary>
	public int AsyncFlushes { get; private set; }

	/// <summary>Whether the stream has been disposed.</summary>
	public bool Disposed { get; private set; }

	/// <inheritdoc/>
	public override bool CanRead => false;

	/// <inheritdoc/>
	public override bool CanSeek => seekable && !Disposed;

	/// <inheritdoc/>
	public override bool CanWrite => !Disposed;

	/// <inheritdoc/>
	public override long Length => seekable ? inner.Length : throw new NotSupportedException();

	/// <summary>The position; setting it is a seek (<see cref="Seeks"/>).</summary>
	public override long Position
	{
		get => seekable ? inner.Position : throw new NotSupportedException();
		set => MoveTo(value);
	}

	/// <summary>The bytes written.</summary>
	/// <returns>A copy of them.</returns>
	public byte[] ToArray() => inner.ToArray();

	/// <inheritdoc/>
	public override void Flush()
	{
		if (ForbidSync)
		{
			throw new InvalidOperationException("The stream can only be flushed asynchronously.");
		}

		Flushes++;
		if (FailFlush)
		{
			throw Failure;
		}
	}

	/// <inheritdoc/>
	public override async Task FlushAsync(CancellationToken cancellationToken)
	{
		if (Yields)
		{
			await Task.Yield();
		}

		if (ForbidAsync)
		{
			throw new InvalidOperationException("The stream can only be flushed synchronously.");
		}

		AsyncFlushes++;
		if (FailFlush)
		{
			throw Failure;
		}
	}

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

	/// <summary>Seeks, recording the position sought (<see cref="Seeks"/>), and failing from <see cref="FailSeekAt"/> on.</summary>
	/// <param name="offset">The offset from the origin.</param>
	/// <param name="origin">The origin.</param>
	/// <returns>The new position.</returns>
	public override long Seek(long offset, SeekOrigin origin)
	{
		var position = origin switch
		{
			SeekOrigin.Begin => offset,
			SeekOrigin.Current => inner.Position + offset,
			_ => inner.Length + offset,
		};
		MoveTo(position);

		return position;
	}

	/// <inheritdoc/>
	public override void SetLength(long value) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count)
	{
		Take(count, false, ReferenceEquals(buffer, Watched));
		inner.Write(buffer, offset, count);
	}

	/// <inheritdoc/>
	public override void WriteByte(byte value)
	{
		Take(1, false, false);
		inner.WriteByte(value);
	}

	/// <inheritdoc/>
	public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		if (Yields)
		{
			await Task.Yield();
		}

		Cancel(cancellationToken);
		Take(count, true, ReferenceEquals(buffer, Watched));
		inner.Write(buffer, offset, count);
	}

#if NET
	/// <inheritdoc/>
	public override void Write(ReadOnlySpan<byte> buffer)
	{
		Take(buffer.Length, false, IsWatched(buffer));
		inner.Write(buffer);
	}

	/// <inheritdoc/>
	public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
	{
		if (Yields)
		{
			await Task.Yield();
		}

		Cancel(cancellationToken);
		Take(buffer.Length, true, IsWatched(buffer.Span));
		inner.Write(buffer.Span);
	}
#endif

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		var first = !Disposed;
		Disposed = true;
		base.Dispose(disposing);
		if (first && FailDispose)
		{
			throw Failure;
		}
	}

	/// <summary>Moves to a position, on a stream that seeks: a seek, recorded, failing from <see cref="FailSeekAt"/> on.</summary>
	/// <param name="position">The position, from the stream's start.</param>
	/// <exception cref="NotSupportedException">The stream doesn't seek.</exception>
	private void MoveTo(long position)
	{
		if (!seekable)
		{
			throw new NotSupportedException();
		}

		Seeks.Add(position);
		if (Seeks.Count >= FailSeekAt)
		{
			throw Failure;
		}

		inner.Position = position;
	}

	/// <summary>
	/// Counts a write, refusing it if it is synchronous or asynchronous and those are forbidden, or failing it from
	/// <see cref="FailAt"/> on (as canceled, with <see cref="FailCanceled"/>).
	/// </summary>
	/// <param name="count">Its size.</param>
	/// <param name="async">Whether it is asynchronous.</param>
	/// <param name="watched">Whether its bytes are <see cref="Watched"/>'s own.</param>
	private void Take(int count, bool async, bool watched)
	{
		if (!async && ForbidSync)
		{
			throw new InvalidOperationException("The stream can only be written asynchronously.");
		}

		if (async && ForbidAsync)
		{
			throw new InvalidOperationException("The stream can only be written synchronously.");
		}

		Attempts++;
		if (Attempts >= FailAt)
		{
			throw FailCanceled ? (Exception)Canceling : Failure;
		}

		Writes.Add((count, async, watched));
		Starts.Add(inner.Position);
	}

	/// <summary>
	/// Cancels <see cref="Canceled"/> if this asynchronous write is <see cref="CancelAt"/>, then throws if the write's token
	/// is canceled.
	/// </summary>
	/// <param name="cancellationToken">The write's token.</param>
	/// <exception cref="OperationCanceledException">The token is canceled.</exception>
	private void Cancel(CancellationToken cancellationToken)
	{
		if (Attempts + 1 == CancelAt)
		{
			Canceled?.Cancel();
		}

		cancellationToken.ThrowIfCancellationRequested();
	}

#if NET
	/// <summary>Whether bytes are <see cref="Watched"/>'s own, rather than a copy.</summary>
	/// <param name="bytes">The bytes.</param>
	/// <returns>Whether they lie in <see cref="Watched"/>.</returns>
	private bool IsWatched(ReadOnlySpan<byte> bytes) => Watched is not null && bytes.Overlaps(new ReadOnlySpan<byte>(Watched));
#endif
}
