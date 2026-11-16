using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper.Excel.Tests.Sheet;
using CsvHelper.Excel.Tests.Writing;

namespace CsvHelper.Excel.Tests.Api;

/// <summary>An order's state, for the import's tests.</summary>
public enum ImportStatus
{
	/// <summary>Open.</summary>
	Open,

	/// <summary>Shipped.</summary>
	Shipped,
}

/// <summary>An order to import: a whole number, an enum, a date, a checked amount and text.</summary>
[CsvRecord]
public partial class ImportedOrder
{
	/// <summary>A whole number.</summary>
	public int Id { get; set; }

	/// <summary>An enum's name.</summary>
	public ImportStatus Status { get; set; }

	/// <summary>A date.</summary>
	public DateTime When { get; set; }

	/// <summary>An amount more than 0.</summary>
	[CsvValidate(nameof(IsPositive), Message = "Amount must be more than 0.")]
	public decimal Amount { get; set; }

	/// <summary>Text.</summary>
	public string Note { get; set; } = "";

	/// <summary>Whether an amount is more than 0.</summary>
	/// <param name="value">The amount.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsPositive(decimal value) => value > 0;
}

/// <summary>A range whose constructor reads its bounds, and throws when they are the wrong way round.</summary>
[CsvRecord]
public partial class ImportedRange
{
	/// <summary>Creates a range.</summary>
	/// <param name="low">The low bound.</param>
	/// <param name="high">The high bound, not below <paramref name="low"/>.</param>
	/// <exception cref="ArgumentException"><paramref name="low"/> is above <paramref name="high"/>.</exception>
	public ImportedRange(int low, int high)
	{
		if (low > high)
		{
			throw new ArgumentException("Low is above high.");
		}

		Low = low;
		High = high;
	}

	/// <summary>The low bound, read by the constructor first.</summary>
	public int Low { get; }

	/// <summary>The high bound, read by the constructor second.</summary>
	public int High { get; }

	/// <summary>A count, read after the constructor.</summary>
	public int Count { get; set; }
}

/// <summary>An amount in a currency, nested in <see cref="ImportedPayment"/>.</summary>
/// <param name="Amount">The amount.</param>
/// <param name="Currency">The currency.</param>
[CsvRecord]
public partial record struct ImportedMoney(decimal Amount, string Currency);

/// <summary>A payment, with a nested record whose columns have a prefix.</summary>
[CsvRecord]
public partial class ImportedPayment
{
	/// <summary>Text.</summary>
	public string Id { get; set; } = "";

	/// <summary>A nested record, from the columns <c>Paid Amount</c> and <c>Paid Currency</c>.</summary>
	[CsvColumn(Prefix = "Paid ")]
	public ImportedMoney Paid { get; set; }
}

/// <summary>A record whose column has another name.</summary>
[CsvRecord]
public partial class ImportedQuantity
{
	/// <summary>Text.</summary>
	public string Name { get; set; } = "";

	/// <summary>The column <c>Qty</c>, else <c>Quantity</c>.</summary>
	[CsvColumn("Qty", "Quantity")]
	public int Count { get; set; }
}

/// <summary>A record whose members take an empty cell.</summary>
[CsvRecord]
public partial class ImportedMaybe
{
	/// <summary>A whole number, or none.</summary>
	public int? Id { get; set; }

	/// <summary>Text, or none.</summary>
	public string? Note { get; set; }
}

/// <summary>A record whose constructor throws <see cref="NotSupportedException"/>, which no record method of the reader catches.</summary>
[CsvRecord]
public partial class ImportedUnsupported
{
	/// <summary>Throws.</summary>
	/// <param name="id">A whole number.</param>
	/// <exception cref="NotSupportedException">Always.</exception>
	public ImportedUnsupported(int id) => throw new NotSupportedException($"Record {id} isn't supported.");

	/// <summary>A whole number.</summary>
	public int Id { get; }
}

/// <summary>A record whose hand-written mapper fails, and whose search for the failing member fails too.</summary>
[CsvConverter(typeof(BrokenSearchMapper))]
public sealed class BrokenSearch
{
	/// <summary>A whole number.</summary>
	public int Id { get; set; }
}

/// <summary>A hand-written mapper that reads <c>Id</c>, and whose <see cref="FindFailingMember"/> throws.</summary>
public sealed class BrokenSearchMapper : CsvRecordMapper<BrokenSearch>
{
	/// <summary>The column of <c>Id</c>.</summary>
	private readonly int id;

	/// <summary>Finds the column.</summary>
	/// <param name="reader">The reader.</param>
	public BrokenSearchMapper(CsvReader reader)
	{
		id = Column(reader, "Id", -1, false, "BrokenSearch.Id");
		ThrowIfColumnsMissing(reader);
	}

	/// <summary>Reads a record.</summary>
	/// <param name="reader">The reader.</param>
	/// <returns>The record.</returns>
	public override BrokenSearch Read(CsvReader reader) => new() { Id = reader.Parse<int>(id) };

	/// <summary>Fails.</summary>
	/// <param name="reader">The reader.</param>
	/// <param name="fieldIndex">Not set.</param>
	/// <returns>Nothing.</returns>
	/// <exception cref="InvalidOperationException">Always.</exception>
	public override string? FindFailingMember(CsvReader reader, out int fieldIndex) => throw new InvalidOperationException("The search failed.");
}

/// <summary>
/// <see cref="ExcelReader.Import{T}(string, ExcelReaderOptions?, int)"/> and its overloads (Phase 7's step 6), from paths,
/// streams and readers, synchronously and asynchronously: the records that read; every bad cell of a record, with its cell,
/// column, member and text, in the sheet's order; a record's error when no cell is at fault; nested records; missing
/// columns at the header's row; empty rows; error cells under each <see cref="ExcelErrorCells"/>; <c>maxErrors</c> and
/// <see cref="ExcelImportResult{T}.Complete"/>; templates and prefilled sheets, edited; CSV through a reader; and the
/// arguments' checks.
/// </summary>
public class ImportTests
{
	/// <summary>The header of <see cref="ImportedOrder"/>.</summary>
	private static readonly string[] OrderHeader = ["Id", "Status", "When", "Amount", "Note"];

	/// <summary>Each source, sync and async.</summary>
	public static TheoryData<string, bool> Sources => new()
	{
		{ "path", false }, { "path", true }, { "stream", false }, { "stream", true }, { "reader", false }, { "reader", true },
	};

	/// <summary>Sync and async.</summary>
	public static TheoryData<bool> Modes => new() { false, true };

	/// <summary>The sources the import opens itself, a path and a stream, sync and async.</summary>
	public static TheoryData<string, bool> Opened => new() { { "path", false }, { "path", true }, { "stream", false }, { "stream", true } };

	[Theory]
	[MemberData(nameof(Sources))]
	public async Task ReadsEveryRecordOfAValidSheet(string source, bool async)
	{
		var workbook = Orders(
			Row(2, "n:1", "Open", "2024-01-15", "n:9.5", "first"),
			Row(3, "n:2", "Shipped", "2024-01-16 10:30", "n:0.25", null));

		var result = await ImportAsync<ImportedOrder>(workbook, source, async);

		Assert.Equal(
			["1|Open|2024-01-15T00:00:00|9.5|first", "2|Shipped|2024-01-16T10:30:00|0.25|"],
			result.Records.Select(Describe));
		Assert.Empty(result.Errors);
		Assert.False(result.HasErrors);
		Assert.True(result.Complete);
	}

	[Theory]
	[MemberData(nameof(Sources))]
	public async Task NamesEachBadCellOfARecord(string source, bool async)
	{
		var workbook = Orders(
			Row(2, "n:1", "Open", "2024-01-15", "n:9.5", "first"),
			Row(3, "abc", "Pending", "soon", "n:-1", "second"),
			Row(5, "n:3", "Shipped", "2024-02-01", "n:2", "third"));

		var result = await ImportAsync<ImportedOrder>(workbook, source, async);

		Assert.Equal(["1|Open|2024-01-15T00:00:00|9.5|first", "3|Shipped|2024-02-01T00:00:00|2|third"], result.Records.Select(Describe));
		Assert.Equal(
		[
			("Orders!A3", 3L, "Id", "ImportedOrder.Id", "abc", typeof(FormatException)),
			("Orders!B3", 3L, "Status", "ImportedOrder.Status", "Pending", typeof(FormatException)),
			("Orders!C3", 3L, "When", "ImportedOrder.When", "soon", typeof(FormatException)),
			("Orders!D3", 3L, "Amount", "ImportedOrder.Amount", "-1", typeof(CsvValidationException)),
		], result.Errors.Select(e => (e.Location, e.Row, e.Column, e.Member, e.Value, e.Exception.GetType())));
		Assert.All(result.Errors, e => Assert.Equal(e.Exception.Message, e.Message));
		Assert.Equal("Amount must be more than 0.", result.Errors[3].Message);
		Assert.Equal("Orders!D3: Amount must be more than 0.", result.Errors[3].ToString());
		Assert.True(result.HasErrors);
		Assert.True(result.Complete);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ListsARecordsCellsInTheSheetsOrder(bool async)
	{
		// The constructor reads Low, then High, then the mapper Count: the errors go by column.
		var workbook = Sheet("Ranges", Row(1, "Count", "High", "Low"), Row(2, "c", "h", "l"));

		var result = await ImportAsync<ImportedRange>(workbook, "stream", async);

		Assert.Empty(result.Records);
		Assert.Equal(["Ranges!A2 Count", "Ranges!B2 High", "Ranges!C2 Low"], result.Errors.Select(e => $"{e.Location} {e.Column}"));
		Assert.Equal(["ImportedRange.Count", "ImportedRange.High", "ImportedRange.Low"], result.Errors.Select(e => e.Member));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task GivesARecordsErrorWhenNoCellIsAtFault(bool async)
	{
		var workbook = Sheet("Ranges", Row(1, "Low", "High", "Count"), Row(2, "n:5", "n:1", "n:0"), Row(3, "n:1", "n:5", "n:2"));

		var result = await ImportAsync<ImportedRange>(workbook, "stream", async);

		Assert.Equal([(1, 5, 2)], result.Records.Select(r => (r.Low, r.High, r.Count)));
		var error = Assert.Single(result.Errors);
		Assert.Equal(("Ranges!2:2", 2L, (string?)null, (string?)null, (string?)null, "Low is above high."), (error.Location, error.Row, error.Column, error.Member, error.Value, error.Message));
		Assert.IsType<ArgumentException>(error.Exception);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task NamesANestedRecordsCellByItsColumn(bool async)
	{
		var workbook = Sheet("Payments", Row(1, "Id", "Paid Amount", "Paid Currency"), Row(2, "P1", "lots", "EUR"));

		var result = await ImportAsync<ImportedPayment>(workbook, "stream", async);

		var error = Assert.Single(result.Errors);
		Assert.Equal(("Payments!B2", "Paid Amount", "ImportedMoney.Amount", "lots"), (error.Location, error.Column, error.Member, error.Value));
	}

	[Theory]
	[MemberData(nameof(Sources))]
	public async Task GivesAnErrorForEachMissingColumnAtTheHeadersRow(string source, bool async)
	{
		var workbook = Sheet("Orders", Row(1, "Id", "Note"), Row(2, "n:1", "first"));

		var result = await ImportAsync<ImportedOrder>(workbook, source, async);

		Assert.Empty(result.Records);
		Assert.Equal(
		[
			("Orders!1:1", 1L, "Status", "ImportedOrder.Status", "The header has no column for ImportedOrder.Status ('Status')."),
			("Orders!1:1", 1L, "When", "ImportedOrder.When", "The header has no column for ImportedOrder.When ('When')."),
			("Orders!1:1", 1L, "Amount", "ImportedOrder.Amount", "The header has no column for ImportedOrder.Amount ('Amount')."),
		], result.Errors.Select(e => (e.Location, e.Row, e.Column, e.Member, e.Message)));
		Assert.All(result.Errors, e => Assert.Null(e.Value));
		Assert.All(result.Errors, e => Assert.IsType<KeyNotFoundException>(e.Exception));
		Assert.False(result.Complete);

		var two = await ImportAsync<ImportedOrder>(workbook, source, async, maxErrors: 2);
		Assert.Equal(["Status", "When"], two.Errors.Select(e => e.Column));
		Assert.False(two.Complete);
	}

	[Theory]
	[MemberData(nameof(Opened))]
	public async Task GivesAnErrorForEachColumnOfAHeaderRowWithoutAValue(string source, bool async)
	{
		// A template's header is under its title, and the upload is another file, whose second row is empty.
		var workbook = Sheet("Orders", Row(1, "Something else"), Row(3, "n:1", "Open"));
		var options = new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.At(2) };

		var result = await ImportAsync<ImportedOrder>(workbook, source, async, options);

		Assert.Empty(result.Records);
		Assert.Equal(OrderHeader, result.Errors.Select(e => e.Column));
		Assert.All(result.Errors, e => Assert.Equal(("Orders!2:2", 2L), (e.Location, e.Row)));
		Assert.Equal("Orders!2:2: The header has no column for ImportedOrder.Id ('Id').", result.Errors[0].ToString());
		Assert.Equal("The header row, Orders!2:2, has no value.", Assert.IsType<CsvFormatException>(result.Errors[0].Exception).Message);
		Assert.False(result.Complete);

		var two = await ImportAsync<ImportedOrder>(workbook, source, async, options, maxErrors: 2);
		Assert.Equal(["Id", "Status"], two.Errors.Select(e => e.Column));
	}

	[Theory]
	[MemberData(nameof(Opened))]
	public async Task GivesAnErrorForEachColumnTheClosestRowLacks(string source, bool async)
	{
		// Row 2 lacks Amount alone, and row 3, tried after it, lacks more.
		var workbook = Sheet("Orders", Row(1, "Orders"), Row(2, "Id", "Status", "When", "Note"), Row(3, "Id"));
		var options = new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.Find<ImportedOrder>() };

		var result = await ImportAsync<ImportedOrder>(workbook, source, async, options);

		var error = Assert.Single(result.Errors);
		Assert.Equal(("Orders!2:2", 2L, "Amount", "ImportedOrder.Amount"), (error.Location, error.Row, error.Column, error.Member));
		Assert.StartsWith("No row of Orders!", Assert.IsType<KeyNotFoundException>(error.Exception).Message);
		Assert.Empty(result.Records);
		Assert.False(result.Complete);

		// No row tried has a value, as of cells cleared: each column, without a place.
		var cleared = Sheet("Orders", "<row r=\"1\"><c r=\"A1\" s=\"0\"/></row>", "<row r=\"2\"><c r=\"B2\" s=\"0\"/></row>");
		var empty = await ImportAsync<ImportedOrder>(cleared, source, async, options);
		Assert.Equal(OrderHeader, empty.Errors.Select(e => e.Column));
		Assert.All(empty.Errors, e => Assert.Equal(((string?)null, 0L), (e.Location, e.Row)));
	}

	[Theory]
	[MemberData(nameof(Opened))]
	public async Task GivesTheSearchsErrorWhenTheClosestRowHasTheTypesColumns(string source, bool async)
	{
		// A search for a name the type doesn't need, in a sheet whose header has every column the type does.
		var workbook = Orders(Row(2, "n:1", "Open", "2024-01-15", "n:1", ""));
		var options = new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.Find(["Customer"]) };

		var result = await ImportAsync<ImportedOrder>(workbook, source, async, options);

		var error = Assert.Single(result.Errors);
		Assert.Equal(("Orders!1:1", 1L, (string?)null, (string?)null), (error.Location, error.Row, error.Column, error.Member));
		Assert.Equal(error.Exception.Message, error.Message);
		Assert.Contains("'Customer'", error.Message);
		Assert.False(result.Complete);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task LocatesMissingColumnsAtAHeaderBelowATitle(bool async)
	{
		var workbook = Sheet("Stock", Row(1, "Stock"), Row(2, "Name", "Amount"), Row(3, "bolt", "n:3"));
		var options = new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.At(2) };

		var result = await ImportAsync<ImportedQuantity>(workbook, "path", async, options, maxErrors: 1);

		var error = Assert.Single(result.Errors);
		Assert.Equal(("Stock!2:2", 2L, "Qty", "ImportedQuantity.Count"), (error.Location, error.Row, error.Column, error.Member));
		Assert.Equal("The header has no column for ImportedQuantity.Count ('Qty' or 'Quantity').", error.Message);
		Assert.False(result.Complete);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task SkipsEmptyRows(bool async)
	{
		// A row element without cells, and one of styled cells without values, as Excel leaves cleared rows.
		var workbook = Orders(
			Row(2, "n:1", "Open", "2024-01-15", "n:9.5", "first"),
			"<row r=\"3\"/>",
			"<row r=\"4\"><c r=\"A4\" s=\"0\"/><c r=\"B4\" s=\"0\"/><c r=\"E4\" s=\"0\"/></row>",
			Row(6, "n:2", "Open", "2024-01-16", "n:1", "second"));

		var result = await ImportAsync<ImportedOrder>(workbook, "stream", async);

		Assert.Equal([1, 2], result.Records.Select(r => r.Id));
		Assert.Empty(result.Errors);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ReadsErrorCellsAsTheOptionsSay(bool async)
	{
		var workbook = Sheet("Data", Row(1, "Id", "Note"), Row(2, "e:#N/A", "e:#DIV/0!"), Row(3, "n:7", "seven"), Row(4, "e:#N/A", "four"));

		// Text: a number's column refuses the error, and a string takes its text.
		var text = await ImportAsync<ImportedMaybe>(workbook, "stream", async);
		Assert.Equal(["Data!A2", "Data!A4"], text.Errors.Select(e => e.Location));
		var error = text.Errors[0];
		Assert.Equal(("Id", "#N/A", "The cell holds the error #N/A, so it is not a valid Int32."), (error.Column, error.Value, error.Message));
		Assert.Equal([7], text.Records.Select(r => r.Id));

		// Null: each is an empty cell, and a row of errors alone an empty row.
		var empty = await ImportAsync<ImportedMaybe>(workbook, "stream", async, new ExcelReaderOptions { ErrorCells = ExcelErrorCells.Null });
		Assert.Empty(empty.Errors);
		Assert.Equal([(7, "seven"), (null, "four")], empty.Records.Select(r => (r.Id, r.Note)));

		// Error: each fails, a string's too, with its error's text.
		var failed = await ImportAsync<ImportedMaybe>(workbook, "stream", async, new ExcelReaderOptions { ErrorCells = ExcelErrorCells.Error });
		Assert.Equal([("Data!A2", "#N/A"), ("Data!B2", "#DIV/0!"), ("Data!A4", "#N/A")], failed.Errors.Select(e => (e.Location, e.Value)));
		Assert.Equal("Data!B2 holds the error #DIV/0!.", failed.Errors[1].Message);
		Assert.Equal([7], failed.Records.Select(r => r.Id));
	}

	[Theory]
	[MemberData(nameof(Sources))]
	public async Task StopsAtMaxErrors(string source, bool async)
	{
		// Rows 2, 4 and 6 read; rows 3, 5 and 7 have two bad cells each.
		var rows = new List<string>();
		for (var row = 2; row <= 7; row++)
		{
			rows.Add(row % 2 == 0
				? Row(row, $"n:{row}", "Open", "2024-01-15", "n:1", "")
				: Row(row, "x", "Open", "2024-01-15", "n:0", ""));
		}

		var workbook = Orders([.. rows]);

		var three = await ImportAsync<ImportedOrder>(workbook, source, async, maxErrors: 3);
		Assert.Equal(["Orders!A3", "Orders!D3", "Orders!A5"], three.Errors.Select(e => e.Location));
		Assert.Equal([2, 4], three.Records.Select(r => r.Id));
		Assert.False(three.Complete);

		var one = await ImportAsync<ImportedOrder>(workbook, source, async, maxErrors: 1);
		Assert.Equal(["Orders!A3"], one.Errors.Select(e => e.Location));
		Assert.Equal([2], one.Records.Select(r => r.Id));
		Assert.False(one.Complete);

		var six = await ImportAsync<ImportedOrder>(workbook, source, async, maxErrors: 6);
		Assert.Equal(6, six.Errors.Count);
		Assert.Equal([2, 4, 6], six.Records.Select(r => r.Id));
		Assert.False(six.Complete);

		var all = await ImportAsync<ImportedOrder>(workbook, source, async, maxErrors: 7);
		Assert.Equal(6, all.Errors.Count);
		Assert.True(all.Complete);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ImportsATemplate(bool async)
	{
		using var stream = new MemoryStream();
		ExcelTemplate.Create<ValidatedOrder>(stream);

		var result = await ImportAsync<ValidatedOrder>(stream.ToArray(), "stream", async);

		Assert.Empty(result.Records);
		Assert.Empty(result.Errors);
		Assert.True(result.Complete);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ImportsAPrefilledSheetAndItsEdits(bool async)
	{
		ValidatedOrder[] orders =
		[
			new() { Id = 1, Status = TicketStatus.OnHold, When = new DateTime(2024, 1, 15, 10, 30, 0), Region = "North, East", Note = "00123" },
			new() { Id = 2, Status = TicketStatus.Closed, When = new DateTime(2024, 1, 16), Region = "South, West", Note = "fine" },
		];
		using var stream = new MemoryStream();
		using (var book = ExcelWorkbookWriter.Create(stream, leaveOpen: true))
		{
			book.WriteSheet("Orders", orders, new ExcelSheetOptions { Title = "Orders", Validate = true, Protect = true });
		}

		var options = new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.At(2) };
		var prefilled = await ImportAsync<ValidatedOrder>(stream.ToArray(), "stream", async, options);
		Assert.Equal(orders.Select(Describe), prefilled.Records.Select(Describe));
		Assert.Empty(prefilled.Errors);

		// A user types text over the second order's Id, and a status no enum has over the first's.
		var edited = Edit(stream.ToArray(), "xl/worksheets/sheet1.xml", sheet =>
		{
			sheet = Regex.Replace(sheet, "<c r=\"A4\"([^>]*)><v>2</v></c>", "<c r=\"A4\"$1 t=\"inlineStr\"><is><t>two</t></is></c>");

			return sheet.Replace("<t>On hold</t>", "<t>Waiting</t>");
		});
		var result = await ImportAsync<ValidatedOrder>(edited, "stream", async, options);
		Assert.Empty(result.Records);
		Assert.Equal([("Orders!B3", "Waiting"), ("Orders!A4", "two")], result.Errors.Select(e => (e.Location, e.Value)));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ImportsCsvThroughAReader(bool async)
	{
		using var reader = CsvReader.FromText("Id,Status,When,Amount,Note\n1,Open,2024-01-15,9.5,first\nabc,Open,2024-01-15,0,x\n");

		var result = async ? await ExcelReader.ImportAsync<ImportedOrder>(reader) : ExcelReader.Import<ImportedOrder>(reader);

		Assert.Equal([1], result.Records.Select(r => r.Id));
		Assert.Equal([(null, 2L, "Id", "abc"), (null, 2L, "Amount", "0")], result.Errors.Select(e => (e.Location, e.Row, e.Column, e.Value)));
		Assert.Equal("Record 2: Amount must be more than 0.", result.Errors[1].ToString());

		using var missing = CsvReader.FromText("Id,Note\n1,x\n");
		var header = async ? await ExcelReader.ImportAsync<ImportedOrder>(missing) : ExcelReader.Import<ImportedOrder>(missing);
		Assert.Equal([(null, 0L, "Status"), (null, 0L, "When"), (null, 0L, "Amount")], header.Errors.Select(e => (e.Location, e.Row, e.Column)));
		Assert.Equal("The header has no column for ImportedOrder.Status ('Status').", header.Errors[0].ToString());
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task GivesAnEmptySheetsMissingColumnsWithoutAPlace(bool async)
	{
		// A sheet without a row has no header row to name.
		var result = await ImportAsync<ImportedQuantity>(Sheet("Empty"), "stream", async);

		Assert.Equal(
			[((string?)null, 0L, "Name", "ImportedQuantity.Name"), (null, 0L, "Qty", "ImportedQuantity.Count")],
			result.Errors.Select(e => (e.Location, e.Row, e.Column, e.Member)));
		Assert.Equal("The header has no column for ImportedQuantity.Count ('Qty' or 'Quantity').", result.Errors[1].ToString());
		Assert.Empty(result.Records);
		Assert.False(result.Complete);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ImportsFromWhereAReaderStands(bool async)
	{
		var bytes = TestPackage.Workbook(new TestSheet("Q1 Orders", xml: Sheets.Worksheet(string.Concat(
			Row(1, OrderHeader),
			Row(2, "n:1", "Open", "2024-01-15", "n:1", ""),
			Row(3, "x", "Open", "2024-01-15", "n:1", ""),
			Row(4, "n:3", "Open", "2024-01-15", "n:1", ""))))).ToArray();
		using var workbook = ExcelWorkbook.Open(new MemoryStream(bytes));
		using var reader = workbook.OpenReader("Q1 Orders");
		Assert.True(reader.Read());

		var result = async ? await ExcelReader.ImportAsync<ImportedOrder>(reader, 10) : ExcelReader.Import<ImportedOrder>(reader, 10);

		Assert.Equal([3], result.Records.Select(r => r.Id));
		Assert.Equal(["'Q1 Orders'!A3"], result.Errors.Select(e => e.Location));
		// The reader isn't disposed.
		Assert.False(reader.Read());
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task GivesANumbersTextAsExcelShowsIt(bool async)
	{
		// 1E+29 and 123456789012345678 typed, as Excel stores them: the import gives them as Excel shows them, to 15 digits.
		var workbook = Sheet("Data", Row(1, "Id", "Note"), Row(2, "n:9.9999999999999991E+28", "x"), Row(3, "n:1.2345678901234499E+17", "x"));

		var result = await ImportAsync<ImportedMaybe>(workbook, "stream", async);

		Assert.Equal(["1E+29", "1.23456789012345E+17"], result.Errors.Select(e => e.Value));
		var digits = await ImportAsync<ImportedMaybe>(workbook, "stream", async, new ExcelReaderOptions { SignificantDigits = null });
		Assert.Equal(["9.9999999999999991E+28", "1.2345678901234499E+17"], digits.Errors.Select(e => e.Value));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task KeepsTheCellsTextWhenMessagesHideValues(bool async)
	{
		var workbook = Orders(Row(2, "secret", "Open", "2024-01-15", "n:1", ""));
		var options = new ExcelReaderOptions { Csv = new CsvReaderOptions { IncludeValuesInExceptions = false } };

		var result = await ImportAsync<ImportedOrder>(workbook, "stream", async, options);

		var error = Assert.Single(result.Errors);
		Assert.Equal("secret", error.Value);
		Assert.DoesNotContain("secret", error.Message);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task GivesARecordsErrorWhenTheSearchFails(bool async)
	{
		var workbook = Sheet("Data", Row(1, "Id"), Row(2, "x"), Row(3, "n:4"));

		var result = await ImportAsync<BrokenSearch>(workbook, "stream", async);

		Assert.Equal([4], result.Records.Select(r => r.Id));
		var error = Assert.Single(result.Errors);
		Assert.Equal(("Data!2:2", (string?)null), (error.Location, error.Member));
		Assert.IsType<FormatException>(error.Exception);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ThrowsWhatTheTypeOrTheOptionsGetWrong(bool async)
	{
		var workbook = Orders(Row(2, "n:1", "Open", "2024-01-15", "n:1", ""));

		// No header: a required member without an index is the type's to fix, not the sheet's.
		var noHeader = new ExcelReaderOptions { Csv = new CsvReaderOptions { HasHeader = false } };
		await Assert.ThrowsAsync<KeyNotFoundException>(() => ImportAsync<ImportedOrder>(workbook, "stream", async, noHeader));
		// A type without a mapper.
		await Assert.ThrowsAsync<InvalidOperationException>(() => ImportAsync<ImportTests>(workbook, "stream", async));
		// What the reader's record methods don't catch either.
		var unsupported = await Assert.ThrowsAsync<NotSupportedException>(() => ImportAsync<ImportedUnsupported>(Sheet("Data", Row(1, "Id"), Row(2, "n:1")), "stream", async));
		Assert.Equal("Record 1 isn't supported.", unsupported.Message);
		// A sheet the workbook lacks, and a workbook that isn't one.
		await Assert.ThrowsAsync<ArgumentException>(() => ImportAsync<ImportedOrder>(workbook, "stream", async, new ExcelReaderOptions { Sheet = "Nope" }));
		await Assert.ThrowsAsync<InvalidDataException>(() => ImportAsync<ImportedOrder>([1, 2, 3], "stream", async));
	}

	[Fact]
	public async Task ChecksItsArguments()
	{
		using var stream = new MemoryStream(Orders());
		using var reader = CsvReader.FromText("Id\n1\n");

		Assert.Throws<ArgumentNullException>("path", () => ExcelReader.Import<ImportedOrder>((string)null!));
		Assert.Throws<ArgumentNullException>("stream", () => ExcelReader.Import<ImportedOrder>((Stream)null!));
		Assert.Throws<ArgumentNullException>("reader", () => ExcelReader.Import<ImportedOrder>((CsvReader)null!));
		await Assert.ThrowsAsync<ArgumentNullException>("path", async () => await ExcelReader.ImportAsync<ImportedOrder>((string)null!));
		await Assert.ThrowsAsync<ArgumentNullException>("stream", async () => await ExcelReader.ImportAsync<ImportedOrder>((Stream)null!));
		await Assert.ThrowsAsync<ArgumentNullException>("reader", async () => await ExcelReader.ImportAsync<ImportedOrder>((CsvReader)null!));
		foreach (var maxErrors in new[] { 0, -1, int.MinValue })
		{
			Assert.Throws<ArgumentOutOfRangeException>("maxErrors", () => ExcelReader.Import<ImportedOrder>("x.xlsx", null, maxErrors));
			Assert.Throws<ArgumentOutOfRangeException>("maxErrors", () => ExcelReader.Import<ImportedOrder>(stream, null, maxErrors));
			Assert.Throws<ArgumentOutOfRangeException>("maxErrors", () => ExcelReader.Import<ImportedOrder>(reader, maxErrors));
			await Assert.ThrowsAsync<ArgumentOutOfRangeException>("maxErrors", async () => await ExcelReader.ImportAsync<ImportedOrder>("x.xlsx", null, maxErrors));
			await Assert.ThrowsAsync<ArgumentOutOfRangeException>("maxErrors", async () => await ExcelReader.ImportAsync<ImportedOrder>(stream, null, maxErrors));
			await Assert.ThrowsAsync<ArgumentOutOfRangeException>("maxErrors", async () => await ExcelReader.ImportAsync<ImportedOrder>(reader, maxErrors));
		}

		// Nothing was read.
		Assert.Equal(0, stream.Position);
		Assert.True(reader.Read());
	}

	[Fact]
	public async Task ReadsOnlyAsynchronouslyWhenAsync()
	{
		// A CSV reader that hasn't read its header, of a stream that can't be read synchronously, its records past the
		// reader's first buffer.
		var text = new StringBuilder("Id,Status,When,Amount,Note\n");
		for (var i = 1; i <= 100; i++)
		{
			text.Append(i == 50 ? "x" : i.ToString(CultureInfo.InvariantCulture)).Append(",Open,2024-01-15,9.5,first\n");
		}

		using var reader = CsvReader.Create(new Conversion.AsyncOnlyStream(Encoding.UTF8.GetBytes(text.ToString())), new CsvReaderOptions { BufferSize = 256 });
		var result = await ExcelReader.ImportAsync<ImportedOrder>(reader);
		Assert.Equal(99, result.Records.Count);
		Assert.Equal([(50L, "Id")], result.Errors.Select(e => (e.Row, e.Column)));

		// A workbook in such a stream.
		var workbook = Orders(Row(2, "n:1", "Open", "2024-01-15", "n:1", ""), Row(3, "x", "Open", "2024-01-15", "n:1", ""));
		var sheet = await ExcelReader.ImportAsync<ImportedOrder>(new Conversion.AsyncOnlyStream(workbook));
		Assert.Equal([1], sheet.Records.Select(r => r.Id));
		Assert.Equal(["Orders!A3"], sheet.Errors.Select(e => e.Location));
	}

	[Fact]
	public async Task CancelsAnAsyncImport()
	{
		var workbook = Orders(Row(2, "n:1", "Open", "2024-01-15", "n:1", ""));
		using var canceled = new CancellationTokenSource();
		canceled.Cancel();
		using var stream = new MemoryStream(workbook);

		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await ExcelReader.ImportAsync<ImportedOrder>(stream, cancellationToken: canceled.Token));
		using var reader = ExcelReader.Create(new MemoryStream(workbook));
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await ExcelReader.ImportAsync<ImportedOrder>(reader, cancellationToken: canceled.Token));
	}

	[Theory]
	[InlineData("Order.Price ('Price')", "Order.Price", "Price")]
	[InlineData("Order.Price ('Unit Price' or 'Price')", "Order.Price", "Unit Price")]
	[InlineData("Order.Price ('Price' number 2)", "Order.Price", "Price")]
	[InlineData("Order.Price ('Price' or 'Cost' number 2)", "Order.Price", "Price")]
	[InlineData("Order.Name ('It's (new)')", "Order.Name", "It's (new)")]
	[InlineData("Order.Price", "Order.Price", null)]
	[InlineData("Order.Price ('Price", "Order.Price ('Price", null)]
	public void SplitsAMissingColumnsMemberAndName(string missing, string member, string? column)
	{
		Assert.Equal((member, column), Importer.SplitMissing(missing));
	}

	/// <summary>A workbook of one sheet, Orders: <see cref="ImportedOrder"/>'s header, then these rows.</summary>
	/// <param name="rows">The rows after the header, from <see cref="Row"/>.</param>
	/// <returns>The workbook's bytes.</returns>
	private static byte[] Orders(params string[] rows) => Sheet("Orders", [Row(1, OrderHeader), .. rows]);

	/// <summary>A workbook of one sheet of these rows.</summary>
	/// <param name="name">The sheet's name.</param>
	/// <param name="rows">The rows, from <see cref="Row"/>.</param>
	/// <returns>The workbook's bytes.</returns>
	private static byte[] Sheet(string name, params string[] rows) =>
		TestPackage.Workbook(new TestSheet(name, xml: Sheets.Worksheet(string.Concat(rows)))).ToArray();

	/// <summary>
	/// A row of cells from column A: <c>n:1.5</c> a number, <c>e:#N/A</c> an error, anything else an inline string, and
	/// <see langword="null"/> no cell.
	/// </summary>
	/// <param name="row">The row's number.</param>
	/// <param name="cells">The cells.</param>
	/// <returns>The row's XML.</returns>
	private static string Row(int row, params string?[] cells)
	{
		var xml = new StringBuilder($"<row r=\"{row}\">");
		for (var i = 0; i < cells.Length; i++)
		{
			var cell = cells[i];
			var reference = $"{(char)('A' + i)}{row}";
			if (cell is null)
			{
				continue;
			}

			if (cell.StartsWith("n:", StringComparison.Ordinal))
			{
				xml.Append($"<c r=\"{reference}\"><v>{cell.Substring(2)}</v></c>");
			}
			else if (cell.StartsWith("e:", StringComparison.Ordinal))
			{
				xml.Append($"<c r=\"{reference}\" t=\"e\"><v>{Sheets.Escape(cell.Substring(2))}</v></c>");
			}
			else
			{
				xml.Append($"<c r=\"{reference}\" t=\"inlineStr\"><is><t>{Sheets.Escape(cell)}</t></is></c>");
			}
		}

		return xml.Append("</row>").ToString();
	}

	/// <summary>Imports a workbook from a file, a stream or a reader, synchronously or not.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <param name="source">"path", "stream" or "reader".</param>
	/// <param name="async">Whether to import asynchronously.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <param name="maxErrors">The errors after which reading stops.</param>
	/// <returns>The result.</returns>
	private static async Task<ExcelImportResult<T>> ImportAsync<T>(byte[] workbook, string source, bool async, ExcelReaderOptions? options = null, int maxErrors = 1000)
	{
		switch (source)
		{
			case "path":
				var path = Path.Combine(Path.GetTempPath(), $"CsvHelper.Excel.Tests.{Guid.NewGuid():N}.xlsx");
				File.WriteAllBytes(path, workbook);
				try
				{
					return async ? await ExcelReader.ImportAsync<T>(path, options, maxErrors) : ExcelReader.Import<T>(path, options, maxErrors);
				}
				finally
				{
					Assert.True(Workbooks.IsClosed(path));
					File.Delete(path);
				}

			case "stream":
				using (var stream = new MemoryStream(workbook))
				{
					var result = async ? await ExcelReader.ImportAsync<T>(stream, options, maxErrors) : ExcelReader.Import<T>(stream, options, maxErrors);

					// The stream is left open.
					Assert.True(stream.CanRead);

					return result;
				}

			default:
				var reader = async ? await ExcelReader.CreateAsync(new MemoryStream(workbook), options) : ExcelReader.Create(new MemoryStream(workbook), options);
				await using (reader)
				{
					return async ? await ExcelReader.ImportAsync<T>(reader, maxErrors) : ExcelReader.Import<T>(reader, maxErrors);
				}
		}
	}

	/// <summary>A copy of a package with one entry's text changed.</summary>
	/// <param name="package">The package.</param>
	/// <param name="name">The entry.</param>
	/// <param name="change">Changes its text.</param>
	/// <returns>The changed package.</returns>
	private static byte[] Edit(byte[] package, string name, Func<string, string> change)
	{
		using var output = new MemoryStream();
		using (var source = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read))
		using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
		{
			foreach (var entry in source.Entries)
			{
				using var input = entry.Open();
				using var copy = new MemoryStream();
				input.CopyTo(copy);
				var data = copy.ToArray();
				if (entry.FullName == name)
				{
					var text = change(Encoding.UTF8.GetString(data));
					Assert.NotEqual(Encoding.UTF8.GetString(data), text);
					data = Encoding.UTF8.GetBytes(text);
				}

				using var written = target.CreateEntry(entry.FullName).Open();
				written.Write(data, 0, data.Length);
			}
		}

		return output.ToArray();
	}

	/// <summary>An order's fields, joined.</summary>
	/// <param name="order">The order.</param>
	/// <returns>The text.</returns>
	private static string Describe(ImportedOrder order) =>
		FormattableString.Invariant($"{order.Id}|{order.Status}|{order.When:s}|{order.Amount}|{order.Note}");

	/// <summary>An order's fields, joined.</summary>
	/// <param name="order">The order.</param>
	/// <returns>The text.</returns>
	private static string Describe(ValidatedOrder order) =>
		FormattableString.Invariant($"{order.Id}|{order.Status}|{order.When:s}|{order.Region}|{order.Note}");
}
