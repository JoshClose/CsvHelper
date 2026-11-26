using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CsvHelper.Excel.Aot;

/// <summary>
/// Phase 5's NativeAOT check: writes a sheet of <see cref="Order"/>, whose <see cref="ExcelSheetAttribute"/> and
/// <see cref="ExcelColumnAttribute"/>s CsvHelper.Excel reads through reflection, checks that the workbook has what they
/// say (its title, table, frozen column, widths, formats, hidden column and links), and reads the orders back, by the
/// header's row and by finding it. Published with NativeAOT, a member the trimmer dropped would lose its look or its value.
/// Phase 6's conversions too: the orders as CSV converted to a sheet, its columns' types inferred and through
/// <see cref="Order"/>, and each sheet back to CSV. And Phase 9's segment scanning: a sheet read under
/// <c>ParallelEnumerate</c>, its rows past the first megabyte in segments on threads of their own.
/// </summary>
internal static class Program
{
	/// <summary>A row's element: its start tag's attributes, and its cells unless it is empty.</summary>
	private static readonly Regex RowElement = new("<row(?<start>(?: [^>]*?)?)(?:/>|>(?<cells>.*?)</row>)", RegexOptions.Singleline | RegexOptions.CultureInvariant);

	/// <summary>A cell's element in a row: its start tag's attributes, and the rest.</summary>
	private static readonly Regex CellElement = new("<c(?<start>(?: [^>]*?)?)(?:/>|>.*?</c>)", RegexOptions.Singleline | RegexOptions.CultureInvariant);

	/// <summary>Writes, checks and reads back the orders.</summary>
	/// <returns>0 when everything is as written, else 1.</returns>
	private static int Main()
	{
		Order[] orders =
		[
			new() { Id = 1, Customer = "Ada", Placed = new DateTime(2024, 1, 15), Total = 1234.5m, Paid = true, Site = new Uri("https://example.com/ada"), Note = "first" },
			new() { Id = 2, Customer = "Grace", Placed = new DateTime(2024, 2, 29), Total = 0.25m, Paid = false, Site = null, Note = null },
		];

		using var stream = new MemoryStream();
		using (var workbook = ExcelWorkbookWriter.Create(stream, leaveOpen: true))
		{
			workbook.WriteSheet("Orders", orders);
		}

		var bytes = stream.ToArray();
		var problems = new List<string>();
		var sheet = Entry(bytes, "xl/worksheets/sheet1.xml");
		var styles = Entry(bytes, "xl/styles.xml");
		var table = Entry(bytes, "xl/tables/table1.xml");
		var title = CellXml(sheet, "A1");
		if (title is null || !title.StartsWith("<c s=\"", StringComparison.Ordinal))
		{
			problems.Add($"The workbook's title's cell, A1, has no style of its own: {title ?? "no cell"}.");
		}

		Expect(problems, sheet, "<mergeCell ref=\"A1:G1\"/>", "the title's merge");
		Expect(problems, sheet, "<pane xSplit=\"1\" ySplit=\"2\"", "the frozen header and first column");
		Expect(problems, sheet, "<col min=\"2\" max=\"2\" width=\"20.7109375\"", "the customer's width");
		Expect(problems, sheet, "hidden=\"1\"", "the hidden note");
		Expect(problems, sheet, "<hyperlink ref=\"F3\"", "the site's link");
		Expect(problems, styles, "formatCode=\"yyyy-mm-dd\"", "the date's format");
		Expect(problems, styles, "<xf numFmtId=\"4\"", "the total's format, Excel's built-in #,##0.00");
		Expect(problems, table, "name=\"Orders\"", "the table's name");
		Expect(problems, table, "<tableStyleInfo name=\"TableStyleLight9\"", "the table's style");

		foreach (var header in new[] { ExcelHeaderRow.At(2), ExcelHeaderRow.Find<Order>() })
		{
			using var book = ExcelWorkbook.Open(new MemoryStream(bytes));
			using var reader = book.OpenReader(new ExcelReaderOptions { HeaderRow = header });
			var read = reader.GetRecords<Order>().ToList();
			if (!read.Select(Text).SequenceEqual(orders.Select(Text)))
			{
				problems.Add($"Read with {header}: {string.Join(" / ", read.Select(Text))}, not {string.Join(" / ", orders.Select(Text))}.");
			}
		}

		Convert(problems, orders);
		Template(problems);
		Shown(problems);
		var lines = ReadInParallel(problems);
		foreach (var problem in problems)
		{
			Console.Error.WriteLine(problem);
		}

		Console.WriteLine(problems.Count == 0 ? $"Wrote and read back {orders.Length} orders, with their look, in {bytes.Length:N0} bytes, converted them from CSV and back, wrote a template and imported a sheet of it, read and converted a sheet's shown text, and read a sheet of {lines:N0} rows in parallel." : $"{problems.Count} problems.");

		return problems.Count == 0 ? 0 : 1;
	}

	/// <summary>
	/// Phase 6's conversions: the orders as CSV converted to a sheet, its columns' types inferred, and through
	/// <see cref="Order"/>, whose attributes give it their look; then each sheet back to CSV.
	/// </summary>
	/// <param name="problems">Takes what isn't as converted.</param>
	/// <param name="orders">The orders, which the second sheet must read back as.</param>
	private static void Convert(List<string> problems, Order[] orders)
	{
		const string csv =
			"Id,Customer,Placed,Total,Paid,Site,Note\r\n" +
			"1,Ada,2024-01-15,1234.5,true,https://example.com/ada,first\r\n" +
			"2,Grace,2024-02-29,0.25,FALSE,,\r\n";

		// Inferred: numbers, dates, booleans and text, each column by its fields; an empty field at a record's end is no
		// cell. Each cell is found at its place, whether or not the sheet writes its reference.
		var inferred = ToExcel(csv, null);
		var sheet = Entry(inferred, "xl/worksheets/sheet1.xml");
		ExpectCell(problems, sheet, "A2", "<c><v>1</v></c>", "the first id, a number");
		ExpectCell(problems, sheet, "D3", "<c><v>0.25</v></c>", "the second total, a number");
		ExpectCell(problems, sheet, "E2", "<c t=\"b\"><v>1</v></c>", "the first order's paid, a boolean");
		ExpectCell(problems, sheet, "B2", "<c t=\"inlineStr\"><is><t>Ada</t></is></c>", "the first customer, text");
		ExpectCell(problems, sheet, "C3", "<c s=\"2\"><v>45351</v></c>", "the second order's date, 2024-02-29");
		ExpectCell(problems, sheet, "E3", "<c t=\"b\"><v>0</v></c>", "the second order's paid, the last with a value");
		foreach (var empty in new[] { "F3", "G3" })
		{
			if (CellXml(sheet, empty) is { } cell)
			{
				problems.Add($"The inferred sheet has a cell for an empty field at a record's end: {empty}, {cell}.");
			}
		}

		// Back to CSV: a boolean in capitals, as the reader gives typed cells.
		var back = ToCsv(inferred, null);
		var expected = csv.Replace(",true,", ",TRUE,", StringComparison.Ordinal);
		if (back != expected)
		{
			problems.Add($"The inferred sheet came back as {back}, not {expected}");
		}

		// Through Order: its title, table and look, and the orders, read by the header's row.
		var typed = ToExcel(csv, true);
		Expect(problems, Entry(typed, "xl/tables/table1.xml"), "name=\"Orders\"", "the converted sheet's table");
		using (var book = ExcelWorkbook.Open(new MemoryStream(typed)))
		{
			using var reader = book.OpenReader(new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.At(2) });
			var read = reader.GetRecords<Order>().ToList();
			if (!read.Select(Text).SequenceEqual(orders.Select(Text)))
			{
				problems.Add($"Converted through Order: {string.Join(" / ", read.Select(Text))}, not {string.Join(" / ", orders.Select(Text))}.");
			}
		}

		var typedBack = ToCsv(typed, new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.At(2) });
		if (typedBack != expected)
		{
			problems.Add($"The sheet of Order came back as {typedBack}, not {expected}");
		}
	}

	/// <summary>
	/// Phase 7's templates and import, as NativeAOT runs them: a template of an enum, a <see cref="bool"/> and a date,
	/// protected with a password, whose hash runs SHA-512; and the import of a sheet of tickets with three cells at fault.
	/// </summary>
	/// <param name="problems">Takes what is wrong.</param>
	private static void Template(List<string> problems)
	{
		using (var stream = new MemoryStream())
		{
			ExcelTemplate.Create<Ticket>(stream, new ExcelTemplateOptions { Sheet = new ExcelSheetOptions { Password = "secret" } });
			var sheet = Entry(stream.ToArray(), "xl/worksheets/sheet1.xml");
			Expect(problems, sheet, "<sheetProtection algorithmName=\"SHA-512\" hashValue=\"", "the template's password's hash");
			Expect(problems, sheet, "<formula1>\"Open,On hold,Closed\"</formula1>", "the status's dropdown, by its names as written");
			Expect(problems, sheet, "<formula1>\"TRUE,FALSE\"</formula1>", "the urgency's dropdown");
			Expect(problems, sheet, "<dataValidation type=\"date\"", "the due date's validation");
		}

		// A sheet users filled in: a row that reads, a row of three cells at fault, and a row that reads.
		using var filled = new MemoryStream();
		using (var workbook = ExcelWorkbookWriter.Create(filled, leaveOpen: true))
		{
			using var sheet = workbook.AddSheet("Ticket");
			foreach (var row in new[] { new[] { "Status", "Urgent", "Due" }, ["On hold", "TRUE", "2024-01-15"], ["Pending", "maybe", "soon"], ["Closed", "FALSE", "2024-02-01"] })
			{
				foreach (var field in row)
				{
					sheet.WriteField(field);
				}

				sheet.EndRecord();
			}
		}

		filled.Position = 0;
		var result = ExcelReader.Import<Ticket>(filled);
		var records = string.Join(" / ", result.Records.Select(t => $"{t.Status} {t.Urgent} {t.Due:yyyy-MM-dd}"));
		var errors = string.Join(" / ", result.Errors.Select(e => $"{e.Location} {e.Member} {e.Value}"));
		if (records != "OnHold True 2024-01-15 / Closed False 2024-02-01" || errors != "Ticket!A3 Ticket.Status Pending / Ticket!B3 Ticket.Urgent maybe / Ticket!C3 Ticket.Due soon" || !result.Complete)
		{
			problems.Add($"The import read {records}, with the errors {errors}, complete {result.Complete}.");
		}
	}

	/// <summary>
	/// Phase 8's displayed text, as NativeAOT runs it under invariant globalization: a sheet of <see cref="Reading"/>s, read
	/// with <see cref="ExcelText.Formatted"/>, gives each cell's text as Excel shows it, a locale code's month in English (the
	/// runtime has no German there), while typed members keep the values and a string member takes the shown text; and
	/// converts to CSV as shown.
	/// </summary>
	/// <param name="problems">Takes what isn't as shown.</param>
	private static void Shown(List<string> problems)
	{
		Reading[] readings = [new() { Taken = new DateTime(2024, 1, 15, 13, 30, 0), Total = 1234.5m, Part = 3.5, Note = "abc" }];
		using var stream = new MemoryStream();
		using (var workbook = ExcelWorkbookWriter.Create(stream, leaveOpen: true))
		{
			workbook.WriteSheet("Readings", readings);
		}

		var bytes = stream.ToArray();
		const string Expected = "Taken|Total|Part|Note / January 15, 2024 1:30 PM|1,234.50|3 1/2|Note: abc";
		var formatted = new ExcelReaderOptions { Text = ExcelText.Formatted, Csv = new CsvReaderOptions { HasHeader = false } };
		using (var book = ExcelWorkbook.Open(new MemoryStream(bytes)))
		{
			using var reader = book.OpenReader(formatted);
			var rows = new List<string>();
			while (reader.Read())
			{
				var fields = new string[reader.FieldCount];
				for (var i = 0; i < fields.Length; i++)
				{
					fields[i] = reader.GetString(i);
				}

				rows.Add(string.Join("|", fields));
			}

			if (string.Join(" / ", rows) != Expected)
			{
				problems.Add($"The shown text read {string.Join(" / ", rows)}, not {Expected}.");
			}
		}

		using (var book = ExcelWorkbook.Open(new MemoryStream(bytes)))
		{
			using var reader = book.OpenReader(new ExcelReaderOptions { Text = ExcelText.Formatted });
			var read = reader.GetRecords<Reading>().Single();
			// Typed members keep the values; a string member takes the shown text.
			if (read.Taken != readings[0].Taken || read.Total != readings[0].Total || read.Part != readings[0].Part || read.Note != "Note: abc")
			{
				problems.Add($"Read as shown text, the values were {read.Taken:O} {read.Total} {read.Part} {read.Note}.");
			}
		}

		using var input = new MemoryStream(bytes);
		using var output = new MemoryStream();
		ExcelConvert.ToCsv(input, output, new ExcelToCsvOptions { Excel = formatted });
		var csv = Encoding.UTF8.GetString(output.ToArray());
		const string ExpectedCsv = "Taken,Total,Part,Note\r\n\"January 15, 2024 1:30 PM\",\"1,234.50\",3 1/2,Note: abc\r\n";
		if (csv != ExpectedCsv)
		{
			problems.Add($"The shown text converted as {csv}, not {ExpectedCsv}");
		}
	}

	/// <summary>
	/// Phase 9's segment scanning, as NativeAOT runs it. The internal switches that force small segments aren't visible to
	/// the app, so the sheet is large enough for the defaults: 60,000 <see cref="Line"/>s, written without references as
	/// CsvHelper.Excel writes every sheet, about 5 MB, whose rows past the first megabyte are cut into segments of 256 KB
	/// and scanned on threads of their own. Read under <c>ParallelEnumerate</c> at degree 4, as text and as records, the
	/// sheet gives the rows the sequential read gives, in order.
	/// </summary>
	/// <param name="problems">Takes what isn't as read sequentially.</param>
	/// <returns>The number of rows read.</returns>
	private static int ReadInParallel(List<string> problems)
	{
		var lines = new Line[60_000];
		for (var i = 0; i < lines.Length; i++)
		{
			lines[i] = new Line { Id = i + 1, Name = "line " + (i + 1).ToString(CultureInfo.InvariantCulture), Amount = (i + 1) * 0.25m };
		}

		using var stream = new MemoryStream();
		using (var workbook = ExcelWorkbookWriter.Create(stream, leaveOpen: true))
		{
			workbook.WriteSheet("Lines", lines);
		}

		var bytes = stream.ToArray();
		var size = Encoding.UTF8.GetByteCount(Entry(bytes, "xl/worksheets/sheet1.xml"));
		if (size < 3 * 1024 * 1024)
		{
			problems.Add($"The sheet of lines is {size:N0} bytes, too few for segments past its first megabyte.");
		}

		var sequential = new List<string>();
		using (var book = ExcelWorkbook.Open(new MemoryStream(bytes)))
		{
			using var reader = book.OpenReader();
			while (reader.Read())
			{
				sequential.Add(Fields(reader));
			}
		}

		using (var book = ExcelWorkbook.Open(new MemoryStream(bytes)))
		{
			using var reader = book.OpenReader();
			var parallel = reader.ParallelEnumerate(r => Fields(r), 4).ToList();
			if (FirstDifference(sequential, parallel) is { } difference)
			{
				problems.Add($"Read in parallel, the sheet of lines differs from the sequential read: {difference}.");
			}
		}

		using (var book = ExcelWorkbook.Open(new MemoryStream(bytes)))
		{
			using var reader = book.OpenReader();
			var read = reader.ParallelEnumerate<Line>(4).ToList();
			var wrong = read.Count != lines.Length ? lines.Length : Enumerable.Range(0, lines.Length).Count(i => read[i].Id != lines[i].Id || read[i].Name != lines[i].Name || read[i].Amount != lines[i].Amount);
			if (wrong > 0)
			{
				problems.Add($"Read in parallel as records, {read.Count:N0} lines of {lines.Length:N0}, {wrong:N0} of them not as written.");
			}
		}

		return lines.Length;
	}

	/// <summary>A record's line and fields as text, to compare reads.</summary>
	/// <param name="reader">The reader, on a record.</param>
	/// <returns>The line, then the fields joined.</returns>
	private static string Fields(CsvReader reader)
	{
		var fields = new string[reader.FieldCount];
		for (var i = 0; i < fields.Length; i++)
		{
			fields[i] = reader.GetString(i);
		}

		return reader.LineNumber.ToString(CultureInfo.InvariantCulture) + ": " + string.Join("|", fields);
	}

	/// <summary>The first difference between two reads' records, described.</summary>
	/// <param name="expected">The sequential read's.</param>
	/// <param name="actual">The other read's.</param>
	/// <returns>The difference, or <see langword="null"/> when they are the same.</returns>
	private static string? FirstDifference(List<string> expected, List<string> actual)
	{
		for (var i = 0; i < Math.Max(expected.Count, actual.Count); i++)
		{
			var sequential = i < expected.Count ? expected[i] : "no record";
			var other = i < actual.Count ? actual[i] : "no record";
			if (sequential != other)
			{
				return $"record {i + 1} of {expected.Count:N0} and {actual.Count:N0} is {other}, not {sequential}";
			}
		}

		return null;
	}

	/// <summary>Converts CSV to a workbook, its columns' types inferred, or through <see cref="Order"/>.</summary>
	/// <param name="csv">The CSV.</param>
	/// <param name="typed">Whether to convert through <see cref="Order"/>; <see langword="null"/> to infer.</param>
	/// <returns>The workbook's bytes.</returns>
	private static byte[] ToExcel(string csv, bool? typed)
	{
		using var input = new MemoryStream(Encoding.UTF8.GetBytes(csv));
		using var output = new MemoryStream();
		if (typed == true)
		{
			ExcelConvert.ToExcel<Order>(input, output);
		}
		else
		{
			ExcelConvert.ToExcel(input, output);
		}

		return output.ToArray();
	}

	/// <summary>Converts a workbook's first worksheet to CSV.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <param name="options">How to read the sheet, or <see langword="null"/>.</param>
	/// <returns>The CSV.</returns>
	private static string ToCsv(byte[] workbook, ExcelReaderOptions? options)
	{
		using var input = new MemoryStream(workbook);
		using var output = new MemoryStream();
		ExcelConvert.ToCsv(input, output, options is null ? null : new ExcelToCsvOptions { Excel = options });

		return Encoding.UTF8.GetString(output.ToArray());
	}

	/// <summary>Adds a problem when a part lacks some text.</summary>
	/// <param name="problems">The problems.</param>
	/// <param name="part">The part's text.</param>
	/// <param name="text">The text it should have.</param>
	/// <param name="what">What the text is, for the problem.</param>
	private static void Expect(List<string> problems, string part, string text, string what)
	{
		if (!part.Contains(text, StringComparison.Ordinal))
		{
			problems.Add($"The workbook lacks {what}: {text}");
		}
	}

	/// <summary>Adds a problem when a sheet's cell at a reference isn't as expected.</summary>
	/// <param name="problems">The problems.</param>
	/// <param name="sheet">The sheet's XML.</param>
	/// <param name="reference">The cell's reference, such as <c>A2</c>.</param>
	/// <param name="xml">The cell's XML without its reference.</param>
	/// <param name="what">What the cell is, for the problem.</param>
	private static void ExpectCell(List<string> problems, string sheet, string reference, string xml, string what)
	{
		if (CellXml(sheet, reference) is var cell && cell != xml)
		{
			problems.Add($"The workbook's {what} at {reference} is {cell ?? "no cell"}, not {xml}.");
		}
	}

	/// <summary>
	/// The cell at a reference, found at its place as SpreadsheetML places it: a row at its <c>r</c> when it has one, else
	/// the row after the previous one (row 1 first); a cell at its <c>r</c>'s column when it has one, else the column after
	/// the previous cell of its row (A first). So it reads a sheet written with references and one written without them.
	/// </summary>
	/// <param name="sheet">The sheet's XML.</param>
	/// <param name="reference">The reference, such as <c>A2</c>.</param>
	/// <returns>The cell's XML without its reference, or <see langword="null"/> when the sheet has no cell there.</returns>
	private static string? CellXml(string sheet, string reference)
	{
		var letters = new string(reference.TakeWhile(char.IsLetter).ToArray());
		var wantedColumn = letters.Aggregate(0, (n, c) => n * 26 + c - 'A' + 1) - 1;
		var wantedRow = long.Parse(reference.Substring(letters.Length), CultureInfo.InvariantCulture);
		long row = 0;
		foreach (Match rowElement in RowElement.Matches(sheet))
		{
			var rowReference = Regex.Match(rowElement.Groups["start"].Value, " r=\"([0-9]+)\"");
			row = rowReference.Success ? long.Parse(rowReference.Groups[1].Value, CultureInfo.InvariantCulture) : row + 1;
			if (row != wantedRow)
			{
				continue;
			}

			var column = -1;
			foreach (Match cell in CellElement.Matches(rowElement.Groups["cells"].Value))
			{
				var cellReference = Regex.Match(cell.Groups["start"].Value, " r=\"([A-Z]+)[0-9]+\"");
				column = cellReference.Success ? cellReference.Groups[1].Value.Aggregate(0, (n, c) => n * 26 + c - 'A' + 1) - 1 : column + 1;
				if (column == wantedColumn)
				{
					return Regex.Replace(cell.Value, "^<c r=\"[^\"]*\"", "<c");
				}
			}

			return null;
		}

		return null;
	}

	/// <summary>A package's entry, as text.</summary>
	/// <param name="package">The package's bytes.</param>
	/// <param name="name">The entry's name.</param>
	/// <returns>Its text, or nothing when the package has no such entry.</returns>
	private static string Entry(byte[] package, string name)
	{
		using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
		if (archive.GetEntry(name) is not { } entry)
		{
			return "";
		}

		using var reader = new StreamReader(entry.Open());

		return reader.ReadToEnd();
	}

	/// <summary>An order's values as text, to compare orders.</summary>
	/// <param name="order">The order.</param>
	/// <returns>Its values, joined.</returns>
	private static string Text(Order order) =>
		string.Join("|", order.Id, order.Customer, order.Placed.ToString("O", CultureInfo.InvariantCulture), order.Total.ToString(CultureInfo.InvariantCulture), order.Paid, order.Site, order.Note);
}

/// <summary>An order, with its sheet's look and its columns' in attributes.</summary>
[CsvRecord]
[ExcelSheet(Title = "Orders", Table = true, TableName = "Orders", TableStyle = "TableStyleLight9", FreezeColumns = 1)]
public sealed partial class Order
{
	/// <summary>The order's number.</summary>
	public int Id { get; set; }

	/// <summary>Who placed it, in a column 20 characters wide.</summary>
	[ExcelColumn(Width = 20)]
	public string Customer { get; set; } = "";

	/// <summary>When, as a date.</summary>
	[ExcelColumn(Format = "yyyy-mm-dd")]
	public DateTime Placed { get; set; }

	/// <summary>How much, with two decimals.</summary>
	[ExcelColumn(Format = "#,##0.00")]
	public decimal Total { get; set; }

	/// <summary>Whether it is paid.</summary>
	public bool Paid { get; set; }

	/// <summary>The customer's site, a link.</summary>
	public Uri? Site { get; set; }

	/// <summary>A note, in a hidden column.</summary>
	[ExcelColumn(Hidden = true)]
	public string? Note { get; set; }
}

/// <summary>A reading, its columns in formats whose shown text differs from the values': a German locale code's date, grouping, a fraction and a text section.</summary>
[CsvRecord]
public sealed partial class Reading
{
	/// <summary>When it was taken, its month named in German where the runtime has German.</summary>
	[ExcelColumn(Format = "[$-407]mmmm d, yyyy h:mm AM/PM")]
	public DateTime Taken { get; set; }

	/// <summary>Its total, grouped, with two decimals.</summary>
	[ExcelColumn(Format = "#,##0.00")]
	public decimal Total { get; set; }

	/// <summary>A part, as a fraction.</summary>
	[ExcelColumn(Format = "# ?/?")]
	public double Part { get; set; }

	/// <summary>A note, after a label.</summary>
	[ExcelColumn(Format = "\"Note: \"@")]
	public string Note { get; set; } = "";
}

/// <summary>A line of the sheet the parallel read reads: a number, a text and an amount.</summary>
[CsvRecord]
public sealed partial class Line
{
	/// <summary>The line's number, from 1.</summary>
	public int Id { get; set; }

	/// <summary>Its text.</summary>
	public string Name { get; set; } = "";

	/// <summary>Its amount, in quarters.</summary>
	public decimal Amount { get; set; }
}

/// <summary>A ticket's state, written by its other name when it has one.</summary>
public enum TicketState
{
	/// <summary>Open.</summary>
	Open,

	/// <summary>On hold.</summary>
	[CsvEnumName("On hold")]
	OnHold,

	/// <summary>Closed.</summary>
	Closed,
}

/// <summary>A ticket of a template: an enum's dropdown, a <see cref="bool"/>'s, and a date's validation.</summary>
[CsvRecord]
public sealed partial class Ticket
{
	/// <summary>Its state.</summary>
	public TicketState Status { get; set; }

	/// <summary>Whether it is urgent.</summary>
	public bool Urgent { get; set; }

	/// <summary>When it is due.</summary>
	public DateTime Due { get; set; }
}
