using System.Globalization;
using System.Text;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// A parallel write's worker sink (<see cref="SheetChunkSink"/>, Phase 9's step 10), driven directly: the chunk's rows and
/// counts, the length it reports, a record discarded, the view read as each chunk begins, each reason to write a chunk
/// again, at the field where the serial write adds a cell format or the link style or throws (through
/// <see cref="ChunkDrift"/>, which checks the rest against the serial write too), and its release.
/// </summary>
/// <param name="output">Shows what the allocation test measured.</param>
public class SheetChunkSinkTests(ITestOutputHelper output)
{
	/// <summary>Phase 4's look, without links, and records of any number of fields.</summary>
	private static readonly ExcelWriterOptions Plain = Written.Phase4() with { Csv = new CsvWriterOptions { ValidateFieldCount = false } };

	/// <summary>The targeted redo cases of <see cref="FlagsWhereTheSerialWriteAddsOrThrows"/>.</summary>
	public static TheoryData<string> Cases => new()
	{
		"fixed format", "translation", "column style", "unlocked", "placeholder", "column start", "link style", "styled link", "long text",
		"long number", "date before 1900", "negative duration", "past XFD", "text by default",
	};

	[Fact]
	public void WritesRowsWithoutReferencesAndCountsWhatTheOwnerAddsUp()
	{
		using var sheet = new Sheet();
		sheet.Row("A", "B", "C");
		sheet.Freeze();
		var worker = sheet.Worker;
		worker.BeginChunk(5);
		Fields(worker, ("", typeof(string)));
		Fields(worker, ("a", typeof(string)), ("", typeof(int)), ("3", typeof(int)));
		Fields(worker, ("", typeof(string)), ("", typeof(string)));
		Fields(worker, ("", typeof(string)), ("007", typeof(string)));
		Fields(worker, ("", typeof(string)));
		var chunk = (SheetChunk)worker.TakeChunk(out var length);
		try
		{
			Assert.Equal("<row><c t=\"inlineStr\"><is><t>a</t></is></c><c/><c><v>3</v></c></row><row/><row><c/><c t=\"inlineStr\"><is><t>007</t></is></c></row>", Text(chunk));
			Assert.Equal(chunk.Xml.Length, length);
			Assert.Equal(5, chunk.Records);
			Assert.Equal(1, chunk.FirstCellRecord);
			Assert.Equal(3, chunk.LastCellRecord);
			Assert.Equal(2, chunk.LastColumn);
			Assert.True(chunk.NumberLikeText);
			Assert.Null(chunk.Links);
			Assert.Equal(SheetChunkRedo.None, chunk.Redo);
			Assert.Equal(5, chunk.FirstRecord);
			Assert.Equal(1, chunk.ViewVersion);
		}
		finally
		{
			chunk.Release();
		}
	}

	[Fact]
	public void TakesAChunkWithoutACellAsNoXml()
	{
		using var sheet = new Sheet();
		sheet.Row("A");
		sheet.Freeze();
		sheet.Worker.BeginChunk(1);
		Fields(sheet.Worker, ("", typeof(string)));
		Fields(sheet.Worker, ("", typeof(int)), ("", typeof(string)));
		var chunk = (SheetChunk)sheet.Worker.TakeChunk(out var length);

		Assert.True(chunk.Xml.IsEmpty);
		Assert.Equal(0, length);
		Assert.Equal(2, chunk.Records);
		Assert.Equal(-1, chunk.FirstCellRecord);
		Assert.Equal(-1, chunk.LastCellRecord);
		Assert.Equal(-1, chunk.LastColumn);
		Assert.False(chunk.NumberLikeText);
		chunk.Release();
	}

	[Fact]
	public void ReportsTheTestsChunkLengthInPlaceOfItsXmls()
	{
		using var sheet = new Sheet();
		sheet.Book.ParallelChunkLength = 7;
		sheet.Row("A");
		sheet.Freeze();
		sheet.Worker.BeginChunk(1);
		Fields(sheet.Worker, ("some text", typeof(string)));
		var chunk = (SheetChunk)sheet.Worker.TakeChunk(out var length);

		Assert.Equal(7, length);
		Assert.True(chunk.Xml.Length > 7);
		chunk.Release();
	}

	[Fact]
	public void DiscardsARecordWithTheEmptyRowsBeforeItAndItsLinks()
	{
		using var sheet = new Sheet(Plain with { Links = ExcelLinks.All });
		sheet.Row("A", "B");
		// The link style, which the worker can't add.
		sheet.Row(new Uri("https://example.com/first"));
		sheet.Freeze();
		var worker = sheet.Worker;
		worker.BeginChunk(2);
		Fields(worker, ("https://example.com/a", typeof(Uri)));
		Fields(worker, ("", typeof(string)));
		worker.WriteField("b"u8, typeof(string));
		worker.WriteField("https://example.com/c"u8, typeof(Uri));
		worker.DiscardRecord();
		Fields(worker, ("d", typeof(string)));
		var chunk = (SheetChunk)worker.TakeChunk(out _);
		try
		{
			var link = $"<c s=\"{sheet.Freeze().LinkStyle}\" t=\"inlineStr\"><is><t>https://example.com/a</t></is></c>";
			Assert.Equal($"<row>{link}</row><row/><row><c t=\"inlineStr\"><is><t>d</t></is></c></row>", Text(chunk));
			Assert.Equal(3, chunk.Records);
			Assert.Equal([new ChunkLink(0, 0, "https://example.com/a")], chunk.Links!);
		}
		finally
		{
			chunk.Release();
		}

		// A chunk whose only links were discarded has none.
		worker.BeginChunk(5);
		worker.WriteField("https://example.com/e"u8, typeof(Uri));
		worker.DiscardRecord();
		Fields(worker, ("f", typeof(string)));
		var unlinked = (SheetChunk)worker.TakeChunk(out _);

		Assert.Null(unlinked.Links);
		unlinked.Release();
	}

	[Fact]
	public void TakesWholeRecordsOnly()
	{
		using var sheet = new Sheet();
		sheet.Row("A", "B");
		sheet.Freeze();
		sheet.Worker.BeginChunk(1);
		Fields(sheet.Worker, ("1", typeof(int)));
		// A record core didn't end or discard.
		sheet.Worker.WriteField("2"u8, typeof(int));
		var chunk = (SheetChunk)sheet.Worker.TakeChunk(out _);

		Assert.Equal("<row><c><v>1</v></c></row>", Text(chunk));
		Assert.Equal(1, chunk.Records);
		chunk.Release();
	}

	[Fact]
	public void KeepsTheFirstReasonWhenTheRecordIsDiscarded()
	{
		using var sheet = new Sheet(Plain with { OutOfRangeDates = ExcelOutOfRangeDates.Throw });
		sheet.Row("A", "B");
		sheet.Freeze();
		var worker = sheet.Worker;
		worker.BeginChunk(1);
		Fields(worker, ("1", typeof(int)));
		// A date's cell format, which the workbook doesn't have yet.
		Assert.Throws<InvalidOperationException>(() => worker.WriteField("2024-01-15T00:00:00.0000000"u8, typeof(DateTime)));
		worker.DiscardRecord();
		// Before 1900, which throws as the options say: still the first reason.
		Assert.Throws<InvalidOperationException>(() => worker.WriteField("1899-12-31T00:00:00.0000000"u8, typeof(DateTime)));
		worker.DiscardRecord();
		Fields(worker, ("3", typeof(int)));
		var chunk = (SheetChunk)worker.TakeChunk(out _);

		Assert.Equal(SheetChunkRedo.Style, chunk.Redo);
		Assert.Equal(2, chunk.Records);
		chunk.Release();
	}

	[Fact]
	public void ReadsTheLatestViewAsEachChunkBegins()
	{
		using var sheet = new Sheet();
		sheet.Row("A");
		Assert.Throws<InvalidOperationException>(() => sheet.Worker.BeginChunk(1));
		sheet.Freeze();
		var worker = sheet.Worker;
		worker.BeginChunk(1);
		Assert.Throws<InvalidOperationException>(() => worker.WriteField("2024-01-15T00:00:00.0000000"u8, typeof(DateTime)));
		var first = (SheetChunk)worker.TakeChunk(out _);
		Assert.Equal(SheetChunkRedo.Style, first.Redo);
		Assert.Equal(1, first.ViewVersion);
		first.Release();

		// The serial write adds the date's cell format, and the owner freezes the view again: the next chunk has it.
		sheet.Row(new DateTime(2024, 1, 15));
		var view = sheet.Freeze();
		worker.BeginChunk(2);
		Fields(worker, ("2024-01-16T00:00:00.0000000", typeof(DateTime)));
		// A view published while a chunk is written is read as the next one begins.
		sheet.Freeze();
		Fields(worker, ("2024-01-17T00:00:00.0000000", typeof(DateTime)));
		var second = (SheetChunk)worker.TakeChunk(out _);

		Assert.Equal(SheetChunkRedo.None, second.Redo);
		Assert.Equal(2, second.ViewVersion);
		Assert.True(view.Styles.TryIndexOf(FixedFormat.Date, out var date));
		Assert.Equal($"<row><c s=\"{date}\"><v>45307</v></c></row><row><c s=\"{date}\"><v>45308</v></c></row>", Text(second));
		second.Release();
		worker.BeginChunk(4);
		Assert.Equal(3, ((SheetChunk)worker.TakeChunk(out _)).ViewVersion);
	}

	[Fact]
	public void WritesAUriAsTextWhenTheViewSaysTheSheetIsFullOfLinks()
	{
		using var sheet = new Sheet(Plain with { Links = ExcelLinks.Web });
		sheet.Row("A");
		var frozen = sheet.Sink.Freeze(1);
		// As the view of a sheet of 65,530 links: Freeze's own, but full.
		sheet.Views.Publish(new SheetView
		{
			Options = frozen.Options,
			Culture = frozen.Culture,
			Styles = frozen.Styles,
			LinkStyle = frozen.LinkStyle,
			LinksFull = true,
			ColumnTypes = frozen.ColumnTypes,
			ColumnKinds = frozen.ColumnKinds,
			Version = 1,
		});
		sheet.Worker.BeginChunk(1);
		Fields(sheet.Worker, ("https://example.com/a", typeof(Uri)));
		var chunk = (SheetChunk)sheet.Worker.TakeChunk(out _);

		Assert.Equal(SheetChunkRedo.None, chunk.Redo);
		Assert.Null(chunk.Links);
		Assert.Equal("<row><c t=\"inlineStr\"><is><t>https://example.com/a</t></is></c></row>", Text(chunk));
		chunk.Release();
	}

	[Theory]
	[MemberData(nameof(Cases))]
	public void FlagsWhereTheSerialWriteAddsOrThrows(string name)
	{
		var (sheet, expected) = Case(name);
		var tally = new SortedDictionary<string, int>(StringComparer.Ordinal);

		Assert.Null(ChunkDrift.Check(sheet, tally));
		output.WriteLine(string.Join(", ", tally.Select(pair => $"{pair.Key}: {pair.Value}")));
		Assert.Equal(expected, tally);
	}

	[Fact]
	public async Task IsNeverFlushedOrCompleted()
	{
		var worker = new SheetChunkSink(new SheetViewSlot());

		Assert.Throws<NotSupportedException>(worker.Flush);
		await Assert.ThrowsAsync<NotSupportedException>(() => worker.FlushAsync(CancellationToken.None).AsTask());
		Assert.Throws<NotSupportedException>(worker.Complete);
		await Assert.ThrowsAsync<NotSupportedException>(() => worker.CompleteAsync().AsTask());
		worker.Release();
	}

	[Fact]
	public void ReleasesOnAnotherThreadOnce()
	{
		using var sheet = new Sheet();
		sheet.Row("A");
		sheet.Freeze();
		var worker = new SheetChunkSink(sheet.Views);
		worker.BeginChunk(1);
		Fields(worker, ("a", typeof(string)));
		var chunk = (SheetChunk)worker.TakeChunk(out _);
		var thread = new Thread(() =>
		{
			worker.Release();
			worker.Release();
		});
		thread.Start();
		thread.Join();
		worker.Release();

		// The chunk's XML is its own, not the worker's buffer.
		Assert.Equal("<row><c t=\"inlineStr\"><is><t>a</t></is></c></row>", Text(chunk));
		chunk.Release();
		chunk.Release();
	}

	[Fact]
	public void CopiesAColumnsFormatWithCachesOfItsOwn()
	{
		var styles = new StyleRegistry();
		var format = new ColumnFormat("dd.MM.yyyy", new CultureInfo("de-DE"), CultureInfo.InvariantCulture);
		var copy = format.ForWorker();
		Assert.NotNull(copy.Translation(CellKind.DateTime));

		Assert.Equal(format.Format, copy.Format);
		Assert.Same(format.Provider, copy.Provider);
		Assert.True(copy.HasCulture);
		Assert.Same(format.Numbers, copy.Numbers);
		Assert.False(copy.TryStyle(styles.Snapshot(), out _));
		Assert.NotNull(format.Translation(CellKind.DateTime));
		var index = format.Style(styles);
		Assert.True(copy.TryStyle(styles.Snapshot(), out var found));
		Assert.Equal(index, found);
		// The copy keeps the one found, whatever a later snapshot has.
		Assert.True(copy.TryStyle(new StyleRegistry().Snapshot(), out found));
		Assert.Equal(index, found);
	}

	[Fact]
	public void LooksAColumnsStyleUpInTheSnapshotOnly()
	{
		var styles = new StyleRegistry();
		var column = new ColumnStyle(new ExcelColumnOptions { Format = "0.000", Wrap = true }, styles);
		var date = styles.IndexOf(FixedFormat.Date);
		var copy = new ChunkColumnStyle(column, styles.Snapshot());
		var count = styles.Count;

		Assert.True(copy.HasFormat);
		Assert.True(copy.Wrap);
		Assert.True(copy.TryOf(0, out var plain));
		Assert.Equal(column.Default, plain);
		Assert.True(copy.TryOf(date, out var dated));
		Assert.Equal(column.Default, dated);
		Assert.False(new ChunkColumnStyle(new ColumnStyle(new ExcelColumnOptions { Wrap = true }, styles), styles.Snapshot()).TryOf(date, out _));
		Assert.Equal(count + 1, styles.Count);
	}

#if NET
	[OptimizedFact]
	public void AllocatesNoMoreForEachCellThanTheSerialSink()
	{
		using var sheet = new Sheet();
		sheet.Row("A", "B", "C", "D", "E", "F", "G");
		sheet.Row(1, 1.5, new DateTime(2024, 1, 15, 10, 30, 0), true, "a", new ExcelFormula("A1"), null, 2);
		sheet.Freeze();
		var worker = sheet.Worker;
		var owner = sheet.Sink;
		var (workerFew, workerMany) = Measure(() => worker.BeginChunk(2), worker.WriteField, () => worker.EndRecord(), () => ((SheetChunk)worker.TakeChunk(out _)).Release());
		// The sheet's own sink, below the header with its widths known, as a serial write reaches it; flushed between the measures.
		var (serialFew, serialMany) = Measure(() => { }, owner.WriteField, () => owner.EndRecord(), owner.Flush);
		output.WriteLine($"300 records more allocated {workerMany - workerFew:N0} bytes through the worker and {serialMany - serialFew:N0} through the sheet's sink.");

		// 300 records more, of 2,400 cells more, allocate less than a byte for each beyond what the serial sink's do.
		Assert.True(workerMany - workerFew < serialMany - serialFew + 256, $"The worker allocated {workerFew:N0} bytes for 300 records and {workerMany:N0} for 600; the sheet's sink {serialFew:N0} and {serialMany:N0}.");
	}

	/// <summary>
	/// Measures records of typed cells, text, a formula and a gap through a sink, 300 and 600 of them: each once first,
	/// then three times, keeping the smallest, as the other allocation tests do.
	/// </summary>
	/// <param name="begin">Begins the records, outside the measure.</param>
	/// <param name="write">Writes a field.</param>
	/// <param name="end">Ends a record.</param>
	/// <param name="after">Takes or flushes the records, outside the measure.</param>
	/// <returns>The bytes for 300 records and for 600.</returns>
	private static (long Few, long Many) Measure(Action begin, FieldWriter write, Action end, Action after)
	{
		Allocated(begin, write, end, after, 300);
		Allocated(begin, write, end, after, 600);
		var few = Math.Min(Allocated(begin, write, end, after, 300), Math.Min(Allocated(begin, write, end, after, 300), Allocated(begin, write, end, after, 300)));
		var many = Math.Min(Allocated(begin, write, end, after, 600), Math.Min(Allocated(begin, write, end, after, 600), Allocated(begin, write, end, after, 600)));

		return (few, many);
	}

	/// <summary>Writes records through a sink, and measures what writing them allocated on this thread.</summary>
	/// <param name="begin">Begins the records, outside the measure.</param>
	/// <param name="write">Writes a field.</param>
	/// <param name="end">Ends a record.</param>
	/// <param name="after">Takes or flushes the records, outside the measure.</param>
	/// <param name="records">The records.</param>
	/// <returns>The bytes allocated while the records were written.</returns>
	private static long Allocated(Action begin, FieldWriter write, Action end, Action after, int records)
	{
		begin();
		var before = GC.GetAllocatedBytesForCurrentThread();
		for (var r = 0; r < records; r++)
		{
			write("12345"u8, typeof(int));
			write("1.5"u8, typeof(double));
			write("2024-01-15T10:30:00.0000000"u8, typeof(DateTime));
			write("True"u8, typeof(bool));
			write("abc"u8, typeof(string));
			write("=A1+1"u8, typeof(ExcelFormula));
			write(""u8, typeof(string));
			write("7"u8, typeof(long));
			end();
		}

		var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		after();

		return allocated;
	}
#endif

	/// <summary>Writes a record's fields through a worker and ends it.</summary>
	/// <param name="worker">The worker.</param>
	/// <param name="fields">Each field's text and type.</param>
	private static void Fields(SheetChunkSink worker, params (string Text, Type Type)[] fields)
	{
		foreach (var (text, type) in fields)
		{
			worker.WriteField(Encoding.UTF8.GetBytes(text), type);
		}

		worker.EndRecord();
	}

	/// <summary>A chunk's XML as text.</summary>
	/// <param name="chunk">The chunk.</param>
	/// <returns>The text.</returns>
	private static string Text(SheetChunk chunk) => Encoding.UTF8.GetString(chunk.Xml.Span.ToArray());

	/// <summary>A sink's <c>WriteField</c>.</summary>
	/// <param name="utf8">The field's text.</param>
	/// <param name="type">Its type.</param>
	private delegate void FieldWriter(ReadOnlySpan<byte> utf8, Type type);

	/// <summary>
	/// A targeted redo case: a sheet whose items after its prefix are each a batch of their own, and what its chunks must
	/// be: a redo where the serial write first adds or throws, then appended once the view is frozen again.
	/// </summary>
	/// <param name="name">The case.</param>
	/// <returns>The sheet and its chunks' tally.</returns>
	private static (DriftSheet Sheet, SortedDictionary<string, int> Expected) Case(string name)
	{
		var day = new DateTime(2024, 1, 15);
		var protect = Plain with { Sheets = Written.Phase4Look with { Protect = true } };
		var sheet = name switch
		{
			"fixed format" => Made(Plain, Items(["A"], [day], [day.AddDays(1)])),
			"translation" => Made(Plain, Items(["c0"], [new DriftFormatted(day, "dd.MM.yyyy", CultureInfo.InvariantCulture)], [new DriftFormatted(day.AddDays(1), "dd.MM.yyyy", CultureInfo.InvariantCulture)]),
				add: book => book.AddSheet("Data", new MemberFormats([new MemberFormat("c0", 0, -1, "dd.MM.yyyy", null)]))),
			// The date's own cell format is the workbook's (in a column without a style), and its form in the wrapped column isn't.
			"column style" => Made(Columns("c0", new ExcelColumnOptions { Wrap = true }), Items(["c0", "c1"], [null, day], [day], [day.AddDays(1)]), prefix: 2),
			// The date's cell format is the workbook's (in a column past the header's, which isn't unlocked), and its unlocked form isn't.
			"unlocked" => Made(protect, Items(["c0", "c1"], [null, null, day], [day], [day.AddDays(1)]), prefix: 2, flush: true),
			// Before the sheet's start, which registers the unlocked columns' styles: a gap's placeholder needs General's unlocked form.
			"placeholder" => Made(Plain with { Sheets = Written.Phase4Look with { Protect = true, AutoFit = 100 } }, Items(["c0", "c1"], [null, 1], [null, 2])),
			// The column's start isn't in the view, and its style is the workbook's: the worker makes it.
			"column start" => Made(Columns("c1", new ExcelColumnOptions { Format = "0.00" }), Items(["c0", "c1"], [null, "x"])),
			"link style" => Made(Plain with { Links = ExcelLinks.Web }, Items(["A"], [new Uri("https://example.com/a")], [new Uri("https://example.com/b")])),
			// The link style is the workbook's, and its form in the wrapped column isn't.
			"styled link" => Made(Columns("c0", new ExcelColumnOptions { Wrap = true }) with { Links = ExcelLinks.Web }, Items(["c0", "c1"], [null, new Uri("https://example.com/a")], [new Uri("https://example.com/b")], [new Uri("https://example.com/c")]), prefix: 2),
			"long text" => Made(Plain, Items(["A"], [new string('x', XmlOutput.MaxCellLength + 1)], ["ok"])),
			"long number" => Made(Plain with { LongNumbers = ExcelLongNumbers.Throw }, Items(["A"], [1_234_567_890_123_456_789L], [1])),
			"date before 1900" => Made(Plain with { OutOfRangeDates = ExcelOutOfRangeDates.Throw }, Items(["A"], [new DateTime(1899, 12, 31)], [1])),
			"negative duration" => Made(Plain with { OutOfRangeDates = ExcelOutOfRangeDates.Throw }, Items(["A"], [TimeSpan.FromHours(-2)], [1])),
			"past XFD" => Made(Plain, Items(["A"], [.. Enumerable.Repeat<object?>(null, CellReference.MaxColumns), "x"], [1])),
			"text by default" => Made(Plain, Items(["A"], [1_234_567_890_123_456_789L], [new DateTime(1899, 12, 31)], [TimeSpan.FromHours(-2)])),
			_ => throw new ArgumentOutOfRangeException(nameof(name), name, "No such case."),
		};

		var expected = new SortedDictionary<string, int>(StringComparer.Ordinal);
		switch (name)
		{
			case "column start":
				expected["chunks: appended"] = 1;

				break;
			case "text by default":
				expected["chunks: appended"] = 3;

				break;
			case "long text" or "long number" or "date before 1900" or "negative duration" or "past XFD":
				expected["chunks: redone, Error"] = 1;
				expected["chunks: appended"] = 1;

				break;
			default:
				expected[name == "link style" ? "chunks: redone, LinkStyle" : "chunks: redone, Style"] = 1;
				expected["chunks: appended"] = 1;
				expected["views: frozen again"] = 1;

				break;
		}

		return (sheet, expected);
	}

	/// <summary>A targeted case's sheet, each of whose items after its prefix is a batch.</summary>
	/// <param name="options">The options.</param>
	/// <param name="items">The items, the header's first.</param>
	/// <param name="prefix">The items the owner writes before the view is frozen.</param>
	/// <param name="flush">Whether the owner flushes after them.</param>
	/// <param name="add">Adds the sheet, or <see langword="null"/> for a sheet named Data.</param>
	/// <returns>The sheet.</returns>
	private static DriftSheet Made(ExcelWriterOptions options, List<DriftItem> items, int prefix = 1, bool flush = false, Func<ExcelWorkbookWriter, CsvWriter>? add = null) =>
		new() { Name = string.Join("; ", items), Options = options, Items = items, Prefix = prefix, Flush = flush, Add = add ?? (book => book.AddSheet("Data")), MaxBatch = 1 };

	/// <summary>Phase 4's look, without links, with options for a column.</summary>
	/// <param name="column">The column's header name.</param>
	/// <param name="options">Its options.</param>
	/// <returns>The options.</returns>
	private static ExcelWriterOptions Columns(string column, ExcelColumnOptions options) =>
		Plain with { Sheets = Written.Phase4Look with { Columns = new Dictionary<string, ExcelColumnOptions>(StringComparer.Ordinal) { [column] = options } } };

	/// <summary>Items of values: the header's names, then the records.</summary>
	/// <param name="header">The header's names.</param>
	/// <param name="records">The records' values.</param>
	/// <returns>The items.</returns>
	private static List<DriftItem> Items(string[] header, params object?[][] records) =>
		[DriftItem.Values([.. header]), .. records.Select(DriftItem.Values)];

	/// <summary>A workbook of one sheet, whose owner writes records, and a worker over the views the tests freeze.</summary>
	private sealed class Sheet : IDisposable
	{
		/// <summary>The package.</summary>
		private readonly MemoryStream stream = new();

		/// <summary>The last view's version.</summary>
		private int version;

		/// <summary>Creates the workbook and its sheet.</summary>
		/// <param name="options">The options, or <see langword="null"/> for <see cref="Plain"/>.</param>
		public Sheet(ExcelWriterOptions? options = null)
		{
			Book = ExcelWorkbookWriter.Create(stream, options ?? Plain, leaveOpen: true);
			Owner = Book.AddSheet("Data");
			Worker = new SheetChunkSink(Views);
		}

		/// <summary>The workbook's writer.</summary>
		public ExcelWorkbookWriter Book { get; }

		/// <summary>The sheet's writer.</summary>
		public CsvWriter Owner { get; }

		/// <summary>The sheet's sink.</summary>
		public SheetSink Sink => Book.OpenSheet!;

		/// <summary>The views the worker reads.</summary>
		public SheetViewSlot Views { get; } = new();

		/// <summary>The worker.</summary>
		public SheetChunkSink Worker { get; }

		/// <summary>Freezes the sheet's view and publishes it, as the owner does.</summary>
		/// <returns>The view.</returns>
		public SheetView Freeze()
		{
			var view = Sink.Freeze(++version);
			Views.Publish(view);

			return view;
		}

		/// <summary>Writes a record through the owner.</summary>
		/// <param name="values">Its values.</param>
		public void Row(params object?[] values)
		{
			foreach (var value in values)
			{
				DriftItem.Writer(value)(Owner);
			}

			Owner.EndRecord();
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Worker.Release();
			Owner.Dispose();
			Book.Dispose();
			stream.Dispose();
		}
	}
}
