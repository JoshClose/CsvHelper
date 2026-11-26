using System.Buffers;
using System.Globalization;
using System.Text;
using CsvHelper.Excel.Tests.Corpus;
using CsvHelper.Excel.Tests.Security;

namespace CsvHelper.Excel.Tests.Api;

/// <summary>
/// The workbook's segment counters (Phase 9's step 8), added as each reader is disposed: the pipelines started, the
/// segments stitched into readers' batches, and the fallbacks to the serial read taken.
/// </summary>
/// <param name="starts">The pipelines started (<c>ExcelWorkbook.segmentStarts</c>).</param>
/// <param name="stitched">The segments appended (<c>ExcelWorkbook.segmentsStitched</c>).</param>
/// <param name="fallbacks">The fallbacks taken (<c>ExcelWorkbook.segmentFallbacks</c>).</param>
internal readonly struct SegmentCounts(long starts, long stitched, long fallbacks)
{
	/// <summary>The pipelines started.</summary>
	public long Starts => starts;

	/// <summary>The segments appended.</summary>
	public long Stitched => stitched;

	/// <summary>The fallbacks taken.</summary>
	public long Fallbacks => fallbacks;

	/// <summary>The counters of a workbook now.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <returns>The counters.</returns>
	public static SegmentCounts Of(ExcelWorkbook workbook) => new(workbook.segmentStarts, workbook.segmentsStitched, workbook.segmentFallbacks);

	/// <summary>What the counters added between two readings of them.</summary>
	/// <param name="after">The later reading.</param>
	/// <param name="before">The earlier reading.</param>
	/// <returns>The difference.</returns>
	public static SegmentCounts operator -(SegmentCounts after, SegmentCounts before) =>
		new(after.Starts - before.Starts, after.Stitched - before.Stitched, after.Fallbacks - before.Fallbacks);

	/// <summary>The sum of two sets of counters.</summary>
	/// <param name="left">A set.</param>
	/// <param name="right">Another.</param>
	/// <returns>The sum.</returns>
	public static SegmentCounts operator +(SegmentCounts left, SegmentCounts right) =>
		new(left.Starts + right.Starts, left.Stitched + right.Stitched, left.Fallbacks + right.Fallbacks);

	/// <inheritdoc/>
	public override string ToString() => $"{starts} started, {stitched} stitched, {fallbacks} fallbacks";
}

/// <summary>
/// A sheet as one read gave it: each record as <see cref="ParallelReads.Describe"/> describes it, the header when the read
/// had one, and the exception that ended it, if one did; and, for a read under <c>ParallelEnumerate</c>, what the
/// workbook's segment counters added.
/// </summary>
/// <param name="records">The records, in order.</param>
/// <param name="headers">The header's names joined with <c>|</c>, or <see langword="null"/> when the read had none or didn't get to it.</param>
/// <param name="error">The exception that ended the read, or <see langword="null"/> when it read to the end.</param>
internal sealed class SheetReading(List<string> records, string? headers, Exception? error)
{
	/// <summary>The records, in order.</summary>
	public List<string> Records => records;

	/// <summary>The header's names joined with <c>|</c>, or <see langword="null"/>.</summary>
	public string? Headers => headers;

	/// <summary>The exception that ended the read, or <see langword="null"/>.</summary>
	public Exception? Error => error;

	/// <summary>What the workbook's segment counters added during the read and the reader's disposal.</summary>
	public SegmentCounts Counts { get; init; }
}

/// <summary>
/// An array pool for a reader's segments (<c>ExcelWorkbook.segmentPool</c>) that checks how they use it: each array it
/// rents is new and its own, a return of an array it didn't rent or of one already returned is a failure, and a returned
/// array is filled with bytes that are no XML, so that a thread still reading it reads garbage, which the records show.
/// </summary>
internal sealed class CheckingSegmentPool : ArrayPool<byte>
{
	/// <summary>Guards the pool's state, which the pipeline's threads share.</summary>
	private readonly object gate = new();

	/// <summary>The arrays rented and not yet returned.</summary>
	private readonly HashSet<byte[]> rented = [];

	/// <summary>What went wrong.</summary>
	private readonly List<string> failures = [];

	/// <summary>The number of arrays rented.</summary>
	private long rents;

	/// <summary>The number of arrays rented.</summary>
	public long Rents
	{
		get
		{
			lock (gate)
			{
				return rents;
			}
		}
	}

	/// <inheritdoc/>
	public override byte[] Rent(int minimumLength)
	{
		var array = new byte[minimumLength];
		lock (gate)
		{
			rented.Add(array);
			rents++;
		}

		return array;
	}

	/// <inheritdoc/>
	public override void Return(byte[] array, bool clearArray = false)
	{
		lock (gate)
		{
			if (!rented.Remove(array))
			{
				failures.Add($"An array of {array.Length} bytes went back that the pool didn't rent, or went back twice.");
			}
		}

		// Bytes that can't start any XML a scanner would take: a thread that reads the array after this reads an error.
		array.AsSpan().Fill(0xFF);
	}

	/// <summary>Checks that every array rented came back once, and none came back that wasn't rented.</summary>
	/// <param name="what">What used the pool, for the failure.</param>
	public void AssertReturned(string what)
	{
		lock (gate)
		{
			Assert.True(failures.Count == 0, $"{what}: {string.Join(" ", failures)}");
			Assert.True(rented.Count == 0, $"{what}: {rented.Count} of the {rents} arrays rented are still out ({string.Join(", ", rented.Select(a => a.Length))} bytes).");
		}
	}
}

/// <summary>
/// Reading sheets in segments (Phase 9's step 8) for the tests: the workbook's switches that force segments, a record as
/// the differential compares it, a sheet read sequentially and under
/// <see cref="CsvReader.ParallelEnumerate{T}(Func{CsvReader, T}, int)"/>, how two readings differ, and a time limit that
/// tells a stalled read from a slow one.
/// </summary>
internal static class ParallelReads
{
	/// <summary>
	/// The segment sizes the tests force: a byte, so that every row start is a cut and each segment is one row; and 4 KB,
	/// so that segments are many rows and their ends fall anywhere in a row's neighbours.
	/// </summary>
	public static readonly int[] Sizes = [1, 4096];

	/// <summary>The degrees the plan's corpus differential reads at.</summary>
	public static readonly int[] CorpusDegrees = [1, 2, 3, 8, 16];

	/// <summary>
	/// How long one read may take before it counts as stalled: far longer than any of these reads takes, even on CI's
	/// cores beside other tests, and far shorter than a deadlock waits.
	/// </summary>
	public static readonly TimeSpan TimeLimit = TimeSpan.FromMinutes(2);

	/// <summary>
	/// A reader's options with the smallest buffer, so that a fill is a few records: the first fill ends between rows, and
	/// the segments begin there, as <see cref="Force"/> lets them. The header is the first row.
	/// </summary>
	public static readonly ExcelReaderOptions Small = new() { Csv = new() { BufferSize = 256 } };

	/// <summary>As <see cref="Small"/>, with no header, so that every row is a record.</summary>
	public static readonly ExcelReaderOptions NoHeader = new() { Csv = new() { HasHeader = false, BufferSize = 256 } };

	/// <summary>
	/// Sets a workbook's switches so that its next readers read in segments as soon as they can under
	/// <c>ParallelEnumerate</c>: segment scans on, the serial read's minimum 0, and segments cut at the first row start after
	/// <paramref name="segmentSize"/> bytes.
	/// </summary>
	/// <param name="workbook">The workbook, before its readers are opened.</param>
	/// <param name="segmentSize">The cut target: 1 cuts at every row.</param>
	/// <param name="pool">The segments' array pool, or <see langword="null"/> for the shared one.</param>
	/// <param name="threads">The scan threads: 0 for one a worker, up to one a processor; more forces that many.</param>
	public static void Force(ExcelWorkbook workbook, int segmentSize, ArrayPool<byte>? pool = null, int threads = 0)
	{
		workbook.segmentScans = true;
		workbook.segmentSize = segmentSize;
		workbook.segmentMinimum = 0;
		workbook.segmentPool = pool ?? ArrayPool<byte>.Shared;
		workbook.segmentThreads = threads;
	}

	/// <summary>
	/// Describes a reader's current record as the differential compares it: its line, and for each field its stored bytes,
	/// its text (the shown text under <see cref="ExcelText.Formatted"/>), whether it reads as null, and what it parses to as
	/// a number, a date, a duration and a boolean, which together tell its kind. A read that throws is described by its
	/// exception, so that a selector never throws.
	/// </summary>
	/// <param name="reader">The reader, on a record: the reader a <c>ParallelEnumerate</c> selector gets, or a reader after <see cref="CsvReader.Read"/>.</param>
	/// <returns>The description.</returns>
	public static string Describe(CsvReader reader)
	{
		var text = new StringBuilder();
		text.Append(reader.LineNumber.ToString(CultureInfo.InvariantCulture)).Append(':');
		for (var i = 0; i < reader.FieldCount; i++)
		{
			text.Append(" | ");
			AppendField(text, reader, i);
		}

		return text.ToString();
	}

	/// <summary>Reads a sheet sequentially: the oracle of the differential.</summary>
	/// <remarks>
	/// With an error, the oracle is every record before the failing row, then the error. A fill that throws keeps the whole
	/// records it wrote before its error, so the reader reads on after its first throw, through them, until it throws the
	/// same exception again. An error in the fill that reads the header ends the read before any record, as it ends
	/// <c>ParallelEnumerate</c> before its workers start.
	/// </remarks>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet, or <see langword="null"/> for the one the options choose.</param>
	/// <param name="options">The reader's options.</param>
	/// <param name="describe">Describes a record; <see langword="null"/> for <see cref="Describe"/>.</param>
	/// <returns>The reading.</returns>
	public static SheetReading ReadSequentially(ExcelWorkbook workbook, ExcelSheet? sheet, ExcelReaderOptions options, Func<CsvReader, string>? describe = null)
	{
		describe ??= Describe;
		var records = new List<string>();
		CsvReader reader;
		try
		{
			reader = Open(workbook, sheet, options);
		}
		catch (Exception e) when (IsOpenError(e))
		{
			return new SheetReading(records, null, e);
		}

		using (reader)
		{
			string? headers = null;
			if (options.Csv.HasHeader)
			{
				try
				{
					reader.ReadHeader();
				}
				catch (Exception e) when (FuzzTests.IsExpected(e))
				{
					return new SheetReading(records, null, e);
				}

				headers = string.Join("|", reader.Headers);
			}

			Exception? error = null;
			while (true)
			{
				bool read;
				try
				{
					read = reader.Read();
				}
				catch (Exception e) when (FuzzTests.IsExpected(e))
				{
					if (error is null)
					{
						error = e;

						continue;
					}

					Assert.True(e.GetType() == error.GetType() && e.Message == error.Message, $"The sequential read threw {Show(error)}, and then {Show(e)}.");

					break;
				}

				if (!read)
				{
					if (error is not null)
					{
						Assert.Fail($"The sequential read threw {Show(error)}, and then read to its end.");
					}

					break;
				}

				records.Add(describe(reader));
			}

			return new SheetReading(records, headers, error);
		}
	}

	/// <summary>
	/// Reads a sheet with <see cref="CsvReader.ParallelEnumerate{T}(Func{CsvReader, T}, int)"/>, each record described by
	/// the selector, under the workbook's switches as they are; then disposes the reader, and notes what the workbook's
	/// segment counters added.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet, or <see langword="null"/> for the one the options choose.</param>
	/// <param name="options">The reader's options.</param>
	/// <param name="degree">The enumeration's degree of parallelism.</param>
	/// <param name="describe">Describes a record on a worker; <see langword="null"/> for <see cref="Describe"/>.</param>
	/// <returns>The reading.</returns>
	public static SheetReading ReadInParallel(ExcelWorkbook workbook, ExcelSheet? sheet, ExcelReaderOptions options, int degree, Func<CsvReader, string>? describe = null)
	{
		describe ??= Describe;
		var before = SegmentCounts.Of(workbook);
		var records = new List<string>();
		CsvReader reader;
		try
		{
			reader = Open(workbook, sheet, options);
		}
		catch (Exception e) when (IsOpenError(e))
		{
			return new SheetReading(records, null, e);
		}

		string? headers = null;
		Exception? error = null;
		using (reader)
		{
			try
			{
				foreach (var record in reader.ParallelEnumerate(describe, degree))
				{
					records.Add(record);
				}
			}
			catch (Exception e) when (FuzzTests.IsExpected(e))
			{
				error = e;
			}

			// A read that yielded records, or none and no error, has read its header.
			if (options.Csv.HasHeader && (error is null || records.Count > 0))
			{
				headers = string.Join("|", reader.Headers);
			}
		}

		return new SheetReading(records, headers, error) { Counts = SegmentCounts.Of(workbook) - before };
	}

	/// <summary>
	/// The first difference between the sequential read and a parallel one: a record, the header, or the error (its type
	/// and message, and for a <see cref="CsvFormatException"/> its <see cref="CsvFormatException.Location"/> and
	/// <see cref="CsvFormatException.LineNumber"/>).
	/// </summary>
	/// <param name="expected">The sequential read.</param>
	/// <param name="actual">The parallel read.</param>
	/// <returns>The difference, described; <see langword="null"/> when they are the same.</returns>
	public static string? Difference(SheetReading expected, SheetReading actual)
	{
		for (var i = 0; i < Math.Max(expected.Records.Count, actual.Records.Count); i++)
		{
			var sequential = i < expected.Records.Count ? expected.Records[i] : "(no record)";
			var parallel = i < actual.Records.Count ? actual.Records[i] : "(no record)";
			if (sequential != parallel)
			{
				return $"record {i + 1} of {expected.Records.Count} and {actual.Records.Count}: sequentially {CellValue.Show(sequential)}, in parallel {CellValue.Show(parallel)}";
			}
		}

		if (expected.Headers is not null && actual.Headers is not null && expected.Headers != actual.Headers)
		{
			return $"the header: sequentially {CellValue.Show(expected.Headers)}, in parallel {CellValue.Show(actual.Headers)}";
		}

		return ErrorDifference(expected.Error, actual.Error);
	}

	/// <summary>
	/// The difference between two errors: whether there is one, its type and message, and for a
	/// <see cref="CsvFormatException"/> its location and line.
	/// </summary>
	/// <param name="expected">The sequential read's error, or <see langword="null"/>.</param>
	/// <param name="actual">The parallel read's error, or <see langword="null"/>.</param>
	/// <returns>The difference, described; <see langword="null"/> when they are the same.</returns>
	public static string? ErrorDifference(Exception? expected, Exception? actual)
	{
		if (expected is null && actual is null)
		{
			return null;
		}

		if (expected is null || actual is null || expected.GetType() != actual.GetType() || expected.Message != actual.Message)
		{
			return $"the error: sequentially {Show(expected)}, in parallel {Show(actual)}";
		}

		if (expected is CsvFormatException format && actual is CsvFormatException other && (format.Location != other.Location || format.LineNumber != other.LineNumber))
		{
			return $"the error's place: sequentially {format.Location} (line {format.LineNumber}), in parallel {other.Location} (line {other.LineNumber})";
		}

		return null;
	}

	/// <summary>
	/// Checks that records read on after an early stop are the sequential records from a later one to the end, in order:
	/// the batches the workers read ahead and the enumeration didn't take are lost, and nothing else.
	/// </summary>
	/// <param name="expected">The sequential records.</param>
	/// <param name="taken">The records the enumeration took before it stopped.</param>
	/// <param name="rest">The records read on after it.</param>
	/// <param name="what">What was read, for the failure.</param>
	public static void AssertReadsOn(IReadOnlyList<string> expected, int taken, IReadOnlyList<string> rest, string what)
	{
		if (rest.Count == 0)
		{
			return;
		}

		var start = -1;
		for (var i = 0; i < expected.Count; i++)
		{
			if (expected[i] == rest[0])
			{
				start = i;

				break;
			}
		}

		Assert.True(start >= taken, $"{what}: reading on began at {CellValue.Show(rest[0])}, record {start + 1} of the sequential read, not after the {taken} taken.");
		Assert.True(
			expected.Skip(start).SequenceEqual(rest),
			$"{what}: the {rest.Count} records read on from record {start + 1} aren't the sequential records from there to the end ({expected.Count - start}).");
	}

	/// <summary>
	/// Waits for a read, failing when it takes longer than a limit: a stalled pipeline (a deadlock, a thread that never
	/// ends) fails here rather than hanging the run. The read goes on on its thread, as the fuzzer leaves an input.
	/// </summary>
	/// <typeparam name="T">The read's result.</typeparam>
	/// <param name="read">The read, run on a thread of the pool.</param>
	/// <param name="what">What is read, for the failure.</param>
	/// <param name="limit">The limit, or <see langword="null"/> for <see cref="TimeLimit"/>.</param>
	/// <returns>A task whose result is the read's.</returns>
	public static async Task<T> WithinAsync<T>(Func<T> read, string what, TimeSpan? limit = null)
	{
		var time = limit ?? TimeLimit;
		var run = Task.Run(read);
		using (var stop = new CancellationTokenSource())
		{
			var finished = await Task.WhenAny(run, Task.Delay(time, stop.Token));
			stop.Cancel();
			Assert.True(finished == run, $"{what}: still running after {time.TotalSeconds} s.");
		}

		return await run;
	}

	/// <summary>Opens a reader of a sheet, or of the one the options choose.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet, or <see langword="null"/>.</param>
	/// <param name="options">The reader's options.</param>
	/// <returns>The reader.</returns>
	public static CsvReader Open(ExcelWorkbook workbook, ExcelSheet? sheet, ExcelReaderOptions options) =>
		sheet is null ? workbook.OpenReader(options) : workbook.OpenReader(sheet, options);

	/// <summary>An exception for a failure message: its type and message.</summary>
	/// <param name="e">The exception, or <see langword="null"/>.</param>
	/// <returns>The text.</returns>
	public static string Show(Exception? e) => e is null ? "no error" : $"{e.GetType().Name}: {e.Message}";

	/// <summary>
	/// Whether an exception is one that opening a reader can end in for a workbook of the corpus or the fuzzer: a sheet
	/// that can't be read, or a header search that finds no row.
	/// </summary>
	/// <param name="e">The exception.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsOpenError(Exception e) => FuzzTests.IsExpected(e) || e is KeyNotFoundException;

	/// <summary>Describes a field: its stored bytes, its text, whether it is null, and its typed reads.</summary>
	/// <param name="text">Takes the description.</param>
	/// <param name="reader">The reader, on a record.</param>
	/// <param name="index">The field's index.</param>
	private static void AppendField(StringBuilder text, CsvReader reader, int index)
	{
		text.Append(Encoding.UTF8.GetString(reader.GetSpan(index).ToArray()));
		text.Append(" ~ ").Append(Attempt(() => reader.GetString(index)));
		text.Append(" ~ ").Append(Attempt(() => reader.IsNull(index) ? "null" : "value"));
		text.Append(" ~ ").Append(Attempt(() => reader.TryParse<double>(index, out var number) ? number.ToString("R", CultureInfo.InvariantCulture) : "-"));
		text.Append(" ~ ").Append(Attempt(() => reader.TryParse<DateTime>(index, out var date) ? date.ToString("O", CultureInfo.InvariantCulture) : "-"));
		text.Append(" ~ ").Append(Attempt(() => reader.TryParse<TimeSpan>(index, out var duration) ? duration.ToString("c", CultureInfo.InvariantCulture) : "-"));
		text.Append(" ~ ").Append(Attempt(() => reader.TryParse<bool>(index, out var flag) ? (flag ? "TRUE" : "FALSE") : "-"));
	}

	/// <summary>A read of a field, or the exception it threw, as text.</summary>
	/// <param name="read">The read.</param>
	/// <returns>Its text, or the exception's type and message after <c>!</c>.</returns>
	private static string Attempt(Func<string> read)
	{
		try
		{
			return read();
		}
		catch (Exception e)
		{
			return "!" + Show(e);
		}
	}
}

/// <summary>A sheet of the differential, generated from a seed: its workbook, the options it is read with, and what it has.</summary>
/// <param name="package">The workbook.</param>
/// <param name="options">The reader's options.</param>
/// <param name="description">What the sheet and the options have, for a failure.</param>
internal sealed class GeneratedSheet(TestPackage package, ExcelReaderOptions options, string description)
{
	/// <summary>The workbook.</summary>
	public TestPackage Package => package;

	/// <summary>The reader's options.</summary>
	public ExcelReaderOptions Options => options;

	/// <summary>What the sheet and the options have.</summary>
	public string Description => description;
}

/// <summary>
/// Sheets for reading in segments: plain ones of numbers, with and without references, and generated ones that mix the
/// forms segments must read exactly, as the plan's differential lists them.
/// </summary>
internal static class SegmentSheets
{
	/// <summary>How a generated sheet's rows are numbered.</summary>
	private enum Numbering
	{
		/// <summary>Every row has an <c>r</c>.</summary>
		Referenced,

		/// <summary>No row or cell has an <c>r</c>: each row is the one before plus one.</summary>
		Unreferenced,

		/// <summary>Each row as it falls: with an <c>r</c>, without one, or numbered by its first cell's <c>r</c>.</summary>
		Mixed,
	}

	/// <summary>How a generated row is numbered.</summary>
	private enum RowNumber
	{
		/// <summary>By its <c>r</c>.</summary>
		Referenced,

		/// <summary>As the row before plus one, with no <c>r</c> on it or its cells.</summary>
		Floating,

		/// <summary>By its first cell's <c>r</c>, with none on the row.</summary>
		ByCell,
	}

	/// <summary>A fault a generated sheet can have, at a row in its second half.</summary>
	private enum Fault
	{
		/// <summary>None.</summary>
		None,

		/// <summary>A row numbered no later than the row before.</summary>
		OutOfOrder,

		/// <summary>A cell without its end tag.</summary>
		Malformed,

		/// <summary>A cell past the grid's last column, <c>XFD</c>.</summary>
		PastLastColumn,

		/// <summary>A <c>&lt;</c> in an attribute value, followed by what the splitter takes for a row start.</summary>
		LessThanInAttribute,
	}

	/// <summary>
	/// A sheet of numbers, as <see cref="Workbooks.Numbers"/> writes it: a header <c>Id|Text</c>, then the rows 1 to
	/// <paramref name="count"/>, each a number and an inline string; with references on every row and cell, or none.
	/// </summary>
	/// <param name="count">The rows after the header.</param>
	/// <param name="references">Whether rows and cells have <c>r</c>.</param>
	/// <returns>The workbook.</returns>
	public static TestPackage Numbers(int count, bool references = true)
	{
		if (references)
		{
			return Workbooks.Data(Workbooks.Numbers(count));
		}

		var rows = new StringBuilder("<row><c t=\"inlineStr\"><is><t>Id</t></is></c><c t=\"inlineStr\"><is><t>Text</t></is></c></row>");
		for (var i = 1; i <= count; i++)
		{
			rows.Append($"<row><c><v>{i}</v></c><c t=\"inlineStr\"><is><t>row {i}</t></is></c></row>");
		}

		return Workbooks.Data(rows.ToString());
	}

	/// <summary>
	/// Generates a sheet and the options to read it with. The sheet's rows are numbered by their <c>r</c>, by none, or each
	/// as it falls (by its <c>r</c>, its first cell's, or as the row before plus one), with missing rows between numbered
	/// ones; rows without cells, as <c>&lt;row/&gt;</c> or with an end tag; hidden rows, and a hidden column; cells of
	/// numbers, styled numbers (dates, durations, fractions, text sections), shared and inline strings, booleans, errors,
	/// formula text and empty cells, with and without <c>r</c>, with gaps between them; one sheet in four prefixed
	/// (<c>x:</c>); one in five with a third of its rows 8 cells of 5,000 characters, larger than a 4 KB segment, read in one
	/// seed of two with a 64 KB part limit, under which they are larger than a segment can be (32 KB); and one in eight with a
	/// fault in its second half. The options mix a header (the first row read, or found), empty rows kept or skipped, hidden
	/// rows and columns skipped or read, formatted text, error cells, pooled strings, and one sheet in four an area: a block,
	/// rows, or columns.
	/// </summary>
	/// <param name="seed">The seed.</param>
	/// <returns>The sheet.</returns>
	public static GeneratedSheet Generate(int seed)
	{
		var random = new Random(seed);
		var numbering = (Numbering)random.Next(3);
		var prefix = random.Next(4) == 0 ? "x:" : "";
		var count = 40 + random.Next(1200);
		var hidden = random.Next(2) == 0;
		var hiddenColumn = random.Next(3) == 0;
		var longRows = random.Next(5) == 0;
		var fault = random.Next(8) == 0 ? (Fault)(1 + random.Next(4)) : Fault.None;
		var faultAt = count / 2 + random.Next(count / 2);
		var rows = new StringBuilder();
		var row = 0L;
		for (var i = 0; i < count; i++)
		{
			var number = numbering switch
			{
				Numbering.Referenced => RowNumber.Referenced,
				Numbering.Unreferenced => RowNumber.Floating,
				_ => (RowNumber)random.Next(3),
			};

			// Missing rows come before a row with an r; a row numbered by its cell is the row before plus one, as its cell says.
			var previous = row;
			row += number == RowNumber.Referenced && random.Next(5) == 0 ? 1 + random.Next(1, 6) : 1;
			var reference = row;
			if (fault == Fault.OutOfOrder && i == faultAt)
			{
				number = RowNumber.Referenced;
				reference = Math.Max(1, previous - random.Next(3));
			}

			rows.Append('<').Append(prefix).Append("row");
			if (number == RowNumber.Referenced)
			{
				rows.Append($" r=\"{reference}\"");
			}

			if (hidden && random.Next(8) == 0)
			{
				rows.Append(" hidden=\"1\"");
			}

			if (random.Next(6) == 0)
			{
				rows.Append(" spans=\"1:6\" ht=\"15\"");
			}

			// A long row is 8 cells of 5,000 characters: larger than a 4 KB segment, and than a segment under a 64 KB part limit.
			var longRow = longRows && random.Next(3) == 0;
			var cells = longRow ? 8 : random.Next(10) == 0 ? 0 : 1 + random.Next(5);
			if (cells == 0 && !(i == faultAt && fault != Fault.None && fault != Fault.OutOfOrder))
			{
				rows.Append(random.Next(2) == 0 ? "/>" : $"></{prefix}row>");

				continue;
			}

			rows.Append('>');
			var column = -1;
			for (var c = 0; c < Math.Max(cells, 1); c++)
			{
				var referenced = number switch
				{
					RowNumber.Floating => false,
					RowNumber.ByCell => c == 0 || random.Next(2) == 0,
					_ => random.Next(3) > 0,
				};
				column += referenced && random.Next(4) == 0 ? 1 + random.Next(1, 4) : 1;
				var at = referenced ? $" r=\"{ColumnName(column)}{reference}\"" : "";
				var v = prefix + "v";
				if (i == faultAt && c == 0 && fault == Fault.PastLastColumn)
				{
					rows.Append($"<{prefix}c r=\"XFE{reference}\"><{v}>1</{v}></{prefix}c>");
				}
				else if (i == faultAt && c == 0 && fault == Fault.LessThanInAttribute)
				{
					rows.Append($"<{prefix}c{at} t=\"str\" note=\"<{prefix}row r=&quot;1&quot;>\"><{v}>1</{v}></{prefix}c>");
				}
				else if (i == faultAt && c == 0 && fault == Fault.Malformed)
				{
					// No end tag: the next cell's start tag, or the row's end tag, comes inside the cell.
					rows.Append($"<{prefix}c{at}><{v}>1</{v}>");
				}
				else if (longRow)
				{
					rows.Append($"<{prefix}c{at} t=\"inlineStr\"><{prefix}is><{prefix}t>{new string('x', 5000)}{i}</{prefix}t></{prefix}is></{prefix}c>");
				}
				else
				{
					Cell(rows, random, prefix, at, i, c);
				}
			}

			rows.Append("</").Append(prefix).Append("row>");
		}

		var cols = hiddenColumn ? $"<{prefix}cols><{prefix}col min=\"2\" max=\"2\" hidden=\"1\"/></{prefix}cols>" : "";
		var namespaceName = prefix.Length == 0 ? "xmlns" : "xmlns:x";
		var part = TestPackage.Declaration + $"<{prefix}worksheet {namespaceName}=\"{TestPackage.MainNamespace}\">{cols}<{prefix}sheetData>{rows}</{prefix}sheetData></{prefix}worksheet>";
		var package = FormattedTextTests.Book("", xml: part);
		var hasHeader = random.Next(3) == 0;
		var options = new ExcelReaderOptions
		{
			Csv = new() { HasHeader = hasHeader, SkipEmptyLines = random.Next(2) == 0, BufferSize = 256 },
			SkipHiddenRows = hidden && random.Next(2) == 0,
			SkipHiddenColumns = hiddenColumn && random.Next(2) == 0,
			Text = random.Next(3) == 0 ? ExcelText.Formatted : ExcelText.Raw,
			ErrorCells = random.Next(4) switch
			{
				0 => ExcelErrorCells.Null,
				1 => ExcelErrorCells.Error,
				_ => ExcelErrorCells.Text,
			},
			PoolStrings = random.Next(2) == 0,
			MaxPartSize = longRows && random.Next(2) == 0 ? 64 * 1024 : 256L * 1024 * 1024,
			Range = random.Next(4) == 0 ? Area(random, row) : null,
			HeaderRow = hasHeader && random.Next(2) == 0 ? ExcelHeaderRow.Find([]) : null,
		};
		var description =
			$"seed {seed}: {count} rows {numbering}{(prefix.Length > 0 ? ", prefixed" : "")}{(hidden ? ", hidden rows" : "")}{(hiddenColumn ? ", a hidden column" : "")}" +
			$"{(longRows ? ", long rows" : "")}{(fault == Fault.None ? "" : $", {fault} at row element {faultAt + 1}")}; header {options.Csv.HasHeader}" +
			$"{(options.HeaderRow is null ? "" : " found")}, skip empty {options.Csv.SkipEmptyLines}, skip hidden rows {options.SkipHiddenRows}, " +
			$"skip hidden columns {options.SkipHiddenColumns}, {options.Text}, error cells {options.ErrorCells}, pooled {options.PoolStrings}, " +
			$"part limit {options.MaxPartSize}, range {options.Range ?? "none"}";

		return new GeneratedSheet(package, options, description);
	}

	/// <summary>A column's letters, from 0 for <c>A</c>.</summary>
	/// <param name="column">The column, from 0.</param>
	/// <returns>The letters.</returns>
	public static string ColumnName(int column)
	{
		var name = "";
		for (var n = column + 1; n > 0; n = (n - 1) / 26)
		{
			name = (char)('A' + ((n - 1) % 26)) + name;
		}

		return name;
	}

	/// <summary>Writes a generated cell: one of the kinds a sheet has, at random.</summary>
	/// <param name="rows">Takes the cell.</param>
	/// <param name="random">The generator's numbers.</param>
	/// <param name="prefix">The elements' prefix, or empty.</param>
	/// <param name="at">The cell's <c>r</c> attribute with its leading space, or empty.</param>
	/// <param name="row">The row element's index, for the text.</param>
	/// <param name="column">The cell's index in its row, for the text.</param>
	private static void Cell(StringBuilder rows, Random random, string prefix, string at, int row, int column)
	{
		var c = prefix + "c";
		var v = prefix + "v";
		switch (random.Next(10))
		{
			case 0:
				rows.Append($"<{c}{at}><{v}>{random.Next(-1000, 100_000)}</{v}></{c}>");

				break;
			case 1:
				rows.Append($"<{c}{at}><{v}>{(random.NextDouble() * 1000).ToString("R", CultureInfo.InvariantCulture)}</{v}></{c}>");

				break;
			case 2:
				// One of FormattedTextTests.Formats: dates, durations, fractions, text sections and the rest.
				rows.Append($"<{c}{at} s=\"{random.Next(FormattedTextTests.Formats.Length)}\"><{v}>{(40_000 + random.NextDouble() * 10_000).ToString("R", CultureInfo.InvariantCulture)}</{v}></{c}>");

				break;
			case 3:
				// FormattedTextTests.Book's shared strings: pear, xyz and empty text.
				rows.Append($"<{c}{at} t=\"s\"><{v}>{random.Next(3)}</{v}></{c}>");

				break;
			case 4:
				rows.Append($"<{c}{at} t=\"inlineStr\"><{prefix}is><{prefix}t>text {row} {column}</{prefix}t></{prefix}is></{c}>");

				break;
			case 5:
				rows.Append($"<{c}{at} t=\"b\"><{v}>{random.Next(2)}</{v}></{c}>");

				break;
			case 6:
				rows.Append($"<{c}{at} t=\"e\"><{v}>{(random.Next(2) == 0 ? "#N/A" : "#DIV/0!")}</{v}></{c}>");

				break;
			case 7:
				rows.Append(random.Next(2) == 0 ? $"<{c}{at}/>" : $"<{c}{at} s=\"2\"/>");

				break;
			case 8:
				rows.Append($"<{c}{at} t=\"str\"><{prefix}f>A1&amp;\"x\"</{prefix}f><{v}>formula {row}</{v}></{c}>");

				break;
			default:
				rows.Append($"<{c}{at} t=\"inlineStr\" s=\"4\"><{prefix}is><{prefix}t xml:space=\"preserve\"> padded {row} </{prefix}t></{prefix}is></{c}>");

				break;
		}
	}

	/// <summary>An area of a generated sheet: a block of its rows and columns, a run of its rows, or a run of its columns.</summary>
	/// <param name="random">The generator's numbers.</param>
	/// <param name="lastRow">The sheet's last row.</param>
	/// <returns>The area, as <see cref="ExcelReaderOptions.Range"/> takes it.</returns>
	private static string Area(Random random, long lastRow)
	{
		var first = 1 + random.Next(5);
		var last = Math.Max(first + 1, (int)(lastRow / 3) + random.Next((int)(lastRow / 2) + 1));

		return random.Next(3) switch
		{
			0 => $"A{first}:{ColumnName(2 + random.Next(6))}{last}",
			1 => $"{first}:{last}",
			_ => $"B:{ColumnName(3 + random.Next(6))}",
		};
	}
}
