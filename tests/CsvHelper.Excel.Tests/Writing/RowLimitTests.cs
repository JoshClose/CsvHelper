using System.Text.RegularExpressions;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>A record of text, a date that widens its column, and text that may start like a number.</summary>
[CsvRecord]
public partial class LimitRecord
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A date cell, whose column gets a width.</summary>
	public DateTime When { get; set; }

	/// <summary>A text cell, or no cell when null.</summary>
	public string? Code { get; set; }
}

/// <summary>
/// The row limit, synchronously and asynchronously: <see cref="ExcelRowLimit.Throw"/>, and <see cref="ExcelRowLimit.NewSheet"/>
/// with the header and widths repeated, Excel's names for copies, and flushes across sheets. An internal limit of a few rows
/// stands in for Excel's 1,048,576, which one test writes.
/// </summary>
public class RowLimitTests
{
	/// <summary>Sync and async, with inline and shared strings.</summary>
	public static TheoryData<bool, ExcelStrings> Modes => new()
	{
		{ false, ExcelStrings.Inline },
		{ true, ExcelStrings.Inline },
		{ false, ExcelStrings.Shared },
		{ true, ExcelStrings.Shared },
	};

	/// <summary>Seven records, the fifth with text that starts like a number.</summary>
	private static readonly LimitRecord[] Records = [.. Enumerable.Range(0, 7).Select(i => new LimitRecord { Name = "n" + i, When = new DateTime(2024, 1, 15 + i, 10, 30, 0), Code = i == 4 ? "00123" : null })];

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ThrowsPastTheLastRow(bool async, ExcelStrings strings)
	{
		InvalidOperationException? e = null;
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 3;
			e = a
				? await Assert.ThrowsAsync<InvalidOperationException>(async () => await book.WriteSheetAsync("Orders", Records))
				: Assert.Throws<InvalidOperationException>(() => book.WriteSheet("Orders", Records));
		}, async, new ExcelWriterOptions { Strings = strings });

		Assert.Equal("The sheet Orders is full at row 3, Excel's last. Set ExcelWriterOptions.RowLimit to NewSheet to go on in a new sheet.", e!.Message);

		// The sheet keeps the rows before it, and the workbook is complete.
		Assert.Equal(["Orders: Name|When|Code / n0|2024-01-15T10:30:00| / n1|2024-01-16T10:30:00|"], Written.Sheets(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ThrowsForEveryFieldOfARecordPastTheLastRow(bool async, ExcelStrings strings)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 2;
			var writer = book.AddSheet("Q1 Orders");
			for (var i = 0; i < 2; i++)
			{
				writer.WriteField("r" + i);
				await Written.EndRecordAsync(writer, a);
			}

			var first = Assert.Throws<InvalidOperationException>(() => writer.WriteField("r2"));
			Assert.StartsWith("The sheet 'Q1 Orders' is full at row 2, Excel's last.", first.Message);
			Assert.Throws<InvalidOperationException>(() => writer.WriteField(""));
			Assert.Throws<InvalidOperationException>(() => writer.WriteField(5));
			Assert.Equal(["Q1 Orders"], book.SheetNames);
		}, async, new ExcelWriterOptions { Strings = strings, Csv = new CsvWriterOptions { ValidateFieldCount = false } });

		Assert.Equal(["Q1 Orders: r0 / r1"], Written.Sheets(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task GoesOnInNewSheetsWithTheHeaderAndWidthsRepeated(bool async, ExcelStrings strings)
	{
		IReadOnlyList<string> names = [];
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 3;
			if (a)
			{
				await book.WriteSheetAsync("Orders", Records);
			}
			else
			{
				book.WriteSheet("Orders", Records);
			}

			names = [.. book.SheetNames];
		}, async, new ExcelWriterOptions { Strings = strings, RowLimit = ExcelRowLimit.NewSheet });

		Assert.Equal(["Orders", "Orders (2)", "Orders (3)", "Orders (4)"], names);
		Assert.Equal(
			[
				"Orders: Name|When|Code / n0|2024-01-15T10:30:00| / n1|2024-01-16T10:30:00|",
				"Orders (2): Name|When|Code / n2|2024-01-17T10:30:00| / n3|2024-01-18T10:30:00|",
				"Orders (3): Name|When|Code / n4|2024-01-19T10:30:00|00123 / n5|2024-01-20T10:30:00|",
				"Orders (4): Name|When|Code / n6|2024-01-21T10:30:00|",
			],
			Written.Sheets(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));

		// Each sheet has the first sheet's widths, and the header as its row 1; only the third has text like a number.
		var sheets = Enumerable.Range(1, 4).Select(n => Written.Entry(workbook, $"xl/worksheets/sheet{n}.xml")).ToList();
		var cols = Regex.Match(sheets[0], "<cols>.*</cols>").Value;
		Assert.Contains("<col min=\"2\" max=\"2\" width=", cols);
		var header = Regex.Match(sheets[0], "<row r=\"1\">.*?</row>").Value;
		Assert.StartsWith("<row r=\"1\"><c r=\"A1\" t=", header);
		Assert.All(sheets, s => Assert.Contains(cols + "<sheetData>" + header + "<row r=\"2\">", s));
		Assert.Equal([false, false, true, false], sheets.Select(s => s.Contains("<ignoredErrors><ignoredError sqref=\"A1:C3\" numberStoredAsText=\"1\"/></ignoredErrors>")));

		// The header's strings are referred to from each sheet.
		if (strings == ExcelStrings.Shared)
		{
			Assert.Contains("count=\"20\" uniqueCount=\"11\"", Written.Entry(workbook, "xl/sharedStrings.xml"));
		}

		// The records read back from the sheets in turn.
		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		var read = book.Sheets.SelectMany(s =>
		{
			using var reader = book.OpenReader(s);

			return reader.GetRecords<LimitRecord>().ToList();
		}).ToList();
		// A null string is an empty cell, which reads as "", as in CSV.
		Assert.Equal(Records.Select(r => (r.Name, r.When, (string?)(r.Code ?? ""))), read.Select(r => (r.Name, r.When, r.Code)));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task GoesOnInNewSheetsWithoutAHeader(bool async, ExcelStrings strings)
	{
		IReadOnlyList<string> names = [];
		var rows = Enumerable.Range(1, 5).Select(i => new[] { "r" + i, i % 2 == 0 ? "1.5" : null });
		var options = new ExcelWriterOptions { Strings = strings, RowLimit = ExcelRowLimit.NewSheet, Csv = new CsvWriterOptions { HasHeader = false, ValidateFieldCount = false } };
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 2;
			var writer = book.AddSheet("Data");
			foreach (var row in rows)
			{
				foreach (var field in row)
				{
					writer.WriteField(field);
				}

				await Written.EndRecordAsync(writer, a);
			}

			await Written.DisposeAsync(writer, a);
			names = [.. book.SheetNames];
		}, async, options);

		Assert.Equal(["Data", "Data (2)", "Data (3)"], names);
		Assert.Equal(["Data: r1 / r2|1.5", "Data (2): r3 / r4|1.5", "Data (3): r5"], Written.Sheets(workbook));
		Assert.Contains("<sheetData><row r=\"1\"><c r=\"A1\" t=", Written.Entry(workbook, "xl/worksheets/sheet2.xml"));
		Assert.Contains("<ignoredError sqref=\"A1:B2\"", Written.Entry(workbook, "xl/worksheets/sheet2.xml"));
		Assert.DoesNotContain("ignoredErrors", Written.Entry(workbook, "xl/worksheets/sheet3.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task AddsNoSheetWhenTheLastRowIsTheLastRecord(bool async, ExcelStrings strings)
	{
		IReadOnlyList<string> names = [];
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 3;
			if (a)
			{
				await book.WriteSheetAsync("Orders", Records.Take(2));
			}
			else
			{
				book.WriteSheet("Orders", Records.Take(2));
			}

			names = [.. book.SheetNames];
		}, async, new ExcelWriterOptions { Strings = strings, RowLimit = ExcelRowLimit.NewSheet });

		Assert.Equal(["Orders"], names);
		Assert.Single(Written.Sheets(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task KeepsTheNewSheetWhenItsFirstRecordIsDiscarded(bool async, ExcelStrings strings)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 3;
			var writer = book.AddSheet("Data");
			await Row(writer, a, "Name", "Code");
			await Row(writer, a, "a", "1");
			await Row(writer, a, "b", "2");

			// The next sheet has begun when the record fails its field count, and keeps its header.
			await Assert.ThrowsAsync<InvalidOperationException>(() => Row(writer, a, "c", "3", "extra"));
			Assert.Equal(["Data", "Data (2)"], book.SheetNames);
			await Row(writer, a, "d", "4");

			// A record of that sheet that fails after the flush that wrote the full one keeps the rows before it.
			await Assert.ThrowsAsync<InvalidOperationException>(() => Row(writer, a, "x", "9", "extra"));
			await Row(writer, a, "e", "5");
			await Row(writer, a, "f", "6");
			await Written.DisposeAsync(writer, a);
		}, async, new ExcelWriterOptions { Strings = strings, RowLimit = ExcelRowLimit.NewSheet });

		Assert.Equal(["Data: Name|Code / a|1 / b|2", "Data (2): Name|Code / d|4 / e|5", "Data (3): Name|Code / f|6"], Written.Sheets(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task CoversTheHeaderInEachNewSheetsUsedRange(bool async, ExcelStrings strings)
	{
		// A header of text like numbers, wider than the rows after it: each sheet's ignoredErrors covers it, and the last
		// sheet has the header alone, its record having failed.
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 2;
			var writer = book.AddSheet("Data");
			await Row(writer, a, "1st", "2nd", "3rd");
			await Row(writer, a, "a", "", "");
			await Row(writer, a, "b", "", "");
			await Assert.ThrowsAsync<InvalidOperationException>(() => Row(writer, a, "c", "", "", ""));
			await Written.DisposeAsync(writer, a);
		}, async, new ExcelWriterOptions { Strings = strings, RowLimit = ExcelRowLimit.NewSheet });

		Assert.Equal(["Data: 1st|2nd|3rd / a||", "Data (2): 1st|2nd|3rd / b||", "Data (3): 1st|2nd|3rd"], Written.Sheets(workbook));
		Assert.Equal(
			["A1:C2", "A1:C2", "A1:C1"],
			Enumerable.Range(1, 3).Select(n => Regex.Match(Written.Entry(workbook, $"xl/worksheets/sheet{n}.xml"), "<ignoredError sqref=\"([^\"]*)\"").Groups[1].Value));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task NamesNewSheetsAsExcelNamesCopies(bool async, ExcelStrings strings)
	{
		IReadOnlyList<string> names = [];
		var options = new ExcelWriterOptions { Strings = strings, RowLimit = ExcelRowLimit.NewSheet, Csv = new CsvWriterOptions { HasHeader = false } };
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 2;

			// Data (2) is taken, as Excel takes DATA (3) to be: Data goes on in Data (4).
			foreach (var name in new[] { "Data (2)", "DATA (3)" })
			{
				var other = book.AddSheet(name);
				await Row(other, a, name);
				await Written.DisposeAsync(other, a);
			}

			foreach (var name in new[] { "Data", "Orders (9)", "abcdefghijklmnopqrstuvwxyz12345" })
			{
				var writer = book.AddSheet(name);
				for (var i = 1; i <= 3; i++)
				{
					await Row(writer, a, i.ToString(System.Globalization.CultureInfo.InvariantCulture));
				}

				await Written.DisposeAsync(writer, a);
			}

			names = [.. book.SheetNames];
		}, async, options);

		Assert.Equal(["Data (2)", "DATA (3)", "Data", "Data (4)", "Orders (9)", "Orders (10)", "abcdefghijklmnopqrstuvwxyz12345", "abcdefghijklmnopqrstuvwxyz1 (2)"], names);
		Assert.Equal(names, Written.Sheets(workbook).Select(s => s.Substring(0, s.IndexOf(": ", StringComparison.Ordinal))));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task FlushesAcrossSheets(bool async, ExcelStrings strings)
	{
		// Rows of 200 characters, so that each sheet is flushed in several pieces, one of them with the next sheet's start.
		var text = new string('x', 200);
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 1000;
			var writer = book.AddSheet("Big");
			for (var i = 0; i < 2500; i++)
			{
				writer.WriteField(i);
				writer.WriteField(text + i);
				await Written.EndRecordAsync(writer, a);
			}

			await Written.DisposeAsync(writer, a);
		}, async, new ExcelWriterOptions { Strings = strings, RowLimit = ExcelRowLimit.NewSheet, Csv = new CsvWriterOptions { HasHeader = false } });

		var sheets = Written.Read(workbook);
		Assert.Equal(["Big", "Big (2)", "Big (3)"], sheets.Select(s => s.Sheet));
		Assert.Equal([1000, 1000, 500], sheets.Select(s => s.Rows.Count));
		Assert.Equal(Enumerable.Range(0, 2500).Select(i => $"{i}|{text}{i}"), sheets.SelectMany(s => s.Rows));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Fact]
	public void ThrowsAtExcelsLastRow()
	{
		// 1,048,576 rows, Excel's last, and one more.
		using var stream = new MemoryStream();
		using (var book = ExcelWorkbookWriter.Create(stream, new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false } }, leaveOpen: true))
		{
			var writer = book.AddSheet("Rows");
			for (var i = 1; i <= 1_048_576; i++)
			{
				writer.WriteField(i);
				writer.EndRecord();
			}

			var e = Assert.Throws<InvalidOperationException>(() => writer.WriteField(0));
			Assert.Equal("The sheet Rows is full at row 1,048,576, Excel's last. Set ExcelWriterOptions.RowLimit to NewSheet to go on in a new sheet.", e.Message);
		}

		// The writer's open record has a field, which Dispose ended without a cell.
		using var workbook = ExcelWorkbook.Open(new MemoryStream(stream.ToArray()));
		using var reader = workbook.OpenReader(Written.AllRows);
		var rows = 0;
		var last = 0;
		while (reader.Read())
		{
			rows++;
			last = reader.Parse<int>(0);
		}

		Assert.Equal(1_048_576, rows);
		Assert.Equal(1_048_576, last);
	}

	[Fact]
	public void ChecksTheOption()
	{
		Assert.Equal("options", Assert.Throws<ArgumentOutOfRangeException>(() => ExcelWorkbookWriter.Create(new MemoryStream(), new ExcelWriterOptions { RowLimit = (ExcelRowLimit)2 })).ParamName);
		Assert.Throws<ArgumentOutOfRangeException>(() => ExcelWorkbookWriter.Create(new MemoryStream(), new ExcelWriterOptions { RowLimit = (ExcelRowLimit)(-1) }));
		Assert.Equal(ExcelRowLimit.Throw, ExcelWriterOptions.Default.RowLimit);
	}

	/// <summary>Writes a record by hand, ending it as the mode says.</summary>
	/// <param name="writer">The writer.</param>
	/// <param name="async">Whether to end it asynchronously.</param>
	/// <param name="fields">The fields.</param>
	/// <returns>A task that completes when the record ends.</returns>
	private static async Task Row(CsvWriter writer, bool async, params string[] fields)
	{
		foreach (var field in fields)
		{
			writer.WriteField(field);
		}

		await Written.EndRecordAsync(writer, async);
	}
}
