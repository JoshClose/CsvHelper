using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// The size limits a call reads parts under, from its options: <c>ExcelReaderOptions.MaxPartSize</c> and
/// <c>ExcelReaderOptions.MaxUncompressedSize</c>.
/// </summary>
internal readonly struct PartLimits
{
	/// <summary>Creates the limits.</summary>
	/// <param name="maxPartSize">The most bytes a part held in memory inflates to, and the largest token or cell of a sheet.</param>
	/// <param name="maxUncompressedSize">The most bytes the workbook inflates to in all, with the copy of a non-seekable input.</param>
	/// <exception cref="ArgumentOutOfRangeException">A limit is 0 or less.</exception>
	public PartLimits(long maxPartSize, long maxUncompressedSize)
	{
		if (maxPartSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxPartSize), maxPartSize, "The limit must be more than 0.");
		}

		if (maxUncompressedSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxUncompressedSize), maxUncompressedSize, "The limit must be more than 0.");
		}

		MaxPartSize = maxPartSize;
		MaxUncompressedSize = maxUncompressedSize;
	}

	/// <summary>The most bytes a part held in memory inflates to, and the largest token or cell of a sheet.</summary>
	public long MaxPartSize { get; }

	/// <summary>The most bytes the workbook inflates to in all, with the copy of a non-seekable input.</summary>
	public long MaxUncompressedSize { get; }
}

/// <summary>
/// The byte budget of one workbook: the bytes inflated from all of its parts, by every call that reads them, and the
/// bytes of a non-seekable input copied to a temporary file. Each call checks the total against its own limit.
/// </summary>
/// <remarks>
/// It counts the bytes actually read, not the sizes the zip declares, because declared sizes can lie: .NET stops
/// inflating an entry at its declared size, which truncates a lying entry, but .NET Framework doesn't. Counting makes
/// every target the same. A workbook is read by one thread at a time, so the count needs no synchronization.
/// </remarks>
internal sealed class InflateGuard
{
	private long total;

	/// <summary>The number of bytes counted so far.</summary>
	public long Total => total;

	/// <summary>Counts bytes of a part toward the total.</summary>
	/// <param name="bytes">The number of bytes.</param>
	/// <param name="limit">The limit of the call that reads them (<see cref="PartLimits.MaxUncompressedSize"/>).</param>
	/// <param name="partName">The part the bytes are from, for the error.</param>
	/// <exception cref="InvalidDataException">The total passes <paramref name="limit"/>.</exception>
	public void Charge(long bytes, long limit, string partName)
	{
		if (!TryCharge(bytes, limit))
		{
			throw new InvalidDataException(
				$"Reading {partName} takes the workbook past ExcelReaderOptions.MaxUncompressedSize ({limit} bytes uncompressed).");
		}
	}

	/// <summary>Counts bytes toward the total, and says whether it is still within the limit.</summary>
	/// <param name="bytes">The number of bytes.</param>
	/// <param name="limit">The limit of the call that reads them (<see cref="PartLimits.MaxUncompressedSize"/>).</param>
	/// <returns>Whether the total is at most <paramref name="limit"/>.</returns>
	public bool TryCharge(long bytes, long limit)
	{
		total += bytes;

		return total <= limit;
	}
}

/// <summary>
/// The stream of one part: counts the bytes read from the part's entry toward the workbook's <see cref="InflateGuard"/>,
/// and, for a part held in memory, toward <see cref="PartLimits.MaxPartSize"/>. It throws when either passes its limit,
/// after at most one read past it. Reading after the workbook is disposed throws <see cref="ObjectDisposedException"/>.
/// </summary>
internal sealed class LimitedReadStream : Stream
{
	private readonly Stream inner;
	private readonly PackageArchive package;
	private readonly string partName;
	private readonly long maxPartSize;
	private readonly long maxUncompressedSize;
	private long read;

	/// <summary>Creates the stream of a part.</summary>
	/// <param name="inner">The entry's stream, which this stream owns.</param>
	/// <param name="package">The package the part is in: its guard, and whether it is disposed.</param>
	/// <param name="partName">The part's name, for errors.</param>
	/// <param name="maxPartSize">The most bytes the part may inflate to; <see cref="long.MaxValue"/> for a streamed sheet.</param>
	/// <param name="maxUncompressedSize">The limit of the workbook's total for this call.</param>
	public LimitedReadStream(Stream inner, PackageArchive package, string partName, long maxPartSize, long maxUncompressedSize)
	{
		this.inner = inner;
		this.package = package;
		this.partName = partName;
		this.maxPartSize = maxPartSize;
		this.maxUncompressedSize = maxUncompressedSize;
	}

	/// <summary>The name of the part.</summary>
	public string PartName => partName;

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

	/// <summary>
	/// Counts bytes that were not inflated toward the workbook's total, such as the empty fields a sheet's gaps make, so
	/// that a small part can't make unbounded work.
	/// </summary>
	/// <param name="bytes">The number of bytes to count.</param>
	/// <exception cref="InvalidDataException">The total passes its limit.</exception>
	public void Charge(long bytes) => package.Guard.Charge(bytes, maxUncompressedSize, partName);

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count)
	{
		CheckOpen();

		return Count(inner.Read(buffer, offset, count));
	}

	/// <inheritdoc/>
	public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		CheckOpen();

		return Count(await inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false));
	}

#if NET || NETSTANDARD2_1
	/// <inheritdoc/>
	public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
	{
		CheckOpen();

		return Count(await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false));
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
		if (disposing)
		{
			inner.Dispose();
		}

		base.Dispose(disposing);
	}

#if NET || NETSTANDARD2_1
	/// <inheritdoc/>
	public override async ValueTask DisposeAsync()
	{
		await inner.DisposeAsync().ConfigureAwait(false);
		await base.DisposeAsync().ConfigureAwait(false);
	}
#endif

	/// <summary>Counts the bytes of a read.</summary>
	/// <param name="count">The number of bytes read.</param>
	/// <returns><paramref name="count"/>.</returns>
	/// <exception cref="InvalidDataException">The part or the workbook passes its limit.</exception>
	private int Count(int count)
	{
		read += count;
		if (read > maxPartSize)
		{
			throw new InvalidDataException($"{partName} is larger than ExcelReaderOptions.MaxPartSize ({maxPartSize} bytes uncompressed).");
		}

		package.Guard.Charge(count, maxUncompressedSize, partName);

		return count;
	}

	/// <summary>Throws when the workbook is disposed.</summary>
	/// <exception cref="ObjectDisposedException">The workbook is disposed.</exception>
	private void CheckOpen()
	{
		if (package.IsDisposed)
		{
			throw new ObjectDisposedException("ExcelWorkbook", "The workbook is disposed.");
		}
	}
}
