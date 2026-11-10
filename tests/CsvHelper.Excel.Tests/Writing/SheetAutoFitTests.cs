using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;


namespace CsvHelper.Excel.Tests.Writing;

/// <summary>An order of text, a number, a date and time, a boolean and a note, for auto-fit.</summary>
[CsvRecord]
public partial class FitOrder
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A number cell.</summary>
	public decimal Amount { get; set; }

	/// <summary>A date cell.</summary>
	public DateTime When { get; set; }

	/// <summary>A boolean cell.</summary>
	public bool Paid { get; set; }

	/// <summary>A text cell.</summary>
	public string Note { get; set; } = "";
}

/// <summary>
/// Auto-fit through the public API (Phase 5's step 3), synchronously and asynchronously: the header and the first records
/// held, each column as wide as its widest cell among them (the header in bold, with its filter button; text on one line,
/// or by its widest line when it wraps; numbers in General or in their column's format; booleans; dates; formulas as the
/// default column), the cap on text, a column's own width, the title left out, a failed record's cells dropped, and the
/// new sheets of <see cref="ExcelRowLimit.NewSheet"/>. Each workbook passes the Open XML SDK's validator and reads back as
/// written.
/// </summary>
public class SheetAutoFitTests
{
	/// <summary>The attributes of a width auto-fit gives.</summary>
	private const string Fitted = " bestFit=\"1\" customWidth=\"1\"";

	/// <summary>Two orders.</summary>
	private static readonly FitOrder[] Orders =
	[
		new() { Name = "apple", Amount = 1234.5m, When = new DateTime(2024, 1, 15, 10, 30, 0), Paid = true, Note = "Fresh from the farm" },
		new() { Name = "a much longer name", Amount = 0.25m, When = new DateTime(2024, 1, 16), Paid = false, Note = "Ripe" },
	];

	/// <summary>Sync and async.</summary>
	public static TheoryData<bool> Modes => new() { false, true };

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FitsEachColumnToItsWidestCellByDefault(bool async)
	{
		var workbook = await Write(Orders, async);

		// The header in bold; numbers in General as Excel's AutoFit counts them (each character a digit); a date and time;
		// booleans; text. Amount and Paid come out narrower than the default column.
		Assert.Equal(
			Columns(
				Fit(Text("Name", bold: true), Text("apple"), Text("a much longer name")),
				Fit(Text("Amount", bold: true), NumberFormatWidth.GeneralPixels(1234.5), NumberFormatWidth.GeneralPixels(0.25)),
				Fit(Text("When", bold: true), ColumnWidths.DateTimePixels),
				Fit(Text("Paid", bold: true), Text("TRUE"), Text("FALSE")),
				Fit(Text("Note", bold: true), Text("Fresh from the farm"), Text("Ripe"))),
			Cols(workbook));
		Assert.True(Width(workbook, 4) < ColumnWidths.Default);
		ReadsBackAsWritten(workbook, Orders);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task MeasuresTheHeaderAndTheRecordsAutoFitHolds(bool async)
	{
		// Two records measured: the third's longer name and the fourth's longer note count nothing.
		FitOrder[] orders = [Orders[0], Orders[0], Orders[1], With(Orders[0], o => o.Note = new string('n', 80))];

		var workbook = await Write(orders, async, new ExcelSheetOptions { AutoFit = 2 });

		Assert.Equal(Fit(Text("Name", bold: true), Text("apple")), Width(workbook, 1));
		Assert.True(Text("a much longer name") > Text("Name", bold: true));
		Assert.Equal(Fit(Text("Fresh from the farm")), Width(workbook, 5));
		ReadsBackAsWritten(workbook, orders);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task HoldsTheRecordsUntilTheirWidthsAreKnown(bool async)
	{
		// Nothing reaches the stream, even when a flush is asked for, until the last record auto-fit measures ends.
		var stream = new MemoryStream();
		var book = ExcelWorkbookWriter.Create(stream, new ExcelWriterOptions { Sheets = new ExcelSheetOptions { AutoFit = 3 } }, leaveOpen: true);
		var writer = book.AddSheet("Held");
		var lengths = new List<long>();
		for (var i = 0; i < 6; i++)
		{
			writer.WriteField(new string('x', 30000));
			await Written.EndRecordAsync(writer, async);
			if (async)
			{
				await writer.FlushAsync();
			}
			else
			{
				writer.Flush();
			}

			lengths.Add(stream.Length);
		}

		await Written.DisposeAsync(writer, async);
		book.Dispose();

		// The header and three records held; the zip writes its local header with the first bytes.
		Assert.Equal([0, 0, 0], lengths.Take(3));
		Assert.All(lengths.Skip(3), l => Assert.True(l > 0));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task DropsTheCellsOfAFailedRecord(bool async)
	{
		// The second record fails at its second field: its first, wider, was measured and is dropped with it.
		var stream = new MemoryStream();
		var book = ExcelWorkbookWriter.Create(stream, Written.Ragged(), leaveOpen: true);
		var writer = book.AddSheet("Failed");
		writer.WriteField("Name");
		writer.WriteField("Note");
		await Written.EndRecordAsync(writer, async);
		Assert.Throws<InvalidOperationException>(() => writer.WriteRecord(new Dictionary<string, object?> { ["Name"] = new string('w', 50), ["Note"] = new string('x', 40000) }));
		writer.WriteRecord(new Dictionary<string, object?> { ["Name"] = "kept", ["Note"] = "n" });
		await Written.DisposeAsync(writer, async);
		book.Dispose();

		var workbook = stream.ToArray();
		Assert.Equal(Fit(Text("Name", bold: true), Text("kept")), Width(workbook, 1));
		Assert.Equal(["Name|Note", "kept|n"], Written.ReadOne(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task CapsTextButNotTypedCells(bool async)
	{
		// At most 5 characters for text; a date and a number keep their widths, which would show ######## narrower.
		FitOrder[] orders = [With(Orders[0], o => { o.Amount = 1234567.891m; o.Note = new string('n', 300); })];

		var workbook = await Write(orders, async, new ExcelSheetOptions { MaxAutoFitWidth = 5 });

		// Paid's TRUE is narrower than the cap.
		var cap = ColumnWidths.FromCharacters(5);
		Assert.Equal(cap, Width(workbook, 1));
		Assert.Equal(Fit(NumberFormatWidth.GeneralPixels(1234567.891)), Width(workbook, 2));
		Assert.Equal(Fit(ColumnWidths.DateTimePixels), Width(workbook, 3));
		Assert.Equal(Fit(Text("Paid", bold: true), Text("TRUE")), Width(workbook, 4));
		Assert.True(Width(workbook, 4) < cap);
		Assert.Equal(cap, Width(workbook, 5));
		ReadsBackAsWritten(workbook, orders);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FitsTextThatWrapsByItsWidestLine(bool async)
	{
		// Note wraps: its widest line. Code doesn't: all of it on one line, as Excel shows it, a line feed 8 pixels.
		var note = "short\na much longer second line\nmid";
		var options = new ExcelSheetOptions { Columns = new Dictionary<string, ExcelColumnOptions> { ["Note"] = new() { Wrap = true } } };

		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Lines", options);
			foreach (var row in new[] { new[] { "Note", "Code" }, new[] { note, note } })
			{
				writer.WriteField(row[0]);
				writer.WriteField(row[1]);
				await Written.EndRecordAsync(writer, a);
			}

			await Written.DisposeAsync(writer, a);
		}, async);

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains($"<col min=\"1\" max=\"1\" width=\"{W(Fit(Text("a much longer second line")))}\" style=\"2\"{Fitted}/>", sheet);
		Assert.Contains($"<col min=\"2\" max=\"2\" width=\"{W(Fit(Text(note)))}\"{Fitted}/>", sheet);
		Assert.Equal(Text("short") + 8 + Text("a much longer second line") + 8 + Text("mid"), Text(note), 6);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task GivesFormulasTheDefaultWidthAtLeast(bool async)
	{
		var workbook = await Written.RowsAsync([["A", "Twice"], ["1", null], ["2", null]], async);
		var formulas = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Data");
			writer.WriteField("A");
			writer.WriteField("Twice");
			await Written.EndRecordAsync(writer, a);
			for (var i = 2; i <= 3; i++)
			{
				writer.WriteField(i - 1);
				writer.WriteFormula($"=A{i}*2");
				await Written.EndRecordAsync(writer, a);
			}

			await Written.DisposeAsync(writer, a);
		}, async);

		// Its value isn't known when the widths are written: the column is the default column, as without auto-fit, where
		// its header alone would be narrower.
		Assert.Equal(Fit(Text("Twice", bold: true)), Width(workbook, 2));
		Assert.Equal(ColumnWidths.Default, Width(formulas, 2));
		Assert.Empty(Written.ValidationErrors(formulas));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WidensTheHeaderForItsFilterButtons(bool async)
	{
		// With a filter, or a table with filter buttons, each of the header's cells has a button, an empty one too.
		var filtered = await Written.RowsAsync([["Name", "", "Code"], ["a", "b", "c"]], async, new ExcelWriterOptions { Sheets = new ExcelSheetOptions { AutoFilter = true } });
		var table = await Written.RowsAsync([["Name", "", "Code"], ["a", "b", "c"]], async, new ExcelWriterOptions { Sheets = new ExcelSheetOptions { Table = new ExcelTableOptions { Style = null } } });
		var plain = await Written.RowsAsync([["Name", "", "Code"], ["a", "b", "c"]], async);

		var name = Fit(Text("Name", bold: true) + ColumnWidths.FilterButtonPixels);
		var empty = Fit(Math.Max(Text("b"), ColumnWidths.FilterButtonPixels));
		Assert.Equal([name, empty, Fit(Text("Code", bold: true) + ColumnWidths.FilterButtonPixels)], Widths(filtered, 3));
		Assert.Equal(Widths(filtered, 3), Widths(table, 3));
		Assert.Equal([Fit(Text("Name", bold: true)), Fit(Text("b")), Fit(Text("Code", bold: true))], Widths(plain, 3));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task MeasuresAStyledTablesHeaderInBold(bool async)
	{
		// A table with a style sets its header's look, which is bold in Excel's built-in styles: the cells aren't, the
		// widths are.
		var workbook = await Written.RowsAsync([["Kept, Pluto"], ["a"]], async, new ExcelWriterOptions { Sheets = new ExcelSheetOptions { Table = new ExcelTableOptions { FilterButtons = false } } });

		Assert.Equal(Fit(Text("Kept, Pluto", bold: true)), Width(workbook, 1));
		Assert.True(Text("Kept, Pluto", bold: true) > Text("Kept, Pluto"));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task TakesAColumnsOwnWidthOverAutoFit(bool async)
	{
		var options = new ExcelSheetOptions { Columns = new Dictionary<string, ExcelColumnOptions> { ["Name"] = new() { Width = 12 }, ["Code"] = new() { Hidden = true } } };

		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			if (a)
			{
				await book.WriteSheetAsync("Data", Orders.Select(o => new LookOrder { Name = o.Name, Code = "C" }), options);
			}
			else
			{
				book.WriteSheet("Data", Orders.Select(o => new LookOrder { Name = o.Name, Code = "C" }), options);
			}
		}, async);

		// Its width, as set, without bestFit; a hidden column keeps the width auto-fit gives it, for when it is shown.
		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains("<col min=\"1\" max=\"1\" width=\"12.7109375\" customWidth=\"1\"/>", sheet);
		Assert.Contains($"<col min=\"5\" max=\"5\" width=\"{W(Fit(Text("Code", bold: true)))}\" hidden=\"1\"{Fitted}/>", sheet);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task LeavesTheTitleOut(bool async)
	{
		var workbook = await Write(Orders, async, new ExcelSheetOptions { Title = "A title far wider than any column below it, merged across them" });

		Assert.Equal(Fit(Text("Name", bold: true), Text("apple"), Text("a much longer name")), Width(workbook, 1));
		Assert.Contains("<mergeCell ref=\"A1:E1\"/>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task GivesNewSheetsTheFirstSheetsWidths(bool async)
	{
		// Three rows a sheet: the first fills before auto-fit's records end, which sets the widths; the next sheets take them,
		// however wide their own cells.
		var orders = Enumerable.Range(0, 6).Select(i => With(Orders[0], o => o.Name = new string('n', 3 + (10 * i)))).ToList();
		var options = new ExcelWriterOptions { RowLimit = ExcelRowLimit.NewSheet };

		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 3;
			if (a)
			{
				await book.WriteSheetAsync("Orders", orders, null);
			}
			else
			{
				book.WriteSheet("Orders", orders, null);
			}
		}, async, options);

		var sheets = Enumerable.Range(1, 3).Select(n => Regex.Match(Written.Entry(workbook, $"xl/worksheets/sheet{n}.xml"), "<cols>.*</cols>").Value).ToList();
		Assert.All(sheets, s => Assert.Equal(sheets[0], s));
		Assert.Equal(Fit(Text(orders[1].Name)), Width(workbook, 1));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FitsCellsInTheirColumnsFormat(bool async)
	{
		// Numbers, dates and booleans in their column's format: its widest text, so that later dates fit too.
		var options = new ExcelSheetOptions
		{
			Columns = new Dictionary<string, ExcelColumnOptions>
			{
				["Amount"] = new() { Format = "#,##0.00 \"EUR\"" },
				["When"] = new() { Format = "dddd, mmmm d, yyyy" },
				["Paid"] = new() { Format = "0;0;0;\"Paid: \"@" },
			},
		};

		var workbook = await Write(Orders, async, options);

		Assert.Equal(Fit(Text("1.234.50 EUR")), Width(workbook, 2));
		Assert.Equal(Fit(Text("Wednesday, September 28, 2024")), Width(workbook, 3));
		Assert.Equal(Fit(Text("Paid: FALSE")), Width(workbook, 4));
		ReadsBackAsWritten(workbook, Orders);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FitsTheFirstRecordsFormattedCellsWithoutAutoFit(bool async)
	{
		// Without auto-fit, a typed cell of the first data record in its column's format gets a width when the default
		// column is too narrow for it (decision 5), a boolean in its text section too; text gets none, and later records'
		// cells count nothing.
		var options = new ExcelSheetOptions
		{
			AutoFit = 0,
			BoldHeader = false,
			FreezeHeader = false,
			Columns = new Dictionary<string, ExcelColumnOptions>
			{
				["Amount"] = new() { Format = "#,##0.00 \"EUR\"" },
				["When"] = new() { Format = "dddd, mmmm d, yyyy" },
				["Paid"] = new() { Format = "0;0;0;\"Paid: \"@" },
				["Note"] = new() { Format = "0.0" },
			},
		};
		FitOrder[] orders = [With(Orders[0], o => o.Amount = 1234567.891m), With(Orders[1], o => o.Amount = 1234567890123m)];

		var workbook = await Write(orders, async, options);

		Assert.Equal(
			$"<cols><col min=\"2\" max=\"2\" width=\"{W(ColumnWidths.OfPixels(Text("1.234.567.89 EUR")))}\" style=\"1\" customWidth=\"1\"/>" +
			$"<col min=\"3\" max=\"3\" width=\"{W(ColumnWidths.OfPixels(Text("Wednesday, September 28, 2024")))}\" style=\"2\" customWidth=\"1\"/>" +
			$"<col min=\"4\" max=\"4\" width=\"{W(ColumnWidths.OfPixels(Text("Paid: TRUE")))}\" style=\"3\" customWidth=\"1\"/>" +
			"<col min=\"5\" max=\"5\" width=\"9.140625\" style=\"4\"/></cols>",
			Cols(workbook));
		ReadsBackAsWritten(workbook, orders);
	}

	/// <summary>A copy of an order, changed.</summary>
	/// <param name="order">The order.</param>
	/// <param name="change">Changes the copy.</param>
	/// <returns>The copy.</returns>
	private static FitOrder With(FitOrder order, Action<FitOrder> change)
	{
		var copy = new FitOrder { Name = order.Name, Amount = order.Amount, When = order.When, Paid = order.Paid, Note = order.Note };
		change(copy);

		return copy;
	}

	/// <summary>The width of the widest of some texts' pixels.</summary>
	/// <param name="pixels">The texts' pixels.</param>
	/// <returns>The width auto-fit gives them.</returns>
	private static double Fit(params double[] pixels) => ColumnWidths.Fit(pixels.Max());

	/// <summary>A text's pixels.</summary>
	/// <param name="text">The text.</param>
	/// <param name="bold">Whether it is bold.</param>
	/// <returns>The pixels.</returns>
	private static double Text(string text, bool bold = false) => ColumnWidths.TextPixels(text.AsSpan(), bold);

	/// <summary>The <c>cols</c> of auto-fitted columns, from A on.</summary>
	/// <param name="widths">Each column's width.</param>
	/// <returns>The XML.</returns>
	private static string Columns(params double[] widths) =>
		"<cols>" + string.Concat(widths.Select((w, i) => $"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{W(w)}\"{Fitted}/>")) + "</cols>";

	/// <summary>The first sheet's <c>cols</c>.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <returns>The XML, or nothing.</returns>
	private static string Cols(byte[] workbook) => Regex.Match(Written.Entry(workbook, "xl/worksheets/sheet1.xml"), "<cols>.*</cols>").Value;

	/// <summary>A column's width in the first sheet.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="column">The column, from 1.</param>
	/// <returns>The width.</returns>
	private static double Width(byte[] workbook, int column)
	{
		var match = Regex.Match(Cols(workbook), $"<col min=\"{column}\" max=\"{column}\" width=\"([^\"]+)\"");
		Assert.True(match.Success, $"Column {column} has no width: {Cols(workbook)}");

		return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
	}

	/// <summary>The first columns' widths in the first sheet.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="count">How many.</param>
	/// <returns>The widths.</returns>
	private static double[] Widths(byte[] workbook, int count) => [.. Enumerable.Range(1, count).Select(c => Width(workbook, c))];

	/// <summary>Checks that a workbook passes the validator, and that its orders read back as written.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="orders">The orders written.</param>
	private static void ReadsBackAsWritten(byte[] workbook, IEnumerable<FitOrder> orders)
	{
		Assert.Empty(Written.ValidationErrors(workbook));
		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		using var reader = book.OpenReader();
		Assert.Equal(orders.Select(OrderText), reader.GetRecords<FitOrder>().Select(OrderText));
	}

	/// <summary>The text of an order's values, to compare orders.</summary>
	/// <param name="order">The order.</param>
	/// <returns>Its values, joined.</returns>
	private static string OrderText(FitOrder order) =>
		string.Join("|", order.Name, order.Amount.ToString(CultureInfo.InvariantCulture), order.When.ToString("O", CultureInfo.InvariantCulture), order.Paid, order.Note);

	/// <summary>Writes records into a sheet named <c>Data</c>.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="records">The records.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="look">How the sheet looks, or <see langword="null"/>.</param>
	/// <returns>The workbook's bytes.</returns>
	private static Task<byte[]> Write<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(IEnumerable<T> records, bool async, ExcelSheetOptions? look = null) =>
		Written.WorkbookAsync(async (book, a) =>
		{
			if (a)
			{
				await book.WriteSheetAsync("Data", records, look);
			}
			else
			{
				book.WriteSheet("Data", records, look);
			}
		}, async);

	/// <summary>A width as the writer writes it.</summary>
	/// <param name="width">The width.</param>
	/// <returns>Its shortest text.</returns>
	private static string W(double width) => width.ToString("R", CultureInfo.InvariantCulture);
}
