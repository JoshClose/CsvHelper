using System.Globalization;
using System.Text;
using CsvHelper.Excel.Tests.Api;
using CsvHelper.Excel.Tests.Package;
using CsvHelper.Excel.Tests.Xml;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// The sheet scanner's fast path (Phase 9's step 3) against its fallback, seeded and deterministic: generated sheets of
/// canonical cells (every type, with and without <c>r</c>, <c>s</c> and <c>t</c>, as <c>/&gt;</c>, <c>&gt;&lt;/c&gt;</c>, a
/// <c>v</c> or an <c>is</c>), in rows with and without <c>r</c>, some hidden, with a merged range from one of their cells,
/// each sheet with one mutation: a value with references, line ends, escapes or white space at its ends, or an empty one
/// (which the fast path takes), each kind of cell it declines, or each kind of content that can't be read; and, as the seed
/// says, <c>x:</c> on every element, white space, line ends, comments, processing instructions, CDATA, other elements and
/// text between cells and rows, a row end tag with white space, and <c>xml:space="preserve"</c> on the root, on
/// <c>sheetData</c> or on a row. Each is read with fast cells on and off, raw skips on both ways, under one reading in turn
/// (the whole sheet, areas, hidden rows and columns skipped, merged cells filled, a held record, <c>Formatted</c> with and
/// without formats that change text, error cells as null, formulas as text, alone and in an area whose rows above are read
/// for them), with empty rows kept or not, in the sync scan modes (whose one-byte reads split each cell at every byte of
/// the window) and one async mode in turn, with fills that take every record and fills that take one. Both reads give the
/// same outcome, byte for byte: each fill's records, each record's row, and each field's bytes, kind and style; and for a
/// sheet that can't be read, the same exception (type, message with its byte offset, location, line) after the same
/// records and fields. Under a workbook's budget small enough to trip mid-sheet, the same trip, after the same records,
/// with the same bytes charged. A failure names the case's seed, mutation, reading and mode, and shows both outcomes.
/// </summary>
public class FastCellFuzzTests
{
	/// <summary>The cases of <see cref="ReadsMutatedCanonicalCellsAsTheFallbackDoes"/>: each mutation, under four readings or so.</summary>
	private const int MutationCases = 240;

	/// <summary>The cases of <see cref="FailsAsTheFallbackDoes"/>: each malformed mutation, under four readings or so.</summary>
	private const int MalformedCases = 92;

	/// <summary>The cases of <see cref="TripsTheBudgetAsTheFallbackDoes"/>.</summary>
	private const int BudgetCases = 12;

	/// <summary>
	/// The shared strings: a text, an empty one, one with white space at its ends, one of 300 bytes (more than its cells'
	/// XML, which counts toward a budget), and one with a reference.
	/// </summary>
	private static readonly SharedStrings Strings = Sheets.Strings("pear", "", " padded ", new string('x', 300), "a&b");

	/// <summary>The styles of <see cref="FormattedTextTests.Formats"/>: dates, durations, and formats with text sections.</summary>
	private static readonly StyleTable TextStyles = FormattedTextTests.Book("").LoadStylesAsync(false).GetAwaiter().GetResult();

	/// <summary>The workbook whose budget the budget's reads charge: its parts are never read.</summary>
	private static readonly TestPackage BudgetPackage = TestPackage.Workbook("Data");

	/// <summary>The types a generated cell has: none, then each that the fast path takes.</summary>
	private static readonly string?[] Types = [null, "n", "s", "b", "e", "str", "inlineStr"];

	/// <summary>What fills the space between cells and rows, when the seed spaces them.</summary>
	private static readonly string[] Spaces = [" ", "\r\n", "\n\t\t", " \r\n ", "\t"];

	/// <summary>What the token loop reads between cells and rows, when the seed puts markup there.</summary>
	private static readonly string[] Markup = ["<!-- note -->", "<?pi between?>", "<extLst/>", "stray text", "<![CDATA[x]]>", "<extLst><x/></extLst>"];

	/// <summary>The mutations of a cell that the fast path takes: none, and its value or reference changed.</summary>
	private static readonly string[] Taken =
	[
		"none", "a reference in the value", "a line feed in the value", "a carriage return in the value", "a CRLF in the value", "an escape in the value",
		"white space around the value", "an empty value", "a lowercase reference",
	];

	/// <summary>The mutations of a cell into each kind of cell the fast path declines, as spec section 2.1 lists them.</summary>
	private static readonly string[] Declines =
	[
		"a formula", "t=\"d\"", "a type Excel doesn't have", "a type with white space", "a type with a reference", "a reference with white space",
		"a reference with a reference", "a style of 10 digits", "a style with white space", "xml:space on the cell", "cm", "vm", "ph",
		"a namespace declaration", "another attribute first", "another attribute last", "t before s", "s before r", "t before r", "single quotes",
		"white space around =", "white space before >", "two spaces before an attribute", "a tab between attributes", "a line feed between attributes",
		"an empty v", "xml:space on v", "another attribute on v", "CDATA in v", "a comment before v", "a comment in v", "a processing instruction in the cell",
		"an element after v", "white space after v", "white space in an empty cell", "runs", "two t", "a phonetic run", "an empty is", "xml:space on t",
		"is for another type", "an end tag with white space", "the cell's end tag with white space", "another prefix",
	];

	/// <summary>The mutations into content that can't be read: of a cell's value, reference or style, of its row, or of the part.</summary>
	private static readonly string[] Malformed =
	[
		"not a shared string index", "a negative shared string index", "a shared string index past 32 bits", "a shared string index past the table",
		"a reference that can't be decoded", "a reference to character 0", "a reference that isn't a cell's", "a style that isn't a number",
		"a style past 32 bits", "an empty style", "an attribute without a value", "a '<' in an attribute value", "a cell before the cell before it",
		"a cell past the last column", "rows out of order", "a row number past the last row", "a row without r before the row before it",
		"the part ends in a cell",
	];

	/// <summary>References that aren't a cell's, one for each column a generated cell can be in.</summary>
	private static readonly string[] BadReferences = ["A0", "XFE1", "1A", "", "A1048577", "AAAA1"];

	/// <summary>The readings, each with its name for failures.</summary>
	private static readonly (string Name, CellReading Reading)[] Readings =
	[
		("the whole sheet", new CellReading(TextStyles)),
		("B2:D5", new CellReading(TextStyles, layout: Area("B2:D5"))),
		("rows 3 on", new CellReading(TextStyles, layout: Area("A3:F12"))),
		("hidden rows skipped", new CellReading(TextStyles, skipHiddenRows: true)),
		("hidden columns skipped", new CellReading(TextStyles, skipHiddenColumns: true)),
		("hidden rows and columns skipped in B1:E12", new CellReading(TextStyles, layout: Area("B1:E12"), skipHiddenRows: true, skipHiddenColumns: true)),
		("merged cells filled", new CellReading(TextStyles, fillMergedCells: true)),
		("merged cells filled and hidden rows skipped in B2:F12", new CellReading(TextStyles, layout: Area("B2:F12"), skipHiddenRows: true, fillMergedCells: true)),
		("a held record over A2:F12", new CellReading(TextStyles, layout: Area("A2:F12"), held: HeldRecord.Names(["H1", "H2", "H3"], 0, 3, 1))),
		("formatted, with formats that change text", new CellReading(TextStyles, formatter: new CellFormatter(TextStyles, DisplayCulture.Invariant, false))),
		(
			"formatted, with formats that change no text",
			new CellReading(StyledScannerTests.PlainStyles, formatter: new CellFormatter(StyledScannerTests.PlainStyles, DisplayCulture.Invariant, false))
		),
		("error cells as null", new CellReading(TextStyles, errorCells: ExcelErrorCells.Null)),
		("formulas as text", new CellReading(TextStyles, formulas: ExcelFormulas.FormulaText)),
		("formulas as text in rows 3 on", new CellReading(TextStyles, formulas: ExcelFormulas.FormulaText, layout: Area("A3:F12"))),
		(
			"formatted with merged cells filled",
			new CellReading(TextStyles, fillMergedCells: true, formatter: new CellFormatter(TextStyles, DisplayCulture.Invariant, false))
		),
	];

	/// <summary>The readings of the budget's cases: those that charge for gaps, missing rows, fills and a held record.</summary>
	private static readonly (string Name, CellReading Reading)[] BudgetReadings =
	[
		("the whole sheet", new CellReading(TextStyles)),
		("merged cells filled", new CellReading(TextStyles, fillMergedCells: true)),
		("a held record over A2:F12", new CellReading(TextStyles, layout: Area("A2:F12"), held: HeldRecord.Names(["H1", "H2", "H3"], 0, 3, 1))),
		("hidden rows skipped", new CellReading(TextStyles, skipHiddenRows: true)),
	];

	/// <summary>The form of a cell's content.</summary>
	private enum CellForm
	{
		/// <summary><c>&lt;c/&gt;</c>.</summary>
		Empty,

		/// <summary><c>&lt;c&gt;&lt;/c&gt;</c>.</summary>
		Closed,

		/// <summary><c>&lt;c&gt;&lt;v&gt;V&lt;/v&gt;&lt;/c&gt;</c>.</summary>
		Value,

		/// <summary><c>&lt;c&gt;&lt;is&gt;&lt;t&gt;V&lt;/t&gt;&lt;/is&gt;&lt;/c&gt;</c>, of an inline string.</summary>
		Inline,
	}

	[Fact]
	public async Task ReadsMutatedCanonicalCellsAsTheFallbackDoes()
	{
		var mutations = Taken.Concat(Declines).ToArray();
		for (var i = 0; i < MutationCases; i++)
		{
			var fuzzCase = Generate(1000 + i, mutations[i % mutations.Length], Readings[i * 7 % Readings.Length]);
			foreach (var (scan, perFill) in ModesOf(i))
			{
				await AssertSameAsync(fuzzCase, scan, perFill, null);
			}
		}
	}

	[Fact]
	public async Task FailsAsTheFallbackDoes()
	{
		for (var i = 0; i < MalformedCases; i++)
		{
			var fuzzCase = Generate(5000 + i, Malformed[i % Malformed.Length], Readings[i * 7 % Readings.Length]);
			foreach (var (scan, perFill) in ModesOf(i))
			{
				await AssertSameAsync(fuzzCase, scan, perFill, null);
			}
		}
	}

	[Fact]
	public async Task TripsTheBudgetAsTheFallbackDoes()
	{
		// Each sheet's charges in full (its bytes, its gaps, missing rows and fills, and its shared strings beyond their
		// cells), measured with the fallback, then budgets that trip before the first byte, within the bytes, and within the
		// charges after them: the same trip both ways, after the same records, with the same bytes charged.
		for (var i = 0; i < BudgetCases; i++)
		{
			var mutation = i % 3 == 0 ? "none" : Declines[i * 5 % Declines.Length];
			var fuzzCase = Generate(9000 + i, mutation, BudgetReadings[i % BudgetReadings.Length], skipEmptyRows: i % 4 == 3);
			var length = Encoding.UTF8.GetByteCount(fuzzCase.Xml);
			foreach (var (scan, perFill) in ModesOf(i).Where(m => !(m.Scan.Async && m.Scan.Trickle)))
			{
				var (_, total) = await OutcomeAsync(fuzzCase, scan.WithFastCells(false), perFill, long.MaxValue / 4);
				var beyond = Math.Max(total - length, 0);
				var allowances = new[] { 0, total / 8, total / 3, total / 2, length, length + 1, length + (beyond / 3), length + (2 * beyond / 3), total - 1, total }
					.Where(a => a >= 0)
					.Distinct();
				foreach (var allowance in allowances)
				{
					await AssertSameAsync(fuzzCase, scan, perFill, allowance);
				}
			}
		}
	}

	[Fact]
	public async Task ChargesDecodedSharedIndexesAsTheFallbackDoes()
	{
		// Indexes the fast path hands to AddValue, which decodes them (a reference, a CR, an escape), of the 300-byte string:
		// each string's bytes beyond its cell count from the end of its v, as the fallback counts them, so that every budget
		// trips at the same cell, with the same bytes charged.
		var rows = string.Concat(Enumerable.Range(1, 8).Select(r =>
			$"<row r=\"{r}\"><c r=\"A{r}\" t=\"s\"><v>&#51;</v></c><c r=\"B{r}\" s=\"1\" t=\"s\"><v>3\r</v></c><c r=\"C{r}\" t=\"s\"><v>_x0033_</v></c><c r=\"D{r}\"><v>1</v></c></row>"));
		var toggles = new Toggles(false, false, false, false, false, false, 0);
		var fuzzCase = new FuzzCase(0, "decoded shared indexes", "whole", CellReading.Default, true, toggles, Sheets.Worksheet(rows));
		var length = Encoding.UTF8.GetByteCount(fuzzCase.Xml);
		foreach (var (scan, perFill) in ModesOf(0))
		{
			var (_, total) = await OutcomeAsync(fuzzCase, scan.WithFastCells(false), perFill, long.MaxValue / 4);
			Assert.True(total > length + 8 * 3 * 250, $"The strings' bytes beyond their cells are charged: {total} for {length} bytes of XML.");
			foreach (var allowance in new[] { length, length + 1, (length + total) / 2, total - 1, total })
			{
				await AssertSameAsync(fuzzCase, scan, perFill, allowance);
			}
		}
	}

	/// <summary>The area of a reference, such as <c>B2:D5</c>, as a layout.</summary>
	/// <param name="reference">The reference.</param>
	/// <returns>The layout.</returns>
	private static SheetLayout Area(string reference) => SheetLayout.Of(ExcelRange.Parse(reference));

	/// <summary>
	/// The modes a case reads in: the four sync scan modes (whole and one-byte reads, into a 64 KB and a 16-byte window), and
	/// one async mode in turn, its one-byte reads every fourth case, since each of their bytes awaits; fills that take every
	/// record or one, alternating.
	/// </summary>
	/// <param name="index">The case's index.</param>
	/// <returns>The scan modes, and the records a fill takes in each.</returns>
	private static IEnumerable<(ScanMode Scan, int PerFill)> ModesOf(int index)
	{
		foreach (var mode in new[] { 0, 2, 4, 6 })
		{
			yield return (ScanMode.All[mode], (mode / 2 + index) % 2 == 0 ? int.MaxValue : 1);
		}

		var async = (index % 8) switch
		{
			6 => 5,
			7 => 7,
			var k => k % 2 == 0 ? 1 : 3,
		};

		yield return (ScanMode.All[async], index / 8 % 2 == 0 ? int.MaxValue : 1);
	}

	/// <summary>Reads a case with fast cells on and off, and fails with both outcomes when they differ.</summary>
	/// <param name="fuzzCase">The case.</param>
	/// <param name="scan">The scan mode, with fast cells on.</param>
	/// <param name="perFill">The records a fill takes.</param>
	/// <param name="allowance">The budget's bytes, or <see langword="null"/> for none.</param>
	/// <returns>A task that completes when the outcomes are the same.</returns>
	private static async Task AssertSameAsync(FuzzCase fuzzCase, ScanMode scan, int perFill, long? allowance)
	{
		var (fast, _) = await OutcomeAsync(fuzzCase, scan.WithFastCells(true), perFill, allowance);
		var (fallback, _) = await OutcomeAsync(fuzzCase, scan.WithFastCells(false), perFill, allowance);
		if (fast == fallback)
		{
			return;
		}

		var fastLines = fast.Split('\n');
		var fallbackLines = fallback.Split('\n');
		var line = 0;
		while (line < fastLines.Length && line < fallbackLines.Length && fastLines[line] == fallbackLines[line])
		{
			line++;
		}

		var budget = allowance is null ? "" : $", a budget of {allowance} bytes";
		throw new Xunit.Sdk.XunitException(
			$"{fuzzCase}, in mode '{scan}', {(perFill == 1 ? "a record" : "every record")} a fill{budget}: the outcomes differ at line {line + 1}.\n" +
			$"Fast cells:\n{fast}\nThe fallback:\n{fallback}\nThe part:\n{fuzzCase.Xml}");
	}

	/// <summary>
	/// Reads a case to its end, or to what it throws, and describes the outcome: each fill's records and fields, and the
	/// exception; under a budget, the bytes charged.
	/// </summary>
	/// <param name="fuzzCase">The case.</param>
	/// <param name="scan">The scan mode, its fast cells included.</param>
	/// <param name="perFill">The records a fill takes.</param>
	/// <param name="allowance">The budget's bytes, or <see langword="null"/> for none.</param>
	/// <returns>A task whose result is the outcome, and the bytes charged (0 without a budget).</returns>
	private static async Task<(string Outcome, long Charged)> OutcomeAsync(FuzzCase fuzzCase, ScanMode scan, int perFill, long? allowance)
	{
		var outcome = new Outcome(perFill);
		PackageArchive? archive = null;
		try
		{
			Func<Stream, LimitedReadStream>? budget = null;
			var start = 0L;
			if (allowance is { } bytes)
			{
				var opened = await BudgetPackage.OpenAsync(false);
				archive = opened;
				start = opened.Guard.Total;
				var limit = start + bytes;
				budget = stream => new LimitedReadStream(stream, opened, Sheets.PartName, long.MaxValue, limit);
			}

			try
			{
				using var sheet = Sheets.Open(fuzzCase.Xml, scan, fuzzCase.SkipEmptyRows, Strings, reading: fuzzCase.Reading, budget: budget);
				var target = new ExactTarget(outcome);
				var more = true;
				while (more)
				{
					outcome.StartFill();
					more = scan.Async ? await sheet.FillAsync(target, CancellationToken.None) : sheet.Fill(target);
					outcome.EndFill(more);
				}
			}
			catch (Exception e) when (e is CsvFormatException or InvalidDataException)
			{
				outcome.Threw(e);
			}

			var charged = archive is null ? 0 : archive.Guard.Total - start;
			if (archive is not null)
			{
				outcome.Charged(charged);
			}

			return (outcome.ToString(), charged);
		}
		finally
		{
			archive?.Dispose();
		}
	}

	/// <summary>Generates a case: a sheet of canonical cells, the mutation of one of them or of the sheet, and how it reads.</summary>
	/// <param name="seed">The case's seed.</param>
	/// <param name="mutation">The mutation.</param>
	/// <param name="reading">The reading, and its name.</param>
	/// <param name="skipEmptyRows">Whether rows without a value are dropped; <see langword="null"/> for the seed to choose.</param>
	/// <returns>The case.</returns>
	private static FuzzCase Generate(int seed, string mutation, (string Name, CellReading Reading) reading, bool? skipEmptyRows = null)
	{
		var random = new Random(Mix(seed));
		var rows = new List<FuzzRow>();
		var previous = 0L;
		var count = random.Next(3, 6);
		for (var i = 0; i < count; i++)
		{
			// One row in four after a missing one.
			var number = previous + (random.Next(4) == 0 ? 2 : 1);
			var row = new FuzzRow(number, random.Next(4) != 0, random.Next(5) == 0);
			var last = -1;
			for (var column = 0; column < 6; column++)
			{
				if (random.Next(5) < 2 && (column < 5 || row.Cells.Count > 0))
				{
					continue;
				}

				// A cell without r is the column after the last: only that one can leave its r out.
				var hasReference = column != last + 1 || random.Next(3) != 0;
				row.Cells.Add(Canonical(random, column, hasReference));
				last = column;
			}

			// A row without r is numbered by its first cell's r, or as the row after the one before.
			if (!row.HasReference && !row.Cells[0].HasReference && number != previous + 1)
			{
				row.Cells[0].HasReference = true;
			}

			rows.Add(row);
			previous = number;
		}

		// The mutation's cell, and a merged range from a cell (the same one, one time in two), two by two.
		var subjectRow = random.Next(rows.Count);
		var subject = rows[subjectRow].Cells[random.Next(rows[subjectRow].Cells.Count)];
		var mergedRow = random.Next(2) == 0 ? subjectRow : random.Next(rows.Count);
		var topLeft = random.Next(2) == 0 && mergedRow == subjectRow ? subject : rows[mergedRow].Cells[random.Next(rows[mergedRow].Cells.Count)];
		var mergedNumber = rows[mergedRow].Number;
		var merge = $"{CellReference.ColumnName(topLeft.Column)}{mergedNumber}:{CellReference.ColumnName(Math.Min(topLeft.Column + 1, 5))}{mergedNumber + 1}";

		var toggles = new Toggles(
			random.Next(4) == 0, random.Next(3) == 0, random.Next(4) == 0, random.Next(6) == 0, random.Next(8) == 0, random.Next(8) == 0,
			random.Next(6) == 0 ? random.Next(rows.Count) : -1);
		var xml = Render(random, rows, subject, mutation, merge, toggles);

		return new FuzzCase(seed, mutation, reading.Name, reading.Reading, skipEmptyRows ?? random.Next(2) == 0, toggles, xml);
	}

	/// <summary>A canonical cell of a random type, style, form and value.</summary>
	/// <param name="random">The case's numbers.</param>
	/// <param name="column">The cell's column.</param>
	/// <param name="hasReference">Whether it has its <c>r</c>.</param>
	/// <returns>The cell.</returns>
	private static FuzzCell Canonical(Random random, int column, bool hasReference)
	{
		var type = Types[random.Next(Types.Length)];
		var style = random.Next(3) == 0 ? null : random.Next(0, 20).ToString(CultureInfo.InvariantCulture);
		var form = random.Next(8) switch
		{
			0 => CellForm.Empty,
			1 => CellForm.Closed,
			_ => type == "inlineStr" && random.Next(2) == 0 ? CellForm.Inline : CellForm.Value,
		};
		var values = ValuesOf(type);

		return new FuzzCell(column, hasReference, style, type, form, values[random.Next(values.Length)]);
	}

	/// <summary>The values a canonical cell of a type takes, as XML: plain, padded, empty, blank, and some the type reads as text.</summary>
	/// <param name="type">The cell's type.</param>
	/// <returns>The values.</returns>
	private static string[] ValuesOf(string? type) => type switch
	{
		"s" => ["0", "1", "2", "3", "4", " 2 ", "", "  "],
		"b" => ["1", "0", "true", "TRUE", "False", " 1 ", "yes", ""],
		"e" => ["#N/A", "#DIV/0!", " #REF! ", ""],
		"str" => ["text", "a b", " padded ", "", "x"],
		"inlineStr" => ["inline", " padded ", "", "two words"],
		_ => ["1", "-2.5", "1E+10", "0", " 3 ", "", "45306", "0.125", "abc", "1.5"],
	};

	/// <summary>
	/// Renders a sheet: its rows, the mutation of its cell, row or part, the spaces and markup its toggles put between cells
	/// and rows, its merged range, and its prefixes.
	/// </summary>
	/// <param name="random">The case's numbers, for what goes between cells and rows.</param>
	/// <param name="rows">The rows.</param>
	/// <param name="subject">The cell the mutation changes, if it changes a cell.</param>
	/// <param name="mutation">The mutation.</param>
	/// <param name="merge">The merged range's reference.</param>
	/// <param name="toggles">The sheet's spaces, markup, prefixes and <c>xml:space</c>.</param>
	/// <returns>The part's XML.</returns>
	private static string Render(Random random, List<FuzzRow> rows, FuzzCell subject, string mutation, string merge, Toggles toggles)
	{
		var root = toggles.RootSpace ? " xml:space=\"preserve\"" : "";
		var data = toggles.DataSpace ? " xml:space=\"preserve\"" : "";
		var text = new StringBuilder(TestPackage.Declaration);
		text.Append($"<worksheet xmlns=\"{TestPackage.MainNamespace}\" xmlns:y=\"{TestPackage.MainNamespace}\"{root}>");
		text.Append("<cols><col min=\"3\" max=\"3\" hidden=\"1\"/></cols>");
		text.Append($"<sheetData{data}>");
		var subjectStart = -1;
		var subjectEnd = -1;
		for (var i = 0; i < rows.Count; i++)
		{
			var row = rows[i];
			text.Append(Between(random, toggles));
			text.Append("<row");
			if (row.HasReference)
			{
				text.Append($" r=\"{row.Number}\"");
			}

			if (row.Hidden)
			{
				text.Append(" hidden=\"1\"");
			}

			if (toggles.SpacedRow == i)
			{
				text.Append(" xml:space=\"preserve\"");
			}

			text.Append('>');
			foreach (var cell in row.Cells)
			{
				text.Append(Between(random, toggles));
				if (cell == subject)
				{
					subjectStart = text.Length;
					text.Append(RenderCell(cell, row.Number, mutation));
					subjectEnd = text.Length;
				}
				else
				{
					text.Append(RenderCell(cell, row.Number, "none"));
				}
			}

			// A cell after the row's last, of a column before it, or past the last column.
			if (row.Cells.Contains(subject))
			{
				if (mutation == "a cell before the cell before it")
				{
					text.Append($"<c r=\"A{row.Number}\"><v>1</v></c>");
				}
				else if (mutation == "a cell past the last column")
				{
					text.Append($"<c r=\"XFD{row.Number}\"><v>1</v></c><c><v>2</v></c>");
				}
			}

			text.Append(Between(random, toggles));
			text.Append(toggles.RowEndSpace ? "</row >" : "</row>");
		}

		// A row after the last, of a number before it, past the sheet's last row, or numbered by a cell before it.
		var lastRow = rows[rows.Count - 1].Number;
		switch (mutation)
		{
			case "rows out of order":
				text.Append($"<row r=\"{rows[0].Number}\"><c><v>1</v></c></row>");

				break;
			case "a row number past the last row":
				text.Append($"<row r=\"{CellReference.MaxRow + 1}\"><c><v>1</v></c></row>");

				break;
			case "a row without r before the row before it":
				text.Append($"<row><c r=\"A{lastRow}\"><v>1</v></c></row>");

				break;
		}

		text.Append("</sheetData>");
		text.Append($"<mergeCells count=\"1\"><mergeCell ref=\"{merge}\"/></mergeCells></worksheet>");
		var xml = text.ToString();
		if (mutation == "the part ends in a cell")
		{
			xml = xml.Substring(0, subjectStart + Math.Max(1, (subjectEnd - subjectStart) / 2));
		}

		return toggles.Prefixed ? FastCellScannerTests.Prefixed(xml) : xml;
	}

	/// <summary>What goes between two cells or rows: nothing, or, as the toggles say, white space or markup.</summary>
	/// <param name="random">The case's numbers.</param>
	/// <param name="toggles">The sheet's toggles.</param>
	/// <returns>The XML.</returns>
	private static string Between(Random random, Toggles toggles)
	{
		if (toggles.Markup && random.Next(3) == 0)
		{
			return Markup[random.Next(Markup.Length)];
		}

		if (toggles.Spaced && random.Next(2) == 0)
		{
			return Spaces[random.Next(Spaces.Length)];
		}

		return "";
	}

	/// <summary>Renders a cell, canonical, or with a mutation.</summary>
	/// <param name="cell">The cell.</param>
	/// <param name="row">Its row's number, for its <c>r</c>.</param>
	/// <param name="mutation">The mutation, or <c>none</c>.</param>
	/// <returns>The cell's XML.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The mutation is none of a cell's.</exception>
	private static string RenderCell(FuzzCell cell, long row, string mutation)
	{
		var reference = CellReference.ColumnName(cell.Column) + row.ToString(CultureInfo.InvariantCulture);
		var forcedR = $" r=\"{reference}\"";
		var r = cell.HasReference ? forcedR : "";
		var forcedS = $" s=\"{cell.Style ?? "0"}\"";
		var s = cell.Style is null ? "" : forcedS;
		var forcedT = $" t=\"{cell.Type ?? "n"}\"";
		var t = cell.Type is null ? "" : forcedT;
		var attributes = r + s + t;
		var value = cell.Value;
		var content = cell.Form switch
		{
			CellForm.Empty => null,
			CellForm.Closed => "",
			CellForm.Inline => $"<is><t>{value}</t></is>",
			_ => $"<v>{value}</v>",
		};
		var body = content ?? "";
		if (mutation is "a reference in the value" or "a line feed in the value" or "a carriage return in the value" or "a CRLF in the value" or "an escape in the value"
			or "white space around the value")
		{
			// The value changed, in a v, or in the t of an inline string's is.
			var mutated = MutatedValue(cell.Type, value, mutation);

			return Whole(attributes, cell.Form == CellForm.Inline ? $"<is><t>{mutated}</t></is>" : $"<v>{mutated}</v>");
		}

		switch (mutation)
		{
			case "none":
				return Whole(attributes, content);
			case "an empty value":
				return Whole(attributes, cell.Form == CellForm.Inline ? "<is><t></t></is>" : "<v></v>");
			case "a lowercase reference":
				return Whole(forcedR.ToLowerInvariant() + s + t, content);
			case "a formula":
				return $"<c{attributes}><f>1+1</f>{body}</c>";
			case "t=\"d\"":
				return $"<c{r}{s} t=\"d\"><v>2024-01-02T03:04:05</v></c>";
			case "a type Excel doesn't have":
				return Whole(r + s + " t=\"x\"", content);
			case "a type with white space":
				return Whole(r + s + $" t=\" {cell.Type ?? "n"}\"", content);
			case "a type with a reference":
				return Whole(r + s + " t=\"&#110;\"", content);
			case "a reference with white space":
				return Whole($" r=\" {reference}\"" + s + t, content);
			case "a reference with a reference":
				return Whole($" r=\"&#{(int)reference[0]};{reference.Substring(1)}\"" + s + t, content);
			case "a style of 10 digits":
				return Whole(r + " s=\"1234567890\"" + t, content);
			case "a style with white space":
				return Whole(r + $" s=\" {cell.Style ?? "1"}\"" + t, content);
			case "xml:space on the cell":
				return Whole(attributes + " xml:space=\"preserve\"", content);
			case "cm":
				return Whole(attributes + " cm=\"1\"", content);
			case "vm":
				return Whole(attributes + " vm=\"1\"", content);
			case "ph":
				return Whole(attributes + " ph=\"1\"", content);
			case "a namespace declaration":
				return Whole(attributes + " xmlns:z=\"urn:z\"", content);
			case "another attribute first":
				return Whole(" note=\"1\"" + attributes, content);
			case "another attribute last":
				return Whole(attributes + " note=\"1\"", content);
			case "t before s":
				return Whole(r + forcedT + forcedS, content);
			case "s before r":
				return Whole(forcedS + forcedR + t, content);
			case "t before r":
				return Whole(forcedT + forcedR + s, content);
			case "single quotes":
				return Whole((forcedR + s + t).Replace('"', '\''), content);
			case "white space around =":
				return Whole($" r = \"{reference}\"" + s + t, content);
			case "white space before >":
				return content is null ? $"<c{attributes} />" : $"<c{attributes} >{content}</c>";
			case "two spaces before an attribute":
				return Whole(" " + forcedR + s + t, content);
			case "a tab between attributes":
				return Whole(forcedR + "\t" + forcedS.Substring(1) + t, content);
			case "a line feed between attributes":
				return Whole(forcedR + "\n" + forcedS.Substring(1) + t, content);
			case "an empty v":
				return $"<c{attributes}><v/></c>";
			case "xml:space on v":
				return $"<c{attributes}><v xml:space=\"preserve\">{value}</v></c>";
			case "another attribute on v":
				return $"<c{attributes}><v note=\"1\">{value}</v></c>";
			case "CDATA in v":
				return $"<c{attributes}><v><![CDATA[{value}]]></v></c>";
			case "a comment before v":
				return $"<c{attributes}><!-- c -->{body}</c>";
			case "a comment in v":
				return $"<c{attributes}><v>{value}<!-- c --></v></c>";
			case "a processing instruction in the cell":
				return $"<c{attributes}><?pi x?>{body}</c>";
			case "an element after v":
				return $"<c{attributes}>{body}<extLst/></c>";
			case "white space after v":
				return $"<c{attributes}>{body} </c>";
			case "white space in an empty cell":
				return $"<c{attributes}> </c>";
			case "runs":
				return $"<c{r}{s} t=\"inlineStr\"><is><r><t>a</t></r><r><t>{value}</t></r></is></c>";
			case "two t":
				return $"<c{r}{s} t=\"inlineStr\"><is><t>a</t><t>{value}</t></is></c>";
			case "a phonetic run":
				return $"<c{r}{s} t=\"inlineStr\"><is><t>{value}</t><rPh sb=\"0\" eb=\"1\"><t>x</t></rPh></is></c>";
			case "an empty is":
				return $"<c{r}{s} t=\"inlineStr\"><is/></c>";
			case "xml:space on t":
				return $"<c{r}{s} t=\"inlineStr\"><is><t xml:space=\"preserve\">{value}</t></is></c>";
			case "is for another type":
				return $"<c{r}{s} t=\"str\"><is><t>{value}</t></is></c>";
			case "an end tag with white space":
				return $"<c{attributes}><v>{value}</v ></c>";
			case "the cell's end tag with white space":
				return $"<c{attributes}>{body}</c >";
			case "another prefix":
				return OtherPrefix(attributes, cell.Form, value);
			case "not a shared string index":
				return $"<c{r}{s} t=\"s\"><v>x1</v></c>";
			case "a negative shared string index":
				return $"<c{r}{s} t=\"s\"><v>-1</v></c>";
			case "a shared string index past 32 bits":
				return $"<c{r}{s} t=\"s\"><v>12345678901</v></c>";
			case "a shared string index past the table":
				return $"<c{r}{s} t=\"s\"><v>{(cell.Column % 2 == 0 ? "5" : " 99 ")}</v></c>";
			case "a reference that can't be decoded":
				return $"<c{attributes}><v>a&bogus;b</v></c>";
			case "a reference to character 0":
				return $"<c{attributes}><v>&#0;</v></c>";
			case "a reference that isn't a cell's":
				return Whole($" r=\"{BadReferences[cell.Column]}\"" + s + t, content);
			case "a style that isn't a number":
				return Whole(r + " s=\"x\"" + t, content);
			case "a style past 32 bits":
				return Whole(r + " s=\"99999999999\"" + t, content);
			case "an empty style":
				return Whole(r + " s=\"\"" + t, content);
			case "an attribute without a value":
				return Whole(attributes + " note", content);
			case "a '<' in an attribute value":
				return Whole(attributes + " note=\"a<b\"", content);
			case "a cell before the cell before it":
			case "a cell past the last column":
			case "rows out of order":
			case "a row number past the last row":
			case "a row without r before the row before it":
			case "the part ends in a cell":
				// Mutations of the row or the part, whose cell is canonical.
				return Whole(attributes, content);
			default:
				throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "No such mutation.");
		}
	}

	/// <summary>A cell, empty or with its content.</summary>
	/// <param name="attributes">Its attributes, each with the space before it.</param>
	/// <param name="content">Its content, or <see langword="null"/> for an empty element.</param>
	/// <returns>The cell's XML.</returns>
	private static string Whole(string attributes, string? content) => content is null ? $"<c{attributes}/>" : $"<c{attributes}>{content}</c>";

	/// <summary>A cell whose elements have the prefix <c>y</c>, which the root declares, rather than <c>sheetData</c>'s.</summary>
	/// <param name="attributes">Its attributes.</param>
	/// <param name="form">The form of its content.</param>
	/// <param name="value">Its value.</param>
	/// <returns>The cell's XML.</returns>
	private static string OtherPrefix(string attributes, CellForm form, string value) => form switch
	{
		CellForm.Empty => $"<y:c{attributes}/>",
		CellForm.Closed => $"<y:c{attributes}></y:c>",
		CellForm.Inline => $"<y:c{attributes}><y:is><y:t>{value}</y:t></y:is></y:c>",
		_ => $"<y:c{attributes}><y:v>{value}</y:v></y:c>",
	};

	/// <summary>
	/// A cell's value with a mutation the fast path takes: references, line ends, escapes or white space around it. A shared
	/// string's index stays an index, so that the cell reads.
	/// </summary>
	/// <param name="type">The cell's type.</param>
	/// <param name="value">Its value, as XML.</param>
	/// <param name="mutation">The mutation.</param>
	/// <returns>The value, as XML.</returns>
	private static string MutatedValue(string? type, string value, string mutation)
	{
		var index = type == "s";

		return mutation switch
		{
			"a reference in the value" => index ? "&#49;" : value + "&amp;&#233;&#x41;",
			"a line feed in the value" => index ? "&#10;2" : value + "&#10;x",
			"a carriage return in the value" => index ? "\r3" : value + "\rx",
			"a CRLF in the value" => index ? "\r\n4" : value + "\r\nx",
			"an escape in the value" => index ? "_x0032_" : value + "_x000D__x0041_",
			_ => index ? " \t0\r\n " : " \t" + value + "\r\n ",
		};
	}

	/// <summary>
	/// A seed for a case's numbers, its bits mixed (MurmurHash3's finalizer, as <c>FuzzTests</c> mixes its seeds), so that
	/// cases near each other don't start alike: <see cref="Random"/> gives seeds near each other nearly the same first numbers.
	/// </summary>
	/// <param name="seed">The case's seed.</param>
	/// <returns>The numbers' seed.</returns>
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

	/// <summary>A generated cell.</summary>
	/// <param name="column">Its column, from 0.</param>
	/// <param name="hasReference">Whether it has its <c>r</c>.</param>
	/// <param name="style">Its <c>s</c>, or <see langword="null"/> for none.</param>
	/// <param name="type">Its <c>t</c>, or <see langword="null"/> for none.</param>
	/// <param name="form">The form of its content.</param>
	/// <param name="value">Its value, as XML.</param>
	private sealed class FuzzCell(int column, bool hasReference, string? style, string? type, CellForm form, string value)
	{
		/// <summary>Its column, from 0.</summary>
		public int Column { get; } = column;

		/// <summary>Whether it has its <c>r</c>: a row without one gives its first cell one when its number needs it.</summary>
		public bool HasReference { get; set; } = hasReference;

		/// <summary>Its <c>s</c>, or <see langword="null"/> for none.</summary>
		public string? Style { get; } = style;

		/// <summary>Its <c>t</c>, or <see langword="null"/> for none.</summary>
		public string? Type { get; } = type;

		/// <summary>The form of its content.</summary>
		public CellForm Form { get; } = form;

		/// <summary>Its value, as XML.</summary>
		public string Value { get; } = value;
	}

	/// <summary>A generated row.</summary>
	/// <param name="number">Its number.</param>
	/// <param name="hasReference">Whether it has its <c>r</c>.</param>
	/// <param name="hidden">Whether it is hidden.</param>
	private sealed class FuzzRow(long number, bool hasReference, bool hidden)
	{
		/// <summary>Its number.</summary>
		public long Number { get; } = number;

		/// <summary>Whether it has its <c>r</c>.</summary>
		public bool HasReference { get; } = hasReference;

		/// <summary>Whether it is hidden.</summary>
		public bool Hidden { get; } = hidden;

		/// <summary>Its cells, in column order.</summary>
		public List<FuzzCell> Cells { get; } = [];
	}

	/// <summary>What a sheet's toggles add to its canonical cells, each of which the fast path takes.</summary>
	/// <param name="prefixed">Whether every element has the prefix <c>x:</c>.</param>
	/// <param name="spaced">Whether white space and line ends go between some cells and rows.</param>
	/// <param name="markup">Whether comments, processing instructions, CDATA, other elements and text go between some cells and rows.</param>
	/// <param name="rowEndSpace">Whether the rows' end tags have white space.</param>
	/// <param name="rootSpace">Whether the root has <c>xml:space="preserve"</c>.</param>
	/// <param name="dataSpace">Whether <c>sheetData</c> has <c>xml:space="preserve"</c>.</param>
	/// <param name="spacedRow">The row with <c>xml:space="preserve"</c>, or -1 for none.</param>
	private sealed class Toggles(bool prefixed, bool spaced, bool markup, bool rowEndSpace, bool rootSpace, bool dataSpace, int spacedRow)
	{
		/// <summary>Whether every element has the prefix <c>x:</c>.</summary>
		public bool Prefixed { get; } = prefixed;

		/// <summary>Whether white space and line ends go between some cells and rows.</summary>
		public bool Spaced { get; } = spaced;

		/// <summary>Whether comments, processing instructions, CDATA, other elements and text go between some cells and rows.</summary>
		public bool Markup { get; } = markup;

		/// <summary>Whether the rows' end tags have white space.</summary>
		public bool RowEndSpace { get; } = rowEndSpace;

		/// <summary>Whether the root has <c>xml:space="preserve"</c>.</summary>
		public bool RootSpace { get; } = rootSpace;

		/// <summary>Whether <c>sheetData</c> has <c>xml:space="preserve"</c>.</summary>
		public bool DataSpace { get; } = dataSpace;

		/// <summary>The row with <c>xml:space="preserve"</c>, or -1 for none.</summary>
		public int SpacedRow { get; } = spacedRow;

		/// <inheritdoc/>
		public override string ToString()
		{
			var on = new List<string>();
			if (Prefixed)
			{
				on.Add("prefixed");
			}

			if (Spaced)
			{
				on.Add("spaced");
			}

			if (Markup)
			{
				on.Add("markup between");
			}

			if (RowEndSpace)
			{
				on.Add("</row >");
			}

			if (RootSpace)
			{
				on.Add("xml:space on the root");
			}

			if (DataSpace)
			{
				on.Add("xml:space on sheetData");
			}

			if (SpacedRow >= 0)
			{
				on.Add($"xml:space on row {SpacedRow + 1} of the file's");
			}

			return on.Count == 0 ? "no toggles" : string.Join(", ", on);
		}
	}

	/// <summary>A case: its sheet, and how it reads.</summary>
	/// <param name="seed">Its seed.</param>
	/// <param name="mutation">Its mutation.</param>
	/// <param name="readingName">Its reading's name.</param>
	/// <param name="reading">Its reading.</param>
	/// <param name="skipEmptyRows">Whether rows without a value are dropped.</param>
	/// <param name="toggles">Its sheet's toggles.</param>
	/// <param name="xml">Its part.</param>
	private sealed class FuzzCase(int seed, string mutation, string readingName, CellReading reading, bool skipEmptyRows, Toggles toggles, string xml)
	{
		/// <summary>Its reading.</summary>
		public CellReading Reading { get; } = reading;

		/// <summary>Whether rows without a value are dropped.</summary>
		public bool SkipEmptyRows { get; } = skipEmptyRows;

		/// <summary>Its part.</summary>
		public string Xml { get; } = xml;

		/// <inheritdoc/>
		public override string ToString() =>
			$"Seed {seed}, {mutation}, {readingName}, empty rows {(SkipEmptyRows ? "skipped" : "kept")}, {toggles}";
	}

	/// <summary>
	/// The outcome of a read, as text to compare: each fill, each record's fields (kind, style and bytes, the bytes that
	/// aren't printable ASCII as <c>\xHH</c>) and row, and what it threw. It checks the target's rules as
	/// <see cref="Captured"/> does: no call between a field's room and its commit, no value longer than its room, whole
	/// records in each fill, and no more records in a fill than it takes.
	/// </summary>
	/// <param name="perFill">The records after which the target is full.</param>
	private sealed class Outcome(int perFill)
	{
		private readonly StringBuilder text = new();
		private byte[]? room;
		private int recordsThisFill;
		private int fieldsThisRecord;

		/// <summary>Whether the fill has written as many records as it takes.</summary>
		public bool IsFull => recordsThisFill >= perFill;

		/// <summary>Starts a fill.</summary>
		public void StartFill()
		{
			recordsThisFill = 0;
			text.Append("fill\n");
		}

		/// <summary>Checks a fill that returned: no field open, a record unended, at least one record when more are to come, and no more than it takes.</summary>
		/// <param name="more">What the fill returned.</param>
		public void EndFill(bool more)
		{
			Assert.True(room is null && fieldsThisRecord == 0, "A fill left a record unended.");
			Assert.True(recordsThisFill <= perFill, $"A fill wrote {recordsThisFill} records, past the {perFill} it takes.");
			if (more)
			{
				Assert.True(recordsThisFill > 0, "A fill that said more records are to come wrote none.");
			}
		}

		/// <summary>Adds a field.</summary>
		/// <param name="utf8">The value.</param>
		/// <param name="kind">The kind.</param>
		/// <param name="style">The style, or -1 for none.</param>
		public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind, int style)
		{
			Assert.True(room is null, "A call between GetFieldSpan and CommitField.");
			Field(utf8, kind, style);
		}

		/// <summary>Gives exactly the room asked for, so that a value longer than its hint fails.</summary>
		/// <param name="sizeHint">The room asked for.</param>
		/// <returns>The room.</returns>
		public Span<byte> GetFieldSpan(int sizeHint)
		{
			Assert.True(room is null, "A call between GetFieldSpan and CommitField.");
			Assert.True(sizeHint >= 0);
			room = new byte[sizeHint];

			return room;
		}

		/// <summary>Adds the field written into the room.</summary>
		/// <param name="length">The value's length.</param>
		/// <param name="kind">The kind.</param>
		/// <param name="style">The style, or -1 for none.</param>
		public void CommitField(int length, SheetCellKind kind, int style)
		{
			Assert.True(room is not null, "CommitField without GetFieldSpan.");
			var written = room!;
			Assert.InRange(length, 0, written.Length);
			room = null;
			Field(written.AsSpan(0, length), kind, style);
		}

		/// <summary>Ends the record.</summary>
		/// <param name="row">Its row.</param>
		public void EndRecord(long row)
		{
			Assert.True(room is null, "A call between GetFieldSpan and CommitField.");
			text.Append(fieldsThisRecord == 0 ? "  (no fields)" : "").Append(" -> row ").Append(row).Append('\n');
			fieldsThisRecord = 0;
			recordsThisFill++;
		}

		/// <summary>Notes what the read threw, after the fields of a record it left unended, if any.</summary>
		/// <param name="e">The exception.</param>
		public void Threw(Exception e)
		{
			if (fieldsThisRecord > 0)
			{
				text.Append(" (unended)\n");
			}

			var location = e is CsvFormatException format ? $" | location {format.Location ?? "none"} | line {format.LineNumber}" : "";
			text.Append($"threw {e.GetType().Name}: {e.Message}{location}\n");
		}

		/// <summary>Notes the bytes the budget was charged.</summary>
		/// <param name="bytes">The bytes.</param>
		public void Charged(long bytes) => text.Append($"charged {bytes}\n");

		/// <inheritdoc/>
		public override string ToString() => text.ToString();

		/// <summary>Writes a field: <c>Kind@style:bytes</c>, after a separator from the record's field before.</summary>
		/// <param name="utf8">The value.</param>
		/// <param name="kind">The kind.</param>
		/// <param name="style">The style, or -1 for none.</param>
		private void Field(ReadOnlySpan<byte> utf8, SheetCellKind kind, int style)
		{
			text.Append(fieldsThisRecord++ == 0 ? "  " : " | ").Append(kind);
			if (style >= 0)
			{
				text.Append('@').Append(style);
			}

			text.Append(':');
			foreach (var b in utf8)
			{
				if (b >= 0x20 && b < 0x7F && b != (byte)'\\' && b != (byte)'|')
				{
					text.Append((char)b);
				}
				else
				{
					text.Append("\\x").Append(b.ToString("X2", CultureInfo.InvariantCulture));
				}
			}
		}
	}

	/// <summary>The <see cref="IRecordTarget"/> of the fuzzer: a struct over <see cref="Outcome"/>, as the reader's is over its batch.</summary>
	/// <param name="outcome">What it writes the records into.</param>
	private readonly struct ExactTarget(Outcome outcome) : IRecordTarget
	{
		/// <inheritdoc/>
		public bool IsFull => outcome.IsFull;

		/// <inheritdoc/>
		public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind) => outcome.AddField(utf8, kind, -1);

		/// <inheritdoc/>
		public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind, int style)
		{
			Assert.True(style >= 0, "A field with a style of -1 given as styled.");
			outcome.AddField(utf8, kind, style);
		}

		/// <inheritdoc/>
		public Span<byte> GetFieldSpan(int sizeHint) => outcome.GetFieldSpan(sizeHint);

		/// <inheritdoc/>
		public void CommitField(int length, SheetCellKind kind) => outcome.CommitField(length, kind, -1);

		/// <inheritdoc/>
		public void CommitField(int length, SheetCellKind kind, int style)
		{
			Assert.True(style >= 0, "A field with a style of -1 given as styled.");
			outcome.CommitField(length, kind, style);
		}

		/// <inheritdoc/>
		public void EndRecord(long row) => outcome.EndRecord(row);
	}
}
