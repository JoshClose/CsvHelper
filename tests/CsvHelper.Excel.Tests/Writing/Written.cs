using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ClosedXML.Excel;
using CsvHelper.Excel.Tests.Package;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using ExcelDataReader;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>A record of text, written with its generated record writer.</summary>
[CsvRecord]
public partial class WrittenText
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A text cell, or no cell when null.</summary>
	public string? Note { get; set; }
}

/// <summary>A row of a written sheet at its place (<see cref="Written.Rows(XDocument)"/>).</summary>
/// <param name="Number">The row's number, from 1.</param>
/// <param name="Element">Its <c>row</c> element.</param>
/// <param name="Cells">Its cells, at their places, in order.</param>
internal sealed record WrittenRow(long Number, XElement Element, List<WrittenCell> Cells);

/// <summary>A cell of a written sheet at its place (<see cref="Written.Rows(XDocument)"/>).</summary>
/// <param name="Row">The cell's row, from 1.</param>
/// <param name="Column">Its column, from 0.</param>
/// <param name="Element">Its <c>c</c> element.</param>
internal sealed record WrittenCell(long Row, int Column, XElement Element)
{
	/// <summary>The cell's reference, such as <c>B4</c>.</summary>
	public string Reference => CellReference.ColumnName(Column) + Row.ToString(CultureInfo.InvariantCulture);

	/// <summary>The cell format it names, 0 without <c>s</c>.</summary>
	public int Style => (int?)Element.Attribute("s") ?? 0;

	/// <summary>
	/// Whether the cell holds nothing: a gap's placeholder (<c>&lt;c/&gt;</c>, <c>&lt;c s="5"/&gt;</c>) or a protected
	/// header's empty field, which only give their cell a style.
	/// </summary>
	public bool IsEmpty => !Element.HasElements;
}

/// <summary>
/// Workbooks written by <see cref="ExcelWorkbookWriter"/> for the writer's tests, synchronously or asynchronously, each
/// package's zip checked as it comes (<see cref="Checked(byte[])"/>), and the readers they are read back with:
/// CsvHelper.Excel's own, and the oracles (the Open XML SDK's validator, ClosedXML and ExcelDataReader); and their sheets'
/// rows and cells found at their places, with or without <c>r</c> (<see cref="Rows(XDocument)"/>).
/// </summary>
internal static class Written
{
	/// <summary>The options of a reader that reads every row as a record, the header too, empty rows included.</summary>
	public static readonly ExcelReaderOptions AllRows = new() { Csv = new CsvReaderOptions { HasHeader = false, SkipEmptyLines = false } };

	/// <summary>
	/// Phase 4's look: no bold or frozen header and no auto-fit, for the tests of cells, whose XML they compare, so that
	/// Phase 5's defaults leave it as it was.
	/// </summary>
	public static readonly ExcelSheetOptions Phase4Look = new() { BoldHeader = false, FreezeHeader = false, AutoFit = 0 };

	/// <summary>SpreadsheetML's namespace.</summary>
	private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

	/// <summary>A row's element in <c>sheetData</c>'s text: empty, or up to its end tag, which no text holds unescaped.</summary>
	private static readonly Regex RowElement = new("<row(?=[\\s/>])[^>]*?(?:/>|>.*?</row>)", RegexOptions.Singleline | RegexOptions.CultureInvariant);

	/// <summary>A cell's element in a row's text: empty, or up to its end tag.</summary>
	private static readonly Regex CellElement = new("<c(?=[\\s/>])[^>]*?(?:/>|>.*?</c>)", RegexOptions.Singleline | RegexOptions.CultureInvariant);

	/// <summary>The <c>r</c> attribute of a row's or a cell's start tag.</summary>
	private static readonly Regex Reference = new("(<(?:row|c)(?=[\\s/>])[^>]*?) r=\"[^\"]*\"", RegexOptions.CultureInvariant);

	/// <summary>Options with Phase 4's look (<see cref="Phase4Look"/>), and no links, so that a URI is the text it was.</summary>
	/// <param name="options">The options, or <see langword="null"/> for the default ones.</param>
	/// <returns>The options with <see cref="ExcelWriterOptions.Sheets"/> set to Phase 4's look, and <see cref="ExcelWriterOptions.Links"/> to none.</returns>
	public static ExcelWriterOptions Phase4(ExcelWriterOptions? options = null) =>
		(options ?? ExcelWriterOptions.Default) with { Sheets = Phase4Look, Links = ExcelLinks.None };

	/// <summary>Options whose sheets' records may have different numbers of fields, with strings as given.</summary>
	/// <param name="strings">Where text cells keep their strings.</param>
	/// <returns>The options.</returns>
	public static ExcelWriterOptions Ragged(ExcelStrings strings = ExcelStrings.Inline) =>
		new() { Strings = strings, Csv = new CsvWriterOptions { ValidateFieldCount = false } };

	/// <summary>Registers the code pages ExcelDataReader needs on .NET, which has only UTF encodings built in.</summary>
	static Written()
	{
#if NET
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
	}

	/// <summary>Writes a workbook into memory, disposes the writer as the mode says, and checks the package's zip (<see cref="Checked(byte[])"/>).</summary>
	/// <param name="write">Writes the sheets; its flag says whether to write asynchronously.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <returns>The workbook's bytes.</returns>
	public static async Task<byte[]> WorkbookAsync(Func<ExcelWorkbookWriter, bool, Task> write, bool async, ExcelWriterOptions? options = null)
	{
		using var stream = new MemoryStream();
		var workbook = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true);
		await write(workbook, async);
		if (async)
		{
			await workbook.DisposeAsync();
		}
		else
		{
			workbook.Dispose();
		}

		return Checked(stream.ToArray());
	}

	/// <summary>
	/// Checks a package that CsvHelper.Excel wrote, before a test reads it: every header, descriptor and record of its zip
	/// against the writer's rules (<see cref="ZipStructure.AssertValid(byte[])"/>), and on .NET Framework, every part read
	/// through by WindowsBase's packaging (<see cref="ZipStructure.AssertPackagingReads(byte[])"/>).
	/// </summary>
	/// <param name="package">The package's bytes, the zip from its first byte.</param>
	/// <returns><paramref name="package"/>.</returns>
	public static byte[] Checked(byte[] package)
	{
		ZipStructure.AssertValid(package);
		ZipStructure.AssertPackagingReads(package);

		return package;
	}

	/// <summary>Checks a package file that CsvHelper.Excel wrote, as <see cref="Checked(byte[])"/> checks a package's bytes.</summary>
	/// <param name="path">The file's path.</param>
	/// <returns><paramref name="path"/>.</returns>
	public static string CheckedFile(string path)
	{
		using (var file = File.OpenRead(path))
		{
			ZipStructure.AssertValid(file);
		}

		ZipStructure.AssertPackagingReads(path);

		return path;
	}

	/// <summary>
	/// A stream for a workbook that a test writes but doesn't read back, such as one whose writer only refuses something:
	/// the workbook writer disposes it as it closes the package, and disposing it checks the package
	/// (<see cref="Checked(byte[])"/>).
	/// </summary>
	/// <returns>The stream, empty.</returns>
	public static MemoryStream CheckedOnClose() => new CheckingStream();

	/// <summary>Writes rows of fields by hand into one sheet, a field per <see cref="CsvWriter.WriteField(string?)"/>.</summary>
	/// <param name="rows">The rows.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <param name="sheet">The sheet's name.</param>
	/// <returns>The workbook's bytes.</returns>
	public static Task<byte[]> RowsAsync(IEnumerable<string?[]> rows, bool async, ExcelWriterOptions? options = null, string sheet = "Data") =>
		WorkbookAsync(async (workbook, a) =>
		{
			var writer = workbook.AddSheet(sheet);
			foreach (var row in rows)
			{
				foreach (var field in row)
				{
					writer.WriteField(field);
				}

				await EndRecordAsync(writer, a);
			}

			await DisposeAsync(writer, a);
		}, async, options);

	/// <summary>Ends a record as the mode says.</summary>
	/// <param name="writer">The writer.</param>
	/// <param name="async">Whether to end it asynchronously.</param>
	/// <returns>A task that completes when the record ends.</returns>
	public static async Task EndRecordAsync(CsvWriter writer, bool async)
	{
		if (async)
		{
			await writer.EndRecordAsync();
		}
		else
		{
			writer.EndRecord();
		}
	}

	/// <summary>Disposes a writer as the mode says.</summary>
	/// <param name="writer">The writer.</param>
	/// <param name="async">Whether to dispose it asynchronously.</param>
	/// <returns>A task that completes when it is disposed.</returns>
	public static async Task DisposeAsync(CsvWriter writer, bool async)
	{
		if (async)
		{
			await writer.DisposeAsync();
		}
		else
		{
			writer.Dispose();
		}
	}

	/// <summary>Reads every row of every worksheet with CsvHelper.Excel, the header too.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>Each sheet's rows, by name in tab order, each row as its fields joined with <c>|</c>.</returns>
	public static List<(string Sheet, List<string> Rows)> Read(byte[] workbook)
	{
		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		var sheets = new List<(string, List<string>)>();
		foreach (var sheet in book.Sheets)
		{
			using var reader = book.OpenReader(sheet, AllRows);
			var rows = new List<string>();
			while (reader.Read())
			{
				rows.Add(Api.Workbooks.Fields(reader));
			}

			sheets.Add((sheet.Name, rows));
		}

		return sheets;
	}

	/// <summary>Reads every worksheet with CsvHelper.Excel, each as one line.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>Each sheet, in tab order, as its name and its rows: <c>Name: row 1 / row 2</c>.</returns>
	public static List<string> Sheets(byte[] workbook) => [.. Read(workbook).Select(s => s.Sheet + ": " + string.Join(" / ", s.Rows))];

	/// <summary>Reads the rows of a workbook's one sheet with CsvHelper.Excel.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>The rows, each as its fields joined with <c>|</c>.</returns>
	public static List<string> ReadOne(byte[] workbook) => Assert.Single(Read(workbook)).Rows;

	/// <summary>The Open XML SDK's validation errors of a workbook.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>The errors, each with its part and path.</returns>
	public static List<string> ValidationErrors(byte[] workbook)
	{
		using var document = SpreadsheetDocument.Open(new MemoryStream(workbook), false);

		return [.. new OpenXmlValidator().Validate(document).Select(e => $"{e.Part?.Uri} {e.Path?.XPath}: {e.Description}")];
	}

	/// <summary>Each worksheet's cells with a value, as ClosedXML reads their text: <c>C14=text</c>, by sheet in tab order.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>The sheets' cells.</returns>
	public static List<(string Sheet, List<string> Cells)> ClosedXml(byte[] workbook)
	{
		using var book = new XLWorkbook(new MemoryStream(workbook));

		return [.. book.Worksheets.Select(w => (w.Name, w.CellsUsed(XLCellsUsedOptions.Contents).Select(c => $"{c.Address}={c.GetString()}").ToList()))];
	}

	/// <summary>
	/// A worksheet's cells with a value as ClosedXML reads their values: <c>C14=Number:42</c>, with numbers in invariant
	/// form, dates as <c>yyyy-MM-ddTHH:mm:ss.fff</c> and durations as <c>c</c> formats them.
	/// </summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>The first sheet's cells.</returns>
	public static List<string> ClosedXmlValues(byte[] workbook)
	{
		using var book = new XLWorkbook(new MemoryStream(workbook));

		return [.. book.Worksheets.First().CellsUsed(XLCellsUsedOptions.Contents).Select(c => $"{c.Address}={c.Value.Type}:{Shown(c.Value)}")];
	}

	/// <summary>
	/// A worksheet's rows as ExcelDataReader reads their values, each as its type and text: <c>Double:42</c>,
	/// <c>DateTime:2024-01-15T10:30:00.000</c>, <c>Boolean:True</c>, <c>String:a</c>, and an empty cell as nothing.
	/// </summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>The first sheet's rows, each as its values joined with <c>|</c>.</returns>
	public static List<string> ExcelDataReaderValues(byte[] workbook)
	{
		using var reader = ExcelReaderFactory.CreateOpenXmlReader(new MemoryStream(workbook));
		var rows = new List<string>();
		while (reader.Read())
		{
			rows.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetValue(i) switch
			{
				null => "",
				DateTime d => "DateTime:" + d.ToString("yyyy-MM-dd'T'HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture),
				double d => "Double:" + d.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
				TimeSpan t => "TimeSpan:" + t.ToString("c", System.Globalization.CultureInfo.InvariantCulture),
				var value => value.GetType().Name + ":" + Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
			})));
		}

		return rows;
	}

	/// <summary>A ClosedXML value's text, in invariant form.</summary>
	/// <param name="value">The value.</param>
	/// <returns>The text.</returns>
	private static string Shown(XLCellValue value) => value.Type switch
	{
		XLDataType.Number => value.GetNumber().ToString("R", System.Globalization.CultureInfo.InvariantCulture),
		XLDataType.DateTime => value.GetDateTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture),
		XLDataType.TimeSpan => value.GetTimeSpan().ToString("c", System.Globalization.CultureInfo.InvariantCulture),
		XLDataType.Boolean => value.GetBoolean() ? "TRUE" : "FALSE",
		_ => value.ToString(System.Globalization.CultureInfo.InvariantCulture),
	};

	/// <summary>Each worksheet's rows as ExcelDataReader reads them, an empty cell as an empty string, by sheet in tab order.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>The sheets' rows, each as its values joined with <c>|</c>.</returns>
	public static List<(string Sheet, List<string> Rows)> ExcelDataReader(byte[] workbook)
	{
		using var reader = ExcelReaderFactory.CreateOpenXmlReader(new MemoryStream(workbook));
		var sheets = new List<(string, List<string>)>();
		do
		{
			var rows = new List<string>();
			while (reader.Read())
			{
				rows.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture) ?? "")));
			}

			sheets.Add((reader.Name, rows));
		}
		while (reader.NextResult());

		return sheets;
	}

	/// <summary>
	/// Text as the writer escapes it for a cell's XML, with its entities decoded: what a reader that doesn't decode the
	/// escapes reads.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <returns>The text with its escapes.</returns>
	public static string EscapedText(string text)
	{
		using var output = new XmlOutput(16);
		output.AppendText(Encoding.UTF8.GetBytes(text));

		return System.Net.WebUtility.HtmlDecode(Encoding.UTF8.GetString(output.Written.ToArray()));
	}

	/// <summary>A package's entries as text, by name, in the zip's order.</summary>
	/// <param name="package">The package's bytes.</param>
	/// <returns>The entries.</returns>
	public static List<(string Name, string Text)> Entries(byte[] package)
	{
		using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
		var entries = new List<(string, string)>();
		foreach (var entry in archive.Entries)
		{
			using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
			entries.Add((entry.FullName, reader.ReadToEnd()));
		}

		return entries;
	}

	/// <summary>A package's entry as text.</summary>
	/// <param name="package">The package's bytes.</param>
	/// <param name="name">The entry's name.</param>
	/// <returns>Its text.</returns>
	public static string Entry(byte[] package, string name) => Entries(package).Single(e => e.Name == name).Text;

	/// <summary>A worksheet's XML.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <param name="sheet">The worksheet's part's number, from 1.</param>
	/// <returns>The XML.</returns>
	public static XDocument Sheet(byte[] workbook, int sheet = 1) => XDocument.Parse(Entry(workbook, $"xl/worksheets/sheet{sheet}.xml"));

	/// <summary>
	/// A worksheet's rows and their cells at their places, as SpreadsheetML places them: a row at its <c>r</c> when it has
	/// one, else in the row after the previous one (row 1 first); a cell at its <c>r</c>'s column when it has one, else in
	/// the column after the previous cell of its row (A first). So it reads a sheet written with references and one
	/// written without them (<c>&lt;row&gt;</c>, <c>&lt;c s="3"&gt;</c>, the gaps' <c>&lt;c/&gt;</c> and the empty records'
	/// <c>&lt;row/&gt;</c>), as <c>Corpus/Oracles.cs</c> reads the corpus. It checks what both shapes hold: rows and their
	/// cells in ascending order, a cell's <c>r</c> in its row, and no sheet that mixes rows, or cells, with and without
	/// <c>r</c>.
	/// </summary>
	/// <param name="sheet">The worksheet's XML.</param>
	/// <returns>The rows, in order.</returns>
	public static List<WrittenRow> Rows(XDocument sheet)
	{
		var rows = new List<WrittenRow>();
		var (rowsWith, rowsWithout, cellsWith, cellsWithout) = (0, 0, 0, 0);
		long number = 0;
		foreach (var row in sheet.Root!.Element(Main + "sheetData")?.Elements(Main + "row") ?? [])
		{
			var previous = number;
			if ((string?)row.Attribute("r") is { } r)
			{
				number = long.Parse(r, NumberStyles.None, CultureInfo.InvariantCulture);
				rowsWith++;
			}
			else
			{
				number++;
				rowsWithout++;
			}

			Assert.True(number > previous, $"Row {number} follows row {previous}.");
			var cells = new List<WrittenCell>();
			var column = -1;
			foreach (var cell in row.Elements(Main + "c"))
			{
				var before = column;
				if ((string?)cell.Attribute("r") is { } reference)
				{
					Assert.True(CellReference.TryParse(Encoding.ASCII.GetBytes(reference), out column, out var cellRow), $"The cell {reference} has no reference.");
					Assert.True(cellRow == number, $"The cell {reference} is in row {number}.");
					cellsWith++;
				}
				else
				{
					column++;
					cellsWithout++;
				}

				Assert.True(column > before, $"A cell in column {column} of row {number} follows one in column {before}.");
				cells.Add(new WrittenCell(number, column, cell));
			}

			rows.Add(new WrittenRow(number, row, cells));
		}

		Assert.False(rowsWith > 0 && rowsWithout > 0, $"The sheet mixes {rowsWith} rows with r and {rowsWithout} without.");
		Assert.False(cellsWith > 0 && cellsWithout > 0, $"The sheet mixes {cellsWith} cells with r and {cellsWithout} without.");

		return rows;
	}

	/// <summary>A worksheet's rows and their cells at their places (<see cref="Rows(XDocument)"/>).</summary>
	/// <param name="sheet">The worksheet's XML, as text.</param>
	/// <returns>The rows, in order.</returns>
	public static List<WrittenRow> Rows(string sheet) => Rows(XDocument.Parse(sheet));

	/// <summary>A worksheet's cells at their places (<see cref="Rows(XDocument)"/>), row by row.</summary>
	/// <param name="sheet">The worksheet's XML.</param>
	/// <returns>The cells, in order.</returns>
	public static List<WrittenCell> Cells(XDocument sheet) => [.. Rows(sheet).SelectMany(r => r.Cells)];

	/// <summary>The cell at a reference (<see cref="Rows(XDocument)"/>).</summary>
	/// <param name="sheet">The worksheet's XML.</param>
	/// <param name="reference">The reference, such as <c>B4</c>.</param>
	/// <returns>The cell's element, or <see langword="null"/> when the sheet has no cell there.</returns>
	public static XElement? Cell(XDocument sheet, string reference) => Cells(sheet).SingleOrDefault(c => c.Reference == reference)?.Element;

	/// <summary>The row at a number (<see cref="Rows(XDocument)"/>).</summary>
	/// <param name="sheet">The worksheet's XML.</param>
	/// <param name="number">The row's number, from 1.</param>
	/// <returns>The row's element, or <see langword="null"/> when the sheet has no row there.</returns>
	public static XElement? Row(XDocument sheet, long number) => Rows(sheet).SingleOrDefault(r => r.Number == number)?.Element;

	/// <summary>Where the cell at a reference is in a worksheet's text (<see cref="Rows(XDocument)"/>).</summary>
	/// <param name="sheet">The worksheet's XML, as text.</param>
	/// <param name="reference">The reference, such as <c>B4</c>.</param>
	/// <returns>The cell's element's first character and its length, or <see langword="null"/> when the sheet has no cell there.</returns>
	public static (int Start, int Length)? CellSpan(string sheet, string reference)
	{
		foreach (var (row, _, cells) in Spans(sheet))
		{
			for (var i = 0; i < row.Cells.Count; i++)
			{
				if (row.Cells[i].Reference == reference)
				{
					return cells[i];
				}
			}
		}

		return null;
	}

	/// <summary>The cell at a reference as a worksheet's text has it, without its <c>r</c>, which a sheet may or may not write.</summary>
	/// <param name="sheet">The worksheet's XML, as text.</param>
	/// <param name="reference">The reference, such as <c>B4</c>.</param>
	/// <returns>The cell's XML, such as <c>&lt;c s="1"&gt;&lt;v&gt;45306&lt;/v&gt;&lt;/c&gt;</c>, or <see langword="null"/> when the sheet has no cell there.</returns>
	public static string? CellXml(string sheet, string reference) =>
		CellSpan(sheet, reference) is { } span ? WithoutReferences(sheet.Substring(span.Start, span.Length)) : null;

	/// <summary>The row at a number as a worksheet's text has it, without the <c>r</c> of the row and of its cells.</summary>
	/// <param name="sheet">The worksheet's XML, as text.</param>
	/// <param name="number">The row's number, from 1.</param>
	/// <returns>The row's XML, such as <c>&lt;row/&gt;</c>, or <see langword="null"/> when the sheet has no row there.</returns>
	public static string? RowXml(string sheet, long number)
	{
		foreach (var (row, (start, length), _) in Spans(sheet))
		{
			if (row.Number == number)
			{
				return WithoutReferences(sheet.Substring(start, length));
			}
		}

		return null;
	}

	/// <summary>
	/// Each row of a worksheet's text, at its place (<see cref="Rows(XDocument)"/>), with where its element is in the text
	/// and where each of its cells' is.
	/// </summary>
	/// <param name="sheet">The worksheet's XML, as text.</param>
	/// <returns>The rows, in order.</returns>
	private static List<(WrittenRow Row, (int Start, int Length) Span, List<(int Start, int Length)> Cells)> Spans(string sheet)
	{
		var rows = Rows(XDocument.Parse(sheet));
		var start = sheet.IndexOf("<sheetData>", StringComparison.Ordinal);
		var end = start < 0 ? -1 : sheet.IndexOf("</sheetData>", start, StringComparison.Ordinal);
		List<Match> elements = start < 0 ? [] : RowElement.Matches(sheet.Substring(0, end), start).Cast<Match>().ToList();
		Assert.Equal(rows.Count, elements.Count);
		var spans = new List<(WrittenRow, (int, int), List<(int, int)>)>();
		for (var i = 0; i < rows.Count; i++)
		{
			var row = elements[i];
			var cells = CellElement.Matches(row.Value).Cast<Match>().Select(c => (row.Index + c.Index, c.Length)).ToList();
			Assert.Equal(rows[i].Cells.Count, cells.Count);
			spans.Add((rows[i], (row.Index, row.Length), cells));
		}

		return spans;
	}

	/// <summary>A row's or a cell's XML without the <c>r</c> of its rows and cells.</summary>
	/// <param name="xml">The XML.</param>
	/// <returns>The XML without them.</returns>
	private static string WithoutReferences(string xml) => Reference.Replace(xml, "$1");

	/// <summary>A stream in memory that checks the package written to it when it is first disposed (<see cref="CheckedOnClose"/>).</summary>
	private sealed class CheckingStream : MemoryStream
	{
		/// <summary>Whether the package has been checked: a stream may be disposed more than once.</summary>
		private bool done;

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			if (disposing && !done)
			{
				done = true;
				Checked(ToArray());
			}

			base.Dispose(disposing);
		}
	}
}
