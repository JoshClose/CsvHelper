using System.Globalization;
using System.IO.Compression;

namespace CsvHelper.Benchmarks;

/// <summary>
/// Writes rows of typed values as a workbook of one sheet, in the shape Excel saves a sheet whose values were entered
/// with their types: text, numbers, booleans, and numbers in a number format, such as dates.
/// </summary>
/// <remarks>
/// <para>
/// The package is <see cref="ExcelTextWorkbook"/>'s: the same parts in Excel's order, the same sheet around the cells
/// (<c>dimension</c>, <c>spans</c> per block of 16 rows, <c>x14ac:dyDescent</c>), and shared strings in the order they
/// are first seen.
/// </para>
/// <para>
/// The cells are as Excel writes them:
/// </para>
/// <list type="bullet">
/// <item>a <see cref="string"/> is a shared string (<c>t="s"</c>), and an empty one has no cell;</item>
/// <item>a <see cref="double"/> is a number, written as Excel writes it as far as its rule is known (see
/// <see cref="Number"/>);</item>
/// <item>a <see cref="bool"/> is a boolean (<c>t="b"</c>, <c>1</c> or <c>0</c>);</item>
/// <item><see langword="null"/> has no cell.</item>
/// </list>
/// <para>
/// A column's number format applies to its cells below row 1, the header, as when a range of values is formatted. Each
/// distinct format is a custom format (<c>numFmtId</c> 164 on) with a cell format of its own, in the order the columns
/// first use them; a cell in General has no <c>s</c>.
/// </para>
/// <para>
/// It writes the values the benchmark data has. A number that isn't finite, a value of another type, and text
/// <see cref="ExcelTextWorkbook"/> doesn't write throw <see cref="NotSupportedException"/>.
/// </para>
/// </remarks>
internal static class ExcelTypedWorkbook
{
	/// <summary>The id of the first custom number format, after the built-in ones.</summary>
	private const int FirstCustomFormat = 164;

	/// <summary>Writes the workbook.</summary>
	/// <param name="stream">The stream the package is written to. It is left open.</param>
	/// <param name="sheetName">The sheet's name. It is written as is, so it must not need escaping.</param>
	/// <param name="rows">The rows, from row 1. Value i is column i: a string, a double, a bool, or null for no cell.</param>
	/// <param name="formats">
	/// The number format code of each column's cells below row 1, as Excel stores it (escapes included), or
	/// <see langword="null"/> for General. Columns past its end are General.
	/// </param>
	/// <param name="level">The compression of every entry.</param>
	/// <exception cref="NotSupportedException">A value is of a type, or has a form, this writer doesn't write.</exception>
	public static void Write(Stream stream, string sheetName, IReadOnlyList<object?[]> rows, IReadOnlyList<string?> formats, CompressionLevel level)
	{
		var shared = new ExcelTextWorkbook.SharedStrings();
		var codes = new List<string>();
		var styles = new int[formats.Count];
		for (var i = 0; i < formats.Count; i++)
		{
			var code = formats[i];
			if (code != null)
			{
				var index = codes.IndexOf(code);
				if (index < 0)
				{
					index = codes.Count;
					codes.Add(code);
				}

				styles[i] = index + 1;
			}
		}

		using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

		ExcelTextWorkbook.WriteEntry(archive, "[Content_Types].xml", level, writer => ExcelTextWorkbook.WriteContentTypes(writer, true));
		ExcelTextWorkbook.WriteEntry(archive, "_rels/.rels", level, ExcelTextWorkbook.WritePackageRelationships);
		ExcelTextWorkbook.WriteEntry(archive, "xl/workbook.xml", level, writer => ExcelTextWorkbook.WriteWorkbook(writer, sheetName));
		ExcelTextWorkbook.WriteEntry(archive, "xl/_rels/workbook.xml.rels", level, writer => ExcelTextWorkbook.WriteWorkbookRelationships(writer, true));
		ExcelTextWorkbook.WriteEntry(archive, "xl/worksheets/sheet1.xml", level, writer => WriteSheet(writer, rows, styles, shared));
		ExcelTextWorkbook.WriteEntry(archive, "xl/styles.xml", level, writer => WriteStyles(writer, codes));
		ExcelTextWorkbook.WriteEntry(archive, "xl/sharedStrings.xml", level, writer => ExcelTextWorkbook.WriteSharedStrings(writer, shared));
	}

	/// <summary>Formats a number as Excel writes it in a cell, as far as Excel's rule is known.</summary>
	/// <param name="value">The number.</param>
	/// <returns>Its text.</returns>
	/// <remarks>
	/// <para>
	/// The digits are 15 significant digits when they read back as the number, and 17 otherwise. Excel sometimes writes 17
	/// where 15 read back (<c>36021.370000000003</c> for 36021.37, but <c>33002.81</c>), by a rule its file didn't show; and
	/// it rounds a tie of the 17th digit away from zero, where .NET rounds it to even. Of 172,368 numbers Excel wrote
	/// through COM, this gives Excel's text for 154,912; the other 17,345 are the first case, and 111 the second.
	/// </para>
	/// <para>
	/// The form is Excel's: an exponent (<c>5.7447142925787315E-2</c>, <c>1E-3</c>, <c>1.5E+20</c>) below 0.01, from 0.01
	/// to 0.1 unless there is one significant digit (<c>0.05</c>), and from 1E+16; otherwise none.
	/// </para>
	/// </remarks>
	/// <exception cref="NotSupportedException">The number isn't finite.</exception>
	public static string Number(double value)
	{
		if (double.IsNaN(value) || double.IsInfinity(value))
		{
			throw new NotSupportedException($"The number {value} isn't finite.");
		}

		if (value == 0)
		{
			return "0";
		}

		var text = value.ToString("E14", CultureInfo.InvariantCulture);
		if (double.Parse(text, CultureInfo.InvariantCulture) != value)
		{
			text = value.ToString("E16", CultureInfo.InvariantCulture);
		}

		var negative = value < 0;
		var start = negative ? 1 : 0;
		var e = text.IndexOf('E');
		var exponent = int.Parse(text.Substring(e + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
		var digits = text.Substring(start, e - start).Replace(".", "").TrimEnd('0');
		var sign = negative ? "-" : "";

		if (exponent >= 16 || exponent <= -3 || (exponent == -2 && digits.Length > 1))
		{
			var mantissa = digits.Length == 1 ? digits : $"{digits[0]}.{digits.Substring(1)}";

			return $"{sign}{mantissa}E{(exponent < 0 ? "-" : "+")}{Math.Abs(exponent).ToString(CultureInfo.InvariantCulture)}";
		}

		if (exponent < 0)
		{
			return $"{sign}0.{new string('0', -exponent - 1)}{digits}";
		}

		if (digits.Length <= exponent + 1)
		{
			return sign + digits + new string('0', exponent + 1 - digits.Length);
		}

		return $"{sign}{digits.Substring(0, exponent + 1)}.{digits.Substring(exponent + 1)}";
	}

	/// <summary>
	/// Writes the styles: Excel's default cell format, then a cell format for each custom number format, which
	/// <c>numFmts</c> defines.
	/// </summary>
	/// <param name="writer">The entry's writer.</param>
	/// <param name="codes">The custom formats' codes, by cell format, from 1.</param>
	private static void WriteStyles(TextWriter writer, List<string> codes)
	{
		writer.Write($"<styleSheet xmlns=\"{ExcelTextWorkbook.Main}\">");

		if (codes.Count > 0)
		{
			writer.Write($"<numFmts count=\"{ExcelTextWorkbook.Number(codes.Count)}\">");

			for (var i = 0; i < codes.Count; i++)
			{
				writer.Write($"<numFmt numFmtId=\"{ExcelTextWorkbook.Number(FirstCustomFormat + i)}\" formatCode=\"");
				writer.Write(Attribute(codes[i]));
				writer.Write("\"/>");
			}

			writer.Write("</numFmts>");
		}

		writer.Write("<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Aptos Narrow\"/><family val=\"2\"/></font></fonts>");
		writer.Write("<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>");
		writer.Write("<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>");
		writer.Write("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");
		writer.Write($"<cellXfs count=\"{ExcelTextWorkbook.Number(codes.Count + 1)}\">");
		writer.Write("<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>");

		for (var i = 0; i < codes.Count; i++)
		{
			writer.Write($"<xf numFmtId=\"{ExcelTextWorkbook.Number(FirstCustomFormat + i)}\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>");
		}

		writer.Write("</cellXfs>");
		writer.Write("<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>");
		writer.Write("</styleSheet>");
	}

	/// <summary>Writes the sheet, adding its strings to the shared-strings table.</summary>
	/// <param name="writer">The entry's writer.</param>
	/// <param name="rows">The rows, from row 1.</param>
	/// <param name="styles">The cell format of each column's cells below row 1, 0 for General.</param>
	/// <param name="shared">The shared-strings table.</param>
	private static void WriteSheet(TextWriter writer, IReadOnlyList<object?[]> rows, int[] styles, ExcelTextWorkbook.SharedStrings shared)
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
		var attributes = new string[columns];
		for (var i = 0; i < names.Length; i++)
		{
			names[i] = ExcelTextWorkbook.ColumnName(i);
			attributes[i] = i < styles.Length && styles[i] > 0 ? $"\" s=\"{ExcelTextWorkbook.Number(styles[i])}" : "";
		}

		writer.Write($"<worksheet xmlns=\"{ExcelTextWorkbook.Main}\" xmlns:r=\"{ExcelTextWorkbook.Relationships}\"");
		writer.Write(" xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" mc:Ignorable=\"x14ac\"");
		writer.Write(" xmlns:x14ac=\"http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac\">");

		var dimension = lastRow == 0
			? "A1"
			: $"{names[firstColumn - 1]}{ExcelTextWorkbook.Number(firstRow)}:{names[lastColumn - 1]}{ExcelTextWorkbook.Number(lastRow)}";
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
				if (i % ExcelTextWorkbook.SpanBlock == 0)
				{
					spans = BlockSpans(rows, i);
				}

				var row = rows[i];
				if (!Span(row, out _, out _))
				{
					continue;
				}

				var number = ExcelTextWorkbook.Number(i + 1);
				writer.Write($"<row r=\"{number}\" spans=\"{spans}\" x14ac:dyDescent=\"0.35\">");

				for (var c = 0; c < row.Length; c++)
				{
					var value = row[c];
					if (!HasValue(value))
					{
						continue;
					}

					writer.Write("<c r=\"");
					writer.Write(names[c]);
					writer.Write(number);

					if (i > 0)
					{
						writer.Write(attributes[c]);
					}

					switch (value)
					{
						case string text:
							writer.Write("\" t=\"s\"><v>");
							writer.Write(ExcelTextWorkbook.Number(shared.IndexOf(text)));

							break;
						case double d:
							writer.Write("\"><v>");
							writer.Write(Number(d));

							break;
						case bool b:
							writer.Write("\" t=\"b\"><v>");
							writer.Write(b ? "1" : "0");

							break;
						default:
							throw new NotSupportedException($"A value of type {value!.GetType()} isn't written.");
					}

					writer.Write("</v></c>");
				}

				writer.Write("</row>");
			}

			writer.Write("</sheetData>");
		}

		writer.Write("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>");
		writer.Write("</worksheet>");
	}

	/// <summary>Escapes a format code for an attribute: <c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c> and <c>"</c>.</summary>
	/// <param name="value">The code.</param>
	/// <returns>The escaped code.</returns>
	private static string Attribute(string value) =>
		value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

	/// <summary>Returns whether a value makes a cell: anything but null and the empty string.</summary>
	/// <param name="value">The value.</param>
	/// <returns>Whether it has a cell.</returns>
	private static bool HasValue(object? value) => value != null && !(value is string text && text.Length == 0);

	/// <summary>Returns the <c>spans</c> of the block of rows that starts at an index: its first and last columns.</summary>
	/// <param name="rows">The rows.</param>
	/// <param name="start">The index of the block's first row.</param>
	/// <returns>The spans, such as <c>1:12</c>, or an empty string if no row of the block has a value.</returns>
	private static string BlockSpans(IReadOnlyList<object?[]> rows, int start)
	{
		var first = int.MaxValue;
		var last = 0;
		var end = Math.Min(start + ExcelTextWorkbook.SpanBlock, rows.Count);
		for (var i = start; i < end; i++)
		{
			if (Span(rows[i], out var rowFirst, out var rowLast))
			{
				first = Math.Min(first, rowFirst);
				last = Math.Max(last, rowLast);
			}
		}

		return last == 0 ? "" : $"{ExcelTextWorkbook.Number(first)}:{ExcelTextWorkbook.Number(last)}";
	}

	/// <summary>Finds a row's first and last columns with a value, 1-based.</summary>
	/// <param name="row">The row.</param>
	/// <param name="first">The first column with a value, or 0.</param>
	/// <param name="last">The last column with a value, or 0.</param>
	/// <returns>Whether the row has a value.</returns>
	private static bool Span(object?[] row, out int first, out int last)
	{
		first = 0;
		last = 0;
		for (var c = 0; c < row.Length; c++)
		{
			if (HasValue(row[c]))
			{
				first = first == 0 ? c + 1 : first;
				last = c + 1;
			}
		}

		return last > 0;
	}
}
