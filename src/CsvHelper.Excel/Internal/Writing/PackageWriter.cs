using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// The zip package of a workbook being written, through the library's own zip writer (<see cref="ZipWriter"/>), which
/// writes one entry at a time, forward only, so a stream that can't seek works too, and the same bytes to any stream.
/// Every entry is deflated at the options' level, or stored at <see cref="CompressionLevel.NoCompression"/>, and dated
/// 1980-01-01, as Excel dates its entries, so a workbook's bytes depend only on what is written and the runtime's deflate.
/// </summary>
/// <remarks>
/// A synchronous call writes the stream only synchronously, and an asynchronous call only asynchronously, on every target:
/// entries are deflated into pooled buffers, and only whole buffers, headers, descriptors and the directory reach the
/// stream; and a stored entry, once past one buffer, writes the call's bytes straight through, in the call's mode.
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
		: this(stream, leaveOpen, level, ZipLimits.Default)
	{
	}

	/// <summary>Starts a package whose fields overflow into Zip64's at the limits given, which tests lower.</summary>
	/// <param name="stream">The stream the package is written to, from its position.</param>
	/// <param name="leaveOpen">Whether disposing the package leaves <paramref name="stream"/> open.</param>
	/// <param name="level">The compression of every entry.</param>
	/// <param name="limits">Where the package's fields overflow into Zip64's.</param>
	/// <exception cref="ArgumentException"><paramref name="stream"/> can't be written.</exception>
	internal PackageWriter(Stream stream, bool leaveOpen, CompressionLevel level, ZipLimits limits)
	{
		zip = new ZipWriter(stream, leaveOpen, level, limits);
	}

	/// <summary>The zip writer, for tests of its buffers and state.</summary>
	internal ZipWriter Zip => zip;

	/// <summary>Opens a part's entry, after the entry before it was closed. It writes nothing yet.</summary>
	/// <param name="partName">The part's name, such as <c>xl/worksheets/sheet1.xml</c>: ASCII.</param>
	/// <param name="async">Whether to open it asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous open.</param>
	/// <returns>A task whose result is the entry's stream, which deflates what is written to it.</returns>
	public ValueTask<Stream> OpenPartAsync(string partName, bool async, CancellationToken cancellationToken) => new(zip.Open(partName));

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
	/// Closes an entry's stream, which finishes the entry: the rest of its data, and its header's sizes or its descriptor.
	/// </summary>
	/// <param name="entry">The entry's stream, from <see cref="OpenPartAsync"/>.</param>
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
