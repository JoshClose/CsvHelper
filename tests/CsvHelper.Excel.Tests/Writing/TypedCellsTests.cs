using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>A record of every type the writer types: written with its generated record writer and read back with its mapper.</summary>
[CsvRecord]
public partial class TypedRecord
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A boolean cell.</summary>
	public bool Paid { get; set; }

	/// <summary>A number cell.</summary>
	public int Quantity { get; set; }

	/// <summary>A number cell, or text past 15 digits.</summary>
	public long Id { get; set; }

	/// <summary>A number cell.</summary>
	public decimal Price { get; set; }

	/// <summary>A number cell, or text for NaN and the infinities.</summary>
	public double Score { get; set; }

	/// <summary>A number cell.</summary>
	public float Ratio { get; set; }

	/// <summary>A date cell.</summary>
	public DateTime Ordered { get; set; }

	/// <summary>A duration cell.</summary>
	public TimeSpan Took { get; set; }

	/// <summary>A text cell, or a date cell as the options say.</summary>
	public DateTimeOffset Stamped { get; set; }

	/// <summary>No cell when null.</summary>
	public int? Missing { get; set; }
}

#if NET
/// <summary>A record of the types only .NET has.</summary>
[CsvRecord]
public partial class ModernRecord
{
	/// <summary>A date cell.</summary>
	public DateOnly Day { get; set; }

	/// <summary>A time cell.</summary>
	public TimeOnly Time { get; set; }

	/// <summary>A number cell.</summary>
	public Half Half { get; set; }

	/// <summary>A number cell, or text past 15 digits.</summary>
	public Int128 Big { get; set; }
}
#endif

/// <summary>
/// Typed cells end to end, synchronously and asynchronously: numbers, booleans, dates, times and durations from .NET's
/// default texts, read back by CsvHelper.Excel's reader (records, dynamic records, <see cref="CsvDataReader"/>), ClosedXML
/// and ExcelDataReader; the options for long numbers, offsets and dates Excel can't show; texts that stay text; the
/// columns' widths; and Excel's warning about numbers stored as text.
/// </summary>
public class TypedCellsTests
{
	/// <summary>Sync and async.</summary>
	public static TheoryData<bool> Modes => new() { false, true };

	/// <summary>Two records: values of each kind, and edge values (milliseconds, a date at midnight, 1900's first days).</summary>
	private static readonly TypedRecord[] Records =
	[
		new()
		{
			Name = "apple", Paid = true, Quantity = 42, Id = 123456789012345, Price = 0.10m, Score = 0.30000000000000004, Ratio = 0.1f,
			Ordered = new DateTime(2024, 1, 15, 10, 30, 0), Took = new TimeSpan(1, 12, 0, 0), Stamped = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.FromHours(2)),
		},
		new()
		{
			Name = "pear", Paid = false, Quantity = -7, Id = -5, Price = -1234.5m, Score = 1E-05, Ratio = 1.5E+20f,
			Ordered = new DateTime(1900, 3, 1, 0, 0, 0, 1), Took = new TimeSpan(0, 0, 0, 1, 500), Stamped = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero), Missing = 3,
		},
	];

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesTypedCellsThatReadBack(bool async)
	{
		var workbook = await Write(Records, async);
		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");

		// Booleans, numbers as their invariant text, dates as serials in the writer's formats.
		Assert.Contains("<c r=\"B2\" t=\"b\"><v>1</v></c><c r=\"C2\"><v>42</v></c><c r=\"D2\"><v>123456789012345</v></c><c r=\"E2\"><v>0.10</v></c><c r=\"F2\"><v>0.30000000000000004</v></c><c r=\"G2\"><v>0.1</v></c><c r=\"H2\" s=\"1\"><v>45306.4375</v></c><c r=\"I2\" s=\"2\"><v>1.5</v></c><c r=\"J2\" t=\"inlineStr\">", sheet);
		Assert.Contains("<c r=\"B3\" t=\"b\"><v>0</v></c><c r=\"C3\"><v>-7</v></c><c r=\"D3\"><v>-5</v></c><c r=\"E3\"><v>-1234.5</v></c><c r=\"F3\"><v>1E-05</v></c><c r=\"G3\"><v>1.5E+20</v></c><c r=\"H3\" s=\"3\"><v>61.00000001157407</v></c><c r=\"I3\" s=\"4\"><v>1.736111111111111E-05</v></c>", sheet);
		Assert.Contains("<c r=\"K3\"><v>3</v></c></row>", sheet);
		var styles = Written.Entry(workbook, "xl/styles.xml");
		Assert.Contains("<numFmts count=\"3\"><numFmt numFmtId=\"164\" formatCode=\"yyyy-mm-dd hh:mm:ss\"/><numFmt numFmtId=\"165\" formatCode=\"yyyy-mm-dd hh:mm:ss.000\"/><numFmt numFmtId=\"166\" formatCode=\"[h]:mm:ss.000\"/></numFmts>", styles);
		Assert.Contains("<cellXfs count=\"5\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/><xf numFmtId=\"46\" ", styles);
		Assert.Empty(Written.ValidationErrors(workbook));

		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		using var reader = book.OpenReader();
		var read = reader.GetRecords<TypedRecord>().ToList();
		Assert.Equal(2, read.Count);
		for (var i = 0; i < 2; i++)
		{
			var (expected, actual) = (Records[i], read[i]);
			Assert.Equal(
				(expected.Name, expected.Paid, expected.Quantity, expected.Id, expected.Price, expected.Score, expected.Ratio, expected.Ordered, expected.Took, expected.Stamped, expected.Missing),
				(actual.Name, actual.Paid, actual.Quantity, actual.Id, actual.Price, actual.Score, actual.Ratio, actual.Ordered, actual.Took, actual.Stamped, actual.Missing));
		}
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ReadsBackThroughDynamicRecordsAndADataReader(bool async)
	{
		var workbook = await Write(Records, async);
		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));

		using (var reader = book.OpenReader())
		{
			Assert.True(reader.Read());
			Assert.Equal("apple|TRUE|42|123456789012345|0.10|0.30000000000000004|0.1|2024-01-15T10:30:00|36:00:00|2024-01-15T10:30:00.0000000+02:00|", Api.Workbooks.Fields(reader));
		}

		using (var reader = book.OpenReader())
		{
			var records = reader.GetRecords<CsvDynamicRecord>().Select(r => r.Detach()).ToList();
			Assert.Equal("TRUE", records[0]["Paid"]);
			Assert.Equal(new DateTime(1900, 3, 1, 0, 0, 0, 1), records[1].Parse<DateTime>("Ordered"));
			Assert.Equal(TimeSpan.FromMilliseconds(1500), records[1].Parse<TimeSpan>("Took"));
			Assert.Equal(-1234.5m, records[1].Parse<decimal>("Price"));
		}

		using (var reader = book.OpenReader())
		using (var data = new CsvDataReader(reader))
		{
			Assert.True(data.Read());
			Assert.True(data.GetBoolean(1));
			Assert.Equal(42, data.GetInt32(2));
			Assert.Equal(0.10m, data.GetDecimal(4));
			Assert.Equal(new DateTime(2024, 1, 15, 10, 30, 0), data.GetDateTime(7));
			var table = new DataTable();
			table.Load(data);
			Assert.Equal("FALSE", table.Rows[0]["Paid"]);
		}
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task IsReadByTheOracles(bool async)
	{
		var workbook = await Write(Records, async);

		// ClosedXML reads each typed cell as its type, but a duration under 24 hours as a time of day is still a TimeSpan.
		Assert.Equal(
			[
				"A1=Text:Name", "B1=Text:Paid", "C1=Text:Quantity", "D1=Text:Id", "E1=Text:Price", "F1=Text:Score", "G1=Text:Ratio", "H1=Text:Ordered",
				"I1=Text:Took", "J1=Text:Stamped", "K1=Text:Missing",
				"A2=Text:apple", "B2=Boolean:TRUE", "C2=Number:42", "D2=Number:123456789012345", "E2=Number:0.1", "F2=Number:0.30000000000000004",
				"G2=Number:0.1", "H2=DateTime:2024-01-15T10:30:00.000", "I2=TimeSpan:1.12:00:00", "J2=Text:2024-01-15T10:30:00.0000000+02:00",
				"A3=Text:pear", "B3=Boolean:FALSE", "C3=Number:-7", "D3=Number:-5", "E3=Number:-1234.5", "F3=Number:1E-05", "G3=Number:1.5E+20",
				"H3=DateTime:1900-03-01T00:00:00.001", "I3=TimeSpan:00:00:01.5000000", "J3=Text:1900-01-01T00:00:00.0000000+00:00", "K3=Number:3",
			],
			Written.ClosedXmlValues(workbook));
		// ExcelDataReader reads dates as DateTime and durations as TimeSpan.
		Assert.Equal(
			[
				"String:Name|String:Paid|String:Quantity|String:Id|String:Price|String:Score|String:Ratio|String:Ordered|String:Took|String:Stamped|String:Missing",
				"String:apple|Boolean:True|Double:42|Double:123456789012345|Double:0.1|Double:0.30000000000000004|Double:0.1|DateTime:2024-01-15T10:30:00.000|TimeSpan:1.12:00:00|String:2024-01-15T10:30:00.0000000+02:00|",
				"String:pear|Boolean:False|Double:-7|Double:-5|Double:-1234.5|Double:1E-05|Double:1.5E+20|DateTime:1900-03-01T00:00:00.001|TimeSpan:00:00:01.5000000|String:1900-01-01T00:00:00.0000000+00:00|Double:3",
			],
			Written.ExcelDataReaderValues(workbook));
	}

#if NET
	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesDotNetsNewerTypes(bool async)
	{
		var records = new[]
		{
			new ModernRecord { Day = new DateOnly(2024, 1, 15), Time = new TimeOnly(10, 30, 15), Half = (Half)0.1, Big = Int128.Parse("123456789012345", CultureInfo.InvariantCulture) },
			new ModernRecord { Day = new DateOnly(1900, 1, 1), Time = new TimeOnly(23, 59, 59, 999), Half = Half.MaxValue, Big = Int128.Parse("1234567890123456789012345", CultureInfo.InvariantCulture) },
		};

		var workbook = await Write(records, async);

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains("<c r=\"A2\" s=\"1\"><v>45306</v></c><c r=\"B2\" s=\"2\"><v>0.43767361111111114</v></c><c r=\"C2\"><v>0.1</v></c><c r=\"D2\"><v>123456789012345</v></c>", sheet);
		Assert.Contains("<c r=\"A3\" s=\"1\"><v>1</v></c><c r=\"B3\" s=\"3\"><v>0.999999988425926</v></c><c r=\"C3\"><v>65500</v></c><c r=\"D3\" t=\"inlineStr\"><is><t>1234567890123456789012345</t></is></c>", sheet);
		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		using var reader = book.OpenReader();
		var read = reader.GetRecords<ModernRecord>().ToList();
		Assert.Equal(records.Select(r => (r.Day, r.Time, r.Half, r.Big)), read.Select(r => (r.Day, r.Time, r.Half, r.Big)));
		Assert.Empty(Written.ValidationErrors(workbook));
	}
#endif

	[Theory]
	[InlineData(ExcelLongNumbers.Text, "<c r=\"A1\" t=\"inlineStr\"><is><t>12345678901234567</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>-0.1234567890123456</t></is></c>")]
	[InlineData(ExcelLongNumbers.Number, "<c r=\"A1\"><v>12345678901234567</v></c><c r=\"B1\"><v>-0.1234567890123456</v></c>")]
	public async Task WritesLongNumbersAsTheOptionsSay(ExcelLongNumbers longNumbers, string cells)
	{
		var options = new ExcelWriterOptions { LongNumbers = longNumbers, Csv = new CsvWriterOptions { HasHeader = false } };

		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Ids");
			writer.WriteField(12345678901234567L);
			writer.WriteField(-0.1234567890123456m);
			// 15 significant digits, and one: numbers whatever the option.
			writer.WriteField(123456789012345000L);
			writer.WriteField(10000000000000000000UL);
			writer.WriteField(1.2345678901234567);
			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
		}, false, options);

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains("<row r=\"1\">" + cells + "<c r=\"C1\"><v>123456789012345000</v></c><c r=\"D1\"><v>10000000000000000000</v></c><c r=\"E1\"><v>1.2345678901234567</v></c></row>", sheet);
		// The text keeps every digit, and so does the number's text, which Excel rounds to 15.
		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		using var reader = book.OpenReader(new ExcelReaderOptions { Csv = new CsvReaderOptions { HasHeader = false } });
		Assert.True(reader.Read());
		Assert.Equal(longNumbers == ExcelLongNumbers.Text ? 12345678901234567L : 12345678901234600L, reader.Parse<long>(0));
		Assert.Equal(longNumbers == ExcelLongNumbers.Text, sheet.Contains("numberStoredAsText"));
	}

	[Fact]
	public void ThrowsForLongNumbersWhenTheOptionsSay()
	{
		var options = new ExcelWriterOptions { LongNumbers = ExcelLongNumbers.Throw };
		using var workbook = ExcelWorkbookWriter.Create(new MemoryStream(), options);
		using var writer = workbook.AddSheet("Orders");
		var records = new[] { new TypedRecord { Name = "ok", Id = 123456789012345 }, new TypedRecord { Name = "long", Id = 1234567890123456789 } };

		var e = Assert.Throws<InvalidOperationException>(() => writer.WriteRecords(records));

		Assert.Equal("Cell Orders!D3 holds 1234567890123456789, which has 19 significant digits, and Excel keeps 15 (ExcelWriterOptions.LongNumbers is Throw).", e.Message);
	}

	[Fact]
	public void ChecksTheOptions()
	{
		Assert.Equal("options", Assert.Throws<ArgumentOutOfRangeException>(() => ExcelWorkbookWriter.Create(new MemoryStream(), new ExcelWriterOptions { LongNumbers = (ExcelLongNumbers)3 })).ParamName);
		Assert.Equal("options", Assert.Throws<ArgumentOutOfRangeException>(() => ExcelWorkbookWriter.Create(new MemoryStream(), new ExcelWriterOptions { DateTimeOffsets = (ExcelDateTimeOffsets)3 })).ParamName);
		Assert.Equal("options", Assert.Throws<ArgumentOutOfRangeException>(() => ExcelWorkbookWriter.Create(new MemoryStream(), new ExcelWriterOptions { OutOfRangeDates = (ExcelOutOfRangeDates)2 })).ParamName);
		Assert.Throws<ArgumentOutOfRangeException>(() => ExcelWorkbookWriter.Create(new MemoryStream(), new ExcelWriterOptions { LongNumbers = (ExcelLongNumbers)(-1) }));
		Assert.Equal(ExcelLongNumbers.Text, ExcelWriterOptions.Default.LongNumbers);
		Assert.Equal(ExcelDateTimeOffsets.Text, ExcelWriterOptions.Default.DateTimeOffsets);
		Assert.Equal(ExcelOutOfRangeDates.Text, ExcelWriterOptions.Default.OutOfRangeDates);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesNaNAndTheInfinitiesAsText(bool async)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Odd");
			writer.WriteField(double.NaN);
			writer.WriteField(double.PositiveInfinity);
			writer.WriteField(float.NegativeInfinity);
			writer.WriteField(-0.0);
			writer.WriteField(double.Epsilon);
			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
		}, async, new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false } });

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains("<c r=\"A1\" t=\"inlineStr\"><is><t>NaN</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>Infinity</t></is></c><c r=\"C1\" t=\"inlineStr\"><is><t>-Infinity</t></is></c><c r=\"D1\"><v>-0</v></c><c r=\"E1\"><v>5E-324</v></c>", sheet);
		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		using var reader = book.OpenReader(new ExcelReaderOptions { Csv = new CsvReaderOptions { HasHeader = false } });
		Assert.True(reader.Read());
		Assert.True(double.IsNaN(reader.Parse<double>(0)));
		Assert.Equal(double.PositiveInfinity, reader.Parse<double>(1));
		Assert.Equal(float.NegativeInfinity, reader.Parse<float>(2));
		Assert.Equal(double.Epsilon, reader.Parse<double>(4));
	}

	[Theory]
	[InlineData(ExcelDateTimeOffsets.Text, "<c r=\"A1\" t=\"inlineStr\"><is><t>2024-01-15T10:30:00.0000000+02:00</t></is></c>")]
	[InlineData(ExcelDateTimeOffsets.Utc, "<c r=\"A1\" s=\"1\"><v>45306.354166666664</v></c>")]
	[InlineData(ExcelDateTimeOffsets.ClockTime, "<c r=\"A1\" s=\"1\"><v>45306.4375</v></c>")]
	public async Task WritesDateTimeOffsetsAsTheOptionsSay(ExcelDateTimeOffsets offsets, string cell)
	{
		var value = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.FromHours(2));
		var options = new ExcelWriterOptions { DateTimeOffsets = offsets, Csv = new CsvWriterOptions { HasHeader = false } };

		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Stamps");
			writer.WriteField(value);
			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
		}, false, options);

		Assert.Contains("<row r=\"1\">" + cell + "</row>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		using var reader = book.OpenReader(new ExcelReaderOptions { Csv = new CsvReaderOptions { HasHeader = false } });
		Assert.True(reader.Read());
		var expected = offsets switch
		{
			ExcelDateTimeOffsets.Text => value,
			ExcelDateTimeOffsets.Utc => new DateTimeOffset(value.UtcDateTime, TimeSpan.Zero),
			_ => new DateTimeOffset(value.DateTime, TimeSpan.Zero),
		};
		Assert.Equal(expected, reader.Parse<DateTimeOffset>(0));
		Assert.Equal(expected.Offset, reader.Parse<DateTimeOffset>(0).Offset);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesDatesExcelCantShowAsText(bool async)
	{
		var options = new ExcelWriterOptions { DateTimeOffsets = ExcelDateTimeOffsets.Utc, Csv = new CsvWriterOptions { HasHeader = false } };

		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Old");
			writer.WriteField(new DateTime(1899, 12, 31, 23, 59, 59));
			writer.WriteField(TimeSpan.FromHours(-1));
			writer.WriteField(new DateTimeOffset(1900, 1, 1, 0, 30, 0, TimeSpan.FromHours(1)));
			writer.WriteField(DateTime.MinValue);
			// Half a millisecond before 1900 rounds to it, and a negative tick to no time.
			writer.WriteField(new DateTime(1900, 1, 1).AddTicks(-5000));
			writer.WriteField(TimeSpan.FromTicks(-1));
			// Excel shows no duration of 2,958,466 days or more, the serial of 10000-01-01; its last millisecond before it
			// shows in [h]:mm:ss.000, and half a millisecond before it rounds to it.
			writer.WriteField(TimeSpan.MaxValue);
			writer.WriteField(TimeSpan.FromDays(2_958_466));
			writer.WriteField(TimeSpan.FromDays(2_958_466) - TimeSpan.FromMilliseconds(1));
			writer.WriteField(TimeSpan.FromDays(2_958_466) - TimeSpan.FromTicks(5000));
			writer.WriteField(TimeSpan.FromDays(2_958_465) + new TimeSpan(23, 59, 59));
			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
		}, async, options);

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains(
			"<c r=\"A1\" t=\"inlineStr\"><is><t>1899-12-31T23:59:59.0000000</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>-01:00:00</t></is></c>" +
			"<c r=\"C1\" t=\"inlineStr\"><is><t>1900-01-01T00:30:00.0000000+01:00</t></is></c><c r=\"D1\" t=\"inlineStr\"><is><t>0001-01-01T00:00:00.0000000</t></is></c>" +
			"<c r=\"E1\" s=\"1\"><v>1</v></c><c r=\"F1\" s=\"2\"><v>0</v></c>" +
			"<c r=\"G1\" t=\"inlineStr\"><is><t>10675199.02:48:05.4775807</t></is></c><c r=\"H1\" t=\"inlineStr\"><is><t>2958466.00:00:00</t></is></c>",
			sheet);
		Assert.Contains("<c r=\"J1\" t=\"inlineStr\"><is><t>2958465.23:59:59.9995000</t></is></c><c r=\"K1\" s=\"2\"><v>2958465.999988426</v></c>", sheet);
		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		using var reader = book.OpenReader(new ExcelReaderOptions { Csv = new CsvReaderOptions { HasHeader = false } });
		Assert.True(reader.Read());
		Assert.Equal(new DateTime(1899, 12, 31, 23, 59, 59), reader.Parse<DateTime>(0));
		Assert.Equal(TimeSpan.FromHours(-1), reader.Parse<TimeSpan>(1));
		Assert.Equal(new DateTime(1900, 1, 1), reader.Parse<DateTime>(4));
		Assert.Equal(TimeSpan.MaxValue, reader.Parse<TimeSpan>(6));
		Assert.Equal(TimeSpan.FromDays(2_958_466) - TimeSpan.FromMilliseconds(1), reader.Parse<TimeSpan>(8));
		Assert.Equal(TimeSpan.FromDays(2_958_465) + new TimeSpan(23, 59, 59), reader.Parse<TimeSpan>(10));
	}

	[Theory]
	[InlineData(0, "Cell Old!A2 holds 1899-12-31T00:00:00.0000000, before 1900-01-01, Excel's first date (ExcelWriterOptions.OutOfRangeDates is Throw).")]
	[InlineData(1, "Cell Old!B2 holds -00:00:00.0005000, a negative duration, which Excel shows as ######## (ExcelWriterOptions.OutOfRangeDates is Throw).")]
	[InlineData(2, "Cell Old!B2 holds 2958465.23:59:59.9995000, a duration of 2,958,466 days or more, which Excel shows as ######## (ExcelWriterOptions.OutOfRangeDates is Throw).")]
	public void ThrowsForDatesExcelCantShowWhenTheOptionsSay(int field, string message)
	{
		var options = new ExcelWriterOptions { OutOfRangeDates = ExcelOutOfRangeDates.Throw, Csv = new CsvWriterOptions { HasHeader = false, ValidateFieldCount = false } };
		var stream = new MemoryStream();
		using (var workbook = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true))
		{
			using var writer = workbook.AddSheet("Old");
			writer.WriteField(new DateTime(1900, 1, 1));
			writer.EndRecord();
			if (field > 0)
			{
				writer.WriteField(new DateTime(1900, 1, 1));
			}

			var e = Assert.Throws<InvalidOperationException>(() =>
			{
				if (field == 0)
				{
					writer.WriteField(new DateTime(1899, 12, 31));
				}
				else if (field == 1)
				{
					writer.WriteField(TimeSpan.FromTicks(-5000));
				}
				else
				{
					writer.WriteField(TimeSpan.FromDays(2_958_466) - TimeSpan.FromTicks(5000));
				}
			});

			Assert.Equal(message, e.Message);
		}

		// The field that threw left no cell, and the workbook completed.
		Assert.Equal(field == 0 ? ["1900-01-01"] : ["1900-01-01", "1900-01-01"], Written.ReadOne(stream.ToArray()));
	}

	[Fact]
	public void ReadsTheWritersCultureStrictly()
	{
		// de-DE's text of a double and an integer are numbers; N2's grouping, D4's zeros and the invariant point stay text.
		var options = new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false, FormatProvider = CultureInfo.GetCultureInfo("de-DE") } };
		var stream = new MemoryStream();
		using (var workbook = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true))
		{
			using var writer = workbook.AddSheet("Kultur");
			writer.WriteField(-1234.5);
			writer.WriteField(1.5E+20);
			writer.WriteField(42);
			writer.WriteField(1234.5, "N2");
			writer.WriteField(42, "D4");
			writer.WriteField(1234.5m, "F1", CultureInfo.InvariantCulture);
			writer.WriteField(true);
			writer.EndRecord();
		}

		var sheet = Written.Entry(stream.ToArray(), "xl/worksheets/sheet1.xml");
		Assert.Contains(
			"<c r=\"A1\"><v>-1234.5</v></c><c r=\"B1\"><v>1.5E+20</v></c><c r=\"C1\"><v>42</v></c><c r=\"D1\" t=\"inlineStr\"><is><t>1.234,50</t></is></c>" +
			"<c r=\"E1\" t=\"inlineStr\"><is><t>0042</t></is></c><c r=\"F1\" t=\"inlineStr\"><is><t>1234.5</t></is></c><c r=\"G1\" t=\"b\"><v>1</v></c>",
			sheet);
	}

	[Fact]
	public void WritesTextOfOtherFormatsAndTypesAsText()
	{
		// A format's text, and types that aren't typed, are text, a URI too without links; the boolean's text options don't
		// apply to a sheet.
		var options = new ExcelWriterOptions { Links = ExcelLinks.None, Csv = new CsvWriterOptions { HasHeader = false, TrueValue = "yes", FalseValue = "no" } };
		var stream = new MemoryStream();
		using (var workbook = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true))
		{
			using var writer = workbook.AddSheet("Other");
			writer.WriteField(new DateTime(2024, 1, 15), "yyyy-MM-dd");
			writer.WriteField(TimeSpan.FromHours(1), "g");
			writer.WriteField(1234.5, "N2");
			writer.WriteField(Guid.Empty);
			writer.WriteField((object)DayOfWeek.Monday);
			writer.WriteField(new Uri("https://example.com/a"));
			writer.WriteField(false);
			writer.EndRecord();
		}

		var sheet = Written.Entry(stream.ToArray(), "xl/worksheets/sheet1.xml");
		Assert.Contains(
			"<c r=\"A1\" t=\"inlineStr\"><is><t>2024-01-15</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>1:00:00</t></is></c><c r=\"C1\" t=\"inlineStr\"><is><t>1,234.50</t></is></c>" +
			"<c r=\"D1\" t=\"inlineStr\"><is><t>00000000-0000-0000-0000-000000000000</t></is></c><c r=\"E1\" t=\"inlineStr\"><is><t>Monday</t></is></c>" +
			"<c r=\"F1\" t=\"inlineStr\"><is><t>https://example.com/a</t></is></c><c r=\"G1\" t=\"b\"><v>0</v></c>",
			sheet);
	}

	[Fact]
	public void TypesAColumnByEachFieldsType()
	{
		// A column's type can change from field to field through WriteField(object).
		var options = new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false } };
		var stream = new MemoryStream();
		using (var workbook = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true))
		{
			using var writer = workbook.AddSheet("Mixed");
			foreach (var value in new object?[] { 1, "1", 1.5, true, null, new DateTime(2024, 1, 15), 2 })
			{
				writer.WriteField(value);
				writer.EndRecord();
			}
		}

		var cells = Regex.Matches(Written.Entry(stream.ToArray(), "xl/worksheets/sheet1.xml"), "<c [^>]*>").Cast<Match>().Select(m => m.Value);
		Assert.Equal(["<c r=\"A1\">", "<c r=\"A2\" t=\"inlineStr\">", "<c r=\"A3\">", "<c r=\"A4\" t=\"b\">", "<c r=\"A6\" s=\"1\">", "<c r=\"A7\">"], cells);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WidensColumnsByTheFirstDataRecord(bool async)
	{
		var workbook = await Write(Records, async);

		// The date and time column gets a date and time's width in seconds, the duration its text's (36:00:00 fits).
		Assert.Contains("<cols><col min=\"8\" max=\"8\" width=\"18.71484375\" customWidth=\"1\"/></cols><sheetData>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
	}

	[Fact]
	public void WidensColumnsOfEachKind()
	{
		// Phase 4's widths, of the first data record's dates, times and durations: without auto-fit.
		var options = Written.Phase4(new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false, ValidateFieldCount = false } });
		var stream = new MemoryStream();
		using (var workbook = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true))
		{
			using var writer = workbook.AddSheet("Widths");
			writer.WriteField(new DateTime(2024, 1, 15));
			writer.WriteField(new DateTime(2024, 1, 15, 1, 2, 3, 4));
			writer.WriteField(TimeSpan.FromHours(1));
			writer.WriteField(TimeSpan.FromDays(1000).Add(TimeSpan.FromMilliseconds(5)));
			writer.WriteField(12345678901234.5);
			writer.WriteField("a long text that would need a wider column");
			writer.WriteField(new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero));
			writer.EndRecord();
			// Later records don't change them.
			writer.WriteField(TimeSpan.FromDays(100000));
			writer.EndRecord();
		}

		var sheet = Written.Entry(stream.ToArray(), "xl/worksheets/sheet1.xml");
		Assert.Contains(
			$"<cols><col min=\"1\" max=\"1\" width=\"{W(ColumnWidths.DateTime)}\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"{W(ColumnWidths.DateTimeMilliseconds)}\" customWidth=\"1\"/>" +
			$"<col min=\"4\" max=\"4\" width=\"{W(ColumnWidths.Of("24000:00:00.005".AsSpan()))}\" customWidth=\"1\"/></cols>",
			sheet);
	}

	[Fact]
	public void TakesWidthsFromTheRecordAfterTheHeader()
	{
		// Without auto-fit, the header's cells set nothing; a discarded first data record's widths go with it; the next
		// record's are kept.
		var stream = new MemoryStream();
		using (var workbook = ExcelWorkbookWriter.Create(stream, Written.Phase4(Written.Ragged()), leaveOpen: true))
		{
			using var writer = workbook.AddSheet("Header");
			writer.WriteField(new DateTime(2024, 1, 15, 1, 2, 3, 4));
			writer.EndRecord();
			Assert.Throws<InvalidOperationException>(() => writer.WriteRecord(new Dictionary<string, object?> { ["A"] = TimeSpan.FromDays(1000), ["B"] = new string('x', 40000) }));
			writer.WriteRecord(new Dictionary<string, object?> { ["A"] = new DateTime(2024, 1, 15), ["B"] = "b" });
		}

		var sheet = Written.Entry(stream.ToArray(), "xl/worksheets/sheet1.xml");
		Assert.Contains($"<cols><col min=\"1\" max=\"1\" width=\"{W(ColumnWidths.DateTime)}\" customWidth=\"1\"/></cols>", sheet);
		Assert.Equal(["2024-01-15T01:02:03.004", "2024-01-15|b"], Written.ReadOne(stream.ToArray()));
	}

	[Fact]
	public void GivesTheHeadersCellsNoWidths()
	{
		// A header written by hand with a typed cell: without auto-fit, only the first data record's cells set widths.
		var stream = new MemoryStream();
		using (var workbook = ExcelWorkbookWriter.Create(stream, Written.Phase4(), leaveOpen: true))
		{
			using var writer = workbook.AddSheet("Header");
			writer.WriteField(new DateTime(2024, 1, 15, 1, 2, 3, 4));
			writer.WriteField(TimeSpan.FromDays(1000));
			writer.EndRecord();
			writer.WriteField(new DateTime(2024, 1, 15));
			writer.WriteField(TimeSpan.FromHours(1));
			writer.EndRecord();
		}

		var workbookBytes = stream.ToArray();
		Assert.Contains($"<cols><col min=\"1\" max=\"1\" width=\"{W(ColumnWidths.DateTime)}\" customWidth=\"1\"/></cols>", Written.Entry(workbookBytes, "xl/worksheets/sheet1.xml"));
		Assert.Empty(Written.ValidationErrors(workbookBytes));
	}

	[Fact]
	public void HoldsRecordsUntilTheWidthsAreKnown()
	{
		// Without auto-fit, a header bigger than the flush size, and a flush asked for, write nothing until the first data
		// record ends.
		var stream = new MemoryStream();
		using (var workbook = ExcelWorkbookWriter.Create(stream, Written.Phase4(Written.Ragged()), leaveOpen: true))
		{
			using var writer = workbook.AddSheet("Held");
			for (var i = 0; i < 20; i++)
			{
				writer.WriteField(new string('h', 5000));
			}

			writer.EndRecord();
			writer.Flush();
			Assert.Equal(0, stream.Length);
			writer.WriteField(new DateTime(2024, 1, 15, 1, 2, 3));
			writer.EndRecord();
			Assert.True(stream.Length > 0);
		}

		var sheet = Written.Entry(stream.ToArray(), "xl/worksheets/sheet1.xml");
		Assert.Contains($"<cols><col min=\"1\" max=\"1\" width=\"{W(ColumnWidths.DateTime)}\" customWidth=\"1\"/></cols><sheetData><row r=\"1\">", sheet);
	}

	[Theory]
	[InlineData("00123", true)]
	[InlineData("1.5", true)]
	[InlineData("-1", true)]
	[InlineData("+1", true)]
	[InlineData(".5", true)]
	[InlineData("1E5", true)]
	[InlineData("a1", false)]
	[InlineData(" 1", false)]
	public async Task IgnoresNumbersStoredAsTextOverTheUsedRange(string text, bool ignored)
	{
		var workbook = await Written.RowsAsync([["Name", "Code"], ["a", text], ["b"]], false, Written.Ragged());

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		if (ignored)
		{
			Assert.EndsWith("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/><ignoredErrors><ignoredError sqref=\"A1:B3\" numberStoredAsText=\"1\"/></ignoredErrors></worksheet>", sheet);
		}
		else
		{
			Assert.DoesNotContain("ignoredErrors", sheet);
		}

		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Fact]
	public void IgnoresNoNumbersOfDiscardedRecords()
	{
		var stream = new MemoryStream();
		using (var workbook = ExcelWorkbookWriter.Create(stream, Written.Ragged(), leaveOpen: true))
		{
			using var writer = workbook.AddSheet("Discarded");
			writer.WriteRecord(new WrittenText { Name = "a" });
			Assert.Throws<InvalidOperationException>(() => writer.WriteRecord(new WrittenText { Name = "00123", Note = new string('x', 40000) }));
			writer.WriteRecord(new WrittenText { Name = "b" });
		}

		Assert.DoesNotContain("ignoredErrors", Written.Entry(stream.ToArray(), "xl/worksheets/sheet1.xml"));
	}

	[Theory]
	[InlineData(typeof(string), "Text")]
	[InlineData(typeof(bool), "Bool")]
	[InlineData(typeof(sbyte), "Integer")]
	[InlineData(typeof(byte), "Integer")]
	[InlineData(typeof(short), "Integer")]
	[InlineData(typeof(ushort), "Integer")]
	[InlineData(typeof(int), "Integer")]
	[InlineData(typeof(uint), "Integer")]
	[InlineData(typeof(long), "Integer")]
	[InlineData(typeof(ulong), "Integer")]
	[InlineData(typeof(int?), "Integer")]
	[InlineData(typeof(decimal), "Decimal")]
	[InlineData(typeof(double), "Float")]
	[InlineData(typeof(float), "Float")]
	[InlineData(typeof(DateTime), "DateTime")]
	[InlineData(typeof(DateTimeOffset), "DateTimeOffset")]
	[InlineData(typeof(TimeSpan), "TimeSpan")]
	[InlineData(typeof(Guid), "Text")]
	[InlineData(typeof(DayOfWeek), "Text")]
	[InlineData(typeof(Uri), "Uri")]
	[InlineData(typeof(char), "Text")]
	[InlineData(typeof(object), "Text")]
	[InlineData(typeof(System.Numerics.BigInteger), "Text")]
#if NET
	[InlineData(typeof(DateOnly), "DateOnly")]
	[InlineData(typeof(TimeOnly), "TimeOnly")]
	[InlineData(typeof(Half), "Float")]
	[InlineData(typeof(Int128), "Integer")]
	[InlineData(typeof(UInt128), "Integer")]
#endif
	public void TypesEachKind(Type type, string kind)
	{
		Assert.Equal(kind, CellKinds.Of(type).ToString());
	}

	/// <summary>Writes records into one sheet, <c>Orders</c>, with the record methods.</summary>
	/// <typeparam name="T">The records' type.</typeparam>
	/// <param name="records">The records.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>The workbook's bytes.</returns>
	private static Task<byte[]> Write<T>(IEnumerable<T> records, bool async) =>
		Written.WorkbookAsync(async (book, a) =>
		{
			if (a)
			{
				await book.WriteSheetAsync("Orders", records);
			}
			else
			{
				book.WriteSheet("Orders", records);
			}
		}, async, Written.Phase4());

	/// <summary>A width as the writer writes it.</summary>
	/// <param name="width">The width.</param>
	/// <returns>Its shortest text.</returns>
	private static string W(double width) => width.ToString("R", CultureInfo.InvariantCulture);
}
