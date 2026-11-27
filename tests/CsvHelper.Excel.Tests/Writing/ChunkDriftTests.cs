using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CsvHelper.Excel.Tests.Security;
using Xunit.Abstractions;
using static CsvHelper.Excel.Tests.Security.WriterFuzz;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>A record of every kind of cell a record type writes, with formats and cultures, for the drift tests.</summary>
[CsvRecord]
public partial class DriftRecord
{
	/// <summary>A number cell.</summary>
	public int Id { get; set; }

	/// <summary>A text cell, or no cell when null.</summary>
	public string? Name { get; set; }

	/// <summary>A number of up to 19 digits, which <see cref="ExcelWriterOptions.LongNumbers"/> rules past 15.</summary>
	public long Big { get; set; }

	/// <summary>A number cell.</summary>
	public decimal Price { get; set; }

	/// <summary>A number cell, or text for NaN and the infinities.</summary>
	public double Ratio { get; set; }

	/// <summary>A boolean cell.</summary>
	public bool Flag { get; set; }

	/// <summary>A date cell in the writer's own format of what it holds.</summary>
	public DateTime When { get; set; }

	/// <summary>A date cell in its format translated.</summary>
	[CsvFormat("dd.MM.yyyy HH:mm")]
	public DateTime Day { get; set; }

	/// <summary>A month's name in a culture: a date cell under fr-FR's locale.</summary>
	[CsvFormat("MMMM yyyy")]
	[CsvCulture("fr-FR")]
	public DateTime Month { get; set; }

	/// <summary>An offset, as <see cref="ExcelWriterOptions.DateTimeOffsets"/> says.</summary>
	public DateTimeOffset Stamp { get; set; }

	/// <summary>An offset's clock time in a format: a date cell only with <see cref="ExcelDateTimeOffsets.ClockTime"/>.</summary>
	[CsvFormat("yyyy-MM-dd HH:mm")]
	public DateTimeOffset Stamped { get; set; }

	/// <summary>A duration cell.</summary>
	public TimeSpan Took { get; set; }

	/// <summary>A number in a culture other than the writer's.</summary>
	[CsvCulture("de-DE")]
	public decimal Amount { get; set; }

	/// <summary>A link, as <see cref="ExcelWriterOptions.Links"/> says, or no cell when null.</summary>
	public Uri? Link { get; set; }

	/// <summary>A formula cell, or no cell by default.</summary>
#if !NET
	[CsvConverter(typeof(ExcelFormulaConverter))]
#endif
	public ExcelFormula Total { get; set; }

	/// <summary>A number cell, or a gap when null.</summary>
	public int? Maybe { get; set; }
}

/// <summary>
/// An item of a drift test (<see cref="ChunkDrift"/>): the fields of one record, each written on its own and its exception
/// caught, as the writer fuzzers write them, before the record ends; or a whole record, which a record writer writes, and
/// which is discarded when it throws, as core discards a parallel write's failed item.
/// </summary>
internal sealed class DriftItem
{
	/// <summary>What the item is, for a failure.</summary>
	private readonly string text;

	/// <summary>Creates an item.</summary>
	/// <param name="fields">The fields' writers, or <see langword="null"/> for a whole record.</param>
	/// <param name="whole">The whole record's writer, or <see langword="null"/>.</param>
	/// <param name="text">What it is.</param>
	private DriftItem(IReadOnlyList<Action<CsvWriter>>? fields, Action<CsvWriter>? whole, string text)
	{
		Fields = fields;
		Whole = whole;
		this.text = text;
	}

	/// <summary>The fields' writers, each written on its own; <see langword="null"/> for a whole record.</summary>
	public IReadOnlyList<Action<CsvWriter>>? Fields { get; }

	/// <summary>The whole record's writer, which ends it; <see langword="null"/> for fields.</summary>
	public Action<CsvWriter>? Whole { get; }

	/// <summary>An item of fields' writers.</summary>
	/// <param name="fields">The writers.</param>
	/// <param name="text">What the item is.</param>
	/// <returns>The item.</returns>
	public static DriftItem Of(IReadOnlyList<Action<CsvWriter>> fields, string text) => new(fields, null, text);

	/// <summary>An item of values, each written as <see cref="Writer"/> writes it.</summary>
	/// <param name="values">The values.</param>
	/// <returns>The item.</returns>
	public static DriftItem Values(params object?[] values) => new([.. values.Select(Writer)], null, string.Join(", ", values.Select(Describe)));

	/// <summary>An item of a whole record.</summary>
	/// <param name="write">Writes the record and ends it.</param>
	/// <param name="text">What the item is.</param>
	/// <returns>The item.</returns>
	public static DriftItem Record(Action<CsvWriter> write, string text) => new(null, write, text);

	/// <summary>The writer of a value: a URI as one, a formatted value in its format and culture, any other as an object.</summary>
	/// <param name="value">The value, or <see langword="null"/> for an empty field.</param>
	/// <returns>The writer.</returns>
	public static Action<CsvWriter> Writer(object? value) => value switch
	{
		Uri uri => writer => writer.WriteField(uri),
		DriftFormatted formatted => formatted.WriteTo,
		_ => writer => writer.WriteField(value),
	};

	/// <inheritdoc/>
	public override string ToString() => text;

	/// <summary>A value for a failure.</summary>
	/// <param name="value">The value.</param>
	/// <returns>Its description.</returns>
	private static string Describe(object? value) => value switch
	{
		null => "null",
		string s => Show(s),
		Uri u => "URI " + Show(u.OriginalString),
		DriftFormatted f => $"{f.Value.GetType().Name} {Show(Convert.ToString(f.Value, CultureInfo.InvariantCulture) ?? "")} in {Show(f.Format)}",
		_ => $"{value.GetType().Name} {Show(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "")}",
	};
}

/// <summary>A value written in a format and a culture, as a member with <see cref="CsvFormatAttribute"/> or <see cref="CsvCultureAttribute"/> is.</summary>
/// <param name="Value">The value: a date, time, duration or number.</param>
/// <param name="Format">The format, empty for the type's default.</param>
/// <param name="Provider">The culture.</param>
internal sealed record DriftFormatted(object Value, string Format, IFormatProvider Provider)
{
	/// <summary>Writes the value.</summary>
	/// <param name="writer">The writer.</param>
	public void WriteTo(CsvWriter writer)
	{
		switch (Value)
		{
			case DateTime value:
				writer.WriteField(value, Format.AsSpan(), Provider);

				break;
			case DateTimeOffset value:
				writer.WriteField(value, Format.AsSpan(), Provider);

				break;
			case TimeSpan value:
				writer.WriteField(value, Format.AsSpan(), Provider);

				break;
			case double value:
				writer.WriteField(value, Format.AsSpan(), Provider);

				break;
			case decimal value:
				writer.WriteField(value, Format.AsSpan(), Provider);

				break;
			case int value:
				writer.WriteField(value, Format.AsSpan(), Provider);

				break;
			case long value:
				writer.WriteField(value, Format.AsSpan(), Provider);

				break;
#if NET
			case DateOnly value:
				writer.WriteField(value, Format.AsSpan(), Provider);

				break;
			case TimeOnly value:
				writer.WriteField(value, Format.AsSpan(), Provider);

				break;
#endif
			default:
				writer.WriteField(Value);

				break;
		}
	}
}

/// <summary>A sheet a drift test writes (<see cref="ChunkDrift.Check"/>).</summary>
internal sealed class DriftSheet
{
	/// <summary>What the sheet is, for a failure.</summary>
	public string Name { get; init; } = "";

	/// <summary>The workbook's options.</summary>
	public ExcelWriterOptions Options { get; init; } = ExcelWriterOptions.Default;

	/// <summary>Adds the sheet to the workbook: its writer.</summary>
	public Func<ExcelWorkbookWriter, CsvWriter> Add { get; init; } = book => book.AddSheet("Data");

	/// <summary>The items, the header's first when the sheet has one.</summary>
	public List<DriftItem> Items { get; init; } = [];

	/// <summary>The items the owner writes before the view is frozen: the header's at least.</summary>
	public int Prefix { get; init; }

	/// <summary>Whether the owner flushes after them, which writes the sheet's start once the widths are known, as the ready rule waits for under <see cref="ExcelSheetOptions.Protect"/>.</summary>
	public bool Flush { get; init; }

	/// <summary>The seed of the batches' sizes.</summary>
	public int Seed { get; init; }

	/// <summary>The most items a batch has.</summary>
	public int MaxBatch { get; init; } = 8;

	/// <summary>The length a worker reports (<see cref="ExcelWorkbookWriter.ParallelChunkLength"/>), 0 for its XML's.</summary>
	public int ChunkLength { get; init; }

	/// <inheritdoc/>
	public override string ToString() => $"{Name}, {Items.Count} items, prefix {Prefix}{(Flush ? " flushed" : "")}, batches of up to {MaxBatch}";
}

/// <summary>
/// Writes a sheet through its owner and, from a point on, through a parallel write's worker over the owner's frozen view
/// (<see cref="SheetSink.Freeze"/>), in one workbook: each batch of items into a chunk, and then the same items through the
/// owner, as the serial write, or a redo, writes them; the view is frozen again after a batch whose serial write added a
/// cell format or the link style, as the owner does. So each chunk is checked against the serial write of the same
/// records: one the worker didn't flag must be the serial rows of its records byte for byte, once the empty rows before it
/// are added, with the serial write's counts and links; and the worker must flag a chunk where, and only where, the
/// serial write first adds a cell format or the link style, or throws, for that reason.
/// </summary>
internal static class ChunkDrift
{
	/// <summary>The search of members the tests reach by reflection.</summary>
	private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

	/// <summary>Core's constructor of a writer over a cell sink, internal to core.</summary>
	private static readonly ConstructorInfo SinkWriter = typeof(CsvWriter).GetConstructors(Any).Single(c => c.GetParameters() is { Length: 2 } parameters && parameters[0].ParameterType.Name == "CsvCellSink");

	/// <summary>Core's discard of a writer's current record, which a parallel write calls for a failed item.</summary>
	private static readonly MethodInfo Discard = typeof(CsvWriter).GetMethod("DiscardRecord", Any, null, Type.EmptyTypes, null)!;

	/// <summary>A link's cell format.</summary>
	private static readonly CellXf LinkFormat = new(0, CellFont.Link, false);

	/// <summary>A row's element in <c>sheetData</c>'s text: empty, or up to its end tag.</summary>
	private static readonly Regex RowElement = new("<row(?=[\\s/>])[^>]*?(?:/>|>.*?</row>)", RegexOptions.Singleline | RegexOptions.CultureInvariant);

	/// <summary>SpreadsheetML's namespace.</summary>
	private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

	/// <summary>The relationships' namespace.</summary>
	private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

	/// <summary>
	/// A writer over a worker's sink, as core makes one for a parallel write's worker: with the sheet's options, but no
	/// header, which the owner wrote (core's worker numbers its records past it). It is never flushed or disposed.
	/// </summary>
	/// <param name="sink">The worker's sink.</param>
	/// <param name="csv">The sheet writer's options.</param>
	/// <returns>The writer.</returns>
	public static CsvWriter WorkerWriter(SheetChunkSink sink, CsvWriterOptions csv) => (CsvWriter)SinkWriter.Invoke([sink, csv with { HasHeader = false }]);

	/// <summary>Discards a writer's current record, as core does for a failed item.</summary>
	/// <param name="writer">The writer.</param>
	public static void DiscardRecord(CsvWriter writer) => Discard.Invoke(writer, null);

	/// <summary>Writes a sheet both ways and checks each chunk (see the class's summary).</summary>
	/// <param name="sheet">The sheet.</param>
	/// <param name="tally">Takes what the chunks were.</param>
	/// <returns>What went wrong, or <see langword="null"/>.</returns>
	public static string? Check(DriftSheet sheet, SortedDictionary<string, int> tally)
	{
		using var stream = new MemoryStream();
		var book = ExcelWorkbookWriter.Create(stream, sheet.Options, leaveOpen: true);
		book.ParallelChunkLength = sheet.ChunkLength;
		CsvWriter owner;
		try
		{
			owner = sheet.Add(book);
		}
		catch (ArgumentException)
		{
			// A fuzzer's sheet of options Excel wouldn't open.
			book.Dispose();
			Count(tally, "sheets: refused");

			return null;
		}

		var sink = book.OpenSheet!;
		var firstRow = sink.Settings.Title is null ? 1L : 2L;
		var items = sheet.Items;
		// The records the owner has ended, the header's included.
		long written = 0;
		var at = 0;
		for (; at < Math.Min(sheet.Prefix, items.Count); at++)
		{
			written += WriteOwner(owner, sink, items[at], null, at);
		}

		if (sheet.Flush)
		{
			owner.Flush();
		}

		var views = new SheetViewSlot();
		var version = 1;
		var view = sink.Freeze(version);
		views.Publish(view);
		var worker = new SheetChunkSink(views);
		var writer = WorkerWriter(worker, book.Options.Csv);
		var random = new Random(sheet.Seed);
		var appended = new List<Appended>();
		string? problem = null;
		try
		{
			while (at < items.Count && problem is null)
			{
				var count = Math.Min(random.Next(1, sheet.MaxBatch + 1), items.Count - at);
				var batch = items.GetRange(at, count);
				worker.BeginChunk(at);
				var thrown = WriteWorker(writer, batch);
				var chunk = (SheetChunk)worker.TakeChunk(out var length);
				try
				{
					var start = written;
					var serial = new FirstEvent();
					for (var i = 0; i < batch.Count; i++)
					{
						written += WriteOwner(owner, sink, batch[i], serial, i);
					}

					var where = $"the batch of items {at} to {at + count - 1} ({string.Join("; ", batch)})";
					problem = Judge(chunk, length, view, version, at, thrown, serial, written - start, batch, where);
					if (problem is null)
					{
						if (chunk.Redo == SheetChunkRedo.None)
						{
							appended.Add(new Appended(start, chunk.Records, chunk.FirstCellRecord, chunk.LastCellRecord, chunk.LastColumn, chunk.NumberLikeText, chunk.Links?.ToList(), chunk.Xml.Span.ToArray(), where));
							Count(tally, chunk.FirstCellRecord < 0 ? "chunks: appended, without a cell" : "chunks: appended");
						}
						else
						{
							Count(tally, $"chunks: redone, {chunk.Redo}");
						}
					}
				}
				finally
				{
					chunk.Release();
				}

				at += count;
				// The owner's refreeze: when the serial write added a cell format or the link style.
				var now = sink.Freeze(version + 1);
				if (now.Styles.Count != view.Styles.Count || now.LinkStyle != view.LinkStyle)
				{
					version++;
					view = now;
					views.Publish(view);
					Count(tally, "views: frozen again");
				}
			}
		}
		finally
		{
			worker.Release();
			owner.Dispose();
			book.Dispose();
		}

		if (problem is not null)
		{
			return problem;
		}

		var package = stream.ToArray();
		var text = Written.Entry(package, "xl/worksheets/sheet1.xml");
		var rows = RowsOf(text);
		var links = LinksOf(package, text);
		foreach (var chunk in appended)
		{
			if (Compare(chunk, rows, links, firstRow) is { } different)
			{
				return different;
			}
		}

		return null;
	}

	/// <summary>Writes a batch's items through the worker, as core's worker does: a whole record that throws is discarded and stops the batch.</summary>
	/// <param name="writer">The worker's writer.</param>
	/// <param name="batch">The items.</param>
	/// <returns>Where the worker first threw: the item, and its field (-1 for a whole record); or <see langword="null"/>.</returns>
	private static (int Item, int Field)? WriteWorker(CsvWriter writer, List<DriftItem> batch)
	{
		(int, int)? thrown = null;
		for (var i = 0; i < batch.Count; i++)
		{
			if (batch[i].Fields is { } fields)
			{
				for (var f = 0; f < fields.Count; f++)
				{
					try
					{
						fields[f](writer);
					}
					catch (InvalidOperationException)
					{
						thrown ??= (i, f);
					}
				}

				writer.EndRecord();

				continue;
			}

			try
			{
				batch[i].Whole!(writer);
			}
			catch (InvalidOperationException)
			{
				DiscardRecord(writer);
				thrown ??= (i, -1);

				break;
			}
		}

		return thrown;
	}

	/// <summary>
	/// Writes an item through the owner, as the serial write does, and notes the first thing it does that a worker can't:
	/// a cell format or the link style added, or an exception.
	/// </summary>
	/// <param name="owner">The sheet's writer.</param>
	/// <param name="sink">The sheet's sink.</param>
	/// <param name="item">The item.</param>
	/// <param name="first">Takes the batch's first such thing, or <see langword="null"/> in the prefix.</param>
	/// <param name="index">The item's place in its batch.</param>
	/// <returns>The records it ended: 1, or 0 for a whole record that threw.</returns>
	private static int WriteOwner(CsvWriter owner, SheetSink sink, DriftItem item, FirstEvent? first, int index)
	{
		if (item.Fields is { } fields)
		{
			for (var f = 0; f < fields.Count; f++)
			{
				var before = first is { Found: false } ? sink.Freeze(0) : null;
				var threw = false;
				try
				{
					fields[f](owner);
				}
				catch (InvalidOperationException)
				{
					threw = true;
				}

				if (before is not null)
				{
					first!.Note(index, f, before, sink.Freeze(0), threw);
				}
			}

			// A record's end can flush, and the sheet's start registers the unlocked columns' styles under Protect: a worker
			// can't, so the tests write that start before the view is frozen, as the ready rule does.
			var end = first is { Found: false } ? sink.Freeze(0) : null;
			owner.EndRecord();
			if (end is not null)
			{
				first!.Note(index, fields.Count, end, sink.Freeze(0), false);
			}

			return 1;
		}

		var start = first is { Found: false } ? sink.Freeze(0) : null;
		try
		{
			item.Whole!(owner);
		}
		catch (InvalidOperationException)
		{
			DiscardRecord(owner);
			if (start is not null)
			{
				first!.Note(index, -1, start, sink.Freeze(0), true);
			}

			return 0;
		}

		if (start is not null)
		{
			first!.Note(index, -1, start, sink.Freeze(0), false);
		}

		return 1;
	}

	/// <summary>Checks a chunk against the serial write of its batch: its flag, its place, its length and its counts.</summary>
	/// <param name="chunk">The chunk.</param>
	/// <param name="length">The length the worker reported.</param>
	/// <param name="view">The view it was written with.</param>
	/// <param name="version">The view's version.</param>
	/// <param name="at">The batch's first item.</param>
	/// <param name="thrown">Where the worker first threw.</param>
	/// <param name="serial">What the serial write first did that a worker can't.</param>
	/// <param name="records">The records the serial write ended for the batch.</param>
	/// <param name="batch">The batch's items.</param>
	/// <param name="where">The batch, for a failure.</param>
	/// <returns>What is wrong, or <see langword="null"/>.</returns>
	private static string? Judge(SheetChunk chunk, int length, SheetView view, int version, int at, (int Item, int Field)? thrown, FirstEvent serial, long records, List<DriftItem> batch, string where)
	{
		if (length != (view.ChunkLength > 0 ? view.ChunkLength : chunk.Xml.Length))
		{
			return $"{where}: the worker reported {length} bytes for {chunk.Xml.Length} (ChunkLength {view.ChunkLength})";
		}

		if (chunk.FirstRecord != at || chunk.ViewVersion != version)
		{
			return $"{where}: the chunk says record {chunk.FirstRecord} and view {chunk.ViewVersion}, for {at} and {version}";
		}

		if (chunk.Redo == SheetChunkRedo.None)
		{
			if (serial.Found)
			{
				return $"{where}: the serial write {serial} at {Place(at, (serial.Item, serial.Field))}, and the worker flagged nothing";
			}

			if (thrown is { } place)
			{
				return $"{where}: the worker threw at {Place(at, place)}, and flagged nothing";
			}

			if (chunk.Records != records)
			{
				return $"{where}: the chunk has {chunk.Records} records, and the serial write ended {records}";
			}

			return null;
		}

		if (!serial.Found)
		{
			return $"{where}: the worker flagged {chunk.Redo} at {Place(at, thrown)}, and the serial write added and threw nothing";
		}

		if (thrown is not { } first || first.Item != serial.Item || first.Field != serial.Field)
		{
			return $"{where}: the worker first threw at {Place(at, thrown)}, and the serial write {serial} at {Place(at, (serial.Item, serial.Field))}";
		}

		var fields = batch[serial.Item].Fields is not null;
		var expected = serial.Link ? SheetChunkRedo.LinkStyle : serial.Error ? SheetChunkRedo.Error : SheetChunkRedo.Style;
		var fits = fields
			? chunk.Redo == expected
			: (chunk.Redo == SheetChunkRedo.LinkStyle && serial.Link) || (chunk.Redo == SheetChunkRedo.Error && serial.Error) || (chunk.Redo == SheetChunkRedo.Style && serial.Style);

		return fits ? null : $"{where}: the worker flagged {chunk.Redo} at {Place(at, thrown)}, where the serial write {serial}";
	}

	/// <summary>A place in a batch, for a failure.</summary>
	/// <param name="at">The batch's first item.</param>
	/// <param name="place">The item in the batch and its field (-1 for a whole record), or <see langword="null"/>.</param>
	/// <returns>The place.</returns>
	private static string Place(int at, (int Item, int Field)? place) => place is { } p ? $"item {at + p.Item}, {(p.Field < 0 ? "a whole record" : $"field {p.Field}")}" : "no field";

	/// <summary>Checks an appended chunk against the serial rows of its records, once the empty rows before it are added.</summary>
	/// <param name="chunk">The chunk.</param>
	/// <param name="rows">The serial sheet's row elements, row 1 first.</param>
	/// <param name="links">The serial sheet's links.</param>
	/// <param name="firstRow">The row of the writer's first record: 2 below a title.</param>
	/// <returns>What is different, or <see langword="null"/>.</returns>
	private static string? Compare(Appended chunk, List<string> rows, List<(long Row, int Column, string Target)> links, long firstRow)
	{
		var startRow = firstRow + chunk.Start;
		var xml = Encoding.UTF8.GetString(chunk.Xml);
		// The rows of the chunk's records without a cell: <row/> when a later row has a cell, else not written.
		bool Empty(long row) => row > rows.Count || rows[(int)row - 1] == "<row/>";
		if (chunk.FirstCellRecord < 0)
		{
			if (chunk.Xml.Length != 0 || chunk.LastCellRecord != -1 || chunk.LastColumn != -1 || chunk.NumberLikeText || chunk.Links is not null)
			{
				return $"{chunk.Where}: a chunk without a cell has XML {Show(xml)}, last record {chunk.LastCellRecord}, last column {chunk.LastColumn}, number-like {chunk.NumberLikeText}, links {chunk.Links?.Count}";
			}

			for (var row = startRow; row < startRow + chunk.Records; row++)
			{
				if (!Empty(row))
				{
					return $"{chunk.Where}: the serial row {row} has a cell, and the chunk has none: {Show(rows[(int)row - 1])}";
				}
			}

			return null;
		}

		// The last row with a cell before the chunk, whose <row/>s the owner writes before it.
		var previous = startRow - 1;
		while (previous > 0 && Empty(previous))
		{
			previous--;
		}

		var lastRow = startRow + chunk.LastCellRecord;
		if (lastRow > rows.Count)
		{
			return $"{chunk.Where}: the chunk's last row with a cell is {lastRow}, and the serial sheet has {rows.Count} rows";
		}

		var serial = string.Concat(rows.Skip((int)previous).Take((int)(lastRow - previous)));
		var leading = new StringBuilder();
		for (var n = startRow + chunk.FirstCellRecord - previous - 1; n > 0; n--)
		{
			leading.Append("<row/>");
		}

		var expected = Encoding.UTF8.GetBytes(serial);
		var actual = Encoding.UTF8.GetBytes(leading.ToString()).Concat(chunk.Xml).ToArray();
		if (!expected.AsSpan().SequenceEqual(actual))
		{
			var common = 0;
			while (common < Math.Min(expected.Length, actual.Length) && expected[common] == actual[common])
			{
				common++;
			}

			var from = Math.Max(0, common - 60);

			return $"{chunk.Where}: rows {previous + 1} to {lastRow} differ at byte {common}: serial {Show(Encoding.UTF8.GetString(expected, from, Math.Min(160, expected.Length - from)))}, chunk {Show(Encoding.UTF8.GetString(actual, from, Math.Min(160, actual.Length - from)))}";
		}

		for (var row = lastRow + 1; row < startRow + chunk.Records; row++)
		{
			if (!Empty(row))
			{
				return $"{chunk.Where}: the serial row {row} has a cell, after the chunk's last record with one ({chunk.LastCellRecord})";
			}
		}

		var lastColumn = -1;
		var numberLike = false;
		for (var row = startRow + chunk.FirstCellRecord; row <= lastRow; row++)
		{
			var element = XElement.Parse(rows[(int)row - 1]);
			var cells = element.Elements("c").ToList();
			if (cells.Count > 0)
			{
				lastColumn = Math.Max(lastColumn, cells.Count - 1);
			}

			numberLike |= cells.Any(c => (string?)c.Attribute("t") == "inlineStr" && c.Element("is")?.Element("t")?.Value is { Length: > 0 } t && t[0] is >= '0' and <= '9' or '+' or '-' or '.');
		}

		if (lastColumn != chunk.LastColumn || numberLike != chunk.NumberLikeText)
		{
			return $"{chunk.Where}: the chunk says last column {chunk.LastColumn} and number-like {chunk.NumberLikeText}, and its serial rows {lastColumn} and {numberLike}";
		}

		var expectedLinks = links.Where(l => l.Row >= startRow && l.Row < startRow + chunk.Records).Select(l => new ChunkLink((int)(l.Row - startRow), l.Column, l.Target)).ToList();
		var chunkLinks = chunk.Links ?? [];
		if (chunk.Links is { Count: 0 } || !expectedLinks.SequenceEqual(chunkLinks))
		{
			return $"{chunk.Where}: the chunk's links [{string.Join(", ", chunkLinks)}], and the serial write's [{string.Join(", ", expectedLinks)}]";
		}

		return null;
	}

	/// <summary>A sheet's row elements, row 1 first: its rows have no <c>r</c>, so each is the one after the one before it.</summary>
	/// <param name="sheet">The sheet's XML.</param>
	/// <returns>The rows' text.</returns>
	private static List<string> RowsOf(string sheet)
	{
		var start = sheet.IndexOf("<sheetData>", StringComparison.Ordinal) + "<sheetData>".Length;
		var end = sheet.IndexOf("</sheetData>", start, StringComparison.Ordinal);
		var data = sheet.Substring(start, end - start);
		var rows = RowElement.Matches(data).Cast<Match>().Select(m => m.Value).ToList();
		Assert.Equal(data, string.Concat(rows));

		return rows;
	}

	/// <summary>A sheet's links: each one's row, column and target.</summary>
	/// <param name="package">The package.</param>
	/// <param name="sheet">The sheet's XML.</param>
	/// <returns>The links, in the sheet's order.</returns>
	private static List<(long Row, int Column, string Target)> LinksOf(byte[] package, string sheet)
	{
		var hyperlinks = XDocument.Parse(sheet).Root!.Element(Main + "hyperlinks")?.Elements(Main + "hyperlink").ToList() ?? [];
		if (hyperlinks.Count == 0)
		{
			return [];
		}

		var targets = XDocument.Parse(Written.Entry(package, "xl/worksheets/_rels/sheet1.xml.rels")).Root!.Elements()
			.ToDictionary(r => (string)r.Attribute("Id")!, r => (string)r.Attribute("Target")!);
		var links = new List<(long, int, string)>();
		foreach (var hyperlink in hyperlinks)
		{
			Assert.True(CellReference.TryParse(Encoding.ASCII.GetBytes((string)hyperlink.Attribute("ref")!), out var column, out var row));
			links.Add((row, column, targets[(string)hyperlink.Attribute(Relationships + "id")!]));
		}

		return links;
	}

	/// <summary>A chunk the owner would have appended, as the tests keep it to compare once the serial sheet is written.</summary>
	/// <param name="Start">The serial records before it, the header's included.</param>
	/// <param name="Records">Its records.</param>
	/// <param name="FirstCellRecord">Its first record with a cell, or -1.</param>
	/// <param name="LastCellRecord">Its last record with a cell, or -1.</param>
	/// <param name="LastColumn">Its last column, or -1.</param>
	/// <param name="NumberLikeText">Whether a text cell of it starts like a number.</param>
	/// <param name="Links">Its links, or <see langword="null"/>.</param>
	/// <param name="Xml">Its XML.</param>
	/// <param name="Where">Its batch, for a failure.</param>
	private sealed record Appended(long Start, int Records, int FirstCellRecord, int LastCellRecord, int LastColumn, bool NumberLikeText, List<ChunkLink>? Links, byte[] Xml, string Where);

	/// <summary>The first thing the serial write of a batch did that a worker can't: where, and what.</summary>
	private sealed class FirstEvent
	{
		/// <summary>The item, in the batch; -1 before one.</summary>
		public int Item { get; private set; } = -1;

		/// <summary>The field, in the item; -1 for a whole record.</summary>
		public int Field { get; private set; } = -1;

		/// <summary>Whether it added the link style.</summary>
		public bool Link { get; private set; }

		/// <summary>Whether it threw.</summary>
		public bool Error { get; private set; }

		/// <summary>Whether it added a cell format other than the link's.</summary>
		public bool Style { get; private set; }

		/// <summary>Whether there was one.</summary>
		public bool Found => Item >= 0;

		/// <summary>Notes what a field or a record did, if it is the first such thing.</summary>
		/// <param name="item">The item.</param>
		/// <param name="field">The field, -1 for a whole record, or the fields' count for the record's end.</param>
		/// <param name="before">The sheet as it was before.</param>
		/// <param name="after">The sheet as it is after.</param>
		/// <param name="threw">Whether it threw.</param>
		public void Note(int item, int field, SheetView before, SheetView after, bool threw)
		{
			var link = after.LinkStyle != before.LinkStyle;
			var added = after.Styles.Count - before.Styles.Count - (link && !before.Styles.TryIndexOf(LinkFormat, out _) ? 1 : 0);
			if (Found || (!link && !threw && added == 0))
			{
				return;
			}

			Item = item;
			Field = field;
			Link = link;
			Error = threw;
			Style = added > 0;
		}

		/// <inheritdoc/>
		public override string ToString() => string.Join(" and ", new[] { Link ? "added the link style" : null, Style ? "added a cell format" : null, Error ? "threw" : null }.Where(s => s is not null));
	}
}

/// <summary>
/// The writer fuzzers' records, from their own generators, for the drift tests. Their types are private to them, so they
/// are reached by reflection: a change to them fails here by name.
/// </summary>
internal static class FuzzedRecords
{
	/// <summary>The search of members the tests reach by reflection.</summary>
	private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

	/// <summary><see cref="TextFuzzTests"/>' rows of a seed: random text, written as strings, spans or UTF-8.</summary>
	/// <param name="seed">The seed.</param>
	/// <returns>The rows, each its fields' writers.</returns>
	public static List<List<Action<CsvWriter>>> Text(int seed)
	{
		var input = Call(Nested(typeof(TextFuzzTests), "Input"), "Of", seed)!;

		return Rows(Get(input, "Rows"));
	}

	/// <summary><see cref="PresentationFuzzTests"/>' sheets of a seed: random options, headers and data, URIs among it.</summary>
	/// <param name="seed">The seed.</param>
	/// <returns>The workbook's links and header, and each sheet's name, options, header and records.</returns>
	public static (ExcelLinks Links, bool HasHeader, List<(string Name, ExcelSheetOptions Options, List<Action<CsvWriter>> Header, List<List<Action<CsvWriter>>> Records)> Sheets) Presentation(int seed)
	{
		var input = Call(Nested(typeof(PresentationFuzzTests), "Input"), "Of", seed)!;
		var sheets = new List<(string, ExcelSheetOptions, List<Action<CsvWriter>>, List<List<Action<CsvWriter>>>)>();
		foreach (var sheet in (IEnumerable)Get(input, "Sheets")!)
		{
			sheets.Add(((string)Get(sheet, "Name")!, (ExcelSheetOptions)Get(sheet, "Options")!, Fields(Get(sheet, "Header")), Rows(Get(sheet, "Records"))));
		}

		return ((ExcelLinks)Get(input, "Links")!, (bool)Get(input, "HasHeader")!, sheets);
	}

	/// <summary>
	/// <see cref="FormatFuzzTests"/>' workbook of a seed, made as its end-to-end test makes it: random formats' columns in
	/// cultures, fields of the column's kind and others in its format and others, and now and then an empty record.
	/// </summary>
	/// <param name="seed">The seed.</param>
	/// <returns>The options (inline strings), the members' formats, and the rows after the header.</returns>
	public static (ExcelWriterOptions Options, MemberFormats Members, List<List<Action<CsvWriter>>> Rows) Formats(int seed)
	{
		var type = typeof(FormatFuzzTests);
		var column = Nested(type, "Column");
		var field = Nested(type, "Field");
		var kinds = (CellKind[])type.GetField("Kinds", Any)!.GetValue(null)!;
		// The fuzzer's draws, in its order: sync or async, the offsets, and the strings, which are inline here, since a sheet
		// of shared strings is written serially.
		var random = new Random(seed);
		_ = random.Next(2);
		var offsets = (ExcelDateTimeOffsets)random.Next(3);
		_ = random.Next(2);
		var options = new ExcelWriterOptions { DateTimeOffsets = offsets, Csv = new CsvWriterOptions { ValidateFieldCount = false } };
		var columns = new List<object>();
		for (var c = random.Next(1, 7); c > 0; c--)
		{
			var format = (string)Call(type, "RandomFormat", random)!;
			var culture = random.Next(4) == 0 ? null : (CultureInfo)Call(type, "RandomCulture", random)!;
			columns.Add(Activator.CreateInstance(column, format, culture, kinds[random.Next(kinds.Length)])!);
		}

		var rows = new List<List<Action<CsvWriter>>>();
		for (var r = random.Next(1, 6); r > 0; r--)
		{
			var empty = random.Next(6) == 0;
			rows.Add([.. columns.Select(c => Writer(empty ? Call(field, "Null", random, c)! : Call(field, "Of", random, c, options)!))]);
		}

		var members = new MemberFormats([.. columns.Select((c, i) => new MemberFormat("c" + i, 0, -1, (string)Get(c, "Format")!, (CultureInfo?)Get(c, "Culture")))]);

		return (options, members, rows);
	}

	/// <summary>A fuzzer's nested type.</summary>
	/// <param name="type">The fuzzer.</param>
	/// <param name="name">The type's name.</param>
	/// <returns>The type.</returns>
	private static Type Nested(Type type, string name) => type.GetNestedType(name, Any) ?? throw new InvalidOperationException($"{type.Name} has no type {name}.");

	/// <summary>A property's value.</summary>
	/// <param name="target">The object.</param>
	/// <param name="name">The property's name.</param>
	/// <returns>The value.</returns>
	private static object? Get(object target, string name) =>
		(target.GetType().GetProperty(name, Any) ?? throw new InvalidOperationException($"{target.GetType().Name} has no property {name}.")).GetValue(target);

	/// <summary>Calls a static method, the only one of its name.</summary>
	/// <param name="type">Its type.</param>
	/// <param name="name">Its name.</param>
	/// <param name="arguments">Its arguments.</param>
	/// <returns>What it returns.</returns>
	private static object? Call(Type type, string name, params object?[] arguments)
	{
		var method = type.GetMethods(Any).SingleOrDefault(m => m.Name == name && m.IsStatic) ?? throw new InvalidOperationException($"{type.Name} has no method {name}.");
		try
		{
			return method.Invoke(null, arguments);
		}
		catch (TargetInvocationException e) when (e.InnerException is not null)
		{
			System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();

			throw;
		}
	}

	/// <summary>A fuzzer's field's writer: its <c>WriteTo(CsvWriter)</c>.</summary>
	/// <param name="field">The field.</param>
	/// <returns>The writer, which throws what the field's write throws.</returns>
	private static Action<CsvWriter> Writer(object field) =>
		(Action<CsvWriter>)Delegate.CreateDelegate(typeof(Action<CsvWriter>), field, field.GetType().GetMethod("WriteTo", Any, null, [typeof(CsvWriter)], null)!);

	/// <summary>A list of a fuzzer's fields as their writers.</summary>
	/// <param name="fields">The list.</param>
	/// <returns>The writers.</returns>
	private static List<Action<CsvWriter>> Fields(object? fields) => [.. ((IEnumerable)fields!).Cast<object>().Select(Writer)];

	/// <summary>A list of a fuzzer's rows as their fields' writers.</summary>
	/// <param name="rows">The list.</param>
	/// <returns>The rows.</returns>
	private static List<List<Action<CsvWriter>>> Rows(object? rows) => [.. ((IEnumerable)rows!).Cast<object>().Select(Fields)];
}

/// <summary>
/// The drift tests of a parallel write's worker (<see cref="SheetChunkSink"/>, Phase 9's step 10): its cell and row code
/// is a copy of the sheet's sink's, so the same records written serially and through a worker must give the same rows,
/// byte for byte, once the empty rows before a chunk are added; and the worker must flag a chunk to be written again where,
/// and only where, the serial write adds a cell format or the link style, or throws (<see cref="ChunkDrift"/>). The
/// sheets are generated ones of every cell kind and option, a record type's, and the writer fuzzers' records.
/// <c>CSVHELPER_EXCEL_FUZZ_DRIFT</c> sets the number of sheets of each test, and <c>CSVHELPER_EXCEL_FUZZ_SEED</c> the
/// first seed.
/// </summary>
/// <param name="output">Shows what the chunks were.</param>
public class ChunkDriftTests(ITestOutputHelper output)
{
	/// <summary>The sheets each test writes, unless <c>CSVHELPER_EXCEL_FUZZ_DRIFT</c> says otherwise.</summary>
	private const int DefaultSheets = 300;

	/// <summary>The cultures of the writers and of the members.</summary>
	private static readonly CultureInfo[] Cultures = [CultureInfo.InvariantCulture, new("de-DE"), new("fr-FR"), new("en-US"), new("ja-JP")];

	/// <summary>Number format codes of columns: built-in ones, custom ones, a date's, a duration's, text and General.</summary>
	private static readonly string[] ColumnCodes = ["0.00", "#,##0", "0%", "0.000", "yyyy-mm-dd", "dd/mm/yyyy hh:mm", "[h]:mm:ss", "@", "General", "0.00E+00"];

	/// <summary>Members' formats of dates and times, which translate or don't.</summary>
	private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd.MM.yyyy HH:mm", "HH:mm:ss.fff", "MMM d, yyyy", "o", "yyyy-MM-dd HH:mm", "dd MMMM", "g"];

	/// <summary>Members' formats of dates alone, which a <c>DateOnly</c> formats.</summary>
	private static readonly string[] DayFormats = ["yyyy-MM-dd", "MMM d, yyyy", "dd MMMM", "o", "d"];

	/// <summary>Members' formats of times of day, which a <c>TimeOnly</c> formats.</summary>
	private static readonly string[] TimeFormats = ["HH:mm:ss.fff", "HH:mm", "o", "t"];

	/// <summary>Members' formats of durations: .NET's default, and others, which are text.</summary>
	private static readonly string[] DurationFormats = ["c", "hh\\:mm", "g"];

	/// <summary>URIs: each policy's and others, relative ones, and text that isn't one.</summary>
	private static readonly string[] UriTexts =
	[
		"https://example.com/a", "http://example.com/a b", "mailto:a@example.com", "ftp://example.com/x", "docs/report.txt", "#Other!B2",
		"https://example.com/" + Units(0xE9), "file:///C:/Temp/report.txt",
	];

	/// <summary>Text: number-like, with white space at its ends, escapes, the entities' characters and line breaks.</summary>
	private static readonly string[] Texts = ["a", "Zz", "007", "1.5", "-2", "+3", ".5", " lead", "trail ", "a&b<c>", "_x0041_", "two\nlines", "tab\t", Units(0xE9), "=A1"];

	/// <summary>The kinds of generated columns.</summary>
	private enum Kind
	{
		/// <summary>Integers.</summary>
		Integer,

		/// <summary>Integers of up to 19 digits.</summary>
		Long,

		/// <summary>Decimals.</summary>
		Decimal,

		/// <summary>Doubles, NaN and the infinities included.</summary>
		Double,

		/// <summary>Booleans.</summary>
		Bool,

		/// <summary>Dates and times, before 1900 too.</summary>
		DateTime,

		/// <summary>Offsets.</summary>
		DateTimeOffset,

		/// <summary>Durations, negative and too long ones included.</summary>
		TimeSpan,

		/// <summary>Dates (text on .NET Framework).</summary>
		DateOnly,

		/// <summary>Times of day (text on .NET Framework).</summary>
		TimeOnly,

		/// <summary>Text.</summary>
		Text,

		/// <summary>URIs.</summary>
		Uri,

		/// <summary>Formulas.</summary>
		Formula,
	}

	[Fact]
	public void GeneratedSheetsMatchTheSerialRows()
	{
		var tally = Run("generated", Generated);
		Assert.True(Got(tally, "chunks: appended") > 0 && Got(tally, "chunks: appended, without a cell") > 0, "No chunk was appended.");
		Assert.True(Got(tally, "chunks: redone, Style") > 0 && Got(tally, "chunks: redone, LinkStyle") > 0 && Got(tally, "chunks: redone, Error") > 0, "A redo's reason never came.");
		Assert.True(Got(tally, "views: frozen again") > 0, "No view was frozen again.");
	}

	[Fact]
	public void RecordTypesMatchTheSerialRows()
	{
		var tally = Run("record type", Records);
		Assert.True(Got(tally, "chunks: appended") > 0, "No chunk was appended.");
		Assert.True(Got(tally, "chunks: redone, Style") > 0 && Got(tally, "chunks: redone, Error") > 0, "A redo's reason never came.");
	}

	[Fact]
	public void TextFuzzRecordsMatchTheSerialRows()
	{
		var tally = Run("text fuzz", seed =>
		{
			var rows = FuzzedRecords.Text(seed);
			var random = new Random(seed);

			return [new DriftSheet
			{
				Name = $"text fuzz seed {seed}",
				Options = Written.Ragged(),
				Add = book => book.AddSheet("Text"),
				Items = [.. rows.Select((row, r) => DriftItem.Of(row, $"row {r + 1}"))],
				Prefix = 1 + random.Next(rows.Count),
				Seed = seed,
				MaxBatch = random.Next(1, 5),
			}];
		});
		Assert.True(Got(tally, "chunks: appended") > 0, "No chunk was appended.");
		Assert.True(Got(tally, "chunks: redone, Error") > 0, "No long text was flagged.");
	}

	[Fact]
	public void PresentationFuzzRecordsMatchTheSerialRows()
	{
		var tally = Run("presentation fuzz", seed =>
		{
			var (links, hasHeader, sheets) = FuzzedRecords.Presentation(seed);
			var random = new Random(seed);

			return [.. sheets.Select(sheet =>
			{
				List<DriftItem> items = hasHeader ? [DriftItem.Of(sheet.Header, "header")] : [];
				items.AddRange(sheet.Records.Select((record, r) => DriftItem.Of(record, $"record {r + 1}")));

				return new DriftSheet
				{
					Name = $"presentation fuzz seed {seed}, sheet {Show(sheet.Name)}",
					Options = new ExcelWriterOptions { Links = links, Csv = new CsvWriterOptions { HasHeader = hasHeader, ValidateFieldCount = false } },
					Add = book => book.AddSheet(sheet.Name, sheet.Options),
					Items = items,
					Prefix = (hasHeader ? 1 : 0) + random.Next(sheet.Records.Count + 1),
					Flush = random.Next(2) == 0,
					Seed = seed,
					MaxBatch = random.Next(1, 5),
				};
			})];
		});
		Assert.True(Got(tally, "chunks: appended") > 0, "No chunk was appended.");
		Assert.True(Got(tally, "chunks: redone, LinkStyle") > 0, "No link was flagged.");
	}

	[Fact]
	public void FormatFuzzRecordsMatchTheSerialRows()
	{
		var tally = Run("format fuzz", seed =>
		{
			var (options, members, rows) = FuzzedRecords.Formats(seed);
			var random = new Random(seed);
			var header = DriftItem.Values([.. Enumerable.Range(0, rows[0].Count).Select(i => (object?)("c" + i))]);

			return [new DriftSheet
			{
				Name = $"format fuzz seed {seed}",
				Options = options,
				Add = book => book.AddSheet("Formats", members),
				Items = [header, .. rows.Select((row, r) => DriftItem.Of(row, $"row {r + 1}"))],
				Prefix = 1 + random.Next(rows.Count),
				Seed = seed,
				MaxBatch = random.Next(1, 4),
			}];
		});
		Assert.True(Got(tally, "chunks: appended") > 0, "No chunk was appended.");
		Assert.True(Got(tally, "chunks: redone, Style") > 0, "No translation's style was flagged.");
	}

	/// <summary>A count of a tally, 0 for none.</summary>
	/// <param name="tally">The tally.</param>
	/// <param name="key">The key.</param>
	/// <returns>The count.</returns>
	private static int Got(SortedDictionary<string, int> tally, string key) => tally.TryGetValue(key, out var count) ? count : 0;

	/// <summary>A pick of a list.</summary>
	/// <typeparam name="T">The items' type.</typeparam>
	/// <param name="random">The random numbers.</param>
	/// <param name="items">The list.</param>
	/// <returns>The item.</returns>
	private static T Pick<T>(Random random, IReadOnlyList<T> items) => items[random.Next(items.Count)];

	/// <summary>
	/// A generated sheet of a seed: columns of each kind, members' formats and cultures, the writer's culture, every option
	/// that changes a cell (links' policies, offsets, long numbers, dates out of range), columns' formats and wrap, a title,
	/// a bold header, auto-fit and <see cref="ExcelSheetOptions.Protect"/>; records with gaps, empty records, number-like
	/// text, long text now and then, and values of other kinds than their column's.
	/// </summary>
	/// <param name="seed">The seed.</param>
	/// <returns>The sheet.</returns>
	private static List<DriftSheet> Generated(int seed)
	{
		var random = new Random(seed);
		var hasHeader = random.Next(6) != 0;
		var provider = Pick(random, Cultures);
		var kinds = Enum.GetValues(typeof(Kind)).Cast<Kind>().ToArray();
		var width = random.Next(1, 9);
		var columns = Enumerable.Range(0, width).Select(_ => GeneratedColumn(random, kinds)).ToList();
		Dictionary<string, ExcelColumnOptions>? columnOptions = null;
		if (hasHeader && random.Next(2) == 0)
		{
			columnOptions = new Dictionary<string, ExcelColumnOptions>(StringComparer.Ordinal);
			for (var c = 0; c < width; c++)
			{
				var format = random.Next(2) == 0 ? Pick(random, ColumnCodes) : null;
				var wrap = random.Next(3) == 0;
				if (format is not null || wrap)
				{
					columnOptions["c" + c] = new ExcelColumnOptions { Format = format, Wrap = wrap };
				}
			}
		}

		// Under Protect the sheet's start registers the unlocked columns' styles, so the ready rule waits for it: no auto-fit
		// here, and the start written before the view is frozen. It needs a header, which it locks.
		var protect = hasHeader && random.Next(3) == 0;
		var options = new ExcelWriterOptions
		{
			Links = Pick(random, new[] { ExcelLinks.None, ExcelLinks.Web, ExcelLinks.Web, ExcelLinks.All }),
			DateTimeOffsets = Pick(random, new[] { ExcelDateTimeOffsets.Text, ExcelDateTimeOffsets.Utc, ExcelDateTimeOffsets.ClockTime }),
			LongNumbers = Pick(random, new[] { ExcelLongNumbers.Text, ExcelLongNumbers.Number, ExcelLongNumbers.Throw }),
			OutOfRangeDates = Pick(random, new[] { ExcelOutOfRangeDates.Text, ExcelOutOfRangeDates.Throw }),
			Csv = new CsvWriterOptions { HasHeader = hasHeader, ValidateFieldCount = false, FormatProvider = provider },
			Sheets = new ExcelSheetOptions
			{
				Title = random.Next(4) == 0 ? "Report" : null,
				BoldHeader = random.Next(2) == 0,
				FreezeHeader = random.Next(2) == 0,
				AutoFit = protect ? 0 : Pick(random, new[] { 0, 0, 2, 100 }),
				Protect = protect,
				Columns = columnOptions,
			},
		};
		var members = new MemberFormats([.. columns.Select((c, i) => new MemberFormat("c" + i, 0, -1, c.Format, c.Culture))]);
		var items = new List<DriftItem>();
		if (hasHeader)
		{
			items.Add(DriftItem.Values([.. columns.Select((_, i) => (object?)("c" + i))]));
		}

		for (var r = random.Next(0, 80); r > 0; r--)
		{
			if (random.Next(10) == 0)
			{
				// An empty record, whose row the next record with a cell writes.
				items.Add(DriftItem.Values([.. Enumerable.Repeat<object?>(null, random.Next(1, width + 1))]));

				continue;
			}

			var count = Math.Max(1, width + (random.Next(5) == 0 ? random.Next(-2, 3) : 0));
			var values = new object?[count];
			for (var c = 0; c < count; c++)
			{
				values[c] = random.Next(5) == 0 ? null : Value(random, c < width ? columns[c] : GeneratedColumn(random, kinds), kinds, provider);
			}

			items.Add(DriftItem.Values(values));
		}

		var header = hasHeader ? 1 : 0;

		return [new DriftSheet
		{
			Name = $"generated seed {seed}: {(hasHeader ? "" : "no header, ")}{provider.Name}, links {options.Links}, offsets {options.DateTimeOffsets}, long numbers {options.LongNumbers}, dates {options.OutOfRangeDates}" +
				$"{(options.Sheets.Protect ? ", protected" : "")}{(options.Sheets.Title is null ? "" : ", title")}, auto-fit {options.Sheets.AutoFit}, columns [{string.Join(", ", columns)}]" +
				$"{(columnOptions is null ? "" : ", options " + string.Join(", ", columnOptions.Select(o => $"{o.Key} {o.Value.Format} {(o.Value.Wrap ? "wrap" : "")}")))}",
			Options = options,
			Add = book => book.AddSheet("Data", members),
			Items = items,
			Prefix = header + (protect ? 1 : 0) + random.Next(0, Math.Max(1, ((items.Count - header) / 2) + 1)),
			Flush = protect || random.Next(2) == 0,
			Seed = seed,
			MaxBatch = Pick(random, new[] { 1, 3, 8, 32 }),
			ChunkLength = random.Next(4) == 0 ? random.Next(1, 1000) : 0,
		}];
	}

	/// <summary>A generated column: a kind, and its member's format and culture, if any.</summary>
	/// <param name="random">The random numbers.</param>
	/// <param name="kinds">The kinds.</param>
	/// <returns>The column.</returns>
	private static Column GeneratedColumn(Random random, Kind[] kinds)
	{
		var kind = Pick(random, kinds);
		string? format = null;
		CultureInfo? culture = null;
		switch (kind)
		{
			case Kind.DateTime or Kind.DateTimeOffset or Kind.DateOnly or Kind.TimeOnly when random.Next(2) == 0:
				format = Pick(random, FormatsOf(kind));
				culture = random.Next(3) == 0 ? Pick(random, Cultures) : null;

				break;
			case Kind.TimeSpan when random.Next(3) == 0:
				format = Pick(random, DurationFormats);

				break;
			case Kind.Integer or Kind.Decimal or Kind.Double when random.Next(4) == 0:
				culture = Pick(random, Cultures);

				break;
		}

		return new Column(kind, format, culture);
	}

	/// <summary>A value of a column: of its kind, or now and then another's; in its member's format and culture when it has them.</summary>
	/// <param name="random">The random numbers.</param>
	/// <param name="column">The column.</param>
	/// <param name="kinds">The kinds.</param>
	/// <param name="provider">The writer's culture.</param>
	/// <returns>The value.</returns>
	private static object? Value(Random random, Column column, Kind[] kinds, CultureInfo provider)
	{
		var kind = random.Next(6) == 0 ? Pick(random, kinds) : column.Kind;
		object value = kind switch
		{
			Kind.Integer => random.Next(-1000, 1000),
			Kind.Long => Pick(random, new[] { 0L, 1_234_567_890_123L, 1_234_567_890_123_456_789L, -98_765_432_109_876_543L }),
			Kind.Decimal => Pick(random, new[] { 0.1m, -7m, 12_345_678_901_234_567m, 1.23456789012345678m, 2.5m }),
			Kind.Double => Pick(random, new[] { 0.5, -1234.5678, 1e15, 1e-7, double.NaN, double.PositiveInfinity, 3.0 }),
			Kind.Bool => random.Next(2) == 0,
			Kind.DateTime => Pick(random, new[] { new DateTime(2024, 1, 15), new DateTime(2024, 1, 15, 10, 30, 0), new DateTime(2024, 1, 15, 10, 30, 0, 123), new DateTime(1899, 12, 31), new DateTime(1900, 2, 28, 23, 59, 59), DateTime.MaxValue, new DateTime(2024, 3, 1, 8, 0, 0, DateTimeKind.Utc) }),
			Kind.DateTimeOffset => Pick(random, new[] { new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.FromHours(2)), new DateTimeOffset(1899, 12, 31, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.FromHours(-5)), new DateTimeOffset(2024, 6, 1, 12, 0, 0, 250, TimeSpan.Zero) }),
			Kind.TimeSpan => Pick(random, new[] { TimeSpan.FromHours(1.5), TimeSpan.FromDays(1000), TimeSpan.FromMilliseconds(1500), TimeSpan.FromTicks(-1), TimeSpan.FromHours(-2), TimeSpan.MaxValue, TimeSpan.Zero }),
#if NET
			Kind.DateOnly => Pick(random, new[] { new DateOnly(2024, 1, 15), new DateOnly(1899, 12, 31), new DateOnly(1900, 3, 1) }),
			Kind.TimeOnly => Pick(random, new[] { new TimeOnly(10, 30), new TimeOnly(10, 30, 15, 250), TimeOnly.MinValue }),
#else
			Kind.DateOnly => "2024-01-15",
			Kind.TimeOnly => "10:30:00",
#endif
			Kind.Text => random.Next(40) == 0 ? new string('x', XmlOutput.MaxCellLength + random.Next(0, 2)) : Pick(random, Texts),
			Kind.Uri => Uri.TryCreate(Pick(random, UriTexts), UriKind.RelativeOrAbsolute, out var uri) ? uri : (object)"not a URI",
			_ => new ExcelFormula(Pick(random, new[] { "=A1+1", "SUM(A1:A3)", "1+1", "IF(A1>1,\"x\",\"y\")" })),
		};

		if (kind != column.Kind || (column.Format is null && column.Culture is null) || value is bool or string or Uri or ExcelFormula)
		{
			return value;
		}

		// Its member's format, or now and then another, as a record writer writes it; in its culture, else the writer's.
		var format = column.Format is null ? "" : random.Next(8) == 0 ? Pick(random, FormatsOf(kind)) : column.Format;

		return new DriftFormatted(value, format, column.Culture ?? provider);
	}

	/// <summary>The members' formats a kind of value formats with.</summary>
	/// <param name="kind">The kind: a date, a time or a duration.</param>
	/// <returns>The formats.</returns>
	private static string[] FormatsOf(Kind kind) => kind switch
	{
		Kind.DateOnly => DayFormats,
		Kind.TimeOnly => TimeFormats,
		Kind.TimeSpan => DurationFormats,
		_ => DateFormats,
	};

	/// <summary>A generated sheet of <see cref="DriftRecord"/>s, written whole by its record writer, under random options.</summary>
	/// <param name="seed">The seed.</param>
	/// <returns>The sheet.</returns>
	private static List<DriftSheet> Records(int seed)
	{
		var random = new Random(seed);
		var protect = random.Next(3) == 0;
		var sheetOptions = new ExcelSheetOptions
		{
			AutoFit = protect ? 0 : Pick(random, new[] { 0, 2, 100 }),
			Protect = protect,
			Validate = random.Next(3) == 0,
			Title = random.Next(4) == 0 ? "Records" : null,
			Columns = random.Next(2) == 0
				? new Dictionary<string, ExcelColumnOptions>(StringComparer.Ordinal)
				{
					["Price"] = new() { Format = "0.00" },
					["When"] = new() { Wrap = true },
					[Pick(random, new[] { "Name", "Link", "Took", "Day" })] = new() { Format = Pick(random, ColumnCodes), Wrap = random.Next(2) == 0 },
				}
				: null,
		};
		var options = new ExcelWriterOptions
		{
			Links = Pick(random, new[] { ExcelLinks.None, ExcelLinks.Web, ExcelLinks.All }),
			DateTimeOffsets = Pick(random, new[] { ExcelDateTimeOffsets.Text, ExcelDateTimeOffsets.Utc, ExcelDateTimeOffsets.ClockTime }),
			LongNumbers = Pick(random, new[] { ExcelLongNumbers.Text, ExcelLongNumbers.Number, ExcelLongNumbers.Throw }),
			OutOfRangeDates = Pick(random, new[] { ExcelOutOfRangeDates.Text, ExcelOutOfRangeDates.Throw }),
			Csv = new CsvWriterOptions { FormatProvider = Pick(random, Cultures) },
		};
		var items = new List<DriftItem> { DriftItem.Record(writer => writer.WriteHeader<DriftRecord>(), "header") };
		for (var r = random.Next(0, 60); r > 0; r--)
		{
			var record = new DriftRecord
			{
				Id = random.Next(-100, 100000),
				Name = random.Next(5) == 0 ? null : Pick(random, Texts),
				Big = random.Next(6) == 0 ? 1_234_567_890_123_456_789L : random.Next(),
				Price = random.Next(1000) / 100m,
				Ratio = random.Next(10) == 0 ? double.NaN : random.NextDouble(),
				Flag = random.Next(2) == 0,
				When = random.Next(8) == 0 ? new DateTime(1899, 12, 31) : new DateTime(2024, 1, 1).AddMinutes(random.Next(100000)).AddMilliseconds(random.Next(3) == 0 ? random.Next(1000) : 0),
				Day = new DateTime(2024, 1, 1).AddMinutes(random.Next(100000)),
				Month = new DateTime(2024, random.Next(1, 13), 1),
				Stamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.FromHours(random.Next(-5, 6))),
				Stamped = new DateTimeOffset(2024, 2, 1, 8, 0, 0, TimeSpan.FromHours(1)),
				Took = random.Next(8) == 0 ? TimeSpan.FromMinutes(-5) : TimeSpan.FromSeconds(random.Next(100000)),
				Amount = random.Next(100000) / 100m,
				Link = random.Next(3) == 0 ? null : new Uri(Pick(random, new[] { "https://example.com/a", "mailto:a@example.com", "ftp://example.com/x" })),
				Total = random.Next(3) == 0 ? default : new ExcelFormula("B2*2"),
				Maybe = random.Next(3) == 0 ? null : random.Next(10),
			};
			items.Add(DriftItem.Record(writer => writer.WriteRecord(record), $"record {record.Id}"));
		}

		return [new DriftSheet
		{
			Name = $"record type seed {seed}: links {options.Links}, offsets {options.DateTimeOffsets}, long numbers {options.LongNumbers}, dates {options.OutOfRangeDates}, {((CultureInfo)options.Csv.FormatProvider!).Name}" +
				$"{(sheetOptions.Protect ? ", protected" : "")}{(sheetOptions.Validate ? ", validated" : "")}{(sheetOptions.Columns is null ? "" : ", columns' options")}",
			Options = options,
			Add = book => book.AddSheet<DriftRecord>("Data", sheetOptions),
			Items = items,
			Prefix = 1 + (protect ? 1 : 0) + random.Next(0, Math.Max(1, items.Count / 2)),
			Flush = protect || random.Next(2) == 0,
			Seed = seed,
			MaxBatch = Pick(random, new[] { 1, 4, 16 }),
		}];
	}

	/// <summary>Checks the sheets of each seed (<see cref="ChunkDrift.Check"/>), and shows what their chunks were.</summary>
	/// <param name="what">What the sheets are.</param>
	/// <param name="sheets">The sheets of a seed.</param>
	/// <returns>The tally of the chunks.</returns>
	private SortedDictionary<string, int> Run(string what, Func<int, List<DriftSheet>> sheets)
	{
		var count = Setting("CSVHELPER_EXCEL_FUZZ_DRIFT", DefaultSheets);
		var first = Setting("CSVHELPER_EXCEL_FUZZ_SEED", 1);
		var failures = new List<string>();
		var tally = new SortedDictionary<string, int>(StringComparer.Ordinal);
		for (var seed = first; seed < first + count && failures.Count < MaxFailures; seed++)
		{
			foreach (var sheet in sheets(seed))
			{
				try
				{
					if (ChunkDrift.Check(sheet, tally) is { } problem)
					{
						failures.Add($"{sheet}: {problem}");
					}
				}
				catch (Exception e)
				{
					failures.Add($"{sheet}: {Describe(e)}");
				}
			}
		}

		output.WriteLine($"{what}, seeds {first} to {first + count - 1}:");
		foreach (var pair in tally)
		{
			output.WriteLine($"  {pair.Key}: {pair.Value}");
		}

		AssertNone(failures, "CSVHELPER_EXCEL_FUZZ_DRIFT");

		return tally;
	}

	/// <summary>A generated column.</summary>
	/// <param name="Kind">The kind of its values.</param>
	/// <param name="Format">Its member's format, or <see langword="null"/>.</param>
	/// <param name="Culture">Its member's culture, or <see langword="null"/> for the writer's.</param>
	private sealed record Column(Kind Kind, string? Format, CultureInfo? Culture)
	{
		/// <inheritdoc/>
		public override string ToString() => $"{Kind}{(Format is null ? "" : " " + Show(Format))}{(Culture is null ? "" : " in " + (Culture.Name.Length == 0 ? "invariant" : Culture.Name))}";
	}
}
