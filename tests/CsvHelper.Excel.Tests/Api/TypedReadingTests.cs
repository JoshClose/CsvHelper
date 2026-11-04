using System.Data;
using System.Globalization;
using CsvHelper.Linq;

namespace CsvHelper.Excel.Tests.Api;

/// <summary>An order read from typed cells: dates, a <c>t="d"</c> date, a duration, rounded numbers, a boolean and an error.</summary>
[CsvRecord]
public partial class TypedOrder
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A date cell (a built-in date format).</summary>
	public DateTime Ordered { get; set; }

	/// <summary>An ISO 8601 cell (<c>t="d"</c>), or none.</summary>
	public DateTime? Shipped { get; set; }

	/// <summary>A duration cell (<c>[h]:mm:ss</c>).</summary>
	public TimeSpan Took { get; set; }

	/// <summary>A number cell with 17 digits.</summary>
	public decimal Total { get; set; }

	/// <summary>A number cell as Apache POI writes integers.</summary>
	public int Quantity { get; set; }

	/// <summary>A boolean cell.</summary>
	public bool Paid { get; set; }

	/// <summary>A number cell, or an error.</summary>
	public double? Score { get; set; }

#if NET
	/// <summary>A custom date cell's day.</summary>
	public DateOnly Day { get; set; }
#endif
}

/// <summary>
/// Typed reading end to end: a sheet of typed cells through every way of reading a <see cref="CsvReader"/>, in both date
/// systems, synchronously and asynchronously, with the error cells' policies, significant digits, and the options' checks.
/// </summary>
public class TypedReadingTests
{
	/// <summary>The header and the rows: apple's cells are typed, pear's error and missing <c>t="d"</c> cell test the policies.</summary>
	private const string Rows =
		"<row r=\"1\">" + "<c r=\"A1\" t=\"inlineStr\"><is><t>Name</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>Ordered</t></is></c>" +
		"<c r=\"C1\" t=\"inlineStr\"><is><t>Shipped</t></is></c><c r=\"D1\" t=\"inlineStr\"><is><t>Took</t></is></c>" +
		"<c r=\"E1\" t=\"inlineStr\"><is><t>Total</t></is></c><c r=\"F1\" t=\"inlineStr\"><is><t>Quantity</t></is></c>" +
		"<c r=\"G1\" t=\"inlineStr\"><is><t>Paid</t></is></c><c r=\"H1\" t=\"inlineStr\"><is><t>Score</t></is></c>" +
		"<c r=\"I1\" t=\"inlineStr\"><is><t>Day</t></is></c></row>" +
		"<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>apple</t></is></c><c r=\"B2\" s=\"1\"><v>45306</v></c>" +
		"<c r=\"C2\" t=\"d\" s=\"1\"><v>2024-01-16T08:30:00</v></c><c r=\"D2\" s=\"3\"><v>1.5</v></c><c r=\"E2\"><v>0.30000000000000004</v></c>" +
		"<c r=\"F2\"><v>42.0</v></c><c r=\"G2\" t=\"b\"><v>1</v></c><c r=\"H2\"><v>7.5</v></c><c r=\"I2\" s=\"2\"><v>45306.75</v></c></row>" +
		"<row r=\"3\"><c r=\"A3\" t=\"inlineStr\"><is><t>pear</t></is></c><c r=\"B3\" s=\"2\"><v>45307.5</v></c>" +
		"<c r=\"D3\" s=\"3\"><v>0.0625</v></c><c r=\"E3\"><v>12345678901234567</v></c><c r=\"F3\"><v>3</v></c><c r=\"G3\" t=\"b\"><v>0</v></c>" +
		"<c r=\"H3\" t=\"e\"><v>#N/A</v></c><c r=\"I3\" s=\"1\"><v>45307</v></c></row>";

	/// <summary>
	/// The workbook: the test package's styles (1 a built-in date, 2 a custom date) with an elapsed-time format as style 3,
	/// in either date system.
	/// </summary>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <returns>The package.</returns>
	private static TestPackage Typed(bool date1904)
	{
		var package = TestPackage.Workbook(new TestSheet("Data", xml: Sheet.Sheets.Worksheet(Rows)))
			.Replace("xl/styles.xml", "<numFmt numFmtId=\"164\" formatCode=\"yyyy\\-mm\\-dd\"/>", "<numFmt numFmtId=\"164\" formatCode=\"yyyy\\-mm\\-dd\"/><numFmt numFmtId=\"165\" formatCode=\"[h]:mm:ss\"/>")
			.Replace("xl/styles.xml", "<xf numFmtId=\"164\" applyNumberFormat=\"1\"/>", "<xf numFmtId=\"164\" applyNumberFormat=\"1\"/><xf numFmtId=\"165\" applyNumberFormat=\"1\"/>");

		return date1904 ? package.Replace("xl/workbook.xml", "<workbookPr ", "<workbookPr date1904=\"1\" ") : package;
	}

	/// <summary>The date of a serial in a date system, by .NET's arithmetic.</summary>
	/// <param name="serial">The serial.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <returns>The date.</returns>
	private static DateTime Date(double serial, bool date1904) => date1904 ? new DateTime(1904, 1, 1).AddDays(serial) : DateTime.FromOADate(serial);

	/// <summary>The options that read the error cell as null, so that pear reads as a record.</summary>
	private static readonly ExcelReaderOptions NullErrors = new() { ErrorCells = ExcelErrorCells.Null };

	public static TheoryData<bool, bool> Systems => new() { { false, false }, { false, true }, { true, false }, { true, true } };

	[Theory]
	[MemberData(nameof(Systems))]
	public async Task ReadsTypedRecords(bool date1904, bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Typed(date1904), async, NullErrors);
		Assert.Equal(date1904, workbook.Date1904);
		using var reader = await workbook.OpenReaderAsync(async);
		var orders = new List<TypedOrder>();
		if (async)
		{
			await foreach (var order in reader.GetRecordsAsync<TypedOrder>())
			{
				orders.Add(order);
			}
		}
		else
		{
			orders.AddRange(reader.GetRecords<TypedOrder>());
		}

		Assert.Equal(2, orders.Count);
		var (apple, pear) = (orders[0], orders[1]);
		Assert.Equal(Date(45306, date1904), apple.Ordered);
		Assert.Equal(Date(45307.5, date1904), pear.Ordered);
		// An ISO 8601 cell is the same date in either date system.
		Assert.Equal(new DateTime(2024, 1, 16, 8, 30, 0), apple.Shipped);
		Assert.Null(pear.Shipped);
		Assert.Equal(TimeSpan.FromHours(36), apple.Took);
		Assert.Equal(TimeSpan.FromMinutes(90), pear.Took);
		Assert.Equal(0.3m, apple.Total);
		Assert.Equal(12345678901234600m, pear.Total);
		Assert.Equal([42, 3], orders.Select(o => o.Quantity));
		Assert.Equal([true, false], orders.Select(o => o.Paid));
		Assert.Equal(7.5, apple.Score);
		Assert.Null(pear.Score);
#if NET
		Assert.Equal(DateOnly.FromDateTime(Date(45306.75, date1904)), apple.Day);
#endif
	}

	[Theory]
	[MemberData(nameof(Systems))]
	public async Task ReadsTypedCellsThroughEveryAccessor(bool date1904, bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Typed(date1904), async);
		var ordered = Date(45306, date1904);
		var day = Date(45306.75, date1904);

		using (var reader = await workbook.OpenReaderAsync(async))
		{
			Assert.True(async ? await reader.ReadAsync() : reader.Read());
			Assert.Equal(
				$"apple|{ordered:yyyy-MM-dd}|2024-01-16T08:30:00|36:00:00|0.30000000000000004|42.0|TRUE|7.5|{day:yyyy-MM-dd'T'HH:mm:ss}",
				Workbooks.Fields(reader));
			Assert.Equal(ordered, reader.Parse<DateTime>("Ordered"));
			Assert.Equal(0.3m, reader.Parse<decimal>("Total"));
			Assert.Equal(0.30000000000000004, reader.Parse<double>("Total"));
			Assert.Equal(42, reader.Parse<int>("Quantity"));
			Assert.Equal(TimeSpan.FromHours(36), reader.Parse<TimeSpan>("Took"));
			Assert.Equal(new DateTimeOffset(ordered, TimeSpan.Zero), reader.Parse<DateTimeOffset>("Ordered"));
			Assert.Equal(ordered, reader.ParseExact<DateTime>(reader.GetFieldIndex("Ordered"), "yyyyMMdd"));
			Assert.True(reader.FieldEquals(reader.GetFieldIndex("Paid"), "TRUE"u8));
			Assert.Equal("45306"u8.ToArray(), reader.GetSpan("Ordered").ToArray());
			Assert.True(async ? await reader.ReadAsync() : reader.Read());
			Assert.Equal("#N/A", reader.GetString("Score"));
			var e = Assert.Throws<FormatException>(() => reader.Parse<double>("Score"));
			Assert.Equal("The cell holds the error #N/A, so it is not a valid Double.", e.Message);
		}

		using (var reader = await workbook.OpenReaderAsync(async))
		{
			var records = reader.GetRecords<CsvDynamicRecord>().Select(r => r.Detach()).ToList();
			Assert.Equal("36:00:00", records[0]["Took"]);
			Assert.Equal("TRUE", records[0]["Paid"]);
			Assert.Equal(ordered, records[0].Parse<DateTime>("Ordered"));
			Assert.Equal(12345678901234600L, records[1].Parse<long>("Total"));
		}

		using (var reader = await workbook.OpenReaderAsync(async))
		using (var data = new CsvDataReader(reader))
		{
			Assert.True(data.Read());
			Assert.Equal(ordered, data.GetDateTime(1));
			Assert.Equal(0.3m, data.GetDecimal(4));
			Assert.Equal(42, data.GetInt32(5));
			Assert.True(data.GetBoolean(6));
			var table = new DataTable();
			table.Load(data);
			Assert.Equal("#N/A", table.Rows[0]["Score"]);
			Assert.Equal("FALSE", table.Rows[0]["Paid"]);
		}

		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { ErrorCells = ExcelErrorCells.Null }))
		{
			var paid = reader.Query<TypedOrder>().Where(o => o.Paid || o.Took > TimeSpan.FromDays(2)).Select(o => o.Name).ToList();
			Assert.Equal(["apple"], paid);
		}

		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { ErrorCells = ExcelErrorCells.Null }))
		{
			var totals = reader.ParallelEnumerate(r => (r.Parse<decimal>("Total"), r.GetString("Ordered")), 2).ToList();
			Assert.Equal([0.3m, 12345678901234600m], totals.Select(t => t.Item1));
			Assert.Equal($"{ordered:yyyy-MM-dd}", totals[0].Item2);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task FollowsTheErrorCellsPolicy(bool async)
	{
		// Text: a string reads the error; a number target fails, and GetRecords sends the cell to OnRecordError.
		var errors = new List<CsvRecordError>();
		var csv = new CsvReaderOptions
		{
			OnRecordError = error =>
			{
				errors.Add(error);

				return true;
			},
		};
		using var workbook = await Workbooks.OpenAsync(Typed(false), async);
		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { Csv = csv }))
		{
			Assert.Equal(["apple"], reader.GetRecords<TypedOrder>().Select(o => o.Name));
		}

		Assert.Equal("Data!H3", Assert.Single(errors).Location);

		// Null: the cell is empty.
		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { ErrorCells = ExcelErrorCells.Null }))
		{
			Assert.True(reader.Read() && reader.Read());
			Assert.Equal("", reader.GetString("Score"));
			Assert.True(reader.IsEmpty(reader.GetFieldIndex("Score")));
		}

		// Error: every read of the cell fails, naming it; the cells around it read.
		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { ErrorCells = ExcelErrorCells.Error }))
		{
			Assert.True(reader.Read() && reader.Read());
			Assert.Equal("Data!H3 holds the error #N/A.", Assert.Throws<FormatException>(() => reader.GetString("Score")).Message);
			Assert.Throws<FormatException>(() => reader.Parse<double>("Score"));
			Assert.Equal("#N/A"u8.ToArray(), reader.GetSpan("Score").ToArray());
			Assert.Equal("pear", reader.GetString("Name"));
		}

		// NullValues can name an error's text.
		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { Csv = new CsvReaderOptions { NullValues = ["#N/A"] } }))
		{
			Assert.Equal([7.5, null], reader.GetRecords<TypedOrder>().Select(o => o.Score));
		}

		// A header cell is a name, not a value: under Error, an error there names its column, as under Text. Under Null it is
		// empty, as every error cell is, so the header ends before it.
		var header = Workbooks.Data(
			"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Name</t></is></c><c r=\"B1\" t=\"e\"><v>#REF!</v></c></row>" +
			"<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>pear</t></is></c><c r=\"B2\"><v>3</v></c></row>");
		using var headed = await Workbooks.OpenAsync(header, async);
		foreach (var (policy, names) in new[] { (ExcelErrorCells.Text, new[] { "Name", "#REF!" }), (ExcelErrorCells.Error, new[] { "Name", "#REF!" }), (ExcelErrorCells.Null, new[] { "Name" }) })
		{
			using var reader = await headed.OpenReaderAsync(async, new ExcelReaderOptions { ErrorCells = policy });
			Assert.True(async ? await reader.ReadAsync() : reader.Read());
			Assert.Equal(names, reader.Headers);
			Assert.Equal(3, reader.Parse<int>(1));
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RoundsToTheSignificantDigits(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Typed(false), async);
		foreach (var (digits, total, big) in new (int?, decimal, decimal)[] { (15, 0.3m, 12345678901234600m), (17, 0.30000000000000004m, 12345678901234567m), (null, 0.30000000000000004m, 12345678901234567m), (2, 0.3m, 12000000000000000m) })
		{
			using var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { SignificantDigits = digits, ErrorCells = ExcelErrorCells.Null });
			var orders = reader.GetRecords<TypedOrder>().ToList();
			Assert.Equal([total, big], orders.Select(o => o.Total));
		}
	}

	[Fact]
	public void ChecksTheOptions()
	{
		using var stream = Typed(false).ToStream();
		using var workbook = ExcelWorkbook.Open(stream);

		var e = Assert.Throws<ArgumentOutOfRangeException>(() => workbook.OpenReader(new ExcelReaderOptions { ErrorCells = (ExcelErrorCells)3 }));
		Assert.StartsWith("ExcelReaderOptions.ErrorCells is not an ExcelErrorCells value.", e.Message);
		e = Assert.Throws<ArgumentOutOfRangeException>(() => workbook.OpenReader(new ExcelReaderOptions { Formulas = (ExcelFormulas)(-1) }));
		Assert.StartsWith("ExcelReaderOptions.Formulas is not an ExcelFormulas value.", e.Message);
		foreach (var digits in new[] { 0, 29, -1 })
		{
			e = Assert.Throws<ArgumentOutOfRangeException>(() => workbook.OpenReader(new ExcelReaderOptions { SignificantDigits = digits }));
			Assert.StartsWith("ExcelReaderOptions.SignificantDigits must be from 1 to 28, or null.", e.Message);
		}

		Assert.Throws<ArgumentOutOfRangeException>(() => ExcelWorkbook.Open(Typed(false).ToStream(), new ExcelReaderOptions { SignificantDigits = 0 }));
		using var reader = workbook.OpenReader(new ExcelReaderOptions { SignificantDigits = 28, Formulas = ExcelFormulas.FormulaText });
		Assert.Equal(ExcelErrorCells.Text, ExcelReaderOptions.Default.ErrorCells);
		Assert.Equal(15, ExcelReaderOptions.Default.SignificantDigits);
		Assert.Equal(ExcelFormulas.CachedValue, ExcelReaderOptions.Default.Formulas);
	}
}
