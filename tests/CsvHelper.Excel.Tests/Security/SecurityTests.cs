using System.Text;
using CsvHelper.Excel.Tests.Api;
using CsvHelper.Excel.Tests.Sheet;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// The plan's security cases end to end, through the public API on real zips and deflate streams: inflate bombs, sizes
/// the zip understates, entries that share one entry's data, gap amplification, huge tokens, deep nesting, external
/// entities, and references that aren't characters; and Phase 2's: a million shared formula groups, a long formula, a
/// shared formula that grows past the part limit, a shared string and a shared formula repeated a million times, formulas
/// nested a million deep, and absurd <c>t="d"</c> texts. Each parser's own tests check the same rules part by part.
/// </summary>
public class SecurityTests
{
	/// <summary>A mebibyte.</summary>
	private const int MiB = 1 << 20;

	/// <summary>The shared strings part.</summary>
	private const string StringsPart = "xl/sharedStrings.xml";

	/// <summary>A row with one number, for the start of a sheet.</summary>
	private const string FirstRow = "<row r=\"1\"><c r=\"A1\"><v>1</v></c></row>";

	/// <summary>A chunk of spaces, for white space that compresses to almost nothing.</summary>
	private static readonly byte[] Spaces = Encoding.ASCII.GetBytes(new string(' ', 64 * 1024));

	/// <summary>A worksheet part as <see cref="Sheets.Worksheet"/> writes it, up to its rows.</summary>
	private static readonly string SheetStart = Sheets.Worksheet("").Substring(0, Sheets.Worksheet("").IndexOf("</sheetData>", StringComparison.Ordinal));

	/// <summary>A worksheet part as <see cref="Sheets.Worksheet"/> writes it, from after its rows.</summary>
	private static readonly string SheetEnd = Sheets.Worksheet("").Substring(SheetStart.Length);

	/// <summary>A sheet of a million rows, each of a cell that defines a shared formula group of its own, zipped once.</summary>
	private static readonly Lazy<byte[]> MillionGroups = new(() => Workbooks.Data("").SetStreamed(Sheets.PartName, stream =>
		WriteRows(stream, 1_000_000, n => $"<row r=\"{n}\"><c r=\"A{n}\"><f t=\"shared\" ref=\"A{n}:B{n}\" si=\"{n - 1}\">B1</f><v>1</v></c></row>")).ToArray());

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StopsASheetThatInflatesPastTheLimit(bool async)
	{
		// 64 MB of white space between two rows, which deflate makes about 64 KB, read under a 16 MB limit. The part limit
		// is 64 KB, which the white space would pass first if the window held it rather than skipping it.
		var package = Workbooks.Data("").SetStreamed(Sheets.PartName, stream => WriteSheet(stream, FirstRow, 64L * MiB, "<row r=\"2\"><c r=\"A2\"><v>2</v></c></row>"));
		var zip = package.ToArray();
		var input = new ReadTrackingStream(zip);
		var options = NoHeader(maxPartSize: 64 * 1024, maxUncompressedSize: 16L * MiB);

		using var workbook = async ? await ExcelWorkbook.OpenAsync(input, options) : ExcelWorkbook.Open(input, options);
		input.Forget();
		using var reader = await workbook.OpenReaderAsync(async);
		var e = await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAllAsync(async));
		Assert.Equal($"Reading {Sheets.PartName} takes the workbook past ExcelReaderOptions.MaxUncompressedSize ({16L * MiB} bytes uncompressed).", e.Message);

		// The limit is a quarter of the sheet, so the inflater stopped about a quarter of the way into its compressed data.
		AssertStoppedEarly(zip, input, Sheets.PartName, 16L * MiB);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RejectsAHeldPartTooLargeBeforeInflatingIt(bool async)
	{
		var package = Workbooks.Data("<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c></row>").SharedStringsPart("")
			.SetStreamed(StringsPart, stream => WriteStrings(stream, 64L * MiB));
		var zip = package.ToArray();
		var input = new ReadTrackingStream(zip);
		var options = NoHeader(maxPartSize: MiB);

		using var workbook = async ? await ExcelWorkbook.OpenAsync(input, options) : ExcelWorkbook.Open(input, options);
		input.Forget();
		var e = await Assert.ThrowsAsync<InvalidDataException>(() => workbook.OpenReaderAsync(async));
		Assert.StartsWith($"{StringsPart} is larger than ExcelReaderOptions.MaxPartSize ({MiB} bytes uncompressed): its size in the zip is", e.Message);

		var (start, length) = ZipBytes.DataRange(zip, StringsPart);
		Assert.Equal(0, input.ReadInto(start, length));
	}

	[Theory]
	[InlineData(Sheets.PartName, "uncompressed", false)]
	[InlineData(Sheets.PartName, "uncompressed", true)]
	[InlineData(Sheets.PartName, "compressed", false)]
	[InlineData(Sheets.PartName, "compressed", true)]
	[InlineData(StringsPart, "uncompressed", false)]
	[InlineData(StringsPart, "uncompressed", true)]
	[InlineData(StringsPart, "compressed", false)]
	[InlineData(StringsPart, "compressed", true)]
	public async Task EndsInAnErrorWhenTheZipUnderstatesASize(string part, string size, bool async)
	{
		// A sheet of 20 MB under a 16 MB limit, or shared strings of 2 MB under a 1 MB part limit, whose headers (the
		// central directory and the local header) declare far less. Whether the zip reader stops at the declared size or
		// not, the read ends in an error: a limit counted on what really inflates, or the XML of a truncated part.
		var package = Workbooks.Data("<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c></row>").SharedStrings("<t>a</t>");
		package = part == Sheets.PartName
			? package.SetStreamed(Sheets.PartName, stream => WriteSheet(stream, FirstRow, 20L * MiB, ""))
			: package.SetStreamed(StringsPart, stream => WriteStrings(stream, 2L * MiB));
		var zip = package.ToArray();
		if (size == "uncompressed")
		{
			ZipBytes.SetUncompressedSize(zip, part, 100);
		}
		else
		{
			ZipBytes.SetCompressedSize(zip, part, ZipBytes.CompressedSize(zip, part) / 2);
		}

		var options = NoHeader(maxPartSize: MiB, maxUncompressedSize: 16L * MiB);
		var e = await Record.ExceptionAsync(async () =>
		{
			using var workbook = async ? await ExcelWorkbook.OpenAsync(new MemoryStream(zip), options) : ExcelWorkbook.Open(new MemoryStream(zip), options);
			using var reader = await workbook.OpenReaderAsync(async);
			await reader.ReadAllAsync(async);
		});

		Assert.True(e is InvalidDataException or CsvFormatException, $"{part} with an understated {size} size: {e?.GetType().FullName ?? "no exception"}: {e?.Message}");
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReportsAZipThatRunsPastItsEndAsInvalidData(bool async)
	{
		// The last central directory record declares 30 bytes of extra field, which run through the 22-byte end record and
		// 8 bytes past the end of the archive. .NET 10's zip reader throws InvalidDataException itself; older ones throw an
		// IOException ("Zip file corrupt: unexpected end of stream reached"), which CsvHelper.Excel reports as
		// InvalidDataException with it inside. (A longer field hits their "Central Directory is invalid" check instead.)
		var package = Workbooks.Data(FirstRow);
		var zip = package.ToArray();
		ZipBytes.SetCentralExtraLength(zip, package.Names.Last(), 30);

		var e = await Assert.ThrowsAsync<InvalidDataException>(async () =>
		{
			using var workbook = async ? await ExcelWorkbook.OpenAsync(new MemoryStream(zip)) : ExcelWorkbook.Open(new MemoryStream(zip));
		});
#if NET10_0_OR_GREATER
		Assert.Null(e.InnerException);
#else
		Assert.IsType<IOException>(e.InnerException);
		Assert.StartsWith("The workbook's zip archive is corrupt: ", e.Message);
#endif
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReportsAnEntryNameNetFrameworkRejectsAsInvalidData(bool async)
	{
		// The styles part's name, in the central directory, gets a '<'. .NET Framework's zip reader rejects the name with
		// ArgumentException ("Illegal characters in path"), which CsvHelper.Excel reports as InvalidDataException with it
		// inside. .NET reads the name, so the workbook has no styles part, and reads as one without.
		var zip = Workbooks.Data(FirstRow).ToArray();
		ZipBytes.SetCentralName(zip, "xl/styles.xml", "xl/style<.xml");

#if NETFRAMEWORK
		var e = await Assert.ThrowsAsync<InvalidDataException>(async () =>
		{
			using var workbook = async ? await ExcelWorkbook.OpenAsync(new MemoryStream(zip)) : ExcelWorkbook.Open(new MemoryStream(zip));
		});
		Assert.IsType<ArgumentException>(e.InnerException);
		Assert.StartsWith("The workbook's zip archive is corrupt: ", e.Message);
#else
		using var workbook = async ? await ExcelWorkbook.OpenAsync(new MemoryStream(zip), NoHeader()) : ExcelWorkbook.Open(new MemoryStream(zip), NoHeader());
		using var reader = await workbook.OpenReaderAsync(async);
		Assert.Equal(["1"], await reader.ReadAllAsync(async));
#endif
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CountsEntriesThatShareOneEntrysDataEachTime(bool async)
	{
		// Twelve sheets whose central directory records all point at the first sheet's data, a sheet of a megabyte: each
		// reads as the first, and each read counts toward the workbook's total, which stops the seventh.
		var names = Enumerable.Range(1, 12).Select(n => $"S{n}").ToArray();
		var package = TestPackage.Workbook(names).SetStreamed(Sheets.PartName, stream => WriteSheet(stream, FirstRow, MiB, ""));
		var zip = package.ToArray();
		for (var n = 2; n <= names.Length; n++)
		{
			ZipBytes.PointAt(zip, $"xl/worksheets/sheet{n}.xml", Sheets.PartName);
		}

		var sheetSize = Encoding.UTF8.GetByteCount(Sheets.Worksheet(FirstRow)) + MiB;
		var otherParts = new[] { "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/styles.xml" }
			.Sum(p => Encoding.UTF8.GetByteCount(package.Text(p)));
		var options = NoHeader(maxUncompressedSize: otherParts + (long)(6.5 * sheetSize));

		using var workbook = async ? await ExcelWorkbook.OpenAsync(new MemoryStream(zip), options) : ExcelWorkbook.Open(new MemoryStream(zip), options);
		for (var n = 0; n < 6; n++)
		{
			using var reader = await workbook.OpenReaderAsync(async, n);
			Assert.Equal(["1"], await reader.ReadAllAsync(async));
		}

		using var seventh = await workbook.OpenReaderAsync(async, 6);
		var e = await Assert.ThrowsAsync<InvalidDataException>(() => seventh.ReadAllAsync(async));
		Assert.StartsWith("Reading xl/worksheets/sheet7.xml takes the workbook past ExcelReaderOptions.MaxUncompressedSize", e.Message);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CountsGapFieldsSoThatAMillionCellsInTheLastColumnStop(bool async)
	{
		// A million rows of one cell in XFD: about 40 MB of XML that would make 16 billion empty fields. Each counts as a
		// byte (decision 5), so a 16 MB limit stops the reader at about the thousandth row.
		var package = Workbooks.Data("").SetStreamed(Sheets.PartName, stream => WriteRows(stream, 1_000_000, n => $"<row r=\"{n}\"><c r=\"XFD{n}\"><v>1</v></c></row>"));
		var options = NoHeader(maxUncompressedSize: 16L * MiB);

		using var workbook = await Workbooks.OpenAsync(package, async, options);
		using var reader = await workbook.OpenReaderAsync(async);
		var rows = 0;
		var e = await Assert.ThrowsAsync<InvalidDataException>(async () =>
		{
			while (async ? await reader.ReadAsync() : reader.Read())
			{
				rows++;
			}
		});

		Assert.StartsWith($"Reading {Sheets.PartName} takes the workbook past ExcelReaderOptions.MaxUncompressedSize", e.Message);
		Assert.InRange(rows, 1, (int)(16L * MiB / (CellReference.MaxColumns - 1)));
	}

	[Theory]
	[InlineData("value", false)]
	[InlineData("value", true)]
	[InlineData("attributes", false)]
	[InlineData("attributes", true)]
	public async Task StopsASingleTokenAtThePartLimit(string token, bool async)
	{
		// A 300 MB value, or a cell with a million attributes, under a 4 MB part limit: the window stops growing there.
		var package = Workbooks.Data("").SetStreamed(Sheets.PartName, stream =>
		{
			if (token == "value")
			{
				WriteSheet(stream, "<row r=\"1\"><c r=\"A1\"><v>", 300L * MiB, "</v></c></row>", (byte)'1');
			}
			else
			{
				Write(stream, SheetStart + "<row r=\"1\"><c r=\"A1\"");
				WriteRows(stream, 1_000_000, n => $" a{n}=\"\"", false);
				Write(stream, "><v>1</v></c></row>" + SheetEnd);
			}
		});
		var zip = package.ToArray();
		var input = new ReadTrackingStream(zip);
		var options = NoHeader(maxPartSize: 4L * MiB);

		using var workbook = async ? await ExcelWorkbook.OpenAsync(input, options) : ExcelWorkbook.Open(input, options);
		input.Forget();
		using var reader = await workbook.OpenReaderAsync(async);
		var e = await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAllAsync(async));
		Assert.EndsWith($"the token or cell there is larger than ExcelReaderOptions.MaxPartSize ({4L * MiB} bytes).", e.Message);

		AssertStoppedEarly(zip, input, Sheets.PartName, 4L * MiB);
	}

	[Theory]
	[InlineData("cell", false)]
	[InlineData("cell", true)]
	[InlineData("sheetData", false)]
	[InlineData("sheetData", true)]
	[InlineData("string", false)]
	[InlineData("string", true)]
	public async Task SkipsAMillionNestedElements(string where, bool async)
	{
		// A million unknown elements nested in a cell, in sheetData, or in a shared string: skipped by depth, without
		// recursion, and the value after them read.
		var package = Workbooks.Data("").SetStreamed(Sheets.PartName, stream =>
		{
			Write(stream, SheetStart);
			if (where == "sheetData")
			{
				Nest(stream);
			}

			Write(stream, where == "string" ? "<row r=\"1\"><c r=\"A1\" t=\"s\">" : "<row r=\"1\"><c r=\"A1\">");
			if (where == "cell")
			{
				Nest(stream);
			}

			Write(stream, (where == "string" ? "<v>0</v>" : "<v>7</v>") + "</c></row>" + SheetEnd);
		}).SharedStringsPart("").SetStreamed(StringsPart, stream =>
		{
			Write(stream, $"<sst xmlns=\"{TestPackage.MainNamespace}\"><si>");
			if (where == "string")
			{
				Nest(stream);
			}

			Write(stream, "<t>seven</t></si></sst>");
		});

		using var workbook = await Workbooks.OpenAsync(package, async, NoHeader());
		using var reader = await workbook.OpenReaderAsync(async);
		Assert.Equal([where == "string" ? "seven" : "7"], await reader.ReadAllAsync(async));
	}

	[Theory]
	[InlineData("xl/workbook.xml", false)]
	[InlineData("xl/workbook.xml", true)]
	[InlineData("xl/styles.xml", false)]
	[InlineData("xl/styles.xml", true)]
	[InlineData(StringsPart, false)]
	[InlineData(StringsPart, true)]
	[InlineData(Sheets.PartName, false)]
	[InlineData(Sheets.PartName, true)]
	public async Task NeverReadsAnExternalEntityOrExpandsOne(string part, bool async)
	{
		// A DOCTYPE with an external entity to a file that no one else may open, and the billion laughs: the part is
		// rejected at its DOCTYPE, the file is never opened (opening it would throw a sharing violation instead), and no
		// entity is expanded.
		var canary = Path.Combine(Path.GetTempPath(), $"CsvHelper.Excel.Tests.canary.{Guid.NewGuid():N}.txt");
		using var locked = new FileStream(canary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
		var laughs = string.Concat(Enumerable.Range(1, 9).Select(n => $"<!ENTITY lol{n} \"{string.Concat(Enumerable.Repeat($"&lol{n - 1};", 10))}\">"));
		var doctype = $"<!DOCTYPE lolz [<!ENTITY lol0 \"lol\">{laughs}<!ENTITY xxe SYSTEM \"{new Uri(canary).AbsoluteUri}\">]>";
		var package = Workbooks.Data(FirstRow).SharedStrings("<t>a</t>")
			.Replace(part, TestPackage.Declaration, TestPackage.Declaration + doctype)
			.Replace(Sheets.PartName, "<v>1</v>", "<v>&xxe;&lol9;</v>");

		var e = await Record.ExceptionAsync(async () =>
		{
			using var workbook = await Workbooks.OpenAsync(package, async, NoHeader());
			using var reader = await workbook.OpenReaderAsync(async);
			await reader.ReadAllAsync(async);
		});

		Assert.True(e is InvalidDataException or CsvFormatException, $"{e?.GetType().FullName ?? "no exception"}: {e?.Message}");
		Assert.Contains("it has a DOCTYPE, which Office Open XML parts must not have", e!.Message);
		Assert.Contains(part, e.Message);
	}

	[Theory]
	[InlineData("&#0;", false)]
	[InlineData("&#0;", true)]
	[InlineData("&#xD800;", false)]
	[InlineData("&#xD800;", true)]
	[InlineData("&#x110000;", false)]
	[InlineData("&#x110000;", true)]
	[InlineData("&undefined;", false)]
	[InlineData("&undefined;", true)]
	public async Task RejectsReferencesThatArentCharacters(string reference, bool async)
	{
		var package = Workbooks.Data($"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>a{reference}b</t></is></c></row>");

		using var workbook = await Workbooks.OpenAsync(package, async, NoHeader());
		using var reader = await workbook.OpenReaderAsync(async);
		var e = await Assert.ThrowsAsync<CsvFormatException>(() => reader.ReadAllAsync(async));
		Assert.Equal("Data!A1", e.Location);
		Assert.Contains($"'{reference}'", e.Message);
	}

	[Theory]
	[InlineData(ExcelFormulas.FormulaText, false)]
	[InlineData(ExcelFormulas.FormulaText, true)]
	[InlineData(ExcelFormulas.CachedValue, false)]
	[InlineData(ExcelFormulas.CachedValue, true)]
	public async Task StopsAMillionSharedFormulaGroupsAtThePartLimit(ExcelFormulas formulas, bool async)
	{
		// A million cells that each define a shared formula group of their own. Read as text, the store counts each group's
		// formula and 64 bytes, 66 MB in all, toward a 4 MB part limit, which stops the reader at about the 63,000th group.
		// Read as their values, the formulas aren't held, and every row reads.
		var zip = MillionGroups.Value;
		var options = NoHeader(maxPartSize: 4L * MiB, formulas: formulas);

		using var workbook = async ? await ExcelWorkbook.OpenAsync(new MemoryStream(zip), options) : ExcelWorkbook.Open(new MemoryStream(zip), options);
		using var reader = await workbook.OpenReaderAsync(async);
		var rows = 0;
		var e = await Record.ExceptionAsync(async () =>
		{
			while (async ? await reader.ReadAsync() : reader.Read())
			{
				rows++;
			}
		});

		if (formulas == ExcelFormulas.CachedValue)
		{
			Assert.Null(e);
			Assert.Equal(1_000_000, rows);
		}
		else
		{
			Assert.Equal($"The shared formulas of {Sheets.PartName} take more than ExcelReaderOptions.MaxPartSize ({4L * MiB} bytes).", Assert.IsType<InvalidDataException>(e).Message);
			Assert.InRange(rows, 1, (int)(4L * MiB / 66));
		}
	}

	[Theory]
	[InlineData(ExcelFormulas.FormulaText, false)]
	[InlineData(ExcelFormulas.FormulaText, true)]
	[InlineData(ExcelFormulas.CachedValue, false)]
	[InlineData(ExcelFormulas.CachedValue, true)]
	public async Task StopsALongFormulaAtThePartLimit(ExcelFormulas formulas, bool async)
	{
		// A formula of 64 MB under a 4 MB part limit. A cell is read whole, its f with it, whether the formula is read or
		// skipped, so the window stops growing at the limit either way.
		var package = Workbooks.Data("").SetStreamed(Sheets.PartName, stream => WriteSheet(stream, "<row r=\"1\"><c r=\"A1\"><f>", 64L * MiB, "</f><v>1</v></c></row>", (byte)'1'));
		var zip = package.ToArray();
		var input = new ReadTrackingStream(zip);
		var options = NoHeader(maxPartSize: 4L * MiB, formulas: formulas);

		using var workbook = async ? await ExcelWorkbook.OpenAsync(input, options) : ExcelWorkbook.Open(input, options);
		input.Forget();
		using var reader = await workbook.OpenReaderAsync(async);
		var e = await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAllAsync(async));
		Assert.EndsWith($"the token or cell there is larger than ExcelReaderOptions.MaxPartSize ({4L * MiB} bytes).", e.Message);

		AssertStoppedEarly(zip, input, Sheets.PartName, 4L * MiB);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StopsASharedFormulaThatGrowsPastThePartLimit(bool async)
	{
		// A shared formula of 1 MB, 1:1 repeated, under a 4 MB part limit. Moved a row down, it stays 1 MB and reads. Moved
		// to the last row it would be 4 MB (1048576:1048576), and the room for it, with the formula the store holds, passes
		// the limit.
		var formula = string.Join(",", Enumerable.Repeat("1:1", MiB / 4));
		var package = Workbooks.Data(
			$"<row r=\"1\"><c r=\"A1\"><f t=\"shared\" ref=\"A1:A1048576\" si=\"0\">{formula}</f></c></row>" +
			"<row r=\"2\"><c r=\"A2\"><f t=\"shared\" si=\"0\"/></c></row>" +
			"<row r=\"1048576\"><c r=\"A1048576\"><f t=\"shared\" si=\"0\"/></c></row>");
		var options = NoHeader(maxPartSize: 4L * MiB, formulas: ExcelFormulas.FormulaText);

		using var workbook = await Workbooks.OpenAsync(package, async, options);
		using var reader = await workbook.OpenReaderAsync(async);
		var records = new List<string>();
		var e = await Assert.ThrowsAsync<InvalidDataException>(async () =>
		{
			while (async ? await reader.ReadAsync() : reader.Read())
			{
				records.Add(reader.GetString(0));
			}
		});

		Assert.Equal($"The shared formulas of {Sheets.PartName} take more than ExcelReaderOptions.MaxPartSize ({4L * MiB} bytes).", e.Message);
		Assert.Equal(["=" + formula, "=" + formula.Replace("1:1", "2:2")], records);
	}

	[Theory]
	[InlineData("rows", false)]
	[InlineData("rows", true)]
	[InlineData("cells", false)]
	[InlineData("cells", true)]
	public async Task CountsARepeatedSharedStringTowardTheLimit(string where, bool async)
	{
		// One shared string of a megabyte, read by a million rows (40 MB of XML), or by a row of 16,384 cells: each cell
		// counts the string's bytes beyond its own XML, so a 16 MB limit stops the reader within 16 of them, rather than after
		// a terabyte of fields, or a record of 16 GB.
		var package = Workbooks.Data("").SharedStrings($"<t>{new string('x', MiB)}</t>").SetStreamed(Sheets.PartName, stream =>
		{
			if (where == "rows")
			{
				WriteRows(stream, 1_000_000, n => $"<row r=\"{n}\"><c r=\"A{n}\" t=\"s\"><v>0</v></c></row>");
			}
			else
			{
				Write(stream, SheetStart + "<row r=\"1\">");
				WriteRows(stream, CellReference.MaxColumns, _ => "<c t=\"s\"><v>0</v></c>", false);
				Write(stream, "</row>" + SheetEnd);
			}
		});
		var options = NoHeader(maxPartSize: 4L * MiB, maxUncompressedSize: 16L * MiB);

		using var workbook = await Workbooks.OpenAsync(package, async, options);
		using var reader = await workbook.OpenReaderAsync(async);
		var rows = 0;
		var e = await Assert.ThrowsAsync<InvalidDataException>(async () =>
		{
			while (async ? await reader.ReadAsync() : reader.Read())
			{
				rows++;
			}
		});

		Assert.StartsWith($"Reading {Sheets.PartName} takes the workbook past ExcelReaderOptions.MaxUncompressedSize", e.Message);
		Assert.InRange(rows, where == "rows" ? 1 : 0, where == "rows" ? 16 : 0);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task CountsARepeatedSharedFormulaTowardTheLimit(bool async)
	{
		// A shared formula of a megabyte and a million cells of its group: each moved formula counts its bytes beyond its
		// cell's XML, so a 16 MB limit stops the reader within 16 of them, rather than after a terabyte of fields and hours
		// of moving references.
		var formula = string.Join("+", Enumerable.Repeat("A1", MiB / 3));
		var package = Workbooks.Data("").SetStreamed(Sheets.PartName, stream =>
		{
			Write(stream, SheetStart + $"<row r=\"1\"><c r=\"B1\"><f t=\"shared\" ref=\"B1:B1000000\" si=\"0\">{formula}</f></c></row>");
			WriteRows(stream, 999_999, n => $"<row r=\"{n + 1}\"><c r=\"B{n + 1}\"><f t=\"shared\" si=\"0\"/></c></row>", false);
			Write(stream, SheetEnd);
		});
		var options = NoHeader(maxPartSize: 4L * MiB, maxUncompressedSize: 16L * MiB, formulas: ExcelFormulas.FormulaText);

		using var workbook = await Workbooks.OpenAsync(package, async, options);
		using var reader = await workbook.OpenReaderAsync(async);
		var rows = 0;
		var e = await Assert.ThrowsAsync<InvalidDataException>(async () =>
		{
			while (async ? await reader.ReadAsync() : reader.Read())
			{
				rows++;
			}
		});

		Assert.StartsWith($"Reading {Sheets.PartName} takes the workbook past ExcelReaderOptions.MaxUncompressedSize", e.Message);
		Assert.InRange(rows, 1, 16);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task MovesFormulasNestedAMillionDeep(bool async)
	{
		// Shared formulas of a million parentheses around a reference, and a million brackets nested around one, and a million
		// brackets that never close: moved a row down in one pass each, without recursion. A reference in brackets is a
		// structured reference's text, which doesn't move.
		const int Depth = 1_000_000;
		var nested = new string('(', Depth) + "A1" + new string(')', Depth) + "+T[" + new string('[', Depth) + "A1" + new string(']', Depth) + "]+B1";
		var unclosed = "B1+T" + new string('[', Depth) + "A1";
		var package = Workbooks.Data(
			$"<row r=\"1\"><c r=\"C1\"><f t=\"shared\" ref=\"C1:C2\" si=\"0\">{nested}</f></c><c r=\"D1\"><f t=\"shared\" ref=\"D1:D2\" si=\"1\">{unclosed}</f></c></row>" +
			"<row r=\"2\"><c r=\"C2\"><f t=\"shared\" si=\"0\"/></c><c r=\"D2\"><f t=\"shared\" si=\"1\"/></c></row>");
		var options = NoHeader(formulas: ExcelFormulas.FormulaText);

		using var workbook = await Workbooks.OpenAsync(package, async, options);
		using var reader = await workbook.OpenReaderAsync(async);
		Assert.True(async ? await reader.ReadAsync() : reader.Read());
		Assert.True(async ? await reader.ReadAsync() : reader.Read());
		var movedNested = new string('(', Depth) + "A2" + new string(')', Depth) + "+T[" + new string('[', Depth) + "A1" + new string(']', Depth) + "]+B2";
		Assert.True(reader.GetString(2) == "=" + movedNested, "The nested formula didn't move as expected.");
		Assert.True(reader.GetString(3) == "=B2+T" + new string('[', Depth) + "A1", "The unclosed formula didn't move as expected.");
		Assert.False(async ? await reader.ReadAsync() : reader.Read());
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadsAbsurdIsoDatesInOnePass(bool async)
	{
		// t="d" texts of a million characters: a year of a million digits, a million digits of a second's fraction (of which
		// the first 20 count), a duration of a million-digit number of hours and one of a million zeros, and a date in a
		// million spaces. Each is read in one pass, the dates as dates and the others as text.
		const int Length = 1_000_000;
		var year = new string('2', Length) + "-01-02";
		var fraction = "2024-01-02T03:04:05.5" + new string('0', Length);
		var hours = "PT" + new string('1', Length) + "H";
		var zeros = "PT" + new string('0', Length) + "S";
		var spaced = new string(' ', Length) + "2024-01-02" + new string(' ', Length);
		var cells = new[] { year, fraction, hours, zeros, spaced }.Select((text, i) => $"<c r=\"{(char)('A' + i)}1\" t=\"d\" s=\"1\"><v>{text}</v></c>");
		var package = Workbooks.Data($"<row r=\"1\">{string.Concat(cells)}</row>");

		using var workbook = await Workbooks.OpenAsync(package, async, NoHeader());
		using var reader = await workbook.OpenReaderAsync(async);
		Assert.True(async ? await reader.ReadAsync() : reader.Read());
		Assert.True(reader.GetString(0) == year, "The year of a million digits isn't its text.");
		Assert.Equal(new DateTime(2024, 1, 2, 3, 4, 5, 500), reader.Parse<DateTime>(1));
		Assert.True(reader.GetString(2) == hours, "The duration of a million-digit number of hours isn't its text.");
		Assert.True(reader.GetString(3) == zeros, "The duration of a million zeros isn't its text.");
		Assert.Equal(new DateTime(2024, 1, 2), reader.Parse<DateTime>(4));
		Assert.False(reader.TryParse<DateTime>(0, out _));
		Assert.False(reader.TryParse<TimeSpan>(2, out _));
	}

	/// <summary>The options of a reader with no header, so that every row is a record, and these limits.</summary>
	/// <param name="maxPartSize">The part limit.</param>
	/// <param name="maxUncompressedSize">The workbook's limit.</param>
	/// <param name="formulas">What cells with a formula read as.</param>
	/// <returns>The options.</returns>
	private static ExcelReaderOptions NoHeader(long maxPartSize = 256L * MiB, long maxUncompressedSize = 4L << 30, ExcelFormulas formulas = ExcelFormulas.CachedValue) => new()
	{
		MaxPartSize = maxPartSize,
		MaxUncompressedSize = maxUncompressedSize,
		Formulas = formulas,
		Csv = new() { HasHeader = false },
	};

	/// <summary>
	/// Checks that the reader stopped early in an entry's compressed data: it read into it, but no further than twice the
	/// limit's share of it, plus 16 KB for the inflater's input buffer.
	/// </summary>
	/// <param name="zip">The archive's bytes.</param>
	/// <param name="input">The stream the workbook read.</param>
	/// <param name="part">The entry.</param>
	/// <param name="limit">The limit that stopped it, in inflated bytes.</param>
	private static void AssertStoppedEarly(byte[] zip, ReadTrackingStream input, string part, long limit)
	{
		var (start, length) = ZipBytes.DataRange(zip, part);
		var bound = (long)(2.0 * length * limit / ZipBytes.UncompressedSize(zip, part)) + 16 * 1024;
		Assert.InRange(bound, 1, length - 1);
		Assert.InRange(input.ReadInto(start, length), 1, bound);
	}

	/// <summary>Writes a million unknown elements, each nested in the one before.</summary>
	/// <param name="stream">The entry's stream.</param>
	private static void Nest(Stream stream)
	{
		WriteRows(stream, 1_000_000, _ => "<x>", false);
		WriteRows(stream, 1_000_000, _ => "</x>", false);
	}

	/// <summary>Writes a worksheet part: its start, some rows, a filler of one byte repeated, more rows, and its end.</summary>
	/// <param name="stream">The entry's stream.</param>
	/// <param name="before">The XML before the filler.</param>
	/// <param name="filler">The filler's length.</param>
	/// <param name="after">The XML after the filler.</param>
	/// <param name="fill">The filler's byte: a space by default.</param>
	private static void WriteSheet(Stream stream, string before, long filler, string after, byte fill = (byte)' ')
	{
		Write(stream, SheetStart + before);
		Fill(stream, filler, fill);
		Write(stream, after + SheetEnd);
	}

	/// <summary>Writes a shared strings part of one string, padded with white space to a length.</summary>
	/// <param name="stream">The entry's stream.</param>
	/// <param name="filler">The white space's length.</param>
	private static void WriteStrings(Stream stream, long filler)
	{
		Write(stream, $"<sst xmlns=\"{TestPackage.MainNamespace}\"><si><t>a</t></si>");
		Fill(stream, filler, (byte)' ');
		Write(stream, "</sst>");
	}

	/// <summary>Writes rows of XML, each from its number, inside a worksheet part's sheetData, or alone.</summary>
	/// <param name="stream">The entry's stream.</param>
	/// <param name="count">The number of rows.</param>
	/// <param name="row">Makes a row's XML from its number, counting from 1.</param>
	/// <param name="wrap">Whether to write the worksheet's start and end around the rows, rather than the rows alone.</param>
	private static void WriteRows(Stream stream, int count, Func<int, string> row, bool wrap = true)
	{
		if (wrap)
		{
			Write(stream, SheetStart);
		}

		var text = new StringBuilder();
		for (var n = 1; n <= count; n++)
		{
			text.Append(row(n));
			if (text.Length > 60_000)
			{
				Write(stream, text.ToString());
				text.Clear();
			}
		}

		Write(stream, text.ToString());
		if (wrap)
		{
			Write(stream, SheetEnd);
		}
	}

	/// <summary>Writes a byte repeated.</summary>
	/// <param name="stream">The entry's stream.</param>
	/// <param name="count">How many times.</param>
	/// <param name="fill">The byte.</param>
	private static void Fill(Stream stream, long count, byte fill)
	{
		var chunk = fill == (byte)' ' ? Spaces : Enumerable.Repeat(fill, Spaces.Length).ToArray();
		for (var left = count; left > 0; left -= chunk.Length)
		{
			stream.Write(chunk, 0, (int)Math.Min(chunk.Length, left));
		}
	}

	/// <summary>Writes text as UTF-8.</summary>
	/// <param name="stream">The entry's stream.</param>
	/// <param name="text">The text.</param>
	private static void Write(Stream stream, string text)
	{
		var bytes = Encoding.UTF8.GetBytes(text);
		stream.Write(bytes, 0, bytes.Length);
	}
}
