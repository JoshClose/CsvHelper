using System.Text;
using CsvHelper.Excel.Tests.Xml;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// A seeded fuzzer of segment mode: random sheets mixing rows with and without <c>r</c> (and first cells with and without
/// one), gaps, hidden rows and columns, markup between rows and in cells, extra elements, prefixes and malformed rows,
/// tags and values, read under random readings (areas, hidden cells, styles, empty rows kept), and cut into segments at
/// random row starts and at every one, scanned by one to three workers. Each read through segments gives the serial read's
/// outcome exactly: the records with their rows, kinds and styles, the error (type, message, location and line), and the
/// bytes charged toward the workbook's limit; and so does each under a budget that trips. A failure names its seed, which
/// replays it alone (<see cref="Generate"/>). Each test writes how its reads went: the segments begun and joined, and the
/// fallbacks by mark.
/// </summary>
/// <param name="output">Takes the counts.</param>
public class SegmentFuzzTests(ITestOutputHelper output)
{
	/// <summary>The cases each test reads.</summary>
	private const int Cases = 2000;

	/// <summary>The cases read under budgets that trip.</summary>
	private const int BudgetCases = 200;

	/// <summary>The shared strings the sheets' cells index.</summary>
	private static readonly SharedStrings Strings = Sheets.Strings("pear", "  spaced  ", new string('x', 90), "", "東京");

	/// <summary>What the fuzzer puts between rows and cells, and in values, when a case has markup.</summary>
	private static readonly string[] Markup = ["<!-- note -->", "<?pi between?>", "<!-- <row r=\"3\"> -->"];

	[Fact]
	public void ReadsRandomSheetsAsTheSerialReadDoes()
	{
		var counts = new Counts();
		for (var seed = 0; seed < Cases; seed++)
		{
			var fuzzCase = Generate(seed);
			var expected = Read(fuzzCase, fast: true, plan: null, workers: 1, allowance: null);
			foreach (var fast in new[] { true, false })
			{
				foreach (var plan in new[] { Plan.Random, Plan.EveryRow })
				{
					var workers = 1 + (seed % 3);
					var actual = Read(fuzzCase, fast, plan, workers, allowance: null, run: counts.Add);
					AssertSame(fuzzCase, $"fast paths {(fast ? "on" : "off")}, {plan} cuts, {workers} workers", expected, actual);
				}
			}

			// Through a 16-byte window the serial part reads ahead less, so the first segments have bytes no stream counted.
			var small = Read(fuzzCase, fast: true, plan: null, workers: 1, allowance: null, window: 16, charged: false);
			AssertSame(fuzzCase, "a 16-byte window", small, Read(fuzzCase, fast: true, Plan.Random, 2, allowance: null, window: 16, charged: false, run: counts.Add));
		}

		output.WriteLine(counts.ToString());

		// The cases begin segments, and join them, and fall back at each mark, often enough to test them.
		Assert.InRange(counts.Started, Cases, int.MaxValue);
		Assert.InRange(counts.Stitched, 20 * Cases, int.MaxValue);
		Assert.InRange(counts.Fallbacks.Count, 6, int.MaxValue);
	}

	[Fact]
	public void TripsTheBudgetAsTheSerialReadDoes()
	{
		var counts = new Counts();
		var tripped = 0;
		for (var seed = 0; seed < BudgetCases; seed++)
		{
			var fuzzCase = Generate(seed, malformed: false);
			var length = Encoding.UTF8.GetByteCount(fuzzCase.Xml);
			var total = Charged(fuzzCase);
			foreach (var allowance in new[] { length - 1, length + ((total - length) / 3), total - 2, total - 1, total })
			{
				var expected = Read(fuzzCase, fast: true, plan: null, workers: 1, allowance);
				tripped += expected.Contains("MaxUncompressedSize") ? 1 : 0;
				foreach (var plan in new[] { Plan.Random, Plan.EveryRow })
				{
					AssertSame(fuzzCase, $"{plan} cuts, a budget of {allowance} bytes", expected, Read(fuzzCase, fast: true, plan, 2, allowance, run: counts.Add));
				}
			}
		}

		output.WriteLine($"{tripped} serial reads tripped; through segments: {counts}");
		Assert.InRange(tripped, BudgetCases, int.MaxValue);
	}

	/// <summary>How a read through segments cuts them.</summary>
	private enum Plan
	{
		/// <summary>At the first row start past a random target, from 1 to 300 bytes, for each segment.</summary>
		Random,

		/// <summary>At every row start.</summary>
		EveryRow,
	}

	/// <summary>Fails with both outcomes and the case when they differ.</summary>
	/// <param name="fuzzCase">The case.</param>
	/// <param name="how">How the read through segments read it.</param>
	/// <param name="expected">The serial read's outcome.</param>
	/// <param name="actual">The read's outcome.</param>
	private static void AssertSame(FuzzCase fuzzCase, string how, string expected, string actual)
	{
		if (expected == actual)
		{
			return;
		}

		var expectedLines = expected.Split('\n');
		var actualLines = actual.Split('\n');
		var line = 0;
		while (line < expectedLines.Length && line < actualLines.Length && expectedLines[line] == actualLines[line])
		{
			line++;
		}

		throw new Xunit.Sdk.XunitException($"{fuzzCase}, {how}: the outcomes differ at line {line + 1}.\nThe serial read:\n{expected}\nThrough segments:\n{actual}\nThe part:\n{fuzzCase.Xml}");
	}

	/// <summary>The bytes a case's serial read charges with no limit in reach: its XML's and its extras.</summary>
	/// <param name="fuzzCase">The case.</param>
	/// <returns>The bytes.</returns>
	private static long Charged(FuzzCase fuzzCase)
	{
		var outcome = Read(fuzzCase, fast: true, plan: null, workers: 1, allowance: null);
		var at = outcome.LastIndexOf("charged ", StringComparison.Ordinal);

		return long.Parse(outcome.Substring(at + "charged ".Length).Trim());
	}

	/// <summary>
	/// Reads a case, serially or through segments, a record a fill, into a window of the given size with whole reads, under a
	/// budget; and describes the outcome: each record styled, the exception, and the bytes charged.
	/// </summary>
	/// <param name="fuzzCase">The case.</param>
	/// <param name="fast">Whether the fast paths (raw skips and fast cells) are on.</param>
	/// <param name="plan">How segments are cut; <see langword="null"/> for a serial read.</param>
	/// <param name="workers">The workers, which take the segments in turn.</param>
	/// <param name="allowance">How far past the guard's total the limit is; <see langword="null"/> for far past the sheet.</param>
	/// <param name="window">The window's starting size.</param>
	/// <param name="charged">Whether the outcome has the bytes charged.</param>
	/// <param name="run">Told of the read through segments, for the tests' counts.</param>
	/// <returns>The outcome.</returns>
	private static string Read(FuzzCase fuzzCase, bool fast, Plan? plan, int workers, long? allowance, int window = XmlWindow.DefaultSize, bool charged = true, Action<SegmentRun>? run = null)
	{
		var text = new StringBuilder();
		using var archive = SegmentReads.OpenBudget();
		var start = archive.Guard.Total;
		var scan = new ScanMode("fuzz", false, window, false, fast, fast);
		var captured = new Captured(1);
		try
		{
			using var sheet = Sheets.Open(fuzzCase.Xml, scan, fuzzCase.SkipEmptyRows, Strings, reading: fuzzCase.Reading, budget: SegmentReads.Budget(archive, allowance ?? SegmentReads.Allowance));
			if (plan is null)
			{
				var target = new CapturingTarget(captured);
				while (true)
				{
					captured.StartFill();
					var more = sheet.Fill(target);
					captured.EndFill(more);
					if (!more)
					{
						break;
					}
				}
			}
			else
			{
				var cuts = new Random(fuzzCase.Seed);
				var segments = new SegmentRun(
					sheet, Encoding.UTF8.GetBytes(fuzzCase.Xml), _ => plan == Plan.EveryRow ? 1 : cuts.Next(1, 301), workers, fuzzCase.BlockCap, fuzzCase.Minimum);
				try
				{
					segments.ReadInto(captured);
				}
				finally
				{
					run?.Invoke(segments);
				}
			}
		}
		catch (Exception e) when (e is CsvFormatException or InvalidDataException)
		{
			foreach (var record in captured.Records)
			{
				text.Append(record.Styled()).Append('\n');
			}

			var format = e as CsvFormatException;
			text.Append($"threw {e.GetType().Name}: {e.Message} at {format?.Location} line {format?.LineNumber}\n");
			if (charged)
			{
				text.Append($"charged {archive.Guard.Total - start}\n");
			}

			return text.ToString();
		}

		foreach (var record in captured.Records)
		{
			text.Append(record.Styled()).Append('\n');
		}

		if (charged)
		{
			text.Append($"charged {archive.Guard.Total - start}\n");
		}

		return text.ToString();
	}

	/// <summary>Generates a case: its sheet, how it is read, and how far the serial read goes before segments can begin.</summary>
	/// <param name="seed">The case's seed.</param>
	/// <param name="malformed">Whether some cases are malformed; <see langword="false"/> for none.</param>
	/// <returns>The case.</returns>
	private static FuzzCase Generate(int seed, bool malformed = true)
	{
		var random = new Random(unchecked((seed * 7919) + 104729));
		var prefix = random.Next(5) == 0 ? "x:" : "";
		var rowsWithR = random.Next(3);
		var cellsWithR = random.Next(3);
		var hides = random.Next(3) == 0;
		var marks = random.Next(5) == 0;
		var spaced = random.Next(2) == 0;
		var styled = random.Next(4) == 0;
		var rows = new StringBuilder();
		var row = 0L;
		var count = random.Next(3, 60);
		for (var i = 0; i < count; i++)
		{
			var rowR = rowsWithR == 0 || (rowsWithR == 2 && random.Next(2) == 0);
			var cellR = cellsWithR == 0 || (cellsWithR == 2 && random.Next(2) == 0);
			var cells = random.Next(4) == 0 ? 0 : random.Next(1, 7);

			// A row numbered by neither r follows the row before; one with an r can come after a gap.
			row += (rowR || (cellR && cells > 0)) && random.Next(4) == 0 ? random.Next(2, 6) : 1;
			if (marks && random.Next(6) == 0)
			{
				rows.Append(Markup[random.Next(Markup.Length)]);
			}

			if (random.Next(25) == 0)
			{
				rows.Append($"<{prefix}extLst><{prefix}row r=\"999\"/></{prefix}extLst>");
			}

			var attributes = new StringBuilder();
			if (rowR)
			{
				attributes.Append($" r=\"{row}\"");
			}

			if (random.Next(4) == 0)
			{
				attributes.Append(" spans=\"1:6\"");
			}

			if (hides && random.Next(5) == 0)
			{
				attributes.Append(random.Next(2) == 0 ? " hidden=\"1\"" : " hidden=\"true\"");
			}

			if (cells == 0 && random.Next(2) == 0)
			{
				rows.Append($"<{prefix}row{attributes}/>");
			}
			else
			{
				rows.Append($"<{prefix}row{attributes}>");
				var column = -1;
				for (var c = 0; c < cells; c++)
				{
					var withR = cellR || (c > 0 && random.Next(3) == 0);
					column += withR && random.Next(4) == 0 ? random.Next(2, 4) : 1;
					var reference = withR ? $" r=\"{CellReference.ColumnName(column)}{row}\"" : "";
					rows.Append(Cell(random, prefix, reference, styled, marks));
					if (spaced && random.Next(3) == 0)
					{
						rows.Append("\n  ");
					}
				}

				if (random.Next(20) == 0)
				{
					rows.Append($"<{prefix}extLst><{prefix}ext uri=\"x\"><{prefix}c r=\"ZZ1\"><{prefix}v>no</{prefix}v></{prefix}c></{prefix}ext></{prefix}extLst>");
				}

				rows.Append($"</{prefix}row>");
			}

			if (spaced)
			{
				rows.Append(random.Next(2) == 0 ? "\r\n" : "\n\t");
			}
		}

		var sheetData = rows.ToString();
		var mutation = "none";
		if (malformed && random.Next(6) == 0)
		{
			(sheetData, mutation) = Malform(random, sheetData, prefix);
		}

		var cols = random.Next(4) == 0 ? "<col min=\"2\" max=\"2\" width=\"0\"/><col min=\"4\" max=\"5\" width=\"9\" hidden=\"1\"/>" : "";
		var xml = prefix.Length == 0
			? Sheets.Worksheet("", cols, sheetData)
			: $"<x:worksheet xmlns:x=\"{TestPackage.MainNamespace}\">" + (cols.Length == 0 ? "" : $"<x:cols>{cols.Replace("<col", "<x:col")}</x:cols>") +
				$"<x:sheetData>{sheetData}</x:sheetData><x:pageMargins left=\"0.7\"/></x:worksheet>";
		if (mutation == "truncated")
		{
			xml = xml.Substring(0, xml.IndexOf("sheetData>" + sheetData, StringComparison.Ordinal) + "sheetData>".Length + (sheetData.Length * 2 / 3));
		}

		// White space kept from sheetData on, which every row inherits.
		if (random.Next(6) == 0)
		{
			xml = xml.Replace($"<{prefix}sheetData>", $"<{prefix}sheetData xml:space=\"preserve\">");
		}

		var (name, reading) = Reading(random, styled);
		// Not with the rows missing up to the sheet's last row, which a row near it would write a million of.
		var skipEmptyRows = random.Next(2) == 0 || mutation is "a row past the last" or "a cell past XFD";
		var minimum = random.Next(3) == 0 ? 0 : random.Next(64, 400);
		var blockCap = random.Next(8) == 0 ? random.Next(60, 400) : SegmentSizes.MaxBlock;

		return new FuzzCase(seed, $"{name}, {(skipEmptyRows ? "empty rows skipped" : "empty rows kept")}, {mutation}", xml, reading, skipEmptyRows, minimum, blockCap);
	}

	/// <summary>A random cell: a number, a shared string, text, an inline string, a boolean, an error, a date, or empty; with or without a style.</summary>
	/// <param name="random">The source of the choices.</param>
	/// <param name="prefix">The names' prefix.</param>
	/// <param name="reference">The cell's <c>r</c> attribute, or empty.</param>
	/// <param name="styled">Whether the cell may have a style.</param>
	/// <param name="marks">Whether its value may have markup.</param>
	/// <returns>The cell's XML.</returns>
	private static string Cell(Random random, string prefix, string reference, bool styled, bool marks)
	{
		var style = styled && random.Next(2) == 0 ? $" s=\"{random.Next(8)}\"" : "";
		string V(string value) => $"<{prefix}v>{value}</{prefix}v>";
		var inner = marks && random.Next(8) == 0 ? "1" + Markup[random.Next(2)] + "2" : random.Next(-50, 5000).ToString(System.Globalization.CultureInfo.InvariantCulture);

		return random.Next(10) switch
		{
			0 => $"<{prefix}c{reference}{style} t=\"s\">{V(random.Next(5).ToString(System.Globalization.CultureInfo.InvariantCulture))}</{prefix}c>",
			1 => $"<{prefix}c{reference}{style} t=\"str\">{V(random.Next(3) == 0 ? " a &amp; b " : "text_x0041_")}</{prefix}c>",
			2 => $"<{prefix}c{reference} t=\"inlineStr\"><{prefix}is><{prefix}t>inline {random.Next(9)}</{prefix}t></{prefix}is></{prefix}c>",
			3 => $"<{prefix}c{reference}{style} t=\"b\">{V(random.Next(2) == 0 ? "1" : "true")}</{prefix}c>",
			4 => $"<{prefix}c{reference} t=\"e\">{V("#DIV/0!")}</{prefix}c>",
			5 => $"<{prefix}c{reference}{style} t=\"d\">{V("2024-01-02T03:04:05")}</{prefix}c>",
			6 => $"<{prefix}c{reference}{style}/>",
			7 => $"<{prefix}c{reference}{style}><{prefix}f>A1+1</{prefix}f>{V("2")}</{prefix}c>",
			_ => $"<{prefix}c{reference}{style}>{V(inner)}</{prefix}c>",
		};
	}

	/// <summary>Breaks a sheet's rows in one way the serial read throws for, at a random row.</summary>
	/// <param name="random">The source of the choices.</param>
	/// <param name="rows">The rows' XML.</param>
	/// <param name="prefix">The names' prefix.</param>
	/// <returns>The broken rows, and what was done.</returns>
	private static (string Rows, string Mutation) Malform(Random random, string rows, string prefix)
	{
		var starts = new List<int>();
		for (var at = rows.IndexOf($"<{prefix}row", StringComparison.Ordinal); at >= 0; at = rows.IndexOf($"<{prefix}row", at + 1, StringComparison.Ordinal))
		{
			starts.Add(at);
		}

		var start = starts[random.Next(starts.Count)];
		var insert = $"<{prefix}row".Length;

		return random.Next(8) switch
		{
			0 => (rows.Insert(start, $"<{prefix}row r=\"1\"/>"), "a row out of order"),
			1 => (rows.Insert(start, $"<{prefix}row r=\"x\"/>"), "a row's r not a number"),
			2 => (rows.Insert(start + insert, " a=\"<\""), "a '<' in a row's attribute"),
			3 => (rows.Insert(start, $"<{prefix}row r=\"1048576\"/><{prefix}row/>"), "a row past the last"),
			4 => (rows.Insert(start, $"<{prefix}row><{prefix}c r=\"XFD1048000\"><{prefix}v>1</{prefix}v></{prefix}c><{prefix}c><{prefix}v>2</{prefix}v></{prefix}c></{prefix}row>"), "a cell past XFD"),
			5 => (rows.Insert(start, $"<{prefix}row><{prefix}c t=\"s\"><{prefix}v>99</{prefix}v></{prefix}c></{prefix}row>"), "a shared string past the table"),
			6 => (rows.Insert(start, $"<{prefix}row><{prefix}c t=\"str\"><{prefix}v>&nbsp;</{prefix}v></{prefix}c></{prefix}row>"), "an undefined entity"),
			_ => (rows, "truncated"),
		};
	}

	/// <summary>A random reading: by default, of hidden cells skipped, of an area, styled, of error cells as null.</summary>
	/// <param name="random">The source of the choices.</param>
	/// <param name="styled">Whether the sheet's cells have styles, for a styled reading.</param>
	/// <returns>The reading and its name.</returns>
	private static (string Name, CellReading Reading) Reading(Random random, bool styled)
	{
		switch (random.Next(8))
		{
			case 6:
			{
				// A held header sets the width before any row is read, so segments can begin above the area.
				var first = random.Next(2, 12);
				var last = random.Next(2) == 0 ? CellReference.MaxRow : random.Next(first, 80);
				var area = new SheetLayout(first, last, 0, random.Next(1, 6));

				return ($"area rows {first}-{last} with a held header", new CellReading(layout: area, held: HeldRecord.Names(["h1", "h2"], 0, area.LastColumn + 1, 0)));
			}
			case 0:
			case 1:
				return ("by default", CellReading.Default);
			case 2:
			{
				var rows = random.Next(2) == 0;
				var columns = random.Next(2) == 0;

				return ($"hidden rows {(rows ? "skipped" : "kept")}, columns {(columns ? "skipped" : "kept")}", CellReading.Hidden(rows, columns));
			}
			case 3:
			{
				var first = random.Next(3) == 0 ? 1 : random.Next(1, 8);
				var last = random.Next(2) == 0 ? CellReference.MaxRow : random.Next(first, 80);
				var area = new SheetLayout(first, last, random.Next(0, 2), random.Next(2, 8));

				return ($"area rows {first}-{last}", new CellReading(layout: area, skipHiddenRows: random.Next(3) == 0));
			}
			case 4 when styled:
			{
				var styles = StyledScannerTests.PlainStyles;

				return ("formatted", new CellReading(styles, formatter: new CellFormatter(styles, DisplayCulture.Invariant, false)));
			}
			case 5:
				return ("error cells null, 1904", new CellReading(date1904: true, errorCells: ExcelErrorCells.Null));
			default:
				return ("by default", CellReading.Default);
		}
	}

	/// <summary>How reads through segments went: the reads that began segments, the segments joined, and the fallbacks by why.</summary>
	private sealed class Counts
	{
		/// <summary>The reads that began segments.</summary>
		public int Started { get; private set; }

		/// <summary>The segments joined.</summary>
		public int Stitched { get; private set; }

		/// <summary>The fallbacks, by the segment's mark (<see cref="SegmentMark.None"/> for the stitcher's own).</summary>
		public SortedDictionary<SegmentMark, int> Fallbacks { get; } = [];

		/// <summary>Counts a read.</summary>
		/// <param name="run">The read.</param>
		public void Add(SegmentRun run)
		{
			Started += run.Started ? 1 : 0;
			Stitched += run.Stitched;
			if (run.FellBackAt is { } segment)
			{
				Fallbacks[segment.Result.Mark] = Fallbacks.TryGetValue(segment.Result.Mark, out var count) ? count + 1 : 1;
			}
		}

		/// <inheritdoc/>
		public override string ToString() =>
			$"{Started} reads began segments, {Stitched} segments joined; fallbacks: {string.Join(", ", Fallbacks.Select(f => $"{f.Key} {f.Value}"))}";
	}

	/// <summary>A generated case.</summary>
	/// <param name="seed">Its seed.</param>
	/// <param name="description">What it is.</param>
	/// <param name="xml">The part.</param>
	/// <param name="reading">How it is read.</param>
	/// <param name="skipEmptyRows">Whether rows without a value are dropped.</param>
	/// <param name="minimum">How far into the part the serial read goes before segments can begin.</param>
	/// <param name="blockCap">Each block's limit.</param>
	private sealed class FuzzCase(int seed, string description, string xml, CellReading reading, bool skipEmptyRows, long minimum, long blockCap)
	{
		/// <summary>Its seed.</summary>
		public int Seed => seed;

		/// <summary>The part.</summary>
		public string Xml => xml;

		/// <summary>How it is read.</summary>
		public CellReading Reading => reading;

		/// <summary>Whether rows without a value are dropped.</summary>
		public bool SkipEmptyRows => skipEmptyRows;

		/// <summary>How far into the part the serial read goes before segments can begin.</summary>
		public long Minimum => minimum;

		/// <summary>Each block's limit.</summary>
		public long BlockCap => blockCap;

		/// <inheritdoc/>
		public override string ToString() => $"Seed {seed} ({description}, segments from byte {minimum}, blocks of {blockCap})";
	}
}
