using System.IO.Compression;
using CsvHelper.Excel.Tests.Writing;

namespace CsvHelper.Excel.Tests.Api;

/// <summary>
/// <see cref="ExcelWriterOptions.CompressionThreads"/> through the public API (Phase 9's step 9): 1 by default; 0 and
/// less than -1 refused when the writer opens, with the option's message; -1 one thread per processor, into both ways of
/// opening a workbook. On every target, a workbook written at any count has the parts it has at 1, synchronously and
/// asynchronously, into a stream that seeks and one that doesn't, its sheets, shared strings and lists sheet included. On
/// net8.0 and net10.0 its bytes are the same at every count above 1, which cuts the streamed parts into chunks deflated on
/// threads, and differ from 1's; on .NET Framework, whose deflater doesn't flush, they are 1's, deflated by one thread
/// beside the writing one. <see cref="CompressionLevel.NoCompression"/> takes no threads. Every buffer and segment returns.
/// </summary>
/// <remarks>
/// The workbooks have a sheet of 6,000 orders, about 1.8 MB of XML, so that the default chunk of 1 MB cuts it; most tests
/// cut chunks of 4 KB (<see cref="ExcelWorkbookWriter.ChunkSize"/>), so that every streamed part has several. When the
/// suites run chunked (<see cref="ExcelWorkbookWriter.TestThreads"/>), a count of 1 deflates on threads too, and what tells
/// 1 apart from more isn't checked.
/// </remarks>
public class CompressionThreadsApiTests
{
	/// <summary>The option's message, which an exception's message starts with.</summary>
	private const string Refused = "ExcelWriterOptions.CompressionThreads must be -1 or at least 1.";

	/// <summary>The chunk the tests cut, but those of the default.</summary>
	private const int SmallChunks = 4096;

	/// <summary>The orders of the big sheet.</summary>
	private static readonly ParallelOrder[] Orders = ParallelWrites.Orders(6000);

	/// <summary>Orders of a validated sheet, whose region's list goes in the lists sheet, a streamed part of its own.</summary>
	private static readonly ValidatedOrder[] Validated =
	[
		.. Enumerable.Range(0, 200).Select(i => new ValidatedOrder
		{
			Id = i + 1,
			Status = (TicketStatus)(i % 3),
			When = new DateTime(2024, 1, 15).AddHours(i),
			Region = i % 2 == 0 ? "North, East" : "South, West",
			Note = "note " + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
		}),
	];

	/// <summary>A count above 1, the strings, the mode and whether the stream seeks.</summary>
	public static TheoryData<int, ExcelStrings, bool, bool> Writes => new()
	{
		{ 2, ExcelStrings.Inline, false, true },
		{ 4, ExcelStrings.Shared, true, true },
		{ 16, ExcelStrings.Inline, true, false },
		{ -1, ExcelStrings.Shared, false, false },
		{ 4, ExcelStrings.Inline, false, false },
	};

	[Fact]
	public void IsOneByDefault()
	{
		Assert.Equal(1, ExcelWriterOptions.Default.CompressionThreads);
		Assert.Equal(1, new ExcelWriterOptions().CompressionThreads);
		Assert.Equal(4, (ExcelWriterOptions.Default with { CompressionThreads = 4 }).CompressionThreads);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-2)]
	[InlineData(int.MinValue)]
	public void RefusesZeroAndLessThanMinusOne(int threads)
	{
		var options = new ExcelWriterOptions { CompressionThreads = threads };

		var e = Assert.Throws<ArgumentOutOfRangeException>(() => ExcelWorkbookWriter.Create(new MemoryStream(), options));
		Assert.Equal("options", e.ParamName);
		Assert.StartsWith(Refused, e.Message, StringComparison.Ordinal);
		Assert.Equal(threads, e.ActualValue);

		// A file isn't created for options that are refused.
		var path = Path.Combine(Path.GetTempPath(), $"CsvHelper.Excel.Tests.{Guid.NewGuid():N}.xlsx");
		var file = Assert.Throws<ArgumentOutOfRangeException>(() => ExcelWorkbookWriter.ToFile(path, options));
		Assert.Equal("options", file.ParamName);
		Assert.StartsWith(Refused, file.Message, StringComparison.Ordinal);
		Assert.False(File.Exists(path));
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(64)]
	[InlineData(int.MaxValue)]
	public void TakesMinusOneAndEveryCountAboveZero(int threads)
	{
		// -1 is one per processor, and a count at most one per processor but never fewer than 2; the suites' chunked mode
		// raises every count to its own.
		var options = new ExcelWriterOptions { CompressionThreads = threads };
		var resolved = threads == -1 ? Environment.ProcessorCount : Math.Min(threads, Math.Max(Environment.ProcessorCount, 2));
		var expected = Math.Max(resolved, ExcelWorkbookWriter.TestThreads);
		using (var book = ExcelWorkbookWriter.Create(new MemoryStream(), options))
		{
			Assert.Equal(threads, book.Options.CompressionThreads);
			Assert.Equal(expected, book.Package.Zip.Threads);
		}

		var path = Path.Combine(Path.GetTempPath(), $"CsvHelper.Excel.Tests.{Guid.NewGuid():N}.xlsx");
		try
		{
			using (var book = ExcelWorkbookWriter.ToFile(path, options))
			{
				Assert.Equal(expected, book.Package.Zip.Threads);
				book.WriteSheet("Orders", Orders.Take(10));
			}

			Written.CheckedFile(path);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Theory]
	[MemberData(nameof(Writes))]
	public async Task WritesTheSamePartsAtEveryCount(int threads, ExcelStrings strings, bool async, bool seekable)
	{
		var one = await WriteAsync(1, strings, async, seekable);
		var book = await WriteAsync(threads, strings, async, seekable);

		ParallelWrites.AssertSameParts(one.Bytes, book.Bytes);
		Assert.Equal(["Orders", "Validated"], book.SheetNames);
		Assert.Equal(0, book.ChunksAppended);
		ParallelWrites.AssertReleased(one);
		ParallelWrites.AssertReleased(book);
	}

	[Theory]
	[InlineData(SmallChunks, ExcelStrings.Inline)]
	[InlineData(SmallChunks, ExcelStrings.Shared)]
	[InlineData(0, ExcelStrings.Inline)]
	public async Task WritesTheSameBytesAtEveryCountAboveOne(int chunkSize, ExcelStrings strings)
	{
		var one = await WriteAsync(1, strings, false, chunkSize: chunkSize);
		var books = new List<ParallelBook>();
		foreach (var threads in new[] { 2, 4, 16 })
		{
			books.Add(await WriteAsync(threads, strings, false, chunkSize: chunkSize));
		}

		foreach (var book in books)
		{
			ParallelWrites.AssertSameParts(one.Bytes, book.Bytes);
			ParallelWrites.AssertReleased(book);
		}

#if NET
		// The chunks' bytes depend on the XML alone: not on the threads, nor on when each was deflated.
		foreach (var book in books)
		{
			ParallelWrites.AssertSameBytes(books[0].Bytes, book.Bytes);
		}

		if (one.Threads == 1)
		{
			// Each chunk but the last ends with a sync flush, and on .NET 10 they deflate at zlib's level 2: not 1's bytes.
			Assert.False(one.Bytes.SequenceEqual(books[0].Bytes), "The package written at 2 threads has the bytes written at 1: its parts weren't chunked.");
		}
#else
		if (one.Threads == 1)
		{
			// One deflater over each part, on one thread beside the writing thread: the bytes written at 1.
			foreach (var book in books)
			{
				ParallelWrites.AssertSameBytes(one.Bytes, book.Bytes);
			}
		}
#endif
	}

	[Fact]
	public async Task WritesAsynchronouslyThePartsItWritesSynchronously()
	{
		var synchronous = await WriteAsync(4, ExcelStrings.Shared, false);
		var asynchronous = await WriteAsync(4, ExcelStrings.Shared, true);

		ParallelWrites.AssertSameParts(synchronous.Bytes, asynchronous.Bytes);
#if NET
		ParallelWrites.AssertSameBytes(synchronous.Bytes, asynchronous.Bytes);
#endif
		ParallelWrites.AssertReleased(asynchronous);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task TakesNoThreadsWithoutCompression(bool async)
	{
		// Stored parts aren't deflated, so there is nothing to chunk: the bytes are 1's on every target.
		var one = await WriteAsync(1, ExcelStrings.Shared, async, level: CompressionLevel.NoCompression);
		var four = await WriteAsync(4, ExcelStrings.Shared, async, level: CompressionLevel.NoCompression);

		ParallelWrites.AssertSameBytes(one.Bytes, four.Bytes);
		ParallelWrites.AssertReleased(four);
	}

	/// <summary>
	/// Writes a workbook of a sheet of <see cref="Orders"/> and a validated sheet of <see cref="Validated"/>, whose list
	/// makes the lists sheet, serially, at a count of threads.
	/// </summary>
	/// <param name="threads">The count (<see cref="ExcelWriterOptions.CompressionThreads"/>).</param>
	/// <param name="strings">Where the text cells keep their strings.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="seekable">Whether the stream seeks.</param>
	/// <param name="chunkSize">The chunk to cut (<see cref="ExcelWorkbookWriter.ChunkSize"/>), or 0 for the default, 1 MB.</param>
	/// <param name="level">The compression.</param>
	/// <returns>A task whose result is the workbook and its writer's counters.</returns>
	private static Task<ParallelBook> WriteAsync(int threads, ExcelStrings strings, bool async, bool seekable = true, int chunkSize = SmallChunks, CompressionLevel level = CompressionLevel.Fastest) =>
		ParallelWrites.WorkbookAsync(
			async (book, a) =>
			{
				if (chunkSize > 0)
				{
					book.ChunkSize = chunkSize;
				}

				var validate = new ExcelSheetOptions { Validate = true };
				if (a)
				{
					await book.WriteSheetAsync("Orders", Orders);
					await book.WriteSheetAsync("Validated", Validated, validate);
				}
				else
				{
					book.WriteSheet("Orders", Orders);
					book.WriteSheet("Validated", Validated, validate);
				}
			},
			async,
			new ExcelWriterOptions { CompressionThreads = threads, Strings = strings, CompressionLevel = level },
			seekable);
}
