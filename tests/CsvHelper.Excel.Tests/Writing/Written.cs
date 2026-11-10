using System.IO.Compression;
using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using ExcelDataReader;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>A record of text, written with its generated record writer.</summary>
[CsvRecord]
public partial class WrittenText
{
	/// <summary>A text cell.</summary>
	public string Name { get; set; } = "";

	/// <summary>A text cell, or no cell when null.</summary>
	public string? Note { get; set; }
}

/// <summary>
/// Workbooks written by <see cref="ExcelWorkbookWriter"/> for the writer's tests, synchronously or asynchronously, and
/// the readers they are read back with: CsvHelper.Excel's own, and the oracles (the Open XML SDK's validator, ClosedXML
/// and ExcelDataReader).
/// </summary>
internal static class Written
{
	/// <summary>The options of a reader that reads every row as a record, the header too, empty rows included.</summary>
	public static readonly ExcelReaderOptions AllRows = new() { Csv = new CsvReaderOptions { HasHeader = false, SkipEmptyLines = false } };

	/// <summary>
	/// Phase 4's look: no bold or frozen header and no auto-fit, for the tests of cells, whose XML they compare, so that
	/// Phase 5's defaults leave it as it was.
	/// </summary>
	public static readonly ExcelSheetOptions Phase4Look = new() { BoldHeader = false, FreezeHeader = false, AutoFit = 0 };

	/// <summary>Options with Phase 4's look (<see cref="Phase4Look"/>).</summary>
	/// <param name="options">The options, or <see langword="null"/> for the default ones.</param>
	/// <returns>The options with <see cref="ExcelWriterOptions.Sheets"/> set to Phase 4's look.</returns>
	public static ExcelWriterOptions Phase4(ExcelWriterOptions? options = null) => (options ?? ExcelWriterOptions.Default) with { Sheets = Phase4Look };

	/// <summary>Options whose sheets' records may have different numbers of fields, with strings as given.</summary>
	/// <param name="strings">Where text cells keep their strings.</param>
	/// <returns>The options.</returns>
	public static ExcelWriterOptions Ragged(ExcelStrings strings = ExcelStrings.Inline) =>
		new() { Strings = strings, Csv = new CsvWriterOptions { ValidateFieldCount = false } };

	/// <summary>Registers the code pages ExcelDataReader needs on .NET, which has only UTF encodings built in.</summary>
	static Written()
	{
#if NET
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
	}

	/// <summary>Writes a workbook into memory, and disposes the writer as the mode says.</summary>
	/// <param name="write">Writes the sheets; its flag says whether to write asynchronously.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <returns>The workbook's bytes.</returns>
	public static async Task<byte[]> WorkbookAsync(Func<ExcelWorkbookWriter, bool, Task> write, bool async, ExcelWriterOptions? options = null)
	{
		using var stream = new MemoryStream();
		var workbook = ExcelWorkbookWriter.Create(stream, options, leaveOpen: true);
		await write(workbook, async);
		if (async)
		{
			await workbook.DisposeAsync();
		}
		else
		{
			workbook.Dispose();
		}

		return stream.ToArray();
	}

	/// <summary>Writes rows of fields by hand into one sheet, a field per <see cref="CsvWriter.WriteField(string?)"/>.</summary>
	/// <param name="rows">The rows.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="options">The options, or <see langword="null"/>.</param>
	/// <param name="sheet">The sheet's name.</param>
	/// <returns>The workbook's bytes.</returns>
	public static Task<byte[]> RowsAsync(IEnumerable<string?[]> rows, bool async, ExcelWriterOptions? options = null, string sheet = "Data") =>
		WorkbookAsync(async (workbook, a) =>
		{
			var writer = workbook.AddSheet(sheet);
			foreach (var row in rows)
			{
				foreach (var field in row)
				{
					writer.WriteField(field);
				}

				await EndRecordAsync(writer, a);
			}

			await DisposeAsync(writer, a);
		}, async, options);

	/// <summary>Ends a record as the mode says.</summary>
	/// <param name="writer">The writer.</param>
	/// <param name="async">Whether to end it asynchronously.</param>
	/// <returns>A task that completes when the record ends.</returns>
	public static async Task EndRecordAsync(CsvWriter writer, bool async)
	{
		if (async)
		{
			await writer.EndRecordAsync();
		}
		else
		{
			writer.EndRecord();
		}
	}

	/// <summary>Disposes a writer as the mode says.</summary>
	/// <param name="writer">The writer.</param>
	/// <param name="async">Whether to dispose it asynchronously.</param>
	/// <returns>A task that completes when it is disposed.</returns>
	public static async Task DisposeAsync(CsvWriter writer, bool async)
	{
		if (async)
		{
			await writer.DisposeAsync();
		}
		else
		{
			writer.Dispose();
		}
	}

	/// <summary>Reads every row of every worksheet with CsvHelper.Excel, the header too.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>Each sheet's rows, by name in tab order, each row as its fields joined with <c>|</c>.</returns>
	public static List<(string Sheet, List<string> Rows)> Read(byte[] workbook)
	{
		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		var sheets = new List<(string, List<string>)>();
		foreach (var sheet in book.Sheets)
		{
			using var reader = book.OpenReader(sheet, AllRows);
			var rows = new List<string>();
			while (reader.Read())
			{
				rows.Add(Api.Workbooks.Fields(reader));
			}

			sheets.Add((sheet.Name, rows));
		}

		return sheets;
	}

	/// <summary>Reads every worksheet with CsvHelper.Excel, each as one line.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>Each sheet, in tab order, as its name and its rows: <c>Name: row 1 / row 2</c>.</returns>
	public static List<string> Sheets(byte[] workbook) => [.. Read(workbook).Select(s => s.Sheet + ": " + string.Join(" / ", s.Rows))];

	/// <summary>Reads the rows of a workbook's one sheet with CsvHelper.Excel.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>The rows, each as its fields joined with <c>|</c>.</returns>
	public static List<string> ReadOne(byte[] workbook) => Assert.Single(Read(workbook)).Rows;

	/// <summary>The Open XML SDK's validation errors of a workbook.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>The errors, each with its part and path.</returns>
	public static List<string> ValidationErrors(byte[] workbook)
	{
		using var document = SpreadsheetDocument.Open(new MemoryStream(workbook), false);

		return [.. new OpenXmlValidator().Validate(document).Select(e => $"{e.Part?.Uri} {e.Path?.XPath}: {e.Description}")];
	}

	/// <summary>Each worksheet's cells with a value, as ClosedXML reads their text: <c>C14=text</c>, by sheet in tab order.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>The sheets' cells.</returns>
	public static List<(string Sheet, List<string> Cells)> ClosedXml(byte[] workbook)
	{
		using var book = new XLWorkbook(new MemoryStream(workbook));

		return [.. book.Worksheets.Select(w => (w.Name, w.CellsUsed(XLCellsUsedOptions.Contents).Select(c => $"{c.Address}={c.GetString()}").ToList()))];
	}

	/// <summary>
	/// A worksheet's cells with a value as ClosedXML reads their values: <c>C14=Number:42</c>, with numbers in invariant
	/// form, dates as <c>yyyy-MM-ddTHH:mm:ss.fff</c> and durations as <c>c</c> formats them.
	/// </summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>The first sheet's cells.</returns>
	public static List<string> ClosedXmlValues(byte[] workbook)
	{
		using var book = new XLWorkbook(new MemoryStream(workbook));

		return [.. book.Worksheets.First().CellsUsed(XLCellsUsedOptions.Contents).Select(c => $"{c.Address}={c.Value.Type}:{Shown(c.Value)}")];
	}

	/// <summary>
	/// A worksheet's rows as ExcelDataReader reads their values, each as its type and text: <c>Double:42</c>,
	/// <c>DateTime:2024-01-15T10:30:00.000</c>, <c>Boolean:True</c>, <c>String:a</c>, and an empty cell as nothing.
	/// </summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>The first sheet's rows, each as its values joined with <c>|</c>.</returns>
	public static List<string> ExcelDataReaderValues(byte[] workbook)
	{
		using var reader = ExcelReaderFactory.CreateOpenXmlReader(new MemoryStream(workbook));
		var rows = new List<string>();
		while (reader.Read())
		{
			rows.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetValue(i) switch
			{
				null => "",
				DateTime d => "DateTime:" + d.ToString("yyyy-MM-dd'T'HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture),
				double d => "Double:" + d.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
				TimeSpan t => "TimeSpan:" + t.ToString("c", System.Globalization.CultureInfo.InvariantCulture),
				var value => value.GetType().Name + ":" + Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
			})));
		}

		return rows;
	}

	/// <summary>A ClosedXML value's text, in invariant form.</summary>
	/// <param name="value">The value.</param>
	/// <returns>The text.</returns>
	private static string Shown(XLCellValue value) => value.Type switch
	{
		XLDataType.Number => value.GetNumber().ToString("R", System.Globalization.CultureInfo.InvariantCulture),
		XLDataType.DateTime => value.GetDateTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture),
		XLDataType.TimeSpan => value.GetTimeSpan().ToString("c", System.Globalization.CultureInfo.InvariantCulture),
		XLDataType.Boolean => value.GetBoolean() ? "TRUE" : "FALSE",
		_ => value.ToString(System.Globalization.CultureInfo.InvariantCulture),
	};

	/// <summary>Each worksheet's rows as ExcelDataReader reads them, an empty cell as an empty string, by sheet in tab order.</summary>
	/// <param name="workbook">The workbook's bytes.</param>
	/// <returns>The sheets' rows, each as its values joined with <c>|</c>.</returns>
	public static List<(string Sheet, List<string> Rows)> ExcelDataReader(byte[] workbook)
	{
		using var reader = ExcelReaderFactory.CreateOpenXmlReader(new MemoryStream(workbook));
		var sheets = new List<(string, List<string>)>();
		do
		{
			var rows = new List<string>();
			while (reader.Read())
			{
				rows.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture) ?? "")));
			}

			sheets.Add((reader.Name, rows));
		}
		while (reader.NextResult());

		return sheets;
	}

	/// <summary>
	/// Text as the writer escapes it for a cell's XML, with its entities decoded: what a reader that doesn't decode the
	/// escapes reads.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <returns>The text with its escapes.</returns>
	public static string EscapedText(string text)
	{
		using var output = new XmlOutput(16);
		output.AppendText(Encoding.UTF8.GetBytes(text));

		return System.Net.WebUtility.HtmlDecode(Encoding.UTF8.GetString(output.Written.ToArray()));
	}

	/// <summary>A package's entries as text, by name, in the zip's order.</summary>
	/// <param name="package">The package's bytes.</param>
	/// <returns>The entries.</returns>
	public static List<(string Name, string Text)> Entries(byte[] package)
	{
		using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
		var entries = new List<(string, string)>();
		foreach (var entry in archive.Entries)
		{
			using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
			entries.Add((entry.FullName, reader.ReadToEnd()));
		}

		return entries;
	}

	/// <summary>A package's entry as text.</summary>
	/// <param name="package">The package's bytes.</param>
	/// <param name="name">The entry's name.</param>
	/// <returns>Its text.</returns>
	public static string Entry(byte[] package, string name) => Entries(package).Single(e => e.Name == name).Text;
}
