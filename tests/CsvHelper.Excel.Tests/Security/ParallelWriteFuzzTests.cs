using System.Globalization;
using System.IO.Compression;
using System.Text;
using CsvHelper.Excel.Tests.Api;
using CsvHelper.Excel.Tests.Writing;
using Xunit.Abstractions;
using static CsvHelper.Excel.Tests.Security.WriterFuzz;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// A record of every kind of member the parallel writing fuzzer writes: text, whole and exact numbers, a double, a boolean,
/// dates, a duration, an offset, a link, a member's format and two columns' own, and .NET's newer dates; its text's getter
/// fails when the record was made to, as user code that a record writer calls may.
/// </summary>
[CsvRecord]
public partial class ParallelFuzzRecord
{
	/// <summary>The text.</summary>
	private string? text;

	/// <summary>The message of the exception the text's getter throws, or <see langword="null"/>.</summary>
	private string? failure;

	/// <summary>Text, or no cell; its getter throws when the record was made to fail (<see cref="Fail"/>).</summary>
	public string? Text
	{
		get => failure is null ? text : throw new InvalidOperationException(failure);
		set => text = value;
	}

	/// <summary>A whole number, of more than 15 digits now and then.</summary>
	public long Whole { get; set; }

	/// <summary>A decimal.</summary>
	public decimal Exact { get; set; }

	/// <summary>A double, not a number now and then.</summary>
	public double Number { get; set; }

	/// <summary>A boolean.</summary>
	public bool Flag { get; set; }

	/// <summary>A date: at midnight, in whole seconds or with milliseconds; before 1900 now and then.</summary>
	public DateTime When { get; set; }

	/// <summary>A duration, negative now and then.</summary>
	public TimeSpan Took { get; set; }

	/// <summary>A date with its offset.</summary>
	public DateTimeOffset Moment { get; set; }

	/// <summary>A link of each kind, or no cell.</summary>
	public Uri? Link { get; set; }

	/// <summary>A date in its member's format, which the sheet translates to Excel's.</summary>
	[CsvFormat("yyyy-MM-dd HH:mm")]
	public DateTime Formatted { get; set; }

	/// <summary>A decimal in its column's own format.</summary>
	[ExcelColumn(Format = "#,##0.00")]
	public decimal Amount { get; set; }

	/// <summary>Text in a column that wraps, or no cell.</summary>
	[ExcelColumn(Wrap = true)]
	public string? Wrapped { get; set; }

#if NET
	/// <summary>A day.</summary>
	public DateOnly Day { get; set; }

	/// <summary>A time of day.</summary>
	public TimeOnly Time { get; set; }
#endif

	/// <summary>Makes the text's getter throw an <see cref="InvalidOperationException"/>.</summary>
	/// <param name="message">The exception's message.</param>
	internal void Fail(string message) => failure = message;
}

/// <summary>
/// Seeded property tests of the parallel write methods (Phase 9's steps 9 and 10): sheets of random records, through
/// <see cref="CsvWriter.ParallelWriteRecords{T}(IEnumerable{T}, int)"/>, and of random rows of fields, through
/// <see cref="CsvWriter.ParallelWrite{T}(IEnumerable{T}, Action{CsvWriter, T}, int)"/>, synchronously, asynchronously and
/// from asynchronous sequences; at random degrees, batch sizes (<see cref="ExcelWorkbookWriter.ParallelChunkLength"/>),
/// chunk sizes (<see cref="ExcelWorkbookWriter.ChunkSize"/>), <see cref="ExcelWriterOptions.CompressionThreads"/> and
/// compressions; with random options (strings, links, long numbers, early dates, offsets, the row limit at a few hundred
/// rows, a culture, records of any length) and looks (titles, a bold or frozen header, frozen columns, filters, tables,
/// auto-fit, protection, columns' formats and wrap). Each sheet is written serially too, with the same options, and the
/// two must agree: the same exception, if one ended the write, by its type and message; the same record count; the same
/// parts, inflated; on net8.0 and net10.0, when both deflated their streamed parts on threads, the same bytes; and no
/// buffer or segment held after the writer is disposed. One input in five plants an error at a random record: a text
/// too long, a long number or an early date where the options throw, a field past XFD, or an exception of user code.
/// <c>CSVHELPER_EXCEL_FUZZ_PARALLEL</c> sets the number of inputs, and <c>CSVHELPER_EXCEL_FUZZ_SEED</c> the first seed,
/// so that a failure can be replayed alone. The test runs alone (<see cref="TimedCollection"/>), so that its workers have
/// the processor, and an input that takes more than a minute, which only a write that waits forever would, ends it.
/// </summary>
/// <param name="output">Shows what the inputs did.</param>
[Collection(TimedCollection.Name)]
public class ParallelWriteFuzzTests(ITestOutputHelper output)
{
	/// <summary>The number of inputs a run writes, unless <c>CSVHELPER_EXCEL_FUZZ_PARALLEL</c> says otherwise.</summary>
	private const int DefaultInputs = 50;

	/// <summary>The columns of Excel's widest sheet: a field after them is past XFD.</summary>
	private const int MaxColumns = 16_384;

	/// <summary>How long one input may take: far longer than any write of its size, so only a write that waits forever reaches it.</summary>
	private static readonly TimeSpan TimeLimit = TimeSpan.FromMinutes(1);

	/// <summary>The pieces of text fields: XML's characters, escapes, line breaks, letters past ASCII, a surrogate pair and a lone one.</summary>
	private static readonly string[] TextPieces =
	[
		"a", "Zz", " ", "\n", "\r\n", "&", "<", ">", "\"", "_x0041_", "=A1", "1.5", "007", "TRUE", "http://example.com/", Units(0xE9),
		Units(0x65E5, 0x672C), Units(0xD83D, 0xDE00), Units(0xD800),
	];

	[Fact]
	public async Task WritesRandomSheetsInParallelAsSerially()
	{
		var inputs = Setting("CSVHELPER_EXCEL_FUZZ_PARALLEL", DefaultInputs);
		var first = Setting("CSVHELPER_EXCEL_FUZZ_SEED", 1);
		var failures = new List<string>();
		var tally = new SortedDictionary<string, int>(StringComparer.Ordinal);
		for (var seed = first; seed < first + inputs && failures.Count < MaxFailures; seed++)
		{
			var input = Input.Of(seed);
			var check = Task.Run(() => CheckAsync(input, tally));
			if (await Task.WhenAny(check, Task.Delay(TimeLimit)) != check)
			{
				failures.Add($"Seed {seed} ({input}): not done in {TimeLimit.TotalSeconds.ToString(CultureInfo.InvariantCulture)} seconds, a write that waits forever.");

				break;
			}

			try
			{
				if (await check is { } problem)
				{
					failures.Add($"Seed {seed} ({input}): {problem}");
				}
			}
			catch (Exception e)
			{
				failures.Add($"Seed {seed} ({input}): {Describe(e)}");
			}
		}

		output.WriteLine($"Seeds {first} to {first + inputs - 1}:");
		foreach (var pair in tally)
		{
			output.WriteLine($"  {pair.Key}: {pair.Value}");
		}

		AssertNone(failures, "CSVHELPER_EXCEL_FUZZ_PARALLEL");
	}

	/// <summary>Writes an input's sheet serially and in parallel, and compares the two.</summary>
	/// <param name="input">The input.</param>
	/// <param name="tally">Takes what the writes did.</param>
	/// <returns>A task whose result is what differs, or <see langword="null"/>.</returns>
	private static async Task<string?> CheckAsync(Input input, SortedDictionary<string, int> tally)
	{
		var serial = await WriteAsync(input, 0);
		var parallel = await WriteAsync(input, input.Degree);
		if (Shown(serial.Error) != Shown(parallel.Error))
		{
			return $"the serial write {Shown(serial.Error)}, and the parallel write {Shown(parallel.Error)}";
		}

		if (serial.RecordCount != parallel.RecordCount)
		{
			return $"the parallel write counted {parallel.RecordCount} records, and the serial write {serial.RecordCount}";
		}

		if (!serial.Book.SheetNames.SequenceEqual(parallel.Book.SheetNames))
		{
			return $"the parallel write's sheets are {string.Join(", ", parallel.Book.SheetNames)}, and the serial write's {string.Join(", ", serial.Book.SheetNames)}";
		}

		if (ParallelWrites.PartsDifference(serial.Book.Bytes, parallel.Book.Bytes) is { } parts)
		{
			return "the parallel write's " + parts;
		}

#if NET
		if (serial.Book.Threads > 1 && parallel.Book.Threads > 1)
		{
			Count(tally, "bytes compared, both chunked");
			if (ParallelWrites.BytesDifference(serial.Book.Bytes, parallel.Book.Bytes) is { } bytes)
			{
				return "the parallel write's package " + bytes;
			}
		}
#endif

		foreach (var (book, which) in new[] { (serial.Book, "serial"), (parallel.Book, "parallel") })
		{
			if (book.RentedBuffers != 0 || book.HeldSegments != 0)
			{
				return $"the {which} write's zip holds {book.RentedBuffers} buffers and {book.HeldSegments} segments after it was disposed";
			}
		}

		Count(tally, input.Records ? "records" : "rows by hand");
		Count(tally, parallel.Book.ChunksAppended > 0 ? "reached the workers" : "written serially");
		if (parallel.Book.ChunksRedone > 0)
		{
			Count(tally, "a batch written again");
		}

		if (serial.Error is { } error)
		{
			Count(tally, "threw " + error.GetType().Name + (input.Planted is { } planted ? $" (planted {planted})" : ""));
		}

		return null;
	}

	/// <summary>Writes an input's sheet into a workbook, serially (degree 0) or in parallel, catching the exception that ends the write.</summary>
	/// <param name="input">The input.</param>
	/// <param name="degree">The degree of parallelism, or 0 for the serial write.</param>
	/// <returns>A task whose result is the workbook, the exception and the sheet's record count.</returns>
	private static async Task<Outcome> WriteAsync(Input input, int degree)
	{
		Exception? error = null;
		long records = 0;
		var book = await ParallelWrites.WorkbookAsync(
			async (workbook, a) =>
			{
				workbook.ChunkSize = input.ChunkSize;
				workbook.ParallelChunkLength = input.ChunkLength;
				if (input.MaxRows > 0)
				{
					workbook.MaxRows = input.MaxRows;
				}

				CsvWriter? sheet = null;
				try
				{
					sheet = input.Records ? workbook.AddSheet<ParallelFuzzRecord>("Data", input.Look) : workbook.AddSheet("Data", input.Look);
					await WriteSheetAsync(sheet, input, degree);
				}
				catch (Exception e)
				{
					error = e;
				}

				if (sheet is not null)
				{
					records = sheet.RecordCount;
					await Written.DisposeAsync(sheet, a);
				}
			},
			input.Async,
			input.Options);

		return new Outcome(book, error, records);
	}

	/// <summary>Writes an input's records or rows into its sheet: the serial write, or the parallel method it stands for.</summary>
	/// <param name="sheet">The sheet's writer.</param>
	/// <param name="input">The input.</param>
	/// <param name="degree">The degree of parallelism, or 0 for the serial write.</param>
	/// <returns>A task that completes when they are written.</returns>
	private static async Task WriteSheetAsync(CsvWriter sheet, Input input, int degree)
	{
		if (input.Records)
		{
			if (!input.Async)
			{
				if (degree == 0)
				{
					sheet.WriteRecords(input.Items);
				}
				else
				{
					sheet.ParallelWriteRecords(input.Items, degree);
				}
			}
			else if (input.Sequence)
			{
				await (degree == 0 ? sheet.WriteRecordsAsync(ParallelWrites.Yield(input.Items)) : sheet.ParallelWriteRecordsAsync(ParallelWrites.Yield(input.Items), degree));
			}
			else
			{
				await (degree == 0 ? sheet.WriteRecordsAsync(input.Items) : sheet.ParallelWriteRecordsAsync(input.Items, degree));
			}

			return;
		}

		if (degree == 0)
		{
			if (input.Async && input.Sequence)
			{
				await foreach (var row in ParallelWrites.Yield(input.Rows))
				{
					ParallelWrites.WriteSerialItem<FuzzRow>(sheet, WriteRow, row);
				}
			}
			else
			{
				foreach (var row in input.Rows)
				{
					ParallelWrites.WriteSerialItem<FuzzRow>(sheet, WriteRow, row);
				}
			}
		}
		else if (!input.Async)
		{
			sheet.ParallelWrite(input.Rows, WriteRow, degree);
		}
		else if (input.Sequence)
		{
			await sheet.ParallelWriteAsync(ParallelWrites.Yield(input.Rows), WriteRow, degree);
		}
		else
		{
			await sheet.ParallelWriteAsync(input.Rows, WriteRow, degree);
		}
	}

	/// <summary>Writes a row's fields and ends its record: the callback; a row made to fail throws halfway through its fields.</summary>
	/// <param name="writer">The writer the callback got: the sheet's, or a worker's.</param>
	/// <param name="row">The row.</param>
	private static void WriteRow(CsvWriter writer, FuzzRow row)
	{
		for (var i = 0; i < row.Fields.Length; i++)
		{
			if (row.Failure is { } failure && i == row.Fields.Length / 2)
			{
				throw new InvalidOperationException(failure);
			}

			if (row.Fields[i] is byte[] utf8)
			{
				writer.WriteField(new ReadOnlySpan<byte>(utf8));
			}
			else
			{
				writer.WriteField(row.Fields[i]);
			}
		}

		writer.EndRecord();
	}

	/// <summary>How a write ended, for a comparison.</summary>
	/// <param name="error">The exception that ended it, or <see langword="null"/>.</param>
	/// <returns>The description.</returns>
	private static string Shown(Exception? error) => error is null ? "wrote every record" : $"threw {error.GetType().FullName}: {error.Message}";

	/// <summary>A write's outcome.</summary>
	/// <param name="Book">The workbook.</param>
	/// <param name="Error">The exception that ended the write, or <see langword="null"/>.</param>
	/// <param name="RecordCount">The sheet's writer's record count after the write.</param>
	private sealed record Outcome(ParallelBook Book, Exception? Error, long RecordCount);

	/// <summary>A row the callback writes: its fields, and the message of the exception it throws halfway through them, if any.</summary>
	/// <param name="fields">The fields: values for <see cref="CsvWriter.WriteField(object)"/>, or UTF-8 bytes.</param>
	/// <param name="failure">The exception's message, or <see langword="null"/>.</param>
	private sealed class FuzzRow(object?[] fields, string? failure = null)
	{
		/// <summary>The fields.</summary>
		public object?[] Fields => fields;

		/// <summary>The exception's message, or <see langword="null"/>.</summary>
		public string? Failure => failure;
	}

	/// <summary>A seed's sheet: what it holds, its options and look, and the knobs of its parallel write.</summary>
	private sealed class Input
	{
		/// <summary>The seed.</summary>
		public int Seed { get; private init; }

		/// <summary>Whether the sheet is of records (<see cref="Items"/>), or of rows written by hand (<see cref="Rows"/>).</summary>
		public bool Records { get; private init; }

		/// <summary>The records of a sheet of records.</summary>
		public ParallelFuzzRecord[] Items { get; private init; } = [];

		/// <summary>The rows of a sheet written by hand, its header's first.</summary>
		public FuzzRow[] Rows { get; private init; } = [];

		/// <summary>The parallel write's degree.</summary>
		public int Degree { get; private init; }

		/// <summary>The length the workers report for each chunk: 0 for its XML's.</summary>
		public int ChunkLength { get; private init; }

		/// <summary>The input a chunk of a chunked part takes.</summary>
		public int ChunkSize { get; private init; }

		/// <summary>The rows a sheet holds, or 0 for Excel's.</summary>
		public int MaxRows { get; private init; }

		/// <summary>Whether the writes are asynchronous.</summary>
		public bool Async { get; private init; }

		/// <summary>Whether an asynchronous write's source is an asynchronous sequence.</summary>
		public bool Sequence { get; private init; }

		/// <summary>The workbook's options.</summary>
		public ExcelWriterOptions Options { get; private init; } = ExcelWriterOptions.Default;

		/// <summary>The sheet's look.</summary>
		public ExcelSheetOptions Look { get; private init; } = ExcelSheetOptions.Default;

		/// <summary>The error planted, and at which record, or <see langword="null"/>.</summary>
		public string? Planted { get; private init; }

		/// <summary>Makes a seed's input.</summary>
		/// <param name="seed">The seed.</param>
		/// <returns>The input.</returns>
		public static Input Of(int seed)
		{
			var random = new Random(seed);
			var records = random.Next(3) > 0;
			var count = random.Next(6) == 0 ? random.Next(0, 120) : random.Next(150, 1500);
			var edges = random.Next(3) == 0;
			var plant = count > 0 && random.Next(5) == 0
				? (records ? Pick(random, "long text", "long number", "early date", "user code") : Pick(random, "long text", "past XFD", "user code"))
				: null;
			var at = plant is null ? -1 : random.Next(count);
			var ragged = !records && (plant == "past XFD" || random.Next(4) == 0);
			var columns = random.Next(3, 13);
			var options = new ExcelWriterOptions
			{
				Strings = random.Next(5) == 0 ? ExcelStrings.Shared : ExcelStrings.Inline,
				CompressionThreads = Pick(random, 1, 1, 2, 4, -1),
				CompressionLevel = Pick(random, CompressionLevel.Fastest, CompressionLevel.Fastest, CompressionLevel.Optimal, CompressionLevel.NoCompression),
				Links = Pick(random, ExcelLinks.Web, ExcelLinks.Web, ExcelLinks.All, ExcelLinks.None),
				LongNumbers = plant == "long number" ? ExcelLongNumbers.Throw : Pick(random, ExcelLongNumbers.Text, ExcelLongNumbers.Text, ExcelLongNumbers.Number, ExcelLongNumbers.Throw),
				OutOfRangeDates = plant == "early date" ? ExcelOutOfRangeDates.Throw : Pick(random, ExcelOutOfRangeDates.Text, ExcelOutOfRangeDates.Text, ExcelOutOfRangeDates.Throw),
				DateTimeOffsets = Pick(random, ExcelDateTimeOffsets.Text, ExcelDateTimeOffsets.Utc, ExcelDateTimeOffsets.ClockTime),
				RowLimit = random.Next(2) == 0 ? ExcelRowLimit.Throw : ExcelRowLimit.NewSheet,
				Csv = new CsvWriterOptions
				{
					ValidateFieldCount = !ragged,
					FormatProvider = random.Next(5) == 0 ? CultureInfo.GetCultureInfo("de-DE") : CultureInfo.InvariantCulture,
				},
			};
			var look = new ExcelSheetOptions
			{
				Title = random.Next(4) == 0 ? "Title " + seed.ToString(CultureInfo.InvariantCulture) : null,
				BoldHeader = random.Next(3) > 0,
				FreezeHeader = random.Next(2) == 0,
				FreezeColumns = random.Next(4) == 0 ? random.Next(1, 3) : 0,
				AutoFilter = random.Next(5) == 0,
				Table = random.Next(5) == 0 ? new ExcelTableOptions() : null,
				AutoFit = Pick(random, 0, 3, 100, 100),
				Protect = random.Next(6) == 0,
				Validate = records && random.Next(8) == 0,
				Columns = !records && random.Next(3) == 0
					? new Dictionary<string, ExcelColumnOptions> { ["C1"] = new() { Format = "0.00" }, ["C2"] = new() { Wrap = true } }
					: null,
			};
			var items = records ? Enumerable.Range(0, count).Select(_ => Record(random, edges)).ToArray() : [];
			var rows = records ? [] : RowsOf(random, count, columns, ragged, edges);
			if (plant is not null)
			{
				Plant(plant, at, items, rows);
			}

			var async = random.Next(2) == 0;

			return new Input
			{
				Seed = seed,
				Records = records,
				Items = items,
				Rows = rows,
				Degree = Pick(random, 1, 2, 3, 4, 8, -1),
				ChunkLength = Pick(random, 0, 0, 1, 1 << 20, random.Next(1, 200_000)),
				ChunkSize = count < 120 && random.Next(2) == 0 ? 64 : Pick(random, 4096, 65_536, 1 << 20),
				MaxRows = random.Next(4) == 0 ? random.Next(20, 600) : 0,
				Async = async,
				Sequence = async && random.Next(2) == 0,
				Options = options,
				Look = look,
				Planted = plant is null ? null : $"{plant} at {at.ToString(CultureInfo.InvariantCulture)}",
			};
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			var text = new StringBuilder();
			text.Append(Records ? FormattableString.Invariant($"{Items.Length:N0} records") : FormattableString.Invariant($"{Rows.Length - 1:N0} rows by hand"));
			text.Append(FormattableString.Invariant($", degree {Degree}, reports {ChunkLength}, chunks of {ChunkSize}, threads {Options.CompressionThreads}, {Options.CompressionLevel}"));
			text.Append(FormattableString.Invariant($", {Options.Strings}, links {Options.Links}, long numbers {Options.LongNumbers}, early dates {Options.OutOfRangeDates}, offsets {Options.DateTimeOffsets}"));
			text.Append(FormattableString.Invariant($", {Options.RowLimit}{(MaxRows > 0 ? " at " + MaxRows.ToString(CultureInfo.InvariantCulture) : "")}, culture '{Options.Csv.FormatProvider}'{(Options.Csv.ValidateFieldCount ? "" : ", ragged")}"));
			text.Append(FormattableString.Invariant($", auto-fit {Look.AutoFit}{(Look.Title is null ? "" : ", a title")}{(Look.Table is null ? "" : ", a table")}{(Look.AutoFilter ? ", a filter" : "")}{(Look.Protect ? ", protected" : "")}{(Look.Validate ? ", validated" : "")}{(Look.Columns is null ? "" : ", columns' options")}"));
			text.Append(Async ? (Sequence ? ", async from a sequence" : ", async") : ", sync");
			if (Planted is not null)
			{
				text.Append(", planted ").Append(Planted);
			}

			return text.ToString();
		}

		/// <summary>One of some values, at random.</summary>
		/// <typeparam name="T">The values' type.</typeparam>
		/// <param name="random">The input's random numbers.</param>
		/// <param name="values">The values.</param>
		/// <returns>The value picked.</returns>
		private static T Pick<T>(Random random, params T[] values) => values[random.Next(values.Length)];

		/// <summary>Plants an error at a record or row.</summary>
		/// <param name="plant">The error: <c>long text</c>, <c>long number</c>, <c>early date</c>, <c>past XFD</c> or <c>user code</c>.</param>
		/// <param name="at">The record's index, or the row's after the header.</param>
		/// <param name="items">The records of a sheet of records.</param>
		/// <param name="rows">The rows of a sheet written by hand.</param>
		private static void Plant(string plant, int at, ParallelFuzzRecord[] items, FuzzRow[] rows)
		{
			if (items.Length > 0)
			{
				var item = items[at];
				if (plant == "long text")
				{
					item.Text = new string('y', 33_000);
				}
				else if (plant == "long number")
				{
					item.Whole = 1234567890123456789;
				}
				else if (plant == "early date")
				{
					item.When = new DateTime(1850, 6, 1);
				}
				else
				{
					item.Fail($"Planted at record {at.ToString(CultureInfo.InvariantCulture)}.");
				}

				return;
			}

			var row = rows[at + 1];
			if (plant == "long text")
			{
				var fields = row.Fields.Length > 0 ? (object?[])row.Fields.Clone() : new object?[1];
				fields[0] = new string('y', 33_000);
				rows[at + 1] = new FuzzRow(fields);
			}
			else if (plant == "past XFD")
			{
				rows[at + 1] = new FuzzRow([.. Enumerable.Range(0, MaxColumns + 1).Select(i => (object?)i)]);
			}
			else
			{
				rows[at + 1] = new FuzzRow(row.Fields, $"Planted at row {(at + 1).ToString(CultureInfo.InvariantCulture)}.");
			}
		}

		/// <summary>A random record.</summary>
		/// <param name="random">The input's random numbers.</param>
		/// <param name="edges">Whether now and then a value is at an edge: a long number, a date before 1900, a negative duration, not a number.</param>
		/// <returns>The record.</returns>
		private static ParallelFuzzRecord Record(Random random, bool edges) => new()
		{
			Text = TextOf(random),
			Whole = edges && random.Next(300) == 0 ? 1234567890123456789 : random.Next(6) == 0 ? -random.Next() : random.Next(100_000),
			Exact = random.Next(4) == 0 ? 0m : random.Next(-100_000, 100_000) / (decimal)Pick(random, 1, 10, 100, 1000),
			Number = edges && random.Next(300) == 0 ? double.NaN : (random.NextDouble() - 0.5) * Pick(random, 1.0, 1000.0, 1E+20),
			Flag = random.Next(2) == 0,
			When = DateOf(random, edges),
			Took = edges && random.Next(300) == 0 ? TimeSpan.FromHours(-1) : TimeSpan.FromSeconds(random.Next(100_000)) + TimeSpan.FromMilliseconds(random.Next(3) == 0 ? random.Next(1000) : 0),
			Moment = new DateTimeOffset(DateOf(random, false), TimeSpan.FromHours(random.Next(-12, 13))),
			Link = LinkOf(random),
			Formatted = DateOf(random, false),
			Amount = random.Next(-1_000_000, 1_000_000) / 100m,
			Wrapped = random.Next(3) == 0 ? null : "a\nb " + random.Next().ToString(CultureInfo.InvariantCulture),
#if NET
			Day = DateOnly.FromDateTime(DateOf(random, edges)),
			Time = TimeOnly.FromTimeSpan(TimeSpan.FromSeconds(random.Next(86_400))),
#endif
		};

		/// <summary>Random rows written by hand: a header of <c>C0</c>, <c>C1</c> and so on, then rows of random fields.</summary>
		/// <param name="random">The input's random numbers.</param>
		/// <param name="count">The number of rows after the header.</param>
		/// <param name="columns">The number of fields in a row.</param>
		/// <param name="ragged">Whether a row may have fewer or more fields.</param>
		/// <param name="edges">Whether now and then a value is at an edge.</param>
		/// <returns>The rows, the header first.</returns>
		private static FuzzRow[] RowsOf(Random random, int count, int columns, bool ragged, bool edges)
		{
			var rows = new FuzzRow[count + 1];
			rows[0] = new FuzzRow([.. Enumerable.Range(0, columns).Select(c => (object?)("C" + c.ToString(CultureInfo.InvariantCulture)))]);
			for (var i = 1; i <= count; i++)
			{
				var length = ragged ? random.Next(0, columns + 3) : columns;
				rows[i] = new FuzzRow([.. Enumerable.Range(0, length).Select(_ => FieldOf(random, edges))]);
			}

			return rows;
		}

		/// <summary>A random field: no value, text, a number of each type, a boolean, a date, a duration, an offset, a link, a formula, or UTF-8 bytes, valid or not.</summary>
		/// <param name="random">The input's random numbers.</param>
		/// <param name="edges">Whether now and then a value is at an edge.</param>
		/// <returns>The field's value.</returns>
		private static object? FieldOf(Random random, bool edges) => random.Next(16) switch
		{
			0 => null,
			1 or 2 => TextOf(random),
			3 => random.Next(100_000),
			4 => edges && random.Next(300) == 0 ? 12345678901234567L : (long)random.Next(),
			5 => random.Next(-100_000, 100_000) / 100m,
			6 => (random.NextDouble() - 0.5) * 1000,
			7 => random.Next(2) == 0,
			8 => DateOf(random, edges),
			9 => TimeSpan.FromSeconds(random.Next(100_000)),
			10 => new DateTimeOffset(DateOf(random, false), TimeSpan.FromHours(random.Next(-12, 13))),
			11 => LinkOf(random),
			12 => new ExcelFormula("ROW()+" + random.Next(100).ToString(CultureInfo.InvariantCulture)),
			13 => random.Next(4) == 0 ? new byte[] { (byte)'b', 0xFF, (byte)'c' } : Encoding.UTF8.GetBytes("b" + random.Next().ToString(CultureInfo.InvariantCulture)),
#if NET
			14 => DateOnly.FromDateTime(DateOf(random, edges)),
			15 => TimeOnly.FromTimeSpan(TimeSpan.FromMilliseconds(random.Next(86_400_000))),
#endif
			_ => "x",
		};

		/// <summary>Random text: none, empty, like a number, of the pieces, or plain.</summary>
		/// <param name="random">The input's random numbers.</param>
		/// <returns>The text.</returns>
		private static string? TextOf(Random random) => random.Next(10) switch
		{
			0 => null,
			1 => "",
			2 => "00" + random.Next(1000).ToString(CultureInfo.InvariantCulture),
			3 or 4 => string.Concat(Enumerable.Range(0, random.Next(1, 8)).Select(_ => Pick(random, TextPieces))),
			_ => "t" + random.Next(100_000).ToString(CultureInfo.InvariantCulture),
		};

		/// <summary>A random date: at midnight, in whole seconds or with milliseconds, from 1917 to 2082; before 1900 now and then at the edges.</summary>
		/// <param name="random">The input's random numbers.</param>
		/// <param name="edges">Whether now and then the date is before 1900.</param>
		/// <returns>The date.</returns>
		private static DateTime DateOf(Random random, bool edges)
		{
			if (edges && random.Next(300) == 0)
			{
				return new DateTime(1850, 6, 1);
			}

			var day = new DateTime(2000, 1, 1).AddDays(random.Next(-30_000, 30_000));

			return random.Next(3) switch
			{
				0 => day,
				1 => day.AddSeconds(random.Next(86_400)),
				_ => day.AddMilliseconds(random.Next(86_400_000)),
			};
		}

		/// <summary>A random link: none, a web page, a mail, a file or a relative path.</summary>
		/// <param name="random">The input's random numbers.</param>
		/// <returns>The link.</returns>
		private static Uri? LinkOf(Random random)
		{
			var number = random.Next().ToString(CultureInfo.InvariantCulture);

			return random.Next(5) switch
			{
				0 => null,
				1 => new Uri("https://example.com/" + number),
				2 => new Uri("mailto:a" + number + "@example.com"),
				3 => new Uri("file:///C:/Temp/" + number + ".txt"),
				_ => new Uri("docs/" + number + ".txt", UriKind.Relative),
			};
		}
	}
}
