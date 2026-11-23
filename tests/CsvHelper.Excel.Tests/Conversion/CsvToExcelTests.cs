using System.Globalization;
using System.Text;
using CsvHelper.Excel.Tests.Writing;

namespace CsvHelper.Excel.Tests.Conversion;

/// <summary>An order, read from CSV and written as its type's cells by <see cref="ExcelConvert.ToExcel{T}(string, string, CsvToExcelOptions?)"/>.</summary>
[CsvRecord]
public partial class ConvertedOrder
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A number cell.</summary>
	public int Quantity { get; set; }

	/// <summary>A number cell.</summary>
	public decimal Price { get; set; }

	/// <summary>A date cell.</summary>
	public DateTime Ordered { get; set; }

	/// <summary>A boolean cell.</summary>
	public bool Paid { get; set; }
}

/// <summary>
/// <see cref="ExcelConvert"/>'s conversions of CSV to a sheet (Phase 6), and
/// <see cref="ExcelWorkbookWriter.WriteSheet(string, CsvReader, ExcelSheetOptions?, int)"/>: each column's type inferred
/// from its first records, the options of both sides, the sheet's name, header and look, files and streams, a reader into a
/// workbook beside other sheets, and a type's records. Each test runs synchronously and asynchronously.
/// </summary>
public class CsvToExcelTests
{
	/// <summary>A CSV of a column of each type, and one of text that looks like a number, and one with an empty field.</summary>
	private const string TypedCsv =
		"Name,Qty,Price,Day,At,Paid,Code,Note\r\n" +
		"apple,3,1.5,2024-01-15,2024-01-15 10:30,true,00123,\r\n" +
		"pear,-4,2.25,2024-01-16,2024-01-16,FALSE,123,x\r\n";

	public static TheoryData<bool> Modes => new() { false, true };

	/// <summary>Converts CSV through the stream overloads.</summary>
	private static async Task<byte[]> ConvertAsync(string csv, bool async, CsvToExcelOptions? options = null)
	{
		using var input = new MemoryStream(Encoding.UTF8.GetBytes(csv));
		using var output = new MemoryStream();
		if (async)
		{
			await ExcelConvert.ToExcelAsync(input, output, options);
		}
		else
		{
			ExcelConvert.ToExcel(input, output, options);
		}

		return output.ToArray();
	}

	/// <summary>A workbook's first sheet's cells with a value, as ClosedXML reads their types and values: <c>B2=Number:3</c>.</summary>
	private static List<string> Values(byte[] workbook) => Written.ClosedXmlValues(workbook);

	/// <summary>A new temporary file's path, which the caller deletes.</summary>
	private static string NewPath(string name) => Path.Combine(Path.GetTempPath(), $"CsvHelper.Excel.Tests.{Guid.NewGuid():N}", name);

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task InfersEachColumnsTypeFromItsFields(bool async)
	{
		var workbook = await ConvertAsync(TypedCsv, async);

		Assert.Equal(
			[
				"A1=Text:Name", "B1=Text:Qty", "C1=Text:Price", "D1=Text:Day", "E1=Text:At", "F1=Text:Paid", "G1=Text:Code", "H1=Text:Note",
				"A2=Text:apple", "B2=Number:3", "C2=Number:1.5", "D2=DateTime:2024-01-15T00:00:00.000", "E2=DateTime:2024-01-15T10:30:00.000",
				"F2=Boolean:TRUE", "G2=Text:00123",
				"A3=Text:pear", "B3=Number:-4", "C3=Number:2.25", "D3=DateTime:2024-01-16T00:00:00.000", "E3=DateTime:2024-01-16T00:00:00.000",
				"F3=Boolean:FALSE", "G3=Text:123", "H3=Text:x",
			],
			Values(workbook));

		// Each date in the format of what it holds, as the reader gives them back.
		Assert.Equal(
			[
				"Name|Qty|Price|Day|At|Paid|Code|Note",
				"apple|3|1.5|2024-01-15|2024-01-15T10:30:00|TRUE|00123|",
				"pear|-4|2.25|2024-01-16|2024-01-16|FALSE|123|x",
			],
			Written.ReadOne(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task KeepsAsTextWhatExcelWouldChange(bool async)
	{
		string[] fields = ["00123", "1234567890123456", "1,234", "+45", ".5", "5.", "$5", "50%", "10:30", "36:00:00", "2024-01-15T10:30:00Z", "1/15/2024", "1900-02-29", "#N/A", "=1+1", "NaN"];
		var csv = string.Join(",", fields.Select((_, i) => "c" + i)) + "\r\n" + string.Join(",", fields.Select(f => f.Contains(',') ? "\"" + f + "\"" : f)) + "\r\n";

		var workbook = await ConvertAsync(csv, async);

		Assert.Equal(fields.Select((f, i) => $"{CellReference.ColumnName(i)}2=Text:{f}"), Values(workbook).Skip(fields.Length));

		// Text that starts like a number gets no warning triangle.
		Assert.Contains("<ignoredErrors>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"), StringComparison.Ordinal);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task TypesAColumnByItsFirstRecords(bool async)
	{
		const string Csv = "note,id,qty\r\n,1,1\r\n,2,2\r\n5,00123,N/A\r\n6,4,4,7\r\n";

		// Two records decide: 00123 and N/A come after them, and are text cells in columns of numbers. The notes, null in
		// both, are text, and so is the fourth column, which no record held has.
		var two = await ConvertAsync(Csv, async, new CsvToExcelOptions { InferTypes = 2 });
		Assert.Equal(
			[
				"A1=Text:note", "B1=Text:id", "C1=Text:qty",
				"B2=Number:1", "C2=Number:1", "B3=Number:2", "C3=Number:2",
				"A4=Text:5", "B4=Text:00123", "C4=Text:N/A",
				"A5=Text:6", "B5=Number:4", "C5=Number:4", "D5=Text:7",
			],
			Values(two));

		// All of them decide: the notes are numbers, 00123 makes the IDs text, and N/A the quantities.
		var all = await ConvertAsync(Csv, async);
		Assert.Equal(
			[
				"A1=Text:note", "B1=Text:id", "C1=Text:qty",
				"B2=Text:1", "C2=Text:1", "B3=Text:2", "C3=Text:2",
				"A4=Number:5", "B4=Text:00123", "C4=Text:N/A",
				"A5=Number:6", "B5=Text:4", "C5=Text:4", "D5=Number:7",
			],
			Values(all));

		// None decide: every field is text, as the CSV has it.
		var none = await ConvertAsync(TypedCsv, async, new CsvToExcelOptions { InferTypes = 0 });
		Assert.Equal(
			[
				"A1=Text:Name", "B1=Text:Qty", "C1=Text:Price", "D1=Text:Day", "E1=Text:At", "F1=Text:Paid", "G1=Text:Code", "H1=Text:Note",
				"A2=Text:apple", "B2=Text:3", "C2=Text:1.5", "D2=Text:2024-01-15", "E2=Text:2024-01-15 10:30", "F2=Text:true", "G2=Text:00123",
				"A3=Text:pear", "B3=Text:-4", "C3=Text:2.25", "D3=Text:2024-01-16", "E3=Text:2024-01-16", "F3=Text:FALSE", "G3=Text:123", "H3=Text:x",
			],
			Values(none));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task HoldsEveryRecordItInfersFrom(bool async)
	{
		// More records, fields and bytes than the held records' first arrays take, so that each of them grows.
		var rows = Enumerable.Range(0, 600)
			.Select(r => Enumerable.Range(0, 12).Select(c => c == 0 ? r.ToString(CultureInfo.InvariantCulture) : $"r{r}c{c}" + new string('x', (r + c) % 7)).ToArray())
			.ToList();
		var csv = string.Concat(rows.Select(r => string.Join(",", r) + "\r\n"));
		var options = new CsvToExcelOptions { Csv = new CsvReaderOptions { HasHeader = false }, Excel = Written.Phase4() };

		var workbook = await ConvertAsync(csv, async, options);

		Assert.Equal(rows.Select(r => string.Join("|", r)), Written.ReadOne(workbook));
		var values = Values(workbook);
		Assert.Equal(["A1=Number:0", "B1=Text:r0c1x"], values.Take(2));
		Assert.Equal("L600=Text:r599c11x", values[values.Count - 1]);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesNullFieldsAsNoCells(bool async)
	{
		const string Csv = "n,t,e\r\n1,x,\r\nNULL,n/a,\r\n2,y,NULL\r\n";
		var options = new CsvToExcelOptions { Csv = new CsvReaderOptions { NullValues = ["NULL", "n/a"] } };

		var workbook = await ConvertAsync(Csv, async, options);

		// The numbers are still numbers, and a column of nulls only has no cells.
		Assert.Equal(["A1=Text:n", "B1=Text:t", "C1=Text:e", "A2=Number:1", "B2=Text:x", "A4=Number:2", "B4=Text:y"], Values(workbook));

		// Without null values, NULL is text, and makes its column text.
		var plain = await ConvertAsync(Csv, async);
		Assert.Contains("A3=Text:NULL", Values(plain));
		Assert.Contains("A2=Text:1", Values(plain));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ReadsNumbersInTheCsvsCulture(bool async)
	{
		const string Csv = "a;b;c\r\n1,5;1.5;-2,5E3\r\n-2,25;2.5;0\r\n";
		var options = new CsvToExcelOptions
		{
			Csv = new CsvReaderOptions { Separator = ';', FormatProvider = new CultureInfo("de-DE") },
			// The workbook's culture is for record writers: it doesn't change the CSV's numbers.
			Excel = new ExcelWriterOptions { Csv = new CsvWriterOptions { FormatProvider = new CultureInfo("fr-FR") } },
		};

		var workbook = await ConvertAsync(Csv, async, options);

		Assert.Equal(
			["A1=Text:a", "B1=Text:b", "C1=Text:c", "A2=Number:1.5", "B2=Text:1.5", "C2=Number:-2500", "A3=Number:-2.25", "B3=Text:2.5", "C3=Number:0"],
			Values(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FollowsLongNumbers(bool async)
	{
		const string Csv = "id\r\n1234567890123456\r\n";

		Assert.Equal(["A1=Text:id", "A2=Text:1234567890123456"], Values(await ConvertAsync(Csv, async)));
		Assert.Equal(
			["A1=Text:id", "A2=Number:1234567890123456"],
			Values(await ConvertAsync(Csv, async, new CsvToExcelOptions { Excel = new ExcelWriterOptions { LongNumbers = ExcelLongNumbers.Number } })));

		var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			ConvertAsync(Csv, async, new CsvToExcelOptions { Excel = new ExcelWriterOptions { LongNumbers = ExcelLongNumbers.Throw } }));
		Assert.Equal("Cell Sheet1!A2 holds 1234567890123456, which has 16 significant digits, and Excel keeps 15 (ExcelWriterOptions.LongNumbers is Throw).", thrown.Message);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FollowsOutOfRangeDates(bool async)
	{
		const string Csv = "d,dt\r\n1899-12-31,1899-12-31 10:30\r\n2024-01-15,2024-01-15 10:30\r\n";

		// A date before 1900 is a text cell of its own text, in a column of dates.
		Assert.Equal(
			["A1=Text:d", "B1=Text:dt", "A2=Text:1899-12-31", "B2=Text:1899-12-31 10:30", "A3=DateTime:2024-01-15T00:00:00.000", "B3=DateTime:2024-01-15T10:30:00.000"],
			Values(await ConvertAsync(Csv, async)));

		var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			ConvertAsync(Csv, async, new CsvToExcelOptions { Excel = new ExcelWriterOptions { OutOfRangeDates = ExcelOutOfRangeDates.Throw } }));
		Assert.Equal("Cell Sheet1!A2 holds 1899-12-31, before 1900-01-01, Excel's first date (ExcelWriterOptions.OutOfRangeDates is Throw).", thrown.Message);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FollowsDateTimeOffsets(bool async)
	{
		const string Csv = "at\r\n2024-01-15T10:30:00+01:00\r\n2024-01-15 10:30Z\r\n";

		Assert.Equal(["A1=Text:at", "A2=Text:2024-01-15T10:30:00+01:00", "A3=Text:2024-01-15 10:30Z"], Values(await ConvertAsync(Csv, async)));
		Assert.Equal(
			["A1=Text:at", "A2=DateTime:2024-01-15T09:30:00.000", "A3=DateTime:2024-01-15T10:30:00.000"],
			Values(await ConvertAsync(Csv, async, new CsvToExcelOptions { Excel = new ExcelWriterOptions { DateTimeOffsets = ExcelDateTimeOffsets.Utc } })));
		Assert.Equal(
			["A1=Text:at", "A2=DateTime:2024-01-15T10:30:00.000", "A3=DateTime:2024-01-15T10:30:00.000"],
			Values(await ConvertAsync(Csv, async, new CsvToExcelOptions { Excel = new ExcelWriterOptions { DateTimeOffsets = ExcelDateTimeOffsets.ClockTime } })));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task TakesTheHeaderFromTheCsv(bool async)
	{
		const string Csv = "123,2024-01-15\r\n1,2024-01-16\r\n";

		// A header: its names are text, bold and frozen, whatever the workbook's CSV options say.
		var header = await ConvertAsync(Csv, async, new CsvToExcelOptions { Excel = new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false } } });
		Assert.Equal(["A1=Text:123", "B1=Text:2024-01-15", "A2=Number:1", "B2=DateTime:2024-01-16T00:00:00.000"], Values(header));
		var xml = Written.Entry(header, "xl/worksheets/sheet1.xml");
		Assert.Contains("<pane ySplit=\"1\"", xml, StringComparison.Ordinal);
		Assert.Contains("<sheetData><row><c s=\"", xml, StringComparison.Ordinal);

		// None: the first record is data, typed, and nothing is frozen.
		var none = await ConvertAsync(Csv, async, new CsvToExcelOptions { Csv = new CsvReaderOptions { HasHeader = false } });
		Assert.Equal(["A1=Number:123", "B1=DateTime:2024-01-15T00:00:00.000", "A2=Number:1", "B2=DateTime:2024-01-16T00:00:00.000"], Values(none));
		Assert.DoesNotContain("<pane", Written.Entry(none, "xl/worksheets/sheet1.xml"), StringComparison.Ordinal);

		// A header alone, and no CSV at all.
		Assert.Equal(["Sheet1: a|b"], Written.Sheets(await ConvertAsync("a,b\r\n", async)));
		Assert.Equal(["Sheet1: "], Written.Sheets(await ConvertAsync("", async)));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesRecordsOfAnyWidth(bool async)
	{
		var workbook = await ConvertAsync("a,b\r\n1\r\n2,3,4\r\n5,6\r\n", async);

		Assert.Equal(["A1=Text:a", "B1=Text:b", "A2=Number:1", "A3=Number:2", "B3=Number:3", "C3=Number:4", "A4=Number:5", "B4=Number:6"], Values(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FollowsTheSheetsLook(bool async)
	{
		var table = await ConvertAsync(TypedCsv, async, new CsvToExcelOptions { Sheet = new ExcelSheetOptions { Table = new ExcelTableOptions(), AutoFit = 0 } });
		Assert.Contains("name=\"Sheet1\"", Written.Entry(table, "xl/tables/table1.xml"), StringComparison.Ordinal);
		Assert.Empty(Written.ValidationErrors(table));

		// A look that needs a header the CSV doesn't have is refused before anything is written.
		var noHeader = new CsvReaderOptions { HasHeader = false };
		var given = await Assert.ThrowsAsync<ArgumentException>(() =>
			ConvertAsync(TypedCsv, async, new CsvToExcelOptions { Csv = noHeader, Sheet = new ExcelSheetOptions { Table = new ExcelTableOptions() } }));
		Assert.Equal("options", given.ParamName);
		Assert.StartsWith("CsvToExcelOptions.Sheet: A table needs a header, and CsvReaderOptions.HasHeader is off.", given.Message, StringComparison.Ordinal);

		var defaults = await Assert.ThrowsAsync<ArgumentException>(() =>
			ConvertAsync(TypedCsv, async, new CsvToExcelOptions { Csv = noHeader, Excel = new ExcelWriterOptions { Sheets = new ExcelSheetOptions { AutoFilter = true } } }));
		Assert.StartsWith("ExcelWriterOptions.Sheets: AutoFilter needs a header, and CsvReaderOptions.HasHeader is off.", defaults.Message, StringComparison.Ordinal);

		// So is it when a sheet of a CSV reader is added, which adds no sheet.
		using var reader = CsvReader.Create(Encoding.UTF8.GetBytes("1,2\r\n"), noHeader);
		await Written.WorkbookAsync(
			(writer, a) =>
			{
				var sheet = Assert.Throws<ArgumentException>(() => writer.WriteSheet("Data", reader, new ExcelSheetOptions { Table = new ExcelTableOptions() }));
				Assert.Equal("options", sheet.ParamName);
				Assert.StartsWith("ExcelSheetOptions: A table needs a header, and CsvReaderOptions.HasHeader is off.", sheet.Message, StringComparison.Ordinal);
				var workbook = Assert.Throws<InvalidOperationException>(() => writer.WriteSheet("Data", reader));
				Assert.Equal("ExcelWriterOptions.Sheets: AutoFilter needs a header, and CsvReaderOptions.HasHeader is off.", workbook.Message);
				Assert.Empty(writer.SheetNames);

				return Task.CompletedTask;
			},
			async,
			new ExcelWriterOptions { Sheets = new ExcelSheetOptions { AutoFilter = true } });
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task NamesTheSheet(bool async)
	{
		var csvPath = NewPath("Orders [2024].csv");
		var excelPath = Path.Combine(Path.GetDirectoryName(csvPath)!, "orders.xlsx");
		Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);
		try
		{
			File.WriteAllText(csvPath, "a\r\n1\r\n");
			await ToExcelAsync(csvPath, excelPath, async);
			Assert.Equal(["Orders (2024): a / 1"], Written.Sheets(File.ReadAllBytes(excelPath)));

			await ToExcelAsync(csvPath, excelPath, async, new CsvToExcelOptions { SheetName = "Data" });
			Assert.Equal(["Data: a / 1"], Written.Sheets(File.ReadAllBytes(excelPath)));

			// A name Excel doesn't open is refused before anything is written.
			File.Delete(excelPath);
			var thrown = await Assert.ThrowsAsync<ArgumentException>(() => ToExcelAsync(csvPath, excelPath, async, new CsvToExcelOptions { SheetName = "a/b" }));
			Assert.Equal("options", thrown.ParamName);
			Assert.False(File.Exists(excelPath));
		}
		finally
		{
			Directory.Delete(Path.GetDirectoryName(csvPath)!, true);
		}

		Assert.Equal(["Data: a / 1"], Written.Sheets(await ConvertAsync("a\r\n1\r\n", async, new CsvToExcelOptions { SheetName = "Data" })));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ConvertsFilesAndDeletesOneThatFails(bool async)
	{
		var csvPath = NewPath("data.csv");
		var folder = Path.GetDirectoryName(csvPath)!;
		var excelPath = Path.Combine(folder, "data.xlsx");
		Directory.CreateDirectory(folder);
		try
		{
			File.WriteAllText(csvPath, TypedCsv);
			await ToExcelAsync(csvPath, excelPath, async);
			Assert.Equal(Values(await ConvertAsync(TypedCsv, async)), Values(File.ReadAllBytes(excelPath)));
			Assert.Equal("data", Assert.Single(Written.Sheets(File.ReadAllBytes(excelPath))).Split(':')[0]);

			// Both files are closed: they can be opened exclusively.
			File.Open(csvPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None).Dispose();
			File.Open(excelPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None).Dispose();

			// The CSV file is never the workbook.
			var same = await Assert.ThrowsAsync<ArgumentException>(() => ToExcelAsync(csvPath, csvPath.ToUpperInvariant(), async));
			Assert.Equal("excelPath", same.ParamName);
			Assert.Equal(TypedCsv, File.ReadAllText(csvPath));

			// A CSV that can't be opened, and options that can't be used, leave a workbook of that name as it was.
			File.WriteAllText(excelPath, "old");
			await Assert.ThrowsAsync<FileNotFoundException>(() => ToExcelAsync(Path.Combine(folder, "missing.csv"), excelPath, async));
			await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ToExcelAsync(csvPath, excelPath, async, new CsvToExcelOptions { InferTypes = -1 }));
			Assert.Equal("old", File.ReadAllText(excelPath));

			// A conversion that fails deletes its workbook.
			File.WriteAllText(csvPath, "id\r\n1234567890123456\r\n");
			await Assert.ThrowsAsync<InvalidOperationException>(() =>
				ToExcelAsync(csvPath, excelPath, async, new CsvToExcelOptions { Excel = new ExcelWriterOptions { LongNumbers = ExcelLongNumbers.Throw } }));
			Assert.False(File.Exists(excelPath));

			// So does the CSV reader's failure.
			File.WriteAllText(csvPath, "a,b\r\n1,2\r\n3\r\n");
			await Assert.ThrowsAsync<CsvFormatException>(() =>
				ToExcelAsync(csvPath, excelPath, async, new CsvToExcelOptions { Csv = new CsvReaderOptions { FieldCountMismatch = CsvFieldCountHandling.Throw } }));
			Assert.False(File.Exists(excelPath));

			if (async)
			{
				using var canceled = new CancellationTokenSource();
				canceled.Cancel();
				await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExcelConvert.ToExcelAsync(csvPath, excelPath, null, canceled.Token).AsTask());
				Assert.False(File.Exists(excelPath));
			}
		}
		finally
		{
			Directory.Delete(folder, true);
		}
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesStreamsFromTheirPositionAndLeavesThemOpen(bool async)
	{
		var prefix = Encoding.UTF8.GetBytes("skipped\r\n");
		using var csv = new MemoryStream([.. prefix, .. Encoding.UTF8.GetBytes(TypedCsv)]);
		csv.Position = prefix.Length;
		// The workbook goes after bytes already written, through a stream that can't seek, so that the zip's offsets count
		// from its own start and it reads back on its own (a seekable stream's count from the stream's).
		using var memory = new MemoryStream();
		memory.Write([1, 2, 3], 0, 3);
		using var excel = new ForwardOnlyStream(memory);
		await ToExcelAsync(csv, excel, async);

		Assert.True(csv.CanRead);
		Assert.True(excel.CanWrite);
		Assert.Equal([1, 2, 3], memory.ToArray().Take(3));
		Assert.Equal(Written.Sheets(await ConvertAsync(TypedCsv, async)), Written.Sheets(memory.ToArray().Skip(3).ToArray()));
		Assert.Empty(Written.ValidationErrors(memory.ToArray().Skip(3).ToArray()));

		// A failure leaves them open too.
		using var failing = new MemoryStream(Encoding.UTF8.GetBytes("id\r\n1234567890123456\r\n"));
		using var output = new MemoryStream();
		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			ToExcelAsync(failing, output, async, new CsvToExcelOptions { Excel = new ExcelWriterOptions { LongNumbers = ExcelLongNumbers.Throw } }));
		Assert.True(failing.CanRead);
		Assert.True(output.CanWrite);

		// Streams that can't be read or written.
		using var closed = new MemoryStream();
		closed.Dispose();
		Assert.Equal("csv", (await Assert.ThrowsAsync<ArgumentException>(() => ToExcelAsync(closed, new MemoryStream(), async))).ParamName);
		Assert.Equal("excel", (await Assert.ThrowsAsync<ArgumentException>(() => ToExcelAsync(new MemoryStream(), new MemoryStream([], false), async))).ParamName);
		await Assert.ThrowsAsync<ArgumentNullException>(() => ToExcelAsync(null!, new MemoryStream(), async));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesASheetOfACsvReaderBesideOthers(bool async)
	{
		using var first = CsvReader.Create(Encoding.UTF8.GetBytes("a,b\r\n1,x\r\n2,y\r\n"));
		using var second = CsvReader.Create(Encoding.UTF8.GetBytes("n\r\n1\r\n2\r\n3\r\n"));
		using var noHeader = CsvReader.Create(Encoding.UTF8.GetBytes("true,2024-01-15\r\n"), new CsvReaderOptions { HasHeader = false });

		// The second is on its first record, which has been read: the sheet has the records after it.
		Assert.True(second.Read());
		var workbook = await Written.WorkbookAsync(
			async (writer, a) =>
			{
				writer.WriteSheet("Orders", new[] { new ConvertedOrder { Name = "apple", Quantity = 3, Ordered = new DateTime(2024, 1, 15) } });
				if (a)
				{
					await writer.WriteSheetAsync("First", first);
					await writer.WriteSheetAsync("Second", second, new ExcelSheetOptions { BoldHeader = false });
					await writer.WriteSheetAsync("Text", noHeader, inferTypes: 0);
				}
				else
				{
					writer.WriteSheet("First", first);
					writer.WriteSheet("Second", second, new ExcelSheetOptions { BoldHeader = false });
					writer.WriteSheet("Text", noHeader, inferTypes: 0);
				}
			},
			async);

		Assert.Equal(
			["Orders: Name|Quantity|Price|Ordered|Paid / apple|3|0|2024-01-15|FALSE", "First: a|b / 1|x / 2|y", "Second: n / 2 / 3", "Text: true|2024-01-15"],
			Written.Sheets(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));

		// The readers aren't disposed: they are at their ends.
		Assert.False(first.Read());
		Assert.False(second.Read());
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ReadsTheHeaderAsTheModeReads(bool async)
	{
		var bytes = Encoding.UTF8.GetBytes("a,b\r\n1,2\r\n");
		if (!async)
		{
			// A stream that can only be read asynchronously fails a synchronous conversion, at the header.
			await Assert.ThrowsAsync<NotSupportedException>(() => ToExcelAsync(new AsyncOnlyStream(bytes), new MemoryStream(), async));

			return;
		}

		using var output = new MemoryStream();
		await ExcelConvert.ToExcelAsync(new AsyncOnlyStream(bytes), output);
		Assert.Equal(["A1=Text:a", "B1=Text:b", "A2=Number:1", "B2=Number:2"], Values(output.ToArray()));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task DiscardsTheRecordThatFailsAndCompletesTheSheet(bool async)
	{
		var csv = "h1,h2\r\n1,a\r\n2,b\r\n3," + new string('x', 32768) + "\r\n4,d\r\n";
		using var reader = CsvReader.Create(Encoding.UTF8.GetBytes(csv));
		using var after = CsvReader.Create(Encoding.UTF8.GetBytes("z\r\n"));
		InvalidOperationException? thrown = null;
		var workbook = await Written.WorkbookAsync(
			async (writer, a) =>
			{
				try
				{
					if (a)
					{
						await writer.WriteSheetAsync("Data", reader);
					}
					else
					{
						writer.WriteSheet("Data", reader);
					}
				}
				catch (InvalidOperationException e)
				{
					thrown = e;
				}

				// The sheet is complete: another can be added.
				writer.WriteSheet("After", after);
			},
			async);

		Assert.Equal("Cell Data!B4 has 32,768 characters, and Excel opens no cell of more than 32,767.", thrown?.Message);
		Assert.Equal(["Data: h1|h2 / 1|a / 2|b", "After: z"], Written.Sheets(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FollowsTheRowLimit(bool async)
	{
		using var reader = CsvReader.Create(Encoding.UTF8.GetBytes("n\r\n1\r\n2\r\n3\r\n4\r\n"));
		var workbook = await Written.WorkbookAsync(
			async (writer, a) =>
			{
				writer.MaxRows = 3;
				if (a)
				{
					await writer.WriteSheetAsync("Data", reader);
				}
				else
				{
					writer.WriteSheet("Data", reader);
				}
			},
			async,
			new ExcelWriterOptions { RowLimit = ExcelRowLimit.NewSheet });

		Assert.Equal(["Data: n / 1 / 2", "Data (2): n / 3 / 4"], Written.Sheets(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesATypesRecords(bool async)
	{
		const string Csv = "Name,Quantity,Price,Ordered,Paid\r\napple,3,1.50,2024-01-15T10:30:00,true\r\n";

		// T's cells, typed by its members, whatever InferTypes says; T's header, as the workbook writer's options say.
		var records = await OrdersAsync(Csv, async, new CsvToExcelOptions { InferTypes = 0 });
		Assert.Equal(
			[
				"A1=Text:Name", "B1=Text:Quantity", "C1=Text:Price", "D1=Text:Ordered", "E1=Text:Paid",
				"A2=Text:apple", "B2=Number:3", "C2=Number:1.5", "D2=DateTime:2024-01-15T10:30:00.000", "E2=Boolean:TRUE",
			],
			Values(records));

		var noHeader = await OrdersAsync(Csv, async, new CsvToExcelOptions { Excel = new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false } } });
		Assert.Equal(["A1=Text:apple", "B1=Number:3", "C1=Number:1.5", "D1=DateTime:2024-01-15T10:30:00.000", "E1=Boolean:TRUE"], Values(noHeader));

		// The sheet's look, which needs T's header when the workbook writer writes none.
		var table = await OrdersAsync(Csv, async, new CsvToExcelOptions { Sheet = new ExcelSheetOptions { Table = new ExcelTableOptions() } });
		Assert.Contains("name=\"Sheet1\"", Written.Entry(table, "xl/tables/table1.xml"), StringComparison.Ordinal);
		var refused = await Assert.ThrowsAsync<ArgumentException>(() => OrdersAsync(
			Csv,
			async,
			new CsvToExcelOptions { Sheet = new ExcelSheetOptions { Table = new ExcelTableOptions() }, Excel = new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false } } }));
		Assert.StartsWith("CsvToExcelOptions.Sheet: A table needs a header, and CsvWriterOptions.HasHeader is off.", refused.Message, StringComparison.Ordinal);

		// A CSV without T's columns, here without a header to find them by, is the reader's failure.
		await Assert.ThrowsAsync<KeyNotFoundException>(() =>
			OrdersAsync("pear,4,2,2024-01-16,false\r\n", async, new CsvToExcelOptions { Csv = new CsvReaderOptions { HasHeader = false } }));

		// A file, named after the CSV.
		var csvPath = NewPath("orders.csv");
		var excelPath = Path.ChangeExtension(csvPath, ".xlsx");
		Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);
		try
		{
			File.WriteAllText(csvPath, Csv);
			if (async)
			{
				await ExcelConvert.ToExcelAsync<ConvertedOrder>(csvPath, excelPath);
			}
			else
			{
				ExcelConvert.ToExcel<ConvertedOrder>(csvPath, excelPath);
			}

			Assert.Equal("orders: Name|Quantity|Price|Ordered|Paid / apple|3|1.50|2024-01-15T10:30:00|TRUE", Assert.Single(Written.Sheets(File.ReadAllBytes(excelPath))));

			// A record that can't be read fails the conversion, which deletes its workbook.
			File.WriteAllText(csvPath, "Name,Quantity,Price,Ordered,Paid\r\napple,many,1,2024-01-15,true\r\n");
			if (async)
			{
				await Assert.ThrowsAsync<CsvFormatException>(() => ExcelConvert.ToExcelAsync<ConvertedOrder>(csvPath, excelPath).AsTask());
			}
			else
			{
				Assert.Throws<CsvFormatException>(() => ExcelConvert.ToExcel<ConvertedOrder>(csvPath, excelPath));
			}

			Assert.False(File.Exists(excelPath));
		}
		finally
		{
			Directory.Delete(Path.GetDirectoryName(csvPath)!, true);
		}
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task RefusesANegativeNumberOfRecordsToInferFrom(bool async)
	{
		var options = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ConvertAsync("a\r\n1\r\n", async, new CsvToExcelOptions { InferTypes = -1 }));
		Assert.Equal("options", options.ParamName);
		Assert.StartsWith("InferTypes is how many records decide the columns' types, or 0 for text cells, and it can't be -1.", options.Message, StringComparison.Ordinal);

		using var reader = CsvReader.Create(Encoding.UTF8.GetBytes("a\r\n1\r\n"));
		var workbook = await Written.WorkbookAsync(
			async (writer, a) =>
			{
				var parameter = a
					? await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => writer.WriteSheetAsync("Data", reader, inferTypes: -5).AsTask())
					: Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteSheet("Data", reader, inferTypes: -5));
				Assert.Equal("inferTypes", parameter.ParamName);

				// No sheet was added.
				Assert.Empty(writer.SheetNames);
			},
			async);

		Assert.Equal(["Sheet1: "], Written.Sheets(workbook));
	}

	/// <summary>Converts a CSV file to a workbook as the mode says.</summary>
	private static async Task ToExcelAsync(string csvPath, string excelPath, bool async, CsvToExcelOptions? options = null)
	{
		if (async)
		{
			await ExcelConvert.ToExcelAsync(csvPath, excelPath, options);
		}
		else
		{
			ExcelConvert.ToExcel(csvPath, excelPath, options);
		}
	}

	/// <summary>Converts CSV in a stream to a workbook in another as the mode says.</summary>
	private static async Task ToExcelAsync(Stream csv, Stream excel, bool async, CsvToExcelOptions? options = null)
	{
		if (async)
		{
			await ExcelConvert.ToExcelAsync(csv, excel, options);
		}
		else
		{
			ExcelConvert.ToExcel(csv, excel, options);
		}
	}

	/// <summary>Converts CSV to a workbook of orders through the stream overloads, as the mode says.</summary>
	private static async Task<byte[]> OrdersAsync(string csv, bool async, CsvToExcelOptions? options = null)
	{
		using var input = new MemoryStream(Encoding.UTF8.GetBytes(csv));
		using var output = new MemoryStream();
		if (async)
		{
			await ExcelConvert.ToExcelAsync<ConvertedOrder>(input, output, options);
		}
		else
		{
			ExcelConvert.ToExcel<ConvertedOrder>(input, output, options);
		}

		return output.ToArray();
	}
}
