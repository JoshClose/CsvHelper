using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// A written sheet's Excel table (<see cref="ExcelSheetOptions.Table"/>): its columns' names, as Excel names them when it
/// makes a table over a header, and its part (<c>xl/tables/table1.xml</c>), which the sheet's <c>table</c> relationship
/// names. Phase 1's <see cref="TablePart"/> reads them.
/// </summary>
/// <remarks>
/// <para>
/// Excel refuses a table whose header cells aren't text equal to its columns' names, which are unique ignoring case and
/// have at most 255 characters (checked on 2026-11-08). So the header's cells are written as text, and named as Excel
/// names them in <c>ListObjects.Add</c> (checked through COM on 2026-11-10; <c>Csv\_bench\results\phase5\step4</c>,
/// <c>rename.txt</c> and <c>rename2.txt</c>), from left to right, with one number for the whole header that starts at 1
/// and goes up with each name tried:
/// </para>
/// <list type="bullet">
/// <item>An empty cell is <c>Column</c> and the number: <c>[, , A]</c> gives <c>Column1, Column2, A</c>.</item>
/// <item>
/// A name that a column to its left has, ignoring case, gets the number after it, 2 at least: <c>[a, a, b, b]</c> gives
/// <c>a, a2, b, b3</c>, and <c>[a, a, , ]</c> gives <c>a, a2, Column3, Column4</c>.
/// </item>
/// <item>
/// A name tried that a column has (any column's own name, or one given to a column to its left) is passed over, and the
/// next number tried: <c>[Name, Name, Name2]</c> gives <c>Name, Name3, Name2</c>, and <c>[x, x, x2, x2]</c> gives
/// <c>x, x3, x2, x24</c>.
/// </item>
/// <item>A name and its number have at most 253 characters, the name cut to fit, as Excel cuts it.</item>
/// <item>White space counts: <c>Name</c>, <c> Name</c> and <c>Name </c> are three names.</item>
/// </list>
/// <para>
/// Excel's table of a number cell is its text as Excel shows it in General (<c>0.333333333</c>), and of a date its text
/// in its format; a field of the writer's is named by its text as written (<c>0.3333333333333333</c>,
/// <c>2024-01-15T00:00:00.0000000</c>), the header's name in CSV.
/// </para>
/// </remarks>
internal static class TableWriter
{
	/// <summary>The most characters Excel gives a name it makes with a number.</summary>
	private const int MaxNumberedLength = 253;

	/// <summary>
	/// The names of a table's columns from its header's fields, as Excel names them when it makes a table: the fields'
	/// own, but for empty ones and repeats.
	/// </summary>
	/// <param name="header">The header's fields, in column order: at most 255 characters each.</param>
	/// <returns>The names, unique ignoring case (<see cref="SheetNames.Key"/>).</returns>
	public static string[] ColumnNames(IReadOnlyList<string> header)
	{
		var names = new string[header.Count];
		// Every column's own name, and each name given so far; and the names of the columns to the left.
		var taken = new HashSet<string>(System.StringComparer.Ordinal);
		var left = new HashSet<string>(System.StringComparer.Ordinal);
		foreach (var field in header)
		{
			if (field.Length > 0)
			{
				taken.Add(SheetNames.Key(field));
			}
		}

		var number = 1L;
		for (var i = 0; i < header.Count; i++)
		{
			var field = header[i];
			var name = field;
			if (field.Length == 0)
			{
				name = Numbered("Column", number++);
				while (taken.Contains(SheetNames.Key(name)))
				{
					name = Numbered("Column", number++);
				}
			}
			else if (left.Contains(SheetNames.Key(field)))
			{
				if (number < 2)
				{
					number = 2;
				}

				name = Numbered(field, number++);
				while (taken.Contains(SheetNames.Key(name)))
				{
					name = Numbered(field, number++);
				}
			}

			var key = SheetNames.Key(name);
			taken.Add(key);
			left.Add(key);
			names[i] = name;
		}

		return names;
	}

	/// <summary>The part name of a table.</summary>
	/// <param name="id">The table's id in the workbook, from 1.</param>
	/// <returns>The name, such as <c>xl/tables/table1.xml</c>.</returns>
	public static string PartName(int id) => "xl/tables/table" + id.ToString(CultureInfo.InvariantCulture) + ".xml";

	/// <summary>
	/// Writes a table's part, as Excel writes one: its id, name and range; filter buttons over the range when it has them;
	/// its columns; and its style, with banded rows as asked.
	/// </summary>
	/// <param name="xml">The buffer.</param>
	/// <param name="id">The table's id in the workbook, unique, from 1.</param>
	/// <param name="name">The table's name, which <see cref="TableNames.Problem"/> accepts.</param>
	/// <param name="range">The table's cells: its header's row and at least one below.</param>
	/// <param name="columns">The columns' names (<see cref="ColumnNames"/>), one for each of the range's columns.</param>
	/// <param name="table">The table's options.</param>
	public static void Write(XmlOutput xml, int id, string name, ExcelRange range, IReadOnlyList<string> columns, ExcelTableOptions table)
	{
		var reference = Encoding.ASCII.GetBytes(range.Corners());
		var nameBytes = Encoding.UTF8.GetBytes(name);
		xml.Append(PackageParts.Declaration);
		xml.Append("<table xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" id=\""u8);
		xml.AppendNumber(id);
		xml.Append("\" name=\""u8);
		xml.AppendText(nameBytes, attribute: true);
		xml.Append("\" displayName=\""u8);
		xml.AppendText(nameBytes, attribute: true);
		xml.Append("\" ref=\""u8);
		xml.Append(reference);
		xml.Append("\" totalsRowShown=\"0\">"u8);
		if (table.FilterButtons)
		{
			xml.Append("<autoFilter ref=\""u8);
			xml.Append(reference);
			xml.Append("\"/>"u8);
		}

		xml.Append("<tableColumns count=\""u8);
		xml.AppendNumber(columns.Count);
		xml.Append("\">"u8);
		for (var i = 0; i < columns.Count; i++)
		{
			xml.Append("<tableColumn id=\""u8);
			xml.AppendNumber(i + 1);
			xml.Append("\" name=\""u8);
			// A tab or line break is written as an escape, which Excel decodes, as XML would read it back as a space.
			xml.AppendText(Encoding.UTF8.GetBytes(columns[i]), attribute: true);
			xml.Append("\"/>"u8);
		}

		xml.Append("</tableColumns><tableStyleInfo"u8);
		if (table.Style is { } style)
		{
			xml.Append(" name=\""u8);
			xml.Append(Encoding.ASCII.GetBytes(ExcelTableOptions.BuiltInStyleName(style)));
			xml.Append((byte)'"');
		}

		xml.Append(table.BandedRows
			? " showFirstColumn=\"0\" showLastColumn=\"0\" showRowStripes=\"1\" showColumnStripes=\"0\"/></table>"u8
			: " showFirstColumn=\"0\" showLastColumn=\"0\" showRowStripes=\"0\" showColumnStripes=\"0\"/></table>"u8);
	}

	/// <summary>
	/// Writes the relationship of a sheet to its table, in the sheet's relationships part: <c>rId1</c>, to the table's
	/// part. The sheet's links' come after it.
	/// </summary>
	/// <param name="xml">The buffer.</param>
	/// <param name="id">The table's id in the workbook.</param>
	public static void AppendRelationship(XmlOutput xml, int id)
	{
		xml.Append("<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/table\" Target=\"../tables/table"u8);
		xml.AppendNumber(id);
		xml.Append(".xml\"/>"u8);
	}

	/// <summary>A name with a number after it, the name cut, between characters, so that both have at most 253 characters.</summary>
	/// <param name="name">The name.</param>
	/// <param name="number">The number.</param>
	/// <returns>The numbered name.</returns>
	private static string Numbered(string name, long number)
	{
		var digits = number.ToString(CultureInfo.InvariantCulture);
		var room = MaxNumberedLength - digits.Length;
		if (name.Length > room)
		{
			name = name.Substring(0, char.IsHighSurrogate(name[room - 1]) ? room - 1 : room);
		}

		return name + digits;
	}
}
