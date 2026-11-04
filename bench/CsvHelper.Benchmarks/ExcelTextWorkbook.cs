using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace CsvHelper.Benchmarks;

/// <summary>Where the text cells of a workbook that <see cref="ExcelTextWorkbook"/> writes hold their strings.</summary>
public enum ExcelStrings
{
	/// <summary>In the shared-strings table, as Excel writes them: each cell holds the index of its string (<c>t="s"</c>).</summary>
	Shared,

	/// <summary>In the cells themselves (<c>t="inlineStr"</c>), as streaming writers often write them.</summary>
	Inline,
}

/// <summary>
/// Writes rows of text as a workbook of one sheet, in the shape Excel saves a CSV file that it opened with every column as
/// text.
/// </summary>
/// <remarks>
/// <para>
/// The parts are the ones the reading needs: content types, relationships, the workbook, minimal styles, the sheet and the
/// shared strings, in Excel's order, so the shared strings come after the sheet. Excel's theme and document properties are
/// left out.
/// </para>
/// <para>
/// The sheet is written as Excel writes it: a <c>dimension</c>, rows with <c>r</c>, <c>spans</c> (the columns of each
/// block of 16 rows) and <c>x14ac:dyDescent</c>, and cells with <c>r</c> and the text format's style (<c>s="1"</c>). An
/// empty field has no cell, and a row without a value has no row element. Shared strings are kept in the order they are
/// first seen.
/// </para>
/// <para>
/// It writes the text the benchmark data has. Leading and trailing spaces are kept with <c>xml:space="preserve"</c>, and
/// <c>&amp;</c>, <c>&lt;</c> and <c>&gt;</c> are escaped. Control characters, which Excel writes as <c>_xHHHH_</c>, and
/// text that Excel would escape because it looks like such an escape, throw <see cref="NotSupportedException"/>.
/// </para>
/// </remarks>
internal static class ExcelTextWorkbook
{
	/// <summary>The XML declaration Excel starts each part with, and the line break after it.</summary>
	internal const string Declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n";

	/// <summary>The SpreadsheetML namespace.</summary>
	internal const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

	/// <summary>The namespace of relationship ids, and the prefix of the relationship types.</summary>
	internal const string Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

	/// <summary>The namespace of relationship parts.</summary>
	internal const string PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

	/// <summary>The prefix of the SpreadsheetML content types.</summary>
	internal const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.";

	/// <summary>The number of rows that share a <c>spans</c> value, as in Excel's files.</summary>
	internal const int SpanBlock = 16;

	/// <summary>UTF-8 without a byte order mark, as Excel writes its parts.</summary>
	private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

	/// <summary>Writes the workbook.</summary>
	/// <param name="stream">The stream the package is written to. It is left open.</param>
	/// <param name="sheetName">The sheet's name. It is written as is, so it must not need escaping.</param>
	/// <param name="rows">The rows, from row 1. Field i is column i; an empty field has no cell.</param>
	/// <param name="strings">Where the cells' strings go.</param>
	/// <param name="level">The compression of every entry.</param>
	/// <exception cref="NotSupportedException">A field has a character this writer doesn't write.</exception>
	public static void Write(Stream stream, string sheetName, IReadOnlyList<string[]> rows, ExcelStrings strings, CompressionLevel level)
	{
		var shared = strings == ExcelStrings.Shared ? new SharedStrings() : null;

		using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

		WriteEntry(archive, "[Content_Types].xml", level, writer => WriteContentTypes(writer, shared != null));
		WriteEntry(archive, "_rels/.rels", level, WritePackageRelationships);
		WriteEntry(archive, "xl/workbook.xml", level, writer => WriteWorkbook(writer, sheetName));
		WriteEntry(archive, "xl/_rels/workbook.xml.rels", level, writer => WriteWorkbookRelationships(writer, shared != null));
		WriteEntry(archive, "xl/worksheets/sheet1.xml", level, writer => WriteSheet(writer, rows, shared));
		WriteEntry(archive, "xl/styles.xml", level, WriteStyles);

		if (shared != null)
		{
			WriteEntry(archive, "xl/sharedStrings.xml", level, writer => WriteSharedStrings(writer, shared));
		}
	}

	/// <summary>Returns the name of a column: A for 0, Z for 25, AA for 26.</summary>
	/// <param name="index">The column's 0-based index.</param>
	/// <returns>The column's letters.</returns>
	public static string ColumnName(int index)
	{
		var name = "";
		for (var n = index + 1; n > 0; n = (n - 1) / 26)
		{
			name = (char)('A' + (n - 1) % 26) + name;
		}

		return name;
	}

	/// <summary>Writes an entry: the declaration, then the part.</summary>
	/// <param name="archive">The package.</param>
	/// <param name="name">The entry's name.</param>
	/// <param name="level">The entry's compression.</param>
	/// <param name="write">Writes the part after the declaration.</param>
	internal static void WriteEntry(ZipArchive archive, string name, CompressionLevel level, Action<TextWriter> write)
	{
		var entry = archive.CreateEntry(name, level);

		using var writer = new StreamWriter(entry.Open(), Utf8, 1 << 16);

		writer.Write(Declaration);
		write(writer);
	}

	/// <summary>Writes the content types of the parts.</summary>
	/// <param name="writer">The entry's writer.</param>
	/// <param name="shared">Whether the package has a shared-strings part.</param>
	internal static void WriteContentTypes(TextWriter writer, bool shared)
	{
		writer.Write("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
		writer.Write("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
		writer.Write("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
		writer.Write($"<Override PartName=\"/xl/workbook.xml\" ContentType=\"{ContentType}sheet.main+xml\"/>");
		writer.Write($"<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"{ContentType}worksheet+xml\"/>");
		writer.Write($"<Override PartName=\"/xl/styles.xml\" ContentType=\"{ContentType}styles+xml\"/>");

		if (shared)
		{
			writer.Write($"<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"{ContentType}sharedStrings+xml\"/>");
		}

		writer.Write("</Types>");
	}

	/// <summary>Writes the package's relationships: the workbook.</summary>
	/// <param name="writer">The entry's writer.</param>
	internal static void WritePackageRelationships(TextWriter writer)
	{
		writer.Write($"<Relationships xmlns=\"{PackageRelationships}\">");
		writer.Write($"<Relationship Id=\"rId1\" Type=\"{Relationships}/officeDocument\" Target=\"xl/workbook.xml\"/>");
		writer.Write("</Relationships>");
	}

	/// <summary>Writes the workbook part, with its one sheet.</summary>
	/// <param name="writer">The entry's writer.</param>
	/// <param name="sheetName">The sheet's name.</param>
	internal static void WriteWorkbook(TextWriter writer, string sheetName)
	{
		writer.Write($"<workbook xmlns=\"{Main}\" xmlns:r=\"{Relationships}\">");
		writer.Write("<bookViews><workbookView/></bookViews>");
		writer.Write($"<sheets><sheet name=\"{sheetName}\" sheetId=\"1\" r:id=\"rId1\"/></sheets>");
		writer.Write("</workbook>");
	}

	/// <summary>Writes the workbook's relationships: the sheet, the styles, and the shared strings if there are any.</summary>
	/// <param name="writer">The entry's writer.</param>
	/// <param name="shared">Whether the package has a shared-strings part.</param>
	internal static void WriteWorkbookRelationships(TextWriter writer, bool shared)
	{
		writer.Write($"<Relationships xmlns=\"{PackageRelationships}\">");
		writer.Write($"<Relationship Id=\"rId1\" Type=\"{Relationships}/worksheet\" Target=\"worksheets/sheet1.xml\"/>");
		writer.Write($"<Relationship Id=\"rId2\" Type=\"{Relationships}/styles\" Target=\"styles.xml\"/>");

		if (shared)
		{
			writer.Write($"<Relationship Id=\"rId3\" Type=\"{Relationships}/sharedStrings\" Target=\"sharedStrings.xml\"/>");
		}

		writer.Write("</Relationships>");
	}

	/// <summary>Writes the styles: Excel's default cell format, and the text format (<c>@</c>) as format 1.</summary>
	/// <param name="writer">The entry's writer.</param>
	private static void WriteStyles(TextWriter writer)
	{
		writer.Write($"<styleSheet xmlns=\"{Main}\">");
		writer.Write("<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Aptos Narrow\"/><family val=\"2\"/></font></fonts>");
		writer.Write("<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>");
		writer.Write("<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>");
		writer.Write("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");
		writer.Write("<cellXfs count=\"2\">");
		writer.Write("<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>");
		writer.Write("<xf numFmtId=\"49\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>");
		writer.Write("</cellXfs>");
		writer.Write("<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>");
		writer.Write("</styleSheet>");
	}

	/// <summary>Writes the sheet, adding its strings to the shared-strings table if there is one.</summary>
	/// <param name="writer">The entry's writer.</param>
	/// <param name="rows">The rows, from row 1.</param>
	/// <param name="shared">The shared-strings table, or <see langword="null"/> to write the strings in the cells.</param>
	private static void WriteSheet(TextWriter writer, IReadOnlyList<string[]> rows, SharedStrings? shared)
	{
		var columns = 0;
		var firstRow = 0;
		var lastRow = 0;
		var firstColumn = int.MaxValue;
		var lastColumn = 0;
		for (var i = 0; i < rows.Count; i++)
		{
			var row = rows[i];
			columns = Math.Max(columns, row.Length);

			if (Span(row, out var first, out var last))
			{
				firstRow = firstRow == 0 ? i + 1 : firstRow;
				lastRow = i + 1;
				firstColumn = Math.Min(firstColumn, first);
				lastColumn = Math.Max(lastColumn, last);
			}
		}

		var names = new string[columns];
		for (var i = 0; i < names.Length; i++)
		{
			names[i] = ColumnName(i);
		}

		writer.Write($"<worksheet xmlns=\"{Main}\" xmlns:r=\"{Relationships}\"");
		writer.Write(" xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" mc:Ignorable=\"x14ac\"");
		writer.Write(" xmlns:x14ac=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac\">");

		var dimension = lastRow == 0
			? "A1"
			: $"{names[firstColumn - 1]}{Number(firstRow)}:{names[lastColumn - 1]}{Number(lastRow)}";
		writer.Write($"<dimension ref=\"{dimension}\"/>");
		writer.Write("<sheetViews><sheetView tabSelected=\"1\" workbookViewId=\"0\"/></sheetViews>");
		writer.Write("<sheetFormatPr defaultRowHeight=\"14.5\" x14ac:dyDescent=\"0.35\"/>");

		if (lastRow == 0)
		{
			writer.Write("<sheetData/>");
		}
		else
		{
			writer.Write("<sheetData>");

			var spans = "";
			for (var i = 0; i < rows.Count; i++)
			{
				if (i % SpanBlock == 0)
				{
					spans = BlockSpans(rows, i);
				}

				var row = rows[i];
				if (!Span(row, out _, out _))
				{
					continue;
				}

				var number = Number(i + 1);
				writer.Write($"<row r=\"{number}\" spans=\"{spans}\" x14ac:dyDescent=\"0.35\">");

				for (var c = 0; c < row.Length; c++)
				{
					var value = row[c];
					if (value.Length == 0)
					{
						continue;
					}

					writer.Write("<c r=\"");
					writer.Write(names[c]);
					writer.Write(number);

					if (shared != null)
					{
						writer.Write("\" s=\"1\" t=\"s\"><v>");
						writer.Write(Number(shared.IndexOf(value)));
						writer.Write("</v></c>");
					}
					else
					{
						writer.Write("\" s=\"1\" t=\"inlineStr\"><is>");
						WriteText(writer, value);
						writer.Write("</is></c>");
					}
				}

				writer.Write("</row>");
			}

			writer.Write("</sheetData>");
		}

		writer.Write("<pageMargins left=\"0.75\" right=\"0.75\" top=\"1\" bottom=\"1\" header=\"0.5\" footer=\"0.5\"/>");
		writer.Write("</worksheet>");
	}

	/// <summary>Writes the shared-strings part.</summary>
	/// <param name="writer">The entry's writer.</param>
	/// <param name="shared">The table the sheet filled.</param>
	internal static void WriteSharedStrings(TextWriter writer, SharedStrings shared)
	{
		writer.Write($"<sst xmlns=\"{Main}\" count=\"{Number(shared.Count)}\" uniqueCount=\"{Number(shared.Strings.Count)}\">");

		foreach (var value in shared.Strings)
		{
			writer.Write("<si>");
			WriteText(writer, value);
			writer.Write("</si>");
		}

		writer.Write("</sst>");
	}

	/// <summary>Writes a <c>t</c> element holding a text, escaped.</summary>
	/// <param name="writer">The entry's writer.</param>
	/// <param name="value">The text, which isn't empty.</param>
	/// <exception cref="NotSupportedException">The text has a character this writer doesn't write.</exception>
	internal static void WriteText(TextWriter writer, string value)
	{
		var preserve = value[0] == ' ' || value[value.Length - 1] == ' ';
		writer.Write(preserve ? "<t xml:space=\"preserve\">" : "<t>");

		var start = 0;
		for (var i = 0; i < value.Length; i++)
		{
			var c = value[i];
			if (c < ' ' || IsEscapeLike(value, i))
			{
				throw new NotSupportedException($"The field \"{value}\" has text that Excel writes as an _xHHHH_ escape.");
			}

			var entity = c switch
			{
				'&' => "&amp;",
				'<' => "&lt;",
				'>' => "&gt;",
				_ => null,
			};

			if (entity != null)
			{
				writer.Write(value.Substring(start, i - start));
				writer.Write(entity);
				start = i + 1;
			}
		}

		writer.Write(value.Substring(start));
		writer.Write("</t>");
	}

	/// <summary>Returns whether the text at an index has the form <c>_xHHHH_</c>, which Excel would escape.</summary>
	/// <param name="value">The text.</param>
	/// <param name="index">The index.</param>
	/// <returns>Whether an escape's form starts there.</returns>
	private static bool IsEscapeLike(string value, int index)
	{
		if (value[index] != '_' || index + 6 >= value.Length || value[index + 1] != 'x' || value[index + 6] != '_')
		{
			return false;
		}

		for (var i = index + 2; i < index + 6; i++)
		{
			if (!Uri.IsHexDigit(value[i]))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>Returns the <c>spans</c> of the block of rows that starts at an index: its first and last columns.</summary>
	/// <param name="rows">The rows.</param>
	/// <param name="start">The index of the block's first row.</param>
	/// <returns>The spans, such as <c>1:25</c>, or an empty string if no row of the block has a value.</returns>
	private static string BlockSpans(IReadOnlyList<string[]> rows, int start)
	{
		var first = int.MaxValue;
		var last = 0;
		var end = Math.Min(start + SpanBlock, rows.Count);
		for (var i = start; i < end; i++)
		{
			if (Span(rows[i], out var rowFirst, out var rowLast))
			{
				first = Math.Min(first, rowFirst);
				last = Math.Max(last, rowLast);
			}
		}

		return last == 0 ? "" : $"{Number(first)}:{Number(last)}";
	}

	/// <summary>Finds a row's first and last columns with a value, 1-based.</summary>
	/// <param name="row">The row.</param>
	/// <param name="first">The first column with a value, or 0.</param>
	/// <param name="last">The last column with a value, or 0.</param>
	/// <returns>Whether the row has a value.</returns>
	private static bool Span(string[] row, out int first, out int last)
	{
		first = 0;
		last = 0;
		for (var c = 0; c < row.Length; c++)
		{
			if (row[c].Length > 0)
			{
				first = first == 0 ? c + 1 : first;
				last = c + 1;
			}
		}

		return last > 0;
	}

	/// <summary>Formats a number for the XML.</summary>
	/// <param name="value">The number.</param>
	/// <returns>Its digits.</returns>
	internal static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

	/// <summary>The shared-strings table: each distinct string once, in the order first seen.</summary>
	internal sealed class SharedStrings
	{
		private readonly Dictionary<string, int> indexes = new(StringComparer.Ordinal);

		/// <summary>Gets the strings, by index.</summary>
		public List<string> Strings { get; } = [];

		/// <summary>Gets the number of cells that refer to a string.</summary>
		public int Count { get; private set; }

		/// <summary>Returns the index of a string, adding it if it's new.</summary>
		/// <param name="value">The string.</param>
		/// <returns>The string's index.</returns>
		public int IndexOf(string value)
		{
			Count++;

			if (!indexes.TryGetValue(value, out var index))
			{
				index = Strings.Count;
				indexes.Add(value, index);
				Strings.Add(value);
			}

			return index;
		}
	}
}
