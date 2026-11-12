using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using CsvHelper.Excel.Tests.Writing;
using Xunit.Abstractions;
using static CsvHelper.Excel.Tests.Security.WriterFuzz;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// Seeded fuzzing of the writer's presentation, as Phase 5's "Testing" describes: workbooks of random sheet options
/// (titles, a bold or frozen header, frozen columns, filters, tables with names and styles, auto-fit, columns' widths,
/// formats, wrap and hidden), with options Excel wouldn't open planted now and then; headers of random names under a
/// table (empty, repeated in other cases, numbered, longer than 255 characters); random data, URIs among it, under each
/// link policy; ranges merged before the header, between records and after them, some of which the writer refuses; and
/// <see cref="ExcelRowLimit.NewSheet"/> at a few rows. Each workbook passes the Open XML SDK's validator, and reads back
/// through CsvHelper.Excel's reader as the same data written in Phase 4's look does, but for a table's header. The test's
/// own rules, from the writer's documentation of Excel's, say which options and ranges are refused, what each table and
/// its columns are named, which cells are links (and that <see cref="ExcelLinks.Web"/> links nothing but <c>http</c>,
/// <c>https</c> and <c>mailto</c>), and what each sheet's panes, filter, merged ranges and hidden columns are.
/// <c>CSVHELPER_EXCEL_FUZZ_PRESENTATIONS</c> sets the number of workbooks, <c>CSVHELPER_EXCEL_FUZZ_SEED</c> the first
/// seed, and <c>CSVHELPER_EXCEL_FUZZ_SAVE</c> a folder to save each workbook in, for the corpus tool's <c>fuzzed</c>
/// command to open in Excel.
/// </summary>
/// <param name="output">Shows what the run wrote.</param>
public class PresentationFuzzTests(ITestOutputHelper output)
{
	/// <summary>The number of workbooks a run writes, unless <c>CSVHELPER_EXCEL_FUZZ_PRESENTATIONS</c> says otherwise.</summary>
	private const int DefaultWorkbooks = 1000;

	/// <summary>The most characters of a table's name and of a table's column's name.</summary>
	private const int MaxName = 255;

	/// <summary>The most characters Excel gives a column's name it makes with a number.</summary>
	private const int MaxNumbered = 253;

	/// <summary>The most characters of a link's target.</summary>
	private const int MaxTarget = 8192;

	/// <summary>The last row Excel has.</summary>
	private const int MaxRow = 1048576;

	/// <summary>The last column Excel has, <c>XFD</c>.</summary>
	private const int MaxColumn = 16384;

	/// <summary>
	/// Sheets' names: plain, with characters a table's name can't have, or that make a table's name a reference, a boolean
	/// or Excel's own, past ASCII, and a surrogate pair. No two are alike ignoring case.
	/// </summary>
	private static readonly string[] SheetNames =
	[
		"Orders", "Q1 Orders", "2024", "A1", "R2D2", "TRUE", "rc", "C", "Sales-2024", Units(0x65E5, 0x672C), Units(0x3A9), "Data.1", "_xlnm.a",
		"Report (draft)", "x" + Units(0xD83D, 0xDE00), Units(0xD83D, 0xDE00) + "y", "XFD1048576", "ABCD1", new string('L', 31),
	];

	/// <summary>
	/// The pieces of headers' names: empty, repeated in other cases, numbered as the writer numbers repeats, white space,
	/// line breaks, escapes, letters whose cases fold to others (ß, the Ohm and Kelvin signs), full-width forms, a lone
	/// surrogate, which is written as U+FFFD, and characters Excel ignores when it compares names (a byte order mark, a soft
	/// hyphen, a direction's mark).
	/// </summary>
	private static readonly string[] HeaderPieces =
	[
		"", "", "Name", "name", "NAME", "Name2", "Name3", "Column", "Column1", "Column3", "x", "X", "x2", " Name", "Name ", "a b", "Two\nLines",
		"tab\there", "_x0041_", "A", "&<>\"'", Units(0xDF), "SS", "ss", Units(0x1E9E), Units(0x3A9), Units(0x2126), Units(0x212A), "K",
		Units(0xFF21), Units(0x65E5, 0x672C), Units(0xD83D, 0xDE00), Units(0xD800), "Total", "2024", Units(0xFEFF), Units(0xAD), Units(0x200E),
	];

	/// <summary>The pieces of titles: words, white space, line breaks, XML's characters, escapes, controls and surrogates.</summary>
	private static readonly string[] TitlePieces =
	[
		"Sales", " ", "2024", "\n", "\r\n", "&", "<b>", "_x0041_", "=A1", Units(1), Units(0xE9), Units(0x65E5, 0x672C), Units(0xD83D, 0xDE00),
		Units(0xDC00), Units(0xFFFE),
	];

	/// <summary>The pieces of text fields.</summary>
	private static readonly string[] TextPieces =
	[
		"a", "Zz", " ", "\n", "\r", "&", "<", "_x0041_", "=A1", "1.5", "007", "TRUE", "http://example.com/", Units(0xE9), Units(0x65E5),
		Units(0xD83D, 0xDE00), Units(0xD800),
	];

	/// <summary>
	/// URIs: each policy's schemes and others, in other cases, with spaces, fragments, user information, ports and queries,
	/// letters past ASCII as they are and escaped, a direction's mark, files and shares, relative paths and a jump within
	/// the workbook, and text around them. Long ones come from <see cref="LongUri"/>.
	/// </summary>
	private static readonly string[] UriTexts =
	[
		"https://example.com/", "http://example.com/a b", "HTTPS://EXAMPLE.COM/Path", "https://example.com/#frag", "https://example.com/?q=1&r=<2>",
		"https://user:pass@example.com:8080/p?q=\"1\"", "mailto:a@example.com?subject=Hi there", "MAILTO:A@EXAMPLE.COM", "ftp://example.com/x",
		"file:///C:/Temp/report.txt", @"\\server\share\a b.txt", @"C:\Temp\r" + Units(0xE9) + "port.txt", "javascript:alert(1)", "data:text/plain,hi",
		"urn:isbn:0451450523", "news:comp.lang.c", "docs/report.txt", "../a b.txt", "#Other!B2", "?q=1", "https://example.com/" + Units(0xE9),
		"https://example.com/%C3%A9", "https://example.com/%E2%80%8E", "https://example.com/" + Units(0x200E), "https://" + Units(0x4F8B, 0x3048) + ".jp/",
		" https://example.com/padded ", "https://example.com/%", "https://example.com/" + Units(0xD83D, 0xDE00),
	];

	/// <summary>Format codes Excel opened, from <see cref="FormatCheckTests.Opened"/>, but the empty one, which means none.</summary>
	private static readonly string[] GoodCodes = [.. ((IEnumerable<object[]>)FormatCheckTests.Opened).Select(row => (string)row[0]).Where(code => code.Length > 0)];

	/// <summary>Format codes Excel refused, from <see cref="FormatCheckTests.Refused"/>, and the empty one.</summary>
	private static readonly string[] BadCodes = ["", .. ((IEnumerable<object[]>)FormatCheckTests.Refused).Select(row => (string)row[0])];

	/// <summary>Table names that break Excel's rules.</summary>
	private static readonly string[] BadTableNames =
	[
		"", "A1", "a1", "XFD1048576", "R1", "C1", "RC", "R1C1", "R2D2", "r", "TRUE", "false", "1abc", ".a", "a b", "a-b", "_xlnm.a", "_XLNM.b",
		new string('t', 256), "a" + Units(0xD800), "a" + Units(0xA0),
	];

	/// <summary>Table names that keep Excel's rules.</summary>
	private static readonly string[] GoodTableNames =
	[
		"Orders", "_1", @"\Orders", "Q1.Orders", "a?b", Units(0x65E5, 0x672C), Units(0xFC), "a" + Units(0xD83D, 0xDE00), "SUM", "Print_Area",
		"Table1", "XFE1", "ABCD1", "A0", "XFD", "Ra", "CA", new string('t', 255),
	];

	/// <summary>Text that isn't a range of two cells or more: not a range, past Excel's edges, one cell.</summary>
	private static readonly string[] BadRanges = ["", "A1", "A1:", ":B2", "A0:B1", "XFE1:XFF2", "A1:B1048577", "A1-B2", "A1:B2:C3", "0:1", "$A$1"];

	[Fact]
	public async Task WritesRandomPresentationsAsTheirOptionsSay()
	{
		var workbooks = Setting("CSVHELPER_EXCEL_FUZZ_PRESENTATIONS", DefaultWorkbooks);
		var first = Setting("CSVHELPER_EXCEL_FUZZ_SEED", 1);
		var save = Environment.GetEnvironmentVariable("CSVHELPER_EXCEL_FUZZ_SAVE");
		var failures = new List<string>();
		var tally = new SortedDictionary<string, int>(StringComparer.Ordinal);
		for (var seed = first; seed < first + workbooks && failures.Count < MaxFailures; seed++)
		{
			var input = Input.Of(seed);
			try
			{
				if (await CheckAsync(input, tally, save) is { } problem)
				{
					failures.Add($"Seed {seed} ({input}): {problem}");
				}
			}
			catch (Exception e)
			{
				failures.Add($"Seed {seed} ({input}): {Describe(e)}");
			}
		}

		output.WriteLine($"Seeds {first} to {first + workbooks - 1}:");
		foreach (var pair in tally)
		{
			output.WriteLine($"  {pair.Key}: {pair.Value}");
		}

		AssertNone(failures, "CSVHELPER_EXCEL_FUZZ_PRESENTATIONS");
	}

	/// <summary>
	/// Writes an input's workbook, and the same data in Phase 4's look, and checks them: the refusals, the validator, the
	/// data read back, and each sheet's look.
	/// </summary>
	/// <param name="input">The input.</param>
	/// <param name="tally">Takes what was written.</param>
	/// <param name="save">A folder to save the workbook in, or <see langword="null"/>.</param>
	/// <returns>A task whose result is what went wrong, or <see langword="null"/>.</returns>
	private static async Task<string?> CheckAsync(Input input, SortedDictionary<string, int> tally, string? save)
	{
		var model = new Model(input);
		var problems = new List<string>();
		var workbook = await WriteAsync(input, model, problems, tally, plain: false, refused: model.Refused);
		if (problems.Count > 0)
		{
			return string.Join("; ", problems);
		}

		if (save is not null)
		{
			Directory.CreateDirectory(save);
			File.WriteAllBytes(Path.Combine(save, $"presentation-{input.Seed.ToString(CultureInfo.InvariantCulture)}.xlsx"), workbook);
		}

		var errors = Written.ValidationErrors(workbook);
		if (errors.Count > 0)
		{
			return "the validator says: " + string.Join("; ", errors.Take(3));
		}

		var plain = await WriteAsync(input, new Model(input), problems, tally: null, plain: true, refused: model.Refused);
		if (problems.Count > 0)
		{
			return "in Phase 4's look: " + string.Join("; ", problems);
		}

		if (ReadBackProblem(model, Read(workbook), Read(plain)) is { } read)
		{
			return read;
		}

		var package = Package.Of(workbook);
		if (model.Sheets.Count == 0)
		{
			return package.Sheets.Count == 1 && package.Sheets[0].Name == "Sheet1" ? null : "a workbook of no sheets has " + string.Join(", ", package.Sheets.Select(s => s.Name));
		}

		if (package.Sheets.Count != model.Sheets.Count)
		{
			return $"{package.Sheets.Count} sheets were written, and the model has {model.Sheets.Count}: {string.Join(", ", package.Sheets.Select(s => s.Name))}";
		}

		for (var i = 0; i < model.Sheets.Count; i++)
		{
			if (LookProblem(model.Sheets[i], package.Sheets[i], package, model.Input) is { } look)
			{
				return $"sheet {Show(package.Sheets[i].Name)}: {look}";
			}
		}

		Count(tally, "workbooks");
		Count(tally, input.Links switch { ExcelLinks.None => "workbooks without links", ExcelLinks.All => "workbooks with every URI a link", _ => "workbooks with web links" });
		foreach (var sheet in model.Sheets)
		{
			Count(tally, "sheets");
			Count(tally, sheet.Index > 0 ? "sheets that NewSheet started" : "sheets added");
			if (sheet.TableName is not null)
			{
				Count(tally, "tables");
			}
		}

		tally["links"] = (tally.TryGetValue("links", out var links) ? links : 0) + package.Sheets.Sum(s => s.Links.Count);

		return null;
	}

	/// <summary>
	/// Writes an input's workbook, as it is or in Phase 4's look (no presentation, links or merged ranges, the title kept,
	/// so that the rows are the same), and adds to the model what the writer did: each sheet added or refused, each range
	/// merged or refused, and each header name refused.
	/// </summary>
	/// <param name="input">The input.</param>
	/// <param name="model">The model, which takes the sheets written.</param>
	/// <param name="problems">Takes what went otherwise than the model says.</param>
	/// <param name="tally">Takes what was written, or <see langword="null"/>.</param>
	/// <param name="plain">Whether to write in Phase 4's look.</param>
	/// <param name="refused">The sheets refused: the presentation's model adds them, and Phase 4's look leaves them out.</param>
	/// <returns>A task whose result is the workbook's bytes.</returns>
	private static async Task<byte[]> WriteAsync(Input input, Model model, List<string> problems, SortedDictionary<string, int>? tally, bool plain, HashSet<SheetInput> refused)
	{
		var options = new ExcelWriterOptions
		{
			Strings = input.Strings,
			Links = plain ? ExcelLinks.None : input.Links,
			RowLimit = input.MaxRows > 0 ? ExcelRowLimit.NewSheet : ExcelRowLimit.Throw,
			Csv = new CsvWriterOptions { HasHeader = input.HasHeader, ValidateFieldCount = false },
		};

		using var stream = new MemoryStream();
		var book = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true);
		if (input.MaxRows > 0)
		{
			book.MaxRows = input.MaxRows;
		}

		foreach (var sheet in input.Sheets)
		{
			var sheetOptions = plain ? new ExcelSheetOptions { Title = sheet.Options.Title, BoldHeader = false, FreezeHeader = false, AutoFit = 0 } : sheet.Options;
			if (plain && refused.Contains(sheet))
			{
				continue;
			}

			var refusal = plain ? null : model.Refusal(sheet);

			CsvWriter writer;
			try
			{
				writer = book.AddSheet(sheet.Name, sheetOptions);
			}
			catch (ArgumentException e) when (!plain)
			{
				refused.Add(sheet);
				if (refusal is null)
				{
					problems.Add($"the sheet {Show(sheet.Name)} was refused: {e.Message}");
				}
				else if (tally is not null)
				{
					Count(tally, "sheets refused: " + refusal);
				}

				continue;
			}

			if (refusal is not null)
			{
				problems.Add($"the sheet {Show(sheet.Name)} was added, which the model refuses: {refusal}");
				await Written.DisposeAsync(writer, input.Async);

				continue;
			}

			var added = model.Add(sheet, plain);
			var slot = 0;
			Merge(writer, added, slot, problems, tally, plain);
			var records = input.HasHeader ? [sheet.Header, .. sheet.Records] : sheet.Records;
			foreach (var record in records)
			{
				var isHeader = input.HasHeader && slot == 0;
				for (var c = 0; c < record.Count; c++)
				{
					var field = record[c];
					try
					{
						field.WriteTo(writer);
						if (isHeader && added.RefusesHeaderName(field))
						{
							problems.Add($"the header's field {c + 1}, {field}, was written, and a table's column has {MaxName} characters at most");
						}
					}
					catch (InvalidOperationException e) when (isHeader && added.RefusesHeaderName(field))
					{
						if (!e.Message.EndsWith($", which Excel opens with {MaxName} at most.", StringComparison.Ordinal))
						{
							problems.Add($"the header's field {c + 1}, {field}, threw \"{e.Message}\"");
						}
						else if (tally is not null)
						{
							Count(tally, "header names refused, longer than 255");
						}
					}
				}

				await Written.EndRecordAsync(writer, input.Async);
				slot++;
				Merge(writer, added, slot, problems, tally, plain);
			}

			await Written.DisposeAsync(writer, input.Async);
			model.Complete(added);
		}

		if (input.Async)
		{
			await book.DisposeAsync();
		}
		else
		{
			book.Dispose();
		}

		return stream.ToArray();
	}

	/// <summary>Merges the ranges of a slot (after as many records), and checks each against the model's rules.</summary>
	/// <param name="writer">The sheet's writer.</param>
	/// <param name="sheet">The model's sheet.</param>
	/// <param name="slot">The records written so far, the header's included.</param>
	/// <param name="problems">Takes what went otherwise than the model says.</param>
	/// <param name="tally">Takes what was merged, or <see langword="null"/>.</param>
	/// <param name="plain">Whether the workbook is in Phase 4's look, which merges nothing.</param>
	private static void Merge(CsvWriter writer, AddedSheet sheet, int slot, List<string> problems, SortedDictionary<string, int>? tally, bool plain)
	{
		if (plain)
		{
			return;
		}

		foreach (var merge in sheet.Source.Merges.Where(m => m.Slot == slot))
		{
			var refusal = sheet.MergeRefusal(merge, slot);
			try
			{
				writer.MergeCells(merge.Text);
				if (refusal is not null)
				{
					problems.Add($"{Show(merge.Text)} was merged after {slot} records, which the model refuses: {refusal}");

					return;
				}

				sheet.Merged.Add(merge.Range!.Value);
				if (tally is not null)
				{
					Count(tally, "ranges merged");
				}
			}
			catch (ArgumentException e)
			{
				if (refusal is null)
				{
					problems.Add($"{Show(merge.Text)} was refused after {slot} records: {e.Message}");

					return;
				}

				if (e.ParamName != "range")
				{
					problems.Add($"{Show(merge.Text)} was refused for {e.ParamName}: {e.Message}");

					return;
				}

				if (tally is not null)
				{
					Count(tally, "ranges refused: " + refusal);
				}
			}
		}
	}

	/// <summary>Reads every row of every sheet with CsvHelper.Excel, each row's fields as their text.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>Each sheet's name and rows.</returns>
	private static List<(string Name, List<List<string>> Rows)> Read(byte[] workbook)
	{
		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		var sheets = new List<(string, List<List<string>>)>();
		foreach (var sheet in book.Sheets)
		{
			using var reader = book.OpenReader(sheet, Written.AllRows);
			var rows = new List<List<string>>();
			while (reader.Read())
			{
				rows.Add([.. Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetString(i) ?? "")]);
			}

			sheets.Add((sheet.Name, rows));
		}

		return sheets;
	}

	/// <summary>
	/// Whether the workbook reads back as the same data in Phase 4's look does, sheet by sheet and cell by cell: but for a
	/// table's header, whose cells are its columns' names, and for numbers, dates and durations in a column's format, which
	/// a reader may read by the format.
	/// </summary>
	/// <param name="model">The model.</param>
	/// <param name="read">The workbook's sheets, read.</param>
	/// <param name="plain">The same data's sheets in Phase 4's look, read.</param>
	/// <returns>What differs, or <see langword="null"/>.</returns>
	private static string? ReadBackProblem(Model model, List<(string Name, List<List<string>> Rows)> read, List<(string Name, List<List<string>> Rows)> plain)
	{
		if (model.Sheets.Count == 0)
		{
			// The writer's empty Sheet1, which the package's check checks.
			return read.Count == 1 && read[0].Rows.Count == 0 && plain.Count == 1 ? null : $"a workbook of no sheets reads back {read.Count} sheets";
		}

		if (read.Count != plain.Count || read.Count != model.Sheets.Count)
		{
			return $"{read.Count} sheets read back, {plain.Count} in Phase 4's look, and the model has {model.Sheets.Count}";
		}

		for (var s = 0; s < read.Count; s++)
		{
			var sheet = model.Sheets[s];
			if (read[s].Name != plain[s].Name || read[s].Name != sheet.Name)
			{
				return $"sheet {s + 1} is {Show(read[s].Name)}, {Show(plain[s].Name)} in Phase 4's look, and {Show(sheet.Name)} in the model";
			}

			var rows = Math.Max(read[s].Rows.Count, plain[s].Rows.Count);
			for (var r = 0; r < rows; r++)
			{
				var got = r < read[s].Rows.Count ? read[s].Rows[r] : [];
				var want = r < plain[s].Rows.Count ? plain[s].Rows[r] : [];
				var row = r + 1;
				if (row == sheet.HeaderRow && sheet.TableColumns is { } names)
				{
					want = [.. names];
				}

				for (var c = 0; c < Math.Max(got.Count, want.Count); c++)
				{
					var have = c < got.Count ? got[c] : "";
					var expected = c < want.Count ? want[c] : "";
					if (have != expected && !sheet.ReadsByFormat(row, c))
					{
						return $"sheet {Show(sheet.Name)}, row {row}, column {c + 1}: read {Show(have)}, expected {Show(expected)}";
					}
				}
			}
		}

		return null;
	}

	/// <summary>
	/// Whether a sheet looks as the model says: its table (name, range, style and columns), filter and its hidden name,
	/// panes, merged ranges, hidden columns and widths, and links.
	/// </summary>
	/// <param name="sheet">The model's sheet.</param>
	/// <param name="written">The sheet as written.</param>
	/// <param name="package">The workbook as written.</param>
	/// <param name="input">The input.</param>
	/// <returns>What differs, or <see langword="null"/>.</returns>
	private static string? LookProblem(OutputSheet sheet, PackageSheet written, Package package, Input input)
	{
		var options = sheet.Source.Options;
		var header = sheet.HeaderRow;
		var last = Column(sheet.Width);

		// The table, named and with its columns named by the rules.
		if (sheet.TableName is { } tableName)
		{
			if (written.Table is not { } table)
			{
				return $"it has no table, and the model's is {Show(tableName)}";
			}

			if (table.Name != tableName || table.DisplayName != tableName)
			{
				return $"its table is named {Show(table.Name)} ({Show(table.DisplayName)}), and the model's is {Show(tableName)}";
			}

			if (TableNameProblem(table.Name) is { } rule)
			{
				return $"its table's name {Show(table.Name)} {rule}";
			}

			if (!table.Reference.StartsWith($"A{header}:{last}", StringComparison.Ordinal))
			{
				return $"its table spans {table.Reference}, from A{header} to column {last}";
			}

			var style = options.Table!.Style is { } s ? ExcelTableOptions.BuiltInStyleName(s) : null;
			if (table.Style != style)
			{
				return $"its table's style is {table.Style}, and {style ?? "none"} was given";
			}

			if (!table.Columns.SequenceEqual(sheet.TableColumns!, StringComparer.Ordinal))
			{
				return $"its table's columns are {string.Join(", ", table.Columns.Select(Show))}, and the model's {string.Join(", ", sheet.TableColumns!.Select(Show))}";
			}

			if (table.Columns.Any(c => c.Length > MaxName) || table.Columns.Select(Key).Distinct(StringComparer.Ordinal).Count() != table.Columns.Count)
			{
				return $"its table's columns, {string.Join(", ", table.Columns.Select(Show))}, are longer than {MaxName} or alike";
			}
		}
		else if (written.Table is { } table)
		{
			return $"it has the table {Show(table.Name)}, and the model has none";
		}

		// The filter, and its hidden name.
		var filtered = options.AutoFilter && options.Table is null && input.HasHeader;
		if (filtered != (written.AutoFilter is not null))
		{
			return filtered ? "it has no filter" : $"it has the filter {written.AutoFilter}";
		}

		if (filtered)
		{
			if (!written.AutoFilter!.StartsWith($"A{header}:{last}", StringComparison.Ordinal))
			{
				return $"its filter spans {written.AutoFilter}, from A{header} to column {last}";
			}

			if (!package.HiddenFilterNames.Contains(written.Index))
			{
				return "its filter has no hidden _xlnm._FilterDatabase name";
			}
		}

		// The panes.
		var frozen = (Rows: options.FreezeHeader && input.HasHeader ? header : 0, Columns: options.FreezeColumns);
		if (written.Frozen != frozen)
		{
			return $"it freezes {written.Frozen.Rows} rows and {written.Frozen.Columns} columns, and the model {frozen.Rows} and {frozen.Columns}";
		}

		// The merged ranges: the title's, and those merged by hand.
		var merged = new List<string>();
		if (options.Title is not null && sheet.TitleWidth > 1)
		{
			merged.Add($"A1:{Column(sheet.TitleWidth)}1");
		}

		if (sheet.Index == 0)
		{
			merged.AddRange(sheet.Added.Merged.Select(r => r.Corners));
		}

		if (!written.Merged.OrderBy(m => m, StringComparer.Ordinal).SequenceEqual(merged.OrderBy(m => m, StringComparer.Ordinal), StringComparer.Ordinal))
		{
			return $"its merged ranges are {string.Join(", ", written.Merged)}, and the model's {string.Join(", ", merged)}";
		}

		// The hidden columns, and the widths.
		var hidden = sheet.HiddenColumns();
		if (!written.Hidden.SetEquals(hidden))
		{
			return $"its hidden columns are {string.Join(", ", written.Hidden.OrderBy(c => c))}, and the model's {string.Join(", ", hidden.OrderBy(c => c))}";
		}

		if (written.Widths.Where(w => !(w is >= 0 and <= 255)).ToList() is { Count: > 0 } wide)
		{
			return $"it has columns {string.Join(", ", wide)} wide, and the schema takes 0 to 255";
		}

		// The links: the model's cells, each to a target the policy takes.
		var links = sheet.Links(input.Links);
		if (!written.Links.Select(l => l.Cell).SequenceEqual(links, StringComparer.Ordinal))
		{
			return $"its links are in {string.Join(", ", written.Links.Select(l => l.Cell))}, and the model's in {string.Join(", ", links)}";
		}

		foreach (var link in written.Links)
		{
			if (link.Target is not { } target || !link.External)
			{
				return $"the link in {link.Cell} has no external target";
			}

			if (target.Length == 0 || target.Length > MaxTarget)
			{
				return $"the link in {link.Cell} has a target of {target.Length} characters";
			}

			if (input.Links == ExcelLinks.Web && !(Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto"))
			{
				return $"the link in {link.Cell} is to {Show(target)}, which the Web policy doesn't link";
			}
		}

		return null;
	}

	/// <summary>
	/// What is wrong with a table's name, by the rules of <c>TableNames</c>' documentation of Excel's: 1 to 255 characters,
	/// starting with a letter, <c>_</c> or <c>\</c>, then letters, digits, <c>_ . ? \</c> and characters beyond the Basic
	/// Multilingual Plane; not Excel's own (<c>_xlnm.</c>), a cell reference, a row or column reference, or a boolean.
	/// </summary>
	/// <param name="name">The name.</param>
	/// <returns>What is wrong, or <see langword="null"/>.</returns>
	private static string? TableNameProblem(string name)
	{
		if (name.Length is 0 or > MaxName)
		{
			return $"has {name.Length} characters";
		}

		if (!(char.IsLetter(name[0]) || name[0] is '_' or '\\'))
		{
			return "has a first character Excel refuses";
		}

		for (var i = 1; i < name.Length; i++)
		{
			if (char.IsHighSurrogate(name[i]) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]))
			{
				i++;

				continue;
			}

			if (!(char.IsLetter(name[i]) || name[i] is (>= '0' and <= '9') or '_' or '.' or '?' or '\\'))
			{
				return $"has {Show(name[i].ToString())}, which Excel refuses";
			}
		}

		if (name.StartsWith("_xlnm.", StringComparison.OrdinalIgnoreCase))
		{
			return "starts as Excel's own names do";
		}

		var letters = 0;
		while (letters < name.Length && letters < 4 && (name[letters] is >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
		{
			letters++;
		}

		if (letters is >= 1 and <= 3 && letters < name.Length && ColumnNumber(name.Substring(0, letters)) <= MaxColumn &&
			name.Skip(letters).All(c => c is >= '0' and <= '9') && name.Skip(letters).Any(c => c != '0'))
		{
			return "is a cell reference";
		}

		var upper = name.ToUpperInvariant();
		var at = upper.StartsWith("RC", StringComparison.Ordinal) ? 2 : upper[0] is 'R' or 'C' ? 1 : 0;
		if (at > 0 && (at == name.Length || name[at] is >= '0' and <= '9'))
		{
			return "is a row or column reference";
		}

		if (upper is "TRUE" or "FALSE")
		{
			return "is a boolean";
		}

		return null;
	}

	/// <summary>A column's number from its letters.</summary>
	/// <param name="letters">One to three ASCII letters.</param>
	/// <returns>The number, from 1.</returns>
	private static int ColumnNumber(string letters) => letters.Aggregate(0, (n, c) => (n * 26) + (char.ToUpperInvariant(c) - 'A' + 1));

	/// <summary>A column's letters.</summary>
	/// <param name="number">The column's number, from 1.</param>
	/// <returns>The letters.</returns>
	private static string Column(int number)
	{
		var letters = "";
		for (var n = number; n > 0; n = (n - 1) / 26)
		{
			letters = (char)('A' + ((n - 1) % 26)) + letters;
		}

		return letters;
	}

	/// <summary>
	/// How Excel compares names ignoring case, as the test models it (Phase 4's step 6, step 7's probes, and
	/// <c>SheetNames.Key</c>'s documentation): without the characters Excel ignores (U+FFFD, which a lone surrogate is written
	/// as, a byte order mark, a soft hyphen, a direction's mark), NFKC, <c>ß</c> and <c>ẞ</c> as <c>ss</c>, and upper case,
	/// twice, as Unicode's caseless match does it.
	/// </summary>
	/// <param name="name">The name, valid UTF-16 without non-characters.</param>
	/// <returns>The key.</returns>
	private static string Key(string name)
	{
		var key = new string([.. name.Where(c => c is not ('\uFFFD' or '\uFEFF' or '\u00AD' or '\u200E'))]);
		for (var i = 0; i < 2; i++)
		{
			key = key.Normalize(NormalizationForm.FormKC).Replace(Units(0xDF), "ss").Replace(Units(0x1E9E), "ss").ToUpperInvariant();
		}

		return key;
	}

	/// <summary>Text as the writer writes it: through UTF-8, so that a lone surrogate is U+FFFD.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The text written.</returns>
	private static string AsWritten(string text) => Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(text));

	/// <summary>A name cut, between characters, to a length.</summary>
	/// <param name="name">The name.</param>
	/// <param name="length">The most characters to keep.</param>
	/// <returns>The name, or its start.</returns>
	private static string Cut(string name, int length) =>
		name.Length <= length ? name : name.Substring(0, char.IsHighSurrogate(name[length - 1]) ? length - 1 : length);

	/// <summary>A random element.</summary>
	/// <typeparam name="T">The elements' type.</typeparam>
	/// <param name="random">The random numbers.</param>
	/// <param name="items">The elements.</param>
	/// <returns>One of them.</returns>
	private static T Pick<T>(Random random, IReadOnlyList<T> items) => items[random.Next(items.Count)];

	/// <summary>Text of one to a few random pieces.</summary>
	/// <param name="random">The random numbers.</param>
	/// <param name="pieces">The pieces.</param>
	/// <param name="most">The most pieces.</param>
	/// <returns>The text.</returns>
	private static string Text(Random random, string[] pieces, int most)
	{
		var text = new StringBuilder();
		for (var i = random.Next(1, most + 1); i > 0; i--)
		{
			text.Append(Pick(random, pieces));
		}

		return text.ToString();
	}

	/// <summary>A long URI, of ASCII that its absolute form keeps as it is, around the most characters of a link's target.</summary>
	/// <param name="random">The random numbers.</param>
	/// <returns>The URI's text.</returns>
	private static string LongUri(Random random)
	{
		var start = random.Next(3) == 0 ? "docs/" : "https://example.com/";
		var length = random.Next(4) == 0 ? random.Next(9000, 12000) : MaxTarget + random.Next(-3, 4);

		return start + new string('a', Math.Max(1, length - start.Length));
	}

	/// <summary>A workbook to write: its options, and its sheets.</summary>
	/// <param name="seed">The seed it was made from.</param>
	private sealed class Input(int seed)
	{
		/// <summary>The seed it was made from.</summary>
		public int Seed { get; } = seed;

		/// <summary>Whether it is written asynchronously.</summary>
		public bool Async { get; private init; }

		/// <summary>Where text cells keep their strings.</summary>
		public ExcelStrings Strings { get; private init; }

		/// <summary>Which URIs are links.</summary>
		public ExcelLinks Links { get; private init; }

		/// <summary>Whether each sheet's first record is its header.</summary>
		public bool HasHeader { get; private init; }

		/// <summary>The rows of a sheet before <see cref="ExcelRowLimit.NewSheet"/> starts the next one, or 0 for no limit.</summary>
		public int MaxRows { get; private init; }

		/// <summary>The sheets.</summary>
		public List<SheetInput> Sheets { get; } = [];

		/// <summary>Makes an input from its seed.</summary>
		/// <param name="seed">The seed.</param>
		/// <returns>The input.</returns>
		public static Input Of(int seed)
		{
			var random = new Random(seed);
			var input = new Input(seed)
			{
				Async = random.Next(2) == 1,
				Strings = random.Next(3) == 0 ? ExcelStrings.Shared : ExcelStrings.Inline,
				Links = random.Next(4) switch { 0 => ExcelLinks.None, 1 => ExcelLinks.All, _ => ExcelLinks.Web },
				HasHeader = random.Next(8) != 0,
				MaxRows = random.Next(6) == 0 ? random.Next(4, 8) : 0,
			};

			var names = SheetNames.OrderBy(_ => random.Next()).Take(random.Next(1, 4)).ToList();
			foreach (var name in names)
			{
				input.Sheets.Add(SheetInput.Of(random, name, input));
			}

			return input;
		}

		/// <inheritdoc/>
		public override string ToString() =>
			$"{(Async ? "async" : "sync")}, {Strings}, links {Links}{(HasHeader ? "" : ", no header")}{(MaxRows > 0 ? $", NewSheet at {MaxRows} rows" : "")}: " +
			string.Join("; ", Sheets);
	}

	/// <summary>A sheet to write: its name, options, header, records and ranges to merge.</summary>
	private sealed class SheetInput
	{
		/// <summary>The sheet's name.</summary>
		public string Name { get; private init; } = "";

		/// <summary>The sheet's options.</summary>
		public ExcelSheetOptions Options { get; private init; } = ExcelSheetOptions.Default;

		/// <summary>What was planted that Excel wouldn't open, to say so; or <see langword="null"/>.</summary>
		public string? Planted { get; private init; }

		/// <summary>Whether its table is given a name of a table that an earlier sheet has, in other cases (planted when the workbook has one).</summary>
		public bool RepeatsATableName { get; private init; }

		/// <summary>The header's fields, or none without a header.</summary>
		public List<Field> Header { get; } = [];

		/// <summary>The records' fields.</summary>
		public List<List<Field>> Records { get; } = [];

		/// <summary>The ranges to merge.</summary>
		public List<MergeInput> Merges { get; } = [];

		/// <summary>Makes a sheet.</summary>
		/// <param name="random">The random numbers.</param>
		/// <param name="name">Its name.</param>
		/// <param name="input">The workbook it is in.</param>
		/// <returns>The sheet.</returns>
		public static SheetInput Of(Random random, string name, Input input)
		{
			var width = random.Next(1, 7);
			var header = new List<Field>();
			if (input.HasHeader)
			{
				for (var c = 0; c < width; c++)
				{
					header.Add(random.Next(12) switch
					{
						0 => new Field(random.Next(-5, 2025)),
						1 => new Field(random.Next(2) == 0),
						2 => Field.OfUri(Pick(random, UriTexts)),
						3 => new Field(new string('h', MaxName + random.Next(-2, 3))),
						4 => new Field(new string('h', MaxNumbered + random.Next(-2, 1)) + Pick(random, ["", "x", Units(0xD83D, 0xDE00)])),
						5 => new Field((string?)null),
						_ => new Field(Text(random, HeaderPieces, 2)),
					});
				}
			}

			// The options, each now and then, and one of them, now and then, planted where Excel wouldn't open it.
			var title = random.Next(3) == 0 ? (random.Next(40) == 0 ? new string('t', 32767) : Text(random, TitlePieces, 5)) : null;
			var table = random.Next(3) == 0
				? new ExcelTableOptions
				{
					Name = random.Next(3) == 0 ? Pick(random, GoodTableNames) : null,
					Style = random.Next(6) switch
					{
						0 => null,
						1 => "tablestylelight" + random.Next(1, 22).ToString(CultureInfo.InvariantCulture),
						2 => "TableStyleDark" + random.Next(1, 12).ToString(CultureInfo.InvariantCulture),
						_ => "TableStyleMedium" + random.Next(1, 29).ToString(CultureInfo.InvariantCulture),
					},
					BandedRows = random.Next(4) != 0,
					FilterButtons = random.Next(4) != 0,
				}
				: null;
			Dictionary<string, ExcelColumnOptions>? columns = null;
			if (input.HasHeader && random.Next(2) == 0)
			{
				columns = new Dictionary<string, ExcelColumnOptions>(StringComparer.Ordinal);
				// Not by a name longer than a table's column's, which a table leaves out of the header's names.
				foreach (var field in header.Where(f => f.Text.Length is > 0 and <= MaxName && random.Next(2) == 0))
				{
					columns[field.Text] = new ExcelColumnOptions
					{
						Width = random.Next(3) == 0 ? Math.Round(random.NextDouble() * 255, random.Next(3)) : null,
						Format = random.Next(2) == 0 ? Pick(random, GoodCodes) : null,
						Wrap = random.Next(3) == 0,
						Hidden = random.Next(6) == 0,
					};
				}
			}

			var options = new ExcelSheetOptions
			{
				Title = title,
				BoldHeader = random.Next(4) != 0,
				FreezeHeader = random.Next(4) != 0,
				FreezeColumns = random.Next(3) == 0 ? random.Next(0, 4) : 0,
				AutoFilter = random.Next(4) == 0,
				Table = table,
				AutoFit = Pick(random, new[] { 0, 1, 2, 3, 100, 100 }),
				MaxAutoFitWidth = random.Next(4) == 0 ? Math.Round(1 + (random.NextDouble() * 254), 1) : 60,
				Columns = columns,
			};

			string? planted = null;
			var repeats = false;
			if (!input.HasHeader && (options.AutoFilter || options.Table is not null))
			{
				planted = options.Table is not null ? "a table without a header" : "a filter without a header";
			}
			else if (random.Next(10) == 0)
			{
				var given = input.Sheets.Select(s => s.Options.Table?.Name).LastOrDefault(n => n is not null && TableNameProblem(n) is null);
				(options, planted, repeats) = Plant(random, options, header, given);
			}

			var sheet = new SheetInput { Name = name, Options = options, Planted = planted, RepeatsATableName = repeats };
			sheet.Header.AddRange(header);
			for (var r = random.Next(0, 7); r > 0; r--)
			{
				var fields = new List<Field>();
				for (var c = Math.Max(1, width + (random.Next(5) == 0 ? random.Next(-1, 3) : 0)); c > 0; c--)
				{
					fields.Add(Field.Random(random));
				}

				sheet.Records.Add(fields);
			}

			if (input.MaxRows == 0 && random.Next(2) == 0)
			{
				var slots = sheet.Records.Count + (input.HasHeader ? 1 : 0);
				var rows = (title is null ? 0 : 1) + slots + 2;
				for (var m = random.Next(1, 5); m > 0; m--)
				{
					// With a table, after the header: a range merged before it in the table's columns fails the header.
					var slot = random.Next(input.HasHeader && options.Table is not null ? 1 : 0, slots + 1);
					sheet.Merges.Add(MergeInput.Of(random, slot, rows, width + 2));
				}
			}

			return sheet;
		}

		/// <summary>Plants one option Excel wouldn't open.</summary>
		/// <param name="random">The random numbers.</param>
		/// <param name="options">The options.</param>
		/// <param name="header">The header's fields.</param>
		/// <param name="given">The name given to an earlier sheet's table, or <see langword="null"/>.</param>
		/// <returns>The options, what was planted, and whether it is a table's name an earlier sheet has.</returns>
		private static (ExcelSheetOptions Options, string Planted, bool Repeats) Plant(Random random, ExcelSheetOptions options, List<Field> header, string? given)
		{
			switch (random.Next(given is null ? 9 : 10))
			{
				case 0:
					return (options with { Title = random.Next(2) == 0 ? "" : new string('t', 32768) }, "a title's length", false);
				case 1:
					return (options with { FreezeColumns = random.Next(2) == 0 ? -1 : 16384 }, "FreezeColumns", false);
				case 2:
					return (options with { AutoFit = -1 }, "AutoFit", false);
				case 3:
					return (options with { MaxAutoFitWidth = Pick(random, new[] { 0, 0.5, 255.5, double.NaN }) }, "MaxAutoFitWidth", false);
				case 4:
					return (options with { Table = (options.Table ?? new ExcelTableOptions()) with { Name = Pick(random, BadTableNames) } }, "a table's name", false);
				case 5:
					return (options with { Table = (options.Table ?? new ExcelTableOptions()) with { Style = Pick(random, new[] { "TableStyleMedium29", "TableStyleLight0", "TableStyleMedium02", "Fancy", "" }) } }, "a table's style", false);
				case 6:
				case 7:
					var name = header.FirstOrDefault(f => f.Text.Length > 0)?.Text ?? "Name";
					var column = random.Next(2) == 0
						? new ExcelColumnOptions { Width = Pick(random, new[] { -1, 255.5, double.NaN, double.PositiveInfinity }) }
						: new ExcelColumnOptions { Format = Pick(random, BadCodes) };

					return (options with { Columns = new Dictionary<string, ExcelColumnOptions>(StringComparer.Ordinal) { [name] = column } }, "a column's options", false);
				case 8:
					return (options with { Table = (options.Table ?? new ExcelTableOptions()) with { Style = "TableStyleLight22" } }, "a table's style", false);
				default:
					var swapped = new string([.. given!.Select(c => char.IsUpper(c) ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c))]);

					return (options with { Table = (options.Table ?? new ExcelTableOptions()) with { Name = swapped } }, "a table's name the workbook has", true);
			}
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			var parts = new List<string> { Show(Name) };
			if (Options.Title is { } title)
			{
				parts.Add("title " + Show(title));
			}

			if (Options.Table is { } table)
			{
				parts.Add($"table {(table.Name is null ? "by the sheet's name" : Show(table.Name))} {table.Style ?? "unstyled"}");
			}

			if (Options.AutoFilter)
			{
				parts.Add("filter");
			}

			if (Options.Columns is { Count: > 0 } columns)
			{
				parts.Add($"{columns.Count} columns' options");
			}

			if (Planted is not null)
			{
				parts.Add("planted " + Planted);
			}

			parts.Add($"header [{string.Join(", ", Header)}]");
			parts.Add($"{Records.Count} records");
			if (Merges.Count > 0)
			{
				parts.Add("merges " + string.Join(", ", Merges));
			}

			return string.Join(", ", parts);
		}
	}

	/// <summary>A field to write: a value of a type the writer types, or text, a URI or nothing.</summary>
	/// <param name="value">The value, or <see langword="null"/> for an empty field.</param>
	private sealed class Field(object? value)
	{
		/// <summary>The value.</summary>
		public object? Value { get; } = value;

		/// <summary>The field's text as the writer writes it: the CSV header's name.</summary>
		public string Text { get; } = value switch
		{
			null => "",
			string s => AsWritten(s),
			Uri u => AsWritten(u.OriginalString),
			bool b => b ? "True" : "False",
			IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
			_ => value.ToString() ?? "",
		};

		/// <summary>Whether a reader may read the field's cell by its column's format: a number, date or duration.</summary>
		public bool IsNumeric => Value is int or double or decimal or DateTime or TimeSpan;

		/// <summary>A URI's field, or its text's when it isn't a URI .NET makes.</summary>
		/// <param name="text">The URI's text.</param>
		/// <returns>The field.</returns>
		public static Field OfUri(string text) => new(Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out var uri) ? uri : text);

		/// <summary>Makes a random field of the data.</summary>
		/// <param name="random">The random numbers.</param>
		/// <returns>The field.</returns>
		public static Field Random(Random random) => random.Next(20) switch
		{
			< 6 => new Field(Text(random, TextPieces, 3)),
			< 10 => OfUri(random.Next(8) == 0 ? LongUri(random) : Pick(random, UriTexts)),
			10 => new Field(random.Next(-1000, 1000)),
			11 => new Field(Pick(random, new[] { 0.5, -1234.5678, 1e15, 1e-7, double.NaN, double.PositiveInfinity, double.MaxValue })),
			12 => new Field(Pick(random, new[] { 0.1m, 12345678901234567m, -7m })),
			13 => new Field(random.Next(2) == 0),
			14 => new Field(Pick(random, new[] { new DateTime(2024, 1, 15), new DateTime(2024, 1, 15, 10, 30, 0, 123), new DateTime(1899, 12, 31), new DateTime(1900, 2, 28), DateTime.MaxValue })),
			15 => new Field(Pick(random, new[] { TimeSpan.FromHours(1.5), TimeSpan.FromDays(1000), TimeSpan.FromTicks(-1), TimeSpan.FromHours(-2), TimeSpan.MaxValue, TimeSpan.FromDays(2_958_466) })),
			_ => new Field((string?)null),
		};

		/// <summary>Writes the field.</summary>
		/// <param name="writer">The sheet's writer.</param>
		public void WriteTo(CsvWriter writer)
		{
			if (Value is Uri uri)
			{
				writer.WriteField(uri);
			}
			else
			{
				writer.WriteField(Value);
			}
		}

		/// <inheritdoc/>
		public override string ToString() => Value switch
		{
			null => "null",
			Uri u => "URI " + Show(u.OriginalString),
			string s => Show(s),
			_ => $"{Value.GetType().Name} {Text}",
		};
	}

	/// <summary>A range to merge after some records: its text, and the range it is, if it is one.</summary>
	/// <param name="Slot">The records written before it, the header's included.</param>
	/// <param name="Text">The text given to <see cref="ExcelWriterExtensions.MergeCells"/>.</param>
	/// <param name="Range">The range, or <see langword="null"/> when the text is none of two cells or more.</param>
	private sealed record MergeInput(int Slot, string Text, Area? Range)
	{
		/// <summary>Makes a random range, in one of the forms <see cref="ExcelWriterExtensions.MergeCells"/> takes, or text that isn't one.</summary>
		/// <param name="random">The random numbers.</param>
		/// <param name="slot">The records written before it.</param>
		/// <param name="rows">The rows the sheet's ranges are in.</param>
		/// <param name="columns">The columns the sheet's ranges are in.</param>
		/// <returns>The range to merge.</returns>
		public static MergeInput Of(Random random, int slot, int rows, int columns)
		{
			switch (random.Next(12))
			{
				case 0:
					return new MergeInput(slot, Pick(random, BadRanges), null);
				case 1:
					var left = random.Next(1, columns + 1);
					var right = random.Next(left + 1, columns + 3);

					return new MergeInput(slot, $"{Column(left)}:{Column(right)}", new Area(1, left, MaxRow, right));
				case 2:
					var top = random.Next(1, rows + 1);
					var bottom = random.Next(top + 1, rows + 3);

					return new MergeInput(slot, $"{top}:{bottom}", new Area(top, 1, bottom, MaxColumn));
				default:
					var (r1, c1) = (random.Next(1, rows + 1), random.Next(1, columns + 1));
					var (r2, c2) = (random.Next(1, rows + 1), random.Next(1, columns + 1));
					var area = new Area(Math.Min(r1, r2), Math.Min(c1, c2), Math.Max(r1, r2), Math.Max(c1, c2));
					var dollar = random.Next(3) == 0 ? "$" : "";
					var text = $"{dollar}{Column(c1)}{dollar}{r1}:{dollar}{Column(c2)}{dollar}{r2}";

					return new MergeInput(slot, text, area.Top == area.Bottom && area.Left == area.Right ? null : area);
			}
		}

		/// <inheritdoc/>
		public override string ToString() => $"{Text} after {Slot}";
	}

	/// <summary>A block of cells.</summary>
	/// <param name="Top">The first row, from 1.</param>
	/// <param name="Left">The first column, from 1.</param>
	/// <param name="Bottom">The last row.</param>
	/// <param name="Right">The last column.</param>
	private readonly record struct Area(int Top, int Left, int Bottom, int Right)
	{
		/// <summary>The range's corners, as a sheet's <c>mergeCell</c> has them.</summary>
		public string Corners => $"{Column(Left)}{Top}:{Column(Right)}{Bottom}";

		/// <summary>Whether two blocks share a cell.</summary>
		/// <param name="other">The other block.</param>
		/// <returns>Whether they do.</returns>
		public bool Overlaps(Area other) => Top <= other.Bottom && other.Top <= Bottom && Left <= other.Right && other.Left <= Right;
	}

	/// <summary>
	/// The test's model of a workbook: the sheets added, the tables' names the workbook has, and the sheets written, as
	/// <see cref="ExcelRowLimit.NewSheet"/> splits them.
	/// </summary>
	/// <param name="input">The input.</param>
	private sealed class Model(Input input)
	{
		// The keys of the workbook's tables' names.
		private readonly HashSet<string> tables = new(StringComparer.Ordinal);

		/// <summary>The input.</summary>
		public Input Input { get; } = input;

		/// <summary>The sheets written, in tab order.</summary>
		public List<OutputSheet> Sheets { get; } = [];

		/// <summary>The sheets the writer refused.</summary>
		public HashSet<SheetInput> Refused { get; } = [];

		/// <summary>Why the writer refuses a sheet's options, by the model's rules, or <see langword="null"/>.</summary>
		/// <param name="sheet">The sheet.</param>
		/// <returns>What it refuses.</returns>
		public string? Refusal(SheetInput sheet)
		{
			if (sheet.Planted is not null && !sheet.RepeatsATableName)
			{
				return sheet.Planted;
			}

			if (!Input.HasHeader && (sheet.Options.AutoFilter || sheet.Options.Table is not null))
			{
				return sheet.Options.Table is not null ? "a table without a header" : "a filter without a header";
			}

			if (sheet.Options.Table?.Name is { } name)
			{
				if (TableNameProblem(name) is not null)
				{
					return "a table's name";
				}

				if (tables.Contains(Key(name)))
				{
					return "a table's name the workbook has";
				}
			}

			return null;
		}

		/// <summary>Adds a sheet the writer added, and reserves its table's name.</summary>
		/// <param name="sheet">The sheet.</param>
		/// <param name="plain">Whether the workbook is in Phase 4's look, without tables.</param>
		/// <returns>The sheet added.</returns>
		public AddedSheet Add(SheetInput sheet, bool plain)
		{
			string? table = null;
			if (!plain && sheet.Options.Table is { } options && Input.HasHeader)
			{
				table = options.Name ?? Unique(FromSheetName(sheet.Name));
				tables.Add(Key(table));
			}

			return new AddedSheet(sheet, Input, table);
		}

		/// <summary>Adds the sheets an added sheet was written as, the ones <see cref="ExcelRowLimit.NewSheet"/> started after it included.</summary>
		/// <param name="added">The sheet added.</param>
		public void Complete(AddedSheet added)
		{
			var source = added.Source;
			var above = (source.Options.Title is null ? 0 : 1) + (Input.HasHeader ? 1 : 0);
			var room = Input.MaxRows > 0 ? Input.MaxRows - above : int.MaxValue;
			var chunks = source.Records.Count == 0 || room == int.MaxValue ? 1 : (source.Records.Count + room - 1) / room;
			for (var i = 0; i < chunks; i++)
			{
				var name = i == 0 ? source.Name : NewSheetName(source.Name, i + 1);
				string? table = null;
				if (added.TableName is { } first)
				{
					table = i == 0 ? first : Next(first);
					if (i > 0)
					{
						tables.Add(Key(table));
					}
				}

				// The title spans the header's columns, or, without one, each sheet's first record's.
				var records = source.Records.Skip(i * room).Take(room).ToList();
				var width = Input.HasHeader ? source.Header.Count : records.FirstOrDefault()?.Count ?? 0;
				Sheets.Add(new OutputSheet(added, i, name, table, records, width));
			}
		}

		/// <summary>The name of a sheet <see cref="ExcelRowLimit.NewSheet"/> starts, as Excel names a copy: <c>Orders (2)</c>, cut to 31.</summary>
		/// <param name="name">The first sheet's name.</param>
		/// <param name="number">The copy's number, from 2.</param>
		/// <returns>The name.</returns>
		private static string NewSheetName(string name, int number)
		{
			var suffix = $" ({number.ToString(CultureInfo.InvariantCulture)})";

			return Cut(name, 31 - suffix.Length) + suffix;
		}

		/// <summary>
		/// A table's name from its sheet's, by <c>TableNames</c>' documented rule: <c>_</c> for each character a table's name
		/// can't have, and <c>_</c> first when the name would still break a rule.
		/// </summary>
		/// <param name="sheet">The sheet's name.</param>
		/// <returns>The table's name.</returns>
		private static string FromSheetName(string sheet)
		{
			var name = new StringBuilder();
			for (var i = 0; i < sheet.Length; i++)
			{
				if (char.IsHighSurrogate(sheet[i]) && i + 1 < sheet.Length && char.IsLowSurrogate(sheet[i + 1]))
				{
					name.Append(sheet, i, 2);
					i++;

					continue;
				}

				name.Append(char.IsLetter(sheet[i]) || sheet[i] is (>= '0' and <= '9') or '_' or '.' or '?' or '\\' ? sheet[i] : '_');
			}

			var made = name.ToString();

			return TableNameProblem(made) is null ? made : "_" + made;
		}

		/// <summary>A name, or the name with the first number from 2 the workbook doesn't have.</summary>
		/// <param name="name">The name.</param>
		/// <returns>The name.</returns>
		private string Unique(string name) => tables.Contains(Key(name)) ? Next(name) : name;

		/// <summary>The name with the first number from 2 the workbook doesn't have: <c>Orders2</c>, or <c>AB_2</c> where <c>AB2</c> breaks a rule.</summary>
		/// <param name="name">The name.</param>
		/// <returns>The numbered name.</returns>
		private string Next(string name)
		{
			for (var number = 2; ; number++)
			{
				var digits = number.ToString(CultureInfo.InvariantCulture);
				var numbered = Cut(name, MaxName - digits.Length) + digits;
				if (TableNameProblem(numbered) is not null)
				{
					numbered = Cut(name, MaxName - digits.Length - 1) + "_" + digits;
				}

				if (!tables.Contains(Key(numbered)))
				{
					return numbered;
				}
			}
		}
	}

	/// <summary>A sheet the writer added: its input, its table's name, its header's columns' names, and the ranges merged.</summary>
	private sealed class AddedSheet
	{
		/// <summary>Makes the sheet added.</summary>
		/// <param name="source">The sheet's input.</param>
		/// <param name="input">The workbook's input.</param>
		/// <param name="tableName">Its table's name, or <see langword="null"/> without one.</param>
		public AddedSheet(SheetInput source, Input input, string? tableName)
		{
			Source = source;
			Input = input;
			TableName = tableName;
			HeaderRow = input.HasHeader ? (source.Options.Title is null ? 1 : 2) : 0;
			if (tableName is not null)
			{
				TableColumns = ColumnNames([.. source.Header.Select(f => RefusesHeaderName(f) ? "" : f.Text)]);
			}
		}

		/// <summary>The sheet's input.</summary>
		public SheetInput Source { get; }

		/// <summary>The workbook's input.</summary>
		public Input Input { get; }

		/// <summary>Its table's name, or <see langword="null"/> without one.</summary>
		public string? TableName { get; }

		/// <summary>The header's row, from 1, or 0 without a header.</summary>
		public int HeaderRow { get; }

		/// <summary>Its table's columns' names, or <see langword="null"/> without a table.</summary>
		public List<string>? TableColumns { get; }

		/// <summary>The ranges merged, in order.</summary>
		public List<Area> Merged { get; } = [];

		/// <summary>Whether the writer refuses a header field: with a table, a name longer than 255 characters.</summary>
		/// <param name="field">The field.</param>
		/// <returns>Whether it does.</returns>
		public bool RefusesHeaderName(Field field) => TableName is not null && field.Text.Length > MaxName;

		/// <summary>Why the writer refuses a range, by the model's rules, or <see langword="null"/>.</summary>
		/// <param name="merge">The range to merge.</param>
		/// <param name="slot">The records written before it, the header's included.</param>
		/// <returns>What it refuses.</returns>
		public string? MergeRefusal(MergeInput merge, int slot)
		{
			if (merge.Range is not { } range)
			{
				return "not a range of two cells";
			}

			if (Merged.Any(range.Overlaps))
			{
				return "overlapping";
			}

			if (Source.Options.Title is not null && range.Top == 1)
			{
				return "in the title's row";
			}

			if (TableName is not null && slot >= 1 && range.Left <= Source.Header.Count && range.Bottom >= HeaderRow)
			{
				return "in the table";
			}

			return null;
		}

		/// <summary>
		/// The names of a table's columns, by <c>TableWriter</c>'s documented rules of Excel's: an empty field is <c>Column</c>
		/// and a number, and a name a column to its left has gets a number, 2 at least, cut to 253 characters with it; the
		/// numbers go up across the header, past names any column has.
		/// </summary>
		/// <param name="header">The header's texts, with "" for a field refused.</param>
		/// <returns>The names.</returns>
		private static List<string> ColumnNames(List<string> header)
		{
			var taken = new HashSet<string>(header.Where(f => f.Length > 0).Select(Key), StringComparer.Ordinal);
			var left = new HashSet<string>(StringComparer.Ordinal);
			var names = new List<string>();
			var number = 1;
			foreach (var field in header)
			{
				var name = field;
				if (field.Length == 0 || left.Contains(Key(field)))
				{
					var stem = field.Length == 0 ? "Column" : field;
					if (field.Length > 0)
					{
						number = Math.Max(number, 2);
					}

					do
					{
						var digits = (number++).ToString(CultureInfo.InvariantCulture);
						name = Cut(stem, MaxNumbered - digits.Length) + digits;
					}
					while (taken.Contains(Key(name)));
				}

				taken.Add(Key(name));
				left.Add(Key(name));
				names.Add(name);
			}

			return names;
		}
	}

	/// <summary>A sheet as written: the sheet added, or one <see cref="ExcelRowLimit.NewSheet"/> started after it.</summary>
	/// <param name="added">The sheet added.</param>
	/// <param name="index">0 for the sheet added, then 1 for the first that NewSheet started after it, and so on.</param>
	/// <param name="name">Its name.</param>
	/// <param name="tableName">Its table's name, or <see langword="null"/> without one.</param>
	/// <param name="records">Its records.</param>
	/// <param name="titleWidth">The columns of the header, or of the sheet's first record without one, which the title spans.</param>
	private sealed class OutputSheet(AddedSheet added, int index, string name, string? tableName, List<List<Field>> records, int titleWidth)
	{
		/// <summary>The sheet added.</summary>
		public AddedSheet Added { get; } = added;

		/// <summary>The sheet added's input.</summary>
		public SheetInput Source => Added.Source;

		/// <summary>0 for the sheet added, then 1 for the first that NewSheet started after it, and so on.</summary>
		public int Index { get; } = index;

		/// <summary>Its name.</summary>
		public string Name { get; } = name;

		/// <summary>Its table's name, or <see langword="null"/> without one.</summary>
		public string? TableName { get; } = tableName;

		/// <summary>Its records.</summary>
		public List<List<Field>> Records { get; } = records;

		/// <summary>The columns of the first record, which the title spans when it has two or more.</summary>
		public int TitleWidth { get; } = titleWidth;

		/// <summary>The header's row, from 1, or 0 without a header.</summary>
		public int HeaderRow => Added.HeaderRow;

		/// <summary>Its table's columns' names, or <see langword="null"/> without a table.</summary>
		public List<string>? TableColumns => TableName is null ? null : Added.TableColumns;

		/// <summary>The header's columns, which a filter and a table span.</summary>
		public int Width => Source.Header.Count;

		/// <summary>The row of its first record.</summary>
		private int FirstRecordRow => (Source.Options.Title is null ? 1 : 2) + (Added.Input.HasHeader ? 1 : 0);

		/// <summary>Whether a reader may read a cell by its column's format: a number, date or duration in a column with a format.</summary>
		/// <param name="row">The cell's row, from 1.</param>
		/// <param name="column">The cell's column, from 0.</param>
		/// <returns>Whether it may.</returns>
		public bool ReadsByFormat(int row, int column)
		{
			var record = row - FirstRecordRow;
			if (record < 0 || record >= Records.Count || column >= Records[record].Count || !Records[record][column].IsNumeric)
			{
				return false;
			}

			return column < Source.Header.Count && Source.Options.Columns is { } columns &&
				columns.TryGetValue(Source.Header[column].Text, out var options) && options.Format is not null;
		}

		/// <summary>The columns hidden by their options, from 1.</summary>
		/// <returns>The columns.</returns>
		public HashSet<int> HiddenColumns()
		{
			var hidden = new HashSet<int>();
			if (Source.Options.Columns is { } columns)
			{
				for (var c = 0; c < Source.Header.Count; c++)
				{
					if (!Added.RefusesHeaderName(Source.Header[c]) && columns.TryGetValue(Source.Header[c].Text, out var options) && options.Hidden)
					{
						hidden.Add(c + 1);
					}
				}
			}

			return hidden;
		}

		/// <summary>
		/// The cells that are links, by the policy as the writer documents it: a URI below the header, trimmed, that parses;
		/// absolute with <c>http</c>, <c>https</c> or <c>mailto</c> under <see cref="ExcelLinks.Web"/>, and absolute or
		/// relative under <see cref="ExcelLinks.All"/>; whose target has 8,192 characters at most. The long URIs are of
		/// ASCII that their targets keep as it is.
		/// </summary>
		/// <param name="policy">The policy.</param>
		/// <returns>The cells, in order.</returns>
		public List<string> Links(ExcelLinks policy)
		{
			var cells = new List<string>();
			for (var r = 0; r < Records.Count; r++)
			{
				for (var c = 0; c < Records[r].Count; c++)
				{
					if (Records[r][c].Value is Uri uri && Takes(uri.OriginalString, policy))
					{
						cells.Add(Column(c + 1) + (FirstRecordRow + r).ToString(CultureInfo.InvariantCulture));
					}
				}
			}

			return cells;
		}

		/// <summary>Whether a policy makes a URI's text a link.</summary>
		/// <param name="text">The text.</param>
		/// <param name="policy">The policy.</param>
		/// <returns>Whether it does.</returns>
		private static bool Takes(string text, ExcelLinks policy)
		{
			var trimmed = text.Trim();
			if (policy == ExcelLinks.None || trimmed.Length == 0 || !Uri.TryCreate(trimmed, UriKind.RelativeOrAbsolute, out var uri))
			{
				return false;
			}

			if (uri.IsAbsoluteUri)
			{
				return (policy == ExcelLinks.All || uri.Scheme is "http" or "https" or "mailto") && uri.AbsoluteUri.Length <= MaxTarget;
			}

			return policy == ExcelLinks.All && trimmed.Length <= MaxTarget;
		}
	}

	/// <summary>A written workbook's parts the checks read: its sheets, and its filters' hidden names.</summary>
	private sealed class Package
	{
		/// <summary>The sheets, in tab order.</summary>
		public List<PackageSheet> Sheets { get; } = [];

		/// <summary>The sheets, by their places from 0, that have a hidden <c>_xlnm._FilterDatabase</c> name.</summary>
		public HashSet<int> HiddenFilterNames { get; } = [];

		/// <summary>Reads a workbook's parts.</summary>
		/// <param name="workbook">The workbook's bytes.</param>
		/// <returns>The parts.</returns>
		public static Package Of(byte[] workbook)
		{
			XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
			XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
			using var archive = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
			XDocument Part(string name)
			{
				using var stream = archive.GetEntry(name)!.Open();

				return XDocument.Load(stream);
			}

			Dictionary<string, (string Type, string Target, bool External)> Relationships(string part)
			{
				var rels = part.Substring(0, part.LastIndexOf('/') + 1) + "_rels/" + part.Substring(part.LastIndexOf('/') + 1) + ".rels";
				if (archive.GetEntry(rels) is null)
				{
					return [];
				}

				return Part(rels).Root!.Elements().ToDictionary(
					e => (string)e.Attribute("Id")!,
					e => ((string)e.Attribute("Type")!, (string)e.Attribute("Target")!, (string?)e.Attribute("TargetMode") == "External"));
			}

			var package = new Package();
			var book = Part("xl/workbook.xml");
			var bookRels = Relationships("xl/workbook.xml");
			foreach (var name in book.Root!.Descendants(main + "definedName"))
			{
				if ((string)name.Attribute("name")! == "_xlnm._FilterDatabase" && (string?)name.Attribute("hidden") == "1")
				{
					package.HiddenFilterNames.Add((int)name.Attribute("localSheetId")!);
				}
			}

			var index = 0;
			foreach (var element in book.Root.Descendants(main + "sheet"))
			{
				var part = "xl/" + bookRels[(string)element.Attribute(r + "id")!].Target;
				var xml = Part(part).Root!;
				var rels = Relationships(part);
				var sheet = new PackageSheet((string)element.Attribute("name")!, index++);
				var pane = xml.Descendants(main + "pane").FirstOrDefault();
				sheet.Frozen = ((int?)pane?.Attribute("ySplit") ?? 0, (int?)pane?.Attribute("xSplit") ?? 0);
				sheet.AutoFilter = (string?)xml.Element(main + "autoFilter")?.Attribute("ref");
				sheet.Merged.AddRange(xml.Descendants(main + "mergeCell").Select(m => (string)m.Attribute("ref")!));
				foreach (var col in xml.Descendants(main + "col"))
				{
					sheet.Widths.Add((double?)col.Attribute("width") ?? 0);
					if ((string?)col.Attribute("hidden") == "1")
					{
						for (var c = (int)col.Attribute("min")!; c <= (int)col.Attribute("max")!; c++)
						{
							sheet.Hidden.Add(c);
						}
					}
				}

				foreach (var link in xml.Descendants(main + "hyperlink"))
				{
					var target = (string?)link.Attribute(r + "id") is { } id && rels.TryGetValue(id, out var rel) ? rel : default;
					sheet.Links.Add(((string)link.Attribute("ref")!, target.Target, target.External));
				}

				if (xml.Descendants(main + "tablePart").FirstOrDefault() is { } tablePart)
				{
					var tableXml = Part("xl/" + rels[(string)tablePart.Attribute(r + "id")!].Target.Replace("../", "")).Root!;
					sheet.Table = new PackageTable(
						(string)tableXml.Attribute("name")!,
						(string)tableXml.Attribute("displayName")!,
						(string)tableXml.Attribute("ref")!,
						(string?)tableXml.Element(main + "tableStyleInfo")?.Attribute("name"),
						[.. tableXml.Descendants(main + "tableColumn").Select(c => Decode((string)c.Attribute("name")!))]);
				}

				package.Sheets.Add(sheet);
			}

			return package;
		}

		/// <summary>Decodes the <c>_xHHHH_</c> escapes of an attribute's text, as Excel does.</summary>
		/// <param name="text">The text.</param>
		/// <returns>The text decoded.</returns>
		private static string Decode(string text) =>
			System.Text.RegularExpressions.Regex.Replace(text, "_x([0-9A-Fa-f]{4})_", m => ((char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString());
	}

	/// <summary>A written sheet's look, as its part has it.</summary>
	/// <param name="name">Its name.</param>
	/// <param name="index">Its place in the workbook, from 0.</param>
	private sealed class PackageSheet(string name, int index)
	{
		/// <summary>Its name.</summary>
		public string Name { get; } = name;

		/// <summary>Its place in the workbook, from 0.</summary>
		public int Index { get; } = index;

		/// <summary>The rows and columns frozen.</summary>
		public (int Rows, int Columns) Frozen { get; set; }

		/// <summary>Its filter's range, or <see langword="null"/>.</summary>
		public string? AutoFilter { get; set; }

		/// <summary>Its merged ranges, in order.</summary>
		public List<string> Merged { get; } = [];

		/// <summary>Its columns' widths.</summary>
		public List<double> Widths { get; } = [];

		/// <summary>Its hidden columns, from 1.</summary>
		public HashSet<int> Hidden { get; } = [];

		/// <summary>Its links: each one's cell, target and whether the target is external.</summary>
		public List<(string Cell, string? Target, bool External)> Links { get; } = [];

		/// <summary>Its table, or <see langword="null"/>.</summary>
		public PackageTable? Table { get; set; }
	}

	/// <summary>A written table.</summary>
	/// <param name="Name">Its name.</param>
	/// <param name="DisplayName">Its display name.</param>
	/// <param name="Reference">Its range.</param>
	/// <param name="Style">Its style, or <see langword="null"/>.</param>
	/// <param name="Columns">Its columns' names, decoded.</param>
	private sealed record PackageTable(string Name, string DisplayName, string Reference, string? Style, List<string> Columns);
}
