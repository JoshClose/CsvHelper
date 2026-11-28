using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// The zip package of a workbook being written, through the library's own zip writer (<see cref="ZipWriter"/>), which
/// writes one entry at a time, forward only, so a stream that can't seek works too. Every entry is deflated at the
/// options' level, or stored at <see cref="CompressionLevel.NoCompression"/>, and dated 1980-01-01, as Excel dates its
/// entries, so a workbook's bytes depend only on what is written, the runtime's deflate, and whether the stream seeks:
/// the same for the same parts, levels, runtime and stream kind, written synchronously or asynchronously.
/// </summary>
/// <remarks>
/// <para>
/// A deflated entry past one buffer is streamed: its CRC and sizes follow its data in a descriptor. A stored entry past
/// one buffer is streamed too on a stream that can't seek; on one that seeks, its CRC and sizes are patched into its
/// local header after its data (one seek there and back), with no descriptor, since readers that check a stored entry's
/// sizes before its data refuse one without them (POI from a stream). Past 4 GiB its sizes don't fit the header, which is
/// then patched to the streamed form, and a descriptor follows. An entry whose sizes or offset need Zip64 has all three in
/// its central Zip64 extra, as readers that take the extra's fields by position (LibreOffice) need.
/// </para>
/// <para>
/// A synchronous call writes the stream only synchronously, and an asynchronous call only asynchronously, on every target:
/// entries are deflated into pooled buffers, and only whole buffers, headers, patches, descriptors and the directory reach
/// the stream; and a stored entry, once past one buffer, writes the call's bytes straight through, in the call's mode.
/// Seeking for a patch is synchronous in either mode, as <see cref="Stream.Seek"/> is.
/// </para>
/// </remarks>
internal sealed class PackageWriter
{
	/// <summary>The zip writer.</summary>
	private readonly ZipWriter zip;

	/// <summary>Starts a package.</summary>
	/// <param name="stream">The stream the package is written to, from its position.</param>
	/// <param name="leaveOpen">Whether disposing the package leaves <paramref name="stream"/> open.</param>
	/// <param name="level">The compression of every entry.</param>
	/// <exception cref="ArgumentException"><paramref name="stream"/> can't be written.</exception>
	public PackageWriter(Stream stream, bool leaveOpen, CompressionLevel level)
		: this(stream, leaveOpen, level, ZipLimits.Default, 1)
	{
	}

	/// <summary>Starts a package whose chunked parts deflate on threads beside the writing thread.</summary>
	/// <param name="stream">The stream the package is written to, from its position.</param>
	/// <param name="leaveOpen">Whether disposing the package leaves <paramref name="stream"/> open.</param>
	/// <param name="level">The compression of every entry.</param>
	/// <param name="threads">The threads that deflate a chunked part (<see cref="ZipWriter.Threads"/>): 1 or more.</param>
	/// <exception cref="ArgumentException"><paramref name="stream"/> can't be written.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="threads"/> is 0 or less.</exception>
	public PackageWriter(Stream stream, bool leaveOpen, CompressionLevel level, int threads)
		: this(stream, leaveOpen, level, ZipLimits.Default, threads)
	{
	}

	/// <summary>Starts a package whose fields overflow into Zip64's at the limits given, which tests lower.</summary>
	/// <param name="stream">The stream the package is written to, from its position.</param>
	/// <param name="leaveOpen">Whether disposing the package leaves <paramref name="stream"/> open.</param>
	/// <param name="level">The compression of every entry.</param>
	/// <param name="limits">Where the package's fields overflow into Zip64's.</param>
	/// <exception cref="ArgumentException"><paramref name="stream"/> can't be written.</exception>
	internal PackageWriter(Stream stream, bool leaveOpen, CompressionLevel level, ZipLimits limits)
		: this(stream, leaveOpen, level, limits, 1)
	{
	}

	/// <summary>Starts a package whose fields overflow into Zip64's at the limits given, with threads for its chunked parts.</summary>
	/// <param name="stream">The stream the package is written to, from its position.</param>
	/// <param name="leaveOpen">Whether disposing the package leaves <paramref name="stream"/> open.</param>
	/// <param name="level">The compression of every entry.</param>
	/// <param name="limits">Where the package's fields overflow into Zip64's.</param>
	/// <param name="threads">The threads that deflate a chunked part (<see cref="ZipWriter.Threads"/>): 1 or more.</param>
	/// <exception cref="ArgumentException"><paramref name="stream"/> can't be written.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="threads"/> is 0 or less.</exception>
	internal PackageWriter(Stream stream, bool leaveOpen, CompressionLevel level, ZipLimits limits, int threads)
	{
		zip = new ZipWriter(stream, leaveOpen, level, limits, threads);
	}

	/// <summary>The zip writer, for tests of its buffers and state.</summary>
	internal ZipWriter Zip => zip;

	/// <summary>
	/// The threads a chunked part takes at least while a parallel write runs on a sheet (<see cref="ZipWriter.ParallelThreads"/>):
	/// set by the sheet's sink as the write begins, and back to 0 as it ends (<see cref="EndParallel"/>); and, as the package
	/// closes, the most a parallel write took (<see cref="UseParallelPeak"/>).
	/// </summary>
	internal int ParallelThreads
	{
		get => zip.ParallelThreads;
		set => zip.ParallelThreads = value;
	}

	/// <summary>
	/// The most threads a parallel write on one of the package's sheets took (<see cref="ParallelThreads"/> as it ended), or 0
	/// when none ran: what the chunked parts written as the package closes deflate on at least (<see cref="UseParallelPeak"/>).
	/// </summary>
	internal int ParallelPeak { get; private set; }

	/// <summary>
	/// A parallel write on a sheet ends, on the writing thread: its threads are kept as <see cref="ParallelPeak"/> when they
	/// are the most so far, and the package's entries take their own threads again (<see cref="ParallelThreads"/> 0).
	/// </summary>
	internal void EndParallel()
	{
		ParallelPeak = Math.Max(ParallelPeak, zip.ParallelThreads);
		zip.ParallelThreads = 0;
	}

	/// <summary>
	/// The parts after the sheets begin, as the package closes: the chunked ones, the lists sheet and the shared strings,
	/// deflate on at least <see cref="ParallelPeak"/> threads. Their XML holds what the sheets wrote, so after a parallel
	/// write they deflate as under <see cref="ExcelWriterOptions.CompressionThreads"/> of its degree, and a package whose
	/// sheets were all written in parallel has the bytes of a serial write at that count.
	/// </summary>
	internal void UseParallelPeak() => zip.ParallelThreads = Math.Max(zip.ParallelThreads, ParallelPeak);

	/// <summary>Opens a part's entry, after the entry before it was closed. It writes nothing yet.</summary>
	/// <param name="partName">The part's name, such as <c>xl/worksheets/sheet1.xml</c>: ASCII.</param>
	/// <param name="async">Whether to open it asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous open.</param>
	/// <returns>A task whose result is the entry's stream, which deflates what is written to it.</returns>
	public ValueTask<Stream> OpenPartAsync(string partName, bool async, CancellationToken cancellationToken) => new(zip.Open(partName));

	/// <summary>
	/// Opens a part's entry, after the entry before it was closed, chunked or not (<see cref="ZipWriter.Open(string, bool)"/>).
	/// It writes nothing yet.
	/// </summary>
	/// <param name="partName">The part's name, such as <c>xl/worksheets/sheet1.xml</c>: ASCII.</param>
	/// <param name="chunked">Whether the part is deflated off the writing thread when the package has threads: a sheet, or the shared strings.</param>
	/// <param name="async">Whether to open it asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous open.</param>
	/// <returns>A task whose result is the entry's stream, which deflates what is written to it.</returns>
	public ValueTask<Stream> OpenPartAsync(string partName, bool chunked, bool async, CancellationToken cancellationToken) => new(zip.Open(partName, chunked));

	/// <summary>Whether an entry takes segments uncopied (<see cref="ZipEntryStream.TakesSegments"/>).</summary>
	/// <param name="entry">The entry's stream, from <see cref="OpenPartAsync(string, bool, bool, CancellationToken)"/>.</param>
	/// <returns>Whether it does.</returns>
	public static bool TakesSegments(Stream entry) => ((ZipEntryStream)entry).TakesSegments;

	/// <summary>Gives an entry a segment of its bytes, which it takes whatever happens (<see cref="ZipEntryStream.AppendAsync"/>).</summary>
	/// <param name="entry">The entry's stream, from <see cref="OpenPartAsync(string, bool, bool, CancellationToken)"/>.</param>
	/// <param name="segment">The bytes.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when the bytes are taken.</returns>
	public static ValueTask AppendAsync(Stream entry, OwnedSegment segment, bool async, CancellationToken cancellationToken) =>
		((ZipEntryStream)entry).AppendAsync(segment, async, cancellationToken);

	/// <summary>Chunks an open entry from here on, as a parallel write on its sheet begins (<see cref="ZipEntryStream.UseChunks"/>).</summary>
	/// <param name="entry">The entry's stream, from <see cref="OpenPartAsync(string, bool, bool, CancellationToken)"/>.</param>
	public static void UseChunks(Stream entry) => ((ZipEntryStream)entry).UseChunks();

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

	/// <summary>
	/// Closes an entry's stream, which finishes the entry: the rest of its data, and its header's sizes (written with the
	/// header, or patched into it) or its descriptor.
	/// </summary>
	/// <param name="entry">The entry's stream, from <see cref="OpenPartAsync(string, bool, CancellationToken)"/> or <see cref="OpenPartAsync(string, bool, bool, CancellationToken)"/>.</param>
	/// <param name="async">Whether to close it asynchronously.</param>
	/// <returns>A task that completes when the entry is closed.</returns>
	public static ValueTask CloseAsync(Stream entry, bool async) => ((ZipEntryStream)entry).CloseAsync(async);

	/// <summary>
	/// Finishes the package: writes its central directory, and disposes the stream unless it was left open. After the
	/// stream has thrown, it writes nothing and throws nothing.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the package is finished.</returns>
	public ValueTask DisposeAsync(bool async) => zip.DisposeAsync(async);
}
