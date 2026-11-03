using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// The zip archive of a workbook, read with <see cref="ZipArchive"/>: the input, checked by its first bytes, and its entries
/// by part name (<see cref="PartName"/>). Every part is read through a <see cref="LimitedReadStream"/>, which counts its
/// bytes toward the workbook's <see cref="InflateGuard"/>.
/// </summary>
/// <remarks>
/// <para>
/// A path opens a <see cref="FileStream"/> that lets other processes read, write and delete the file, so that a workbook
/// open in Excel can be read, and Excel can still save it: Excel saves to a new file and swaps it in, and this archive
/// goes on reading the copy it opened. A seekable stream is read as it is, from its start whatever its position (as
/// <see cref="ZipArchive"/> reads it). A non-seekable one is copied to a temporary
/// file first, because <see cref="ZipArchive"/> would copy it into memory; the copy counts toward
/// <see cref="PartLimits.MaxUncompressedSize"/>, and the file is deleted when the archive is disposed (on Windows, by the
/// operating system even if the process ends first; elsewhere, as soon as it is open).
/// </para>
/// <para>
/// Before net10.0 the async open reads the zip's central directory synchronously, and parts open synchronously:
/// <see cref="ZipArchive"/> has no async API there. A non-seekable input is still copied asynchronously.
/// </para>
/// </remarks>
internal sealed class PackageArchive : IDisposable, IAsyncDisposable
{
	/// <summary>The bytes the input's first read checks: enough for a compound file's signature.</summary>
	private const int SignatureLength = 8;

	/// <summary>The size of the buffer that copies a non-seekable input.</summary>
	private const int CopyBufferSize = 81920;

	/// <summary>The signature of a zip's local file header, <c>50 4B 03 04</c> (<c>PK</c> and two bytes): the first entry of an archive.</summary>
	private static ReadOnlySpan<byte> LocalFileHeader => [0x50, 0x4B, 0x03, 0x04];

	/// <summary>The signature of a zip's end of central directory, <c>50 4B 05 06</c>: first, it is an archive with no entries.</summary>
	private static ReadOnlySpan<byte> EndOfCentralDirectory => [0x50, 0x4B, 0x05, 0x06];

	/// <summary>The signature of a compound file (OLE2), which encrypted workbooks and <c>.xls</c> workbooks are.</summary>
	private static ReadOnlySpan<byte> CompoundFile => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

	private readonly ZipArchive archive;
	private readonly Stream stream;
	private readonly bool ownsStream;
	private readonly Dictionary<string, ZipArchiveEntry> entries;
	private readonly InflateGuard guard;
	private bool disposed;

	/// <summary>Creates the archive over a stream it can seek.</summary>
	/// <param name="archive">The zip archive.</param>
	/// <param name="stream">The stream the archive reads.</param>
	/// <param name="ownsStream">Whether disposing the archive disposes <paramref name="stream"/>.</param>
	/// <param name="guard">The workbook's byte budget, which counts the copy of a non-seekable input already.</param>
	private PackageArchive(ZipArchive archive, Stream stream, bool ownsStream, InflateGuard guard)
	{
		this.archive = archive;
		this.stream = stream;
		this.ownsStream = ownsStream;
		this.guard = guard;
		entries = new Dictionary<string, ZipArchiveEntry>(PartName.Comparer);
		foreach (var entry in archive.Entries)
		{
			var name = PartName.Normalize(entry.FullName);
			// A folder, not a part. Of two entries with the same part name, the first one counts.
			if (name.Length > 0 && !name.EndsWith("/", StringComparison.Ordinal))
			{
				entries.TryAdd(name, entry);
			}
		}
	}

	/// <summary>The workbook's byte budget.</summary>
	public InflateGuard Guard => guard;

	/// <summary>Whether the archive is disposed: its parts' streams then throw <see cref="ObjectDisposedException"/>.</summary>
	public bool IsDisposed => disposed;

	/// <summary>The names of the package's parts, normalized.</summary>
	public IEnumerable<string> PartNames => entries.Keys;

	/// <summary>Opens the workbook at a path.</summary>
	/// <param name="path">The path of the workbook.</param>
	/// <param name="limits">The limits, for the copy of a non-seekable input.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous open.</param>
	/// <returns>A task whose result is the archive.</returns>
	/// <exception cref="InvalidDataException">The file is not a zip archive, or is an empty one.</exception>
	/// <exception cref="NotSupportedException">The file is a compound file: an encrypted workbook or an <c>.xls</c>.</exception>
	public static async ValueTask<PackageArchive> OpenAsync(string path, PartLimits limits, bool async, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(path);
		var options = async ? FileOptions.Asynchronous : FileOptions.None;
		var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, options);
		try
		{
			return await OpenAsync(file, false, limits, async, cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			await DisposeAsync(file, async).ConfigureAwait(false);

			throw;
		}
	}

	/// <summary>Opens the workbook in a stream.</summary>
	/// <param name="input">The stream of the workbook: seekable, or copied to a temporary file.</param>
	/// <param name="leaveOpen">Whether to leave <paramref name="input"/> open when the archive is disposed.</param>
	/// <param name="limits">The limits, for the copy of a non-seekable input.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous open.</param>
	/// <param name="tempDirectory">The folder of the temporary copy; <see cref="Path.GetTempPath"/> unless a test says otherwise.</param>
	/// <returns>A task whose result is the archive.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="input"/> can't be read.</exception>
	/// <exception cref="InvalidDataException">
	/// The stream is not a zip archive, or is an empty one, or a non-seekable stream is longer than the limit.
	/// </exception>
	/// <exception cref="NotSupportedException">The stream is a compound file: an encrypted workbook or an <c>.xls</c>.</exception>
	public static async ValueTask<PackageArchive> OpenAsync(
		Stream input, bool leaveOpen, PartLimits limits, bool async, CancellationToken cancellationToken, string? tempDirectory = null)
	{
		ArgumentNullException.ThrowIfNull(input);
		if (!input.CanRead)
		{
			throw new ArgumentException("The stream can't be read.", nameof(input));
		}

		var guard = new InflateGuard();
		var signature = new byte[SignatureLength];
		Stream stream;
		bool ownsStream;
		if (input.CanSeek)
		{
			// From the start, wherever the stream is: ZipArchive reads a seekable stream as a whole, from its end and at
			// the offsets the zip gives from its start.
			input.Position = 0;
			var length = await ReadSignatureAsync(input, signature, async, cancellationToken).ConfigureAwait(false);
			input.Position = 0;
			CheckSignature(signature.AsSpan(0, length));
			stream = input;
			ownsStream = !leaveOpen;
		}
		else
		{
			var length = await ReadSignatureAsync(input, signature, async, cancellationToken).ConfigureAwait(false);
			CheckSignature(signature.AsSpan(0, length));
			stream = await CopyToTempFileAsync(input, signature.AsMemory(0, length), guard, limits, async, cancellationToken, tempDirectory).ConfigureAwait(false);
			ownsStream = true;
			if (!leaveOpen)
			{
				await DisposeAsync(input, async).ConfigureAwait(false);
			}
		}

		var copied = stream != input;
		try
		{
#if NET10_0_OR_GREATER
			var archive = async
				? await ZipArchive.CreateAsync(stream, ZipArchiveMode.Read, true, Encoding.UTF8, cancellationToken).ConfigureAwait(false)
				: new ZipArchive(stream, ZipArchiveMode.Read, true, Encoding.UTF8);
#else
			var archive = new ZipArchive(stream, ZipArchiveMode.Read, true, Encoding.UTF8);
#endif

			return new PackageArchive(archive, stream, ownsStream, guard);
		}
		catch
		{
			// Only the copy: the caller's stream is the caller's to dispose when the open fails.
			if (copied)
			{
				await DisposeAsync(stream, async).ConfigureAwait(false);
			}

			throw;
		}
	}

	/// <summary>Whether the package has a part.</summary>
	/// <param name="partName">The part's name, normalized.</param>
	/// <returns>Whether the part is there.</returns>
	public bool Contains(string partName) => entries.ContainsKey(partName);

	/// <summary>
	/// The uncompressed size of a part as the zip declares it, before it is inflated: a hint for sizing, since a zip can
	/// state it wrongly. What a part really inflates to is counted as it is read.
	/// </summary>
	/// <param name="partName">The part's name, normalized.</param>
	/// <returns>The declared size, or 0 when the package has no such part.</returns>
	public long DeclaredSize(string partName) => entries.TryGetValue(partName, out var entry) ? entry.Length : 0;

	/// <summary>
	/// Opens a part for reading. A part held in memory (<paramref name="held"/>) must declare a size within
	/// <see cref="PartLimits.MaxPartSize"/> before it is inflated, and must stay within it as it is read.
	/// </summary>
	/// <param name="partName">The part's name, normalized.</param>
	/// <param name="held">Whether the part is held in memory, rather than streamed as a sheet is.</param>
	/// <param name="limits">The limits of the call that reads the part.</param>
	/// <param name="async">Whether to open the entry asynchronously (on net10.0).</param>
	/// <param name="cancellationToken">Cancels an asynchronous open.</param>
	/// <returns>A task whose result is the part's stream.</returns>
	/// <exception cref="InvalidDataException">The part is missing, or declares a size past the limit, or can't be inflated.</exception>
	/// <exception cref="ObjectDisposedException">The archive is disposed.</exception>
	public ValueTask<LimitedReadStream> OpenPartAsync(string partName, bool held, PartLimits limits, bool async, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		if (!entries.TryGetValue(partName, out var entry))
		{
			throw new InvalidDataException($"The workbook has no part {partName}.");
		}

		if (held && entry.Length > limits.MaxPartSize)
		{
			throw new InvalidDataException(
				$"{partName} is larger than ExcelReaderOptions.MaxPartSize ({limits.MaxPartSize} bytes uncompressed): its size in the zip is {entry.Length} bytes.");
		}

		var maxPartSize = held ? limits.MaxPartSize : long.MaxValue;
#if NET10_0_OR_GREATER
		if (async)
		{
			return OpenEntryAsync(entry, partName, maxPartSize, limits, cancellationToken);
		}
#endif

		return new ValueTask<LimitedReadStream>(new LimitedReadStream(entry.Open(), this, partName, maxPartSize, limits.MaxUncompressedSize));
	}

	/// <summary>Opens a part to read its XML, with a window bounded by <see cref="PartLimits.MaxPartSize"/>. See <see cref="OpenPartAsync"/>.</summary>
	/// <param name="partName">The part's name, normalized.</param>
	/// <param name="held">Whether the part is held in memory, rather than streamed as a sheet is.</param>
	/// <param name="limits">The limits of the call that reads the part.</param>
	/// <param name="async">Whether to open the entry asynchronously (on net10.0).</param>
	/// <param name="cancellationToken">Cancels an asynchronous open.</param>
	/// <returns>A task whose result is the scanner, which owns the part's stream.</returns>
	/// <exception cref="InvalidDataException">The part is missing, or declares a size past the limit, or can't be inflated.</exception>
	/// <exception cref="ObjectDisposedException">The archive is disposed.</exception>
	public async ValueTask<XmlScanner> OpenXmlAsync(string partName, bool held, PartLimits limits, bool async, CancellationToken cancellationToken)
	{
		var stream = await OpenPartAsync(partName, held, limits, async, cancellationToken).ConfigureAwait(false);

		return new XmlScanner(new XmlWindow(stream, partName, limits.MaxPartSize));
	}

#if NET10_0_OR_GREATER
	/// <summary>Opens an entry asynchronously, with <see cref="ZipArchiveEntry.OpenAsync"/>.</summary>
	/// <param name="entry">The entry.</param>
	/// <param name="partName">The part's name.</param>
	/// <param name="maxPartSize">The most bytes the part may inflate to.</param>
	/// <param name="limits">The limits of the call that reads the part.</param>
	/// <param name="cancellationToken">Cancels the open.</param>
	/// <returns>A task whose result is the part's stream.</returns>
	private async ValueTask<LimitedReadStream> OpenEntryAsync(
		ZipArchiveEntry entry, string partName, long maxPartSize, PartLimits limits, CancellationToken cancellationToken)
	{
		var inner = await entry.OpenAsync(cancellationToken).ConfigureAwait(false);

		return new LimitedReadStream(inner, this, partName, maxPartSize, limits.MaxUncompressedSize);
	}

#endif
	/// <summary>Disposes the archive, and the stream it reads when it owns it (a temporary copy is deleted).</summary>
	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		disposed = true;
		archive.Dispose();
		if (ownsStream)
		{
			stream.Dispose();
		}
	}

	/// <summary>Asynchronously disposes the archive, and the stream it reads when it owns it.</summary>
	/// <returns>A task that completes when they are disposed.</returns>
	public async ValueTask DisposeAsync()
	{
		if (disposed)
		{
			return;
		}

		disposed = true;
#if NET10_0_OR_GREATER
		await archive.DisposeAsync().ConfigureAwait(false);
#else
		archive.Dispose();
#endif
		if (ownsStream)
		{
			await stream.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>Reads the first bytes of the input, as many as a signature has, or fewer when the input is shorter.</summary>
	/// <param name="input">The input.</param>
	/// <param name="signature">Where the bytes go.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task whose result is the number of bytes read.</returns>
	private static async ValueTask<int> ReadSignatureAsync(Stream input, byte[] signature, bool async, CancellationToken cancellationToken)
	{
		var length = 0;
		while (length < signature.Length)
		{
			var count = async
				? await input.ReadAsync(signature.AsMemory(length), cancellationToken).ConfigureAwait(false)
				: input.Read(signature, length, signature.Length - length);
			if (count == 0)
			{
				break;
			}

			length += count;
		}

		return length;
	}

	/// <summary>Checks that the input starts as a zip archive with entries does.</summary>
	/// <param name="signature">The first bytes of the input.</param>
	/// <exception cref="InvalidDataException">The input is not a zip archive, or is an empty one.</exception>
	/// <exception cref="NotSupportedException">The input is a compound file: an encrypted workbook or an <c>.xls</c>.</exception>
	private static void CheckSignature(ReadOnlySpan<byte> signature)
	{
		// A local file header: the first entry of an archive.
		if (signature.StartsWith(LocalFileHeader))
		{
			return;
		}

		// The end of the central directory, first: an archive with no entries.
		if (signature.StartsWith(EndOfCentralDirectory))
		{
			throw new InvalidDataException("The input is an empty zip archive, which has no workbook.");
		}

		if (signature.SequenceEqual(CompoundFile))
		{
			throw new NotSupportedException(
				"The input is a compound file, as password-protected workbooks and Excel 97-2003 workbooks (.xls) are. CsvHelper.Excel " +
				"reads .xlsx, .xlsm, .xltx and .xltm workbooks without a password.");
		}

		throw new InvalidDataException("The input is not a workbook: an .xlsx workbook is a zip archive, which starts with 'PK'.");
	}

	/// <summary>Copies a non-seekable input to a temporary file, which is deleted when it is disposed.</summary>
	/// <param name="input">The input, after its first bytes.</param>
	/// <param name="signature">The input's first bytes, already read.</param>
	/// <param name="guard">The workbook's byte budget, which the copy counts toward.</param>
	/// <param name="limits">The limits.</param>
	/// <param name="async">Whether to copy asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous copy.</param>
	/// <param name="tempDirectory">The folder of the file, or <see langword="null"/> for <see cref="Path.GetTempPath"/>.</param>
	/// <returns>A task whose result is the file, at its start.</returns>
	/// <exception cref="InvalidDataException">The input is longer than <see cref="PartLimits.MaxUncompressedSize"/>.</exception>
	private static async ValueTask<FileStream> CopyToTempFileAsync(
		Stream input, ReadOnlyMemory<byte> signature, InflateGuard guard, PartLimits limits, bool async, CancellationToken cancellationToken, string? tempDirectory)
	{
		var windows = IsWindows();
		var path = Path.Combine(tempDirectory ?? Path.GetTempPath(), "CsvHelper.Excel." + Path.GetRandomFileName());
		var options = (windows ? FileOptions.DeleteOnClose : FileOptions.None) | (async ? FileOptions.Asynchronous : FileOptions.None);
		var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, options);
		try
		{
			if (!windows)
			{
				// The open handle keeps the file readable, and nothing is left behind if the process ends.
				File.Delete(path);
			}

			Count(signature.Length);
			await WriteAsync(file, signature, async, cancellationToken).ConfigureAwait(false);
			var buffer = new byte[CopyBufferSize];
			while (true)
			{
				var count = async
					? await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)
					: input.Read(buffer, 0, buffer.Length);
				if (count == 0)
				{
					break;
				}

				Count(count);
				await WriteAsync(file, buffer.AsMemory(0, count), async, cancellationToken).ConfigureAwait(false);
			}

			if (async)
			{
				await file.FlushAsync(cancellationToken).ConfigureAwait(false);
			}
			else
			{
				file.Flush();
			}

			file.Position = 0;

			return file;
		}
		catch
		{
			await DisposeAsync(file, async).ConfigureAwait(false);

			throw;
		}

		void Count(int bytes)
		{
			if (!guard.TryCharge(bytes, limits.MaxUncompressedSize))
			{
				throw new InvalidDataException(
					$"The input stream is longer than ExcelReaderOptions.MaxUncompressedSize ({limits.MaxUncompressedSize} bytes), which bounds its copy to a temporary file too.");
			}
		}
	}

	/// <summary>Writes bytes to a file.</summary>
	/// <param name="file">The file.</param>
	/// <param name="bytes">The bytes.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when the bytes are written.</returns>
	private static ValueTask WriteAsync(FileStream file, ReadOnlyMemory<byte> bytes, bool async, CancellationToken cancellationToken)
	{
		if (async)
		{
			return file.WriteAsync(bytes, cancellationToken);
		}

#if NET || NETSTANDARD2_1
		file.Write(bytes.Span);
#else
		var array = bytes.ToArray();
		file.Write(array, 0, array.Length);
#endif

		return default;
	}

	/// <summary>Disposes a stream, synchronously or not.</summary>
	/// <param name="stream">The stream.</param>
	/// <param name="async">Whether to dispose it asynchronously.</param>
	/// <returns>A task that completes when it is disposed.</returns>
	private static ValueTask DisposeAsync(Stream stream, bool async)
	{
		if (async)
		{
			return stream.DisposeAsync();
		}

		stream.Dispose();

		return default;
	}

	/// <summary>Whether the process runs on Windows, which can delete a file when its last handle closes.</summary>
	/// <returns>Whether it is Windows.</returns>
	private static bool IsWindows()
	{
#if NET
		return OperatingSystem.IsWindows();
#else
		return Environment.OSVersion.Platform == PlatformID.Win32NT;
#endif
	}
}
