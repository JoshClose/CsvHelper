namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// A stream that a package is written to, over memory, for the zip writer's tests: it records each write's size and
/// whether it was asynchronous, can refuse synchronous calls, and can fail at its nth write, its flush or its dispose.
/// It seeks or not, as made.
/// </summary>
/// <param name="seekable">Whether it seeks.</param>
internal sealed class ZipTestStream(bool seekable = true) : Stream
{
	/// <summary>The bytes written.</summary>
	private readonly MemoryStream inner = new();

	/// <summary>Whether synchronous writes and flushes throw, as some network streams' do.</summary>
	public bool ForbidSync { get; init; }

	/// <summary>The write that throws <see cref="Failure"/>, from 1; it and every write after it throw.</summary>
	public int FailAt { get; init; } = int.MaxValue;

	/// <summary>Whether a flush throws <see cref="Failure"/>.</summary>
	public bool FailFlush { get; init; }

	/// <summary>Whether disposing throws <see cref="Failure"/>.</summary>
	public bool FailDispose { get; init; }

	/// <summary>The exception the stream throws when it fails.</summary>
	public IOException Failure { get; } = new("The test stream failed.");

	/// <summary>Each write that reached the stream, in order: its size, and whether it was asynchronous.</summary>
	public List<(int Size, bool Async)> Writes { get; } = [];

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

	/// <inheritdoc/>
	public override long Position
	{
		get => seekable ? inner.Position : throw new NotSupportedException();
		set => inner.Position = seekable ? value : throw new NotSupportedException();
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
		await Task.Yield();
		AsyncFlushes++;
		if (FailFlush)
		{
			throw Failure;
		}
	}

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override long Seek(long offset, SeekOrigin origin) => seekable ? inner.Seek(offset, origin) : throw new NotSupportedException();

	/// <inheritdoc/>
	public override void SetLength(long value) => throw new NotSupportedException();

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count)
	{
		Take(count, false);
		inner.Write(buffer, offset, count);
	}

	/// <inheritdoc/>
	public override void WriteByte(byte value)
	{
		Take(1, false);
		inner.WriteByte(value);
	}

	/// <inheritdoc/>
	public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		await Task.Yield();
		cancellationToken.ThrowIfCancellationRequested();
		Take(count, true);
		inner.Write(buffer, offset, count);
	}

#if NET
	/// <inheritdoc/>
	public override void Write(ReadOnlySpan<byte> buffer)
	{
		Take(buffer.Length, false);
		inner.Write(buffer);
	}

	/// <inheritdoc/>
	public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
	{
		await Task.Yield();
		cancellationToken.ThrowIfCancellationRequested();
		Take(buffer.Length, true);
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

	/// <summary>Counts a write, refusing it if it is synchronous and those are forbidden, or failing it from <see cref="FailAt"/> on.</summary>
	/// <param name="count">Its size.</param>
	/// <param name="async">Whether it is asynchronous.</param>
	private void Take(int count, bool async)
	{
		if (!async && ForbidSync)
		{
			throw new InvalidOperationException("The stream can only be written asynchronously.");
		}

		Attempts++;
		if (Attempts >= FailAt)
		{
			throw Failure;
		}

		Writes.Add((count, async));
	}
}
