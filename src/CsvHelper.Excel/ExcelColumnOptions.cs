using System;
using System.Globalization;

namespace CsvHelper.Excel;

/// <summary>
/// How a written sheet shows a column (<see cref="ExcelSheetOptions.Columns"/>, by its header name, or
/// <see cref="ExcelColumnAttribute"/> on its member): its width, its number format, whether its text wraps, and whether it
/// is hidden. Immutable: use <c>with</c> to change a copy.
/// </summary>
public sealed record ExcelColumnOptions
{
	/// <summary>The widest a column can be, in characters: Excel's <c>ColumnWidth</c> takes at most 255.</summary>
	internal const double MaxWidth = 255;

	/// <summary>
	/// The column's width, as Excel's <c>ColumnWidth</c> counts it: the number of the default font's digits it shows, 0 to
	/// 255. It beats auto-fit's width and the width of a date's format. <see langword="null"/>, the default, for none.
	/// </summary>
	public double? Width { get; init; }

	/// <summary>
	/// The column's number format, in Excel's format code (<c>#,##0.00</c>, <c>0.0%</c>, <c>dd/mm/yyyy</c>), for display only:
	/// the cells hold their values as they are. The column's numbers, booleans and text are written in it, and its dates,
	/// times and durations in it rather than in the format of what each holds or of its member's
	/// <see cref="CsvFormatAttribute"/>. It is also the column's default, which cells added in Excel take. A code Excel
	/// doesn't open throws when the sheet is added. <see langword="null"/>, the default, for none.
	/// </summary>
	public string? Format { get; init; }

	/// <summary>
	/// Whether the column's text wraps at its width, in rows as high as their text, which Excel and LibreOffice set when they
	/// open the workbook. It is also the column's default, which cells added in Excel take. Off by default.
	/// </summary>
	public bool Wrap { get; init; }

	/// <summary>Whether the column is hidden. Off by default.</summary>
	public bool Hidden { get; init; }

	/// <summary>What is wrong with the options, if anything.</summary>
	/// <returns>What is wrong, to follow the column's name in a message, or <see langword="null"/>.</returns>
	internal string? Problem()
	{
		if (Width is { } width && !(width is >= 0 and <= MaxWidth))
		{
			return $"has the width {width.ToString(CultureInfo.InvariantCulture)}, and Excel's widths are 0 to 255 characters.";
		}

		if (Format is { } format)
		{
			if (format.Length == 0)
			{
				return "has an empty format: use null for none.";
			}

			if (FormatCheck.Problem(format) is { } problem)
			{
				return $"has a format Excel doesn't open. {problem}";
			}
		}

		return null;
	}
}
