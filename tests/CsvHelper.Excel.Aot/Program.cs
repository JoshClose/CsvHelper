using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace CsvHelper.Excel.Aot;

/// <summary>
/// Phase 5's NativeAOT check: writes a sheet of <see cref="Order"/>, whose <see cref="ExcelSheetAttribute"/> and
/// <see cref="ExcelColumnAttribute"/>s CsvHelper.Excel reads through reflection, checks that the workbook has what they
/// say (its title, table, frozen column, widths, formats, hidden column and links), and reads the orders back, by the
/// header's row and by finding it. Published with NativeAOT, a member the trimmer dropped would lose its look or its value.
/// Phase 6's conversions too: the orders as CSV converted to a sheet, its columns' types inferred and through
/// <see cref="Order"/>, and each sheet back to CSV.
/// </summary>
internal static class Program
{
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
		Expect(problems, sheet, "<c r=\"A1\" s=\"", "the title's cell");
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
		foreach (var problem in problems)
		{
			Console.Error.WriteLine(problem);
		}

		Console.WriteLine(problems.Count == 0 ? $"Wrote and read back {orders.Length} orders, with their look, in {bytes.Length:N0} bytes, converted them from CSV and back, wrote a template and imported a sheet of it, and read and converted a sheet's shown text." : $"{problems.Count} problems.");

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

		// Inferred: numbers, dates, booleans and text, each column by its fields; an empty field is no cell.
		var inferred = ToExcel(csv, null);
		var sheet = Entry(inferred, "xl/worksheets/sheet1.xml");
		Expect(problems, sheet, "<c r=\"A2\"><v>1</v></c>", "the first id, a number");
		Expect(problems, sheet, "<c r=\"D3\"><v>0.25</v></c>", "the second total, a number");
		Expect(problems, sheet, "<c r=\"E2\" t=\"b\"><v>1</v></c>", "the first order's paid, a boolean");
		Expect(problems, sheet, "<c r=\"B2\" t=\"inlineStr\"><is><t>Ada</t></is></c>", "the first customer, text");
		Expect(problems, sheet, "<c r=\"C3\" s=\"2\"><v>45351</v></c>", "the second order's date, 2024-02-29");
		if (sheet.Contains("<c r=\"F3\"", StringComparison.Ordinal))
		{
			problems.Add("The inferred sheet has a cell for an empty field: F3.");
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
