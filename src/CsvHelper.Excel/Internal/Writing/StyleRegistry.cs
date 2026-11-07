using System;
using System.Collections.Generic;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>The number formats the writer gives typed cells of its own, by what each value holds.</summary>
internal enum FixedFormat
{
	/// <summary><c>yyyy-mm-dd</c>: a date at midnight.</summary>
	Date,

	/// <summary><c>yyyy-mm-dd hh:mm:ss</c>: a date and time in whole seconds.</summary>
	DateTime,

	/// <summary><c>yyyy-mm-dd hh:mm:ss.000</c>: a date and time with milliseconds.</summary>
	DateTimeMilliseconds,

	/// <summary><c>hh:mm:ss</c>: a time of day in whole seconds.</summary>
	Time,

	/// <summary><c>hh:mm:ss.000</c>: a time of day with milliseconds.</summary>
	TimeMilliseconds,

	/// <summary><c>[h]:mm:ss</c>, built-in format 46: a duration in whole seconds.</summary>
	Duration,

	/// <summary><c>[h]:mm:ss.000</c>: a duration with milliseconds.</summary>
	DurationMilliseconds,
}

/// <summary>
/// The workbook's cell formats: an index (a cell's <c>s</c>) for each number format code, handed out in the order codes
/// are first used, after the default cell format 0 (General). Codes that are one of Excel's built-in formats get its id,
/// as Excel saves them; the others get ids from 164 on, in <c>numFmts</c>. Formats that a discarded record added stay:
/// Excel opens a workbook with cell formats no cell uses.
/// </summary>
internal sealed class StyleRegistry
{
	/// <summary>The codes of <see cref="FixedFormat"/>'s formats, in its order.</summary>
	private static readonly string[] FixedCodes =
	[
		"yyyy-mm-dd",
		"yyyy-mm-dd hh:mm:ss",
		"yyyy-mm-dd hh:mm:ss.000",
		"hh:mm:ss",
		"hh:mm:ss.000",
		"[h]:mm:ss",
		"[h]:mm:ss.000",
	];

	private readonly Dictionary<string, int> indexes = new(StringComparer.Ordinal);
	// The number format id of each cell format after the default one.
	private readonly List<int> formatIds = [];
	// The custom formats' codes: the first has id 164.
	private readonly List<string> customCodes = [];
	// The cell format of each fixed format, 0 until it is first used.
	private readonly int[] fixedIndexes = new int[FixedCodes.Length];

	/// <summary>The number of cell formats, the default one included.</summary>
	public int Count => formatIds.Count + 1;

	/// <summary>The cell format of one of the writer's own number formats.</summary>
	/// <param name="format">The format.</param>
	/// <returns>Its index.</returns>
	public int IndexOf(FixedFormat format)
	{
		var index = fixedIndexes[(int)format];
		if (index == 0)
		{
			index = fixedIndexes[(int)format] = IndexOf(FixedCodes[(int)format]);
		}

		return index;
	}

	/// <summary>The cell format of a number format code, added when it is first used.</summary>
	/// <param name="code">The code, which Excel opens (see Phase 4's <c>FormatCheck</c>).</param>
	/// <returns>Its index: 0 for General (in any case) or an empty code.</returns>
	public int IndexOf(string code)
	{
		if (code.Length == 0 || string.Equals(code, "General", StringComparison.OrdinalIgnoreCase))
		{
			return 0;
		}

		if (indexes.TryGetValue(code, out var index))
		{
			return index;
		}

		var id = BuiltInId(code);
		if (id < 0)
		{
			id = 164 + customCodes.Count;
			customCodes.Add(code);
		}

		formatIds.Add(id);
		index = formatIds.Count;
		indexes.Add(code, index);

		return index;
	}

	/// <summary>Writes the custom number formats' <c>numFmts</c>, if there are any, which come first in the styles part.</summary>
	/// <param name="xml">The buffer.</param>
	public void WriteNumberFormats(XmlOutput xml)
	{
		if (customCodes.Count == 0)
		{
			return;
		}

		xml.Append("<numFmts count=\""u8);
		xml.AppendNumber(customCodes.Count);
		xml.Append("\">"u8);
		for (var i = 0; i < customCodes.Count; i++)
		{
			xml.Append("<numFmt numFmtId=\""u8);
			xml.AppendNumber(164 + i);
			xml.Append("\" formatCode=\""u8);
			xml.AppendText(Encoding.UTF8.GetBytes(customCodes[i]), attribute: true);
			xml.Append("\"/>"u8);
		}

		xml.Append("</numFmts>"u8);
	}

	/// <summary>Writes the cell formats' <c>cellXfs</c>: the default one, then one for each number format, in Calibri 11.</summary>
	/// <param name="xml">The buffer.</param>
	public void WriteCellFormats(XmlOutput xml)
	{
		xml.Append("<cellXfs count=\""u8);
		xml.AppendNumber(Count);
		xml.Append("\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>"u8);
		foreach (var id in formatIds)
		{
			xml.Append("<xf numFmtId=\""u8);
			xml.AppendNumber(id);
			xml.Append("\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>"u8);
		}

		xml.Append("</cellXfs>"u8);
	}

	/// <summary>
	/// The id of a built-in format whose code is the same in every locale, as Excel saves a cell in that code: the
	/// numbers' 1 to 4 and 9 to 13, 46 (<c>[h]:mm:ss</c>), 48 and 49 (<c>@</c>). Other dates and times stay custom: ids 14
	/// to 22 show in the reader's locale, not as their codes.
	/// </summary>
	/// <param name="code">The code.</param>
	/// <returns>The id, or -1.</returns>
	private static int BuiltInId(string code) => code switch
	{
		"0" => 1,
		"0.00" => 2,
		"#,##0" => 3,
		"#,##0.00" => 4,
		"0%" => 9,
		"0.00%" => 10,
		"0.00E+00" => 11,
		"# ?/?" => 12,
		"# ??/??" => 13,
		"[h]:mm:ss" => 46,
		"##0.0E+0" => 48,
		"@" => 49,
		_ => -1,
	};
}
