using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// The zip package of a workbook being written: a <see cref="ZipArchive"/> in create mode, which writes one entry at a
/// time, straight to the stream, so a stream that can't seek works too. Every entry is compressed at the options' level
/// and dated 1980-01-01, as Excel dates its entries, so a workbook's bytes depend only on what is written.
/// </summary>
/// <remarks>
/// On .NET 10 the asynchronous calls use <c>ZipArchiveEntry.OpenAsync</c> and <c>ZipArchive.DisposeAsync</c>. Before .NET
/// 10, closing an entry and the package writes synchronously.
/// </remarks>
internal sealed class PackageWriter
{
	/// <summary>The date of every entry: the first a zip's date can hold, which Excel's own entries have.</summary>
	private static readonly DateTimeOffset EntryDate = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

	private readonly ZipArchive archive;
	private readonly CompressionLevel level;

	/// <summary>Starts a package.</summary>
	/// <param name="stream">The stream the package is written to, from its position.</param>
	/// <param name="leaveOpen">Whether disposing the package leaves <paramref name="stream"/> open.</param>
	/// <param name="level">The compression of every entry.</param>
	/// <exception cref="ArgumentException"><paramref name="stream"/> can't be written.</exception>
	public PackageWriter(Stream stream, bool leaveOpen, CompressionLevel level)
	{
#if !NET
		// .NET Framework's zip reads the position of a stream that can't seek, which most such streams can't tell; .NET's
		// wraps the stream as this does.
		if (!stream.CanSeek)
		{
			stream = new CountingStream(stream, leaveOpen);
			leaveOpen = false;
		}
#endif
		archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen);
		this.level = level;
	}

	/// <summary>Opens a part's entry, after the entry before it was closed.</summary>
	/// <param name="partName">The part's name, such as <c>xl/worksheets/sheet1.xml</c>.</param>
	/// <param name="async">Whether to open it asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous open.</param>
	/// <returns>A task whose result is the entry's stream, which deflates what is written to it.</returns>
	public ValueTask<Stream> OpenPartAsync(string partName, bool async, CancellationToken cancellationToken)
	{
		var entry = archive.CreateEntry(partName, level);
		entry.LastWriteTime = EntryDate;
#if NET10_0_OR_GREATER
		if (async)
		{
			return new ValueTask<Stream>(entry.OpenAsync(cancellationToken));
		}
#endif

		return new ValueTask<Stream>(entry.Open());
	}

	/// <summary>Writes a part held whole: opens its entry, writes the bytes and closes it.</summary>
	/// <param name="partName">The part's name.</param>
	/// <param name="xml">The part's XML.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when the entry is closed.</returns>
	public async ValueTask WritePartAsync(string partName, XmlOutput xml, bool async, CancellationToken cancellationToken)
	{
		var entry = await OpenPartAsync(partName, async, cancellationToken).ConfigureAwait(false);
		try
		{
			await xml.WriteToAsync(entry, xml.Length, async, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await CloseAsync(entry, async).ConfigureAwait(false);
		}
	}

	/// <summary>Closes an entry's stream, which finishes the entry: its data descriptor, or its header's sizes.</summary>
	/// <param name="entry">The entry's stream.</param>
	/// <param name="async">Whether to close it asynchronously.</param>
	/// <returns>A task that completes when the entry is closed.</returns>
	public static ValueTask CloseAsync(Stream entry, bool async)
	{
		if (async)
		{
			return entry.DisposeAsync();
		}

		entry.Dispose();

		return default;
	}

	/// <summary>
	/// Finishes the package: writes its central directory, and disposes the stream unless it was left open.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the package is finished.</returns>
	public ValueTask DisposeAsync(bool async)
	{
#if NET10_0_OR_GREATER
		if (async)
		{
			return archive.DisposeAsync();
		}
#endif
		archive.Dispose();

		return default;
	}

#if !NET
	/// <summary>
	/// A stream that can't seek, written forward only, which tells its position by counting the bytes written, as .NET's
	/// zip wraps such a stream.
	/// </summary>
	/// <param name="inner">The stream written to.</param>
	/// <param name="leaveOpen">Whether disposing this stream leaves <paramref name="inner"/> open.</param>
	private sealed class CountingStream(Stream inner, bool leaveOpen) : Stream
	{
		private long position;

		/// <inheritdoc/>
		public override bool CanRead => false;

		/// <inheritdoc/>
		public override bool CanSeek => false;

		/// <inheritdoc/>
		public override bool CanWrite => inner.CanWrite;

		/// <inheritdoc/>
		public override long Length => throw new NotSupportedException();

		/// <summary>The number of bytes written. It can't be set.</summary>
		public override long Position
		{
			get => position;
			set => throw new NotSupportedException();
		}

		/// <inheritdoc/>
		public override void Flush() => inner.Flush();

		/// <inheritdoc/>
		public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

		/// <inheritdoc/>
		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void SetLength(long value) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void Write(byte[] buffer, int offset, int count)
		{
			inner.Write(buffer, offset, count);
			position += count;
		}

		/// <inheritdoc/>
		public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			await inner.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
			position += count;
		}

		/// <inheritdoc/>
		public override void WriteByte(byte value)
		{
			inner.WriteByte(value);
			position++;
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			if (disposing && !leaveOpen)
			{
				inner.Dispose();
			}

			base.Dispose(disposing);
		}
	}
#endif
}
