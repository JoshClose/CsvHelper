using System.IO.Compression;

namespace CsvHelper.Benchmarks;

/// <summary>
/// Writes the package assets data as <see cref="ExcelReport"/> lays it out, in the shape Excel saves the report when the
/// corpus tool builds it through COM.
/// </summary>
/// <remarks>
/// <para>
/// The package is <see cref="ExcelTextWorkbook"/>'s, with the sheet's relationships and the table part after the shared
/// strings, in Excel's order. Excel's theme and document properties are left out.
/// </para>
/// <para>The sheet is as Excel writes it:</para>
/// <list type="bullet">
/// <item>a <c>dimension</c> from <c>B1</c>, and <c>cols</c> for the hidden keys' column (a width of 0) and for the
/// columns Excel widened for the table's header (<see cref="ExcelReport.Fields"/>);</item>
/// <item>every cell of the text-formatted block, an empty one as a cell with only its style; each key as a number in
/// General, without a style;</item>
/// <item>the merged cells in formats of their own: Excel gives the cells of a merged range a new cell format, a copy of
/// the one they had (2, General, for the title's; 3, text, for the regions'), and writes each cell the range covers as
/// an empty cell in it;</item>
/// <item><c>spans</c> of <c>2:28</c> on every row, since every block of 16 rows has cells from <c>B</c> to <c>AB</c>,
/// and <c>hidden="1"</c> after them on a hidden row;</item>
/// <item>the merged ranges in the order they were made, the title's first; the page margins of a new workbook; and the
/// table part.</item>
/// </list>
/// <para>
/// The styles have Excel's four cell formats and, for the table, a differential format of the text format for its header
/// row (0) and each of its text columns (numbered from the last column, as Excel numbers them), as Excel writes them,
/// with <c>numFmtId="30"</c>. The table part is Excel's without the revision ids (<c>xr:uid</c>) Excel makes at random.
/// </para>
/// <para>
/// It writes the text the benchmark data has, as <see cref="ExcelTextWorkbook"/> does, which throws
/// <see cref="NotSupportedException"/> for text Excel would escape; <see cref="ExcelReport.Check"/> says which records
/// it lays out.
/// </para>
/// </remarks>
internal static class ExcelReportWorkbook
{
	/// <summary>The cell format of text: the header row's, the regions' and the fields'.</summary>
	private const string TextStyle = "1";

	/// <summary>The cell format of the title's merged cells, a copy of General.</summary>
	private const string TitleStyle = "2";

	/// <summary>The cell format of the regions' merged cells, a copy of the text format.</summary>
	private const string RegionStyle = "3";

	/// <summary>Writes the workbook.</summary>
	/// <param name="stream">The stream the package is written to. It is left open.</param>
	/// <param name="records">The header, the package assets data's names, then the data rows.</param>
	/// <param name="level">The compression of every entry.</param>
	/// <exception cref="NotSupportedException">The records are not ones the report lays out (see
	/// <see cref="ExcelReport.Check"/>), or a field has a character this writer doesn't write.</exception>
	public static void Write(Stream stream, IReadOnlyList<string[]> records, CompressionLevel level)
	{
		ExcelReport.Check(records);

		var shared = new ExcelTextWorkbook.SharedStrings();
		var dataRows = records.Count - 1;

		using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

		ExcelTextWorkbook.WriteEntry(archive, "[Content_Types].xml", level, WriteContentTypes);
		ExcelTextWorkbook.WriteEntry(archive, "_rels/.rels", level, ExcelTextWorkbook.WritePackageRelationships);
		ExcelTextWorkbook.WriteEntry(archive, "xl/workbook.xml", level, writer => ExcelTextWorkbook.WriteWorkbook(writer, ExcelReport.SheetName));
		ExcelTextWorkbook.WriteEntry(archive, "xl/_rels/workbook.xml.rels", level, writer => ExcelTextWorkbook.WriteWorkbookRelationships(writer, shared: true));
		ExcelTextWorkbook.WriteEntry(archive, "xl/worksheets/sheet1.xml", level, writer => WriteSheet(writer, records, shared));
		ExcelTextWorkbook.WriteEntry(archive, "xl/styles.xml", level, WriteStyles);
		ExcelTextWorkbook.WriteEntry(archive, "xl/sharedStrings.xml", level, writer => ExcelTextWorkbook.WriteSharedStrings(writer, shared));
		ExcelTextWorkbook.WriteEntry(archive, "xl/worksheets/_rels/sheet1.xml.rels", level, WriteSheetRelationships);
		ExcelTextWorkbook.WriteEntry(archive, "xl/tables/table1.xml", level, writer => WriteTable(writer, dataRows));
	}

	/// <summary>Writes the content types of the parts, the table's among them.</summary>
	/// <param name="writer">The entry's writer.</param>
	private static void WriteContentTypes(TextWriter writer)
	{
		var type = ExcelTextWorkbook.ContentType;

		writer.Write("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
		writer.Write("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
		writer.Write("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
		writer.Write($"<Override PartName=\"/xl/workbook.xml\" ContentType=\"{type}sheet.main+xml\"/>");
		writer.Write($"<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"{type}worksheet+xml\"/>");
		writer.Write($"<Override PartName=\"/xl/styles.xml\" ContentType=\"{type}styles+xml\"/>");
		writer.Write($"<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"{type}sharedStrings+xml\"/>");
		writer.Write($"<Override PartName=\"/xl/tables/table1.xml\" ContentType=\"{type}table+xml\"/>");
		writer.Write("</Types>");
	}

	/// <summary>Writes the sheet, adding its strings to the shared-strings table in the order the cells first use them.</summary>
	/// <param name="writer">The entry's writer.</param>
	/// <param name="records">The header, then the data rows.</param>
	/// <param name="shared">The shared-strings table.</param>
	private static void WriteSheet(TextWriter writer, IReadOnlyList<string[]> records, ExcelTextWorkbook.SharedStrings shared)
	{
		var first = ExcelReport.FirstColumn;
		var last = ExcelReport.LastColumn;
		var dataRows = records.Count - 1;
		var lastRow = ExcelReport.RowOf(dataRows - 1);
		var spans = $"{ExcelTextWorkbook.Number(first)}:{ExcelTextWorkbook.Number(last)}";

		var names = new string[last + 1];
		for (var c = 1; c <= last; c++)
		{
			names[c] = ExcelReport.ColumnName(c);
		}

		writer.Write($"<worksheet xmlns=\"{ExcelTextWorkbook.Main}\" xmlns:r=\"{ExcelTextWorkbook.Relationships}\"");
		writer.Write(" xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" mc:Ignorable=\"x14ac\"");
		writer.Write(" xmlns:x14ac=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac\">");
		writer.Write($"<dimension ref=\"{ExcelReport.Block(first, 1, last, lastRow)}\"/>");
		writer.Write("<sheetViews><sheetView tabSelected=\"1\" workbookViewId=\"0\"/></sheetViews>");
		writer.Write("<sheetFormatPr defaultRowHeight=\"14.5\" x14ac:dyDescent=\"0.35\"/>");
		WriteColumns(writer);
		writer.Write("<sheetData>");

		// The title, and the cells its merge covers.
		BeginRow(writer, "1", spans, hidden: false);
		TextCell(writer, names[first], "1", TitleStyle, shared.IndexOf(ExcelReport.Title));
		for (var c = first + 1; c <= last; c++)
		{
			EmptyCell(writer, names[c], "1", TitleStyle);
		}

		writer.Write("</row>");

		// The second title, in General.
		BeginRow(writer, "2", spans, hidden: false);
		writer.Write($"<c r=\"{names[first]}2\" t=\"s\"><v>{ExcelTextWorkbook.Number(shared.IndexOf(ExcelReport.Subtitle))}</v></c>");
		writer.Write("</row>");

		// The header, as text.
		var header = ExcelTextWorkbook.Number(ExcelReport.HeaderRow);
		BeginRow(writer, header, spans, hidden: false);
		TextCell(writer, names[first], header, TextStyle, shared.IndexOf(ExcelReport.RegionHeader));
		TextCell(writer, names[ExcelReport.KeyColumn], header, TextStyle, shared.IndexOf(ExcelReport.KeyHeader));
		for (var f = 0; f < ExcelReport.Fields.Length; f++)
		{
			TextCell(writer, names[ExcelReport.KeyColumn + 1 + f], header, TextStyle, shared.IndexOf(ExcelReport.Fields[f].Name));
		}

		writer.Write("</row>");

		for (var i = 0; i < dataRows; i++)
		{
			var row = ExcelTextWorkbook.Number(ExcelReport.RowOf(i));
			BeginRow(writer, row, spans, ExcelReport.IsHidden(i));

			if (ExcelReport.RegionOf(i) is { } region)
			{
				TextCell(writer, names[first], row, RegionStyle, shared.IndexOf(region));
			}
			else
			{
				EmptyCell(writer, names[first], row, RegionStyle);
			}

			writer.Write($"<c r=\"{names[ExcelReport.KeyColumn]}{row}\"><v>{ExcelTextWorkbook.Number(i + 1)}</v></c>");

			var fields = records[i + 1];
			for (var f = 0; f < fields.Length; f++)
			{
				var column = names[ExcelReport.KeyColumn + 1 + f];
				if (fields[f].Length > 0)
				{
					TextCell(writer, column, row, TextStyle, shared.IndexOf(fields[f]));
				}
				else
				{
					EmptyCell(writer, column, row, TextStyle);
				}
			}

			writer.Write("</row>");
		}

		writer.Write("</sheetData>");

		var ranges = ExcelReport.RegionRanges(dataRows).ToList();
		writer.Write($"<mergeCells count=\"{ExcelTextWorkbook.Number(ranges.Count + 1)}\">");
		writer.Write($"<mergeCell ref=\"{ExcelReport.TitleArea()}\"/>");
		foreach (var (firstRow, lastRegionRow) in ranges)
		{
			writer.Write($"<mergeCell ref=\"{ExcelReport.Block(first, firstRow, first, lastRegionRow)}\"/>");
		}

		writer.Write("</mergeCells>");
		writer.Write("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>");
		writer.Write("<tableParts count=\"1\"><tablePart r:id=\"rId1\"/></tableParts>");
		writer.Write("</worksheet>");
	}

	/// <summary>
	/// Writes the <c>cols</c>: the hidden keys' column, then each column Excel widened for the table's header, in column
	/// order.
	/// </summary>
	/// <param name="writer">The entry's writer.</param>
	private static void WriteColumns(TextWriter writer)
	{
		var key = ExcelTextWorkbook.Number(ExcelReport.KeyColumn);

		writer.Write("<cols>");
		writer.Write($"<col min=\"{key}\" max=\"{key}\" width=\"0\" hidden=\"1\" customWidth=\"1\"/>");
		for (var f = 0; f < ExcelReport.Fields.Length; f++)
		{
			if (ExcelReport.Fields[f].Width is { } width)
			{
				var column = ExcelTextWorkbook.Number(ExcelReport.KeyColumn + 1 + f);
				writer.Write($"<col min=\"{column}\" max=\"{column}\" width=\"{width}\" customWidth=\"1\"/>");
			}
		}

		writer.Write("</cols>");
	}

	/// <summary>Writes a row's start tag.</summary>
	/// <param name="writer">The entry's writer.</param>
	/// <param name="row">The row's number, as text.</param>
	/// <param name="spans">The row's <c>spans</c>.</param>
	/// <param name="hidden">Whether the row is hidden.</param>
	private static void BeginRow(TextWriter writer, string row, string spans, bool hidden)
	{
		writer.Write("<row r=\"");
		writer.Write(row);
		writer.Write("\" spans=\"");
		writer.Write(spans);
		writer.Write(hidden ? "\" hidden=\"1\" x14ac:dyDescent=\"0.35\">" : "\" x14ac:dyDescent=\"0.35\">");
	}

	/// <summary>Writes a cell of a shared string.</summary>
	/// <param name="writer">The entry's writer.</param>
	/// <param name="column">The cell's column letters.</param>
	/// <param name="row">The cell's row number, as text.</param>
	/// <param name="style">The cell's format.</param>
	/// <param name="index">The string's index in the shared-strings table.</param>
	private static void TextCell(TextWriter writer, string column, string row, string style, int index)
	{
		writer.Write("<c r=\"");
		writer.Write(column);
		writer.Write(row);
		writer.Write("\" s=\"");
		writer.Write(style);
		writer.Write("\" t=\"s\"><v>");
		writer.Write(ExcelTextWorkbook.Number(index));
		writer.Write("</v></c>");
	}

	/// <summary>Writes a cell without a value, which has only its format.</summary>
	/// <param name="writer">The entry's writer.</param>
	/// <param name="column">The cell's column letters.</param>
	/// <param name="row">The cell's row number, as text.</param>
	/// <param name="style">The cell's format.</param>
	private static void EmptyCell(TextWriter writer, string column, string row, string style)
	{
		writer.Write("<c r=\"");
		writer.Write(column);
		writer.Write(row);
		writer.Write("\" s=\"");
		writer.Write(style);
		writer.Write("\"/>");
	}

	/// <summary>
	/// Writes the styles: Excel's four cell formats (General, text, and their copies for merged cells), and a
	/// differential format of the text format for the table's header row and for each of its text columns.
	/// </summary>
	/// <param name="writer">The entry's writer.</param>
	private static void WriteStyles(TextWriter writer)
	{
		writer.Write($"<styleSheet xmlns=\"{ExcelTextWorkbook.Main}\">");
		writer.Write("<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Aptos Narrow\"/><family val=\"2\"/></font></fonts>");
		writer.Write("<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>");
		writer.Write("<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>");
		writer.Write("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");
		writer.Write("<cellXfs count=\"4\">");
		writer.Write("<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>");
		writer.Write("<xf numFmtId=\"49\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>");
		writer.Write("<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>");
		writer.Write("<xf numFmtId=\"49\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>");
		writer.Write("</cellXfs>");
		writer.Write("<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>");

		var formats = ExcelReport.Fields.Length + 1;
		writer.Write($"<dxfs count=\"{ExcelTextWorkbook.Number(formats)}\">");
		for (var i = 0; i < formats; i++)
		{
			writer.Write("<dxf><numFmt numFmtId=\"30\" formatCode=\"@\"/></dxf>");
		}

		writer.Write("</dxfs>");
		writer.Write("<tableStyles count=\"0\" defaultTableStyle=\"TableStyleMedium2\" defaultPivotStyle=\"PivotStyleLight16\"/>");
		writer.Write("</styleSheet>");
	}

	/// <summary>Writes the sheet's relationships: its table part.</summary>
	/// <param name="writer">The entry's writer.</param>
	private static void WriteSheetRelationships(TextWriter writer)
	{
		writer.Write($"<Relationships xmlns=\"{ExcelTextWorkbook.PackageRelationships}\">");
		writer.Write($"<Relationship Id=\"rId1\" Type=\"{ExcelTextWorkbook.Relationships}/table\" Target=\"../tables/table1.xml\"/>");
		writer.Write("</Relationships>");
	}

	/// <summary>
	/// Writes the table part: the table over the keys and the fields, its filter, its columns (the key's in General, the
	/// fields' with the text format's differential format), and Excel's default table style.
	/// </summary>
	/// <param name="writer">The entry's writer.</param>
	/// <param name="dataRows">The number of data rows.</param>
	private static void WriteTable(TextWriter writer, int dataRows)
	{
		var area = ExcelReport.TableArea(dataRows);
		var fields = ExcelReport.Fields.Length;

		writer.Write($"<table xmlns=\"{ExcelTextWorkbook.Main}\" id=\"1\" name=\"{ExcelReport.TableName}\" displayName=\"{ExcelReport.TableName}\"");
		writer.Write($" ref=\"{area}\" totalsRowShown=\"0\" headerRowDxfId=\"0\">");
		writer.Write($"<autoFilter ref=\"{area}\"/>");
		writer.Write($"<tableColumns count=\"{ExcelTextWorkbook.Number(fields + 1)}\">");
		writer.Write($"<tableColumn id=\"1\" name=\"{ExcelReport.KeyHeader}\"/>");
		for (var f = 0; f < fields; f++)
		{
			var id = ExcelTextWorkbook.Number(f + 2);
			var format = ExcelTextWorkbook.Number(fields - f);
			writer.Write($"<tableColumn id=\"{id}\" name=\"{ExcelReport.Fields[f].Name}\" dataDxfId=\"{format}\"/>");
		}

		writer.Write("</tableColumns>");
		writer.Write("<tableStyleInfo name=\"TableStyleMedium2\" showFirstColumn=\"0\" showLastColumn=\"0\" showRowStripes=\"1\" showColumnStripes=\"0\"/>");
		writer.Write("</table>");
	}
}
