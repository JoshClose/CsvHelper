using System.Data;
using CsvHelper.Linq;

namespace CsvHelper.Excel.Tests.Api;

/// <summary>
/// The gate: a sheet's reader works with everything that reads a <see cref="CsvReader"/> (generated mappers, dynamic
/// records, <see cref="CsvDataReader"/>, CsvHelper.Linq, <see cref="CsvReader.ParallelEnumerate{T}(Func{CsvReader, T}, int)"/>,
/// header validation), and its errors name their cell. Each test runs synchronously and asynchronously.
/// </summary>
public class ReadingTests
{
	/// <summary>A sheet whose third row's quantity is text, and whose last row is out of order when <c>broken</c>.</summary>
	private const string Quantities =
		"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Name</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>Quantity</t></is></c></row>" +
		"<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>apple</t></is></c><c r=\"B2\"><v>3</v></c></row>" +
		"<row r=\"3\"><c r=\"A3\" t=\"inlineStr\"><is><t>pear</t></is></c><c r=\"B3\" t=\"inlineStr\"><is><t>lots</t></is></c></row>" +
		"<row r=\"4\"><c r=\"A4\" t=\"inlineStr\"><is><t>plum</t></is></c><c r=\"B4\"><v>7</v></c></row>";

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsRecordsWithAGeneratedMapper(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Workbooks.Orders(), async);
		using var reader = await workbook.OpenReaderAsync(async, "Orders");
		var orders = new List<SheetOrder>();
		if (async)
		{
			await foreach (var order in reader.GetRecordsAsync<SheetOrder>())
			{
				orders.Add(order);
			}
		}
		else
		{
			orders.AddRange(reader.GetRecords<SheetOrder>());
		}

		Assert.Equal(["apple 3 1.5 True first", "pear 10 0.25 False ", "plum 7 2 True third"], orders.Select(o => $"{o.Name} {o.Quantity} {o.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)} {o.Paid} {o.Note}"));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsDynamicRecords(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Workbooks.Orders(), async);
		using var reader = await workbook.OpenReaderAsync(async, "Orders");
		var records = reader.GetRecords<CsvDynamicRecord>().Select(r => r.Detach()).ToList();

		Assert.Equal(3, records.Count);
		Assert.Equal("pear", records[1]["Name"]);
		Assert.Equal("10", records[1]["Quantity"]);
		Assert.Equal("", records[1]["Note"]);
		Assert.Equal(1.5m, records[0].Parse<decimal>("Price"));
		Assert.True(records[2].Parse<bool>("Paid"));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task LoadsADataTable(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Workbooks.Orders(), async);
		using var reader = await workbook.OpenReaderAsync(async, "Orders");
		using var data = new CsvDataReader(reader);
		var table = new DataTable();
		table.Load(data);

		Assert.Equal(["Name", "Quantity", "Price", "Paid", "Note"], table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
		Assert.Equal(Workbooks.OrderRows, table.Rows.Cast<DataRow>().Select(r => string.Join("|", r.ItemArray)));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task QueriesWithLinq(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Workbooks.Orders(), async);
		using var reader = await workbook.OpenReaderAsync(async, "Orders");

		Assert.Equal(["apple", "plum"], reader.Query<SheetOrder>().Where(o => o.Paid).Select(o => o.Name).ToList());
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task EnumeratesInParallel(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Workbooks.Data(Workbooks.Numbers(5000)), async);
		using var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { Csv = new() { BufferSize = 256 } });

		Assert.Equal(Enumerable.Range(1, 5000), reader.ParallelEnumerate(r => r.Parse<int>(0), 4));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ValidatesTheHeader(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Workbooks.Orders(), async);
		using var reader = await workbook.OpenReaderAsync(async, "Orders");

		var e = Assert.Throws<KeyNotFoundException>(() => reader.GetRecords<SheetOrderWithCustomer>().ToList());
		Assert.Contains("Customer", e.Message);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NamesTheCellOfAFieldThatDoesntParse(bool async)
	{
		foreach (var (sheet, cell) in new[] { ("Data", "Data!B3"), ("Q1 Orders", "'Q1 Orders'!B3") })
		{
			var package = TestPackage.Workbook(new TestSheet(sheet, xml: Sheet.Sheets.Worksheet(Quantities)));
			using var workbook = await Workbooks.OpenAsync(package, async);

			using (var reader = await workbook.OpenReaderAsync(async))
			{
				var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<SheetQuantity>().ToList());
				Assert.Equal(cell, e.Location);
				Assert.Equal(3, e.LineNumber);
				Assert.Contains($"({cell})", e.Message);
			}

			var errors = new List<CsvRecordError>();
			var options = new ExcelReaderOptions { Csv = new() { OnRecordError = error => { errors.Add(error); return true; } } };
			using (var reader = await workbook.OpenReaderAsync(async, options))
			{
				Assert.Equal(["apple", "plum"], reader.GetRecords<SheetQuantity>().Select(q => q.Name));
				Assert.Equal(cell, Assert.Single(errors).Location);
			}
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ThrowsTheSheetsErrorsFromReadNotOnRecordError(bool async)
	{
		var broken = Workbooks.Data(Quantities + "<row r=\"2\"><c r=\"A2\"><v>1</v></c></row>");
		using var workbook = await Workbooks.OpenAsync(broken, async);
		var errors = new List<CsvRecordError>();
		var options = new ExcelReaderOptions { Csv = new() { OnRecordError = error => { errors.Add(error); return true; } } };

		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<SheetQuantity>().ToList());
			Assert.StartsWith("Row Data!2:2 can't be read at byte", e.Message);
			Assert.Equal("Data!2:2", e.Location);
		}

		// The sheet's error ends the read in the fill that finds it: no handler sees it, nor the records of that fill.
		Assert.Empty(errors);

		using (var reader = await workbook.OpenReaderAsync(async, options))
		{
			var e = await Assert.ThrowsAsync<CsvFormatException>(() => reader.ReadAllAsync(async));
			Assert.Equal("Data!2:2", e.Location);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ParsesNumbersInvariantlyAndTextInTheReadersCulture(bool async)
	{
		var package = TestPackage.Workbook(new TestSheet("Data", xml: Sheet.Sheets.Worksheet(
			"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Name</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>Price</t></is></c></row>" +
			"<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>number</t></is></c><c r=\"B2\"><v>1.5</v></c></row>" +
			"<row r=\"3\"><c r=\"A3\" t=\"inlineStr\"><is><t>text</t></is></c><c r=\"B3\" t=\"inlineStr\"><is><t>2,25</t></is></c></row>")));
		using var workbook = await Workbooks.OpenAsync(package, async);
		var german = new ExcelReaderOptions { Csv = new() { FormatProvider = new System.Globalization.CultureInfo("de-DE") } };
		using var reader = await workbook.OpenReaderAsync(async, german);
		var prices = new List<decimal>();
		while (async ? await reader.ReadAsync() : reader.Read())
		{
			prices.Add(reader.Parse<decimal>(1));
		}

		Assert.Equal([1.5m, 2.25m], prices);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task SkipsMissingRowsWithoutCountingThem(bool async)
	{
		// With empty rows skipped, a row a million down costs nothing for the rows before it.
		var far = Workbooks.Data("<row r=\"1000000\"><c r=\"A1000000\"><v>1</v></c></row>");
		var size = far.Text(Sheet.Sheets.PartName).Length;
		using var workbook = await Workbooks.OpenAsync(far, async);
		var limits = new ExcelReaderOptions { MaxUncompressedSize = 100_000, Csv = new() { HasHeader = false } };

		using (var reader = await workbook.OpenReaderAsync(async, limits))
		{
			Assert.Equal(["1"], await reader.ReadAllAsync(async));
		}

		using (var reader = await workbook.OpenReaderAsync(async, limits with { Csv = limits.Csv with { SkipEmptyLines = false } }))
		{
			var e = await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAllAsync(async));
			Assert.StartsWith("Reading xl/worksheets/sheet1.xml takes the workbook past ExcelReaderOptions.MaxUncompressedSize", e.Message);
		}

		Assert.True(size < 1000);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task AppliesTheCsvOptions(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Workbooks.Orders(), async);

		// Row 4 is missing: with empty lines kept, it is a record of one empty field.
		using (var reader = await workbook.OpenReaderAsync(async, "Orders", new ExcelReaderOptions { Csv = new() { SkipEmptyLines = false } }))
		{
			Assert.Equal([Workbooks.OrderRows[0], Workbooks.OrderRows[1], "", Workbooks.OrderRows[2]], await reader.ReadAllAsync(async));
		}

		var padded = Workbooks.Data("<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t xml:space=\"preserve\"> padded </t></is></c><c r=\"B1\"><v> 2 </v></c></row>");
		using var paddedWorkbook = await Workbooks.OpenAsync(padded, async);
		using (var reader = await paddedWorkbook.OpenReaderAsync(async, new ExcelReaderOptions { Csv = new() { HasHeader = false, TrimWhitespace = true } }))
		{
			Assert.Equal(["padded|2"], await reader.ReadAllAsync(async));
		}

		using (var reader = await paddedWorkbook.OpenReaderAsync(async, new ExcelReaderOptions { Csv = new() { HasHeader = false } }))
		{
			Assert.Equal([" padded |2"], await reader.ReadAllAsync(async));
		}
	}
}
