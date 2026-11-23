using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>An order whose columns have default styles: its type's format with Validate, a format of its own, or none.</summary>
[CsvRecord]
public partial class ShapeOrder
{
	/// <summary>A whole number: General, so no default style.</summary>
	public int Id { get; set; }

	/// <summary>A date and time: its type's format with Validate.</summary>
	public DateTime When { get; set; }

	/// <summary>A number in a format of its own.</summary>
	[ExcelColumn(Format = "0.00")]
	public decimal Total { get; set; }

	/// <summary>Text: <c>@</c> with Validate, which its column's default style has and its cells don't.</summary>
	public string? Note { get; set; }

	/// <summary>Text, the last column.</summary>
	public string? Code { get; set; }
}

/// <summary>
/// The shape of a written sheet's rows (Phase 9's step 5), synchronously and asynchronously: no row or cell has an
/// <c>r</c>; a gap before a later value in its record is a placeholder in its column's default style, unlocked under
/// <see cref="ExcelSheetOptions.Protect"/>, and the header's once the header has ended; a record without a value before a
/// later one is an empty row; and nothing else changes: a sheet without gaps or empty records is today's sheet without its
/// <c>r</c>s, its other parts and its styles byte for byte (pinned by their hashes, made at <c>8bbad63c</c>), and the
/// ranges outside the rows are the same with them. Each workbook passes the Open XML SDK's validator, and its values read
/// back through CsvHelper.Excel's reader in their cells.
/// </summary>
public class SheetShapeTests
{
	/// <summary>SpreadsheetML's namespace.</summary>
	private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

	/// <summary>The kinds' sheet's header.</summary>
	private static readonly string[] KindNames = ["Plain", "Spaced", "Code", "Flag", "Int", "Price", "Score", "When", "Stamped", "Took", "Sum", "Site", "Long"];

	/// <summary>Three orders without an empty field.</summary>
	private static readonly ShapeOrder[] Orders =
	[
		new() { Id = 1, When = new DateTime(2024, 1, 15, 10, 30, 0), Total = 12.5m, Note = "first", Code = "A-1" },
		new() { Id = 2, When = new DateTime(2024, 1, 16), Total = 3m, Note = "second", Code = "00123" },
		new() { Id = 3, When = new DateTime(2024, 1, 17, 8, 0, 0, 250), Total = 0.25m, Note = "third", Code = "C-3" },
	];

	/// <summary>
	/// Each workbook's parts as written at <c>8bbad63c</c>, by the first 12 hexadecimal digits of their SHA-256, in name
	/// order: each sheet without its rows' and cells' <c>r</c>s, and, in the workbooks with gaps and empty records (the
	/// ranges'), without its rows.
	/// </summary>
	private static readonly Dictionary<string, string> TodaysParts = new()
	{
		["kinds"] = "[Content_Types].xml=70b36d07058b _rels/.rels=2a8e2dd23d06 xl/_rels/workbook.xml.rels=c9b5d133e344 xl/styles.xml=482a2a9a2961 xl/workbook.xml=affdebdf5ea7 xl/worksheets/_rels/sheet1.xml.rels=cd7005e892a1 xl/worksheets/sheet1.xml=b0c3d5694bb5",
		["kinds-protect"] = "[Content_Types].xml=70b36d07058b _rels/.rels=2a8e2dd23d06 xl/_rels/workbook.xml.rels=c9b5d133e344 xl/styles.xml=08f92be7d669 xl/workbook.xml=affdebdf5ea7 xl/worksheets/_rels/sheet1.xml.rels=cd7005e892a1 xl/worksheets/sheet1.xml=aba92e5e12c2",
		["kinds-shared"] = "[Content_Types].xml=0e37127c7232 _rels/.rels=2a8e2dd23d06 xl/_rels/workbook.xml.rels=6258d8998995 xl/sharedStrings.xml=e6282eee4fbd xl/styles.xml=482a2a9a2961 xl/workbook.xml=affdebdf5ea7 xl/worksheets/_rels/sheet1.xml.rels=cd7005e892a1 xl/worksheets/sheet1.xml=b6dfc805ad3e",
		["orders"] = "[Content_Types].xml=70b36d07058b _rels/.rels=2a8e2dd23d06 xl/_rels/workbook.xml.rels=c9b5d133e344 xl/styles.xml=9ec328158c7f xl/workbook.xml=413d5454cc03 xl/worksheets/sheet1.xml=bda334ec1e61",
		["orders-newsheet"] = "[Content_Types].xml=9f0a3f8b3305 _rels/.rels=2a8e2dd23d06 xl/_rels/workbook.xml.rels=6faf2d3cf2cc xl/styles.xml=4b7eeb1a5eb3 xl/workbook.xml=240a4c4f688a xl/worksheets/sheet1.xml=64d3ea42866b xl/worksheets/sheet2.xml=5151a35774bf xl/worksheets/sheet3.xml=9c8eb79e65f2",
		["ranges-filter"] = "[Content_Types].xml=70b36d07058b _rels/.rels=2a8e2dd23d06 xl/_rels/workbook.xml.rels=c9b5d133e344 xl/styles.xml=6fe1806ae3f1 xl/workbook.xml=cc35ece0ae0a xl/worksheets/_rels/sheet1.xml.rels=dfa4781db85a xl/worksheets/sheet1.xml=bb8af0acca30",
		["ranges-table"] = "[Content_Types].xml=3b8ac2f693e1 _rels/.rels=2a8e2dd23d06 xl/_rels/workbook.xml.rels=c9b5d133e344 xl/styles.xml=0a25a1de7fb1 xl/tables/table1.xml=3f4d85dba368 xl/workbook.xml=a69f040a131a xl/worksheets/_rels/sheet1.xml.rels=0c236fc268a7 xl/worksheets/sheet1.xml=3b73462d1f0e",
		["template"] = "[Content_Types].xml=70b36d07058b _rels/.rels=2a8e2dd23d06 xl/_rels/workbook.xml.rels=c9b5d133e344 xl/styles.xml=64eac0c5fc5a xl/workbook.xml=d564de262680 xl/worksheets/sheet1.xml=7b02e0c52da3",
	};

	/// <summary>Sync and async.</summary>
	public static TheoryData<bool> Modes => new() { false, true };

	/// <summary>Sync and async, with inline and shared strings.</summary>
	public static TheoryData<bool, ExcelStrings> StringModes => new()
	{
		{ false, ExcelStrings.Inline },
		{ true, ExcelStrings.Inline },
		{ false, ExcelStrings.Shared },
		{ true, ExcelStrings.Shared },
	};

	/// <summary>Sync and async, without and with <see cref="ExcelSheetOptions.Protect"/>.</summary>
	public static TheoryData<bool, bool> ProtectModes => new()
	{
		{ false, false },
		{ true, false },
		{ false, true },
		{ true, true },
	};

	/// <summary>Each workbook of <see cref="TodaysParts"/>, sync and async.</summary>
	public static TheoryData<string, bool> Workbooks
	{
		get
		{
			var data = new TheoryData<string, bool>();
			foreach (var name in new[] { "kinds", "kinds-protect", "kinds-shared", "orders", "orders-newsheet", "ranges-filter", "ranges-table", "template" })
			{
				data.Add(name, false);
				data.Add(name, true);
			}

			return data;
		}
	}

	[Theory]
	[MemberData(nameof(Workbooks))]
	public async Task KeepsTodaysBytesButTheReferences(string name, bool async)
	{
		var workbook = await WorkbookAsync(name, async);

		// Only the rows' shape changed: the styles, the shared strings, the ranges, the tables and everything else are
		// today's, and so are the rows of the sheets without gaps or empty records, but for their r.
		Assert.Equal(TodaysParts[name], Parts(workbook, blankRows: name.StartsWith("ranges", StringComparison.Ordinal)));
		Assert.All(Worksheets(workbook), sheet => Assert.DoesNotContain(" r=\"", SheetData(sheet)));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesEveryKindOfCellWithoutReferences(bool async)
	{
		var workbook = await KindsAsync(async, new ExcelSheetOptions { Title = "Kinds" }, ExcelStrings.Inline);

		Assert.Equal(
			"<sheetData><row><c s=\"1\" t=\"inlineStr\"><is><t>Kinds</t></is></c></row><row><c s=\"2\" t=\"inlineStr\"><is><t>Plain</t></is></c><c s=\"2\" t=\"inlineStr\"><is><t>Spaced</t></is></c><c s=\"2\" t=\"inlineStr\"><is><t>Code</t></is></c><c s=\"2\" t=\"inlineStr\"><is><t>Flag</t></is></c><c s=\"2\" t=\"inlineStr\"><is><t>Int</t></is></c><c s=\"2\" t=\"inlineStr\"><is><t>Price</t></is></c><c s=\"2\" t=\"inlineStr\"><is><t>Score</t></is></c><c s=\"2\" t=\"inlineStr\"><is><t>When</t></is></c><c s=\"2\" t=\"inlineStr\"><is><t>Stamped</t></is></c><c s=\"2\" t=\"inlineStr\"><is><t>Took</t></is></c><c s=\"2\" t=\"inlineStr\"><is><t>Sum</t></is></c><c s=\"2\" t=\"inlineStr\"><is><t>Site</t></is></c><c s=\"2\" t=\"inlineStr\"><is><t>Long</t></is></c></row>" +
			"<row><c t=\"inlineStr\"><is><t>plain0</t></is></c><c t=\"inlineStr\"><is><t xml:space=\"preserve\"> spaced</t></is></c><c t=\"inlineStr\"><is><t>00123</t></is></c><c t=\"b\"><v>1</v></c><c><v>42</v></c><c><v>1.25</v></c><c><v>0.1</v></c><c s=\"3\"><v>45306.4375</v></c><c t=\"inlineStr\"><is><t>2024-01-15T10:30:00.0000000+02:00</t></is></c><c s=\"4\"><v>1.085462962962963</v></c><c><f>SUM(E3:E4)</f></c><c s=\"5\" t=\"inlineStr\"><is><t>https://example.com/0</t></is></c><c t=\"inlineStr\"><is><t>1234567890123456789</t></is></c></row>" +
			"<row><c t=\"inlineStr\"><is><t>plain1</t></is></c><c t=\"inlineStr\"><is><t>a &amp; &lt;b&gt;</t></is></c><c t=\"inlineStr\"><is><t>00123</t></is></c><c t=\"b\"><v>0</v></c><c><v>43</v></c><c><v>2.25</v></c><c><v>1.1</v></c><c s=\"6\"><v>45307</v></c><c t=\"inlineStr\"><is><t>2024-01-15T10:30:00.0000000+02:00</t></is></c><c s=\"7\"><v>1.08546875</v></c><c><f>SUM(E3:E4)</f></c><c s=\"5\" t=\"inlineStr\"><is><t>https://example.com/1</t></is></c><c t=\"inlineStr\"><is><t>1234567890123456790</t></is></c></row></sheetData>",
			SheetData(Written.Entry(workbook, "xl/worksheets/sheet1.xml")));
		Assert.Equal(
			[
				"Kinds",
				string.Join("|", KindNames),
				"plain0| spaced|00123|TRUE|42|1.25|0.1|2024-01-15T10:30:00|2024-01-15T10:30:00.0000000+02:00|26:03:04||https://example.com/0|1234567890123456789",
				"plain1|a & <b>|00123|FALSE|43|2.25|1.1|2024-01-16|2024-01-15T10:30:00.0000000+02:00|26:03:04.5||https://example.com/1|1234567890123456790",
			],
			Written.ReadOne(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

#if NET
	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesDatesAndTimesWithoutReferences(bool async)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Days", Written.Phase4Look);
			await WriteRows(writer, a, ["Day", "Gap", "Time"], [new DateOnly(2024, 1, 15), "", new TimeOnly(10, 30)]);
			await Written.DisposeAsync(writer, a);
		}, async);

		Assert.Matches("<row><c s=\"[0-9]+\"><v>45306</v></c><c/><c s=\"[0-9]+\"><v>0.4375</v></c></row></sheetData>$", SheetData(Written.Entry(workbook, "xl/worksheets/sheet1.xml")));
		Assert.Equal(["Day|Gap|Time", "2024-01-15||10:30:00"], Written.ReadOne(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}
#endif

	[Theory]
	[MemberData(nameof(ProtectModes))]
	public async Task WritesGapsAsPlaceholdersInTheirColumnsDefaultStyles(bool async, bool protect)
	{
		// Id has no default style, When its type's format (Validate), Total its own format, Note and Code text's (Validate).
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet<ShapeOrder>("Orders", new ExcelSheetOptions { Validate = true, Protect = protect, BoldHeader = false, FreezeHeader = false, AutoFit = 0 });
			await WriteRows(writer, a,
				["Id", "When", "Total", "Note", "Code"],
				["", "", "", "", "x"],
				[1, "", "", "", ""],
				["", new DateTime(2024, 1, 15), "", "n", ""],
				[2, new DateTime(2024, 1, 16, 12, 0, 0), 3.5m, "", "y", "", "past"]);
			await Written.DisposeAsync(writer, a);
		}, async, new ExcelWriterOptions { Csv = new CsvWriterOptions { ValidateFieldCount = false } });

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		var columns = ColumnStyles(sheet);
		var placeholders = Cells(sheet).Where(c => c.Element.EndsWith("/>", StringComparison.Ordinal)).ToList();

		// In the first column, in runs and before the last field, and none after it: each in its column's default style,
		// the col's, or <c/> without one, and past the header's columns.
		Assert.Equal(["A2", "B2", "C2", "D2", "A4", "C4", "D5", "F5"], placeholders.Select(c => c.Reference));
		Assert.All(placeholders, c => Assert.Equal(columns.TryGetValue(Column(c.Reference), out var style) ? $"<c s=\"{style}\"/>" : "<c/>", c.Element));
		Assert.Equal(["B", "C", "D"], new[] { "B2", "C2", "D2" }.Where(r => placeholders.Single(c => c.Reference == r).Element != "<c/>").Select(r => r.Substring(0, 1)));
		Assert.Equal("<c/>", placeholders.Single(c => c.Reference == "F5").Element);
		Assert.Equal(protect ? "<c s=\"" + columns[0] + "\"/>" : "<c/>", placeholders.Single(c => c.Reference == "A2").Element);
		string[] unlocked = protect ? ["A2", "B2", "C2", "D2", "A4", "C4", "D5"] : [];
		Assert.Equal(unlocked, placeholders.Where(c => !Locked(workbook, c.Element)).Select(c => c.Reference));

		Assert.Equal(
			[
				"Id|When|Total|Note|Code",
				"||||x",
				"1||||",
				"|2024-01-15||n|",
				"2|2024-01-16T12:00:00|3.5||y||past",
			],
			Written.Read(workbook)[0].Rows);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task StylesTheHeadersPlaceholdersOnceTheHeaderEnds(bool async)
	{
		// The header's second field is empty and its fourth fails: their columns' formats are known when the header ends,
		// by their names (the failed field's is its text), and each new sheet of NewSheet repeats the header so.
		var options = new ExcelSheetOptions
		{
			Columns = new Dictionary<string, ExcelColumnOptions> { [""] = new() { Format = "0.00" }, ["1234567890123456789"] = new() { Format = "0" } },
		};
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 2;
			var writer = book.AddSheet("Data", options);
			writer.WriteField("Name");
			writer.WriteField("");
			writer.WriteField("Total");
			Assert.Throws<InvalidOperationException>(() => writer.WriteField(1234567890123456789L));
			writer.WriteField("Code");
			await Written.EndRecordAsync(writer, a);
			await WriteRows(writer, a, ["a", "", "", "", "x"], ["b", 1.5, "", 7, "y"]);
			await Written.DisposeAsync(writer, a);
		}, async, new ExcelWriterOptions { LongNumbers = ExcelLongNumbers.Throw, RowLimit = ExcelRowLimit.NewSheet });

		var first = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		var second = Written.Entry(workbook, "xl/worksheets/sheet2.xml");
		var columns = ColumnStyles(first);
		var gap = $"<c s=\"{columns[1]}\"/>";
		var failed = $"<c s=\"{columns[3]}\"/>";
		Assert.Equal([1, 3], columns.Keys.OrderBy(k => k));
		Assert.Equal(
			$"<row><c s=\"1\" t=\"inlineStr\"><is><t>Name</t></is></c>{gap}<c s=\"1\" t=\"inlineStr\"><is><t>Total</t></is></c>{failed}<c s=\"1\" t=\"inlineStr\"><is><t>Code</t></is></c></row>",
			Regex.Match(SheetData(first), "<row>.*?</row>").Value);
		Assert.Equal(
			$"<row><c t=\"inlineStr\"><is><t>a</t></is></c>{gap}<c/>{failed}<c t=\"inlineStr\"><is><t>x</t></is></c></row>",
			Regex.Matches(SheetData(first), "<row>.*?</row>")[1].Value);
		Assert.Equal(Regex.Match(SheetData(first), "<row>.*?</row>").Value, Regex.Match(SheetData(second), "<row>.*?</row>").Value);
		Assert.Equal(columns, ColumnStyles(second));
		Assert.Equal(["Data: Name||Total||Code / a||||x", "Data (2): Name||Total||Code / b|1.5||7|y"], Written.Sheets(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task LocksTheProtectedHeadersEmptyFieldsAndTheColumnsItsFailedFieldsSkip(bool async)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Data", new ExcelSheetOptions { Protect = true });
			writer.WriteField("");
			writer.WriteField("Name");
			Assert.Throws<InvalidOperationException>(() => writer.WriteField(1234567890123456789L));
			writer.WriteField("Code");
			writer.WriteField("");
			writer.WriteField("");
			await Written.EndRecordAsync(writer, a);
			await WriteRows(writer, a, ["", "a", "", "", "", "", "", "past"]);
			await Written.DisposeAsync(writer, a);
		}, async, new ExcelWriterOptions { LongNumbers = ExcelLongNumbers.Throw, Csv = new CsvWriterOptions { ValidateFieldCount = false } });

		var sheet = Written.Entry(workbook, "xl/worksheets/sheet1.xml");
		var cells = Cells(sheet);
		var unlocked = ColumnStyles(sheet)[0];

		// The header's empty fields, trailing ones included, and the failed one's column: cells in the bold header's style,
		// locked. Below, the gaps in the header's columns are unlocked, as their columns' default style; past them, <c/>.
		Assert.Equal(
			"<row><c s=\"1\"/><c s=\"1\" t=\"inlineStr\"><is><t>Name</t></is></c><c s=\"1\"/><c s=\"1\" t=\"inlineStr\"><is><t>Code</t></is></c><c s=\"1\"/><c s=\"1\"/></row>",
			Regex.Match(SheetData(sheet), "<row>.*?</row>").Value);
		Assert.Equal(["A1", "B1", "C1", "D1", "E1", "F1"], cells.Where(c => c.Reference.EndsWith("1", StringComparison.Ordinal) && Locked(workbook, c.Element)).Select(c => c.Reference));
		Assert.Equal(
			$"<row><c s=\"{unlocked}\"/><c s=\"{unlocked}\" t=\"inlineStr\"><is><t>a</t></is></c><c s=\"{unlocked}\"/><c s=\"{unlocked}\"/><c s=\"{unlocked}\"/><c s=\"{unlocked}\"/><c/><c t=\"inlineStr\"><is><t>past</t></is></c></row>",
			Regex.Matches(SheetData(sheet), "<row>.*?</row>")[1].Value);
		Assert.Equal(["G2", "H2"], cells.Where(c => c.Reference.EndsWith("2", StringComparison.Ordinal) && Locked(workbook, c.Element)).Select(c => c.Reference));
		Assert.Equal(["|Name||Code", "|a||||||past"], Written.ReadOne(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(StringModes))]
	public async Task WritesEmptyRowsBeforeALaterRowOnly(bool async, ExcelStrings strings)
	{
		var workbook = await Written.RowsAsync([["a"], [""], [null, null], ["", "b"], [""], [null, ""]], async, Written.Ragged(strings));

		var b = strings == ExcelStrings.Inline ? "<c t=\"inlineStr\"><is><t>b</t></is></c>" : "<c t=\"s\"><v>1</v></c>";
		Assert.EndsWith($"</row><row/><row/><row><c/>{b}</row></sheetData>", SheetData(Written.Entry(workbook, "xl/worksheets/sheet1.xml")));
		Assert.Equal(["A1", "A4", "B4"], Cells(Written.Entry(workbook, "xl/worksheets/sheet1.xml")).Select(c => c.Reference));
		Assert.Equal(["a", "", "", "|b"], Written.ReadOne(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task DropsTheEmptyRowsOfADiscardedRecord(bool async)
	{
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Data", Written.Phase4Look);
			await WriteRows(writer, a, ["Name", "Code", "Note"], ["a", "1", "n"], ["", "", ""], ["", "", ""]);

			// Discarded at its end, for its field count, after its row's start and the two empty rows before it.
			await Assert.ThrowsAsync<InvalidOperationException>(() => WriteRows(writer, a, ["x", "9", "n", "extra"]));

			// A field that fails leaves a gap in its record.
			writer.WriteField("");
			Assert.Throws<InvalidOperationException>(() => writer.WriteField(1234567890123456789L));
			writer.WriteField("z");
			await Written.EndRecordAsync(writer, a);
			await WriteRows(writer, a, ["b", "2", ""]);
			await Written.DisposeAsync(writer, a);
		}, async, new ExcelWriterOptions { LongNumbers = ExcelLongNumbers.Throw });

		Assert.Equal(
			"<sheetData><row><c t=\"inlineStr\"><is><t>Name</t></is></c><c t=\"inlineStr\"><is><t>Code</t></is></c><c t=\"inlineStr\"><is><t>Note</t></is></c></row>" +
			"<row><c t=\"inlineStr\"><is><t>a</t></is></c><c t=\"inlineStr\"><is><t>1</t></is></c><c t=\"inlineStr\"><is><t>n</t></is></c></row>" +
			"<row/><row/><row><c/><c/><c t=\"inlineStr\"><is><t>z</t></is></c></row>" +
			"<row><c t=\"inlineStr\"><is><t>b</t></is></c><c t=\"inlineStr\"><is><t>2</t></is></c></row></sheetData>",
			SheetData(Written.Entry(workbook, "xl/worksheets/sheet1.xml")));
		Assert.Equal(["Name|Code|Note", "a|1|n", "", "", "||z", "b|2|"], Written.ReadOne(workbook));
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesEmptyRowsInTheSheetsOfNewSheet(bool async)
	{
		// The full sheets' empty records at their ends write nothing; a new sheet's count from its header.
		var withHeader = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 3;
			var writer = book.AddSheet("Data", Written.Phase4Look);
			await WriteRows(writer, a, ["Name", "Code"], ["a", "1"], ["", ""], ["", ""], ["b", "2"], ["", ""], ["", ""]);
			await Written.DisposeAsync(writer, a);
		}, async, new ExcelWriterOptions { RowLimit = ExcelRowLimit.NewSheet });

		// A header without a cell is an empty row in each new sheet too.
		var emptyHeader = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 2;
			var writer = book.AddSheet("Data", Written.Phase4Look);
			await WriteRows(writer, a, ["", ""], ["a", "1"], ["b", "2"]);
			await Written.DisposeAsync(writer, a);
		}, async, new ExcelWriterOptions { RowLimit = ExcelRowLimit.NewSheet });

		var header = "<row><c t=\"inlineStr\"><is><t>Name</t></is></c><c t=\"inlineStr\"><is><t>Code</t></is></c></row>";
		Assert.Equal(
			[
				$"<sheetData>{header}<row><c t=\"inlineStr\"><is><t>a</t></is></c><c t=\"inlineStr\"><is><t>1</t></is></c></row></sheetData>",
				$"<sheetData>{header}<row/><row><c t=\"inlineStr\"><is><t>b</t></is></c><c t=\"inlineStr\"><is><t>2</t></is></c></row></sheetData>",
				$"<sheetData>{header}</sheetData>",
			],
			Worksheets(withHeader).Select(SheetData));
		Assert.Equal(["Data: Name|Code / a|1", "Data (2): Name|Code /  / b|2", "Data (3): Name|Code"], Written.Sheets(withHeader));
		Assert.Equal(
			[
				"<sheetData><row/><row><c t=\"inlineStr\"><is><t>a</t></is></c><c t=\"inlineStr\"><is><t>1</t></is></c></row></sheetData>",
				"<sheetData><row/><row><c t=\"inlineStr\"><is><t>b</t></is></c><c t=\"inlineStr\"><is><t>2</t></is></c></row></sheetData>",
			],
			Worksheets(emptyHeader).Select(SheetData));
		Assert.Equal(["Data:  / a|1", "Data (2):  / b|2"], Written.Sheets(emptyHeader));
		Assert.Empty(Written.ValidationErrors(withHeader));
		Assert.Empty(Written.ValidationErrors(emptyHeader));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesAGapInColumnABelowATitle(bool async)
	{
		// The title's cell is in column A, and the record after it, the header here and the first record of each new sheet,
		// starts its gaps from column A again.
		var workbook = await Written.WorkbookAsync(async (book, a) =>
		{
			book.MaxRows = 3;
			var writer = book.AddSheet("Data", new ExcelSheetOptions { BoldHeader = false, FreezeHeader = false, AutoFit = 0, Title = "Codes" });
			await WriteRows(writer, a, ["", "Code"], ["", "1"], ["", "2"]);
			await Written.DisposeAsync(writer, a);
		}, async, new ExcelWriterOptions { RowLimit = ExcelRowLimit.NewSheet });

		var sheets = Worksheets(workbook);
		Assert.Equal(2, sheets.Count);
		for (var i = 0; i < sheets.Count; i++)
		{
			var cells = Cells(sheets[i]);
			Assert.Equal(["A1", "A2", "B2", "A3", "B3"], cells.Select(c => c.Reference));
			Assert.Equal("<c/>", cells[1].Element);
			Assert.Contains("<t>Code</t>", cells[2].Element, StringComparison.Ordinal);
			Assert.Equal("<c/>", cells[3].Element);
			Assert.Contains($"<t>{i + 1}</t>", cells[4].Element, StringComparison.Ordinal);
		}

		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task WritesEverySheetWithoutReferencesButTheListsSheet(bool async)
	{
		// NewSheet's sheets each repeat the protected title's row and the header, without r.
		var orders = await WorkbookAsync("orders-newsheet", async);
		Assert.Equal(3, Worksheets(orders).Count);
		Assert.All(Worksheets(orders), sheet =>
		{
			Assert.StartsWith("<sheetData><row s=\"1\" customFormat=\"1\"><c s=\"1\" t=\"inlineStr\"><is><t>Orders</t></is></c></row><row><c s=\"2\" t=\"inlineStr\"><is><t>Id</t></is></c>", SheetData(sheet));
			Assert.Equal(["A1", "A2", "B2", "C2", "D2", "E2", "A3", "B3", "C3", "D3", "E3"], Cells(sheet).Select(c => c.Reference));
		});
		Assert.Equal(["Orders", "Orders (2)", "Orders (3)"], Written.Read(orders).Select(s => s.Sheet));

		// The lists sheet, written apart, keeps its r.
		var listed = await Written.WorkbookAsync(async (book, a) =>
		{
			ValidatedOrder[] records = [new() { Id = 1, Status = TicketStatus.Open, When = new DateTime(2024, 1, 15), Region = "North, East", Note = "x" }];
			if (a)
			{
				await book.WriteSheetAsync("Orders", records, new ExcelSheetOptions { Validate = true });
			}
			else
			{
				book.WriteSheet("Orders", records, new ExcelSheetOptions { Validate = true });
			}
		}, async);
		Assert.DoesNotContain(" r=\"", SheetData(Written.Entry(listed, "xl/worksheets/sheet1.xml")));
		Assert.Contains("<sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>North, East</t></is></c></row><row r=\"2\"><c r=\"A2\" ", Written.Entry(listed, "xl/worksheets/sheet2.xml"));
		Assert.Empty(Written.ValidationErrors(orders));
		Assert.Empty(Written.ValidationErrors(listed));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task KeepsTheRangesOfASheetWithGapsAndEmptyRecords(bool async)
	{
		var filtered = await WorkbookAsync("ranges-filter", async);
		var tabled = await WorkbookAsync("ranges-table", async);

		var sheet = Written.Entry(filtered, "xl/worksheets/sheet1.xml");
		Assert.Equal(
			"<sheetData><row><c s=\"1\" t=\"inlineStr\"><is><t>Ranges</t></is></c></row><row><c s=\"2\" t=\"inlineStr\"><is><t>Name</t></is></c><c s=\"2\" t=\"inlineStr\"><is><t>Site</t></is></c><c s=\"2\" t=\"inlineStr\"><is><t>Amount</t></is></c><c s=\"2\" t=\"inlineStr\"><is><t>Note</t></is></c></row>" +
			"<row><c t=\"inlineStr\"><is><t>a</t></is></c><c s=\"3\" t=\"inlineStr\"><is><t>https://example.com/a</t></is></c><c><v>1</v></c></row><row/>" +
			"<row><c/><c/><c><v>2</v></c><c t=\"inlineStr\"><is><t>x</t></is></c></row><row><c t=\"inlineStr\"><is><t>b</t></is></c></row>" +
			"<row><c/><c s=\"3\" t=\"inlineStr\"><is><t>https://example.com/b</t></is></c></row></sheetData>",
			SheetData(sheet));

		// The filter and the table to the last row with a cell, the merge, and the links on the cells that hold them.
		var cells = Cells(sheet).ToDictionary(c => c.Reference, c => c.Element);
		Assert.Contains("<autoFilter ref=\"A2:D7\"/><mergeCells count=\"2\"><mergeCell ref=\"A1:D1\"/><mergeCell ref=\"C6:D7\"/></mergeCells><hyperlinks><hyperlink ref=\"B3\" r:id=\"rId1\"/><hyperlink ref=\"B7\" r:id=\"rId2\"/></hyperlinks>", sheet);
		Assert.Contains("https://example.com/a", cells["B3"]);
		Assert.Contains("https://example.com/b", cells["B7"]);
		Assert.Contains("ref=\"A2:D7\"", Written.Entry(tabled, "xl/tables/table1.xml"));
		Assert.Equal(SheetData(sheet).Replace(" s=\"2\"", "").Replace(" s=\"3\"", " s=\"2\""), SheetData(Written.Entry(tabled, "xl/worksheets/sheet1.xml")));
		var rows = new[] { "Ranges", "Name|Site|Amount|Note", "a|https://example.com/a|1", "", "||2|x", "b", "|https://example.com/b" };
		Assert.Equal(rows, Written.ReadOne(filtered));
		Assert.Equal(rows, Written.ReadOne(tabled));
	}

	/// <summary>The cells of every row of a sheet, placed as readers place cells without <c>r</c>.</summary>
	/// <param name="sheet">The sheet's XML.</param>
	/// <returns>Each cell's reference and element.</returns>
	private static List<(string Reference, string Element)> Cells(string sheet)
	{
		var cells = new List<(string, string)>();
		var row = 0;
		var column = 0;
		foreach (Match element in Regex.Matches(SheetData(sheet), "<row/>|<row(?: [^>]*)?>|<c(?: [^>]*?)?/>|<c(?: [^>]*)?>.*?</c>"))
		{
			if (element.Value.StartsWith("<row", StringComparison.Ordinal))
			{
				row++;
				column = 0;

				continue;
			}

			cells.Add((CellReference.ColumnName(column) + row.ToString(System.Globalization.CultureInfo.InvariantCulture), element.Value));
			column++;
		}

		return cells;
	}

	/// <summary>A column's index from a cell's reference.</summary>
	/// <param name="reference">The reference, such as <c>C14</c>.</param>
	/// <returns>The 0-based column.</returns>
	private static int Column(string reference) => reference.TakeWhile(char.IsLetter).Aggregate(0, (n, c) => (n * 26) + c - 'A' + 1) - 1;

	/// <summary>The default style of each column that has one: its <c>col</c>'s <c>style</c>.</summary>
	/// <param name="sheet">The sheet's XML.</param>
	/// <returns>The styles, by 0-based column.</returns>
	private static Dictionary<int, int> ColumnStyles(string sheet) =>
		Regex.Matches(sheet, "<col min=\"([0-9]+)\" max=\"[0-9]+\"[^>]* style=\"([0-9]+)\"").Cast<Match>()
			.ToDictionary(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) - 1, m => int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));

	/// <summary>Whether a written cell is locked: its style's protection.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="element">The cell's element.</param>
	/// <returns>Whether it is locked, as every cell is unless its style unlocks it.</returns>
	private static bool Locked(byte[] workbook, string element)
	{
		var style = Regex.Match(element, "^<c s=\"([0-9]+)\"") is { Success: true } s ? int.Parse(s.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
		var xf = XDocument.Parse(Written.Entry(workbook, "xl/styles.xml")).Root!.Element(Main + "cellXfs")!.Elements(Main + "xf").ElementAt(style);

		return (string?)xf.Element(Main + "protection")?.Attribute("locked") != "0";
	}

	/// <summary>Writes records by hand, a field per <see cref="CsvWriter.WriteField(object)"/>, ending each as the mode says.</summary>
	/// <param name="writer">The writer.</param>
	/// <param name="async">Whether to end them asynchronously.</param>
	/// <param name="rows">The records' fields.</param>
	/// <returns>A task that completes when the records end.</returns>
	private static async Task WriteRows(CsvWriter writer, bool async, params object?[][] rows)
	{
		foreach (var row in rows)
		{
			foreach (var field in row)
			{
				writer.WriteField(field);
			}

			await Written.EndRecordAsync(writer, async);
		}
	}

	/// <summary>A workbook of <see cref="TodaysParts"/>.</summary>
	/// <param name="name">Its name.</param>
	/// <param name="async">Whether to write it asynchronously.</param>
	/// <returns>The workbook.</returns>
	private static async Task<byte[]> WorkbookAsync(string name, bool async) => name switch
	{
		"kinds" => await KindsAsync(async, new ExcelSheetOptions { Title = "Kinds" }, ExcelStrings.Inline),
		"kinds-shared" => await KindsAsync(async, new ExcelSheetOptions { Title = "Kinds" }, ExcelStrings.Shared),
		"kinds-protect" => await KindsAsync(async, new ExcelSheetOptions
		{
			Title = "Kinds",
			Protect = true,
			Columns = new Dictionary<string, ExcelColumnOptions> { ["Int"] = new() { Format = "0.00" }, ["Plain"] = new() { Wrap = true } },
		}, ExcelStrings.Inline),
		"orders" => await OrdersAsync(async, OrdersOptions, 0),
		"orders-newsheet" => await OrdersAsync(async, OrdersOptions with { Title = "Orders" }, 3),
		"ranges-filter" => await RangesAsync(async, new ExcelSheetOptions { Title = "Ranges", AutoFilter = true }),
		"ranges-table" => await RangesAsync(async, new ExcelSheetOptions { Title = "Ranges", Table = new ExcelTableOptions() }),
		_ => await TemplateAsync(async),
	};

	/// <summary>The orders' sheet's options: protected and validated, with a wrapping column.</summary>
	private static ExcelSheetOptions OrdersOptions => new()
	{
		Protect = true,
		Validate = true,
		Columns = new Dictionary<string, ExcelColumnOptions> { ["Note"] = new() { Wrap = true } },
	};

	/// <summary>Writes the kinds' sheet: a header and two records of every kind of cell.</summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="options">The sheet's options.</param>
	/// <param name="strings">Where text cells keep their strings.</param>
	/// <returns>The workbook.</returns>
	private static Task<byte[]> KindsAsync(bool async, ExcelSheetOptions options, ExcelStrings strings) =>
		Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Kinds", options);
			foreach (var name in KindNames)
			{
				writer.WriteField(name);
			}

			await Written.EndRecordAsync(writer, a);
			for (var i = 0; i < 2; i++)
			{
				writer.WriteField("plain" + i);
				writer.WriteField(i == 0 ? " spaced" : "a & <b>");
				writer.WriteField("00123");
				writer.WriteField(i == 0);
				writer.WriteField(42 + i);
				writer.WriteField(1.25m + i);
				writer.WriteField(0.1 + i);
				writer.WriteField(i == 0 ? new DateTime(2024, 1, 15, 10, 30, 0) : new DateTime(2024, 1, 16));
				writer.WriteField(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.FromHours(2)));
				writer.WriteField(new TimeSpan(1, 2, 3, 4, i * 500));
				writer.WriteFormula("SUM(E3:E4)");
				writer.WriteField(new Uri("https://example.com/" + i));
				writer.WriteField(1234567890123456789L + i);
				await Written.EndRecordAsync(writer, a);
			}

			await Written.DisposeAsync(writer, a);
		}, async, new ExcelWriterOptions { Strings = strings });

	/// <summary>Writes the orders' sheet, its records written by the record writer.</summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="options">The sheet's options.</param>
	/// <param name="maxRows">The rows a sheet holds, with <see cref="ExcelRowLimit.NewSheet"/>; or 0 for Excel's.</param>
	/// <returns>The workbook.</returns>
	private static Task<byte[]> OrdersAsync(bool async, ExcelSheetOptions options, int maxRows) =>
		Written.WorkbookAsync(async (book, a) =>
		{
			if (maxRows > 0)
			{
				book.MaxRows = maxRows;
			}

			if (a)
			{
				await book.WriteSheetAsync("Orders", Orders, options);
			}
			else
			{
				book.WriteSheet("Orders", Orders, options);
			}
		}, async, new ExcelWriterOptions { RowLimit = ExcelRowLimit.NewSheet });

	/// <summary>
	/// Writes a sheet of gaps and empty records: links, a range merged below the header (but in a table), and empty
	/// records in the middle and at the end.
	/// </summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="options">The sheet's options.</param>
	/// <returns>The workbook.</returns>
	private static Task<byte[]> RangesAsync(bool async, ExcelSheetOptions options) =>
		Written.WorkbookAsync(async (book, a) =>
		{
			var writer = book.AddSheet("Ranges", options);
			await WriteRows(writer, a,
				["Name", "Site", "Amount", "Note"],
				["a", new Uri("https://example.com/a"), 1, ""],
				["", "", "", ""],
				["", "", 2, "x"],
				["b", "", "", ""],
				["", new Uri("https://example.com/b"), "", ""],
				["", "", "", ""],
				["", "", "", ""]);

			if (options.Table is null)
			{
				writer.MergeCells("C6:D7");
			}

			await Written.DisposeAsync(writer, a);
		}, async);

	/// <summary>Writes the orders' template.</summary>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <returns>The workbook.</returns>
	private static async Task<byte[]> TemplateAsync(bool async)
	{
		using var stream = new MemoryStream();
		if (async)
		{
			await ExcelTemplate.CreateAsync<ShapeOrder>(stream);
		}
		else
		{
			ExcelTemplate.Create<ShapeOrder>(stream);
		}

		return stream.ToArray();
	}

	/// <summary>A workbook's parts by the first 12 hexadecimal digits of their SHA-256, in name order.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="blankRows">Whether each sheet's rows are left out, as <c>&lt;sheetData/&gt;</c>.</param>
	/// <returns>The parts, as <c>name=hash</c>, separated by spaces.</returns>
	private static string Parts(byte[] workbook, bool blankRows)
	{
		using var sha = SHA256.Create();

		return string.Join(" ", Written.Entries(workbook).OrderBy(e => e.Name, StringComparer.Ordinal).Select(e =>
		{
			var text = blankRows && e.Name.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal)
				? Regex.Replace(e.Text, "<sheetData>.*</sheetData>", "<sheetData/>", RegexOptions.Singleline)
				: e.Text;
			var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));

			return e.Name + "=" + string.Concat(hash.Take(6).Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));
		}));
	}

	/// <summary>A workbook's worksheets' XML.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <returns>Each worksheet's XML, by part name.</returns>
	private static List<string> Worksheets(byte[] workbook) =>
		[.. Written.Entries(workbook).Where(e => Regex.IsMatch(e.Name, "^xl/worksheets/sheet[0-9]+\\.xml$")).Select(e => e.Text)];

	/// <summary>A sheet's rows: its <c>sheetData</c> element.</summary>
	/// <param name="sheet">The sheet's XML.</param>
	/// <returns>The element's XML.</returns>
	private static string SheetData(string sheet)
	{
		var start = sheet.IndexOf("<sheetData>", StringComparison.Ordinal);
		var end = sheet.IndexOf("</sheetData>", StringComparison.Ordinal) + "</sheetData>".Length;

		return sheet.Substring(start, end - start);
	}
}
