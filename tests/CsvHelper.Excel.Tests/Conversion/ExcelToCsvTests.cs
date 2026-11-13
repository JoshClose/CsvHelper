using System.Globalization;
using System.Text;
using CsvHelper.Excel.Tests.Api;

namespace CsvHelper.Excel.Tests.Conversion;

/// <summary>
/// <see cref="ExcelConvert"/>'s conversions of a sheet to CSV (Phase 6): each cell's text, the rows as the reader gives
/// them, the options of both sides, files and streams, a reader into a writer, and every worksheet to a file each. Each
/// test runs synchronously and asynchronously.
/// </summary>
public class ExcelToCsvTests
{
	/// <summary>A typed sheet: dates (styles 1 and 2), durations (3), a time alone (4), long numbers, booleans and an error.</summary>
	private const string TypedRows =
		"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Name</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>Ordered</t></is></c>" +
		"<c r=\"C1\" t=\"inlineStr\"><is><t>Took</t></is></c><c r=\"D1\" t=\"inlineStr\"><is><t>Total</t></is></c>" +
		"<c r=\"E1\" t=\"inlineStr\"><is><t>Paid</t></is></c><c r=\"F1\" t=\"inlineStr\"><is><t>Score</t></is></c>" +
		"<c r=\"G1\" t=\"inlineStr\"><is><t>Note</t></is></c></row>" +
		"<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>apple</t></is></c><c r=\"B2\" s=\"1\"><v>45306</v></c><c r=\"C2\" s=\"3\"><v>1.5</v></c>" +
		"<c r=\"D2\"><v>0.30000000000000004</v></c><c r=\"E2\" t=\"b\"><v>1</v></c><c r=\"F2\"><v>7.5</v></c>" +
		"<c r=\"G2\" t=\"inlineStr\"><is><t>first, with a comma</t></is></c></row>" +
		"<row r=\"3\"><c r=\"A3\" t=\"inlineStr\"><is><t>pear</t></is></c><c r=\"B3\" s=\"2\"><v>45307.5</v></c><c r=\"C3\" s=\"3\"><v>0.0625</v></c>" +
		"<c r=\"D3\"><v>12345678901234567</v></c><c r=\"E3\" t=\"b\"><v>0</v></c><c r=\"F3\" t=\"e\"><v>#N/A</v></c></row>" +
		"<row r=\"4\"><c r=\"A4\" t=\"inlineStr\"><is><t>plum</t></is></c><c r=\"B4\" s=\"1\"><v>60</v></c><c r=\"C4\" s=\"4\"><v>0.4375</v></c>" +
		"<c r=\"D4\"><v>1.0000000000000001E-5</v></c><c r=\"E4\" t=\"b\"><v>1</v></c><c r=\"F4\"><v>-1234567.8910000001</v></c>" +
		"<c r=\"G4\" t=\"inlineStr\"><is><t>say \"hi\"</t></is></c></row>";

	/// <summary>The CSV of <see cref="TypedRows"/> in the 1900 date system, with the default options.</summary>
	private const string TypedCsv =
		"Name,Ordered,Took,Total,Paid,Score,Note\r\n" +
		"apple,2024-01-15,36:00:00,0.3,TRUE,7.5,\"first, with a comma\"\r\n" +
		"pear,2024-01-16T12:00:00,01:30:00,12345678901234600,FALSE,#N/A,\r\n" +
		"plum,1900-02-29,10:30:00,1E-5,TRUE,-1234567.891,\"say \"\"hi\"\"\"\r\n";

	public static TheoryData<bool> Modes => new() { false, true };

	/// <summary>A stream that can only be read asynchronously, as some network streams are.</summary>
	private sealed class AsyncOnlyStream(byte[] bytes) : Stream
	{
		private readonly MemoryStream inner = new(bytes, false);

		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => false;

		public override long Length => throw new NotSupportedException();

		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("The stream can only be read asynchronously.");

		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Task.FromResult(inner.Read(buffer, offset, count));

#if NET
		public override int Read(Span<byte> buffer) => throw new NotSupportedException("The stream can only be read asynchronously.");

		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => new(inner.Read(buffer.Span));
#endif

		public override void Flush()
		{
		}

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => throw new NotSupportedException();

		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
	}

	/// <summary>The typed workbook: the test package's styles (1 a built-in date, 2 a custom date), with <c>[h]:mm:ss</c> as 3 and <c>h:mm:ss</c> as 4.</summary>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <returns>The package.</returns>
	private static TestPackage Typed(bool date1904 = false)
	{
		var package = TestPackage.Workbook(new TestSheet("Data", xml: Sheet.Sheets.Worksheet(TypedRows)))
			.Replace("xl/styles.xml", "<numFmt numFmtId=\"164\" formatCode=\"yyyy\\-mm\\-dd\"/>", "<numFmt numFmtId=\"164\" formatCode=\"yyyy\\-mm\\-dd\"/><numFmt numFmtId=\"165\" formatCode=\"[h]:mm:ss\"/>")
			.Replace("xl/styles.xml", "<xf numFmtId=\"164\" applyNumberFormat=\"1\"/>", "<xf numFmtId=\"164\" applyNumberFormat=\"1\"/><xf numFmtId=\"165\" applyNumberFormat=\"1\"/><xf numFmtId=\"21\" applyNumberFormat=\"1\"/>");

		return date1904 ? package.Replace("xl/workbook.xml", "<workbookPr ", "<workbookPr date1904=\"1\" ") : package;
	}

	/// <summary>An inline string cell.</summary>
	private static string Text(string reference, string text) => $"<c r=\"{reference}\" t=\"inlineStr\"><is><t>{text}</t></is></c>";

	/// <summary>A number cell.</summary>
	private static string Number(string reference, string value) => $"<c r=\"{reference}\"><v>{value}</v></c>";

	/// <summary>A row.</summary>
	private static string Row(int row, params string[] cells) => $"<row r=\"{row}\">{string.Concat(cells)}</row>";

	/// <summary>Converts a package's sheet through the stream overloads.</summary>
	private static async Task<string> ConvertAsync(TestPackage package, bool async, ExcelToCsvOptions? options = null)
	{
		using var excel = package.ToStream();
		using var csv = new MemoryStream();
		if (async)
		{
			await ExcelConvert.ToCsvAsync(excel, csv, options);
		}
		else
		{
			ExcelConvert.ToCsv(excel, csv, options);
		}

		return Encoding.UTF8.GetString(csv.ToArray());
	}

	/// <summary>Options that read with these reader options.</summary>
	private static ExcelToCsvOptions Reading(ExcelReaderOptions excel) => new() { Excel = excel };

	/// <summary>Reader options with these CSV reader options.</summary>
	private static ExcelReaderOptions WithCsv(CsvReaderOptions csv) => new() { Csv = csv };

	/// <summary>A new folder's path under the temporary folder, which the caller deletes.</summary>
	private static string NewFolder() => Path.Combine(Path.GetTempPath(), $"CsvHelper.Excel.Tests.{Guid.NewGuid():N}");

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesEachCellAsTheTextGetStringGets(bool async)
	{
		Assert.Equal(TypedCsv, await ConvertAsync(Typed(), async));

		// Each field is what GetString reads, but numbers, which are rounded to 15 digits as decimal targets read them.
		using var reader = ExcelReader.Create(Typed().ToStream());
		using var written = CsvReader.FromText(TypedCsv);
		Assert.Equal(reader.Headers, written.Headers);
		while (reader.Read())
		{
			Assert.True(written.Read());
			for (var i = 0; i < reader.FieldCount; i++)
			{
				if (i is 3 || reader.GetString(i) is "-1234567.8910000001")
				{
					Assert.Equal(reader.Parse<decimal>(i), decimal.Parse(written.GetString(i), NumberStyles.Float, CultureInfo.InvariantCulture));
				}
				else
				{
					Assert.Equal(reader.GetString(i), written.GetString(i));
				}
			}
		}

		Assert.False(written.Read());

		// The 1904 date system's serials are other days.
		var csv1904 = await ConvertAsync(Typed(date1904: true), async);
		Assert.Contains($"apple,{new DateTime(1904, 1, 1).AddDays(45306):yyyy-MM-dd},", csv1904);
		Assert.Contains($"plum,{new DateTime(1904, 1, 1).AddDays(60):yyyy-MM-dd},", csv1904);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesNumbersInTheirSignificantDigits(bool async)
	{
		// The stored text, the cell's exact double, with SignificantDigits = null; fewer digits with fewer.
		var exact = await ConvertAsync(Typed(), async, Reading(new ExcelReaderOptions { SignificantDigits = null }));
		Assert.Contains(",0.30000000000000004,", exact);
		Assert.Contains(",12345678901234567,", exact);
		Assert.Contains(",1.0000000000000001E-5,", exact);
		Assert.Contains(",-1234567.8910000001,", exact);

		var three = await ConvertAsync(Typed(), async, Reading(new ExcelReaderOptions { SignificantDigits = 3 }));
		Assert.Contains("apple,2024-01-15,36:00:00,0.3,TRUE,7.5,", three);
		Assert.Contains("pear,2024-01-16T12:00:00,01:30:00,12300000000000000,", three);
		Assert.Contains(",-1230000,", three);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task GivesInjectionProtectionNumbersAsNumbers(bool async)
	{
		var package = Workbooks.Data(Row(1, Number("A1", "-5"), Text("B1", "-5"), Text("C1", "=1+1"), Text("D1", "+1+1"), Text("E1", "@SUM(1)"), Number("F1", "-0.30000000000000004")));
		var noHeader = new ExcelReaderOptions { Csv = new CsvReaderOptions { HasHeader = false } };

		// Text that Excel computes when it opens a CSV is written as it is, as Excel's export writes it.
		Assert.Equal("-5,-5,=1+1,+1+1,@SUM(1),-0.3\r\n", await ConvertAsync(package, async, Reading(noHeader)));

		// Injection protects text cells, and passes over a number cell's minus sign, rounded or not, as it does a double's.
		var escaped = new ExcelToCsvOptions { Excel = noHeader, Csv = new CsvWriterOptions { Injection = CsvInjection.Escape } };
		Assert.Equal("-5,\"'-5\",\"'=1+1\",\"'+1+1\",\"'@SUM(1)\",-0.3\r\n", await ConvertAsync(package, async, escaped));

		// ShouldQuote gets each field's type by its cell's kind, and a negative duration is no text to protect either.
		var cells = Typed().Set(
			"xl/worksheets/sheet1.xml",
			Sheet.Sheets.Worksheet(Row(1, Text("A1", "a"), Number("B1", "1.5"), "<c r=\"C1\" s=\"1\"><v>45306</v></c>", "<c r=\"D1\" s=\"3\"><v>-0.0625</v></c>", "<c r=\"E1\" t=\"b\"><v>1</v></c>", "<c r=\"F1\" t=\"e\"><v>#N/A</v></c>")));
		var types = new List<Type>();
		var quoting = new ExcelToCsvOptions
		{
			Excel = noHeader,
			Csv = new CsvWriterOptions
			{
				Injection = CsvInjection.Escape,
				ShouldQuote = (_, type, _, _) =>
				{
					types.Add(type);

					return false;
				},
			},
		};
		Assert.Equal("a,1.5,2024-01-15,-01:30:00,TRUE,#N/A\r\n", await ConvertAsync(cells, async, quoting));
		Assert.Equal([typeof(string), typeof(double), typeof(DateTime), typeof(TimeSpan), typeof(bool), typeof(string)], types);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesTheRowsAsTheReaderReadsThem(bool async)
	{
		// Data from C3: field i is column i, so two empty fields first. A short row is padded, and a long one stays long.
		var package = Workbooks.Data(
			Row(3, Text("C3", "h1"), Text("D3", "h2"), Text("E3", "h3")) +
			Row(4, Number("C4", "1")) +
			Row(6, Number("C6", "5"), Number("D6", "6"), Number("E6", "7"), Number("F6", "8"), Number("G6", "9")));
		Assert.Equal(",,h1,h2,h3\r\n,,1,,\r\n,,5,6,7,8,9\r\n", await ConvertAsync(package, async));

		// Empty rows kept are as wide as the first record with a value, those before it included: the reader's header is
		// the first, empty, row.
		var kept = Reading(WithCsv(new CsvReaderOptions { SkipEmptyLines = false }));
		Assert.Equal(",,,,\r\n,,,,\r\n,,h1,h2,h3\r\n,,1,,\r\n,,,,\r\n,,5,6,7,8,9\r\n", await ConvertAsync(package, async, kept));

		// A sheet of one column: an empty row is one empty field, which the writer quotes. So are empty rows when no row has a value.
		var keptWithoutHeader = Reading(WithCsv(new CsvReaderOptions { SkipEmptyLines = false, HasHeader = false }));
		Assert.Equal("x\r\n\"\"\r\ny\r\n", await ConvertAsync(Workbooks.Data(Row(1, Text("A1", "x")) + Row(3, Text("A3", "y"))), async, keptWithoutHeader));
		Assert.Equal("\"\"\r\n\"\"\r\n", await ConvertAsync(Workbooks.Data("<row r=\"2\"/>"), async, keptWithoutHeader));

		// A sheet without rows, and one of a header alone.
		Assert.Equal("", await ConvertAsync(Workbooks.Data(""), async));
		Assert.Equal("a,b\r\n", await ConvertAsync(Workbooks.Data(Row(1, Text("A1", "a"), Text("B1", "b"))), async));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FollowsTheReadersOptions(bool async)
	{
		// The header is the first record either way; without one, a typed header cell is a typed field.
		var noHeader = Reading(WithCsv(new CsvReaderOptions { HasHeader = false }));
		Assert.Equal(TypedCsv, await ConvertAsync(Typed(), async, noHeader));

		// Error cells as ErrorCells says: text, or empty.
		var nullErrors = await ConvertAsync(Typed(), async, Reading(new ExcelReaderOptions { ErrorCells = ExcelErrorCells.Null }));
		Assert.Contains("pear,2024-01-16T12:00:00,01:30:00,12345678901234600,FALSE,,\r\n", nullErrors);

		var thrown = await Assert.ThrowsAsync<FormatException>(() => ConvertAsync(Typed(), async, Reading(new ExcelReaderOptions { ErrorCells = ExcelErrorCells.Error })));
		Assert.Equal("Data!F3 holds the error #N/A.", thrown.Message);

		// Formulas as their saved values, or their text; an area, its header row and hidden rows, as the reader reads them.
		var formulas = Workbooks.Data(
			Row(1, Text("A1", "title")) +
			Row(2, Text("A2", "a"), Text("B2", "b"), Text("C2", "c")) +
			"<row r=\"3\" hidden=\"1\"><c r=\"A3\"><v>1</v></c><c r=\"B3\"><v>2</v></c><c r=\"C3\"><v>3</v></c></row>" +
			Row(4, Number("A4", "4"), Number("B4", "5"), "<c r=\"C4\"><f>A4+B4</f><v>9</v></c>"));
		Assert.Equal("title\r\na,b,c\r\n1,2,3\r\n4,5,9\r\n", await ConvertAsync(formulas, async));
		Assert.Equal("title\r\na,b,c\r\n1,2,3\r\n4,5,=A4+B4\r\n", await ConvertAsync(formulas, async, Reading(new ExcelReaderOptions { Formulas = ExcelFormulas.FormulaText })));
		Assert.Equal("b,c\r\n5,9\r\n", await ConvertAsync(formulas, async, Reading(new ExcelReaderOptions { Range = "B2:C4", SkipHiddenRows = true })));
		Assert.Equal("a,b,c\r\n4,5,9\r\n", await ConvertAsync(formulas, async, Reading(new ExcelReaderOptions { HeaderRow = ExcelHeaderRow.At(2), SkipHiddenRows = true })));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FollowsTheWritersOptions(bool async)
	{
		var options = new ExcelToCsvOptions
		{
			Csv = new CsvWriterOptions { Separator = ';', NewLine = "\n", Quoting = CsvQuoting.Always, WriteByteOrderMark = true },
		};
		var csv = await ConvertAsync(Workbooks.Data(Row(1, Text("A1", "a"), Text("B1", "b")) + Row(2, Number("A2", "1.5"))), async, options);
		Assert.Equal("﻿\"a\";\"b\"\n\"1.5\";\"\"\n", csv);

		// The writer's field-count check is off, so a long row is written; and its header option doesn't apply.
		var longRow = Workbooks.Data(Row(1, Text("A1", "a")) + Row(2, Number("A2", "1"), Number("B2", "2")));
		var checking = new ExcelToCsvOptions { Csv = new CsvWriterOptions { ValidateFieldCount = true, HasHeader = false } };
		Assert.Equal("a\r\n1,2\r\n", await ConvertAsync(longRow, async, checking));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ConvertsFilesAndDeletesOneThatFails(bool async)
	{
		var excelPath = Workbooks.ToFile(Typed());
		var csvPath = Path.ChangeExtension(excelPath, ".csv");
		try
		{
			await ConvertFileAsync(null);
			Assert.Equal(TypedCsv, File.ReadAllText(csvPath));
			Assert.True(Workbooks.IsClosed(excelPath));
			Assert.True(Workbooks.IsClosed(csvPath));

			// Options that can't be read leave the CSV file as it was: the workbook is read first.
			File.WriteAllText(csvPath, "old");
			var e = await Assert.ThrowsAsync<ArgumentException>(() => ConvertFileAsync(Reading(new ExcelReaderOptions { Sheet = "Nope" })));
			Assert.Equal("options", e.ParamName);
			Assert.Equal("old", File.ReadAllText(csvPath));

			// So do writer options that can't be used: they're checked before the CSV file is created, which nothing then holds.
			await Assert.ThrowsAsync<ArgumentException>(() => ConvertFileAsync(new ExcelToCsvOptions { Csv = new CsvWriterOptions { NewLine = "x" } }));
			Assert.Equal("old", File.ReadAllText(csvPath));
			Assert.True(Workbooks.IsClosed(csvPath));

			// A conversion that fails deletes the file it was writing.
			var error = await Assert.ThrowsAsync<FormatException>(() => ConvertFileAsync(Reading(new ExcelReaderOptions { ErrorCells = ExcelErrorCells.Error })));
			Assert.Equal("Data!F3 holds the error #N/A.", error.Message);
			Assert.False(File.Exists(csvPath));
			Assert.True(Workbooks.IsClosed(excelPath));

			// The workbook is no CSV file to write.
			var same = await Assert.ThrowsAsync<ArgumentException>(() => async ? ExcelConvert.ToCsvAsync(excelPath, excelPath).AsTask() : Task.Run(() => ExcelConvert.ToCsv(excelPath, excelPath)));
			Assert.Equal("csvPath", same.ParamName);
			Assert.Equal(Typed().ToArray(), File.ReadAllBytes(excelPath));

			// A folder that doesn't exist fails without holding the workbook.
			var missing = Path.Combine(NewFolder(), "x.csv");
			await Assert.ThrowsAsync<DirectoryNotFoundException>(() => async ? ExcelConvert.ToCsvAsync(excelPath, missing).AsTask() : Task.Run(() => ExcelConvert.ToCsv(excelPath, missing)));
			Assert.True(Workbooks.IsClosed(excelPath));

			await Assert.ThrowsAsync<ArgumentNullException>(() => async ? ExcelConvert.ToCsvAsync(null!, csvPath).AsTask() : Task.Run(() => ExcelConvert.ToCsv(null!, csvPath)));
			await Assert.ThrowsAsync<ArgumentNullException>(() => async ? ExcelConvert.ToCsvAsync(excelPath, null!).AsTask() : Task.Run(() => ExcelConvert.ToCsv(excelPath, null!)));
		}
		finally
		{
			File.Delete(excelPath);
			File.Delete(csvPath);
		}

		async Task ConvertFileAsync(ExcelToCsvOptions? options)
		{
			if (async)
			{
				await ExcelConvert.ToCsvAsync(excelPath, csvPath, options);
			}
			else
			{
				ExcelConvert.ToCsv(excelPath, csvPath, options);
			}
		}
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesStreamsFromTheirPositionAndLeavesThemOpen(bool async)
	{
		using var excel = Typed().ToStream();
		using var csv = new MemoryStream();
		csv.Write(Encoding.UTF8.GetBytes("before\n"), 0, 7);
		if (async)
		{
			await ExcelConvert.ToCsvAsync(excel, csv);
		}
		else
		{
			ExcelConvert.ToCsv(excel, csv);
		}

		Assert.Equal("before\n" + TypedCsv, Encoding.UTF8.GetString(csv.ToArray()));
		Assert.True(excel.CanRead);
		Assert.True(csv.CanWrite);

		// A failure leaves them open too, with what was written.
		using var failed = new MemoryStream();
		await Assert.ThrowsAsync<FormatException>(() => async
			? ExcelConvert.ToCsvAsync(excel, failed, Reading(new ExcelReaderOptions { ErrorCells = ExcelErrorCells.Error })).AsTask()
			: Task.Run(() => ExcelConvert.ToCsv(excel, failed, Reading(new ExcelReaderOptions { ErrorCells = ExcelErrorCells.Error }))));
		Assert.True(excel.CanRead);
		Assert.True(failed.CanWrite);
		Assert.StartsWith("Name,Ordered,", Encoding.UTF8.GetString(failed.ToArray()));

		// A stream that can't be read or written.
		var closed = new MemoryStream();
		closed.Dispose();
		var unread = await Assert.ThrowsAsync<ArgumentException>(() => async ? ExcelConvert.ToCsvAsync(closed, csv).AsTask() : Task.Run(() => ExcelConvert.ToCsv(closed, csv)));
		Assert.Equal("excel", unread.ParamName);
		var unwritten = await Assert.ThrowsAsync<ArgumentException>(() => async ? ExcelConvert.ToCsvAsync(excel, new MemoryStream([], false)).AsTask() : Task.Run(() => ExcelConvert.ToCsv(excel, new MemoryStream([], false))));
		Assert.Equal("csv", unwritten.ParamName);

		// The options are checked: the reader's, and the writer's before the workbook is read.
		await Assert.ThrowsAsync<ArgumentException>(() => ConvertAsync(Typed(), async, new ExcelToCsvOptions { Excel = null! }));
		await Assert.ThrowsAsync<ArgumentException>(() => ConvertAsync(Typed(), async, new ExcelToCsvOptions { Csv = null! }));
		await Assert.ThrowsAsync<ArgumentException>(() => ConvertAsync(Typed(), async, new ExcelToCsvOptions { Csv = new CsvWriterOptions { Separator = '\n' } }));
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ConvertAsync(Typed(), async, Reading(new ExcelReaderOptions { SignificantDigits = 0 })));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task CopiesAReaderIntoAWriterFromWhereItStands(bool async)
	{
		using var workbook = ExcelWorkbook.Open(Typed().ToStream());

		// A reader not yet started: its header, read asynchronously when the copy is, then every record.
		Assert.Equal(TypedCsv, await CopyAsync(workbook.OpenReader(), null));

		// A reader on a record: the header, then the records after it.
		var started = workbook.OpenReader();
		Assert.True(started.Read());
		Assert.Equal(string.Join("\r\n", TypedCsv.Split(["\r\n"], StringSplitOptions.None).Where((_, i) => i != 1)), await CopyAsync(started, null));

		// A reader without a header writes none. The writer's own field-count check applies.
		var noHeader = workbook.OpenReader(new ExcelReaderOptions { Csv = new CsvReaderOptions { HasHeader = false } });
		Assert.Equal(TypedCsv, await CopyAsync(noHeader, null));
		using var longRow = ExcelWorkbook.Open(Workbooks.Data(Row(1, Text("A1", "a")) + Row(2, Number("A2", "1"), Number("B2", "2"))).ToStream());
		await Assert.ThrowsAsync<InvalidOperationException>(() => CopyAsync(longRow.OpenReader(), new CsvWriterOptions()));

		// Any reader: CSV's fields as it reads them, an empty line kept as wide as the first record, a short record as it is.
		var csv = CsvReader.FromText("a,b\r\n1,2\r\n\r\n3\r\n", new CsvReaderOptions { SkipEmptyLines = false });
		Assert.Equal("a,b\r\n1,2\r\n,\r\n3\r\n", await CopyAsync(csv, null));

		if (async)
		{
			// Every read is asynchronous, the header's too: a reader of a stream that can't be read synchronously.
			using var asyncOnly = CsvReader.Create(new AsyncOnlyStream(Encoding.UTF8.GetBytes("a,b\r\n1,2\r\n")));
			var stream = new MemoryStream();
			await using (var writer = CsvWriter.Create(stream, leaveOpen: true))
			{
				await ExcelConvert.ToCsvAsync(asyncOnly, writer);
			}

			Assert.Equal("a,b\r\n1,2\r\n", Encoding.UTF8.GetString(stream.ToArray()));
		}

		await Assert.ThrowsAsync<ArgumentNullException>(() => async ? ExcelConvert.ToCsvAsync(null!, CsvWriter.Create(Stream.Null)).AsTask() : Task.Run(() => ExcelConvert.ToCsv(null!, CsvWriter.Create(Stream.Null))));
		await Assert.ThrowsAsync<ArgumentNullException>(() => async ? ExcelConvert.ToCsvAsync(workbook.OpenReader(), null!).AsTask() : Task.Run(() => ExcelConvert.ToCsv(workbook.OpenReader(), null!)));

		// Copies, and disposes neither: the reader is at its end, and the writer still writes.
		async Task<string> CopyAsync(CsvReader reader, CsvWriterOptions? writerOptions)
		{
			var stream = new MemoryStream();
			var writer = CsvWriter.Create(stream, writerOptions ?? new CsvWriterOptions { ValidateFieldCount = false }, leaveOpen: true);
			if (async)
			{
				await ExcelConvert.ToCsvAsync(reader, writer);
			}
			else
			{
				ExcelConvert.ToCsv(reader, writer);
			}

			Assert.False(reader.Read());
			writer.Flush();
			var written = Encoding.UTF8.GetString(stream.ToArray());
			writer.WriteField("more");
			writer.EndRecord();
			writer.Dispose();
			reader.Dispose();
			Assert.EndsWith("more\r\n", Encoding.UTF8.GetString(stream.ToArray()));

			return written;
		}
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesEveryWorksheetToAFileNamedAfterIt(bool async)
	{
		var sheet = Sheet.Sheets.Worksheet(Row(1, Text("A1", "a")) + Row(2, Number("A2", "1")));
		var package = TestPackage.Workbook(
			new TestSheet("Chart", type: "chartsheet"),
			new TestSheet("Hidden", state: "hidden", xml: sheet),
			new TestSheet("Orders", xml: Sheet.Sheets.Worksheet(Row(1, Text("A1", "Name"), Text("B1", "Day")) + Row(2, Text("A2", "apple"), "<c r=\"B2\" s=\"1\"><v>45306</v></c>"))),
			new TestSheet("Q1 Orders", xml: sheet),
			new TestSheet("orders", xml: sheet),
			new TestSheet("../x", xml: sheet),
			new TestSheet("CON", xml: sheet),
			new TestSheet("Sales &lt;2024&gt;", xml: sheet),
			new TestSheet("..", xml: sheet),
			new TestSheet("Macro", type: "xlMacrosheet"),
			new TestSheet("orders (2)", xml: sheet));
		var excelPath = Workbooks.ToFile(package);
		var parent = NewFolder();
		var folder = Path.Combine(parent, "nested", "out");
		try
		{
			var paths = async ? await ExcelConvert.ToCsvFilesAsync(excelPath, folder) : ExcelConvert.ToCsvFiles(excelPath, folder);
			string[] names = ["Hidden.csv", "Orders.csv", "Q1 Orders.csv", "orders (3).csv", ".._x.csv", "_CON.csv", "Sales _2024_.csv", "Sheet9.csv", "orders (2).csv"];
			Assert.Equal(names.Select(n => Path.Combine(folder, n)), paths);
			Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal), Directory.GetFiles(folder).Select(f => Path.GetFileName(f)).OrderBy(n => n, StringComparer.Ordinal));
			Assert.Equal(new[] { folder }, Directory.GetFileSystemEntries(Path.Combine(parent, "nested")));
			Assert.Equal("a\r\n1\r\n", File.ReadAllText(paths[0]));

			Assert.Equal("Name,Day\r\napple,2024-01-15\r\n", File.ReadAllText(paths[1]));
			Assert.True(Workbooks.IsClosed(excelPath));
			Assert.All(paths, p => Assert.True(Workbooks.IsClosed(p)));

			// Again, over the files.
			File.WriteAllText(paths[0], "old");
			Assert.Equal(paths, async ? await ExcelConvert.ToCsvFilesAsync(excelPath, folder) : ExcelConvert.ToCsvFiles(excelPath, folder));
			Assert.Equal("a\r\n1\r\n", File.ReadAllText(paths[0]));
		}
		finally
		{
			File.Delete(excelPath);
			if (Directory.Exists(parent))
			{
				Directory.Delete(parent, recursive: true);
			}
		}
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesNoSheetOfOptionsThatChooseOne(bool async)
	{
		var excelPath = Workbooks.ToFile(Typed());
		var folder = NewFolder();
		try
		{
			ExcelReaderOptions[] choosing =
			[
				new() { Sheet = "Data" }, new() { SheetIndex = 0 }, new() { Range = "A1:B2" }, new() { Table = "T" }, new() { DefinedName = "N" },
			];
			string[] names = ["Sheet", "SheetIndex", "Range", "Table", "DefinedName"];
			for (var i = 0; i < choosing.Length; i++)
			{
				var options = Reading(choosing[i]);
				var e = await Assert.ThrowsAsync<ArgumentException>(() => async ? ExcelConvert.ToCsvFilesAsync(excelPath, folder, options).AsTask() : Task.Run(() => ExcelConvert.ToCsvFiles(excelPath, folder, options)));
				Assert.StartsWith($"ToCsvFiles writes every worksheet, so ExcelReaderOptions.{names[i]} can't be set. Use ToCsv for one sheet.", e.Message);
				Assert.Equal("options", e.ParamName);
			}

			Assert.False(Directory.Exists(folder));

			// A sheet that fails deletes its file, and leaves those before it.
			var failing = TestPackage.Workbook(
				new TestSheet("First", xml: Sheet.Sheets.Worksheet(Row(1, Text("A1", "a")))),
				new TestSheet("Second", xml: Sheet.Sheets.Worksheet(Row(1, Text("A1", "a")) + Row(2, "<c r=\"A2\" t=\"e\"><v>#REF!</v></c>"))));
			File.WriteAllBytes(excelPath, failing.ToArray());
			var errors = Reading(new ExcelReaderOptions { ErrorCells = ExcelErrorCells.Error });
			var error = await Assert.ThrowsAsync<FormatException>(() => async ? ExcelConvert.ToCsvFilesAsync(excelPath, folder, errors).AsTask() : Task.Run(() => ExcelConvert.ToCsvFiles(excelPath, folder, errors)));
			Assert.Equal("Second!A2 holds the error #REF!.", error.Message);
			Assert.Equal(new[] { "First.csv" }, Directory.GetFiles(folder).Select(f => Path.GetFileName(f)));
			Assert.True(Workbooks.IsClosed(excelPath));

			// A workbook in the folder, named as a sheet's file would be, isn't overwritten.
			var inFolder = Path.Combine(folder, "First.csv");
			File.WriteAllBytes(inFolder, failing.ToArray());
			var same = await Assert.ThrowsAsync<ArgumentException>(() => async ? ExcelConvert.ToCsvFilesAsync(inFolder, folder).AsTask() : Task.Run(() => ExcelConvert.ToCsvFiles(inFolder, folder)));
			Assert.Equal("folder", same.ParamName);
			Assert.Equal(failing.ToArray(), File.ReadAllBytes(inFolder));
		}
		finally
		{
			File.Delete(excelPath);
			if (Directory.Exists(folder))
			{
				Directory.Delete(folder, recursive: true);
			}
		}
	}

	// .NET 10 only: on .NET 8, the unoptimized (tier 0) code of core's double parser, which a date's and a duration's text
	// starts with, allocates 64 bytes a call until the JIT optimizes it, which a test's run may not last until. With tiered
	// compilation off (DOTNET_TieredCompilation=0), .NET 8 allocates nothing per row either (checked 2026-11-12).
#if NET10_0_OR_GREATER
	[Fact]
	public void AllocatesTheSameForAnyNumberOfRows()
	{
		// Text, numbers rounded and not, dates, durations, booleans and errors: no field makes a string.
		var bytes = new Func<int, long>(rows =>
		{
			var data = new StringBuilder();
			for (var r = 1; r <= rows; r++)
			{
				data.Append($"<row r=\"{r}\"><c r=\"A{r}\" t=\"inlineStr\"><is><t>name {r}</t></is></c><c r=\"B{r}\"><v>{r}.5</v></c>")
					.Append($"<c r=\"C{r}\"><v>0.30000000000000004</v></c><c r=\"D{r}\" s=\"1\"><v>{45000 + (r % 1000)}</v></c><c r=\"E{r}\" s=\"3\"><v>1.25</v></c>")
					.Append($"<c r=\"F{r}\" t=\"b\"><v>{r % 2}</v></c><c r=\"G{r}\" t=\"e\"><v>#N/A</v></c><c r=\"H{r}\" s=\"2\"><v>45000.5</v></c></row>");
			}

			var package = Typed().Set("xl/worksheets/sheet1.xml", Sheet.Sheets.Worksheet(data.ToString()));
			using var reader = ExcelReader.Create(package.ToStream(), new ExcelReaderOptions { Csv = new CsvReaderOptions { HasHeader = false } });
			using var writer = CsvWriter.Create(Stream.Null);

			// The first record warms the reader's and the writer's buffers.
			Assert.True(reader.Read());
			var before = GC.GetAllocatedBytesForCurrentThread();
			ExcelConvert.ToCsv(reader, writer);

			return GC.GetAllocatedBytesForCurrentThread() - before;
		});

		bytes(1_000);
		var few = bytes(1_000);
		var many = bytes(100_000);
		Assert.True(many - few < 4096, $"1,000 rows allocated {few:N0} bytes and 100,000 rows {many:N0}.");
	}
#endif
}
