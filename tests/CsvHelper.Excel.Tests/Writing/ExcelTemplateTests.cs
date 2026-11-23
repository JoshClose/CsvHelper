using System.Diagnostics.CodeAnalysis;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>A record type whose name is longer than a sheet's, with a header name wider than Excel's default column.</summary>
[CsvRecord]
public partial class TemplateRecordWithANameLongerThanASheets
{
	/// <summary>A short name.</summary>
	public string Name { get; set; } = "";

	/// <summary>A long name, whose column auto-fit makes wider than Excel's default.</summary>
	public string TheCustomersFullNameAsOnTheInvoice { get; set; } = "";
}

/// <summary>
/// <see cref="ExcelTemplate"/> (Phase 7's step 5), from paths and streams, synchronously and asynchronously: a workbook of
/// one sheet of the type's header, validated and protected whatever look it takes, named after the type; its columns
/// Excel's default width at least; its checks before anything is written; the file deleted after a failure, and the
/// stream left open.
/// </summary>
public class ExcelTemplateTests
{
	/// <summary>The template's protection, without a password.</summary>
	private const string Protection = "<sheetProtection sheet=\"1\" objects=\"1\" scenarios=\"1\" formatColumns=\"0\" formatRows=\"0\"/>";

	/// <summary>Sync and async, to a stream and to a file.</summary>
	public static TheoryData<bool, bool> Modes => new() { { false, false }, { false, true }, { true, false }, { true, true } };

	/// <summary>Sync and async.</summary>
	public static TheoryData<bool> AsyncModes => new() { false, true };

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesTheTypesHeaderValidatedAndProtected(bool async, bool toFile)
	{
		var workbook = await CreateAsync<ValidatedOrder>(null, async, toFile);

		using (var book = ExcelWorkbook.Open(new MemoryStream(workbook)))
		{
			Assert.Equal([("ValidatedOrder", ExcelSheetVisibility.Visible), ("Lists", ExcelSheetVisibility.VeryHidden)], book.Sheets.Select(s => (s.Name, s.Visibility)));
		}

		Assert.Equal(["Id|Status|When|Region|Note"], Written.Read(workbook)[0].Rows);
		Assert.Equal(new Dictionary<string, string>
		{
			["Id"] = "whole -2147483648 2147483647 | General",
			["Status"] = "list \"Open,On hold,Closed\" | @",
			["When"] = "date 1 2958465.9999999884 | yyyy-mm-dd hh:mm:ss",
			["Region"] = "list _CsvHelperList1 title=Region prompt=The order's region. | @",
			["Note"] = "none | @",
		}, SheetValidations.Describe(workbook));
		Assert.Contains("</sheetData>" + Protection + "<dataValidations ", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(AsyncModes))]
	public async Task TakesItsLookFromTheOptionsThenTheAttributeThenTheWorkbook(bool async)
	{
		// The options' look, which needs no Protect for its password.
		var given = await CreateAsync<TemplatedOrder>(new ExcelTemplateOptions { Sheet = new ExcelSheetOptions { Title = "Given", Password = "secret" } }, async);
		var sheet = Written.Entry(given, "xl/worksheets/sheet1.xml");
		Assert.Contains("<t>Given</t>", sheet);
		Assert.Contains("<sheetProtection algorithmName=\"SHA-512\" ", sheet);
		Assert.Equal("whole -2147483648 2147483647 | General", SheetValidations.Describe(given, headerRow: 2)["Count"]);

		// The type's attribute, validated and protected though it says neither; its columns' attributes too.
		var attribute = await CreateAsync<SheetedOrder>(null, async);
		sheet = Written.Entry(attribute, "xl/worksheets/sheet1.xml");
		Assert.Contains("<t>Orders</t>", sheet);
		Assert.Contains("<pane xSplit=\"1\" ySplit=\"2\" ", sheet);
		Assert.Contains("</sheetData>" + Protection, sheet);
		Assert.Equal("decimal -7.92281625142643E+28 7.92281625142643E+28 | #,##0.00", SheetValidations.Describe(attribute, headerRow: 2)["Amount"]);

		// The workbook's look, for a type without an attribute: a plain header, which says its style.
		var workbook = await CreateAsync<ValidatedOrder>(new ExcelTemplateOptions { Excel = ExcelWriterOptions.Default with { Sheets = new ExcelSheetOptions { BoldHeader = false } } }, async);
		sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains("<sheetData><row><c s=\"0\" t=\"inlineStr\"><is><t>Id</t></is></c>", sheet);
		Assert.Contains("</sheetData>" + Protection, sheet);
		Assert.Empty(Written.ValidationErrors(given));
		Assert.Empty(Written.ValidationErrors(attribute));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Fact]
	public async Task NamesTheSheetAfterTheType()
	{
		var named = await CreateAsync<ValidatedOrder>(new ExcelTemplateOptions { SheetName = "Orders" }, false);
		var cut = await CreateAsync<TemplateRecordWithANameLongerThanASheets>(null, false);

		Assert.Equal("Orders", SheetNames(named)[0]);
		Assert.Equal("TemplateRecordWithANameLongerTh", SheetNames(cut)[0]);
		Assert.Equal("Dictionary", ExcelTemplate.SheetNameOf(typeof(Dictionary<,>)));
		Assert.Equal("List", ExcelTemplate.SheetNameOf(typeof(List<int>)));
	}

	[Theory]
	[MemberData(nameof(AsyncModes))]
	public async Task WidensShortColumnsToExcelsDefaultWidth(bool async)
	{
		var template = await CreateAsync<TemplateRecordWithANameLongerThanASheets>(null, async);
		var dated = await CreateAsync<ValidatedOrder>(null, async);
		var unfitted = await CreateAsync<ValidatedOrder>(new ExcelTemplateOptions { Sheet = new ExcelSheetOptions { AutoFit = 0 } }, async);
		using var stream = new MemoryStream();
		using (var book = ExcelWorkbookWriter.Create(stream, leaveOpen: true))
		{
			book.WriteSheet("Sheet", Array.Empty<TemplateRecordWithANameLongerThanASheets>(), new ExcelSheetOptions { Validate = true, Protect = true });
		}

		// A short name's column is Excel's default width, where the same sheet written by hand is as wide as the name; a
		// long name's is as wide as it.
		var sheet = SheetValidations.Widths(Written.Checked(stream.ToArray()));
		var widths = SheetValidations.Widths(template);
		Assert.True(sheet["Name"] < ColumnWidths.Default);
		Assert.Equal(ColumnWidths.Default, widths["Name"]);
		Assert.True(sheet["TheCustomersFullNameAsOnTheInvoice"] > ColumnWidths.Default);
		Assert.Equal(sheet["TheCustomersFullNameAsOnTheInvoice"], widths["TheCustomersFullNameAsOnTheInvoice"]);
		// A date's column is as wide as its widest value; without auto-fit, the others have Excel's default width.
		Assert.Equal(new Dictionary<string, double>
		{
			["Id"] = ColumnWidths.Default,
			["Status"] = ColumnWidths.Default,
			["When"] = ColumnWidths.Fit(ColumnWidths.TextPixels("9999-12-31 23:59:59".AsSpan())),
			["Region"] = ColumnWidths.Default,
			["Note"] = ColumnWidths.Default,
		}, SheetValidations.Widths(dated));
		Assert.Equal(new Dictionary<string, double>
		{
			["Id"] = 0,
			["Status"] = 0,
			["When"] = ColumnWidths.OfPixels(ColumnWidths.TextPixels("9999-12-31 23:59:59".AsSpan())),
			["Region"] = 0,
			["Note"] = 0,
		}, SheetValidations.Widths(unfitted));
	}

	[Fact]
	public void RefusesATableBeforeWriting()
	{
		var path = NewPath();

		var given = Assert.Throws<ArgumentException>(() => ExcelTemplate.Create<ValidatedOrder>(path, new ExcelTemplateOptions { Sheet = new ExcelSheetOptions { Table = new ExcelTableOptions() } }));
		var workbook = Assert.Throws<ArgumentException>(() => ExcelTemplate.Create<ValidatedOrder>(path, new ExcelTemplateOptions { Excel = ExcelWriterOptions.Default with { Sheets = new ExcelSheetOptions { Table = new ExcelTableOptions() } } }));
		var attribute = Assert.Throws<InvalidOperationException>(() => ExcelTemplate.Create<TabledOrder>(path));

		const string Problem = "A template's sheet is protected, so it can't have a table, since Excel doesn't add rows to a table on a protected sheet: leave Table out.";
		Assert.StartsWith("ExcelTemplateOptions.Sheet: " + Problem, given.Message);
		Assert.StartsWith("ExcelWriterOptions.Sheets: " + Problem, workbook.Message);
		Assert.Equal(("options", "options"), (given.ParamName, workbook.ParamName));
		Assert.Equal("The [ExcelSheet] of TabledOrder: " + Problem, attribute.Message);
		Assert.False(File.Exists(path));
	}

	[Fact]
	public void RefusesOptionsBeforeWriting()
	{
		var path = NewPath();
		var noHeader = new ExcelTemplateOptions { Excel = ExcelWriterOptions.Default with { Csv = CsvWriterOptions.Default with { HasHeader = false } } };

		Assert.StartsWith("ExcelTemplateOptions.Excel: a template is its record type's header, and CsvWriterOptions.HasHeader is off.", Assert.Throws<ArgumentException>(() => ExcelTemplate.Create<ValidatedOrder>(path, noHeader)).Message);
		Assert.StartsWith("ExcelTemplateOptions.Excel can't be null.", Assert.Throws<ArgumentException>(() => ExcelTemplate.Create<ValidatedOrder>(path, new ExcelTemplateOptions { Excel = null! })).Message);
		Assert.StartsWith("ExcelTemplateOptions.Sheet: FreezeColumns is -1", Assert.Throws<ArgumentException>(() => ExcelTemplate.Create<ValidatedOrder>(path, new ExcelTemplateOptions { Sheet = new ExcelSheetOptions { FreezeColumns = -1 } })).Message);
		Assert.Equal("options", Assert.Throws<ArgumentException>(() => ExcelTemplate.Create<ValidatedOrder>(path, new ExcelTemplateOptions { SheetName = "a/b" })).ParamName);
		Assert.Throws<InvalidOperationException>(() => ExcelTemplate.Create<BadSheet>(path));
		Assert.Throws<ArgumentNullException>(() => ExcelTemplate.Create<ValidatedOrder>((string)null!));
		Assert.Throws<ArgumentNullException>(() => ExcelTemplate.Create<ValidatedOrder>((Stream)null!));
		Assert.Equal("stream", Assert.Throws<ArgumentException>(() => ExcelTemplate.Create<ValidatedOrder>(new MemoryStream([], false))).ParamName);
		Assert.False(File.Exists(path));
	}

	[Theory]
	[MemberData(nameof(AsyncModes))]
	public async Task DeletesItsFileWhenWritingFails(bool async)
	{
		var path = NewPath();
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		try
		{
			// A column the header doesn't have fails when the header ends; the file it overwrote is gone.
			File.WriteAllText(path, "an older file");
			var missing = new ExcelTemplateOptions { Sheet = new ExcelSheetOptions { Columns = new Dictionary<string, ExcelColumnOptions> { ["Missing"] = new() { Width = 10 } } } };
			var e = await Assert.ThrowsAsync<InvalidOperationException>(() => Create<ValidatedOrder>(path, missing, async));
			Assert.StartsWith("ExcelSheetOptions.Columns names columns that the header doesn't have: \"Missing\".", e.Message);
			Assert.False(File.Exists(path));

			// A type without a record writer.
			await Assert.ThrowsAsync<InvalidOperationException>(() => Create<PlainSheeted>(path, null, async));
			Assert.False(File.Exists(path));
		}
		finally
		{
			Directory.Delete(Path.GetDirectoryName(path)!, true);
		}
	}

	[Theory]
	[MemberData(nameof(AsyncModes))]
	public async Task WritesFromTheStreamsPositionAndLeavesItOpen(bool async)
	{
		// After bytes already written, through a stream that can't seek, so that the zip's offsets count from its own start
		// and it reads back on its own (a seekable stream's count from the stream's).
		using var memory = new MemoryStream();
		memory.Write([1, 2, 3], 0, 3);
		using var stream = new ForwardOnlyStream(memory);
		if (async)
		{
			await ExcelTemplate.CreateAsync<ValidatedOrder>(stream);
		}
		else
		{
			ExcelTemplate.Create<ValidatedOrder>(stream);
		}

		Assert.True(stream.CanWrite);
		var bytes = memory.ToArray();
		Assert.Equal([1, 2, 3], bytes.Take(3));
		Assert.Equal(["Id|Status|When|Region|Note"], Written.Read(Written.Checked([.. bytes.Skip(3)]))[0].Rows);
	}

	[Fact]
	public async Task CancelsBeforeWriting()
	{
		var path = NewPath();
		using var source = new CancellationTokenSource();
		source.Cancel();
		using var stream = new MemoryStream();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExcelTemplate.CreateAsync<ValidatedOrder>(path, null, source.Token).AsTask());
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExcelTemplate.CreateAsync<ValidatedOrder>(stream, null, source.Token).AsTask());

		Assert.False(File.Exists(path));
		Assert.Equal(0, stream.Length);
	}

	/// <summary>A new file's path, in a folder of its own that doesn't exist yet.</summary>
	/// <returns>The path.</returns>
	private static string NewPath() => Path.Combine(Path.GetTempPath(), $"CsvHelper.Excel.Tests.{Guid.NewGuid():N}", "Template.xlsx");

	/// <summary>A workbook's sheets' names.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <returns>The names, in order.</returns>
	private static List<string> SheetNames(byte[] workbook)
	{
		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));

		return [.. book.Sheets.Select(s => s.Name)];
	}

	/// <summary>Writes a template to a file, as the mode says, and checks the file's zip (<see cref="Written.CheckedFile(string)"/>).</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="path">The file's path.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>A task that completes when the template is written.</returns>
	private static async Task Create<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(string path, ExcelTemplateOptions? options, bool async)
	{
		if (async)
		{
			await ExcelTemplate.CreateAsync<T>(path, options);
		}
		else
		{
			ExcelTemplate.Create<T>(path, options);
		}

		Written.CheckedFile(path);
	}

	/// <summary>Writes a template, as the modes say, and returns its bytes, its zip checked (<see cref="Written.Checked(byte[])"/>).</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="toFile">Whether to write to a file rather than a stream.</param>
	/// <returns>The workbook.</returns>
	private static async Task<byte[]> CreateAsync<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(ExcelTemplateOptions? options, bool async, bool toFile = false)
	{
		if (toFile)
		{
			var path = NewPath();
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			try
			{
				await Create<T>(path, options, async);

				return File.ReadAllBytes(path);
			}
			finally
			{
				Directory.Delete(Path.GetDirectoryName(path)!, true);
			}
		}

		using var stream = new MemoryStream();
		if (async)
		{
			await ExcelTemplate.CreateAsync<T>(stream, options);
		}
		else
		{
			ExcelTemplate.Create<T>(stream, options);
		}

		return Written.Checked(stream.ToArray());
	}
}
