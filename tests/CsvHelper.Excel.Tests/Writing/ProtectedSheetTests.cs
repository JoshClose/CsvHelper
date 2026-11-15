using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ClosedXML.Excel;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// Protected sheets through the public API (Phase 7's step 4), synchronously and asynchronously: with
/// <see cref="ExcelSheetOptions.Protect"/>, the title's and the header's cells locked, the header's empty fields among them,
/// and so the columns past the header's; the cells below the header in its columns unlocked, those written in their own
/// formats and the columns' default styles, which the cells users add take; the protection's element, with a filter's
/// allowances and a password's hash, once for every sheet of <see cref="ExcelRowLimit.NewSheet"/>. Each workbook passes
/// the Open XML SDK's validator, and ClosedXML reads the locks and checks the password.
/// </summary>
public class ProtectedSheetTests
{
	/// <summary>SpreadsheetML's namespace.</summary>
	private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

	/// <summary>The element of a protected sheet without a password or a filter.</summary>
	private const string Protection = "<sheetProtection sheet=\"1\" objects=\"1\" scenarios=\"1\" formatColumns=\"0\" formatRows=\"0\"/>";

	/// <summary>Two orders.</summary>
	private static readonly ValidatedOrder[] Orders =
	[
		new() { Id = 1, Status = TicketStatus.OnHold, When = new DateTime(2024, 1, 15, 10, 30, 0), Region = "North, East", Note = "00123" },
		new() { Id = 2, Status = TicketStatus.Closed, When = new DateTime(2024, 1, 16), Region = "South, West", Note = "fine" },
	];

	/// <summary>Sync and async.</summary>
	public static TheoryData<bool> Modes => new() { false, true };

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ProtectsATemplateWithItsHeaderLocked(bool async)
	{
		var workbook = await Written.WorkbookAsync((book, a) => WriteSheet(book, "Orders", Array.Empty<ValidatedOrder>(), a, new ExcelSheetOptions { Validate = true, Protect = true }), async);

		// After the rows, before the validations, as the schema orders them.
		Assert.Contains("</sheetData>" + Protection + "<dataValidations ", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		// The header locked, its columns below it unlocked to Excel's last row, and the column past them locked.
		Assert.Equal(
			"A1:locked E1:locked F1:locked A2:unlocked E2:unlocked F2:locked A1048576:unlocked E1048576:unlocked F1048576:locked",
			Locks(workbook, 1, "A1", "E1", "F1", "A2", "E2", "F2", "A1048576", "E1048576", "F1048576"));
		// The columns keep their types' formats and validations.
		Assert.Equal(new Dictionary<string, string>
		{
			["Id"] = "whole -2147483648 2147483647 | General",
			["Status"] = "list \"Open,On hold,Closed\" | @",
			["When"] = "date 1 2958465.9999999884 | yyyy-mm-dd hh:mm:ss",
			["Region"] = "list _CsvHelperList1 title=Region prompt=The order's region. | @",
			["Note"] = "none | @",
		}, SheetValidations.Describe(workbook));
		// The lists sheet keeps its own protection, without the resizing.
		Assert.Contains("</sheetData><sheetProtection sheet=\"1\" objects=\"1\" scenarios=\"1\"/><pageMargins ", Written.Entry(workbook, "xl/worksheets/sheet2.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task UnlocksTheCellsWrittenBelowTheHeaderInTheirOwnFormats(bool async)
	{
		var plain = await Written.WorkbookAsync((book, a) => WriteSheet(book, "Orders", Orders, a, new ExcelSheetOptions()), async);
		var workbook = await Written.WorkbookAsync((book, a) => WriteSheet(book, "Orders", Orders, a, new ExcelSheetOptions { Protect = true }), async);

		Assert.Equal("A1:locked E1:locked A2:unlocked C2:unlocked E3:unlocked A4:unlocked F2:locked", Locks(workbook, 1, "A1", "E1", "A2", "C2", "E3", "A4", "F2"));
		// Each cell keeps its own format, unlocked: the dates' and General's; and the records read back as written.
		Assert.Equal(Style(plain, 1, "C2") with { Locked = false }, Style(workbook, 1, "C2"));
		Assert.Equal(Style(plain, 1, "C3") with { Locked = false }, Style(workbook, 1, "C3"));
		Assert.NotEqual(Style(workbook, 1, "C2").NumberFormatId, Style(workbook, 1, "C3").NumberFormatId);
		Assert.Equal((0, false, false), Style(workbook, 1, "A2"));
		Assert.Equal(Written.Read(plain)[0].Rows, Written.Read(workbook)[0].Rows);
		Assert.DoesNotContain("<sheetProtection", Written.Entry(plain, "xl/worksheets/sheet1.xml"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task LocksTheTitlesMergedCellsAndTheHeadersEmptyFields(bool async)
	{
		var options = new ExcelSheetOptions
		{
			Title = "Notes",
			BoldHeader = false,
			Protect = true,
			Columns = new Dictionary<string, ExcelColumnOptions> { ["Note"] = new() { Wrap = true, Format = "@" } },
		};
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Data", options);
			foreach (var row in new[] { new[] { "Name", "", "Note" }, ["Ann", "", "a note", "past the header"] })
			{
				foreach (var field in row)
				{
					writer.WriteField(field);
				}

				await Written.EndRecordAsync(writer, a);
			}

			await Written.DisposeAsync(writer, a);
		}, async, new ExcelWriterOptions { Csv = new CsvWriterOptions { ValidateFieldCount = false } });

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		// The title's row in the title's style, which its merged cells take; the header's empty field a cell, in General.
		Assert.Contains("<row r=\"1\" s=\"1\" customFormat=\"1\"><c r=\"A1\" s=\"1\" t=\"inlineStr\"><is><t>Notes</t></is></c></row>", sheet);
		Assert.Contains("<mergeCell ref=\"A1:C1\"/>", sheet);
		Assert.Contains("<c r=\"B2\" s=\"0\"/>", sheet);
		// A record's empty field has no cell, as without Protect: its column's style unlocks it.
		Assert.DoesNotContain("<c r=\"B3\"", sheet);
		Assert.Equal(
			"A1:locked B1:locked C1:locked D1:locked A2:locked B2:locked C2:locked A3:unlocked B3:unlocked C3:unlocked D3:locked B4:unlocked D4:locked",
			Locks(workbook, 1, "A1", "B1", "C1", "D1", "A2", "B2", "C2", "A3", "B3", "C3", "D3", "B4", "D4"));
		// A column's format and wrap, unlocked, its cells' and its default style.
		Assert.Equal((49, true, false), Style(workbook, 1, "C3"));
		Assert.Equal((49, true, false), Style(workbook, 1, "C9"));
		Assert.Equal(["Notes", "Name||Note", "Ann||a note|past the header"], Written.Read(workbook)[0].Rows);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesTheHeadersEmptyFieldsInBold(bool async)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Data", new ExcelSheetOptions { Protect = true });
			foreach (var field in new[] { "", "Name", "" })
			{
				writer.WriteField(field);
			}

			await Written.EndRecordAsync(writer, a);
			await Written.DisposeAsync(writer, a);
		}, async);

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		Assert.Contains("<row r=\"1\"><c r=\"A1\" s=\"1\"/><c r=\"B1\" s=\"1\" t=\"inlineStr\"><is><t>Name</t></is></c><c r=\"C1\" s=\"1\"/></row>", sheet);
		Assert.Equal("A1:locked C1:locked A2:unlocked C2:unlocked D2:locked", Locks(workbook, 1, "A1", "C1", "A2", "C2", "D2"));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task HashesThePasswordAsExcelChecksIt(bool async)
	{
		var workbook = await Written.WorkbookAsync((book, a) => WriteSheet(book, "Orders", Orders, a, new ExcelSheetOptions { Protect = true, Password = "secret" }), async);

		var element = XDocument.Parse(Written.Entry(workbook, "xl/worksheets/sheet1.xml")).Root!.Element(Main + "sheetProtection")!;
		var salt = Convert.FromBase64String((string)element.Attribute("saltValue")!);
		Assert.Equal(16, salt.Length);
		Assert.Equal(Convert.ToBase64String(SheetProtection.Hash("secret", salt, 100_000)), (string)element.Attribute("hashValue")!);
		Assert.Equal(("SHA-512", "100000", "1", "0", "0"), ((string)element.Attribute("algorithmName")!, (string)element.Attribute("spinCount")!, (string)element.Attribute("sheet")!, (string)element.Attribute("formatColumns")!, (string)element.Attribute("formatRows")!));
		// ClosedXML checks the password by the element's algorithm, salt and spins.
		using var book = new XLWorkbook(new MemoryStream(workbook));
		var orders = book.Worksheet("Orders");
		Assert.True(orders.IsProtected);
		Assert.ThrowsAny<Exception>(() => orders.Unprotect("Secret"));
		Assert.True(orders.IsProtected);
		orders.Unprotect("secret");
		Assert.False(orders.IsProtected);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task AllowsSortingAndFilteringWithAFilter(bool async)
	{
		var workbook = await Written.WorkbookAsync((book, a) => WriteSheet(book, "Orders", Orders, a, new ExcelSheetOptions { AutoFilter = true, Protect = true }), async);

		Assert.Contains(
			"</sheetData><sheetProtection sheet=\"1\" objects=\"1\" scenarios=\"1\" formatColumns=\"0\" formatRows=\"0\" sort=\"0\" autoFilter=\"0\"/><autoFilter ref=\"A1:E3\"/>",
			Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		using var book = new XLWorkbook(new MemoryStream(workbook));
		var protection = book.Worksheet("Orders").Protection;
		Assert.Equal(
			XLSheetProtectionElements.SelectEverything | XLSheetProtectionElements.FormatColumns | XLSheetProtectionElements.FormatRows | XLSheetProtectionElements.Sort | XLSheetProtectionElements.AutoFilter,
			protection.AllowedElements);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ProtectsEachSheetOfNewSheetWithOneHash(bool async)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 3;
			await WriteSheet(book, "Orders", Orders, a, new ExcelSheetOptions { Title = "Orders", Protect = true, Password = "secret" });
		}, async, new ExcelWriterOptions { RowLimit = ExcelRowLimit.NewSheet });

		// Each sheet repeats the title, in its row's style, and the header, locked; its record is unlocked.
		var first = XDocument.Parse(Written.Entry(workbook, "xl/worksheets/sheet1.xml")).Root!.Element(Main + "sheetProtection")!.ToString();
		var second = XDocument.Parse(Written.Entry(workbook, "xl/worksheets/sheet2.xml")).Root!.Element(Main + "sheetProtection")!.ToString();
		Assert.Equal(first, second);
		for (var sheet = 1; sheet <= 2; sheet++)
		{
			Assert.Equal("A1:locked B1:locked A2:locked A3:unlocked E3:unlocked F3:locked", Locks(workbook, sheet, "A1", "B1", "A2", "A3", "E3", "F3"));
		}

		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ProtectsConvertedCsv(bool async)
	{
		var csv = Encoding.UTF8.GetBytes("Id,Status,When,Region,Note\r\n7,Open,2024-01-15T10:30:00,\"North, East\",x\r\n");
		// A plain header, whose cells say General's style, locked; and no auto-fit, so that the header's columns have their
		// default styles without widths.
		var options = new CsvToExcelOptions { Sheet = new ExcelSheetOptions { Protect = true, BoldHeader = false, AutoFit = 0 } };
		using var typed = new MemoryStream();
		using var untyped = new MemoryStream();
		if (async)
		{
			await ExcelConvert.ToExcelAsync<ValidatedOrder>(new MemoryStream(csv), typed, options);
			await ExcelConvert.ToExcelAsync(new MemoryStream(csv), untyped, options);
		}
		else
		{
			ExcelConvert.ToExcel<ValidatedOrder>(new MemoryStream(csv), typed, options);
			ExcelConvert.ToExcel(new MemoryStream(csv), untyped, options);
		}

		foreach (var workbook in new[] { typed.ToArray(), untyped.ToArray() })
		{
			Assert.Contains("</sheetData>" + Protection, Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
			Assert.Equal("A1:locked E1:locked A2:unlocked E2:unlocked A3:unlocked F2:locked", Locks(workbook, 1, "A1", "E1", "A2", "E2", "A3", "F2"));
			Assert.Empty(Written.ValidationErrors(workbook));
		}
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ClosedXmlReadsTheLocks(bool async)
	{
		var workbook = await Written.WorkbookAsync((book, a) => WriteSheet(book, "Orders", Orders, a, new ExcelSheetOptions { Title = "Orders", Protect = true }), async);

		using var book = new XLWorkbook(new MemoryStream(workbook));
		var sheet = book.Worksheet("Orders");
		Assert.True(sheet.IsProtected);
		// Not the title's merged cells, which ClosedXML gives their columns' styles, unlocked, where Excel, LibreOffice and
		// GridCraft give them their row's, locked (checked on 2026-11-15).
		Assert.Equal(
			[true, true, false, false, false, true],
			new[] { "A1", "A2", "A3", "E4", "B9", "F3" }.Select(c => sheet.Cell(c).Style.Protection.Locked));
	}

	[Fact]
	public void UnlocksNothingWithoutProtect()
	{
		using var stream = new MemoryStream();
		using (var book = ExcelWorkbookWriter.Create(stream, leaveOpen: true))
		{
			book.WriteSheet("Orders", Orders, new ExcelSheetOptions { Validate = true });
		}

		var workbook = stream.ToArray();
		Assert.DoesNotContain("<sheetProtection", Written.Entry(workbook, "xl/worksheets/sheet1.xml"));
		Assert.DoesNotContain("<protection", Written.Entry(workbook, "xl/styles.xml"));
	}

	/// <summary>The lock of each of a sheet's cells, as Excel resolves its style (<see cref="Style"/>).</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet's number, from 1.</param>
	/// <param name="cells">The cells.</param>
	/// <returns>Each cell's lock, such as <c>A1:locked A2:unlocked</c>.</returns>
	private static string Locks(byte[] workbook, int sheet, params string[] cells) =>
		string.Join(" ", cells.Select(c => c + (Style(workbook, sheet, c).Locked ? ":locked" : ":unlocked")));

	/// <summary>
	/// A cell's style, as Excel resolves it: its own when the cell is in the file; else its row's, when the row has one
	/// (<c>customFormat</c>); else its column's default style; else the workbook's default.
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="sheet">The sheet's number, from 1.</param>
	/// <param name="cell">The cell, such as <c>B2</c>.</param>
	/// <returns>The style's number format's id, whether it wraps, and whether it is locked.</returns>
	private static (int NumberFormatId, bool Wrap, bool Locked) Style(byte[] workbook, int sheet, string cell)
	{
		var xfs = XDocument.Parse(Written.Entry(workbook, "xl/styles.xml")).Root!.Element(Main + "cellXfs")!.Elements(Main + "xf").ToList();
		var xml = XDocument.Parse(Written.Entry(workbook, $"xl/worksheets/sheet{sheet}.xml"));
		var match = Regex.Match(cell, "^([A-Z]+)([0-9]+)$");
		var column = match.Groups[1].Value.Aggregate(0, (n, c) => n * 26 + c - 'A' + 1);
		var row = xml.Descendants(Main + "row").SingleOrDefault(r => (string)r.Attribute("r")! == match.Groups[2].Value);
		int style;
		if (row?.Elements(Main + "c").SingleOrDefault(c => (string)c.Attribute("r")! == cell) is { } written)
		{
			style = (int?)written.Attribute("s") ?? 0;
		}
		else if (row is not null && (string?)row.Attribute("customFormat") == "1")
		{
			style = (int)row.Attribute("s")!;
		}
		else
		{
			var col = xml.Descendants(Main + "col").SingleOrDefault(c => (int)c.Attribute("min")! <= column && column <= (int)c.Attribute("max")!);
			style = (int?)col?.Attribute("style") ?? 0;
		}

		var xf = xfs[style];

		return ((int)xf.Attribute("numFmtId")!, (string?)xf.Element(Main + "alignment")?.Attribute("wrapText") == "1", (string?)xf.Element(Main + "protection")?.Attribute("locked") != "0");
	}

	/// <summary>Writes a sheet of records, as the mode says.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="book">The workbook writer.</param>
	/// <param name="sheet">The sheet's name.</param>
	/// <param name="records">The records.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="options">The sheet's options.</param>
	/// <returns>A task that completes when the sheet is.</returns>
	private static async Task WriteSheet<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(ExcelWorkbookWriter book, string sheet, IEnumerable<T> records, bool async, ExcelSheetOptions options)
	{
		if (async)
		{
			await book.WriteSheetAsync(sheet, records, options);
		}
		else
		{
			book.WriteSheet(sheet, records, options);
		}
	}
}
