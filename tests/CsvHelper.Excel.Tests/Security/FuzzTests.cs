using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper.Excel.Tests.Api;
using CsvHelper.Excel.Tests.Corpus;
using CsvHelper.Excel.Tests.Sheet;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// A record of typed members by position, for the fuzzer's typed reading: whatever cell a field holds goes into each kind
/// of member in turn, as the fields move along the record's members.
/// </summary>
[CsvRecord]
public partial class FuzzRecord
{
	/// <summary>Field 0, as text.</summary>
	[CsvColumn(Index = 0)]
	public string? Text { get; set; }

	/// <summary>Field 1, as a <see cref="double"/>.</summary>
	[CsvColumn(Index = 1)]
	public double? Number { get; set; }

	/// <summary>Field 2, as a <see cref="decimal"/>.</summary>
	[CsvColumn(Index = 2)]
	public decimal? Exact { get; set; }

	/// <summary>Field 3, as a <see cref="long"/>.</summary>
	[CsvColumn(Index = 3)]
	public long? Whole { get; set; }

	/// <summary>Field 4, as a <see cref="DateTime"/>.</summary>
	[CsvColumn(Index = 4)]
	public DateTime? Date { get; set; }

	/// <summary>Field 5, as a <see cref="TimeSpan"/>.</summary>
	[CsvColumn(Index = 5)]
	public TimeSpan? Duration { get; set; }

	/// <summary>Field 6, as a <see cref="bool"/>.</summary>
	[CsvColumn(Index = 6)]
	public bool? Flag { get; set; }

	/// <summary>Field 7, as a <see cref="DateTimeOffset"/>.</summary>
	[CsvColumn(Index = 7)]
	public DateTimeOffset? Moment { get; set; }

	/// <summary>Field 8, as an <see cref="int"/>.</summary>
	[CsvColumn(Index = 8)]
	public int? Small { get; set; }

#if NET
	/// <summary>Field 9, as a <see cref="DateOnly"/>.</summary>
	[CsvColumn(Index = 9)]
	public DateOnly? Day { get; set; }

	/// <summary>Field 10, as a <see cref="TimeOnly"/>.</summary>
	[CsvColumn(Index = 10)]
	public TimeOnly? Time { get; set; }
#endif
}

/// <summary>
/// Seeded mutation fuzzing, as the plan's "Fuzzing" describes: workbooks from synthetic packages and from the corpus,
/// mutated in an XML part (their cells, and their columns, merged ranges, defined names and tables) or in their zip bytes,
/// each read through the public API to the end of every worksheet, with formulas read as values or as text, each policy
/// for error cells, and several significant digits. Two seeds in three also read an area of each sheet, or one of the
/// workbook's own tables, defined names or a block of a sheet named with it, with a header row given or found among the
/// area's rows, skipping hidden rows and columns and filling merged cells. A seed reads its fields as strings, as typed
/// values (where <c>TryParse</c> and <c>Parse</c> must agree), or as typed records. Each input must finish within five
/// seconds, and either read or throw <see cref="InvalidDataException"/>, <see cref="CsvFormatException"/> or
/// <see cref="NotSupportedException"/>; a field may only fail to parse, with <see cref="FormatException"/> or
/// <see cref="OverflowException"/>, and an error cell fail to read when error cells fail every read. What a seed reads
/// comes from the workbook, so choosing it never fails: a table, a name or a sheet it has, and a header row it has.
/// <c>CSVHELPER_EXCEL_FUZZ_INPUTS</c> sets the number of inputs, and <c>CSVHELPER_EXCEL_FUZZ_SEED</c> the first seed, so
/// that a failure can be replayed alone.
/// </summary>
/// <param name="output">Shows how the inputs ended.</param>
public class FuzzTests(ITestOutputHelper output)
{
	/// <summary>The number of inputs a run reads, unless <c>CSVHELPER_EXCEL_FUZZ_INPUTS</c> says otherwise.</summary>
	private const int DefaultInputs = 3000;

	/// <summary>The records of each sheet whose fields are parsed as typed values; the rest are read as strings, for time.</summary>
	private const int ParsedRecords = 64;

	/// <summary>The types a field is parsed as, of which <c>Parse</c> is called for one in turn: exceptions are slow.</summary>
	private const int ParsedTypes = 11;

	/// <summary>How long one input may take.</summary>
	private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Markup and values the XML mutations insert: the scanner's delimiters, its elements, formulas and their grammar's
	/// delimiters, ISO 8601 texts, and edge cases.
	/// </summary>
	private static readonly string[] Tokens =
	[
		"<", ">", "&", "\"", "'", "/", "=", "<row>", "</row>", "<row r=\"1048576\">", "<c>", "</c>", "<c r=\"XFD1048576\">", "<c r=\"A0\">",
		"<v>", "</v>", "<v/>", "<is>", "</is>", "<t>", "</t>", "<r>", "</r>", "<si>", "</si>", "<rPh>", "<sheetData>", "</sheetData>",
		"<x:c>", "<![CDATA[", "]]>", "<!--", "-->", "<?pi?>", "<!DOCTYPE x>", "&amp;", "&#0;", "&#x10FFFF;", "&#xD800;", "&bogus;",
		"_x0041_", "_xD83D_", "_x00", " xml:space=\"preserve\"", " t=\"s\"", " t=\"inlineStr\"", " t=\"b\"", " t=\"e\"", " t=\"d\"",
		" s=\"99999\"", " s=\"1\"", " s=\"3\"", " r=\"\"", " r=\"A1\"", "\uFEFF", "\u0000", "\r\n",
		"<f>", "</f>", "<f/>", "<f t=\"shared\" ref=\"A1:XFD1048576\" si=\"0\">", "<f t=\"shared\" si=\"0\"/>", "<f t=\"array\" ref=\"A1:A2\">",
		"<f t=\"dataTable\" ref=\"A1\"/>", " t=\"shared\"", " si=\"0\"", " si=\"1\"", " si=\"-1\"", " si=\"2147483648\"", " ref=\"A1\"",
		"$", "[", "]", "{", "}", "(", ")", "!", ":", "#", "#REF!", "A1", "$A$1", "XFD1048576", "1048576:1048576", "A:XFD", "'Q1 Orders'!",
		"_xlfn.", "P", "T", "PT36H0M0.000S", "P1DT12H", "9999-12-31T23:59:59.999", "1899-12-31", "1900-02-29", "1904-01-01", "24:00",
		".99999999999999999999", "Z",
		"<cols>", "</cols>", "<col/>", "<col min=\"1\" max=\"16384\" hidden=\"1\"/>", "<col min=\"2\" max=\"3\" width=\"0\"/>", " min=\"0\"", " max=\"16385\"",
		" hidden=\"1\"", " hidden=\"true\"", " ht=\"0\"", " customHeight=\"1\"", " zeroHeight=\"1\"", " defaultColWidth=\"0\"", " defaultRowHeight=\"0\"",
		" width=\"0\"", " width=\"\"", "<mergeCells>", "</mergeCells>", "<mergeCell ref=\"A1:B2\"/>", "<mergeCell ref=\"A1:XFD1048576\"/>",
		"<mergeCell ref=\"B2\"/>", "<mergeCell/>", "<definedNames>", "</definedNames>", "<definedName name=\"N\">Data!$A$1:$B$3</definedName>",
		" localSheetId=\"0\"", " localSheetId=\"1\"", "_xlnm.", "Print_Area", "<tableColumns count=\"1\">", "</tableColumns>",
		"<tableColumn id=\"1\" name=\"X\"/>", " headerRowCount=\"0\"", " totalsRowCount=\"1\"", " ref=\"A1:XFD1048576\"", " displayName=\"T\"",
	];

	/// <summary>Values the XML mutations put in place of a run of digits.</summary>
	private static readonly string[] Numbers =
	[
		"0", "-1", "1048577", "2147483648", "99999999999999999999", "1E+308", "1E+309", "4.9E-324", "-0", "NaN", "60", "2958466", "",
	];

	/// <summary>Names the structure mutations give a table's columns: plain, empty, with white space, an escape, one alike in case.</summary>
	private static readonly string[] ColumnNames = ["X", "", " Name ", "_x000a_", "name", "Total", "&amp;"];

	/// <summary>The significant digits a seed reads numbers to.</summary>
	private static readonly int?[] Digits = [15, 17, null, 1, 28];

	/// <summary>The workbooks the inputs start from, loaded once.</summary>
	private static readonly Lazy<List<Source>> Sources = new(LoadSources);

	/// <summary>How a seed reads each sheet's fields.</summary>
	private enum ReadMode
	{
		/// <summary>Each field as a string.</summary>
		Strings,

		/// <summary>Each field as a string, then as each type a cell can convert to, with <c>TryParse</c> and <c>Parse</c>.</summary>
		Fields,

		/// <summary>Each record as a <see cref="FuzzRecord"/>, the records that can't be read going to <c>OnRecordError</c>.</summary>
		Records,
	}

	/// <summary>What a seed reads besides every worksheet whole.</summary>
	private enum AreaMode
	{
		/// <summary>Every worksheet whole, and nothing else.</summary>
		Sheet,

		/// <summary>A random block of each worksheet, as a range without a sheet.</summary>
		Range,

		/// <summary>Every worksheet whole, then one of the workbook's tables.</summary>
		Table,

		/// <summary>Every worksheet whole, then one of the workbook's defined names that is a block of cells.</summary>
		DefinedName,

		/// <summary>Every worksheet whole, then a random block of one of them, as a range with the sheet's name.</summary>
		SheetRange,
	}

	/// <summary>How a seed gives the header row, when it reads a header.</summary>
	private enum HeaderMode
	{
		/// <summary>The area's first record, as without Phase 3.</summary>
		None,

		/// <summary>A row of the area with a value, given (<see cref="ExcelHeaderRow.At"/>).</summary>
		At,

		/// <summary>A search for the texts of a row of the area with a value (<see cref="ExcelHeaderRow.Find(IEnumerable{string}, int)"/>).</summary>
		Find,
	}

	[Fact]
	public async Task ReadsOrRejectsEveryMutatedWorkbook()
	{
		var inputs = Setting("CSVHELPER_EXCEL_FUZZ_INPUTS", DefaultInputs);
		var first = Setting("CSVHELPER_EXCEL_FUZZ_SEED", 1);
		var outcomes = new SortedDictionary<string, int>(StringComparer.Ordinal);
		var reads = new SortedDictionary<string, int>(StringComparer.Ordinal);
		var failures = new List<string>();
		for (var seed = first; seed < first + inputs && failures.Count < 5; seed++)
		{
			var settings = Settings.Of(seed);
			var input = Mutate(seed, settings.Area);
			var events = new List<string>();
			var run = Task.Run(() => ReadAsync(input.Zip, settings, events));
			using (var stop = new CancellationTokenSource())
			{
				var finished = await Task.WhenAny(run, Task.Delay(TimeLimit, stop.Token));
				stop.Cancel();
				if (finished != run)
				{
					// The input's thread goes on; the run stops here, since it may be using a core.
					failures.Add($"{input.Describe(seed, settings)}: still running after {TimeLimit.TotalSeconds} s");

					break;
				}
			}

			try
			{
				Count(outcomes, $"{settings.Mode}: {await run}");
				foreach (var read in events)
				{
					Count(reads, read);
				}
			}
			catch (Exception e)
			{
				failures.Add($"{input.Describe(seed, settings)}: {e.GetType().FullName}: {e.Message}\n{string.Join("\n", (e.StackTrace ?? "").Split('\n').Take(12))}");
			}
		}

		output.WriteLine($"Seeds {first} to {first + inputs - 1}:");
		foreach (var pair in outcomes)
		{
			output.WriteLine($"  {pair.Key}: {pair.Value}");
		}

		output.WriteLine("Their reads of areas and header rows:");
		foreach (var pair in reads)
		{
			output.WriteLine($"  {pair.Key}: {pair.Value}");
		}

		Assert.True(failures.Count == 0, string.Join("\n\n", failures) + "\n\nReplay one with CSVHELPER_EXCEL_FUZZ_SEED=<seed> and CSVHELPER_EXCEL_FUZZ_INPUTS=1.");
	}

	/// <summary>
	/// Reads a workbook to the end of each worksheet, or of an area of each, and then, as the seed says, one of its tables,
	/// defined names or blocks of a sheet, under small limits.
	/// </summary>
	/// <param name="zip">The workbook's bytes.</param>
	/// <param name="settings">How to read it.</param>
	/// <param name="events">Takes how each read of an area or with a header row ended, for the run's tally.</param>
	/// <returns>
	/// How it ended: <c>read</c>, or the expected exception that ended it (or that the first sheet to fail threw, or the
	/// workbook's table, name or block).
	/// </returns>
	private static async Task<string> ReadAsync(byte[] zip, Settings settings, List<string> events)
	{
		var async = settings.Async;
		var choices = new Random(settings.Choices);
		try
		{
			using var workbook = async ? await ExcelWorkbook.OpenAsync(new MemoryStream(zip), settings.Options) : ExcelWorkbook.Open(new MemoryStream(zip), settings.Options);
			var outcome = "read";
			foreach (var sheet in workbook.Sheets.Where(s => s.Kind == ExcelSheetKind.Worksheet))
			{
				try
				{
					var options = settings.Options;
					var firstRow = 1;
					if (settings.Area == AreaMode.Range)
					{
						var block = RandomBlock(choices);
						options = options with { Range = block.ToString() };
						firstRow = block.FirstRow;
					}

					options = await WithHeaderAsync(workbook, sheet, options, firstRow, settings, choices);
					await TallyAsync(events, settings, options, ReadAreaAsync(workbook, sheet, options, settings));
				}
				catch (Exception e) when (IsExpected(e))
				{
					outcome = outcome == "read" ? "a sheet threw " + e.GetType().Name : outcome;
				}
			}

			if (settings.Area is AreaMode.Table or AreaMode.DefinedName or AreaMode.SheetRange)
			{
				if (Target(workbook, settings, choices) is not { } chosen)
				{
					events.Add($"{settings.Area}: none in the workbook");

					return outcome;
				}

				try
				{
					// A table's header is its column names, so it takes no header row.
					var (target, targetFirstRow) = chosen;
					var options = target.Table is null ? await WithHeaderAsync(workbook, null, target, targetFirstRow, settings, choices) : target;
					await TallyAsync(events, settings, options, ReadAreaAsync(workbook, null, options, settings));
				}
				catch (Exception e) when (IsExpected(e))
				{
					outcome = outcome == "read" ? $"the {settings.Area} threw {e.GetType().Name}" : outcome;
				}
			}

			return outcome;
		}
		catch (Exception e) when (IsExpected(e))
		{
			return "the workbook threw " + e.GetType().Name;
		}
	}

	/// <summary>
	/// Waits for a read, and notes how it ended when it read an area or had a header row given: its area (a range, a
	/// table, a defined name, or a range with a sheet's name) and its header row, read or with the exception it threw.
	/// </summary>
	/// <param name="events">Takes the notes.</param>
	/// <param name="settings">How the seed reads.</param>
	/// <param name="options">The read's options.</param>
	/// <param name="read">The read.</param>
	/// <returns>A task that completes when the read does, and fails as it does.</returns>
	private static async Task TallyAsync(List<string> events, Settings settings, ExcelReaderOptions options, Task read)
	{
		var area = options.Table is not null ? "Table" : options.DefinedName is not null ? "DefinedName" : options.Range is null ? null : options.Range.IndexOf('!') >= 0 ? "SheetRange" : "Range";
		var header = options.HeaderRow is null ? null : $"header row {settings.Header}";
		try
		{
			await read;
		}
		catch (Exception e) when (IsExpected(e))
		{
			Note(events, area, header, "threw " + e.GetType().Name);

			throw;
		}

		Note(events, area, header, "read");
	}

	/// <summary>Notes how a read of an area, or with a header row, ended.</summary>
	/// <param name="events">Takes the notes.</param>
	/// <param name="area">The area, or <see langword="null"/> for a whole sheet.</param>
	/// <param name="header">The header row, or <see langword="null"/> for none given.</param>
	/// <param name="result">How the read ended.</param>
	private static void Note(List<string> events, string? area, string? header, string result)
	{
		if (area is not null)
		{
			events.Add($"{area}: {result}");
		}

		if (header is not null)
		{
			events.Add($"{header}: {result}");
		}
	}

	/// <summary>Adds one to a key's count.</summary>
	/// <param name="counts">The counts.</param>
	/// <param name="key">The key.</param>
	private static void Count(SortedDictionary<string, int> counts, string key) => counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;

	/// <summary>
	/// The options that read one of the workbook's own tables, defined names or blocks of a sheet, as the seed's area says:
	/// a table by its name, a defined name that is a block of cells by the text that finds it, or a random block of a
	/// worksheet with the sheet's name, quoted as Excel quotes it.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="settings">How the seed reads.</param>
	/// <param name="choices">The seed's numbers for what it reads.</param>
	/// <returns>The options and the area's first row; <see langword="null"/> when the seed reads none, or the workbook has none.</returns>
	private static (ExcelReaderOptions Options, int FirstRow)? Target(ExcelWorkbook workbook, Settings settings, Random choices)
	{
		switch (settings.Area)
		{
			case AreaMode.Table when workbook.Tables.Count > 0:
				var table = workbook.Tables[choices.Next(workbook.Tables.Count)];

				return (settings.Options with { Table = table.Name }, table.Range.FirstRow + table.HeaderRowCount);
			case AreaMode.DefinedName:
				var names = workbook.DefinedNames.Where(n => n.Range is not null && IsFoundByItsText(workbook, n)).ToList();
				if (names.Count == 0)
				{
					return null;
				}

				var name = names[choices.Next(names.Count)];

				return (settings.Options with { DefinedName = name.ToString() }, name.Range!.Value.FirstRow);
			case AreaMode.SheetRange:
				var sheets = workbook.Sheets.Where(s => s.Kind == ExcelSheetKind.Worksheet).ToList();
				if (sheets.Count == 0)
				{
					return null;
				}

				var sheet = sheets[choices.Next(sheets.Count)];
				var block = RandomBlock(choices);

				return (settings.Options with { Range = SheetLocation.Quote(sheet.Name) + "!" + block }, block.FirstRow);
			default:
				return null;
		}
	}

	/// <summary>
	/// Whether a defined name is the one its text (<see cref="ExcelDefinedName.ToString"/>) finds: no name before it has its
	/// name in its scope, as Excel's own names never do, but a workbook can hold a built-in name and a name of one's own
	/// alike once the built-in one's <c>_xlnm.</c> is gone, or two sheets whose names differ only in case.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="name">The name.</param>
	/// <returns>Whether its text finds it.</returns>
	private static bool IsFoundByItsText(ExcelWorkbook workbook, ExcelDefinedName name) =>
		ReferenceEquals(name, workbook.DefinedNames.First(n => string.Equals(n.Name, name.Name, StringComparison.OrdinalIgnoreCase) && (n.Scope is null
			? name.Scope is null
			: name.Scope is not null && string.Equals(n.Scope.Name, name.Scope.Name, StringComparison.OrdinalIgnoreCase))));

	/// <summary>A random block near a sheet's start: a few cells, whole columns or whole rows.</summary>
	/// <param name="choices">The seed's numbers for what it reads.</param>
	/// <returns>The block.</returns>
	private static ExcelRange RandomBlock(Random choices)
	{
		var firstRow = choices.Next(3) == 0 ? 1 : choices.Next(1, 30);
		var lastRow = firstRow + choices.Next(40);
		var firstColumn = choices.Next(3) == 0 ? 1 : choices.Next(1, 8);
		var lastColumn = firstColumn + choices.Next(8);

		return choices.Next(6) switch
		{
			0 => new ExcelRange(1, firstColumn, CellReference.MaxRow, lastColumn),
			1 => new ExcelRange(firstRow, 1, lastRow, CellReference.MaxColumns),
			2 => new ExcelRange(firstRow, firstColumn, firstRow, firstColumn),
			_ => new ExcelRange(firstRow, firstColumn, lastRow, lastColumn),
		};
	}

	/// <summary>
	/// The options with the header row the seed gives, when it reads a header: a row of the area with a value, given or
	/// searched for by its texts within as many rows as reach it, or a few more. The rows are the area's first 32 with a
	/// value, read as the reader would read them without a header; when there are none, or they can't be read, there is no
	/// header row to give.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet, or <see langword="null"/> when the options choose it.</param>
	/// <param name="options">The options.</param>
	/// <param name="firstRow">The area's first row.</param>
	/// <param name="settings">How the seed reads.</param>
	/// <param name="choices">The seed's numbers for what it reads.</param>
	/// <returns>A task whose result is the options, with a header row or not.</returns>
	private static async Task<ExcelReaderOptions> WithHeaderAsync(ExcelWorkbook workbook, ExcelSheet? sheet, ExcelReaderOptions options, int firstRow, Settings settings, Random choices)
	{
		if (settings.Header == HeaderMode.None)
		{
			return options;
		}

		var async = settings.Async;
		var rows = new List<(long Line, List<string> Texts)>();
		try
		{
			var plain = options with { Csv = options.Csv with { HasHeader = false, SkipEmptyLines = true, TrackLineNumbers = true } };
			using var reader = sheet is null ? await workbook.OpenReaderAsync(async, plain) : await workbook.OpenReaderAsync(async, sheet, plain);
			while (rows.Count < 32 && (async ? await reader.ReadAsync() : reader.Read()))
			{
				var texts = new List<string>();
				for (var i = 0; i < reader.FieldCount; i++)
				{
					try
					{
						if (reader.GetString(i) is { Length: > 0 } text)
						{
							texts.Add(text);
						}
					}
					catch (FormatException e) when (options.ErrorCells == ExcelErrorCells.Error && e is not CsvFormatException)
					{
						// An error cell, which names its column with the error's text; the other texts find the row.
					}
				}

				rows.Add((reader.LineNumber, texts));
			}
		}
		catch (Exception e) when (IsExpected(e))
		{
			// The read with a header fails as this one did, or later.
			return options;
		}

		if (rows.Count == 0)
		{
			return options;
		}

		var (line, names) = rows[choices.Next(rows.Count)];
		var headerRow = settings.Header == HeaderMode.At
			? ExcelHeaderRow.At((int)line)
			: ExcelHeaderRow.Find(names, (int)Math.Min(line - firstRow + 1 + choices.Next(3), CellReference.MaxRow));

		return options with { HeaderRow = headerRow };
	}

	/// <summary>Reads an area: its fields, or its records, as the seed says.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet, or <see langword="null"/> when the options choose it.</param>
	/// <param name="options">The options.</param>
	/// <param name="settings">How the seed reads.</param>
	/// <returns>A task that completes when the area is read.</returns>
	private static Task ReadAreaAsync(ExcelWorkbook workbook, ExcelSheet? sheet, ExcelReaderOptions options, Settings settings) =>
		settings.Mode == ReadMode.Records ? ReadRecordsAsync(workbook, sheet, options, settings) : ReadFieldsAsync(workbook, sheet, options, settings);

	/// <summary>Opens a reader of a sheet, or of what the options choose.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet, or <see langword="null"/> when the options choose it.</param>
	/// <param name="options">The options.</param>
	/// <param name="async">Whether to open it asynchronously.</param>
	/// <returns>A task whose result is the reader.</returns>
	private static async Task<CsvReader> OpenAsync(ExcelWorkbook workbook, ExcelSheet? sheet, ExcelReaderOptions options, bool async) =>
		sheet is null ? await workbook.OpenReaderAsync(async, options) : await workbook.OpenReaderAsync(async, sheet, options);

	/// <summary>
	/// Reads an area's fields as strings, and in <see cref="ReadMode.Fields"/> the fields of its first records as typed
	/// values too.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet, or <see langword="null"/> when the options choose it.</param>
	/// <param name="options">The options.</param>
	/// <param name="settings">How to read it.</param>
	/// <returns>A task that completes when the area is read.</returns>
	private static async Task ReadFieldsAsync(ExcelWorkbook workbook, ExcelSheet? sheet, ExcelReaderOptions options, Settings settings)
	{
		var async = settings.Async;
		using var reader = await OpenAsync(workbook, sheet, options, async);
		var records = 0;
		while (async ? await reader.ReadAsync() : reader.Read())
		{
			for (var i = 0; i < reader.FieldCount; i++)
			{
				ReadString(reader, i, settings.Options.ErrorCells);
				if (settings.Mode == ReadMode.Fields && records < ParsedRecords)
				{
					ParseField(reader, i, (records + i) % ParsedTypes);
				}
			}

			records++;
		}
	}

	/// <summary>
	/// Reads an area's records as <see cref="FuzzRecord"/>s. A record that can't be read goes to <c>OnRecordError</c>,
	/// which skips it; why it can't must be a field that doesn't parse.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet, or <see langword="null"/> when the options choose it.</param>
	/// <param name="area">The options.</param>
	/// <param name="settings">How to read it.</param>
	/// <returns>A task that completes when the area is read.</returns>
	/// <exception cref="InvalidOperationException">A record failed for another reason.</exception>
	private static async Task ReadRecordsAsync(ExcelWorkbook workbook, ExcelSheet? sheet, ExcelReaderOptions area, Settings settings)
	{
		var errors = new List<CsvRecordError>();
		var options = area with
		{
			Csv = area.Csv with
			{
				OnRecordError = error =>
				{
					errors.Add(error);

					return true;
				},
			},
		};
		using var reader = await OpenAsync(workbook, sheet, options, settings.Async);
		if (settings.Async)
		{
			await foreach (var record in reader.GetRecordsAsync<FuzzRecord>())
			{
				GC.KeepAlive(record);
			}
		}
		else
		{
			foreach (var record in reader.GetRecords<FuzzRecord>())
			{
				GC.KeepAlive(record);
			}
		}

		foreach (var error in errors)
		{
			if (error.Exception is not CsvFormatException { InnerException: FormatException or OverflowException })
			{
				throw new InvalidOperationException($"Record {error.RowNumber} ({error.Location}) failed with {error.Exception.InnerException?.GetType().FullName ?? "nothing inside"}: {error.Exception}");
			}
		}
	}

	/// <summary>Reads a field as a string: an error cell may fail, when error cells fail every read.</summary>
	/// <param name="reader">The reader, at a record.</param>
	/// <param name="index">The field.</param>
	/// <param name="errorCells">How error cells read.</param>
	private static void ReadString(CsvReader reader, int index, ExcelErrorCells errorCells)
	{
		try
		{
			reader.GetString(index);
		}
		catch (FormatException e) when (errorCells == ExcelErrorCells.Error && e is not CsvFormatException && e.Message.Contains(" holds the error "))
		{
			// The policy's failure, which names the cell.
		}
	}

	/// <summary>Parses a field as each type a cell can convert to: <c>TryParse</c> for each, and <c>Parse</c> for one.</summary>
	/// <param name="reader">The reader, at a record.</param>
	/// <param name="index">The field.</param>
	/// <param name="parse">Which type <c>Parse</c> is called for, from 0 to <see cref="ParsedTypes"/> less one.</param>
	private static void ParseField(CsvReader reader, int index, int parse)
	{
		Parse<double>(reader, index, parse == 0);
		Parse<decimal>(reader, index, parse == 1);
		Parse<long>(reader, index, parse == 2);
		Parse<int>(reader, index, parse == 3);
		Parse<byte>(reader, index, parse == 4);
		Parse<bool>(reader, index, parse == 5);
		Parse<DateTime>(reader, index, parse == 6);
		Parse<DateTimeOffset>(reader, index, parse == 7);
		Parse<TimeSpan>(reader, index, parse == 8);
#if NET
		Parse<DateOnly>(reader, index, parse == 9);
		Parse<TimeOnly>(reader, index, parse == 10);
#endif
	}

	/// <summary>
	/// Parses a field with <c>TryParse</c>, which must not throw, and when asked with <c>Parse</c> too, which must give the
	/// same value, or throw <see cref="FormatException"/> or <see cref="OverflowException"/> where <c>TryParse</c> failed.
	/// </summary>
	/// <typeparam name="T">The type.</typeparam>
	/// <param name="reader">The reader, at a record.</param>
	/// <param name="index">The field.</param>
	/// <param name="parse">Whether to call <c>Parse</c> too.</param>
	/// <exception cref="InvalidOperationException">The two disagree.</exception>
	private static void Parse<T>(CsvReader reader, int index, bool parse)
#if NET
		where T : ISpanParsable<T>
#endif
	{
		var parsed = reader.TryParse<T>(index, out var value);
		if (!parse)
		{
			return;
		}

		T again;
		try
		{
			again = reader.Parse<T>(index);
		}
		catch (Exception e) when (e is FormatException or OverflowException)
		{
			if (parsed)
			{
				throw new InvalidOperationException($"Field {index} ({Shown(reader, index)}): TryParse<{typeof(T).Name}> read {value}, but Parse threw {e.GetType().Name}: {e.Message}");
			}

			return;
		}

		if (!parsed || !EqualityComparer<T>.Default.Equals(value, again))
		{
			throw new InvalidOperationException($"Field {index} ({Shown(reader, index)}): Parse<{typeof(T).Name}> read {again}, but TryParse {(parsed ? $"read {value}" : "failed")}.");
		}
	}

	/// <summary>A field's stored form, for a failure.</summary>
	/// <param name="reader">The reader, at a record.</param>
	/// <param name="index">The field.</param>
	/// <returns>The field's bytes as text, cut short.</returns>
	private static string Shown(CsvReader reader, int index) => CellValue.Show(Encoding.UTF8.GetString(reader.GetSpan(index).ToArray()));

	/// <summary>Whether an exception is one a workbook that can't be read may end in.</summary>
	/// <param name="e">The exception.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsExpected(Exception e) => e is InvalidDataException or CsvFormatException or NotSupportedException;

	/// <summary>
	/// Makes the input of a seed: a source workbook, mutated in one XML part or in its zip bytes. A seed that reads a table
	/// or a defined name starts, one time in two, from a workbook that has one, since most have none.
	/// </summary>
	/// <param name="seed">The seed.</param>
	/// <param name="area">What the seed reads besides every worksheet whole.</param>
	/// <returns>The input.</returns>
	private static Input Mutate(int seed, AreaMode area)
	{
		var random = new Random(seed);
		var sources = Sources.Value;
		var having = area switch
		{
			AreaMode.Table => sources.Where(s => s.HasTables).ToList(),
			AreaMode.DefinedName => sources.Where(s => s.HasDefinedNames).ToList(),
			_ => sources,
		};
		var pool = having.Count > 0 && having.Count < sources.Count && random.Next(2) == 0 ? having : sources;
		var source = pool[random.Next(pool.Count)];
		var steps = new List<string>();
		byte[] zip;
		if (random.Next(3) < 2)
		{
			// Mostly the parts the scanner reads (sheets, then shared strings), then the workbook part with its defined names
			// and the table parts, then any of the package's parts.
			var parts = source.Entries.Select((e, i) => (e.Name, Index: i)).Where(e => e.Name.EndsWith(".xml", StringComparison.Ordinal) || e.Name.EndsWith(".rels", StringComparison.Ordinal)).ToList();
			var groups = new[]
			{
				parts.Where(p => p.Name.Contains("worksheets/")).ToList(),
				parts.Where(p => p.Name.Contains("sharedStrings")).ToList(),
				parts.Where(p => p.Name.EndsWith("/workbook.xml", StringComparison.Ordinal)).ToList(),
				parts.Where(p => p.Name.Contains("tables/")).ToList(),
			};
			var group = random.Next(8) switch
			{
				0 or 1 or 2 or 3 => groups[0],
				4 => groups[1],
				5 => groups[2],
				6 => groups[3],
				_ => parts,
			};
			var (name, index) = group.Count > 0 ? group[random.Next(group.Count)] : parts[random.Next(parts.Count)];
			var data = source.Entries[index].Data;
			for (var n = random.Next(1, 5); n > 0; n--)
			{
				data = MutateXml(random, name, data, steps, source);
			}

			var entries = source.Entries.ToList();
			entries[index] = (name, data);
			var level = random.Next(4) == 0 ? CompressionLevel.NoCompression : CompressionLevel.Fastest;
			zip = Zip(entries, level);
			steps.Insert(0, $"{name} ({level})");
		}
		else
		{
			zip = source.Zip.ToArray();
			for (var n = random.Next(1, 4); n > 0; n--)
			{
				zip = MutateZip(random, zip, steps);
			}

			steps.Insert(0, "zip bytes");
		}

		return new Input(source.Name, string.Join(", ", steps), zip);
	}

	/// <summary>Applies one mutation to an XML part's bytes.</summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <param name="name">The part's name, for the structures a worksheet, the workbook part or a table part has.</param>
	/// <param name="data">The part.</param>
	/// <param name="steps">Takes a description of the mutation.</param>
	/// <param name="source">The workbook, for its sheets' names.</param>
	/// <returns>The mutated part.</returns>
	private static byte[] MutateXml(Random random, string name, byte[] data, List<string> steps, Source source)
	{
		var at = random.Next(data.Length + 1);
		switch (random.Next(8))
		{
			case 6 when Structure(random, name, data, source) is var (place, replaced, markup) && place >= 0:
				steps.Add($"structure {Show(markup.Length > 120 ? markup.Substring(0, 120) + "..." : markup)} at {place}{(replaced > 0 ? $" for {replaced}" : "")}");

				return Replace(data, place, replaced, Encoding.UTF8.GetBytes(markup));
			case 0 when data.Length > 0:
				at = Math.Min(at, data.Length - 1);
				var bit = random.Next(8);
				steps.Add($"flip bit {bit} at {at}");

				return Replace(data, at, 1, [(byte)(data[at] ^ (1 << bit))]);
			case 1 when data.Length > 0:
				var length = Math.Min(random.Next(1, 33), data.Length - Math.Min(at, data.Length - 1));
				at = Math.Min(at, data.Length - length);
				steps.Add($"delete {length} at {at}");

				return Replace(data, at, length, []);
			case 2:
				var token = Tokens[random.Next(Tokens.Length)];
				steps.Add($"insert {Show(token)} at {at}");

				return Replace(data, at, 0, Encoding.UTF8.GetBytes(token));
			case 3 when data.Length > 0:
				var from = random.Next(data.Length);
				var copy = Math.Min(random.Next(1, 400), data.Length - from);
				steps.Add($"copy {copy} from {from} to {at}");

				return Replace(data, at, 0, data.AsSpan(from, copy).ToArray());
			case 4:
				steps.Add($"truncate at {at}");

				return data.AsSpan(0, at).ToArray();
			case 5:
				var digits = Enumerable.Range(0, data.Length).Where(i => data[i] >= '0' && data[i] <= '9' && (i == 0 || data[i - 1] < '0' || data[i - 1] > '9')).ToList();
				if (digits.Count == 0)
				{
					goto default;
				}

				var start = digits[random.Next(digits.Count)];
				var end = start;
				while (end < data.Length && data[end] >= '0' && data[end] <= '9')
				{
					end++;
				}

				var number = Numbers[random.Next(Numbers.Length)];
				steps.Add($"number {Show(number)} at {start}");

				return Replace(data, start, end - start, Encoding.UTF8.GetBytes(number));
			default:
				var value = (byte)random.Next(256);
				at = Math.Min(at, Math.Max(data.Length - 1, 0));
				steps.Add($"byte {value} at {at}");

				return data.Length == 0 ? [value] : Replace(data, at, 1, [value]);
		}
	}

	/// <summary>
	/// Markup of a structure that Phase 3 reads, and where in the part the schema puts it: in a worksheet, <c>cols</c>
	/// before <c>sheetData</c>, <c>mergeCells</c> after it, or an attribute that hides on a row or on <c>sheetFormatPr</c>;
	/// in the workbook part, <c>definedNames</c> after <c>sheets</c>, naming its sheets; in a table part, a count or a
	/// <c>ref</c> for the table's, or a column. Their blocks are near a sheet's start or at the grid's edges, in the forms
	/// Excel takes and some it doesn't.
	/// </summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <param name="name">The part's name.</param>
	/// <param name="data">The part.</param>
	/// <param name="source">The workbook, for its sheets' names.</param>
	/// <returns>Where the markup goes and how many bytes it replaces there; a start of -1 when the part has no place for one.</returns>
	private static (int Start, int Replaced, string Markup) Structure(Random random, string name, byte[] data, Source source)
	{
		if (name.Contains("worksheets/"))
		{
			return random.Next(4) switch
			{
				0 => (After(data, "</sheetData>"u8), 0, MergeCells(random)),
				1 => (data.AsSpan().IndexOf("<sheetData"u8), 0, Cols(random)),
				2 => (AfterAny(random, data, "<row"u8), 0, random.Next(3) switch { 0 => " hidden=\"1\"", 1 => " hidden=\"true\"", _ => " ht=\"0\" customHeight=\"1\"" }),
				_ => (After(data, "<sheetFormatPr"u8), 0, random.Next(3) switch { 0 => " zeroHeight=\"1\"", 1 => " defaultColWidth=\"0\"", _ => " customHeight=\"1\"" }),
			};
		}

		if (name.EndsWith("/workbook.xml", StringComparison.Ordinal))
		{
			return (After(data, "</sheets>"u8), 0, DefinedNames(random, source.SheetNames));
		}

		if (name.Contains("tables/"))
		{
			var table = data.AsSpan().IndexOf("<table"u8);
			switch (random.Next(3))
			{
				case 0 when table >= 0:
					return (table + "<table".Length, 0, $" {(random.Next(2) == 0 ? "headerRowCount" : "totalsRowCount")}=\"{random.Next(3)}\"");
				case 1 when table >= 0 && data.AsSpan(table).IndexOf(" ref=\""u8) is var reference && reference >= 0:
					var start = table + reference + " ref=\"".Length;
					var end = data.AsSpan(start).IndexOf((byte)'"');

					return (start, end < 0 ? 0 : end, RandomRef(random));
				default:
					var columns = data.AsSpan().IndexOf("<tableColumns"u8);
					var close = columns < 0 ? -1 : data.AsSpan(columns).IndexOf((byte)'>');

					return (close < 0 ? -1 : columns + close + 1, 0, $"<tableColumn id=\"{random.Next(1, 99)}\" name=\"{ColumnNames[random.Next(ColumnNames.Length)]}\"/>");
			}
		}

		return (-1, 0, "");
	}

	/// <summary>The offset after the first occurrence of some bytes.</summary>
	/// <param name="data">The bytes searched.</param>
	/// <param name="value">The bytes to find.</param>
	/// <returns>The offset; -1 when they don't occur.</returns>
	private static int After(byte[] data, ReadOnlySpan<byte> value)
	{
		var found = data.AsSpan().IndexOf(value);

		return found < 0 ? -1 : found + value.Length;
	}

	/// <summary>The offset after a random occurrence of some bytes.</summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <param name="data">The bytes searched.</param>
	/// <param name="value">The bytes to find.</param>
	/// <returns>The offset; -1 when they don't occur.</returns>
	private static int AfterAny(Random random, byte[] data, ReadOnlySpan<byte> value)
	{
		var offsets = new List<int>();
		for (var at = data.AsSpan().IndexOf(value); at >= 0;)
		{
			offsets.Add(at + value.Length);
			var next = data.AsSpan(at + value.Length).IndexOf(value);
			at = next < 0 ? -1 : at + value.Length + next;
		}

		return offsets.Count == 0 ? -1 : offsets[random.Next(offsets.Count)];
	}

	/// <summary>A <c>mergeCells</c> of one to four ranges, mostly in rows apart so that they don't overlap, now and then any block.</summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <returns>The markup.</returns>
	private static string MergeCells(Random random)
	{
		var count = random.Next(1, 5);
		var ranges = Enumerable.Range(0, count).Select(i =>
		{
			if (random.Next(6) == 0)
			{
				return RandomRef(random);
			}

			var firstRow = 1 + (i * 6) + random.Next(3);
			var firstColumn = random.Next(1, 7);

			return new ExcelRange(firstRow, firstColumn, firstRow + random.Next(3), firstColumn + random.Next(3)).ToString();
		});

		return $"<mergeCells count=\"{count}\">" + string.Concat(ranges.Select(r => $"<mergeCell ref=\"{r}\"/>")) + "</mergeCells>";
	}

	/// <summary>A <c>cols</c> of one to three columns' blocks, each hidden, of no width, or shown.</summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <returns>The markup.</returns>
	private static string Cols(Random random)
	{
		var cols = Enumerable.Range(0, random.Next(1, 4)).Select(_ =>
		{
			var min = random.Next(1, 11);
			var max = random.Next(5) == 0 ? CellReference.MaxColumns : min + random.Next(6);
			var look = random.Next(4) switch { 0 => " hidden=\"1\"", 1 => " width=\"0\" customWidth=\"1\"", 2 => "", _ => " width=\"9\"" };

			return $"<col min=\"{min}\" max=\"{max}\"{look}/>";
		});

		return "<cols>" + string.Concat(cols) + "</cols>";
	}

	/// <summary>
	/// A <c>definedNames</c> of one to four names of the workbook's sheets' blocks, the workbook's own or a sheet's (now and
	/// then of no sheet there is), hidden or not, and now and then a name that isn't a block.
	/// </summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <param name="sheets">The workbook's sheets' names.</param>
	/// <returns>The markup.</returns>
	private static string DefinedNames(Random random, IReadOnlyList<string> sheets)
	{
		var names = Enumerable.Range(0, random.Next(1, 5)).Select(i =>
		{
			var sheet = sheets.Count == 0 ? "Data" : sheets[random.Next(sheets.Count)];
			var scope = random.Next(3) == 0 ? $" localSheetId=\"{random.Next(sheets.Count + 1)}\"" : "";
			var hidden = random.Next(4) == 0 ? " hidden=\"1\"" : "";
			var refersTo = random.Next(5) switch
			{
				0 => random.Next(3) switch { 0 => "#REF!$A$1", 1 => "OFFSET(A1,0,0,2,2)", _ => SheetLocation.Quote(sheet) + "!$A$1," + SheetLocation.Quote(sheet) + "!$C$3" },
				_ => SheetLocation.Quote(sheet) + "!" + RandomRef(random),
			};

			return $"<definedName name=\"{(random.Next(4) == 0 ? "_xlnm.Print_Area" : $"Fz{i}")}\"{scope}{hidden}>{Sheets.Escape(refersTo)}</definedName>";
		});

		return "<definedNames>" + string.Concat(names) + "</definedNames>";
	}

	/// <summary>
	/// A block's text: a few cells near the start, whole columns or rows, the whole grid, or one cell, with <c>$</c>, in
	/// lowercase or with its corners reversed now and then, and now and then one Excel can't open.
	/// </summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <returns>The text.</returns>
	private static string RandomRef(Random random)
	{
		var firstRow = random.Next(1, 30);
		var firstColumn = random.Next(1, 8);
		var range = random.Next(8) switch
		{
			0 => new ExcelRange(1, firstColumn, CellReference.MaxRow, firstColumn + random.Next(3)),
			1 => new ExcelRange(firstRow, 1, firstRow + random.Next(3), CellReference.MaxColumns),
			2 => new ExcelRange(1, 1, CellReference.MaxRow, CellReference.MaxColumns),
			3 => new ExcelRange(firstRow, firstColumn, firstRow, firstColumn),
			_ => new ExcelRange(firstRow, firstColumn, firstRow + random.Next(5), firstColumn + random.Next(5)),
		};
		var text = range.ToString();

		return random.Next(8) switch
		{
			0 => text.ToLowerInvariant(),
			1 => string.Join(":", Enumerable.Reverse(text.Split(':'))),
			2 => string.Join(":", text.Split(':').Select(Absolute)),
			3 => random.Next(3) switch { 0 => "A0:B2", 1 => "A1:XFE2", _ => "A1:B2 C3:D4" },
			_ => text,
		};
	}

	/// <summary>A corner of a block, or a column or row of one, with <c>$</c> before its column and its row.</summary>
	/// <param name="corner">The corner, such as <c>B5</c>, <c>B</c> or <c>5</c>.</param>
	/// <returns>The corner, such as <c>$B$5</c>.</returns>
	private static string Absolute(string corner)
	{
		var letters = string.Concat(corner.TakeWhile(char.IsLetter));
		var digits = corner.Substring(letters.Length);

		return (letters.Length > 0 ? "$" + letters : "") + (digits.Length > 0 ? "$" + digits : "");
	}

	/// <summary>Applies one mutation to a zip's bytes: in a header, anywhere, or at its end.</summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <param name="zip">The zip.</param>
	/// <param name="steps">Takes a description of the mutation.</param>
	/// <returns>The mutated zip.</returns>
	private static byte[] MutateZip(Random random, byte[] zip, List<string> steps)
	{
		if (zip.Length == 0)
		{
			// A truncation left nothing: the next mutation is a byte.
			var only = (byte)random.Next(256);
			steps.Add($"byte {only}");

			return [only];
		}

		switch (random.Next(4))
		{
			case 0:
			case 1:
				// A byte of a local header (30 bytes), a central directory record (46) or the end record (22).
				var headers = Enumerable.Range(0, Math.Max(zip.Length - 3, 0))
					.Where(i => zip[i] == 'P' && zip[i + 1] == 'K' && (zip[i + 2], zip[i + 3]) is (3, 4) or (1, 2) or (5, 6))
					.ToList();
				if (headers.Count == 0)
				{
					goto default;
				}

				var header = headers[random.Next(headers.Count)];
				var size = zip[header + 2] == 3 ? 30 : zip[header + 2] == 1 ? 46 : 22;
				var at = Math.Min(header + 4 + random.Next(size - 4), zip.Length - 1);
				var value = random.Next(3) == 0 ? (byte)0xFF : (byte)random.Next(256);
				steps.Add($"header byte {value} at {at}");

				return Replace(zip, at, 1, [value]);
			case 2:
				var end = random.Next(zip.Length);
				steps.Add($"truncate at {end}");

				return zip.AsSpan(0, end).ToArray();
			default:
				var anywhere = random.Next(zip.Length);
				var any = (byte)random.Next(256);
				steps.Add($"byte {any} at {anywhere}");

				return Replace(zip, anywhere, 1, [any]);
		}
	}

	/// <summary>Replaces a range of bytes with others.</summary>
	/// <param name="data">The bytes.</param>
	/// <param name="at">The range's start.</param>
	/// <param name="length">The range's length.</param>
	/// <param name="with">The bytes to put there.</param>
	/// <returns>The new bytes.</returns>
	private static byte[] Replace(byte[] data, int at, int length, byte[] with)
	{
		var result = new byte[data.Length - length + with.Length];
		data.AsSpan(0, at).CopyTo(result);
		with.CopyTo(result.AsSpan(at));
		data.AsSpan(at + length).CopyTo(result.AsSpan(at + with.Length));

		return result;
	}

	/// <summary>Writes entries as a zip, in order.</summary>
	/// <param name="entries">The entries.</param>
	/// <param name="level">Their compression.</param>
	/// <returns>The zip's bytes.</returns>
	private static byte[] Zip(List<(string Name, byte[] Data)> entries, CompressionLevel level)
	{
		var stream = new MemoryStream();
		using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
		{
			foreach (var (name, data) in entries)
			{
				using var entry = zip.CreateEntry(name, level).Open();
				entry.Write(data, 0, data.Length);
			}
		}

		return stream.ToArray();
	}

	/// <summary>
	/// The workbooks the inputs start from: four synthetic packages (Excel's shape with every kind of cell, the orders
	/// workbook, numbers, and formulas with typed cells) and every corpus workbook CsvHelper.Excel reads.
	/// </summary>
	/// <returns>The sources.</returns>
	private static List<Source> LoadSources()
	{
		var variety = Workbooks.Data(
				"<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\" t=\"s\"><v>1</v></c><c r=\"D1\" t=\"inlineStr\"><is><r><t xml:space=\"preserve\"> rich </t></r><r><t>run</t></r></is></c></row>" +
				"<row><c t=\"b\"><v>1</v></c><c t=\"e\"><v>#N/A</v></c><c s=\"1\"><v>45293.5</v></c><c t=\"str\"><f>A1</f><v>_x0041_&amp;</v></c></row>" +
				"<row r=\"5\" spans=\"1:3\"><c r=\"C5\" t=\"d\"><v>2024-01-02T03:04:05</v></c><c r=\"D5\"><v> 1.5E-3 </v></c></row>")
			.SharedStrings("<t>Name</t>", "<r><rPr><b/></rPr><t>Bold</t></r><r><t xml:space=\"preserve\"> plain</t></r><rPh sb=\"0\" eb=\"1\"><t>no</t></rPh>");

		// Shared formulas (a group, a group of two columns, and one whose defining cell has no text), an array, a data table
		// and a formula with a structured reference, among dates, a t="d" date and duration, an error and 17 digits. Style 3
		// is an elapsed time.
		var formulas = Workbooks.Data(
				"<row r=\"1\"><c r=\"A1\"><f t=\"shared\" ref=\"A1:A3\" si=\"0\">B1*2+$C$1</f><v>2</v></c><c r=\"B1\" s=\"1\"><v>45293.5</v></c>" +
				"<c r=\"C1\" t=\"d\" s=\"1\"><v>2024-01-02T03:04:05.123</v></c><c r=\"D1\" t=\"d\" s=\"3\"><v>PT36H0M0.000S</v></c><c r=\"E1\" t=\"e\"><v>#DIV/0!</v></c></row>" +
				"<row r=\"2\"><c r=\"A2\"><f t=\"shared\" si=\"0\"/><v>4</v></c><c r=\"B2\"><f t=\"array\" ref=\"B2:B3\">C1:C2*2</f><v>1</v></c>" +
				"<c r=\"C2\" t=\"str\"><f>SUM(Orders[[#This Row],[Qty]])&amp;\"A1\"</f><v>x</v></c><c r=\"D2\"><f t=\"shared\" ref=\"D2:E3\" si=\"1\">'Other Sheet'!A1+D1</f><v>5</v></c>" +
				"<c r=\"E2\"><f t=\"shared\" si=\"1\"/><v>6</v></c></row>" +
				"<row r=\"3\"><c r=\"A3\"><f t=\"shared\" si=\"0\">ignored</f><v>6</v></c><c r=\"B3\"><v>1</v></c><c r=\"C3\"><f t=\"dataTable\" ref=\"C3:C4\" dt2D=\"0\" dtr=\"0\" r1=\"A1\"/><v>7</v></c>" +
				"<c r=\"D3\" t=\"b\"><f t=\"shared\" si=\"1\"/><v>1</v></c><c r=\"E3\" s=\"2\"><v>12345678901234567</v></c><c r=\"F3\"><f t=\"shared\" ref=\"F3:F4\" si=\"2\"/><v>8</v></c></row>" +
				"<row r=\"4\"><c r=\"F4\"><f t=\"shared\" si=\"2\"/><v>9</v></c><c r=\"G4\" s=\"3\"><v>1.5</v></c><c r=\"H4\" t=\"d\"><v>1900-02-28</v></c></row>")
			.Replace("xl/styles.xml", "<numFmt numFmtId=\"164\" formatCode=\"yyyy\\-mm\\-dd\"/>", "<numFmt numFmtId=\"164\" formatCode=\"yyyy\\-mm\\-dd\"/><numFmt numFmtId=\"165\" formatCode=\"[h]:mm:ss\"/>")
			.Replace("xl/styles.xml", "<xf numFmtId=\"164\" applyNumberFormat=\"1\"/>", "<xf numFmtId=\"164\" applyNumberFormat=\"1\"/><xf numFmtId=\"165\" applyNumberFormat=\"1\"/>");
		var packages = new[]
		{
			("synthetic/variety", variety), ("synthetic/orders", Workbooks.Orders()), ("synthetic/numbers", Workbooks.Data(Workbooks.Numbers(40))), ("synthetic/formulas", formulas),
		};
		var sources = packages.Select(p => Source.Of(p.Item1, p.Item2.ToArray())).ToList();
		sources.AddRange(CorpusFiles.Workbooks.Select(file => Source.Of(file, File.ReadAllBytes(CorpusFiles.PathOf(file)))));

		return sources;
	}

	/// <summary>The value of an integer environment variable.</summary>
	/// <param name="name">The variable.</param>
	/// <param name="fallback">The value when it isn't set.</param>
	/// <returns>The value.</returns>
	private static int Setting(string name, int fallback) =>
		int.TryParse(Environment.GetEnvironmentVariable(name), out var value) ? value : fallback;

	/// <summary>Text for a description: control characters as code points.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The text to show.</returns>
	private static string Show(string text) => "\"" + string.Concat(text.Select(c => c < ' ' || c == '\uFEFF' ? $"\\u{(int)c:X4}" : c.ToString())) + "\"";

	/// <summary>A workbook an input starts from: its zip, its entries' inflated bytes in order, and its sheets' names.</summary>
	/// <param name="name">Where it comes from.</param>
	/// <param name="zip">The zip's bytes.</param>
	/// <param name="entries">The entries.</param>
	private sealed class Source(string name, byte[] zip, List<(string Name, byte[] Data)> entries)
	{
		/// <summary>Where it comes from.</summary>
		public string Name { get; } = name;

		/// <summary>The zip's bytes.</summary>
		public byte[] Zip { get; } = zip;

		/// <summary>The entries, in the zip's order.</summary>
		public List<(string Name, byte[] Data)> Entries { get; } = entries;

		/// <summary>Whether it has a table part.</summary>
		public bool HasTables { get; } = entries.Any(e => e.Name.Contains("tables/"));

		/// <summary>Whether its workbook part has a defined name of a sheet's cells: one whose text has a <c>!</c>.</summary>
		public bool HasDefinedNames { get; } = entries.Any(e =>
			e.Name.EndsWith("/workbook.xml", StringComparison.Ordinal) && Regex.IsMatch(Encoding.UTF8.GetString(e.Data), @"<(?:\w+:)?definedName\b[^>]*>[^<]*![^<]*<"));

		/// <summary>The names of its sheets, from its workbook part's <c>sheet</c> elements, for the defined names the mutations write.</summary>
		public IReadOnlyList<string> SheetNames { get; } = entries
			.Where(e => e.Name.EndsWith("/workbook.xml", StringComparison.Ordinal))
			.SelectMany(e => Regex.Matches(Encoding.UTF8.GetString(e.Data), "<(?:\\w+:)?sheet\\b[^>]*?\\bname=\"([^\"]*)\"").Cast<Match>())
			.Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
			.ToList();

		/// <summary>Reads a workbook's entries.</summary>
		/// <param name="name">Where it comes from.</param>
		/// <param name="zip">The zip's bytes.</param>
		/// <returns>The source.</returns>
		public static Source Of(string name, byte[] zip)
		{
			using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
			var entries = archive.Entries.Select(e =>
			{
				using var stream = e.Open();
				var data = new MemoryStream();
				stream.CopyTo(data);

				return (e.FullName, data.ToArray());
			}).ToList();

			return new Source(name, zip, entries);
		}
	}

	/// <summary>An input: a mutated workbook.</summary>
	/// <param name="source">The workbook it starts from.</param>
	/// <param name="steps">The mutations.</param>
	/// <param name="zip">Its bytes.</param>
	private sealed class Input(string source, string steps, byte[] zip)
	{
		/// <summary>Its bytes.</summary>
		public byte[] Zip { get; } = zip;

		/// <summary>Describes the input for a failure.</summary>
		/// <param name="seed">Its seed.</param>
		/// <param name="settings">How it was read.</param>
		/// <returns>The description.</returns>
		public string Describe(int seed, Settings settings) => $"Seed {seed} ({settings}), {source}: {steps}";
	}

	/// <summary>
	/// How a seed reads its input: synchronously or not, with which CSV options (alternating with the seed, as before), and
	/// with which formulas, error cells, significant digits and reading (chosen by numbers of the seed's own); and, two
	/// seeds in three, which area, header row and switches, with a seed for the choices that depend on the workbook.
	/// </summary>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="options">The options, under small limits.</param>
	/// <param name="mode">How to read the fields.</param>
	/// <param name="area">What to read besides every worksheet whole.</param>
	/// <param name="header">How to give the header row.</param>
	/// <param name="choices">The seed of the choices made once the workbook is open.</param>
	private sealed class Settings(bool async, ExcelReaderOptions options, ReadMode mode, AreaMode area, HeaderMode header, int choices)
	{
		/// <summary>Whether to read asynchronously.</summary>
		public bool Async { get; } = async;

		/// <summary>The options.</summary>
		public ExcelReaderOptions Options { get; } = options;

		/// <summary>How to read the fields.</summary>
		public ReadMode Mode { get; } = mode;

		/// <summary>What to read besides every worksheet whole.</summary>
		public AreaMode Area { get; } = area;

		/// <summary>How to give the header row.</summary>
		public HeaderMode Header { get; } = header;

		/// <summary>The seed of the choices made once the workbook is open: the block, the table or name, the header row.</summary>
		public int Choices { get; } = choices;

		/// <summary>The settings of a seed.</summary>
		/// <param name="seed">The seed.</param>
		/// <returns>The settings.</returns>
		public static Settings Of(int seed)
		{
			var random = new Random(Mix(seed));
			var options = new ExcelReaderOptions
			{
				MaxPartSize = 1 << 20,
				MaxUncompressedSize = 16 << 20,
				Formulas = random.Next(2) == 0 ? ExcelFormulas.CachedValue : ExcelFormulas.FormulaText,
				ErrorCells = (ExcelErrorCells)random.Next(3),
				SignificantDigits = Digits[random.Next(Digits.Length)],
				Csv = new() { HasHeader = seed % 3 != 0, SkipEmptyLines = seed % 5 != 0, TrimWhitespace = seed % 7 == 0 },
			};
			var mode = (ReadMode)random.Next(3);

			// Drawn after the earlier settings, which stay as they were for a seed; one seed in three reads as before Phase 3.
			var area = AreaMode.Sheet;
			var header = HeaderMode.None;
			if (random.Next(3) > 0)
			{
				area = (AreaMode)random.Next(5);
				header = options.Csv.HasHeader ? (HeaderMode)random.Next(3) : HeaderMode.None;
				options = options with { SkipHiddenRows = random.Next(2) == 0, SkipHiddenColumns = random.Next(2) == 0, FillMergedCells = random.Next(2) == 0 };
			}

			return new Settings(seed % 2 == 1, options, mode, area, header, random.Next());
		}

		/// <summary>
		/// A seed for the settings' numbers, its bits mixed (MurmurHash3's finalizer), so that they don't follow the
		/// mutations': <see cref="Random"/> gives seeds near each other nearly the same first numbers, and takes
		/// <c>~seed</c> as <c>seed + 1</c>.
		/// </summary>
		/// <param name="seed">The input's seed.</param>
		/// <returns>The settings' seed.</returns>
		private static int Mix(int seed)
		{
			var x = unchecked((uint)seed * 0x9E3779B9u);
			x ^= x >> 16;
			x = unchecked(x * 0x85EBCA6Bu);
			x ^= x >> 13;
			x = unchecked(x * 0xC2B2AE35u);
			x ^= x >> 16;

			return (int)(x & int.MaxValue);
		}

		/// <inheritdoc/>
		public override string ToString() =>
			$"{(Async ? "async" : "sync")}, {Mode}, formulas {Options.Formulas}, error cells {Options.ErrorCells}, digits {Options.SignificantDigits?.ToString() ?? "null"}, " +
			$"header {Options.Csv.HasHeader}, skip empty {Options.Csv.SkipEmptyLines}, trim {Options.Csv.TrimWhitespace}, area {Area}, header row {Header}, " +
			$"hidden rows {(Options.SkipHiddenRows ? "skipped" : "read")}, hidden columns {(Options.SkipHiddenColumns ? "skipped" : "read")}, " +
			$"merged cells {(Options.FillMergedCells ? "filled" : "not filled")}";
	}
}
