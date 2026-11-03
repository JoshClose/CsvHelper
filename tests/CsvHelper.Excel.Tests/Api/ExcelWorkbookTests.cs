using CsvHelper.Excel.Tests.Sheet;

namespace CsvHelper.Excel.Tests.Api;

/// <summary>
/// <see cref="ExcelWorkbook"/>: opening from a path and a stream, the sheets, choosing the sheet a reader reads, options,
/// ownership and disposal. Each test runs synchronously and asynchronously.
/// </summary>
public class ExcelWorkbookTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ListsTheSheetsInTabOrder(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Workbooks.Orders(), async);

		Assert.Equal(
			["0 Chart Visible Chartsheet", "1 Hidden Hidden Worksheet", "2 Orders Visible Worksheet", "3 Q1 Orders Visible Worksheet"],
			workbook.Sheets.Select(s => $"{s.Index} {s.Name} {s.Visibility} {s.Kind}"));
		Assert.Equal("Q1 Orders", workbook.Sheets[3].ToString());
		Assert.False(workbook.Date1904);
		Assert.True(((ICollection<ExcelSheet>)workbook.Sheets).IsReadOnly);

		using var mac = await Workbooks.OpenAsync(Workbooks.Orders().Replace("xl/workbook.xml", "<workbookPr ", "<workbookPr date1904=\"1\" "), async);
		Assert.True(mac.Date1904);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task OpensAPathAndAStreamThatCantSeek(bool async)
	{
		var path = Workbooks.ToFile(Workbooks.Orders());
		try
		{
			using (var workbook = async ? await ExcelWorkbook.OpenAsync(path) : ExcelWorkbook.Open(path))
			{
				using var reader = await workbook.OpenReaderAsync(async, "Orders");
				Assert.Equal(Workbooks.OrderRows, await reader.ReadAllAsync(async));
			}

			Assert.True(Workbooks.IsClosed(path));
		}
		finally
		{
			File.Delete(path);
		}

		var stream = new TrickleStream(Workbooks.Orders().ToArray(), 4096);
		using (var workbook = async ? await ExcelWorkbook.OpenAsync(stream) : ExcelWorkbook.Open(stream))
		{
			using var reader = await workbook.OpenReaderAsync(async, "Orders");
			Assert.Equal(Workbooks.OrderRows, await reader.ReadAllAsync(async));
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task LeavesTheStreamOpenAsAsked(bool async)
	{
		foreach (var leaveOpen in new[] { false, true })
		{
			var stream = Workbooks.Orders().ToStream();
			var workbook = async ? await ExcelWorkbook.OpenAsync(stream, leaveOpen: leaveOpen) : ExcelWorkbook.Open(stream, leaveOpen: leaveOpen);
			if (async)
			{
				await workbook.DisposeAsync();
			}
			else
			{
				workbook.Dispose();
			}

			Assert.Equal(leaveOpen, stream.CanRead);
		}

		// An open that fails leaves the caller's stream open, as leaving it to the caller to dispose.
		var broken = Workbooks.Orders().Replace("xl/workbook.xml", "<sheets>", "<sheets><!DOCTYPE x>").ToStream();
		await Assert.ThrowsAsync<InvalidDataException>(async () => _ = async ? await ExcelWorkbook.OpenAsync(broken) : ExcelWorkbook.Open(broken));
		Assert.True(broken.CanRead);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ChecksTheOptions(bool async)
	{
		var cases = new (ExcelReaderOptions Options, Type Exception, string Message)[]
		{
			(new() { Sheet = "Orders", SheetIndex = 2 }, typeof(ArgumentException), "ExcelReaderOptions.Sheet and ExcelReaderOptions.SheetIndex can't both be set."),
			(new() { Csv = null! }, typeof(ArgumentException), "ExcelReaderOptions.Csv can't be null."),
			(new() { MaxPartSize = 0 }, typeof(ArgumentOutOfRangeException), "ExcelReaderOptions.MaxPartSize must be more than 0."),
			(new() { MaxUncompressedSize = -1 }, typeof(ArgumentOutOfRangeException), "ExcelReaderOptions.MaxUncompressedSize must be more than 0."),
		};

		using var workbook = await Workbooks.OpenAsync(Workbooks.Orders(), async);
		foreach (var (options, type, message) in cases)
		{
			var open = await Record.ExceptionAsync(() => Workbooks.OpenAsync(Workbooks.Orders(), async, options));
			var reader = await Record.ExceptionAsync(() => workbook.OpenReaderAsync(async, options));
			foreach (var e in new[] { open, reader })
			{
				Assert.IsType(type, e);
				Assert.StartsWith(message, e!.Message);
				Assert.Equal("options", ((ArgumentException)e).ParamName);
			}
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task GivesEachReaderItsOwnOptions(bool async)
	{
		var package = Workbooks.Orders();
		using var workbook = await Workbooks.OpenAsync(package, async, new ExcelReaderOptions { Sheet = "Orders", Csv = new() { HasHeader = false } });

		// The workbook's options, then a reader's, which replace them rather than merge.
		using (var reader = await workbook.OpenReaderAsync(async))
		{
			Assert.Equal(["Name|Quantity|Price|Paid|Note", .. Workbooks.OrderRows], await reader.ReadAllAsync(async));
		}

		using (var reader = await workbook.OpenReaderAsync(async, new ExcelReaderOptions { SheetIndex = 3 }))
		{
			Assert.Empty(await reader.ReadAllAsync(async));
			Assert.Equal(["q1"], reader.Headers);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ChecksEachCallAgainstItsOwnLimits(bool async)
	{
		// Shared strings longer than the styles, so that a limit between them stops only the shared strings.
		var package = Workbooks.Orders().Replace("xl/sharedStrings.xml", "</sst>", $"<si><t>{new string('y', 2000)}</t></si></sst>");
		var strings = package.Text("xl/sharedStrings.xml").Length;
		Assert.True(strings > package.Text("xl/styles.xml").Length);
		using var workbook = await Workbooks.OpenAsync(package, async);

		// The first reader loads the shared strings under its own limits.
		var e = await Assert.ThrowsAsync<InvalidDataException>(() => workbook.OpenReaderAsync(async, "Orders", new ExcelReaderOptions { MaxPartSize = strings - 1 }));
		Assert.StartsWith("xl/sharedStrings.xml is larger than ExcelReaderOptions.MaxPartSize", e.Message);

		using (var reader = await workbook.OpenReaderAsync(async, "Orders"))
		{
			Assert.Equal(3, (await reader.ReadAllAsync(async)).Count);
		}

		// Once loaded, they are kept: a later reader with the smaller limit doesn't read them again.
		using (var reader = await workbook.OpenReaderAsync(async, "Orders", new ExcelReaderOptions { MaxPartSize = strings - 1 }))
		{
			Assert.Equal(3, (await reader.ReadAllAsync(async)).Count);
		}

		// A sheet's cell past a reader's MaxPartSize stops that reader.
		var big = Workbooks.Data($"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>{new string('x', 5000)}</t></is></c></row>");
		using var bigWorkbook = await Workbooks.OpenAsync(big, async);
		using var limited = await bigWorkbook.OpenReaderAsync(async, new ExcelReaderOptions { MaxPartSize = 4096, Csv = new() { HasHeader = false } });
		e = await Assert.ThrowsAsync<InvalidDataException>(() => limited.ReadAllAsync(async));
		Assert.EndsWith("the token or cell there is larger than ExcelReaderOptions.MaxPartSize (4096 bytes).", e.Message);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task LeavesTheWorkbookOpenWhenAReaderIsDisposed(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Workbooks.Orders(), async);
		for (var i = 0; i < 3; i++)
		{
			using var reader = await workbook.OpenReaderAsync(async, "Orders");
			Assert.Equal(Workbooks.OrderRows, await reader.ReadAllAsync(async));
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StopsItsReadersWhenDisposed(bool async)
	{
		var workbook = await Workbooks.OpenAsync(Workbooks.Data(Workbooks.Numbers(2000)), async);
		var options = new ExcelReaderOptions { Csv = new() { BufferSize = 256 } };
		using var unread = await workbook.OpenReaderAsync(async, options);
		using var started = await workbook.OpenReaderAsync(async, options);
		Assert.True(async ? await started.ReadAsync() : started.Read());

		workbook.Dispose();

		// The next fill of each throws; a reader's records already filled can still be read.
		await Assert.ThrowsAsync<ObjectDisposedException>(async () => _ = async ? await unread.ReadAsync() : unread.Read());
		var e = await Assert.ThrowsAsync<ObjectDisposedException>(() => started.ReadAllAsync(async));
		Assert.Contains("The workbook is disposed.", e.Message);
		await Assert.ThrowsAsync<ObjectDisposedException>(() => workbook.OpenReaderAsync(async));

		// Also when the window holds the rest of the sheet, so that the scanner needs no read of the disposed stream.
		var small = await Workbooks.OpenAsync(Workbooks.Data(Workbooks.Numbers(100)), async);
		using var buffered = await small.OpenReaderAsync(async, options);
		Assert.True(async ? await buffered.ReadAsync() : buffered.Read());
		small.Dispose();
		var records = 0;
		e = await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
		{
			while (async ? await buffered.ReadAsync() : buffered.Read())
			{
				records++;
			}
		});
		Assert.InRange(records, 0, 98);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsWithSeveralReadersInTurn(bool async)
	{
		var package = TestPackage.Workbook(new TestSheet("A", xml: Sheets.Worksheet(Workbooks.Numbers(3000))), new TestSheet("B", xml: Sheets.Worksheet(Workbooks.Numbers(2000))));
		using var workbook = await Workbooks.OpenAsync(package, async);
		var options = new ExcelReaderOptions { Csv = new() { BufferSize = 256 } };
		using var first = await workbook.OpenReaderAsync(async, "A", options);
		using var second = await workbook.OpenReaderAsync(async, "B", options);
		using var again = await workbook.OpenReaderAsync(async, "A", options);
		var readers = new[] { first, second, again };
		var seen = new List<int>[] { [], [], [] };
		var more = new[] { true, true, true };
		while (more.Any(m => m))
		{
			for (var i = 0; i < readers.Length; i++)
			{
				if (more[i] && (more[i] = async ? await readers[i].ReadAsync() : readers[i].Read()))
				{
					seen[i].Add(readers[i].Parse<int>(0));
				}
			}
		}

		Assert.Equal(Enumerable.Range(1, 3000), seen[0]);
		Assert.Equal(Enumerable.Range(1, 2000), seen[1]);
		Assert.Equal(Enumerable.Range(1, 3000), seen[2]);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ChoosesTheSheet(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Workbooks.Orders(), async);

		// The first worksheet, hidden ones included, past the chartsheet.
		Assert.Equal(["hidden"], (await workbook.OpenReaderAsync(async)).Headers);

		Assert.Equal(Workbooks.OrderRows, await (await workbook.OpenReaderAsync(async, "orders")).ReadAllAsync(async));
		Assert.Equal(Workbooks.OrderRows, await (await workbook.OpenReaderAsync(async, 2)).ReadAllAsync(async));
		Assert.Equal(Workbooks.OrderRows, await (await workbook.OpenReaderAsync(async, workbook.Sheets[2])).ReadAllAsync(async));
		Assert.Equal(Workbooks.OrderRows, await (await workbook.OpenReaderAsync(async, new ExcelReaderOptions { Sheet = "ORDERS" })).ReadAllAsync(async));
		Assert.Equal(Workbooks.OrderRows, await (await workbook.OpenReaderAsync(async, new ExcelReaderOptions { SheetIndex = 2 })).ReadAllAsync(async));

		// A sheet given to OpenReader wins over the options'.
		Assert.Equal(["q1"], (await workbook.OpenReaderAsync(async, "Q1 Orders", new ExcelReaderOptions { Sheet = "Orders" })).Headers);
		Assert.Equal(["q1"], (await workbook.OpenReaderAsync(async, 3, new ExcelReaderOptions { SheetIndex = 2 })).Headers);
		Assert.Equal(["q1"], (await workbook.OpenReaderAsync(async, workbook.Sheets[3], new ExcelReaderOptions { Sheet = "Orders" })).Headers);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ThrowsForASheetItCantRead(bool async)
	{
		using var workbook = await Workbooks.OpenAsync(Workbooks.Orders(), async);
		const string Sheets = "Its sheets are \"Chart\" (chartsheet), \"Hidden\", \"Orders\", \"Q1 Orders\".";

		var name = await Assert.ThrowsAsync<ArgumentException>(() => workbook.OpenReaderAsync(async, "Order"));
		Assert.StartsWith($"The workbook has no sheet named \"Order\". {Sheets}", name.Message);
		Assert.Equal("sheetName", name.ParamName);

		name = await Assert.ThrowsAsync<ArgumentException>(() => workbook.OpenReaderAsync(async, new ExcelReaderOptions { Sheet = "Order" }));
		Assert.Equal("options", name.ParamName);

		foreach (var index in new[] { -1, 4 })
		{
			var e = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => workbook.OpenReaderAsync(async, index));
			Assert.StartsWith("The workbook has 4 sheets, so an index is 0 to 3.", e.Message);
			Assert.Equal("sheetIndex", e.ParamName);
		}

		Assert.Equal("options", (await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => workbook.OpenReaderAsync(async, new ExcelReaderOptions { SheetIndex = 9 }))).ParamName);

		var chart = await Assert.ThrowsAsync<NotSupportedException>(() => workbook.OpenReaderAsync(async, "Chart"));
		Assert.Equal("The sheet \"Chart\" is a chartsheet, and only worksheets have cells to read.", chart.Message);

		using var other = await Workbooks.OpenAsync(Workbooks.Orders(), async);
		var foreign = await Assert.ThrowsAsync<ArgumentException>(() => workbook.OpenReaderAsync(async, other.Sheets[2]));
		Assert.Equal("sheet", foreign.ParamName);

		await Assert.ThrowsAsync<ArgumentNullException>(() => workbook.OpenReaderAsync(async, (string)null!));
		await Assert.ThrowsAsync<ArgumentNullException>(() => workbook.OpenReaderAsync(async, (ExcelSheet)null!));

		using var missing = await Workbooks.OpenAsync(Workbooks.Orders().Remove("xl/worksheets/sheet3.xml"), async);
		var part = await Assert.ThrowsAsync<InvalidDataException>(() => missing.OpenReaderAsync(async, "Orders"));
		Assert.Equal("The sheet \"Orders\" can't be read: its part xl/worksheets/sheet3.xml is missing.", part.Message);

		using var charts = await Workbooks.OpenAsync(TestPackage.Workbook(new TestSheet("C1", type: "chartsheet"), new TestSheet("C2", type: "chartsheet")), async);
		var none = await Assert.ThrowsAsync<NotSupportedException>(() => charts.OpenReaderAsync(async));
		Assert.Equal("The workbook has no worksheet, and only worksheets have cells to read. Its sheets are \"C1\" (chartsheet), \"C2\" (chartsheet).", none.Message);
	}
}
