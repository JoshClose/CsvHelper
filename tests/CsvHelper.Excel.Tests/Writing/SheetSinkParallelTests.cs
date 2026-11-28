using System.Buffers;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using CsvHelper.Excel.Tests.Package;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// The owner's side of a parallel write on a sheet (Phase 9's step 10): the sheet's sink's parallel members, called as a
/// parallel write calls them, with chunks made by hand where a worker would write them. A chunk's rows are cut from the
/// same records' serial write, or made up to show what a refused chunk would have changed; each test compares the sheet
/// with the serial write's, part by part, and each refusal leaves the sheet's state as it was and its package's bytes as
/// the same calls without the chunk leave them.
/// </summary>
public class SheetSinkParallelTests
{
	/// <summary>Sync and async.</summary>
	public static TheoryData<bool> Modes => new() { false, true };

	/// <summary>The reasons a worker flags its chunk, sync and async.</summary>
	public static TheoryData<string, bool> Redos => new()
	{
		{ nameof(SheetChunkRedo.Style), false },
		{ nameof(SheetChunkRedo.Style), true },
		{ nameof(SheetChunkRedo.LinkStyle), false },
		{ nameof(SheetChunkRedo.LinkStyle), true },
		{ nameof(SheetChunkRedo.Error), false },
		{ nameof(SheetChunkRedo.Error), true },
	};

	/// <summary>The row limits, sync and async.</summary>
	public static TheoryData<ExcelRowLimit, bool> Limits => new()
	{
		{ ExcelRowLimit.Throw, false },
		{ ExcelRowLimit.Throw, true },
		{ ExcelRowLimit.NewSheet, false },
		{ ExcelRowLimit.NewSheet, true },
	};

	/// <summary>SpreadsheetML's namespace.</summary>
	private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

	/// <summary>The namespace of a part's relationships' ids.</summary>
	private static readonly XNamespace RelationshipIds = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

	/// <summary>The namespace of a relationships part.</summary>
	private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";

	/// <summary>The header of the sheets: four columns, the last mostly empty.</summary>
	private static readonly object?[] Header = ["Name", "Code", "Note", "Extra"];

	/// <summary>A record without a value: a row without a cell.</summary>
	private static readonly object?[] Empty = [null, null, null, null];

	/// <summary>The one target of the links of the tests of the links' cap, so that the sheet has one relationship.</summary>
	private static readonly Uri Site = new("https://example.com/");

	/// <summary>The sheets of the tests: no auto-fit, so the first data record makes the widths known, and a filter, which spans the rows to the last with a cell.</summary>
	private static readonly ExcelSheetOptions Look = new() { AutoFit = 0, AutoFilter = true };

	/// <summary>A record of text in three columns.</summary>
	/// <param name="i">Its number.</param>
	/// <returns>The record.</returns>
	private static object?[] Record(int i) => ["n" + i, "c" + i, "note " + i, null];

	/// <summary>Records of text in three columns.</summary>
	/// <param name="first">The first one's number.</param>
	/// <param name="count">The records.</param>
	/// <returns>The records.</returns>
	private static object?[][] Records(int first, int count) => [.. Enumerable.Range(first, count).Select(Record)];

	/// <summary>Options with the tests' look, inline strings, and a row limit.</summary>
	/// <param name="limit">The row limit.</param>
	/// <param name="sheets">The sheets' options, or <see langword="null"/> for <see cref="Look"/>.</param>
	/// <returns>The options.</returns>
	private static ExcelWriterOptions Options(ExcelRowLimit limit = ExcelRowLimit.Throw, ExcelSheetOptions? sheets = null) =>
		new() { Strings = ExcelStrings.Inline, RowLimit = limit, Sheets = sheets ?? Look };

	/// <summary>Options for sheets of many links without a header, whose records have any number of fields.</summary>
	/// <param name="limit">The row limit.</param>
	/// <returns>The options.</returns>
	private static ExcelWriterOptions LinkOptions(ExcelRowLimit limit = ExcelRowLimit.Throw) =>
		new() { Strings = ExcelStrings.Inline, RowLimit = limit, Sheets = new ExcelSheetOptions { AutoFit = 0 }, Csv = new CsvWriterOptions { HasHeader = false, ValidateFieldCount = false } };

	[Fact]
	public async Task SupportsParallelWritesAndIsReadyOnceAutoFitHasItsRecords()
	{
		await Written.WorkbookAsync(async (book, a) =>
		{
			var sheet = new HandSheet(book, a);
			Assert.True(Owner.SupportsParallel(sheet.Sink));
			Assert.False(Owner.ReadyForWorkers(sheet.Sink));
			await sheet.WriteAsync(Header);
			for (var i = 0; i < 100; i++)
			{
				// Auto-fit measures the first 100 records below the header, which come before the columns' widths.
				Assert.False(Owner.ReadyForWorkers(sheet.Sink));
				await sheet.WriteAsync(Record(i));
			}

			Assert.True(Owner.ReadyForWorkers(sheet.Sink));

			// Not in the middle of a record.
			sheet.Writer.WriteField("n100");
			Assert.False(Owner.ReadyForWorkers(sheet.Sink));
			sheet.Writer.WriteField("c100");
			sheet.Writer.WriteField("");
			sheet.Writer.WriteField("");
			await Written.EndRecordAsync(sheet.Writer, a);
			Assert.True(Owner.ReadyForWorkers(sheet.Sink));
			await sheet.DisposeAsync();
		}, false, new ExcelWriterOptions());
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task IsReadyWithoutAutoFitOnceTheFirstDataRecordIsWritten(bool async)
	{
		await Written.WorkbookAsync(async (book, a) =>
		{
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(Header);
			Assert.False(Owner.ReadyForWorkers(sheet.Sink));
			await sheet.WriteAsync(Record(0));
			Assert.True(Owner.ReadyForWorkers(sheet.Sink));
			await sheet.DisposeAsync();
		}, async, Options());

		// Without a header, the first record is the first data record, whose typed cells set the widths.
		await Written.WorkbookAsync(async (book, a) =>
		{
			var sheet = new HandSheet(book, a);
			Assert.False(Owner.ReadyForWorkers(sheet.Sink));
			await sheet.WriteAsync(Record(0));
			Assert.True(Owner.ReadyForWorkers(sheet.Sink));
			await sheet.DisposeAsync();
		}, async, LinkOptions());
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task IsReadyUnderProtectOnceTheSheetsStartIsWritten(bool async)
	{
		// The first flush, past 64 KB, writes the start, whose columns' unlocked styles it adds to the cell formats.
		var flushedAt = await ReadyRowsAsync(async, new ExcelSheetOptions { AutoFit = 0, Protect = true });
		Assert.True(flushedAt > 100, $"The start was written after {flushedAt} records.");

		// Without Protect, the sheet is ready before its entry is opened.
		Assert.Equal(1, await ReadyRowsAsync(async, Look));
	}

	[Fact]
	public async Task IsNeverReadyWithSharedStrings()
	{
		await Written.WorkbookAsync(async (book, a) =>
		{
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(Header);
			foreach (var record in Records(0, 200))
			{
				await sheet.WriteAsync(record);
				Assert.False(Owner.ReadyForWorkers(sheet.Sink));
			}

			await sheet.DisposeAsync();
		}, false, Options() with { Strings = ExcelStrings.Shared });
	}

	[Theory]
	[MemberData(nameof(Limits))]
	public async Task IsNotReadyOnAFullSheet(ExcelRowLimit limit, bool async)
	{
		await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 4;
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync([Header, .. Records(0, 2)]);
			Assert.True(Owner.ReadyForWorkers(sheet.Sink));
			await sheet.WriteAsync(Record(2));
			Assert.False(Owner.ReadyForWorkers(sheet.Sink));
			if (limit == ExcelRowLimit.NewSheet)
			{
				// The record that starts the next sheet is written serially, and then that sheet is ready.
				await sheet.WriteAsync(Record(3));
				Assert.Equal(["Data", "Data (2)"], book.SheetNames);
				Assert.True(Owner.ReadyForWorkers(sheet.Sink));
			}

			await sheet.DisposeAsync();
		}, async, Options(limit));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task AppendsAChunkAsTheSerialWriteWritesItsRecords(bool async)
	{
		object?[][] records = [Header, .. Records(0, 10)];
		records[5] = ["n4", "00123", "note 4", null];
		records[7] = ["n6", "c6", "note 6", "wide"];
		var serial = await SerialAsync(async, Options(), records);

		ExcelWorkbookWriter? written = null;
		var parallel = await Written.WorkbookAsync(async (book, a) =>
		{
			written = book;
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(records.Take(3).ToArray());
			Begin(sheet.Sink);
			var chunk = ChunkOf(serial, 4, 5);
			Assert.True(await sheet.AppendAsync(chunk));
			Assert.True(chunk.Xml.IsEmpty);
			await sheet.WriteAsync(records.Skip(8).ToArray());
			Owner.EndParallel(sheet.Sink);
			Assert.Equal((1, 0), (book.ChunksAppended, book.ChunksRedone));
			await sheet.DisposeAsync();
		}, async, Options());

		Assert.Equal(Written.Entries(serial), Written.Entries(parallel));
		Assert.Contains("<ignoredError sqref=\"A1:D11\" numberStoredAsText=\"1\"/>", Written.Entry(parallel, "xl/worksheets/sheet1.xml"));
		Assert.Equal(0, written!.Package.Zip.HeldSegments);
		Assert.Equal(0, written.Package.Zip.RentedBuffers);
		Assert.Empty(Written.ValidationErrors(parallel));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesTheEmptyRowsBeforeAChunksFirstCell(bool async)
	{
		// Two empty records of the sheet's and one of the chunk's before its first cell, one between its cells and one after
		// them, and one more of the sheet's before the next cell.
		object?[][] records = [Header, Record(0), Empty, Empty, Empty, Record(4), Empty, Record(6), Empty, Empty, Record(9)];
		var serial = await SerialAsync(async, Options(), records);

		var parallel = await Written.WorkbookAsync(async (book, a) =>
		{
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(records.Take(4).ToArray());
			Begin(sheet.Sink);
			var chunk = ChunkOf(serial, 5, 5);
			Assert.Equal((1, 3), (chunk.FirstCellRecord, chunk.LastCellRecord));
			Assert.True(await sheet.AppendAsync(chunk));
			Assert.Equal((10L, 8L), (sheet.Sink.ChunkState.Row, sheet.Sink.ChunkState.LastRow));
			await sheet.WriteAsync(records.Skip(9).ToArray());
			Owner.EndParallel(sheet.Sink);
			await sheet.DisposeAsync();
		}, async, Options());

		Assert.Equal(Written.Entries(serial), Written.Entries(parallel));
		Assert.Contains("<row><c t=\"inlineStr\"><is><t>n0</t></is></c><c t=\"inlineStr\"><is><t>c0</t></is></c><c t=\"inlineStr\"><is><t>note 0</t></is></c></row><row/><row/><row/><row><c t=\"inlineStr\"><is><t>n4</t>", Written.Entry(parallel, "xl/worksheets/sheet1.xml"));
		Assert.Equal(["Name|Code|Note|Extra", "n0|c0|note 0|", "", "", "", "n4|c4|note 4|", "", "n6|c6|note 6|", "", "", "n9|c9|note 9|"], Written.ReadOne(parallel));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task CountsTheRowsOfAChunkWithoutACell(bool async)
	{
		object?[][] records = [Header, Record(0), Empty, Empty, Empty, Record(4)];
		var serial = await SerialAsync(async, Options(), records);

		var parallel = await Written.WorkbookAsync(async (book, a) =>
		{
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(records.Take(2).ToArray());
			Begin(sheet.Sink);
			var chunk = ChunkOf(serial, 3, 3);
			Assert.True(chunk.Xml.IsEmpty);
			var before = sheet.Sink.ChunkState;
			Assert.True(await sheet.AppendAsync(chunk));

			// It writes nothing, and doesn't flush the records before it: the next record with a cell writes its rows.
			Assert.Equal(before with { Row = before.Row + 3 }, sheet.Sink.ChunkState);
			Assert.Null(sheet.Sink.Entry);
			await sheet.WriteAsync(records.Skip(5).ToArray());
			Owner.EndParallel(sheet.Sink);
			await sheet.DisposeAsync();
		}, async, Options());

		Assert.Equal(Written.Entries(serial), Written.Entries(parallel));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task AddsUpTheUsedRangeAndTheFlagOfTextLikeANumber(bool async)
	{
		// The chunk has the sheet's last row with a cell, its last column (the header's last name is empty), and its only text
		// like a number; empty records follow.
		object?[][] records = [["Name", "Code", "Note", null], Record(0), Record(1), ["n2", "c2", "note 2", "wide"], ["n3", "+44", "note 3", null], Empty, Empty];
		var serial = await SerialAsync(async, Options(), records);

		var parallel = await Written.WorkbookAsync(async (book, a) =>
		{
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(records.Take(3).ToArray());
			Begin(sheet.Sink);
			Assert.Equal((2, false), (sheet.Sink.ChunkState.LastColumn, sheet.Sink.ChunkState.NumberLikeText));
			var chunk = ChunkOf(serial, 4, 2);
			Assert.Equal((3, true), (chunk.LastColumn, chunk.NumberLikeText));
			Assert.True(await sheet.AppendAsync(chunk));
			Assert.Equal((5L, 3, true), (sheet.Sink.ChunkState.LastRow, sheet.Sink.ChunkState.LastColumn, sheet.Sink.ChunkState.NumberLikeText));
			await sheet.WriteAsync(records.Skip(5).ToArray());
			Owner.EndParallel(sheet.Sink);
			await sheet.DisposeAsync();
		}, async, Options());

		Assert.Equal(Written.Entries(serial), Written.Entries(parallel));
		var xml = Written.Entry(parallel, "xl/worksheets/sheet1.xml");
		Assert.Contains("<autoFilter ref=\"A1:D5\"/>", xml);
		Assert.Contains("<ignoredError sqref=\"A1:D5\" numberStoredAsText=\"1\"/>", xml);
		Assert.Empty(Written.ValidationErrors(parallel));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task RebasesAChunksLinksToTheirRows(bool async)
	{
		// The chunk's links: a new target, a URI the policy leaves as text, and the target of a link before it again.
		object?[][] records =
		[
			Header,
			["n0", "c0", new Uri("https://first.example/"), null],
			Record(1),
			["n2", "c2", new Uri("https://second.example/a"), null],
			["n3", new Uri("ftp://files.example/f"), "note 3", new Uri("https://first.example/")],
			Record(4),
			["n5", "c5", new Uri("https://third.example/"), null],
		];
		var serial = await SerialAsync(async, Options(), records, last: (sheet, _) => FailRecord(sheet), lastRecord: Empty);

		var parallel = await Written.WorkbookAsync(async (book, a) =>
		{
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(records.Take(3).ToArray());
			Begin(sheet.Sink);
			var chunk = ChunkOf(serial, 4, 4);
			Assert.Equal(new[] { new ChunkLink(0, 2, "https://second.example/a"), new ChunkLink(1, 3, "https://first.example/"), new ChunkLink(3, 2, "https://third.example/") }, chunk.Links!);
			Assert.True(await sheet.AppendAsync(chunk));
			Assert.Equal(4, sheet.Sink.ChunkState.Links);

			// They are their records' once the chunk is appended: a record that fails after it drops only its own.
			await FailRecord(sheet);
			Assert.Equal(4, sheet.Sink.ChunkState.Links);
			Owner.EndParallel(sheet.Sink);
			await sheet.DisposeAsync();
		}, async, Options());

		Assert.Equal(Written.Entries(serial), Written.Entries(parallel));
		Assert.Contains("<hyperlinks><hyperlink ref=\"C2\" r:id=\"rId1\"/><hyperlink ref=\"C4\" r:id=\"rId2\"/><hyperlink ref=\"D5\" r:id=\"rId1\"/><hyperlink ref=\"C7\" r:id=\"rId3\"/></hyperlinks>", Written.Entry(parallel, "xl/worksheets/sheet1.xml"));
		Assert.Empty(Written.ValidationErrors(parallel));
	}

	/// <summary>
	/// Writes a record whose second field fails, a link and a text too long for a cell, and drops the record from the sink,
	/// as a parallel write's serial records drop one that fails; its writer keeps the failed field, so a test ends the
	/// sheet after it.
	/// </summary>
	/// <param name="sheet">The sheet.</param>
	/// <returns>A task that completes when the record is dropped.</returns>
	private static Task FailRecord(HandSheet sheet)
	{
		var links = sheet.Sink.ChunkState.Links;
		sheet.Writer.WriteField(new Uri("https://failed.example/"));
		Assert.Throws<InvalidOperationException>(() => sheet.Writer.WriteField(new string('x', XmlOutput.MaxCellLength + 1)));
		Assert.Equal(links + 1, sheet.Sink.ChunkState.Links);
		Owner.DiscardRecord(sheet.Sink);
		Assert.Equal(links, sheet.Sink.ChunkState.Links);

		return Task.CompletedTask;
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task AppendsAChunkToAProtectedSheet(bool async)
	{
		var options = Options(sheets: new ExcelSheetOptions { AutoFit = 0, Protect = true });
		object?[][] records = [Header, .. Enumerable.Range(0, 700).Select(LongRecord)];
		var serial = await SerialAsync(async, options, records);

		var parallel = await Written.WorkbookAsync(async (book, a) =>
		{
			var sheet = new HandSheet(book, a);
			var written = 0;
			while (!Owner.ReadyForWorkers(sheet.Sink))
			{
				await sheet.WriteAsync(records[written++]);
			}

			Assert.InRange(written, 100, 690);
			Begin(sheet.Sink);
			Assert.True(await sheet.AppendAsync(ChunkOf(serial, written + 1, 10)));
			await sheet.WriteAsync(records.Skip(written + 10).ToArray());
			Owner.EndParallel(sheet.Sink);
			await sheet.DisposeAsync();
		}, async, options);

		// The cells below the header unlocked, in the styles the serial write added, in its order.
		Assert.Equal(Written.Entries(serial), Written.Entries(parallel));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task AppendsAChunkToTheNextSheetOfNewSheet(bool async)
	{
		object?[][] records = [Header, .. Records(0, 8)];
		var serial = await SerialAsync(async, Options(ExcelRowLimit.NewSheet), records, maxRows: 5);

		var parallel = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 5;
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(records.Take(6).ToArray());
			Begin(sheet.Sink);
			Assert.Equal(["Data", "Data (2)"], book.SheetNames);

			// Its rows from the next sheet's third, below the header and the record that started it.
			Assert.True(await sheet.AppendAsync(ChunkOf(serial, 3, 2, sheet: 2)));
			await sheet.WriteAsync(records.Skip(8).ToArray());
			Owner.EndParallel(sheet.Sink);
			await sheet.DisposeAsync();
		}, async, Options(ExcelRowLimit.NewSheet));

		Assert.Equal(Written.Entries(serial), Written.Entries(parallel));
		Assert.Equal(["Data: Name|Code|Note|Extra / n0|c0|note 0| / n1|c1|note 1| / n2|c2|note 2| / n3|c3|note 3|", "Data (2): Name|Code|Note|Extra / n4|c4|note 4| / n5|c5|note 5| / n6|c6|note 6| / n7|c7|note 7|"], Written.Sheets(parallel));
	}

	[Theory]
	[MemberData(nameof(Limits))]
	public async Task AppendsAChunkToTheLastRowAndRefusesTheNextOne(ExcelRowLimit limit, bool async)
	{
		object?[][] records = [Header, .. Records(0, 6)];
		var serial = await SerialAsync(async, Options(limit), records, maxRows: 6, last: ThrowsAtTheLimit(limit));

		var parallel = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 6;
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(records.Take(3).ToArray());
			Begin(sheet.Sink);

			// Rows 4 to 6, the last: the sheet is full after it, so the next record finds the limit.
			Assert.True(await sheet.AppendAsync(ChunkOf(serial, 4, 3)));
			Assert.Equal((7L, 0), (sheet.Sink.ChunkState.Row, sheet.Sink.ChunkState.FieldLimit));
			Assert.False(Owner.ReadyForWorkers(sheet.Sink));
			var state = sheet.Sink.ChunkState;
			var next = Chunk("<row><c t=\"inlineStr\"><is><t>n5</t></is></c></row>", 1, 0, 0, 0);
			Assert.False(await sheet.AppendAsync(next));
			Assert.True(next.Xml.IsEmpty);
			Assert.Equal(state, sheet.Sink.ChunkState);
			Assert.Equal((1, 1), (book.ChunksAppended, book.ChunksRedone));

			// The batch again, serially: the record past the limit throws, or starts the next sheet.
			await ThrowsAtTheLimit(limit)(sheet, records[6]);
			Owner.EndParallel(sheet.Sink);
			await sheet.DisposeAsync();
		}, async, Options(limit));

		Assert.Equal(Written.Entries(serial), Written.Entries(parallel));
	}

	[Theory]
	[MemberData(nameof(Redos))]
	public async Task RefusesAChunkItsWorkerFlagged(string redo, bool async)
	{
		var poison = Poison();
		poison.Redo = (SheetChunkRedo)Enum.Parse(typeof(SheetChunkRedo), redo);
		await AssertRefusedAsync(async, Options(), [Header, Record(0), Record(1)], [], () => poison, Records(2, 3), [Record(5)]);
	}

	[Theory]
	[MemberData(nameof(Limits))]
	public async Task RefusesAChunkWhoseRowsPassTheLastRow(ExcelRowLimit limit, bool async)
	{
		// Rows 4 to 7, past the last, 6.
		var chunk = Poison();
		chunk.Records = 4;
		await AssertRefusedAsync(async, Options(limit), [Header, Record(0), Record(1)], [], () => chunk, Records(2, 3), [], maxRows: 6, last: ThrowsAtTheLimit(limit), lastRecord: Record(5));
	}

	[Fact]
	public async Task DropsAChunkWhenTheWriteIsCanceled()
	{
		object?[][] records = [Header, Record(0), Record(1), .. Records(2, 3)];
		var serial = await SerialAsync(true, Options(), records);
		var parallel = await Written.WorkbookAsync(async (book, a) =>
		{
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(records.Take(3).ToArray());
			Begin(sheet.Sink);
			var state = sheet.Sink.ChunkState;

			// A chunk with rows, and one of empty records, which would write nothing: the sink takes each and adds nothing of it.
			foreach (var chunk in new[] { Poison(), Chunk("", 2, -1, -1, -1) })
			{
				await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Owner.WriteChunkAsync(sheet.Sink, chunk, new CancellationToken(true)));
				Assert.True(chunk.Xml.IsEmpty);
				Assert.Equal(state, sheet.Sink.ChunkState);
			}

			Assert.Equal((0, 0), (book.ChunksAppended, book.ChunksRedone));
			await sheet.WriteAsync(records.Skip(3).ToArray());
			Owner.EndParallel(sheet.Sink);
			await sheet.DisposeAsync();
		}, true, Options());

		Assert.Equal(Written.Entries(serial), Written.Entries(parallel));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task DropsAChunkAndItsLinksWhenItsWriteFails(bool async)
	{
		// Stored entries write through once past a buffer: the sheet's records before the chunk reach the stream as it is
		// appended, and the stream fails then.
		var options = Options() with { CompressionLevel = CompressionLevel.NoCompression };
		var before = await WriteToTheChunkAsync(new ZipTestStream(), null);
		var failing = new ZipTestStream { FailAt = before + 1 };
		Assert.Equal(before, await WriteToTheChunkAsync(failing, failing.Failure));
		Assert.Equal(before + 1, failing.Attempts);

		// Writes a sheet up to a chunk, and the chunk when the stream is to fail; returns the writes the stream had before it.
		async Task<int> WriteToTheChunkAsync(ZipTestStream stream, IOException? failure)
		{
			var book = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true);
			var sheet = new HandSheet(book, async);
			await sheet.WriteAsync([Header, .. Enumerable.Range(0, 800).Select(LongRecord)]);
			Assert.NotNull(sheet.Sink.Entry);
			Begin(sheet.Sink);
			var attempts = stream.Attempts;
			if (failure is not null)
			{
				var state = sheet.Sink.ChunkState;
				var chunk = Poison();
				var e = await Assert.ThrowsAsync<IOException>(() => sheet.AppendAsync(chunk));
				Assert.Same(failure, e);

				// The sink took the chunk, and its links went with it.
				Assert.True(chunk.Xml.IsEmpty);
				Assert.Equal(state.Links, sheet.Sink.ChunkState.Links);
				Assert.Equal((0, 0), (book.ChunksAppended, book.ChunksRedone));
			}

			// Once the stream has failed, the package writes nothing more, and nothing throws.
			Owner.EndParallel(sheet.Sink);
			await sheet.DisposeAsync();
			if (async)
			{
				await book.DisposeAsync();
			}
			else
			{
				book.Dispose();
			}

			Assert.Equal(0, book.Package.Zip.HeldSegments);
			Assert.Equal(0, book.Package.Zip.RentedBuffers);

			return attempts;
		}
	}

	[Fact]
	public async Task LinksAChunkUpToTheCapAndRefusesOnePastIt()
	{
		// 65,528 links before the chunks: the first chunk's two make Excel's 65,530, and the next chunk's one is past them.
		object?[][] records = [.. Enumerable.Range(0, 8).Select(_ => Links(8_191)), Links(2), Links(1), Links(1)];
		var serial = await SerialAsync(false, LinkOptions(), records);
		Assert.Equal(65_530, CountOf(Written.Entry(serial, "xl/worksheets/sheet1.xml"), "<hyperlink "));

		var parallel = await Written.WorkbookAsync(async (book, a) =>
		{
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(records.Take(8).ToArray());
			Begin(sheet.Sink);
			Assert.Equal(1, sheet.Sink.Views!.View.Version);
			Assert.True(await sheet.AppendAsync(ChunkOf(serial, 9, 1)));
			Assert.Equal(65_530, sheet.Sink.ChunkState.Links);

			// Written with the first view, whose links had room: its URI is a link, past the cap.
			var state = sheet.Sink.ChunkState;
			var past = Chunk(LinkRow(1), 1, 0, 0, 0, links: [new ChunkLink(0, 0, Site.AbsoluteUri)]);
			Assert.False(await sheet.AppendAsync(past));
			Assert.Equal(state, sheet.Sink.ChunkState);

			// The view is frozen again with the links full, whose workers write URIs as text.
			Assert.Equal((2, true), (sheet.Sink.Views.View.Version, sheet.Sink.Views.View.LinksFull));
			await sheet.WriteAsync(records[9]);
			var text = ChunkOf(serial, 11, 1, version: 2);
			Assert.Null(text.Links);
			Assert.True(await sheet.AppendAsync(text));
			Assert.Equal((2, 1), (book.ChunksAppended, book.ChunksRedone));
			Owner.EndParallel(sheet.Sink);
			await sheet.DisposeAsync();
		}, false, LinkOptions());

		Assert.Equal(Written.Entries(serial), Written.Entries(parallel));
	}

	[Fact]
	public async Task RefusesAChunkWrittenWithTheLinksFullOnceTheNextSheetHasRoom()
	{
		// The first sheet's links fill in its ten records; it is full at row 12, and its next record starts the next sheet.
		object?[][] records = [.. Enumerable.Range(0, 10).Select(_ => Links(6_553)), Record(10), Record(11), Links(1), Links(1), Links(1)];
		var serial = await SerialAsync(false, LinkOptions(ExcelRowLimit.NewSheet), records, maxRows: 12);

		var parallel = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 12;
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(records.Take(10).ToArray());
			Begin(sheet.Sink);
			Assert.True(sheet.Sink.Views!.View.LinksFull);
			await sheet.WriteAsync(records.Skip(10).Take(3).ToArray());
			Assert.Equal(["Data", "Data (2)"], book.SheetNames);

			// Its worker read the first view, so its URI is text, where the next sheet links it.
			var state = sheet.Sink.ChunkState;
			var text = Chunk(TextRow(Site.OriginalString), 1, 0, 0, 0);
			Assert.False(await sheet.AppendAsync(text));
			Assert.Equal(state, sheet.Sink.ChunkState);
			Assert.Equal((2, false), (sheet.Sink.Views.View.Version, sheet.Sink.Views.View.LinksFull));
			await sheet.WriteAsync(records[13]);

			// Written with the second view: its URI is a link.
			var linked = ChunkOf(serial, 3, 1, sheet: 2, version: 2);
			Assert.Equal(new[] { new ChunkLink(0, 0, Site.AbsoluteUri) }, linked.Links!);
			Assert.True(await sheet.AppendAsync(linked));
			Assert.Equal((1, 1), (book.ChunksAppended, book.ChunksRedone));
			Owner.EndParallel(sheet.Sink);
			await sheet.DisposeAsync();
		}, false, LinkOptions(ExcelRowLimit.NewSheet));

		Assert.Equal(Written.Entries(serial), Written.Entries(parallel));
		Assert.Contains("<hyperlinks><hyperlink ref=\"A1\" r:id=\"rId1\"/><hyperlink ref=\"A2\" r:id=\"rId1\"/><hyperlink ref=\"A3\" r:id=\"rId1\"/></hyperlinks>", Written.Entry(parallel, "xl/worksheets/sheet2.xml"));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task LinksAChunkOnTheNextSheetOnceARedoHasCrossedToIt(bool async)
	{
		// The first sheet's 65,530 links fill in its ten records, and it is full at row 12. A batch across that row is written
		// again serially, and its last record starts the next sheet, whose links have room.
		object?[][] records = [.. Enumerable.Range(0, 10).Select(_ => Links(6_553)), Record(10), Record(11), Links(1), Links(1), Links(1), Links(1)];
		var serial = await SerialAsync(async, LinkOptions(ExcelRowLimit.NewSheet), records, maxRows: 12);

		var parallel = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 12;
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(records.Take(10).ToArray());
			Begin(sheet.Sink);
			Assert.True(sheet.Sink.Views!.View.LinksFull);

			// Rows 11 to 13: past the last row, so written again serially.
			var across = Chunk(TextRow("n10") + TextRow("n11") + TextRow(Site.OriginalString), 3, 0, 2, 0);
			Assert.False(await sheet.AppendAsync(across));
			await sheet.WriteAsync(records.Skip(10).Take(3).ToArray());
			Assert.Equal(["Data", "Data (2)"], book.SheetNames);

			// A chunk its worker wrote with the first view, whose links were full: its URI is text, so it is written again,
			// and the view is frozen again, with room.
			var stale = Chunk(TextRow(Site.OriginalString), 1, 0, 0, 0);
			Assert.False(await sheet.AppendAsync(stale));
			Assert.Equal((2, false), (sheet.Sink.Views.View.Version, sheet.Sink.Views.View.LinksFull));
			await sheet.WriteAsync(records[13]);

			// The next chunk, written with that view, has its links, which the sheet appends at their rows.
			var linked = ChunkOf(serial, 3, 2, sheet: 2, version: 2);
			Assert.Equal(new[] { new ChunkLink(0, 0, Site.AbsoluteUri), new ChunkLink(1, 0, Site.AbsoluteUri) }, linked.Links!);
			Assert.True(await sheet.AppendAsync(linked));
			Assert.Equal(4, sheet.Sink.ChunkState.Links);
			Assert.Equal((1, 2), (book.ChunksAppended, book.ChunksRedone));
			Owner.EndParallel(sheet.Sink);
			await sheet.DisposeAsync();
		}, async, LinkOptions(ExcelRowLimit.NewSheet));

		Assert.Equal(Written.Entries(serial), Written.Entries(parallel));
		Assert.Contains("<hyperlinks><hyperlink ref=\"A1\" r:id=\"rId1\"/><hyperlink ref=\"A2\" r:id=\"rId1\"/><hyperlink ref=\"A3\" r:id=\"rId1\"/><hyperlink ref=\"A4\" r:id=\"rId1\"/></hyperlinks>", Written.Entry(parallel, "xl/worksheets/sheet2.xml"));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FreezesTheViewAgainOnceARedoneBatchAddsACellFormat(bool async)
	{
		object?[][] records = [Header, Record(0), ["n1", "c1", "note 1", new DateTime(2024, 1, 15, 10, 30, 0)], Record(2), Record(3)];
		var serial = await SerialAsync(async, Options(), records);

		var parallel = await Written.WorkbookAsync(async (book, a) =>
		{
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(records.Take(2).ToArray());
			Begin(sheet.Sink);
			var first = sheet.Sink.Views!.View;

			// The date's cell format isn't in the view: its worker flags the chunk, and the batch is written again serially.
			var dated = Poison();
			dated.Redo = SheetChunkRedo.Style;
			Assert.False(await sheet.AppendAsync(dated));
			Assert.Same(first, sheet.Sink.Views.View);
			await sheet.WriteAsync(records[2]);

			Assert.True(await sheet.AppendAsync(ChunkOf(serial, 4, 1)));
			var second = sheet.Sink.Views.View;
			Assert.Equal(2, second.Version);
			Assert.Equal(first.Styles.Count + 1, second.Styles.Count);
			Assert.True(second.Styles.TryIndexOf(FixedFormatOf(serial), out _));

			// Nothing added since: the same view.
			Assert.True(await sheet.AppendAsync(ChunkOf(serial, 5, 1, version: 2)));
			Assert.Same(second, sheet.Sink.Views.View);
			Owner.EndParallel(sheet.Sink);
			await sheet.DisposeAsync();
		}, async, Options());

		Assert.Equal(Written.Entries(serial), Written.Entries(parallel));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FreezesTheViewAgainOnceARedoneBatchAddsTheLinkStyle(bool async)
	{
		object?[][] records = [Header, Record(0), ["n1", "c1", new Uri("https://first.example/"), null], ["n2", "c2", new Uri("https://second.example/"), null]];
		var serial = await SerialAsync(async, Options(), records);

		var parallel = await Written.WorkbookAsync(async (book, a) =>
		{
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(records.Take(2).ToArray());
			Begin(sheet.Sink);
			Assert.Equal(0, sheet.Sink.Views!.View.LinkStyle);
			var linked = Poison();
			linked.Redo = SheetChunkRedo.LinkStyle;
			Assert.False(await sheet.AppendAsync(linked));
			await sheet.WriteAsync(records[2]);

			Assert.True(await sheet.AppendAsync(ChunkOf(serial, 4, 1)));
			Assert.Equal(2, sheet.Sink.Views.View.Version);
			Assert.NotEqual(0, sheet.Sink.Views.View.LinkStyle);
			Owner.EndParallel(sheet.Sink);
			await sheet.DisposeAsync();
		}, async, Options());

		Assert.Equal(Written.Entries(serial), Written.Entries(parallel));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FreezesTheViewAgainOnceARedoneBatchTakesTheWorkbooksLinkStyle(bool async)
	{
		// The workbook's first sheet added the link style: the second sheet's first link takes it, adding no cell format.
		object?[][] records = [Header, Record(0), ["n1", "c1", new Uri("https://first.example/"), null], Record(2)];
		var serial = await Written.WorkbookAsync(async (book, a) =>
		{
			await LinkedSheetAsync(book, a);
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(records);
			await sheet.DisposeAsync();
		}, async, Options());

		var parallel = await Written.WorkbookAsync(async (book, a) =>
		{
			await LinkedSheetAsync(book, a);
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(records.Take(2).ToArray());
			Begin(sheet.Sink);
			var first = sheet.Sink.Views!.View;
			Assert.Equal(0, first.LinkStyle);
			var linked = Poison();
			linked.Redo = SheetChunkRedo.LinkStyle;
			Assert.False(await sheet.AppendAsync(linked));
			await sheet.WriteAsync(records[2]);

			Assert.True(await sheet.AppendAsync(ChunkOf(serial, 4, 1, sheet: 2)));
			var second = sheet.Sink.Views.View;
			Assert.Equal((2, first.Styles.Count), (second.Version, second.Styles.Count));
			Assert.NotEqual(0, second.LinkStyle);
			Owner.EndParallel(sheet.Sink);
			await sheet.DisposeAsync();
		}, async, Options());

		Assert.Equal(Written.Entries(serial), Written.Entries(parallel));
	}

	/// <summary>Writes a sheet, <c>Links</c>, of a header and a link, which adds the link style to the workbook.</summary>
	/// <param name="book">The workbook's writer.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the sheet is complete.</returns>
	private static async Task LinkedSheetAsync(ExcelWorkbookWriter book, bool async)
	{
		var writer = book.AddSheet("Links");
		writer.WriteField("Site");
		await Written.EndRecordAsync(writer, async);
		writer.WriteField(new Uri("https://linked.example/"));
		await Written.EndRecordAsync(writer, async);
		await Written.DisposeAsync(writer, async);
	}

	[Fact]
	public async Task SetsTheParallelThreadsForTheWriteAndClearsThem()
	{
		await Written.WorkbookAsync(async (book, a) =>
		{
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync([Header, Record(0)]);
			Assert.Equal(0, book.Package.ParallelThreads);
			Owner.BeginParallel(sheet.Sink, 6);
			Assert.Equal(Math.Min(6, Math.Max(Environment.ProcessorCount, 2)), book.Package.ParallelThreads);
			Assert.NotNull(sheet.Sink.Views);

			// A sheet whose entry isn't open yet opens it as its first flush asks.
			Assert.Null(sheet.Sink.Entry);
			Assert.Equal(0, book.Package.ParallelPeak);
			Owner.EndParallel(sheet.Sink);
			Assert.Equal(0, book.Package.ParallelThreads);
			Assert.Equal(Math.Min(6, Math.Max(Environment.ProcessorCount, 2)), book.Package.ParallelPeak);
			Assert.Null(sheet.Sink.Views);
			Assert.Throws<InvalidOperationException>(() => Owner.CreateWorker(sheet.Sink));
			await sheet.DisposeAsync();
		}, false, Options());
	}

	[Fact]
	public async Task KeepsTheMostThreadsOfItsParallelWritesForThePartsAfterTheSheets()
	{
		// Two sheets' parallel writes, at 6 and then at 2: the sheets between and after them take their own threads, and
		// the parts written as the package closes, the most the writes took.
		var most = Math.Min(6, Math.Max(Environment.ProcessorCount, 2));
		ExcelWorkbookWriter? closed = null;
		await Written.WorkbookAsync(async (book, a) =>
		{
			closed = book;
			var first = new HandSheet(book, a);
			await first.WriteAsync([Header, Record(0)]);
			Owner.BeginParallel(first.Sink, 6);
			Owner.EndParallel(first.Sink);
			await first.DisposeAsync();

			var second = new HandSheet(book, a, "Second");
			await second.WriteAsync([Header, Record(0)]);
			Assert.Equal(0, book.Package.ParallelThreads);
			Owner.BeginParallel(second.Sink, 2);
			Assert.Equal(2, book.Package.ParallelThreads);
			Owner.EndParallel(second.Sink);
			Assert.Equal(0, book.Package.ParallelThreads);
			Assert.Equal(most, book.Package.ParallelPeak);
			await second.DisposeAsync();
		}, false, Options());

		Assert.Equal(most, closed!.Package.ParallelPeak);
		Assert.Equal(most, closed.Package.ParallelThreads);
	}

	[Fact]
	public async Task OpensTheEntryOnTheWritesThreadsWhenTheWriteEndedBeforeAFlush()
	{
		// A parallel write that ends before the sheet's first flush leaves its rows in the buffer: the next write-out opens
		// the entry on the write's threads, and the sheets NewSheet begins after it on the package's own (1 here).
		var most = Math.Min(6, Math.Max(Environment.ProcessorCount, 2));
		await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 100;
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync([Header, Record(0)]);
			Owner.BeginParallel(sheet.Sink, 6);
			Owner.EndParallel(sheet.Sink);
			Assert.Null(sheet.Sink.Entry);
			Assert.Equal(most, sheet.Sink.HeldThreads);

			// Past the first sheet's 100 rows: its flush opens its entry, and the next sheet's.
			await sheet.WriteAsync([.. Enumerable.Range(1, 100).Select(Record)]);
			Assert.Equal(0, sheet.Sink.HeldThreads);
			Assert.Equal(0, book.Package.ParallelThreads);

			// Past the second sheet's: the third's entry opens on the package's thread.
			await sheet.WriteAsync([.. Enumerable.Range(101, 100).Select(Record)]);
			Assert.NotNull(sheet.Sink.Entry);
			if (!TestThreads.Chunked)
			{
				Assert.False(PackageWriter.TakesSegments(sheet.Sink.Entry), "A sheet NewSheet began after the parallel write's rows were out was chunked.");
			}

			await sheet.DisposeAsync();
		}, false, Options(ExcelRowLimit.NewSheet));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ChunksTheSheetsEntryWhenAFlushHasOpenedIt(bool async)
	{
		var options = Options();
		object?[][] records = [Header, .. Enumerable.Range(0, 700).Select(LongRecord)];
		var serial = await SerialAsync(async, options, records);

		var parallel = await Written.WorkbookAsync(async (book, a) =>
		{
			var sheet = new HandSheet(book, a);
			var written = 0;
			while (sheet.Sink.Entry is null)
			{
				await sheet.WriteAsync(records[written++]);
			}

			var entry = (ZipEntryStream)sheet.Sink.Entry;
#if !NET
			var before = PackageWriter.TakesSegments(entry);
#endif
			Owner.BeginParallel(sheet.Sink, 4);

			// The entry, opened chunked, takes segments from here on where its bytes so far can end as a chunk: on .NET 8
			// and 10, deflated.
#if NET
			Assert.Equal(entry.Chunked && entry.Method == 8, PackageWriter.TakesSegments(entry));
#else
			// Elsewhere an entry that has deflated stays as it was: serial, or chunked from its start when the writer has
			// threads (the chunked suite mode).
			Assert.Equal(before, PackageWriter.TakesSegments(entry));
#endif
			Assert.True(Owner.ReadyForWorkers(sheet.Sink));
			Owner.BeginWorkers(sheet.Sink);
			Assert.True(await sheet.AppendAsync(ChunkOf(serial, written + 1, 20)));
			await sheet.WriteAsync(records.Skip(written + 20).ToArray());
			Owner.EndParallel(sheet.Sink);
			await sheet.DisposeAsync();
		}, async, options);

		Assert.Equal(Written.Entries(serial), Written.Entries(parallel));
	}

	[Fact]
	public async Task DiscardsChunksAndReleasesWorkersOnAnyThread()
	{
		await Written.WorkbookAsync(async (book, a) =>
		{
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync([Header, Record(0)]);
			Begin(sheet.Sink);
			var worker = await Task.Run(() => (SheetChunkSink)Owner.CreateWorker(sheet.Sink));
			var chunk = Poison();
			await Task.Run(() => Owner.DiscardChunk(sheet.Sink, chunk));
			Assert.True(chunk.Xml.IsEmpty);
			Owner.DiscardChunk(sheet.Sink, chunk);
			Owner.EndParallel(sheet.Sink);

			// A worker may be released after the write ends.
			await Task.Run(() => Owner.ReleaseWorker(sheet.Sink, worker));
			Assert.Equal((0, 0), (book.ChunksAppended, book.ChunksRedone));
			await sheet.DisposeAsync();
		}, false, Options());
	}

	[Fact]
	public async Task MergeCellsRefusesAWorkersWriter()
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync([Header, Record(0)]);
			Begin(sheet.Sink);
			var worker = Owner.WriterOf((SheetChunkSink)Owner.CreateWorker(sheet.Sink));
			var e = Assert.Throws<NotSupportedException>(() => worker.MergeCells("A3:B3"));
			Assert.Equal(
				"MergeCells can't merge cells from a ParallelWrite callback, whose CsvWriter writes a batch of the sheet's rows on a worker thread. Merge the cells with the sheet's own writer, after the ParallelWrite call, or write the sheet serially (WriteRecords, or a loop over the items with the sheet's writer).",
				e.Message);

			// The sheet's own writer refuses too while the parallel write runs (a callback gets it for the items written
			// serially), and merges once it has ended.
			Assert.Equal(e.Message, Assert.Throws<NotSupportedException>(() => sheet.Writer.MergeCells("A3:B3")).Message);
			Owner.EndParallel(sheet.Sink);
			sheet.Writer.MergeCells("A3:B3");
			await sheet.DisposeAsync();
		}, false, Options());

		Assert.Contains("<mergeCell ref=\"A3:B3\"/>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
	}

	/// <summary>
	/// Checks that a sink refuses a chunk without changing the sheet: its state as it was, the chunk released, only
	/// <see cref="ExcelWorkbookWriter.ChunksRedone"/> counted; and that its batch, written again serially, gives the package
	/// of the same calls without the chunk, byte for byte, and the parts of the serial write.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="options">The workbook's options.</param>
	/// <param name="before">The records before the workers start.</param>
	/// <param name="between">The records written serially once they have, before the chunk.</param>
	/// <param name="chunk">Makes the chunk, refused.</param>
	/// <param name="batch">The chunk's batch, written again serially.</param>
	/// <param name="after">The records after it.</param>
	/// <param name="maxRows">The rows a sheet holds, or 0 for Excel's.</param>
	/// <param name="last">Writes <paramref name="lastRecord"/> after the others, or <see langword="null"/> for none.</param>
	/// <param name="lastRecord">The record that <paramref name="last"/> writes.</param>
	/// <returns>A task that completes when the workbooks are checked.</returns>
	private static async Task AssertRefusedAsync(bool async, ExcelWriterOptions options, object?[][] before, object?[][] between, Func<SheetChunk> chunk, object?[][] batch, object?[][] after,
		int maxRows = 0, Func<HandSheet, object?[], Task>? last = null, object?[]? lastRecord = null)
	{
		var parallel = await WriteAsync(true);
		var reference = await WriteAsync(false);
		var serial = await SerialAsync(async, options, [.. before, .. between, .. batch, .. after], maxRows, last, lastRecord);

		Assert.Equal(reference, parallel);
		Assert.Equal(Written.Entries(serial), Written.Entries(parallel));

		Task<byte[]> WriteAsync(bool refuse) => Written.WorkbookAsync(async (book, a) =>
		{
			if (maxRows > 0)
			{
				book.MaxRows = maxRows;
			}

			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(before);
			Begin(sheet.Sink);
			await sheet.WriteAsync(between);
			if (refuse)
			{
				var state = sheet.Sink.ChunkState;
				var refused = chunk();
				Assert.False(await sheet.AppendAsync(refused));
				Assert.True(refused.Xml.IsEmpty);
				Assert.Equal(state, sheet.Sink.ChunkState);
				Assert.Equal((0, 1), (book.ChunksAppended, book.ChunksRedone));
			}

			await sheet.WriteAsync(batch);
			await sheet.WriteAsync(after);
			if (last is not null)
			{
				await last(sheet, lastRecord!);
			}

			Owner.EndParallel(sheet.Sink);
			await sheet.DisposeAsync();
		}, async, options);
	}

	/// <summary>Writes records serially, without a parallel write's calls.</summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="options">The workbook's options.</param>
	/// <param name="records">The records.</param>
	/// <param name="maxRows">The rows a sheet holds, or 0 for Excel's.</param>
	/// <param name="last">Writes <paramref name="lastRecord"/> after the others, or <see langword="null"/> for none.</param>
	/// <param name="lastRecord">The record that <paramref name="last"/> writes; the last of <paramref name="records"/> when it isn't given.</param>
	/// <returns>The workbook's bytes.</returns>
	private static Task<byte[]> SerialAsync(bool async, ExcelWriterOptions options, object?[][] records, int maxRows = 0, Func<HandSheet, object?[], Task>? last = null, object?[]? lastRecord = null) =>
		Written.WorkbookAsync(async (book, a) =>
		{
			if (maxRows > 0)
			{
				book.MaxRows = maxRows;
			}

			var sheet = new HandSheet(book, a);
			if (last is not null && lastRecord is null)
			{
				await sheet.WriteAsync(records.Take(records.Length - 1).ToArray());
				await last(sheet, records[records.Length - 1]);
			}
			else
			{
				await sheet.WriteAsync(records);
				if (last is not null)
				{
					await last(sheet, lastRecord!);
				}
			}

			await sheet.DisposeAsync();
		}, async, options);

	/// <summary>
	/// Writes a record past the sheet's last row as the row limit says: with <see cref="ExcelRowLimit.Throw"/>, its first
	/// field throws the serial write's exception, and the record is discarded; with <see cref="ExcelRowLimit.NewSheet"/>,
	/// it starts the next sheet.
	/// </summary>
	/// <param name="limit">The row limit.</param>
	/// <returns>Writes the record.</returns>
	private static Func<HandSheet, object?[], Task> ThrowsAtTheLimit(ExcelRowLimit limit) => async (sheet, record) =>
	{
		if (limit == ExcelRowLimit.NewSheet)
		{
			await sheet.WriteAsync(record);

			return;
		}

		var e = Assert.Throws<InvalidOperationException>(() => sheet.Writer.WriteField((string?)record[0]));
		Assert.Equal("The sheet Data is full at row 6, Excel's last. Set ExcelWriterOptions.RowLimit to NewSheet to go on in a new sheet.", e.Message);
	};

	/// <summary>Writes a header and records, noting after each whether the sheet is ready for workers, which must be once its entry is open at the latest.</summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="sheets">The sheet's options.</param>
	/// <returns>The records written when the sheet first was ready.</returns>
	private static async Task<int> ReadyRowsAsync(bool async, ExcelSheetOptions sheets)
	{
		var readyAt = 0;
		await Written.WorkbookAsync(async (book, a) =>
		{
			var sheet = new HandSheet(book, a);
			await sheet.WriteAsync(Header);
			for (var i = 0; readyAt == 0; i++)
			{
				await sheet.WriteAsync(LongRecord(i));
				var ready = Owner.ReadyForWorkers(sheet.Sink);
				if (sheets.Protect)
				{
					Assert.Equal(sheet.Sink.Entry is not null, ready);
				}

				readyAt = ready ? i + 1 : 0;
			}

			await sheet.DisposeAsync();
		}, async, Options(sheets: sheets));

		return readyAt;
	}

	/// <summary>A record of text of about 100 bytes, so that some hundreds of them pass a flush's 64 KB.</summary>
	/// <param name="i">Its number.</param>
	/// <returns>The record.</returns>
	private static object?[] LongRecord(int i) => ["n" + i, "c" + i, "note " + i + " " + new string('x', 80), null];

	/// <summary>A record of URIs, each a link to <see cref="Site"/> while the sheet has room.</summary>
	/// <param name="count">The URIs.</param>
	/// <returns>The record.</returns>
	private static object?[] Links(int count) => [.. Enumerable.Repeat<object?>(Site, count)];

	/// <summary>A row of one link's cell, in the first cell format a workbook adds: the link style's.</summary>
	/// <param name="count">The cells.</param>
	/// <returns>The row's XML.</returns>
	private static string LinkRow(int count) =>
		"<row>" + string.Concat(Enumerable.Repeat("<c s=\"1\" t=\"inlineStr\"><is><t>" + Site.OriginalString + "</t></is></c>", count)) + "</row>";

	/// <summary>A row of one text cell.</summary>
	/// <param name="text">Its text, which needs no escaping.</param>
	/// <returns>The row's XML.</returns>
	private static string TextRow(string text) => "<row><c t=\"inlineStr\"><is><t>" + text + "</t></is></c></row>";

	/// <summary>The number of times a text has another.</summary>
	/// <param name="text">The text.</param>
	/// <param name="part">The other.</param>
	/// <returns>The count.</returns>
	private static int CountOf(string text, string part)
	{
		var count = 0;
		for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
		{
			count++;
		}

		return count;
	}

	/// <summary>The fixed format of a workbook's one date and time with seconds, as its cell's style has it.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <returns>The fixed format.</returns>
	private static FixedFormat FixedFormatOf(byte[] workbook)
	{
		Assert.Contains("formatCode=\"yyyy-mm-dd hh:mm:ss\"", Written.Entry(workbook, "xl/styles.xml"));

		return FixedFormat.DateTime;
	}

	/// <summary>Begins a parallel write on a sheet that is ready for workers, and starts them, as a parallel write does.</summary>
	/// <param name="sink">The sheet's sink.</param>
	private static void Begin(SheetSink sink)
	{
		Owner.BeginParallel(sink, 4);
		Assert.True(Owner.ReadyForWorkers(sink));
		Owner.BeginWorkers(sink);
	}

	/// <summary>
	/// A chunk that a refusal must not have added anything of: three records, whose first has a cell, text like a number,
	/// a cell in column J, and a link.
	/// </summary>
	/// <returns>The chunk.</returns>
	private static SheetChunk Poison() =>
		Chunk("<row><c t=\"inlineStr\"><is><t>poison</t></is></c><c/><c t=\"inlineStr\"><is><t>12</t></is></c></row>", 3, 0, 0, 9, numberLike: true, links: [new ChunkLink(0, 0, "https://poison.example/")]);

	/// <summary>A chunk made by hand, its XML in a pooled array as a worker hands it over.</summary>
	/// <param name="xml">Its rows' XML, empty for none.</param>
	/// <param name="records">Its records.</param>
	/// <param name="firstCell">Its first record with a cell, or -1.</param>
	/// <param name="lastCell">Its last record with a cell, or -1.</param>
	/// <param name="lastColumn">Its last column, or -1.</param>
	/// <param name="numberLike">Whether a text cell of it starts like a number.</param>
	/// <param name="links">Its links, or <see langword="null"/>.</param>
	/// <param name="version">The view it was written with.</param>
	/// <returns>The chunk.</returns>
	private static SheetChunk Chunk(string xml, int records, int firstCell, int lastCell, int lastColumn, bool numberLike = false, List<ChunkLink>? links = null, int version = 1)
	{
		var segment = default(OwnedSegment);
		if (xml.Length > 0)
		{
			var bytes = Encoding.UTF8.GetBytes(xml);
			var array = ArrayPool<byte>.Shared.Rent(bytes.Length);
			bytes.CopyTo(array, 0);
			segment = new OwnedSegment(array, bytes.Length);
		}

		return new SheetChunk
		{
			Xml = segment,
			Records = records,
			FirstCellRecord = firstCell,
			LastCellRecord = lastCell,
			LastColumn = lastColumn,
			NumberLikeText = numberLike,
			Links = links,
			ViewVersion = version,
		};
	}

	/// <summary>
	/// The chunk a worker writes for records of a serial write: their rows cut from its sheet, from the first with a cell
	/// to the last, with what the chunk notes of them.
	/// </summary>
	/// <param name="serial">The serial write's workbook.</param>
	/// <param name="firstRow">The row of the chunk's first record.</param>
	/// <param name="records">The chunk's records.</param>
	/// <param name="sheet">The sheet's part's number, from 1.</param>
	/// <param name="version">The view it was written with.</param>
	/// <returns>The chunk.</returns>
	private static SheetChunk ChunkOf(byte[] serial, long firstRow, int records, int sheet = 1, int version = 1)
	{
		var text = Written.Entry(serial, $"xl/worksheets/sheet{sheet}.xml");
		var document = XDocument.Parse(text);
		var rows = Written.Rows(document).Where(r => r.Number >= firstRow && r.Number < firstRow + records && r.Cells.Count > 0).ToList();
		if (rows.Count == 0)
		{
			return Chunk("", records, -1, -1, -1, version: version);
		}

		var first = rows[0].Number;
		var last = rows[rows.Count - 1].Number;
		var xml = new StringBuilder();
		for (var number = first; number <= last; number++)
		{
			xml.Append(Written.RowXml(text, number));
		}

		var numberLike = rows.SelectMany(r => r.Cells).Any(c => (string?)c.Element.Attribute("t") == "inlineStr" && c.Element.Value is { Length: > 0 } value && value[0] is >= '0' and <= '9' or '+' or '-' or '.');

		return Chunk(xml.ToString(), records, (int)(first - firstRow), (int)(last - firstRow), rows.Max(r => r.Cells.Max(c => c.Column)), numberLike, LinksOf(serial, document, sheet, firstRow, records), version);
	}

	/// <summary>The links of a serial write's rows, as a chunk of them notes them: each one's record in the chunk, column and target.</summary>
	/// <param name="serial">The serial write's workbook.</param>
	/// <param name="document">Its sheet.</param>
	/// <param name="sheet">The sheet's part's number.</param>
	/// <param name="firstRow">The row of the chunk's first record.</param>
	/// <param name="records">The chunk's records.</param>
	/// <returns>The links, or <see langword="null"/> for none.</returns>
	private static List<ChunkLink>? LinksOf(byte[] serial, XDocument document, int sheet, long firstRow, int records)
	{
		var hyperlinks = document.Root!.Element(Main + "hyperlinks")?.Elements(Main + "hyperlink").ToList();
		if (hyperlinks is null)
		{
			return null;
		}

		var relationships = XDocument.Parse(Written.Entry(serial, $"xl/worksheets/_rels/sheet{sheet}.xml.rels")).Root!.Elements(Relationships + "Relationship")
			.ToDictionary(r => (string)r.Attribute("Id")!, r => (string)r.Attribute("Target")!);
		List<ChunkLink>? links = null;
		foreach (var hyperlink in hyperlinks)
		{
			Assert.True(CellReference.TryParse(Encoding.ASCII.GetBytes((string)hyperlink.Attribute("ref")!), out var column, out var row));
			if (row >= firstRow && row < firstRow + records)
			{
				(links ??= []).Add(new ChunkLink((int)(row - firstRow), column, relationships[(string)hyperlink.Attribute(RelationshipIds + "id")!]));
			}
		}

		return links;
	}

	/// <summary>A sheet written by hand: its workbook's writer, its own writer and sink, and records written as the mode says.</summary>
	private sealed class HandSheet
	{
		/// <summary>Adds a sheet to a workbook.</summary>
		/// <param name="book">The workbook's writer.</param>
		/// <param name="async">Whether to write asynchronously.</param>
		/// <param name="name">The sheet's name.</param>
		public HandSheet(ExcelWorkbookWriter book, bool async, string name = "Data")
		{
			Async = async;
			Writer = book.AddSheet(name);
			Sink = book.OpenSheet!;
		}

		/// <summary>Whether to write asynchronously.</summary>
		public bool Async { get; }

		/// <summary>The sheet's writer.</summary>
		public CsvWriter Writer { get; }

		/// <summary>The sheet's sink.</summary>
		public SheetSink Sink { get; }

		/// <summary>Writes records: each field as its type writes it (text, a URI, a date), a null one empty.</summary>
		/// <param name="records">The records.</param>
		/// <returns>A task that completes when they are written.</returns>
		public async Task WriteAsync(params object?[][] records)
		{
			foreach (var record in records)
			{
				foreach (var field in record)
				{
					switch (field)
					{
						case Uri uri:
							Writer.WriteField(uri);

							break;
						case DateTime date:
							Writer.WriteField(date);

							break;
						default:
							Writer.WriteField((string?)field);

							break;
					}
				}

				await Written.EndRecordAsync(Writer, Async);
			}
		}

		/// <summary>Hands the sink a worker's chunk, as a parallel write does, sync or async.</summary>
		/// <param name="chunk">The chunk.</param>
		/// <returns>Whether the sink appended it.</returns>
		public async Task<bool> AppendAsync(SheetChunk chunk) => Async ? await Owner.WriteChunkAsync(Sink, chunk, CancellationToken.None) : Owner.WriteChunk(Sink, chunk);

		/// <summary>Disposes the sheet's writer, which completes the sheet.</summary>
		/// <returns>A task that completes when it is disposed.</returns>
		public Task DisposeAsync() => Written.DisposeAsync(Writer, Async);
	}

	/// <summary>
	/// The members of core's cell sink that a parallel write calls on a sheet's sink, bound by reflection: CsvHelper shows
	/// its <c>CsvCellSink</c> to CsvHelper.Excel, not to these tests, which can't name its members.
	/// </summary>
	private static class Owner
	{
		/// <summary>Core's cell sink, <see cref="SheetSink"/>'s base.</summary>
		private static readonly Type CellSink = typeof(SheetSink).BaseType!;

		/// <summary><c>SupportsParallel</c>.</summary>
		public static readonly Func<SheetSink, bool> SupportsParallel = Bind<Func<SheetSink, bool>>("get_SupportsParallel");

		/// <summary><c>ReadyForWorkers</c>.</summary>
		public static readonly Func<SheetSink, bool> ReadyForWorkers = Bind<Func<SheetSink, bool>>("get_ReadyForWorkers");

		/// <summary><c>BeginParallel(int)</c>.</summary>
		public static readonly Action<SheetSink, int> BeginParallel = Bind<Action<SheetSink, int>>("BeginParallel", typeof(int));

		/// <summary><c>BeginWorkers()</c>.</summary>
		public static readonly Action<SheetSink> BeginWorkers = Bind<Action<SheetSink>>("BeginWorkers");

		/// <summary><c>CreateWorker()</c>.</summary>
		public static readonly Func<SheetSink, object> CreateWorker = Bind<Func<SheetSink, object>>("CreateWorker");

		/// <summary><c>WriteChunk(object)</c>.</summary>
		public static readonly Func<SheetSink, object, bool> WriteChunk = Bind<Func<SheetSink, object, bool>>("WriteChunk", typeof(object));

		/// <summary><c>WriteChunkAsync(object, CancellationToken)</c>.</summary>
		public static readonly Func<SheetSink, object, CancellationToken, ValueTask<bool>> WriteChunkAsync =
			Bind<Func<SheetSink, object, CancellationToken, ValueTask<bool>>>("WriteChunkAsync", typeof(object), typeof(CancellationToken));

		/// <summary><c>DiscardChunk(object)</c>.</summary>
		public static readonly Action<SheetSink, object> DiscardChunk = Bind<Action<SheetSink, object>>("DiscardChunk", typeof(object));

		/// <summary><c>ReleaseWorker(CsvCellSink)</c>.</summary>
		public static readonly Action<SheetSink, SheetChunkSink> ReleaseWorker = Bind<Action<SheetSink, SheetChunkSink>>("ReleaseWorker", CellSink);

		/// <summary><c>EndParallel()</c>.</summary>
		public static readonly Action<SheetSink> EndParallel = Bind<Action<SheetSink>>("EndParallel");

		/// <summary><c>DiscardRecord()</c>, which a parallel write's serial records call for a record that fails.</summary>
		public static readonly Action<SheetSink> DiscardRecord = Bind<Action<SheetSink>>("DiscardRecord");

		/// <summary>A writer over a worker's sink, as a parallel write makes one for each worker (through core's internal constructor).</summary>
		/// <param name="worker">The worker's sink.</param>
		/// <returns>The writer, which isn't disposed: a worker's sink is never flushed or completed.</returns>
		public static CsvWriter WriterOf(SheetChunkSink worker) =>
			(CsvWriter)typeof(CsvWriter).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, [CellSink, typeof(CsvWriterOptions)], null)!.Invoke([worker, null]);

		/// <summary>Binds a public member of <see cref="SheetSink"/> to a delegate whose first parameter is the sink.</summary>
		/// <typeparam name="T">The delegate's type.</typeparam>
		/// <param name="name">The method's name, <c>get_</c> and the name for a property.</param>
		/// <param name="parameters">The method's parameters' types.</param>
		/// <returns>The delegate.</returns>
		private static T Bind<T>(string name, params Type[] parameters)
			where T : Delegate =>
			(T)Delegate.CreateDelegate(typeof(T), typeof(SheetSink).GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, parameters, null)!);
	}
}
