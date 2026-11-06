using System.Globalization;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ExcelDataReader;
using Cell = DocumentFormat.OpenXml.Spreadsheet.Cell;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// An oracle's reading of a workbook's structures: each worksheet's merged ranges, hidden rows (of the rows asked about)
/// and hidden columns, its tables and its defined names; <see langword="null"/> or missing for what the oracle doesn't read.
/// </summary>
/// <param name="tables">The tables, as <see cref="CorpusFiles.TableText"/> writes them, or <see langword="null"/>.</param>
/// <param name="names">The defined names, as <see cref="CorpusFiles.NameText"/> writes them, by name, or <see langword="null"/>.</param>
internal sealed class OracleStructure(List<string>? tables, Dictionary<string, string>? names)
{
	/// <summary>Each worksheet's merged ranges, as <see cref="Ordered"/> gives them.</summary>
	public Dictionary<string, List<string>> Merged { get; } = [];

	/// <summary>Each worksheet's hidden rows, of those asked about.</summary>
	public Dictionary<string, HashSet<long>> HiddenRows { get; } = [];

	/// <summary>Whether each column, from 0, is hidden, by worksheet; missing when the oracle doesn't read columns.</summary>
	public Dictionary<string, bool[]> HiddenColumns { get; } = [];

	/// <summary>The tables, in order, or <see langword="null"/>.</summary>
	public List<string>? Tables => tables;

	/// <summary>The defined names, by name, or <see langword="null"/>.</summary>
	public Dictionary<string, string>? Names => names;

	/// <summary>Ranges of cells in <see cref="ExcelRange"/>'s form, by first row and then first column.</summary>
	/// <param name="ranges">The ranges, in A1 form.</param>
	/// <returns>The ranges.</returns>
	public static List<string> Ordered(IEnumerable<string> ranges) =>
		[.. ranges.Select(ExcelRange.Parse).OrderBy(r => r.FirstRow).ThenBy(r => r.FirstColumn).Select(r => r.ToString())];
}

/// <summary>
/// Other readers of the corpus, each giving the cells with a value of every worksheet in <see cref="CellValue"/> form:
/// the Open XML SDK's raw cells read by CsvHelper.Excel's documented rules, ClosedXML's typed values, and
/// ExcelDataReader's streaming values. A date or time ClosedXML or ExcelDataReader gives as a <see cref="DateTime"/> is
/// a <see cref="SheetCellKind.DateNumber"/> with <see cref="CorpusFiles.DateText"/>, and a duration they give as a
/// <see cref="TimeSpan"/> a <see cref="SheetCellKind.Duration"/> with <see cref="CorpusFiles.DurationText"/>, to compare
/// with CsvHelper.Excel's conversions (<see cref="CorpusFiles.AsDotNet"/>).
/// </summary>
internal static class Oracles
{
	/// <summary>Registers the code pages ExcelDataReader needs on .NET, which has only UTF encodings built in.</summary>
	static Oracles()
	{
#if NET
		System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
#endif
	}

	/// <summary>
	/// The Open XML SDK (DOM): each cell's reference, type, value, inline runs and shared string, read by the rules
	/// CsvHelper.Excel documents (placement, valueless cells, trimming, booleans), with escapes decoded by a regular
	/// expression and phonetic runs left out.
	/// </summary>
	/// <param name="file">The workbook, relative to the corpus folder.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system, for the serials of <c>t="d"</c> cells.</param>
	/// <returns>The cells.</returns>
	public static SheetCells OpenXmlSdk(string file, bool date1904)
	{
		using var document = SpreadsheetDocument.Open(CorpusFiles.PathOf(file), false);
		var workbookPart = document.WorkbookPart!;
		var strings = workbookPart.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().Select(Joined).ToList() ?? [];
		var result = new SheetCells();
		foreach (var sheet in workbookPart.Workbook!.Sheets!.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>())
		{
			if (workbookPart.GetPartById(sheet.Id!.Value!) is not WorksheetPart part)
			{
				continue;
			}

			var cells = new Dictionary<(long, int), CellValue>();
			long lastRow = 0;
			foreach (var row in part.Worksheet!.Elements<SheetData>().FirstOrDefault()?.Elements<Row>() ?? [])
			{
				var rowCells = row.Elements<Cell>().ToList();
				var number = row.RowIndex?.Value is { } index ? index
					: rowCells.FirstOrDefault()?.CellReference?.Value is { } first ? RowOf(first)
					: lastRow + 1;
				var column = -1;
				foreach (var cell in rowCells)
				{
					column = cell.CellReference?.Value is { } reference ? ColumnOf(reference) : column + 1;
					if (ValueOf(cell, strings, date1904) is { } value)
					{
						cells[(number, column)] = value;
					}
				}

				lastRow = number;
			}

			result[sheet.Name!.Value!] = cells;
		}

		return result;
	}

	/// <summary>ClosedXML: each used cell's typed value, as <c>IXLCell.Value</c> gives it.</summary>
	/// <param name="file">The workbook, relative to the corpus folder.</param>
	/// <returns>The cells.</returns>
	public static SheetCells ClosedXml(string file)
	{
		using var workbook = new XLWorkbook(CorpusFiles.PathOf(file));
		var result = new SheetCells();
		foreach (var worksheet in workbook.Worksheets)
		{
			var cells = new Dictionary<(long, int), CellValue>();
			foreach (var cell in worksheet.CellsUsed(XLCellsUsedOptions.Contents))
			{
				var value = cell.Value;
				CellValue? typed = value.Type switch
				{
					XLDataType.Boolean => new CellValue(value.GetBoolean() ? "1" : "0", SheetCellKind.Bool),
					XLDataType.Number => new CellValue(Number(value.GetNumber()), SheetCellKind.Number),
					XLDataType.Text when value.GetText().Length > 0 => new CellValue(value.GetText(), SheetCellKind.Text),
					XLDataType.Error => new CellValue(ErrorText(value.GetError()), SheetCellKind.Error),
					XLDataType.DateTime => new CellValue(CorpusFiles.DateText(value.GetDateTime()), SheetCellKind.DateNumber),
					XLDataType.TimeSpan => new CellValue(CorpusFiles.DurationText(value.GetTimeSpan()), SheetCellKind.Duration),
					_ => null,
				};
				if (typed is { } some)
				{
					cells[(cell.Address.RowNumber, cell.Address.ColumnNumber - 1)] = some;
				}
			}

			result[worksheet.Name] = cells;
		}

		return result;
	}

	/// <summary>ClosedXML: each formula cell's <c>FormulaA1</c>, with <c>=</c> before it, as text.</summary>
	/// <param name="file">The workbook, relative to the corpus folder.</param>
	/// <returns>The formula cells.</returns>
	public static SheetCells ClosedXmlFormulas(string file)
	{
		using var workbook = new XLWorkbook(CorpusFiles.PathOf(file));
		var result = new SheetCells();
		foreach (var worksheet in workbook.Worksheets)
		{
			result[worksheet.Name] = worksheet.CellsUsed(XLCellsUsedOptions.All)
				.Where(c => c.HasFormula)
				.ToDictionary(c => ((long)c.Address.RowNumber, c.Address.ColumnNumber - 1), c => new CellValue("=" + c.FormulaA1, SheetCellKind.Text));
		}

		return result;
	}

	/// <summary>ExcelDataReader: each row's values, the rows counted from the first, as its reader returns them.</summary>
	/// <param name="file">The workbook, relative to the corpus folder.</param>
	/// <returns>The cells.</returns>
	public static SheetCells ExcelDataReader(string file)
	{
		using var stream = File.OpenRead(CorpusFiles.PathOf(file));
		using var reader = ExcelReaderFactory.CreateReader(stream);
		var result = new SheetCells();
		do
		{
			var cells = new Dictionary<(long, int), CellValue>();
			long row = 0;
			while (reader.Read())
			{
				row++;
				for (var i = 0; i < reader.FieldCount; i++)
				{
					CellValue? typed = reader.GetValue(i) switch
					{
						null => reader.GetCellError(i) is { } error ? new CellValue(ErrorText(error), SheetCellKind.Error) : null,
						string text when text.Length > 0 => new CellValue(text, SheetCellKind.Text),
						double number => new CellValue(Number(number), SheetCellKind.Number),
						bool flag => new CellValue(flag ? "1" : "0", SheetCellKind.Bool),
						DateTime date => new CellValue(CorpusFiles.DateText(date), SheetCellKind.DateNumber),
						TimeSpan time => new CellValue(CorpusFiles.DurationText(time), SheetCellKind.Duration),
						string => null,
						var other => new CellValue(Convert.ToString(other, CultureInfo.InvariantCulture) ?? "", SheetCellKind.Text),
					};
					if (typed is { } some)
					{
						cells[(row, i)] = some;
					}
				}
			}

			result[reader.Name] = cells;
		}
		while (reader.NextResult());

		return result;
	}

	/// <summary>
	/// ClosedXML's structures: each worksheet's merged ranges (<c>MergedRanges</c>), its hidden rows of 1 to the row given
	/// (<c>Row(r).IsHidden</c>) and hidden columns (<c>Column(c).IsHidden</c>), its tables, and the workbook's and each
	/// worksheet's defined names.
	/// </summary>
	/// <param name="file">The workbook, relative to the corpus folder.</param>
	/// <param name="lastRows">The last row to ask about, by worksheet.</param>
	/// <returns>The structures.</returns>
	public static OracleStructure ClosedXmlStructure(string file, IReadOnlyDictionary<string, long> lastRows)
	{
		using var workbook = new XLWorkbook(CorpusFiles.PathOf(file));
		var result = new OracleStructure(tables: [], names: []);
		foreach (var worksheet in workbook.Worksheets)
		{
			var name = worksheet.Name;
			result.Merged[name] = OracleStructure.Ordered(worksheet.MergedRanges.Select(r => r.RangeAddress.ToString()!));
			var last = lastRows.TryGetValue(name, out var row) ? row : 0;
			result.HiddenRows[name] = [.. Enumerable.Range(1, (int)last).Where(r => worksheet.Row(r).IsHidden).Select(r => (long)r)];
			result.HiddenColumns[name] = [.. Enumerable.Range(1, XLHelper.MaxColumnNumber).Select(c => worksheet.Column(c).IsHidden)];
			result.Tables!.AddRange(worksheet.Tables.Select(t =>
				CorpusFiles.TableText(name, t.Name, t.RangeAddress.ToString()!, t.ShowHeaderRow ? 1 : 0, t.ShowTotalsRow ? 1 : 0, t.Fields.Select(f => f.Name))));
			foreach (var defined in worksheet.DefinedNames)
			{
				result.Names![new SheetLocation(name).QuotedName + "!" + defined.Name] = NameText(defined);
			}
		}

		foreach (var defined in workbook.DefinedNames)
		{
			result.Names![defined.Name] = NameText(defined);
		}

		result.Tables!.Sort(StringComparer.Ordinal);

		return result;
	}

	/// <summary>
	/// ExcelDataReader's structures: each worksheet's merged ranges (<c>MergeCells</c>), and its hidden rows as rows whose
	/// <c>RowHeight</c> is 0, the rows counted from the first as its reader returns them. It has no hidden columns, tables
	/// or names.
	/// </summary>
	/// <param name="file">The workbook, relative to the corpus folder.</param>
	/// <returns>The structures.</returns>
	public static OracleStructure ExcelDataReaderStructure(string file)
	{
		using var stream = File.OpenRead(CorpusFiles.PathOf(file));
		using var reader = ExcelReaderFactory.CreateReader(stream);
		var result = new OracleStructure(tables: null, names: null);
		do
		{
			result.Merged[reader.Name] = OracleStructure.Ordered((reader.MergeCells ?? []).Select(m => $"{ColumnName(m.FromColumn)}{m.FromRow + 1}:{ColumnName(m.ToColumn)}{m.ToRow + 1}"));
			var hidden = new HashSet<long>();
			long row = 0;
			while (reader.Read())
			{
				row++;
				if (reader.RowHeight == 0)
				{
					hidden.Add(row);
				}
			}

			result.HiddenRows[reader.Name] = hidden;
		}
		while (reader.NextResult());

		return result;
	}

	/// <summary>A ClosedXML defined name, as <see cref="CorpusFiles.NameText"/> writes CsvHelper.Excel's.</summary>
	/// <param name="name">The name.</param>
	/// <returns>The description.</returns>
	private static string NameText(IXLDefinedName name) => $"{(name.Visible ? "shown" : "hidden")} ={name.RefersTo}";

	/// <summary>A 0-based column's letters.</summary>
	/// <param name="column">The column.</param>
	/// <returns>The letters, such as <c>B</c>.</returns>
	private static string ColumnName(int column)
	{
		var name = "";
		for (column++; column > 0; column = (column - 1) / 26)
		{
			name = (char)('A' + (column - 1) % 26) + name;
		}

		return name;
	}

	/// <summary>A number as CsvHelper.Excel's canonical comparison form: the shortest text that round-trips.</summary>
	/// <param name="number">The number.</param>
	/// <returns>The text.</returns>
	public static string Number(double number) => number.ToString("R", CultureInfo.InvariantCulture);

	/// <summary>A cell's value by CsvHelper.Excel's rules, or <see langword="null"/> when it is valueless.</summary>
	/// <param name="cell">The cell.</param>
	/// <param name="strings">The shared strings' texts.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <returns>The value.</returns>
	private static CellValue? ValueOf(Cell cell, List<string> strings, bool date1904)
	{
		var type = cell.DataType?.InnerText ?? "n";
		var element = cell.ChildElements.FirstOrDefault(e => e is DocumentFormat.OpenXml.Spreadsheet.CellValue || (type == "inlineStr" && e is InlineString));
		if (element is null)
		{
			return null;
		}

		var text = element is InlineString inline ? Joined(inline) : CorpusFiles.DecodeEscapes(Trimmed(element, element.InnerText));
		var trimmed = text.Trim(' ', '\t', '\r', '\n');
		switch (type)
		{
			case "s":
				var shared = trimmed.Length == 0 ? "" : strings[int.Parse(trimmed, CultureInfo.InvariantCulture)];

				return shared.Length == 0 ? null : new CellValue(shared, SheetCellKind.Text);
			case "n":
				return trimmed.Length == 0 ? null : new CellValue(trimmed, SheetCellKind.Number);
			case "b":
				return trimmed.Length == 0 ? null
					: trimmed is "1" or "0" ? new CellValue(trimmed, SheetCellKind.Bool)
					: trimmed.Equals("true", StringComparison.OrdinalIgnoreCase) ? new CellValue("1", SheetCellKind.Bool)
					: trimmed.Equals("false", StringComparison.OrdinalIgnoreCase) ? new CellValue("0", SheetCellKind.Bool)
					: new CellValue(trimmed, SheetCellKind.Text);
			case "e":
				return text.Length == 0 ? null : new CellValue(text, SheetCellKind.Error);
			case "d" when IsoSerials.Of(trimmed, date1904) is { } serial:
				return new CellValue(serial, SheetCellKind.DateNumber);
			default:
				return text.Length == 0 ? null : new CellValue(text, SheetCellKind.Text);
		}
	}

	/// <summary>The text of a shared string or an inline string: its <c>t</c> elements and its runs' joined, each decoded on its own, phonetic runs left out.</summary>
	/// <param name="item">The <c>si</c> or <c>is</c> element.</param>
	/// <returns>The text.</returns>
	private static string Joined(OpenXmlElement item) =>
		string.Concat(item.ChildElements
			.SelectMany(e => e is Text t ? [t] : e is Run run ? run.Elements<Text>() : Enumerable.Empty<Text>())
			.Select(t => CorpusFiles.DecodeEscapes(Trimmed(t, t.Text))));

	/// <summary>
	/// The text of a <c>v</c> or <c>t</c>, trimmed of the XML white space around it unless the nearest <c>xml:space</c>, on
	/// it or an ancestor, is <c>preserve</c>: as Excel reads it.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <param name="text">Its text.</param>
	/// <returns>The text, trimmed or not.</returns>
	private static string Trimmed(OpenXmlElement element, string text)
	{
		for (var e = element; e is not null; e = e.Parent)
		{
			foreach (var attribute in e.GetAttributes())
			{
				if (attribute.LocalName == "space" && attribute.NamespaceUri == "http://www.w3.org/XML/1998/namespace")
				{
					return attribute.Value == "preserve" ? text : text.Trim(' ', '\t', '\r', '\n');
				}
			}
		}

		return text.Trim(' ', '\t', '\r', '\n');
	}

	/// <summary>The 0-based column of a reference such as <c>C14</c>.</summary>
	/// <param name="reference">The reference.</param>
	/// <returns>The column.</returns>
	private static int ColumnOf(string reference) => reference.TakeWhile(char.IsLetter).Aggregate(0, (column, c) => column * 26 + (char.ToUpperInvariant(c) - 'A' + 1)) - 1;

	/// <summary>The row of a reference such as <c>C14</c>.</summary>
	/// <param name="reference">The reference.</param>
	/// <returns>The row.</returns>
	private static long RowOf(string reference) => long.Parse(new string([.. reference.SkipWhile(char.IsLetter)]), CultureInfo.InvariantCulture);

	/// <summary>The text of a ClosedXML error.</summary>
	/// <param name="error">The error.</param>
	/// <returns>The text, as Excel writes it.</returns>
	private static string ErrorText(XLError error) => error switch
	{
		XLError.NullValue => "#NULL!",
		XLError.DivisionByZero => "#DIV/0!",
		XLError.IncompatibleValue => "#VALUE!",
		XLError.CellReference => "#REF!",
		XLError.NameNotRecognized => "#NAME?",
		XLError.NumberInvalid => "#NUM!",
		XLError.NoValueAvailable => "#N/A",
		_ => error.ToString(),
	};

	/// <summary>The text of an ExcelDataReader error.</summary>
	/// <param name="error">The error.</param>
	/// <returns>The text, as Excel writes it.</returns>
	private static string ErrorText(CellError error) => error switch
	{
		CellError.NULL => "#NULL!",
		CellError.DIV0 => "#DIV/0!",
		CellError.VALUE => "#VALUE!",
		CellError.REF => "#REF!",
		CellError.NAME => "#NAME?",
		CellError.NUM => "#NUM!",
		CellError.NA => "#N/A",
		_ => error.ToString(),
	};
}
