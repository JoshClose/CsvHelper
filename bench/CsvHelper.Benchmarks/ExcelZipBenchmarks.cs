using System.IO.Compression;
using BenchmarkDotNet.Attributes;
using CsvHelper.Excel;

namespace CsvHelper.Benchmarks;

/// <summary>The sheets whose XML <see cref="ExcelZipBenchmarks"/> deflates.</summary>
public enum ZipSheet
{
	/// <summary>The package assets' sheet, as <c>ExcelPackageAssetsWrite.CsvHelper_Write</c> writes it.</summary>
	PackageAssets,

	/// <summary>The orders' sheet, as <c>ExcelOrdersWrite.CsvHelper_Write</c> writes it.</summary>
	Orders,
}

/// <summary>
/// A write's zip stage alone: the sheet XML that CsvHelper.Excel's writer makes for the package assets or the orders,
/// zipped and deflated as a write does it, by the library's own zip writer, and by <see cref="ZipArchive"/>, and deflated
/// in 1 MB chunks on several threads, as Phase 9's parallel deflate will (its decisions 31 to 33), to a stream that counts
/// and drops the bytes.
/// </summary>
/// <remarks>
/// <para>
/// The setup captures the XML: the class's default sheet (what <c>CsvHelper_WriteStored</c> of
/// <c>ExcelPackageAssetsWrite</c> or <c>ExcelOrdersWrite</c> writes) written through <see cref="ExcelWorkbookWriter"/> with
/// stored entries into memory, and <c>xl/worksheets/sheet1.xml</c> read out of it. It is held in memory for the process,
/// about 1.1 GB for the package assets and 0.6 GB for the orders, and never cached on disk: it is the library writer's XML,
/// which Phase 9 changes (sheets without cell references, its step 5), so a file keyed by the workbook generator's sources
/// would serve old XML. A capture costs about a stored write's time, and the package's memory until the XML is read out.
/// </para>
/// <para>
/// Every method writes the XML in 64 KB pieces, as the sheet's writer flushes it, into a <see cref="CountingStream"/> that
/// can't seek, as a response body can't, and returns the bytes written: the compressed size, with the zip's headers for
/// the <see cref="PackageWriter"/> and <see cref="ZipArchive"/> methods. <c>verify ExcelZipBenchmarks</c> prints each
/// method's size (they differ by design, so it flags them as different).
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="ZipArchive_Fastest"/> (the baseline): a <see cref="ZipArchive"/> entry at
/// <see cref="CompressionLevel.Fastest"/>, which the library's own zip writer is measured against.
/// </description></item>
/// <item><description>
/// <see cref="ZipArchive_NoCompression"/>: a stored entry, so CRC-32 and the entry's own work without deflate.
/// </description></item>
/// <item><description>
/// <see cref="PackageWriter_Fastest"/>: the same entry through the library's own zip writer, the internal
/// <see cref="PackageWriter"/>, what a write's zip stage is: one entry, <c>xl/worksheets/sheet1.xml</c>, and the package
/// disposed.
/// </description></item>
/// <item><description>
/// <see cref="PackageWriter_NoCompression"/>: its stored entry, so CRC-32 and the writer's own work without deflate.
/// </description></item>
/// <item><description>
/// <see cref="DeflateStream_Fastest"/>: a bare <see cref="DeflateStream"/> at the same level, so deflate alone; the
/// baseline's time less this one's is what <see cref="ZipArchive"/> adds, and <see cref="PackageWriter_Fastest"/>'s
/// less it what the own writer adds.
/// </description></item>
/// <item><description>
/// <see cref="DeflateStream_Level2"/>: zlib's level 2 through <see cref="ZLibCompressionOptions"/>, the level decision 33
/// gives parallel deflate on .NET 10: about .NET 8's size at <see cref="CompressionLevel.Fastest"/>, for more time than
/// level 1. It is a bare stream because .NET 10's <see cref="ZipArchive"/> takes only <see cref="CompressionLevel"/>'s
/// four values (another value throws <see cref="ArgumentOutOfRangeException"/>), and no zlib level.
/// </description></item>
/// <item><description>
/// <see cref="DeflateChunked_1"/> to <see cref="DeflateChunked_16"/>: the XML cut by byte count into 1 MB chunks, each
/// deflated at <see cref="CompressionLevel.Fastest"/> by a fresh <see cref="DeflateStream"/> on one of 1, 2, 4, 8 or 16
/// threads of its own, which take the chunks in turn. <see cref="DeflateStream.Flush()"/> ends each chunk with a sync flush
/// and the chunk is cut there, but the last, which is closed; written in order, the chunks are one deflate stream. A
/// write would start such threads for each sheet, so each call starts its own.
/// </description></item>
/// <item><description>
/// <see cref="DeflateChunkedLevel2_1"/> to <see cref="DeflateChunkedLevel2_16"/>: the same chunks at zlib's level 2, the
/// level decision 33 gives parallel deflate on .NET 10, so that the base window times what step 9 will ship.
/// </description></item>
/// </list>
/// <para>
/// The chunked methods' setup checks once that the chunks joined, deflated on 16 threads at either level, inflate to the
/// XML byte for byte. Each chunk's output buffer is then kept at the size it needs, since every call deflates the same bytes, so that a
/// call allocates no output buffers. The class runs on the CCD0 job (<see cref="Ccd0Config"/>).
/// </para>
/// </remarks>
[Config(typeof(Ccd0Config))]
[MemoryDiagnoser]
public class ExcelZipBenchmarks
{
	/// <summary>The size of the chunks the chunked methods deflate apart: 1 MB of XML.</summary>
	public const int ChunkSize = 1 << 20;

	/// <summary>The size of each write into a stream: the sheet's writer's flush size (<c>SheetSink.FlushSize</c>).</summary>
	private const int WriteSize = 64 * 1024;

	/// <summary>The sheet's part in the package.</summary>
	private const string SheetPart = "xl/worksheets/sheet1.xml";

	/// <summary>The number of threads the chunked methods' setup checks the join on: the most any method uses.</summary>
	private const int CheckThreads = 16;

	/// <summary>zlib's level 2.</summary>
	private static readonly ZLibCompressionOptions Level2 = new() { CompressionLevel = 2, CompressionStrategy = ZLibCompressionStrategy.Default };

	/// <summary>The sheet's XML, as the writer makes it.</summary>
	private byte[] xml = [];

	/// <summary>Each chunk's deflated bytes, a buffer kept from call to call.</summary>
	private MemoryStream[] deflated = [];

	/// <summary>The length of each chunk's deflated bytes, up to its sync flush.</summary>
	private int[] deflatedLengths = [];

	/// <summary>Gets or sets the sheet whose XML is deflated.</summary>
	[Params(ZipSheet.PackageAssets, ZipSheet.Orders)]
	public ZipSheet Sheet { get; set; }

	/// <summary>Captures the sheet's XML, once: the runners call every setup of the class.</summary>
	[GlobalSetup]
	public void Setup()
	{
		if (xml.Length > 0)
		{
			return;
		}

		xml = Capture(Sheet);
		var count = (xml.Length + ChunkSize - 1) / ChunkSize;
		deflated = new MemoryStream[count];
		for (var i = 0; i < deflated.Length; i++)
		{
			deflated[i] = new MemoryStream();
		}

		deflatedLengths = new int[count];
		Console.WriteLine($"// {Sheet}: {xml.Length:N0} bytes of sheet XML, {count:N0} chunks of 1 MB");
	}

	/// <summary>
	/// Captures the sheet's XML, then checks that the chunks deflated on <see cref="CheckThreads"/> threads and joined
	/// inflate to it, and keeps each chunk's buffer at the size it needs.
	/// </summary>
	/// <exception cref="InvalidOperationException">The joined chunks don't inflate to the XML.</exception>
	[GlobalSetup(Targets = new[] { nameof(DeflateChunked_1), nameof(DeflateChunked_2), nameof(DeflateChunked_4), nameof(DeflateChunked_8), nameof(DeflateChunked_16), nameof(DeflateChunkedLevel2_1), nameof(DeflateChunkedLevel2_2), nameof(DeflateChunkedLevel2_4), nameof(DeflateChunkedLevel2_8), nameof(DeflateChunkedLevel2_16) })]
	public void SetupChunked()
	{
		Setup();

		var capacities = new int[deflated.Length];
		foreach (var level2 in new[] { false, true })
		{
			var size = Check(level2);
			for (var i = 0; i < deflated.Length; i++)
			{
				capacities[i] = Math.Max(capacities[i], (int)deflated[i].Length);
			}

			var level = level2 ? "level 2" : "Fastest";
			Console.WriteLine($"// {Sheet}: {deflated.Length:N0} chunks deflate at {level} to {size:N0} bytes on {CheckThreads} threads, and inflate to the XML");
		}

		for (var i = 0; i < deflated.Length; i++)
		{
			deflated[i].Capacity = capacities[i];
		}
	}

	/// <summary>A <see cref="ZipArchive"/> entry at <see cref="CompressionLevel.Fastest"/>, the baseline.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark(Baseline = true)]
	public long ZipArchive_Fastest() => Archive(CompressionLevel.Fastest);

	/// <summary>A stored <see cref="ZipArchive"/> entry: CRC-32 and the entry's own work, without deflate.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	public long ZipArchive_NoCompression() => Archive(CompressionLevel.NoCompression);

	/// <summary>
	/// An entry of the library's own zip writer (<see cref="PackageWriter"/>) at <see cref="CompressionLevel.Fastest"/>: a
	/// write's zip stage.
	/// </summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	public long PackageWriter_Fastest() => Package(CompressionLevel.Fastest);

	/// <summary>A stored entry of the library's own zip writer: CRC-32 and the writer's own work, without deflate.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	public long PackageWriter_NoCompression() => Package(CompressionLevel.NoCompression);

	/// <summary>A bare <see cref="DeflateStream"/> at <see cref="CompressionLevel.Fastest"/>: deflate alone.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	public long DeflateStream_Fastest()
	{
		var output = new CountingStream(keep: false);
		using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true))
		{
			WriteInPieces(deflate, 0, xml.Length);
		}

		return output.Count;
	}

	/// <summary>A bare <see cref="DeflateStream"/> at zlib's level 2, through <see cref="ZLibCompressionOptions"/>.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	public long DeflateStream_Level2()
	{
		var output = new CountingStream(keep: false);
		using (var deflate = new DeflateStream(output, Level2, leaveOpen: true))
		{
			WriteInPieces(deflate, 0, xml.Length);
		}

		return output.Count;
	}

	/// <summary>The XML deflated in 1 MB chunks on one thread of its own.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	public long DeflateChunked_1() => Chunked(1, new CountingStream(keep: false));

	/// <summary>The XML deflated in 1 MB chunks on 2 threads.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	public long DeflateChunked_2() => Chunked(2, new CountingStream(keep: false));

	/// <summary>The XML deflated in 1 MB chunks on 4 threads.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	public long DeflateChunked_4() => Chunked(4, new CountingStream(keep: false));

	/// <summary>The XML deflated in 1 MB chunks on 8 threads.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	public long DeflateChunked_8() => Chunked(8, new CountingStream(keep: false));

	/// <summary>The XML deflated in 1 MB chunks on 16 threads.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	public long DeflateChunked_16() => Chunked(16, new CountingStream(keep: false));

	/// <summary>The XML deflated in 1 MB chunks at zlib's level 2 on one thread of its own.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	public long DeflateChunkedLevel2_1() => Chunked(1, new CountingStream(keep: false), level2: true);

	/// <summary>The XML deflated in 1 MB chunks at zlib's level 2 on 2 threads.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	public long DeflateChunkedLevel2_2() => Chunked(2, new CountingStream(keep: false), level2: true);

	/// <summary>The XML deflated in 1 MB chunks at zlib's level 2 on 4 threads.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	public long DeflateChunkedLevel2_4() => Chunked(4, new CountingStream(keep: false), level2: true);

	/// <summary>The XML deflated in 1 MB chunks at zlib's level 2 on 8 threads.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	public long DeflateChunkedLevel2_8() => Chunked(8, new CountingStream(keep: false), level2: true);

	/// <summary>The XML deflated in 1 MB chunks at zlib's level 2 on 16 threads.</summary>
	/// <returns>The bytes written.</returns>
	[Benchmark]
	public long DeflateChunkedLevel2_16() => Chunked(16, new CountingStream(keep: false), level2: true);

	/// <summary>
	/// Deflates the XML in chunks on <see cref="CheckThreads"/> threads at a level, and checks that the chunks joined
	/// inflate to it byte for byte.
	/// </summary>
	/// <param name="level2">Whether the chunks are deflated at zlib's level 2 rather than <see cref="CompressionLevel.Fastest"/>.</param>
	/// <returns>The joined chunks' size.</returns>
	/// <exception cref="InvalidOperationException">The joined chunks don't inflate to the XML.</exception>
	private long Check(bool level2)
	{
		var output = new CountingStream(keep: true);
		var size = Chunked(CheckThreads, output, level2);
		using var inflate = new DeflateStream(new MemoryStream(output.Kept, writable: false), CompressionMode.Decompress);
		var block = new byte[ChunkSize];
		var at = 0L;
		int read;
		while ((read = inflate.Read(block, 0, block.Length)) > 0)
		{
			if (at + read > xml.Length || !block.AsSpan(0, read).SequenceEqual(xml.AsSpan((int)at, read)))
			{
				throw new InvalidOperationException($"The joined chunks don't inflate to the sheet's XML: they differ from byte {at:N0} on.");
			}

			at += read;
		}

		if (at != xml.Length)
		{
			throw new InvalidOperationException($"The joined chunks inflate to {at:N0} bytes, not the XML's {xml.Length:N0}.");
		}

		return size;
	}

	/// <summary>
	/// Writes a sheet's workbook through <see cref="ExcelWorkbookWriter"/> with stored entries, as the write classes'
	/// <c>CsvHelper_WriteStored</c> does, and reads its sheet's XML out of it.
	/// </summary>
	/// <param name="sheet">The sheet.</param>
	/// <returns>The XML.</returns>
	/// <exception cref="InvalidOperationException">The package has no <see cref="SheetPart"/>.</exception>
	private static byte[] Capture(ZipSheet sheet)
	{
		var stored = new ExcelWriterOptions { CompressionLevel = CompressionLevel.NoCompression };
		using var package = new PackageBuffer();
		if (sheet == ZipSheet.PackageAssets)
		{
			var records = ExcelDataGenerator.PackageAssetRecords(ExcelDataGenerator.PackageAssetRows);
			using (var workbook = ExcelWorkbookWriter.Create(package, stored, leaveOpen: true))
			using (var writer = workbook.AddSheet(ExcelDataGenerator.SheetName))
			{
				writer.WriteHeader(records[0]);
				for (var r = 1; r < records.Count; r++)
				{
					var row = records[r];
					for (var i = 0; i < row.Length; i++)
					{
						writer.WriteField(row[i]);
					}

					writer.EndRecord();
				}
			}
		}
		else
		{
			var orders = ExcelOrders.Generate(ExcelDataGenerator.OrderRows);
			using (var workbook = ExcelWorkbookWriter.Create(package, stored with { DateTimeOffsets = ExcelDateTimeOffsets.Utc }, leaveOpen: true))
			{
				workbook.WriteSheet(ExcelOrders.SheetName, orders);
			}
		}

		package.Seal();
		using var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
		var entry = zip.GetEntry(SheetPart) ?? throw new InvalidOperationException($"The workbook has no {SheetPart}.");
		var bytes = GC.AllocateUninitializedArray<byte>(checked((int)entry.Length));
		using (var input = entry.Open())
		{
			input.ReadExactly(bytes);
		}

		return bytes;
	}

	/// <summary>Writes the XML into a <see cref="ZipArchive"/> entry, to a stream that can't seek.</summary>
	/// <param name="level">The entry's level.</param>
	/// <returns>The bytes written.</returns>
	private long Archive(CompressionLevel level)
	{
		var output = new CountingStream(keep: false);
		using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
		{
			var entry = zip.CreateEntry(SheetPart, level);
			using var stream = entry.Open();
			WriteInPieces(stream, 0, xml.Length);
		}

		return output.Count;
	}

	/// <summary>
	/// Writes the XML into an entry of the library's own zip writer (<see cref="PackageWriter"/>), synchronously, as the
	/// sheet's writer does, to a stream that can't seek, and disposes the package, which writes its central directory.
	/// </summary>
	/// <param name="level">The entry's level.</param>
	/// <returns>The bytes written.</returns>
	private long Package(CompressionLevel level)
	{
		var output = new CountingStream(keep: false);
		var package = new PackageWriter(output, leaveOpen: true, level);
		var entry = SyncOrAsync.Result(package.OpenPartAsync(SheetPart, false, CancellationToken.None));
		WriteInPieces(entry, 0, xml.Length);
		SyncOrAsync.Wait(PackageWriter.CloseAsync(entry, false));
		SyncOrAsync.Wait(package.DisposeAsync(false));

		return output.Count;
	}

	/// <summary>
	/// Deflates the XML in <see cref="ChunkSize"/> chunks on threads of their own, each chunk by a fresh
	/// <see cref="DeflateStream"/> ended by a sync flush (the last one closed), and writes the chunks in order.
	/// </summary>
	/// <param name="threads">The number of threads.</param>
	/// <param name="output">The stream the chunks are written to.</param>
	/// <param name="level2">Whether the chunks are deflated at zlib's level 2 rather than <see cref="CompressionLevel.Fastest"/>.</param>
	/// <returns>The bytes written.</returns>
	private long Chunked(int threads, CountingStream output, bool level2 = false)
	{
		var next = -1;

		void Deflate()
		{
			int i;
			while ((i = Interlocked.Increment(ref next)) < deflated.Length)
			{
				var offset = i * ChunkSize;
				var last = i == deflated.Length - 1;
				var chunk = deflated[i];
				chunk.SetLength(0);
				using (var deflate = level2 ? new DeflateStream(chunk, Level2, leaveOpen: true) : new DeflateStream(chunk, CompressionLevel.Fastest, leaveOpen: true))
				{
					WriteInPieces(deflate, offset, Math.Min(ChunkSize, xml.Length - offset));
					if (!last)
					{
						// A sync flush ends the chunk's bits on a byte, with an empty stored block; the final block that
						// closing the stream writes after it is cut off.
						deflate.Flush();
						deflatedLengths[i] = (int)chunk.Length;
					}
				}

				if (last)
				{
					deflatedLengths[i] = (int)chunk.Length;
				}
			}
		}

		var workers = new Thread[threads];
		for (var t = 0; t < workers.Length; t++)
		{
			workers[t] = new Thread(Deflate) { IsBackground = true };
			workers[t].Start();
		}

		foreach (var worker in workers)
		{
			worker.Join();
		}

		for (var i = 0; i < deflated.Length; i++)
		{
			output.Write(deflated[i].GetBuffer(), 0, deflatedLengths[i]);
		}

		return output.Count;
	}

	/// <summary>Writes part of the XML into a stream in <see cref="WriteSize"/> pieces.</summary>
	/// <param name="stream">The stream.</param>
	/// <param name="offset">Where the part starts.</param>
	/// <param name="count">The part's length.</param>
	private void WriteInPieces(Stream stream, int offset, int count)
	{
		var end = offset + count;
		for (var at = offset; at < end; at += WriteSize)
		{
			stream.Write(xml, at, Math.Min(WriteSize, end - at));
		}
	}

	/// <summary>
	/// A package written into memory in blocks, so that its gigabyte is never copied to grow: write-only and unseekable
	/// while the workbook's writer writes, as <see cref="CountingStream"/> is, then, once sealed, readable and seekable for
	/// <see cref="ZipArchive"/>.
	/// </summary>
	private sealed class PackageBuffer : Stream
	{
		/// <summary>The size of each block.</summary>
		private const int BlockSize = 4 << 20;

		/// <summary>The blocks, each full but the last.</summary>
		private readonly List<byte[]> blocks = [];

		/// <summary>The number of bytes written.</summary>
		private long length;

		/// <summary>Where the next read starts, once sealed.</summary>
		private long position;

		/// <summary>Whether the writes are over and the bytes can be read.</summary>
		private bool isSealed;

		/// <inheritdoc/>
		public override bool CanRead => isSealed;

		/// <inheritdoc/>
		public override bool CanSeek => isSealed;

		/// <inheritdoc/>
		public override bool CanWrite => !isSealed;

		/// <inheritdoc/>
		public override long Length => isSealed ? length : throw new NotSupportedException();

		/// <inheritdoc/>
		public override long Position
		{
			get => isSealed ? position : throw new NotSupportedException();
			set => position = isSealed ? value : throw new NotSupportedException();
		}

		/// <summary>Ends the writes: the bytes can then be read, from the start.</summary>
		public void Seal()
		{
			isSealed = true;
			position = 0;
		}

		/// <inheritdoc/>
		public override void Flush()
		{
		}

		/// <inheritdoc/>
		public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

		/// <inheritdoc/>
		public override int Read(Span<byte> buffer)
		{
			if (!isSealed)
			{
				throw new NotSupportedException();
			}

			var read = 0;
			while (read < buffer.Length && position < length)
			{
				var at = (int)(position % BlockSize);
				var count = (int)Math.Min(Math.Min(buffer.Length - read, BlockSize - at), length - position);
				blocks[(int)(position / BlockSize)].AsSpan(at, count).CopyTo(buffer[read..]);
				read += count;
				position += count;
			}

			return read;
		}

		/// <inheritdoc/>
		public override long Seek(long offset, SeekOrigin origin)
		{
			if (!isSealed)
			{
				throw new NotSupportedException();
			}

			position = origin switch
			{
				SeekOrigin.Begin => offset,
				SeekOrigin.Current => position + offset,
				SeekOrigin.End => length + offset,
				_ => throw new ArgumentOutOfRangeException(nameof(origin)),
			};

			return position;
		}

		/// <inheritdoc/>
		public override void SetLength(long value) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

		/// <inheritdoc/>
		public override void Write(ReadOnlySpan<byte> buffer)
		{
			if (isSealed)
			{
				throw new NotSupportedException();
			}

			while (!buffer.IsEmpty)
			{
				var at = (int)(length % BlockSize);
				if (at == 0)
				{
					blocks.Add(GC.AllocateUninitializedArray<byte>(BlockSize));
				}

				var count = Math.Min(buffer.Length, BlockSize - at);
				buffer[..count].CopyTo(blocks[^1].AsSpan(at));
				buffer = buffer[count..];
				length += count;
			}
		}
	}
}
