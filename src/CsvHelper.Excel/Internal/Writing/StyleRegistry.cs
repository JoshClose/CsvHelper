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

/// <summary>The fonts of the writer's cell formats: Calibri, which every Excel since 2007 has.</summary>
internal enum CellFont
{
	/// <summary>Calibri 11, the workbook's default.</summary>
	Regular,

	/// <summary>Bold Calibri 11: a header's.</summary>
	Bold,

	/// <summary>Bold Calibri 14: a title's.</summary>
	Title,

	/// <summary>
	/// Calibri 11, underlined, in Excel's link blue (<c>#0563C1</c>): a link's, as Excel's built-in <c>Hyperlink</c> cell
	/// style has it, which its cell formats are in.
	/// </summary>
	Link,
}

/// <summary>A cell format: a number format, a font and whether text wraps.</summary>
/// <param name="NumberFormatId">The number format's id: 0 for General, a built-in format's, or a custom one's from 164.</param>
/// <param name="Font">The font.</param>
/// <param name="Wrap">Whether text wraps at the column's width.</param>
internal readonly record struct CellXf(int NumberFormatId, CellFont Font, bool Wrap);

/// <summary>
/// The workbook's cell formats: an index (a cell's <c>s</c>) for each <see cref="CellXf"/>, handed out in the order
/// they are first used, after the default cell format 0 (General, in Calibri 11). Number format codes that are one of
/// Excel's built-in formats get its id, as Excel saves them; the others get ids from 164 on, in <c>numFmts</c>. Fonts get
/// ids in the order they are first used, after Calibri 11, so that a workbook has only the fonts its cells use. Formats
/// that a discarded record added stay: Excel opens a workbook with cell formats no cell uses. A link's cell formats are
/// in Excel's built-in <c>Hyperlink</c> cell style, whose entries come with the first: Excel shows such a cell's style as
/// <c>Hyperlink</c> (checked on 2026-11-08).
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

	// The id of each number format code used, by its code.
	private readonly Dictionary<string, int> numberFormatIds = new(StringComparer.Ordinal);
	// The custom formats' codes: the first has id 164.
	private readonly List<string> customCodes = [];
	// The cell formats, the default one first, and each one's index.
	private readonly List<CellXf> formats = [default];
	private readonly Dictionary<CellXf, int> indexes = new() { [default] = 0 };
	// The cell format of each fixed format, 0 until it is first used.
	private readonly int[] fixedIndexes = new int[FixedCodes.Length];
	// The fonts, in the order of their ids; and each font's id, by CellFont, -1 until it is first used.
	private readonly List<CellFont> fonts = [CellFont.Regular];
	private readonly int[] fontIds = [0, -1, -1, -1];

	/// <summary>The number of cell formats, the default one included.</summary>
	public int Count => formats.Count;

	/// <summary>A cell format, by its index.</summary>
	/// <param name="index">The index.</param>
	/// <returns>The cell format.</returns>
	public CellXf this[int index] => formats[index];

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

	/// <summary>The cell format of a number format code, in Calibri 11 without wrap, added when it is first used.</summary>
	/// <param name="code">The code, which Excel opens (see Phase 4's <c>FormatCheck</c>).</param>
	/// <returns>Its index: 0 for General (in any case) or an empty code.</returns>
	public int IndexOf(string code) => IndexOf(new CellXf(NumberFormatId(code), CellFont.Regular, false));

	/// <summary>The index of a cell format, added when it is first used.</summary>
	/// <param name="format">The cell format.</param>
	/// <returns>Its index.</returns>
	public int IndexOf(CellXf format)
	{
		if (indexes.TryGetValue(format, out var index))
		{
			return index;
		}

		if (fontIds[(int)format.Font] < 0)
		{
			fontIds[(int)format.Font] = fonts.Count;
			fonts.Add(format.Font);
		}

		index = formats.Count;
		formats.Add(format);
		indexes.Add(format, index);

		return index;
	}

	/// <summary>The index of a cell format with another font: a header's or a title's cell, in its own number format.</summary>
	/// <param name="index">The cell format's index.</param>
	/// <param name="font">The font.</param>
	/// <returns>The index of the cell format with that font.</returns>
	public int WithFont(int index, CellFont font) => IndexOf(formats[index] with { Font = font });

	/// <summary>The id of a number format code, a custom one added when it is first used.</summary>
	/// <param name="code">The code, which Excel opens.</param>
	/// <returns>Its id: 0 for General (in any case) or an empty code, a built-in format's, or a custom one's from 164.</returns>
	public int NumberFormatId(string code)
	{
		if (code.Length == 0 || string.Equals(code, "General", StringComparison.OrdinalIgnoreCase))
		{
			return 0;
		}

		if (numberFormatIds.TryGetValue(code, out var id))
		{
			return id;
		}

		id = BuiltInId(code);
		if (id < 0)
		{
			id = 164 + customCodes.Count;
			customCodes.Add(code);
		}

		numberFormatIds.Add(code, id);

		return id;
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

	/// <summary>Writes the <c>fonts</c>: Calibri 11, then the others the cell formats use, in the order of their ids.</summary>
	/// <param name="xml">The buffer.</param>
	public void WriteFonts(XmlOutput xml)
	{
		xml.Append("<fonts count=\""u8);
		xml.AppendNumber(fonts.Count);
		xml.Append("\">"u8);
		foreach (var font in fonts)
		{
			xml.Append(font switch
			{
				CellFont.Bold => "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font>"u8,
				CellFont.Title => "<font><b/><sz val=\"14\"/><name val=\"Calibri\"/><family val=\"2\"/></font>"u8,
				CellFont.Link => "<font><u/><sz val=\"11\"/><color rgb=\"FF0563C1\"/><name val=\"Calibri\"/><family val=\"2\"/></font>"u8,
				_ => "<font><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font>"u8,
			});
		}

		xml.Append("</fonts>"u8);
	}

	/// <summary>
	/// Writes the cell styles' formats, <c>cellStyleXfs</c>: Normal's, and, once a link's cell format is used,
	/// <c>Hyperlink</c>'s, in the link's font, as Excel writes it.
	/// </summary>
	/// <param name="xml">The buffer.</param>
	public void WriteCellStyleFormats(XmlOutput xml)
	{
		var link = fontIds[(int)CellFont.Link];
		if (link < 0)
		{
			xml.Append("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"u8);

			return;
		}

		xml.Append("<cellStyleXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/><xf numFmtId=\"0\" fontId=\""u8);
		xml.AppendNumber(link);
		xml.Append("\" fillId=\"0\" borderId=\"0\" applyNumberFormat=\"0\" applyFill=\"0\" applyBorder=\"0\" applyAlignment=\"0\" applyProtection=\"0\"/></cellStyleXfs>"u8);
	}

	/// <summary>Writes the cell styles, <c>cellStyles</c>: Normal, and <c>Hyperlink</c> (built-in 8) once a link's cell format is used.</summary>
	/// <param name="xml">The buffer.</param>
	public void WriteCellStyles(XmlOutput xml) => xml.Append(fontIds[(int)CellFont.Link] < 0
		? "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>"u8
		: "<cellStyles count=\"2\"><cellStyle name=\"Hyperlink\" xfId=\"1\" builtinId=\"8\"/><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>"u8);

	/// <summary>
	/// Writes the cell formats' <c>cellXfs</c>: the default one, then the others in the order of their indexes, each with
	/// the <c>apply</c> flags of what it sets, as Excel writes them. A link's is in the <c>Hyperlink</c> cell style.
	/// </summary>
	/// <param name="xml">The buffer.</param>
	public void WriteCellFormats(XmlOutput xml)
	{
		xml.Append("<cellXfs count=\""u8);
		xml.AppendNumber(Count);
		xml.Append("\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>"u8);
		for (var i = 1; i < formats.Count; i++)
		{
			var format = formats[i];
			xml.Append("<xf numFmtId=\""u8);
			xml.AppendNumber(format.NumberFormatId);
			xml.Append("\" fontId=\""u8);
			xml.AppendNumber(fontIds[(int)format.Font]);
			xml.Append(format.Font == CellFont.Link ? "\" fillId=\"0\" borderId=\"0\" xfId=\"1\""u8 : "\" fillId=\"0\" borderId=\"0\" xfId=\"0\""u8);
			if (format.NumberFormatId != 0)
			{
				xml.Append(" applyNumberFormat=\"1\""u8);
			}

			if (format.Font != CellFont.Regular)
			{
				xml.Append(" applyFont=\"1\""u8);
			}

			xml.Append(format.Wrap ? " applyAlignment=\"1\"><alignment wrapText=\"1\"/></xf>"u8 : "/>"u8);
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
