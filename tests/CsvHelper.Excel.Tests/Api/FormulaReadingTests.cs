namespace CsvHelper.Excel.Tests.Api;

/// <summary>A row of a sheet of formulas, read as text.</summary>
[CsvRecord]
public partial class FormulaRow
{
	/// <summary>A text cell.</summary>
	public string Item { get; set; } = "";

	/// <summary>A number cell, or a formula.</summary>
	public string Price { get; set; } = "";

	/// <summary>A shared formula, or an error cell's formula.</summary>
	public string Total { get; set; } = "";
}

/// <summary>
/// Formulas end to end: a sheet of a shared formula, an own formula and an error cell's formula, read as cached values and
/// as text, synchronously and asynchronously, through fields, generated mappers, dynamic records and parallel workers.
/// </summary>
public class FormulaReadingTests
{
	/// <summary>The header, three rows whose totals are a shared formula, and a row of an own formula and an error's.</summary>
	private const string Rows =
		"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Item</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>Price</t></is></c>" +
		"<c r=\"C1\" t=\"inlineStr\"><is><t>Total</t></is></c></row>" +
		"<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>apple</t></is></c><c r=\"B2\"><v>1.5</v></c><c r=\"C2\"><f t=\"shared\" ref=\"C2:C4\" si=\"0\">B2*2</f><v>3</v></c></row>" +
		"<row r=\"3\"><c r=\"A3\" t=\"inlineStr\"><is><t>pear</t></is></c><c r=\"B3\"><v>0.25</v></c><c r=\"C3\"><f t=\"shared\" si=\"0\"/><v>0.5</v></c></row>" +
		"<row r=\"4\"><c r=\"A4\" t=\"inlineStr\"><is><t>plum</t></is></c><c r=\"B4\"><v>2</v></c><c r=\"C4\"><f t=\"shared\" si=\"0\"/><v>4</v></c></row>" +
		"<row r=\"5\"><c r=\"A5\" t=\"inlineStr\"><is><t>sum</t></is></c><c r=\"B5\"><f>SUM(B2:B4)</f><v>3.75</v></c><c r=\"C5\" t=\"e\"><f>1/0</f><v>#DIV/0!</v></c></row>";

	/// <summary>The options that read formulas as text.</summary>
	private static readonly ExcelReaderOptions FormulaText = new() { Formulas = ExcelFormulas.FormulaText };

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsFormulasAsCachedValuesOrText(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Workbooks.Data(Rows), async);
		using (var reader = await workbook.OpenReaderAsync(async))
		{
			Assert.Equal(["apple|1.5|3", "pear|0.25|0.5", "plum|2|4", "sum|3.75|#DIV/0!"], await reader.ReadAllAsync(async));
		}

		using (var reader = await workbook.OpenReaderAsync(async, FormulaText))
		{
			Assert.Equal(["apple|1.5|=B2*2", "pear|0.25|=B3*2", "plum|2|=B4*2", "sum|=SUM(B2:B4)|=1/0"], await reader.ReadAllAsync(async));
		}

		// A formula cell is its formula whatever its value: an error cell's doesn't fail.
		var strict = new ExcelReaderOptions { Formulas = ExcelFormulas.FormulaText, ErrorCells = ExcelErrorCells.Error };
		using (var reader = await workbook.OpenReaderAsync(async, strict))
		{
			var rows = new List<FormulaRow>();
			if (async)
			{
				await foreach (var row in reader.GetRecordsAsync<FormulaRow>())
				{
					rows.Add(row);
				}
			}
			else
			{
				rows.AddRange(reader.GetRecords<FormulaRow>());
			}

			Assert.Equal(["=B2*2", "=B3*2", "=B4*2", "=1/0"], rows.Select(r => r.Total));
			Assert.Equal("=SUM(B2:B4)", rows[3].Price);
		}

		// As a cached value, the error fails.
		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { ErrorCells = ExcelErrorCells.Error }))
		{
			for (var i = 0; i < 4; i++)
			{
				Assert.True(async ? await reader.ReadAsync() : reader.Read());
			}

			Assert.Equal("Data!C5 holds the error #DIV/0!.", Assert.Throws<FormatException>(() => reader.GetString("Total")).Message);
		}
	}

	[Fact]
	public void ReadsFormulasThroughDynamicRecordsAndWorkers()
	{
		using var workbook = ExcelWorkbook.Open(Workbooks.Data(Rows).ToStream());
		using (var reader = workbook.OpenReader(FormulaText))
		{
			var records = reader.GetRecords<CsvDynamicRecord>().Select(r => r.Detach()).ToList();
			Assert.Equal(["=B2*2", "=B3*2", "=B4*2", "=1/0"], records.Select(r => r["Total"]));
			Assert.Equal(1.5, records[0].Parse<double>("Price"));
		}

		using (var reader = workbook.OpenReader(FormulaText))
		{
			var totals = reader.ParallelEnumerate(r => (r.GetString("Item"), r.GetString("Total")), 2).ToList();
			Assert.Equal([("apple", "=B2*2"), ("pear", "=B3*2"), ("plum", "=B4*2"), ("sum", "=1/0")], totals);
		}
	}
}
