using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// <see cref="ExcelWorkbookWriter"/> end to end, each test synchronously and asynchronously: sheets written by the record
/// methods and by hand, read back by CsvHelper.Excel's reader, ClosedXML and ExcelDataReader, and checked by the Open XML
/// SDK's validator; the package's parts; and the errors.
/// </summary>
public class WorkbookWriterTests
{
	/// <summary>Sync and async, with inline and shared strings.</summary>
	public static IEnumerable<object[]> Modes() =>
		from async in new[] { false, true }
		from strings in new[] { ExcelStrings.Inline, ExcelStrings.Shared }
		select new object[] { async, strings };

	/// <summary>Texts that need escapes or care: entities, spaces at either end, line ends, controls, escapes' look-alikes.</summary>
	private static readonly string[] Tricky =
	[
		"a&b<c>d\"e'f", " lead", "trail ", "\ttab", "lf\n", "cr\rx", "crlf\r\n", "\r", "a" + (char)1 + "b" + (char)0x1F, "_x0041_", "_X0041_",
		"_x0041", "a_x00e9_b", ((char)0xFFFE).ToString() + (char)0xFFFF, "日本語", char.ConvertFromUtf32(0x1F600), "=1+1", "#N/A", "TRUE", "00123",
		"1.5", new string('x', 1000),
	];

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesRecordsAsTextCells(bool async, ExcelStrings strings)
	{
		var records = new[] { new WrittenText { Name = "apple", Note = "first" }, new WrittenText { Name = "pear" }, new WrittenText { Name = "plum", Note = "" } };

		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			if (a)
			{
				await book.WriteSheetAsync("Fruit", records);
			}
			else
			{
				book.WriteSheet("Fruit", records);
			}
		}, async, new ExcelWriterOptions { Strings = strings });

		Assert.Equal(["Name|Note", "apple|first", "pear|", "plum|"], Written.ReadOne(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
		var closed = Assert.Single(Written.ClosedXml(workbook));
		Assert.Equal("Fruit", closed.Sheet);
		Assert.Equal(["A1=Name", "B1=Note", "A2=apple", "B2=first", "A3=pear", "A4=plum"], closed.Cells);
		var excelDataReader = Assert.Single(Written.ExcelDataReader(workbook));
		Assert.Equal("Fruit", excelDataReader.Sheet);
		Assert.Equal(["Name|Note", "apple|first", "pear|", "plum|"], excelDataReader.Rows);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WritesAnAsynchronousSequence(bool async)
	{
		var workbook = await Written.WorkbookAsync(async (book, _) => await book.WriteSheetAsync("Fruit", Fruit(3)), async);

		Assert.Equal(["Name|Note", "fruit 0|note 0", "fruit 1|note 1", "fruit 2|note 2"], Written.ReadOne(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesRecordsAndFieldsByHand(bool async, ExcelStrings strings)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Mixed");
			writer.WriteRecords(new[] { new WrittenText { Name = "a", Note = "b" } });
			if (a)
			{
				await writer.WriteRecordAsync(new WrittenText { Name = "c" });
			}
			else
			{
				writer.WriteRecord(new WrittenText { Name = "c" });
			}

			writer.WriteField("by");
			writer.WriteField("hand".AsSpan());
			writer.WriteField("utf8"u8);
			writer.WriteField((string?)null);
			writer.WriteField("end");
			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
		}, async, Written.Ragged(strings));

		Assert.Equal(["Name|Note", "a|b", "c|", "by|hand|utf8||end"], Written.ReadOne(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WritesDynamicRecords(bool async)
	{
		var records = new[]
		{
			new Dictionary<string, object?> { ["Name"] = "apple", ["Count"] = "3" },
			new Dictionary<string, object?> { ["Name"] = "pear", ["Count"] = null },
		};

		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			if (a)
			{
				await book.WriteSheetAsync("Dynamic", records);
			}
			else
			{
				book.WriteSheet("Dynamic", records);
			}
		}, async);

		Assert.Equal(["Name|Count", "apple|3", "pear|"], Written.ReadOne(workbook));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WritesNoHeaderWithoutHasHeader(bool async)
	{
		var options = new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false } };

		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			if (a)
			{
				await book.WriteSheetAsync("Fruit", Fruit(2));
			}
			else
			{
				book.WriteSheet("Fruit", new[] { new WrittenText { Name = "fruit 0", Note = "note 0" } });
			}
		}, async, options);

		Assert.Equal(async ? ["fruit 0|note 0", "fruit 1|note 1"] : ["fruit 0|note 0"], Written.ReadOne(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesSheetsInTabOrder(bool async, ExcelStrings strings)
	{
		ExcelWorkbookWriter? writer = null;
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			writer = book;
			book.WriteSheet("First", new[] { new WrittenText { Name = "one" } });
			Assert.Equal(["First"], book.SheetNames);
			var second = book.AddSheet("Second");
			Assert.Equal(["First", "Second"], book.SheetNames);
			second.WriteField("two");
			await Written.EndRecordAsync(second, a);
			await Written.DisposeAsync(second, a);
			await book.WriteSheetAsync("Third", new[] { new WrittenText { Name = "one" } });
		}, async, new ExcelWriterOptions { Strings = strings });

		Assert.Equal(["First: Name|Note / one|", "Second: two", "Third: Name|Note / one|"], Written.Sheets(workbook));
		Assert.Equal(["First", "Second", "Third"], writer!.SheetNames);
		Assert.Empty(Written.ValidationErrors(workbook));
		Assert.Equal(["First", "Second", "Third"], Written.ExcelDataReader(workbook).Select(s => s.Sheet));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesTextThatNeedsEscapesSoThatItReadsBack(bool async, ExcelStrings strings)
	{
		var workbook = await Written.RowsAsync(Tricky.Select(t => new string?[] { t, t }), async, new ExcelWriterOptions { Strings = strings });

		Assert.Equal(Tricky.Select(t => t + "|" + t), Written.ReadOne(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));

		// ClosedXML reads an inline string as its XML has it, without decoding the escapes, and with LF as CR LF; it reads a
		// shared string as written.
		string ClosedXmlText(string t) => strings == ExcelStrings.Shared ? t : Written.EscapedText(t).Replace("\n", "\r\n");
		var closed = Assert.Single(Written.ClosedXml(workbook)).Cells;
		Assert.Equal(Tricky.SelectMany((t, i) => new[] { $"A{i + 1}={ClosedXmlText(t)}", $"B{i + 1}={ClosedXmlText(t)}" }), closed);
		var excelDataReader = Assert.Single(Written.ExcelDataReader(workbook)).Rows;
		Assert.Equal(Tricky.Select(t => t + "|" + t), excelDataReader);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task EscapesAsExcelDoes(bool async)
	{
		var workbook = await Written.RowsAsync([[" a", "cr\r", "_x0041_", "a&b"]], async);

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");

		Assert.Contains(
			"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t xml:space=\"preserve\"> a</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>cr_x000D_</t></is></c>" +
			"<c r=\"C1\" t=\"inlineStr\"><is><t>_x005F_x0041_</t></is></c><c r=\"D1\" t=\"inlineStr\"><is><t>a&amp;b</t></is></c></row>",
			sheet);
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ReplacesInvalidUtf8(bool async, ExcelStrings strings)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Bytes");
			writer.WriteField(new byte[] { 0x61, 0xFF, 0x62, 0xED, 0xA0, 0x80, 0x63 });
			writer.WriteField(new byte[] { 0xE2, 0x82 });
			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
		}, async, new ExcelWriterOptions { Strings = strings });

		// Each invalid sequence becomes U+FFFD as the runtime's decoder replaces it: .NET Framework's takes ED A0 80 for two
		// sequences, where .NET takes it for three.
		var expected = Encoding.UTF8.GetString([0x61, 0xFF, 0x62, 0xED, 0xA0, 0x80, 0x63]) + "|" + (char)0xFFFD;

		Assert.Equal([expected], Written.ReadOne(workbook));
		Assert.Contains((char)0xFFFD + "b" + (char)0xFFFD + (char)0xFFFD, expected);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesCellsUpTo32767CodeUnits(bool async, ExcelStrings strings)
	{
		var emoji = char.ConvertFromUtf32(0x1F600);
		var longest = new[] { new string('x', 32767), new string('x', 32765) + emoji };

		var workbook = await Written.RowsAsync([longest], async, new ExcelWriterOptions { Strings = strings });

		Assert.Equal([string.Join("|", longest)], Written.ReadOne(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task RefusesCellsPast32767CodeUnitsAndDiscardsTheirRecords(bool async, ExcelStrings strings)
	{
		var emoji = char.ConvertFromUtf32(0x1F600);
		var records = new[]
		{
			new WrittenText { Name = "kept" },
			new WrittenText { Name = "long", Note = new string('x', 32766) + emoji },
			new WrittenText { Name = "after" },
		};
		InvalidOperationException? error = null;

		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			error = a
				? await Assert.ThrowsAsync<InvalidOperationException>(async () => await book.WriteSheetAsync("Notes", records))
				: Assert.Throws<InvalidOperationException>(() => book.WriteSheet("Notes", records));
			book.WriteSheet("Next", new[] { new WrittenText { Name = "next" } });
		}, async, new ExcelWriterOptions { Strings = strings });

		Assert.Equal("Cell Notes!B3 has 32,768 characters, and Excel opens no cell of more than 32,767.", error!.Message);
		Assert.Equal(["Notes: Name|Note / kept|", "Next: Name|Note / next|"], Written.Sheets(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RefusesFieldsPastTheLastColumn(bool async)
	{
		InvalidOperationException? error = null;

		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Wide");
			for (var i = 0; i < 16384; i++)
			{
				writer.WriteField("v");
			}

			error = Assert.Throws<InvalidOperationException>(() => writer.WriteField("past"));
			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
		}, async);

		Assert.Equal("Record 1 has a field in column XFE, past XFD, Excel's last column.", error!.Message);
		var row = Assert.Single(Written.ReadOne(workbook));
		Assert.Equal(16384, row.Split('|').Length);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task LeavesGapsForRecordsWithoutAValue(bool async, ExcelStrings strings)
	{
		var workbook = await Written.RowsAsync([["a"], [""], [null, null], ["", "b"]], async, Written.Ragged(strings));

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");

		Assert.Equal(["1", "4"], Regex.Matches(sheet, "<row r=\"(\\d+)\"").Cast<Match>().Select(m => m.Groups[1].Value));
		Assert.Contains("<c r=\"B4\"", sheet);
		Assert.Equal(["a", "", "", "|b"], Written.ReadOne(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task StreamsSheetsLargerThanItsBuffer(bool async, ExcelStrings strings)
	{
		var rows = Enumerable.Range(0, 20000).Select(i => new string?[] { "row " + i, i % 3 == 0 ? null : new string('y', i % 50), "z" + (i % 100) }).ToList();

		var workbook = await Written.RowsAsync(rows, async, new ExcelWriterOptions { Strings = strings });

		Assert.Equal(rows.Select(r => string.Join("|", r.Select(f => f ?? ""))), Written.ReadOne(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task KeepsARecordWholeWhenFlushedInTheMiddle(bool async)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Flushed");
			writer.WriteField("first");
			await Written.EndRecordAsync(writer, a);
			writer.WriteField("a");
			if (a)
			{
				await writer.FlushAsync();
			}
			else
			{
				writer.Flush();
			}

			writer.WriteField("b");
			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
		}, async, Written.Ragged());

		Assert.Equal(["first", "a|b"], Written.ReadOne(workbook));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task DiscardsARecordFlushedInTheMiddle(bool async)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Flushed");
			writer.WriteField("a");
			writer.WriteField("b");
			await Written.EndRecordAsync(writer, a);
			writer.WriteField("half");
			if (a)
			{
				await writer.FlushAsync();
			}
			else
			{
				writer.Flush();
			}

			// One field where the first record has two: the record is discarded, though it was flushed in the middle.
			await Assert.ThrowsAsync<InvalidOperationException>(() => Written.EndRecordAsync(writer, a));
			writer.WriteField("c");
			writer.WriteField("d");
			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
		}, async);

		Assert.Equal(["a|b", "c|d"], Written.ReadOne(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task DiscardsARecordWhoseFieldCountIsWrong(bool async, ExcelStrings strings)
	{
		var options = new ExcelWriterOptions { Strings = strings, Csv = new CsvWriterOptions { ValidateFieldCount = true } };

		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Counted");
			writer.WriteField("a");
			writer.WriteField("b");
			await Written.EndRecordAsync(writer, a);
			writer.WriteField("lonely");
			await Assert.ThrowsAsync<InvalidOperationException>(() => Written.EndRecordAsync(writer, a));
			writer.WriteField("c");
			writer.WriteField("d");
			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
		}, async, options);

		Assert.Equal(["a|b", "c|d"], Written.ReadOne(workbook));
		Assert.DoesNotContain("lonely", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesOneEmptySheetWhenThereIsNone(bool async, ExcelStrings strings)
	{
		var workbook = await Written.WorkbookAsync((_, _) => Task.CompletedTask, async, new ExcelWriterOptions { Strings = strings });

		Assert.Equal(["Sheet1: "], Written.Sheets(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
		Assert.Equal("Sheet1", Assert.Single(Written.ClosedXml(workbook)).Sheet);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CompletesAnOpenSheetWhenDisposed(bool async)
	{
		CsvWriter? open = null;

		var workbook = await Written.WorkbookAsync((book, _) =>
		{
			open = book.AddSheet("Open");
			open.WriteField("ended");
			open.EndRecord();
			open.WriteField("left open");

			return Task.CompletedTask;
		}, async);

		// Disposing a CsvWriter ends its open record, as for CSV.
		Assert.Equal(["ended", "left open"], Written.ReadOne(workbook));
		Assert.Throws<ObjectDisposedException>(() => open!.WriteField("late"));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesThePartsAfterTheSheets(bool async, ExcelStrings strings)
	{
		var workbook = await Written.WorkbookAsync(async (book, _) =>
		{
			book.WriteSheet("A", new[] { new WrittenText { Name = "x" } });
			await book.WriteSheetAsync("B", new[] { new WrittenText { Name = "x" } });
		}, async, new ExcelWriterOptions { Strings = strings });

		using var archive = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
		var shared = strings == ExcelStrings.Shared ? new[] { "xl/sharedStrings.xml" } : [];

		Assert.Equal(
			["xl/worksheets/sheet1.xml", "xl/worksheets/sheet2.xml", .. shared, "xl/styles.xml", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "_rels/.rels", "[Content_Types].xml"],
			archive.Entries.Select(e => e.FullName));
		Assert.All(archive.Entries, e => Assert.Equal(new DateTime(1980, 1, 1), e.LastWriteTime.DateTime));
		Assert.Contains("<name val=\"Calibri\"/>", Written.Entry(workbook, "xl/styles.xml"));
		if (strings == ExcelStrings.Shared)
		{
			// Name, Note and x: three distinct strings, in six cells.
			Assert.Contains("count=\"6\" uniqueCount=\"3\"", Written.Entry(workbook, "xl/sharedStrings.xml"));
		}
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesTheSameBytesForTheSameSheets(bool async, ExcelStrings strings)
	{
		Task Write(ExcelWorkbookWriter book, bool a) => book.WriteSheetAsync("Same", Fruit(100)).AsTask();
		var options = new ExcelWriterOptions { Strings = strings };

		var first = await Written.WorkbookAsync(Write, async, options);
		var second = await Written.WorkbookAsync(Write, async, options);

		Assert.Equal(first, second);
	}

	[Theory]
	[InlineData(CompressionLevel.Optimal)]
	[InlineData(CompressionLevel.Fastest)]
	[InlineData(CompressionLevel.NoCompression)]
#if NET
	[InlineData(CompressionLevel.SmallestSize)]
#endif
	public async Task CompressesAsAsked(CompressionLevel level)
	{
		var workbook = await Written.WorkbookAsync((book, _) => book.WriteSheetAsync("Fruit", Fruit(1000)).AsTask(), true, new ExcelWriterOptions { CompressionLevel = level });

		Assert.Equal(1001, Written.ReadOne(workbook).Count);
		using var archive = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
		var sheet = archive.GetEntry("xl/worksheets/sheet1.xml")!;
		Assert.Equal(level == CompressionLevel.NoCompression, sheet.CompressedLength >= sheet.Length);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WritesNamesThatNeedEscapes(bool async)
	{
		string[] names = ["Q&A <1>", "\"Quoted\"", "a_x0041_b", "History", "日本語", " spaced "];

		var workbook = await Written.WorkbookAsync(async (book, _) =>
		{
			foreach (var name in names)
			{
				await book.WriteSheetAsync(name, Fruit(1));
			}
		}, async);

		Assert.Equal(names, Written.Read(workbook).Select(s => s.Sheet));
		// Neither ClosedXML nor ExcelDataReader decodes the escapes of sheet names, which Excel does.
		var undecoded = names.Select(n => n.Replace("_x0041_", "_x005F_x0041_"));
		Assert.Equal(undecoded, Written.ClosedXml(workbook).Select(s => s.Sheet));
		Assert.Equal(undecoded, Written.ExcelDataReader(workbook).Select(s => s.Sheet));
		Assert.Empty(Written.ValidationErrors(workbook));
		Assert.Contains("name=\"a_x005F_x0041_b\"", Written.Entry(workbook, "xl/workbook.xml"));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WritesToAStreamThatCantSeek(bool async)
	{
		using var memory = new MemoryStream();
		using (var stream = new ForwardOnlyStream(memory))
		{
			var book = ExcelWorkbookWriter.Create(stream);
			await book.WriteSheetAsync("Fruit", Fruit(2000));
			if (async)
			{
				await book.DisposeAsync();
			}
			else
			{
				book.Dispose();
			}

			Assert.True(stream.Disposed);
		}

		var workbook = memory.ToArray();

		Assert.Equal(2001, Written.ReadOne(workbook).Count);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task WritesFiles(bool async)
	{
		var path = Path.Combine(Path.GetTempPath(), $"csvhelper-excel-{Guid.NewGuid():N}.xlsx");
		File.WriteAllText(path, "overwritten");
		try
		{
			var book = ExcelWorkbookWriter.ToFile(path, fileOptions: async ? FileOptions.Asynchronous : FileOptions.None);
			await book.WriteSheetAsync("Fruit", Fruit(3));
			// Other processes can read the file while it is written.
			using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
			}

			if (async)
			{
				await book.DisposeAsync();
			}
			else
			{
				book.Dispose();
			}

			Assert.Equal(["Name|Note", "fruit 0|note 0", "fruit 1|note 1", "fruit 2|note 2"], Written.ReadOne(File.ReadAllBytes(path)));
			Assert.True(Api.Workbooks.IsClosed(path));
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void LeavesTheStreamOpenWhenAsked()
	{
		var open = new MemoryStream();
		var closed = new MemoryStream();

		ExcelWorkbookWriter.Create(open, leaveOpen: true).Dispose();
		ExcelWorkbookWriter.Create(closed).Dispose();

		Assert.True(open.CanWrite);
		Assert.False(closed.CanWrite);
	}

	[Fact]
	public void WritesOneSheetAtATime()
	{
		using var book = ExcelWorkbookWriter.Create(new MemoryStream());
		using var raw = book.AddSheet("Raw");

		var e = Assert.Throws<InvalidOperationException>(() => book.AddSheet("Next"));
		Assert.Equal("The sheet \"Raw\" is still being written: dispose its CsvWriter before adding a sheet. A workbook is written one sheet at a time.", e.Message);
		Assert.Throws<InvalidOperationException>(() => book.WriteSheet("Next", Array.Empty<WrittenText>()));
		Assert.Equal(["Raw"], book.SheetNames);
	}

	[Fact]
	public void RefusesNamesExcelTakesToBeTheSame()
	{
		using var book = ExcelWorkbookWriter.Create(new MemoryStream());
		book.WriteSheet("Data", Array.Empty<WrittenText>());

		var e = Assert.Throws<ArgumentException>(() => book.AddSheet("data"));
		Assert.StartsWith("The workbook already has the sheet \"Data\", which Excel takes \"data\" to be.", e.Message);
		Assert.Equal("name", e.ParamName);
		Assert.Throws<ArgumentException>(() => book.WriteSheet("DATA", Array.Empty<WrittenText>()));
		Assert.Throws<ArgumentException>(() => book.AddSheet("a/b"));
		Assert.Throws<ArgumentNullException>(() => book.AddSheet(null!));
		Assert.Throws<ArgumentNullException>(() => book.WriteSheet<WrittenText>("Other", null!));
		Assert.Equal(["Data"], book.SheetNames);
	}

	[Fact]
	public void RefusesToWriteAfterItIsDisposed()
	{
		var book = ExcelWorkbookWriter.Create(new MemoryStream());
		book.Dispose();
		book.Dispose();

		Assert.Throws<ObjectDisposedException>(() => book.AddSheet("Late"));
		Assert.Throws<ObjectDisposedException>(() => book.WriteSheet("Late", Array.Empty<WrittenText>()));
	}

	[Fact]
	public void WritesNoComments()
	{
		using var book = ExcelWorkbookWriter.Create(new MemoryStream());
		using var writer = book.AddSheet("Comments");

		Assert.Throws<NotSupportedException>(() => writer.WriteComment("no"));
	}

	[Fact]
	public async Task CompletesASheetWhoseRecordsCantBeWritten()
	{
		using var stream = new MemoryStream();
		using (var book = ExcelWorkbookWriter.Create(stream, leaveOpen: true))
		{
			// int has no record writer: the sheet is added, and completed empty.
			Assert.Throws<InvalidOperationException>(() => book.WriteSheet("Ints", new[] { 1, 2 }));
			await Assert.ThrowsAsync<InvalidOperationException>(async () => await book.WriteSheetAsync("Ints 2", new[] { 1, 2 }));
			book.WriteSheet("After", Array.Empty<WrittenText>());
		}

		Assert.Equal(["Ints: ", "Ints 2: ", "After: Name|Note"], Written.Sheets(stream.ToArray()));
	}

	[Fact]
	public void ChecksTheOptions()
	{
		Assert.Throws<ArgumentNullException>(() => ExcelWorkbookWriter.Create(null!));
		Assert.Throws<ArgumentNullException>(() => ExcelWorkbookWriter.ToFile(null!));
		Assert.Throws<ArgumentException>(() => ExcelWorkbookWriter.Create(new MemoryStream([], writable: false)));
		Assert.Equal("options", Assert.Throws<ArgumentException>(() => ExcelWorkbookWriter.Create(new MemoryStream(), new ExcelWriterOptions { Csv = null! })).ParamName);
		Assert.Equal("options", Assert.Throws<ArgumentOutOfRangeException>(() => ExcelWorkbookWriter.Create(new MemoryStream(), new ExcelWriterOptions { Strings = (ExcelStrings)2 })).ParamName);
		Assert.Equal("options", Assert.Throws<ArgumentOutOfRangeException>(() => ExcelWorkbookWriter.Create(new MemoryStream(), new ExcelWriterOptions { CompressionLevel = (CompressionLevel)9 })).ParamName);
		Assert.Throws<ArgumentOutOfRangeException>(() => ExcelWorkbookWriter.Create(new MemoryStream(), new ExcelWriterOptions { Csv = new CsvWriterOptions { BufferSize = 1 } }));
		Assert.Same(ExcelWriterOptions.Default, ExcelWorkbookWriter.Create(new MemoryStream()).Options);
	}

	/// <summary>Records of fruit, as an asynchronous sequence.</summary>
	/// <param name="count">The number of records.</param>
	/// <returns>The records.</returns>
	private static async IAsyncEnumerable<WrittenText> Fruit(int count)
	{
		for (var i = 0; i < count; i++)
		{
			await Task.Yield();

			yield return new WrittenText { Name = "fruit " + i, Note = "note " + i };
		}
	}

	/// <summary>A stream that writes forward only: it can't seek or tell its position.</summary>
	/// <param name="inner">The stream written to.</param>
	private sealed class ForwardOnlyStream(Stream inner) : Stream
	{
		/// <summary>Whether it has been disposed.</summary>
		public bool Disposed { get; private set; }

		/// <inheritdoc/>
		public override bool CanRead => false;

		/// <inheritdoc/>
		public override bool CanSeek => false;

		/// <inheritdoc/>
		public override bool CanWrite => !Disposed;

		/// <inheritdoc/>
		public override long Length => throw new NotSupportedException();

		/// <inheritdoc/>
		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		/// <inheritdoc/>
		public override void Flush() => inner.Flush();

		/// <inheritdoc/>
		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void SetLength(long value) => throw new NotSupportedException();

		/// <inheritdoc/>
		public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			Disposed = true;
			base.Dispose(disposing);
		}
	}
}
