using System.Globalization;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>A record whose members have formats and cultures: written with its generated record writer.</summary>
[CsvRecord]
public partial class FormattedRecord
{
	/// <summary>Text.</summary>
	public string Name { get; set; } = "";

	/// <summary>A custom format: a date cell in <c>dd"."mm"."yyyy</c>, of the day the text shows.</summary>
	[CsvFormat("dd.MM.yyyy")]
	public DateTime Day { get; set; }

	/// <summary>A standard format in a culture: de-DE's short date and time.</summary>
	[CsvFormat("g")]
	[CsvCulture("de-DE")]
	public DateTime Local { get; set; }

	/// <summary>Names in a culture: under fr-FR's locale.</summary>
	[CsvFormat("MMMM yyyy")]
	[CsvCulture("fr-FR")]
	public DateTime Month { get; set; }

	/// <summary>A time of day: a fraction of a day.</summary>
	[CsvFormat("HH:mm")]
	public DateTime Clock { get; set; }

	/// <summary>No year: text.</summary>
	[CsvFormat("dd MMMM")]
	public DateTime NoYear { get; set; }

	/// <summary>A custom duration format: text.</summary>
	[CsvFormat("hh\\:mm")]
	public TimeSpan Took { get; set; }

	/// <summary>A duration in .NET's default format: a duration cell.</summary>
	[CsvFormat("c")]
	public TimeSpan Plain { get; set; }

	/// <summary>A number in a culture other than the writer's.</summary>
	[CsvCulture("de-DE")]
	public decimal Amount { get; set; }

	/// <summary>An offset's clock time: as the options say.</summary>
	[CsvFormat("yyyy-MM-dd HH:mm")]
	public DateTimeOffset Stamped { get; set; }
}

/// <summary>Formats that are .NET's defaults, and one whose text looks like the default's.</summary>
[CsvRecord]
public partial class FormatEdges
{
	/// <summary>.NET's default, <c>O</c>: read as it is without a format.</summary>
	[CsvFormat("O")]
	public DateTime Iso { get; set; }

	/// <summary>An empty format, which the record writer writes as <c>O</c>.</summary>
	[CsvFormat("")]
	public DateTime Empty { get; set; }

	/// <summary>A custom format whose text is in <c>c</c>'s layout, but drops the days: text.</summary>
	[CsvFormat("hh\\:mm\\:ss")]
	public TimeSpan LooksDefault { get; set; }

	/// <summary><c>t</c>, another name of <c>c</c>: a duration cell.</summary>
	[CsvFormat("t")]
	public TimeSpan Alias { get; set; }
}

/// <summary>A number in the invariant culture, for a writer in another.</summary>
[CsvRecord]
public partial class InvariantNumber
{
	/// <summary>Written with the invariant culture's <c>.</c>.</summary>
	[CsvCulture("")]
	public double Value { get; set; }

	/// <summary>Written with the writer's culture.</summary>
	public double Other { get; set; }
}

#if NET
/// <summary>.NET's newer types, with formats.</summary>
[CsvRecord]
public partial class FormattedModern
{
	/// <summary>A date.</summary>
	[CsvFormat("dd.MM.yyyy")]
	public DateOnly Day { get; set; }

	/// <summary>A time of day.</summary>
	[CsvFormat("hh:mm tt")]
	public TimeOnly Time { get; set; }

	/// <summary>The standard RFC 1123 format, which a <c>DateOnly</c> writes without a time.</summary>
	[CsvFormat("R")]
	public DateOnly Stamp { get; set; }
}
#endif

/// <summary>
/// Formatted values end to end, synchronously and asynchronously: dates and times in their members' formats as date
/// cells, read back by CsvHelper.Excel (through the members' formats), ClosedXML and ExcelDataReader; numbers in their
/// members' cultures; what stays text; the columns found with and without a header, and by a header written by hand.
/// </summary>
public class FormattedCellsTests
{
	/// <summary>Sync and async.</summary>
	public static TheoryData<bool> Modes => new() { false, true };

	/// <summary>A morning value, with seconds and milliseconds the formats drop.</summary>
	private static readonly DateTime When = new(2024, 1, 15, 10, 30, 5, 123);

	/// <summary>The record of the tests.</summary>
	private static readonly FormattedRecord Record = new()
	{
		Name = "apple", Day = When, Local = When, Month = When, Clock = When, NoYear = When, Took = new TimeSpan(1, 30, 0), Plain = new TimeSpan(1, 30, 0),
		Amount = 1234.5m, Stamped = new DateTimeOffset(When, TimeSpan.FromHours(2)),
	};

	/// <summary>Row 2's cells, as the writer writes them by default.</summary>
	private static readonly string Row2 =
		"<c r=\"B2\" s=\"1\"><v>45306</v></c><c r=\"C2\" s=\"2\"><v>45306.4375</v></c><c r=\"D2\" s=\"3\"><v>45292</v></c><c r=\"E2\" s=\"4\"><v>0.4375</v></c>" +
		"<c r=\"F2\" t=\"inlineStr\"><is><t>15 January</t></is></c><c r=\"G2\" t=\"inlineStr\"><is><t>01:30</t></is></c><c r=\"H2\" s=\"5\"><v>0.0625</v></c>" +
		"<c r=\"I2\"><v>1234.5</v></c><c r=\"J2\" t=\"inlineStr\"><is><t>2024-01-15 10:30</t></is></c></row>";

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesFormattedValuesAsTypedCells(bool async)
	{
		var workbook = await Write([Record], async);

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains(Row2, sheet);
		var styles = Written.Entry(workbook, "xl/styles.xml");
		Assert.Contains(
			"<numFmts count=\"4\"><numFmt numFmtId=\"164\" formatCode=\"dd&quot;.&quot;mm&quot;.&quot;yyyy\"/>" +
			"<numFmt numFmtId=\"165\" formatCode=\"dd&quot;.&quot;mm&quot;.&quot;yyyy&quot; &quot;hh&quot;:&quot;mm\"/>" +
			"<numFmt numFmtId=\"166\" formatCode=\"[$-40C]mmmm&quot; &quot;yyyy\"/><numFmt numFmtId=\"167\" formatCode=\"hh&quot;:&quot;mm\"/></numFmts>", styles);
		Assert.Contains("<xf numFmtId=\"46\" ", styles);
		// The dates' columns are as wide as their formats' widest texts; the time fits the default column.
		Assert.Contains(
			$"<cols><col min=\"2\" max=\"2\" width=\"{Width("dd.MM.yyyy", "")}\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"{Width("g", "de-DE")}\" customWidth=\"1\"/>" +
			$"<col min=\"4\" max=\"4\" width=\"{Width("MMMM yyyy", "fr-FR")}\" customWidth=\"1\"/></cols>", sheet);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ReadsFormattedValuesBackAsTheirTextsShowThem(bool async)
	{
		var workbook = await Write([Record], async);

		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		using var reader = book.OpenReader();
		var read = Assert.Single(reader.GetRecords<FormattedRecord>().ToList());
		Assert.Equal(new DateTime(2024, 1, 15), read.Day);
		Assert.Equal(new DateTime(2024, 1, 15, 10, 30, 0), read.Local);
		Assert.Equal(new DateTime(2024, 1, 1), read.Month);
		Assert.Equal(new TimeSpan(10, 30, 0), read.Clock.TimeOfDay);
		Assert.Equal((1, 15), (read.NoYear.Month, read.NoYear.Day));
		Assert.Equal(new TimeSpan(1, 30, 0), read.Took);
		Assert.Equal(new TimeSpan(1, 30, 0), read.Plain);
		Assert.Equal(1234.5m, read.Amount);
		Assert.Equal(new DateTime(2024, 1, 15, 10, 30, 0), read.Stamped.DateTime);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task IsReadByTheOracles(bool async)
	{
		var workbook = await Write([Record], async);

		// Both read the dates as dates; ClosedXML reads the time of day as a duration.
		Assert.Equal(
			[
				"B2=DateTime:2024-01-15T00:00:00.000", "C2=DateTime:2024-01-15T10:30:00.000", "D2=DateTime:2024-01-01T00:00:00.000", "E2=TimeSpan:10:30:00",
				"F2=Text:15 January", "G2=Text:01:30", "H2=TimeSpan:01:30:00", "I2=Number:1234.5", "J2=Text:2024-01-15 10:30",
			],
			Written.ClosedXmlValues(workbook).Where(c => c.Split('=')[0] is var cell && cell.EndsWith("2", StringComparison.Ordinal) && cell != "A2"));
		Assert.Equal(
			"String:apple|DateTime:2024-01-15T00:00:00.000|DateTime:2024-01-15T10:30:00.000|DateTime:2024-01-01T00:00:00.000|DateTime:1899-12-31T10:30:00.000|" +
			"String:15 January|String:01:30|TimeSpan:01:30:00|Double:1234.5|String:2024-01-15 10:30",
			Written.ExcelDataReaderValues(workbook)[1]);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FindsTheColumnsWithoutAHeader(bool async)
	{
		var options = new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false } };

		// The record writer's header, which isn't written, says which member each column is.
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet<FormattedRecord>("Data");
			if (a)
			{
				await writer.WriteRecordsAsync([Record]);
			}
			else
			{
				writer.WriteRecords([Record]);
			}

			await Written.DisposeAsync(writer, a);
		}, async, options);

		Assert.Contains(System.Text.RegularExpressions.Regex.Replace(Row2, "r=\"([A-J])2\"", "r=\"${1}1\""), Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FindsTheColumnsByTheNamesOfAHeaderWrittenByHand(bool async)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet<FormattedRecord>("Data");
			foreach (var name in new[] { "Amount", "Other", "Day", "day" })
			{
				writer.WriteField(name);
			}

			await Written.EndRecordAsync(writer, a);
			writer.WriteField(1234.5m, default, CultureInfo.GetCultureInfo("de-DE"));
			writer.WriteField(When, "dd.MM.yyyy");
			writer.WriteField(When, "dd.MM.yyyy");
			writer.WriteField(When, "dd.MM.yyyy");
			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
		}, async);

		// Amount and Day are members, under their names exactly; Other and day aren't, and their texts stay text.
		Assert.Contains(
			"<c r=\"A2\"><v>1234.5</v></c><c r=\"B2\" t=\"inlineStr\"><is><t>15.01.2024</t></is></c><c r=\"C2\" s=\"1\"><v>45306</v></c>" +
			"<c r=\"D2\" t=\"inlineStr\"><is><t>15.01.2024</t></is></c></row>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task KeepsTextThatTheMembersFormatDoesntReadBack(bool async)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet<FormattedRecord>("Data");
			writer.WriteField("Day");
			await Written.EndRecordAsync(writer, a);
			// Another format's text, and .NET's default text, in a column whose member has a format.
			writer.WriteField(When, "yyyy-MM-dd");
			await Written.EndRecordAsync(writer, a);
			writer.WriteField(When);
			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
		}, async, Written.Ragged());

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains("<c r=\"A2\" t=\"inlineStr\"><is><t>2024-01-15</t></is></c>", sheet);
		Assert.Contains("<c r=\"A3\" t=\"inlineStr\"><is><t>2024-01-15T10:30:05.1230000</t></is></c>", sheet);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ReadsDotNetsDefaultFormatsAsWithoutOne(bool async)
	{
		var duration = new TimeSpan(2, 1, 30, 0);

		var workbook = await Write([new FormatEdges { Iso = When, Empty = When, LooksDefault = duration, Alias = duration }], async);

		// O and an empty format are typed as .NET's default text is; a custom duration format is text, even in c's layout.
		Assert.Contains(
			"<c r=\"A2\" s=\"1\"><v>45306.43755929398</v></c><c r=\"B2\" s=\"1\"><v>45306.43755929398</v></c><c r=\"C2\" t=\"inlineStr\"><is><t>01:30:00</t></is></c>" +
			"<c r=\"D2\" s=\"2\"><v>2.0625</v></c>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
	}

	[Fact]
	public void ReadsANumberInADateMembersColumnWithTheWritersCulture()
	{
		var options = new ExcelWriterOptions { Csv = new CsvWriterOptions { FormatProvider = CultureInfo.GetCultureInfo("de-DE") } };
		var stream = new MemoryStream();

		// A column whose member has a format but no culture reads numbers with the writer's.
		using (var book = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true))
		{
			using var writer = book.AddSheet<FormattedRecord>("Data");
			writer.WriteField("Day");
			writer.EndRecord();
			writer.WriteField(1234.5m);
			writer.EndRecord();
		}

		Assert.Contains("<c r=\"A2\"><v>1234.5</v></c>", Written.Entry(stream.ToArray(), "xl/worksheets/sheet1.xml"));
	}

	[Fact]
	public async Task WritesTheFormatsOfAnAsynchronousSequence()
	{
		var workbook = await Written.WorkbookAsync((book, _) => book.WriteSheetAsync("Data", Sequence()).AsTask(), async: true);

		Assert.Contains(Row2, Written.Entry(workbook, "xl/worksheets/sheet1.xml"));

		static async IAsyncEnumerable<FormattedRecord> Sequence()
		{
			await Task.Yield();

			yield return Record;
		}
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task KeepsFormattedTextAsTextWithoutAType(bool async)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Data");
			if (a)
			{
				await writer.WriteRecordsAsync([Record]);
			}
			else
			{
				writer.WriteRecords([Record]);
			}

			await Written.DisposeAsync(writer, a);
		}, async);

		// AddSheet without T knows no member's format: only the duration in .NET's default format is typed.
		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains("<c r=\"B2\" t=\"inlineStr\"><is><t>15.01.2024</t></is></c>", sheet);
		Assert.Contains("<c r=\"E2\" t=\"inlineStr\"><is><t>10:30</t></is></c>", sheet);
		Assert.Contains("<c r=\"H2\" s=\"1\"><v>0.0625</v></c><c r=\"I2\" t=\"inlineStr\"><is><t>1234,5</t></is></c>", sheet);
	}

	[Theory]
	[InlineData(ExcelDateTimeOffsets.Text, "<c r=\"J2\" t=\"inlineStr\"><is><t>2024-01-15 10:30</t></is></c>")]
	[InlineData(ExcelDateTimeOffsets.Utc, "<c r=\"J2\" t=\"inlineStr\"><is><t>2024-01-15 10:30</t></is></c>")]
	[InlineData(ExcelDateTimeOffsets.ClockTime, "<c r=\"J2\" s=\"6\"><v>45306.4375</v></c>")]
	public async Task FollowsTheOptionsForAnOffsetsFormat(ExcelDateTimeOffsets offsets, string cell)
	{
		// The text shows the clock time without its offset, which is no UTC time.
		var workbook = await Write([Record], async: false, new ExcelWriterOptions { DateTimeOffsets = offsets });

		Assert.Contains(cell, Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		if (offsets == ExcelDateTimeOffsets.ClockTime)
		{
			Assert.Contains("<numFmt numFmtId=\"168\" formatCode=\"yyyy&quot;-&quot;mm&quot;-&quot;dd&quot; &quot;hh&quot;:&quot;mm\"/>", Written.Entry(workbook, "xl/styles.xml"));
		}
	}

	[Theory]
	[InlineData(ExcelOutOfRangeDates.Text)]
	[InlineData(ExcelOutOfRangeDates.Throw)]
	public async Task FollowsTheOptionsForAFormattedDateBefore1900(ExcelOutOfRangeDates outOfRange)
	{
		var old = new FormattedRecord { Name = "old", Day = new DateTime(1899, 12, 31) };
		var options = new ExcelWriterOptions { OutOfRangeDates = outOfRange };

		if (outOfRange == ExcelOutOfRangeDates.Throw)
		{
			var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Write([old], async: false, options));
			Assert.Equal("Cell Data!B2 holds 31.12.1899, before 1900-01-01, Excel's first date (ExcelWriterOptions.OutOfRangeDates is Throw).", error.Message);

			return;
		}

		Assert.Contains("<c r=\"B2\" t=\"inlineStr\"><is><t>31.12.1899</t></is></c>", Written.Entry(await Write([old], async: false, options), "xl/worksheets/sheet1.xml"));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ReadsNumbersWithTheirMembersCulture(bool async)
	{
		var options = new ExcelWriterOptions { Csv = new CsvWriterOptions { FormatProvider = CultureInfo.GetCultureInfo("de-DE") } };

		// Under a de-DE writer, an invariant member's 1234.5 is a number, and so is the other member's 1234,5.
		var workbook = await Written.WorkbookAsync(
			(book, a) => a ? book.WriteSheetAsync("Data", [new InvariantNumber { Value = 1234.5, Other = 1234.5 }]).AsTask() : Sync(() => book.WriteSheet("Data", [new InvariantNumber { Value = 1234.5, Other = 1234.5 }])),
			async, options);

		Assert.Contains("<c r=\"A2\"><v>1234.5</v></c><c r=\"B2\"><v>1234.5</v></c>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		using var reader = book.OpenReader();
		var read = Assert.Single(reader.GetRecords<InvariantNumber>().ToList());
		Assert.Equal((1234.5, 1234.5), (read.Value, read.Other));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task KeepsTheFormatsInSheetsThatTheRowLimitAdds(bool async)
	{
		var records = Enumerable.Range(0, 4).Select(i => new FormattedRecord { Name = "r" + i, Day = When.AddDays(i) }).ToList();

		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 3;
			if (a)
			{
				await book.WriteSheetAsync("Data", records);
			}
			else
			{
				book.WriteSheet("Data", records);
			}
		}, async, new ExcelWriterOptions { RowLimit = ExcelRowLimit.NewSheet });

		Assert.Contains("<c r=\"B3\" s=\"1\"><v>45307</v></c>", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.Contains("<c r=\"B2\" s=\"1\"><v>45308</v></c>", Written.Entry(workbook, "xl/worksheets/sheet2.xml"));
		Assert.Contains("<c r=\"B3\" s=\"1\"><v>45309</v></c>", Written.Entry(workbook, "xl/worksheets/sheet2.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

#if NET
	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesTheNewerTypesInTheirFormats(bool async)
	{
		var record = new FormattedModern { Day = new DateOnly(2024, 1, 15), Time = new TimeOnly(22, 5, 9), Stamp = new DateOnly(2024, 1, 15) };

		var workbook = await Write([record], async);

		// The time shows no seconds, and the cell holds none.
		var time = ((22 * 60) + 5) / 1440.0;
		Assert.Contains($"<c r=\"A2\" s=\"1\"><v>45306</v></c><c r=\"B2\" s=\"2\"><v>{time.ToString("R", CultureInfo.InvariantCulture)}</v></c><c r=\"C2\" s=\"3\"><v>45306</v></c>",
			Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.Contains("formatCode=\"[$-409]hh&quot;:&quot;mm&quot; &quot;AM/PM\"", Written.Entry(workbook, "xl/styles.xml"));
		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		using var reader = book.OpenReader();
		var read = Assert.Single(reader.GetRecords<FormattedModern>().ToList());
		Assert.Equal((record.Day, new TimeOnly(22, 5), record.Stamp), (read.Day, read.Time, read.Stamp));
		Assert.Empty(Written.ValidationErrors(workbook));
	}
#endif

	/// <summary>Writes records into a sheet named <c>Data</c> with <see cref="ExcelWorkbookWriter.WriteSheet{T}(string, IEnumerable{T})"/> or its async form.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="records">The records.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <returns>The workbook's bytes.</returns>
	private static Task<byte[]> Write<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(MemberFormats.Members)] T>(IEnumerable<T> records, bool async, ExcelWriterOptions? options = null) =>
		Written.WorkbookAsync((book, a) => a ? book.WriteSheetAsync("Data", records).AsTask() : Sync(() => book.WriteSheet("Data", records)), async, options);

	/// <summary>Runs an action, as a completed task.</summary>
	/// <param name="action">The action.</param>
	/// <returns>A completed task.</returns>
	private static Task Sync(Action action)
	{
		action();

		return Task.CompletedTask;
	}

	/// <summary>The width a column of a format's dates gets, as the writer writes it.</summary>
	/// <param name="format">The .NET format.</param>
	/// <param name="culture">The culture's name.</param>
	/// <returns>The width's text.</returns>
	private static string Width(string format, string culture) =>
		FormatTranslator.Translate(format, CellKind.DateTime, CultureInfo.GetCultureInfo(culture))!.Width.ToString("R", CultureInfo.InvariantCulture);
}
