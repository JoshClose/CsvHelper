using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace CsvHelper.Excel.Aot;

/// <summary>
/// Phase 5's NativeAOT check: writes a sheet of <see cref="Order"/>, whose <see cref="ExcelSheetAttribute"/> and
/// <see cref="ExcelColumnAttribute"/>s CsvHelper.Excel reads through reflection, checks that the workbook has what they
/// say (its title, table, frozen column, widths, formats, hidden column and links), and reads the orders back, by the
/// header's row and by finding it. Published with NativeAOT, a member the trimmer dropped would lose its look or its value.
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

		foreach (var problem in problems)
		{
			Console.Error.WriteLine(problem);
		}

		Console.WriteLine(problems.Count == 0 ? $"Wrote and read back {orders.Length} orders, with their look, in {bytes.Length:N0} bytes." : $"{problems.Count} problems.");

		return problems.Count == 0 ? 0 : 1;
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
