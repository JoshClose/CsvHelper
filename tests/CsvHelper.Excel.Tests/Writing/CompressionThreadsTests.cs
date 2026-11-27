using System.IO.Compression;
using CsvHelper.Excel.Tests.Package;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// <see cref="ExcelWriterOptions.CompressionThreads"/>: its check and message; -1 as one thread per processor; workbooks
/// (a sheet past many chunks as a table, shared strings, and the lists sheet) with the same bytes at 2, 4 and 16
/// threads and one per processor, whose parts read back as those written with 1; on .NET 8 and later the sheets, the lists
/// sheet and the shared strings chunked and the parts written whole not, a part of one chunk deflated whole at the
/// chunks' level (zlib's level 2 on .NET 10, level 1 on .NET 8), and on the other builds the bytes of 1; stored packages
/// the same at any count; a parallel write's threads used when they are more; every write in the calls' mode; a stream
/// that fails, after which every buffer and segment returns; canceled flushes of a chunked sheet, which keep every row
/// once; and the chunked suite mode reaching the writers.
/// </summary>
public class CompressionThreadsTests
{
	/// <summary>The records of the workbooks' first sheet: enough to pass many chunks of 4,096 bytes.</summary>
	private const int Records = 30_000;

	/// <summary>The dropdown list of the workbooks' second sheet: long enough for the lists sheet to pass a flush.</summary>
	private static readonly string[] ListValues = [.. Enumerable.Range(0, 5_000).Select(i => $"item, {i:D5}")];

	/// <summary>Streams that seek and don't, synchronously and asynchronously.</summary>
	/// <returns>The cases.</returns>
	public static TheoryData<bool, bool> Modes()
	{
		var data = new TheoryData<bool, bool>();
		foreach (var seekable in new[] { true, false })
		{
			data.Add(seekable, false);
			data.Add(seekable, true);
		}

		return data;
	}

	/// <summary>
	/// 0 and counts below -1 throw <see cref="ArgumentOutOfRangeException"/> for the options, with the count and the
	/// message, as the writer is created (to a stream or a file, which isn't created then), and as a conversion and a
	/// template are.
	/// </summary>
	/// <param name="threads">The count.</param>
	[Theory]
	[InlineData(0)]
	[InlineData(-2)]
	[InlineData(int.MinValue)]
	public void RefusesZeroAndCountsBelowMinusOne(int threads)
	{
		var options = new ExcelWriterOptions { CompressionThreads = threads };
		var path = Path.Combine(Path.GetTempPath(), "csvhelper-threads-" + Guid.NewGuid().ToString("N") + ".xlsx");

		var created = Assert.Throws<ArgumentOutOfRangeException>(() => ExcelWorkbookWriter.Create(new MemoryStream(), options));
		var file = Assert.Throws<ArgumentOutOfRangeException>(() => ExcelWorkbookWriter.ToFile(path, options));
		var converted = Assert.Throws<ArgumentOutOfRangeException>(() => ExcelConvert.ToExcel(new MemoryStream([(byte)'a']), new MemoryStream(), new CsvToExcelOptions { Excel = options }));
		var template = Assert.Throws<ArgumentOutOfRangeException>(() => ExcelTemplate.Create<WrittenText>(new MemoryStream(), new ExcelTemplateOptions { Excel = options }));

		foreach (var thrown in new[] { created, file, converted, template })
		{
			Assert.Equal("options", thrown.ParamName);
			Assert.Equal(threads, thrown.ActualValue);
			Assert.StartsWith("ExcelWriterOptions.CompressionThreads must be -1 or at least 1.", thrown.Message);
		}

		Assert.False(File.Exists(path));
	}

	/// <summary>
	/// -1 and counts of 1 or more are taken: the package's zip writer deflates its chunked parts on the count, at most one
	/// thread per processor but never fewer than 2, and -1 on one per processor (raised to the suite mode's,
	/// <see cref="ExcelWorkbookWriter.TestThreads"/>). The option keeps the count it was given. The default is 1.
	/// </summary>
	/// <param name="threads">The count.</param>
	[Theory]
	[InlineData(-1)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(64)]
	[InlineData(int.MaxValue)]
	public void TakesMinusOneAndCountsOfOneOrMore(int threads)
	{
		var options = new ExcelWriterOptions { CompressionThreads = threads };
		using var book = ExcelWorkbookWriter.Create(new MemoryStream(), options);

		var resolved = threads == -1 ? Environment.ProcessorCount : Math.Min(threads, Math.Max(Environment.ProcessorCount, 2));
		Assert.Equal(Math.Max(resolved, ExcelWorkbookWriter.TestThreads), book.Package.Zip.Threads);
		Assert.Equal(threads, options.CompressionThreads);
		Assert.Equal(1, ExcelWriterOptions.Default.CompressionThreads);
		Assert.Equal(1, new ExcelWriterOptions().CompressionThreads);
	}

	/// <summary>
	/// The chunked suite mode (<see cref="TestThreads"/>) reaches the writers: the variable's count is the writers' least
	/// (<see cref="ExcelWorkbookWriter.TestThreads"/>), so that a writer of the default options deflates on it when it is
	/// above 1.
	/// </summary>
	[Fact]
	public void TheSuiteModeSetsEveryWritersThreads()
	{
		using var book = ExcelWorkbookWriter.Create(new MemoryStream());

		Assert.Equal(TestThreads.Count, ExcelWorkbookWriter.TestThreads);
		Assert.Equal(TestThreads.Chunked ? TestThreads.Count : 1, book.Package.Zip.Threads);
	}

	/// <summary>
	/// A sheet whose flushes are canceled as they hand its buffer to a chunked entry: each canceled flush throws, the bytes
	/// handed over all the same, and the sheet's offsets moved with them, a full sheet's end too
	/// (<see cref="ExcelRowLimit.NewSheet"/>); the writing goes on, and the workbook holds every row once, in the sheets of
	/// a write that wasn't canceled.
	/// </summary>
	/// <returns>A task that completes when the test is done.</returns>
	[Fact]
	public async Task KeepsEveryRowOnceWhenAFlushIsCanceled()
	{
		var options = new ExcelWriterOptions
		{
			CompressionThreads = 4,
			RowLimit = ExcelRowLimit.NewSheet,
			Csv = new CsvWriterOptions { HasHeader = false },
			Sheets = new ExcelSheetOptions { AutoFit = 0 },
		};

		var (canceled, workbook) = await WriteRowsAsync(options, true);
		var (none, plain) = await WriteRowsAsync(options with { CompressionThreads = 1 }, false);

		Assert.True(canceled >= 2, $"{canceled} flushes were canceled.");
		Assert.Equal(0, none);
		Assert.Equal(["Data", "Data (2)", "Data (3)"], Written.Read(workbook).Select(s => s.Sheet));
		Assert.Equal(Written.Sheets(plain), Written.Sheets(workbook));
	}

	/// <summary>
	/// A workbook written with 2, 4 or 16 threads, or one per processor, has the same bytes, valid, read by WindowsBase,
	/// with every buffer and segment returned; its parts inflate to those written with 1, and CsvHelper.Excel reads its
	/// rows back. On .NET 8 and later the sheet past many chunks, the lists sheet and the shared strings are chunked, their
	/// data other than 1's, while the parts written whole (relationships, the table, styles, the workbook, the content
	/// types) have 1's data; on the other builds the workbook has 1's bytes.
	/// </summary>
	/// <param name="seekable">Whether the stream seeks.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesTheSameBytesAtAnyCountAboveOne(bool seekable, bool async)
	{
		var one = await WriteWorkbookAsync(new ExcelWriterOptions { CompressionThreads = 1 }, new ZipTestStream(seekable), async);

		var packages = new List<byte[]>();
		foreach (var threads in new[] { 2, 4, 16, -1 })
		{
			packages.Add(await WriteWorkbookAsync(new ExcelWriterOptions { CompressionThreads = threads }, new ZipTestStream(seekable), async));
		}

		foreach (var package in packages)
		{
			Assert.True(packages[0].AsSpan().SequenceEqual(package), "A workbook written with other threads has other bytes.");
		}

		var chunked = packages[0];
		Written.Checked(chunked);
		Assert.Equal(Parts(one).ToList(), Parts(chunked).ToList());
		Assert.Equal(Written.Sheets(one), Written.Sheets(chunked));
		Assert.Equal([("Orders", Records + 1), ("Listed", 2), ("Lists", ListValues.Length)], Written.Read(chunked).Select(s => (s.Sheet, s.Rows.Count)));
#if NET
		string[] streamed = ["xl/worksheets/sheet1.xml", "xl/worksheets/sheet3.xml", "xl/sharedStrings.xml"];
		var whole = ZipStructure.Read(chunked).Entries.Select(e => e.Name).Where(n => !n.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal) && n != "xl/sharedStrings.xml").ToList();
		Assert.Contains("xl/tables/table1.xml", whole);
		Assert.Contains("xl/worksheets/_rels/sheet1.xml.rels", whole);
		foreach (var name in whole)
		{
			Assert.True(Data(one, name).AsSpan().SequenceEqual(Data(chunked, name)), $"{name}, written whole, has other data than with 1 thread.");
		}

		if (!TestThreads.Chunked)
		{
			foreach (var name in streamed)
			{
				Assert.False(Data(one, name).AsSpan().SequenceEqual(Data(chunked, name)), $"{name} has the data of 1 thread: it wasn't chunked.");
			}
		}
#else
		Assert.True(one.AsSpan().SequenceEqual(chunked), "On this build, a workbook written with threads has other bytes than with 1.");
#endif
	}

	/// <summary>
	/// A stored workbook has the same bytes at any count: <see cref="CompressionLevel.NoCompression"/> ignores it.
	/// </summary>
	/// <param name="seekable">Whether the stream seeks.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(Modes))]
	public async Task StoresTheSameBytesAtAnyCount(bool seekable, bool async)
	{
		var one = await WriteWorkbookAsync(new ExcelWriterOptions { CompressionLevel = CompressionLevel.NoCompression }, new ZipTestStream(seekable), async);

		var four = await WriteWorkbookAsync(new ExcelWriterOptions { CompressionLevel = CompressionLevel.NoCompression, CompressionThreads = 4 }, new ZipTestStream(seekable), async);

		Assert.True(one.AsSpan().SequenceEqual(four), "A stored workbook written with threads has other bytes.");
		Written.Checked(four);
	}

#if NET
	/// <summary>
	/// A sheet of 1 MB or less is one chunk, deflated whole as it closes: its data is the sheet's XML deflated by one
	/// <see cref="GZipStream"/> fed in writes of 64 KB, at zlib's level 2 on .NET 10 at <see cref="CompressionLevel.Fastest"/>
	/// (level 1 on .NET 8), and at the level asked for otherwise. With 1 thread it is deflated as it is written, at level 1
	/// on .NET 10, so its data differs there.
	/// </summary>
	/// <param name="level">The compression.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(CompressionLevel.Fastest)]
	[InlineData(CompressionLevel.Optimal)]
	[InlineData(CompressionLevel.SmallestSize)]
	public async Task DeflatesASheetOfOneChunkWhole(CompressionLevel level)
	{
		var options = new ExcelWriterOptions { CompressionLevel = level, CompressionThreads = 2 };
		var package = await WriteWorkbookAsync(options, new ZipTestStream(), false, DeflateSizes.ChunkSize, records: 4_000);

		var name = "xl/worksheets/sheet1.xml";
		var xml = Inflate(Data(package, name));
		Assert.InRange(xml.Length, 100_000, DeflateSizes.ChunkSize);
		Assert.True(OneChunk(xml, level).AsSpan().SequenceEqual(Data(package, name)), "The sheet of one chunk isn't its XML deflated whole at the chunks' level.");
#if NET10_0_OR_GREATER
		if (level == CompressionLevel.Fastest && !TestThreads.Chunked)
		{
			var one = await WriteWorkbookAsync(options with { CompressionThreads = 1 }, new ZipTestStream(), false, DeflateSizes.ChunkSize, records: 4_000);
			Assert.False(Data(one, name).AsSpan().SequenceEqual(Data(package, name)), "With 1 thread, the sheet has the data of level 2.");
		}
#endif
	}
#endif

	/// <summary>
	/// A parallel write's threads, when more than the option's (<see cref="PackageWriter.ParallelThreads"/>, which the
	/// sheet's sink sets as the write begins), chunk the parts opened meanwhile: the workbook has the bytes of one written
	/// with that many compression threads.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task UsesAParallelWritesThreadsWhenMore(bool async)
	{
		var four = await WriteWorkbookAsync(new ExcelWriterOptions { CompressionThreads = 4 }, new ZipTestStream(), async);

		var parallel = await WriteWorkbookAsync(new ExcelWriterOptions(), new ZipTestStream(), async, parallelThreads: 3);
		var fewer = await WriteWorkbookAsync(new ExcelWriterOptions { CompressionThreads = 8 }, new ZipTestStream(), async, parallelThreads: 3);

		Assert.True(four.AsSpan().SequenceEqual(parallel), "A workbook written in a parallel write's threads differs from one written with compression threads.");
		Assert.True(four.AsSpan().SequenceEqual(fewer), "A workbook written with more compression threads than a parallel write's differs.");
#if NET
		if (!TestThreads.Chunked)
		{
			var one = await WriteWorkbookAsync(new ExcelWriterOptions(), new ZipTestStream(), async);
			Assert.False(one.AsSpan().SequenceEqual(parallel), "A parallel write's threads chunked nothing.");
		}
#endif
	}

	/// <summary>
	/// A workbook written with threads through the asynchronous calls to a stream whose synchronous writes and flush throw,
	/// and through the synchronous calls to one whose asynchronous ones throw: every write and flush in the calls' mode, at
	/// every level, to a stream that seeks and to one that doesn't.
	/// </summary>
	/// <param name="seekable">Whether the stream seeks.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesOnlyInTheCallsMode(bool seekable, bool async)
	{
		foreach (var level in ZipPackages.Levels)
		{
			var stream = new ZipTestStream(seekable) { ForbidSync = async, ForbidAsync = !async };

			var package = await WriteWorkbookAsync(new ExcelWriterOptions { CompressionLevel = level, CompressionThreads = 4 }, stream, async);

			Assert.NotEmpty(stream.Writes);
			Assert.All(stream.Writes, w => Assert.Equal(async, w.Async));
			Assert.Equal(async ? (0, 1) : (1, 0), (stream.Flushes, stream.AsyncFlushes));
			Written.Checked(package);
		}
	}

	/// <summary>
	/// A stream that fails under a workbook written with threads, at its first write, in the middle of its sheet and at
	/// its end records: the stream's exception reaches the caller once; writing another sheet and disposing the writer
	/// throw nothing more; the stream is disposed; and every buffer and segment the package held has returned.
	/// </summary>
	/// <param name="point">Where the stream fails.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the test is done.</returns>
	[Theory]
	[InlineData("first write", false)]
	[InlineData("first write", true)]
	[InlineData("middle write", false)]
	[InlineData("middle write", true)]
	[InlineData("last write", false)]
	[InlineData("last write", true)]
	public async Task ReturnsEveryBufferWhenTheStreamFails(string point, bool async)
	{
		var plain = new ZipTestStream(false);
		await WriteFailingAsync(plain, async);
		var writes = plain.Writes.Count;
		var failAt = point switch
		{
			"first write" => 1,
			"middle write" => writes / 2,
			_ => writes,
		};
		var stream = new ZipTestStream(false) { FailAt = failAt };

		var (inside, outside, book) = await WriteFailingAsync(stream, async);

		Assert.Same(stream.Failure, inside ?? outside);
		Assert.True(inside is null || outside is null, $"Two exceptions reached the caller: {inside} and {outside}.");
		Assert.Equal(failAt, stream.Attempts);
		Assert.True(stream.Disposed);
		Assert.Equal((0, 0), (book.Package.Zip.RentedBuffers, book.Package.Zip.HeldSegments));
	}

	/// <summary>
	/// Writes a workbook of the tests: a sheet of <see cref="Records"/> records as a table, its strings shared, then a
	/// sheet of a record with a list long enough for the lists sheet; the chunks of the size given, and a parallel write's
	/// threads as given for the whole write. Every buffer and segment has returned at the end.
	/// </summary>
	/// <param name="options">The options, whose strings are made shared.</param>
	/// <param name="stream">The stream, left open.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="chunkSize">The input a chunk takes.</param>
	/// <param name="parallelThreads">A parallel write's threads, or 0.</param>
	/// <param name="records">The first sheet's records.</param>
	/// <returns>The workbook.</returns>
	private static async Task<byte[]> WriteWorkbookAsync(ExcelWriterOptions options, ZipTestStream stream, bool async, int chunkSize = 4096, int parallelThreads = 0, int records = Records)
	{
		var rows = Enumerable.Range(0, records).Select(i => new WrittenText { Name = $"name {i}", Note = $"note {i * 7919 % 10007}" }).ToArray();
		var second = new ExcelSheetOptions { Columns = new Dictionary<string, ExcelColumnOptions> { ["Name"] = new() { List = ListValues } } };
		var listed = new[] { new WrittenText { Name = "item, 00001", Note = "listed" } };
		var book = ExcelWorkbookWriter.Create(stream, options with { Strings = ExcelStrings.Shared }, leaveOpen: true);
		book.ChunkSize = chunkSize;
		book.Package.ParallelThreads = parallelThreads;
		if (async)
		{
			await using (book)
			{
				var writer = book.AddSheet<WrittenText>("Orders", new ExcelSheetOptions { Table = new() });
				await writer.WriteRecordsAsync(rows);
				await writer.DisposeAsync();
				await book.WriteSheetAsync("Listed", listed, second);
			}
		}
		else
		{
			using (book)
			{
				using (var writer = book.AddSheet<WrittenText>("Orders", new ExcelSheetOptions { Table = new() }))
				{
					writer.WriteRecords(rows);
				}

				book.WriteSheet("Listed", listed, second);
			}
		}

		Assert.Equal((0, 0), (book.Package.Zip.RentedBuffers, book.Package.Zip.HeldSegments));

		return stream.ToArray();
	}

	/// <summary>
	/// Writes a workbook with threads, in chunks of 4,096 bytes, in a <c>using</c> block: a sheet of 20,000 records, whose
	/// exception is caught inside the block, then a small sheet, then the dispose.
	/// </summary>
	/// <param name="stream">The stream.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>The exception caught inside the block, the one the block threw, and the writer.</returns>
	private static async Task<(Exception? Inside, Exception? Outside, ExcelWorkbookWriter Book)> WriteFailingAsync(ZipTestStream stream, bool async)
	{
		var records = Enumerable.Range(0, 20_000).Select(i => new WrittenText { Name = $"name {i}", Note = $"note {i * 7919 % 10007}" }).ToArray();
		var few = new[] { new WrittenText { Name = "a" } };
		var book = ExcelWorkbookWriter.Create(stream, new ExcelWriterOptions { CompressionThreads = 4 });
		book.ChunkSize = 4096;
		Exception? inside = null;
		var outside = await Record(async () =>
		{
			if (async)
			{
				await using (book)
				{
					inside = await Record(() => book.WriteSheetAsync("Orders", records).AsTask());
					await book.WriteSheetAsync("More", few);
				}
			}
			else
			{
				using (book)
				{
					inside = await Record(() =>
					{
						book.WriteSheet("Orders", records);

						return Task.CompletedTask;
					});
					book.WriteSheet("More", few);
				}
			}
		});

		return (inside, outside, book);
	}

	/// <summary>
	/// Writes 25 rows by hand into sheets of 10 rows, asynchronously: the first row flushed at once, which opens the entry
	/// and writes the sheet's start, and each later row ended with a canceled token, or not, whose flushes then throw.
	/// </summary>
	/// <param name="options">The options.</param>
	/// <param name="cancel">Whether to end the rows after the first with a canceled token.</param>
	/// <returns>The flushes that threw, and the workbook.</returns>
	private static async Task<(int Canceled, byte[] Workbook)> WriteRowsAsync(ExcelWriterOptions options, bool cancel)
	{
		var canceled = 0;
		using var stream = new MemoryStream();
		using var source = new CancellationTokenSource();
		if (cancel)
		{
			source.Cancel();
		}

		await using (var book = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true))
		{
			book.MaxRows = 10;
			await using var writer = book.AddSheet("Data");
			for (var i = 0; i < 25; i++)
			{
				writer.WriteField($"row {i}");
				if (i == 0)
				{
					await writer.EndRecordAsync();
					await writer.FlushAsync();

					continue;
				}

				try
				{
					await writer.EndRecordAsync(source.Token);
				}
				catch (OperationCanceledException)
				{
					canceled++;
				}
			}
		}

		return (canceled, Written.Checked(stream.ToArray()));
	}

	/// <summary>A package's parts, inflated, by name in order.</summary>
	/// <param name="package">The package.</param>
	/// <returns>The parts, each as its name and its bytes in base 64.</returns>
	private static IEnumerable<(string Name, string Bytes)> Parts(byte[] package)
	{
		using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);

		return [.. archive.Entries.Select(e => (e.FullName, Convert.ToBase64String(ZipWriterTests.ReadEntry(archive, e.FullName)))).OrderBy(p => p.FullName, StringComparer.Ordinal)];
	}

	/// <summary>An entry's data, as written: deflated or stored.</summary>
	/// <param name="package">The package.</param>
	/// <param name="name">The entry's name.</param>
	/// <returns>The data.</returns>
	private static byte[] Data(byte[] package, string name)
	{
		var entry = ZipStructure.Read(package).Entries.Single(e => e.Name == name);

		return package.AsSpan((int)entry.Local.DataStart, (int)entry.CompressedSize).ToArray();
	}

	/// <summary>Inflates raw deflate data.</summary>
	/// <param name="data">The data.</param>
	/// <returns>The bytes.</returns>
	private static byte[] Inflate(byte[] data)
	{
		using var inflater = new DeflateStream(new MemoryStream(data), CompressionMode.Decompress);
		using var copy = new MemoryStream();
		inflater.CopyTo(copy);

		return copy.ToArray();
	}

#if NET
	/// <summary>
	/// Bytes deflated as one last chunk, made without the library: a <see cref="GZipStream"/> fed in writes of 64 KB, at
	/// zlib's level 2 on .NET 10 at <see cref="CompressionLevel.Fastest"/> and at the level otherwise, disposed, its 10-byte
	/// header and 8-byte trailer dropped.
	/// </summary>
	/// <param name="input">The bytes.</param>
	/// <param name="level">The compression.</param>
	/// <returns>The raw deflate data.</returns>
	private static byte[] OneChunk(byte[] input, CompressionLevel level)
	{
		using var gzip = new MemoryStream();
#if NET10_0_OR_GREATER
		var deflater = level == CompressionLevel.Fastest
			? new GZipStream(gzip, new ZLibCompressionOptions { CompressionLevel = 2, CompressionStrategy = ZLibCompressionStrategy.Default }, leaveOpen: true)
			: new GZipStream(gzip, level, leaveOpen: true);
#else
		var deflater = new GZipStream(gzip, level, leaveOpen: true);
#endif
		using (deflater)
		{
			for (var at = 0; at < input.Length; at += 65536)
			{
				deflater.Write(input, at, Math.Min(65536, input.Length - at));
			}
		}

		var bytes = gzip.ToArray();

		return bytes.AsSpan(10, bytes.Length - 18).ToArray();
	}
#endif

	/// <summary>Runs code, returning the exception it throws.</summary>
	/// <param name="run">The code.</param>
	/// <returns>The exception, or <see langword="null"/>.</returns>
	private static async Task<Exception?> Record(Func<Task> run)
	{
		try
		{
			await run();

			return null;
		}
		catch (Exception e)
		{
			return e;
		}
	}
}
