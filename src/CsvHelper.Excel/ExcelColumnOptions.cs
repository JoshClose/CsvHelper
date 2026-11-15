using System;
using System.Collections.Generic;
using System.Globalization;

namespace CsvHelper.Excel;

/// <summary>
/// How a written sheet shows a column (<see cref="ExcelSheetOptions.Columns"/>, by its header name, or
/// <see cref="ExcelColumnAttribute"/> on its member): its width, its number format, whether its text wraps, and whether it
/// is hidden; and a dropdown of values and a prompt for what users type into it. Immutable: use <c>with</c> to change a
/// copy; a sheet copies a column's list when it is added.
/// </summary>
public sealed record ExcelColumnOptions
{
	/// <summary>The widest a column can be, in characters: Excel's <c>ColumnWidth</c> takes at most 255.</summary>
	internal const double MaxWidth = 255;

	/// <summary>The longest value of a list: Excel matches no longer one, even typed exactly.</summary>
	internal const int MaxListValueLength = 255;

	/// <summary>The longest prompt: Excel repairs a workbook with a longer one by dropping its validation.</summary>
	internal const int MaxPromptLength = 255;

	/// <summary>
	/// The column's width, as Excel's <c>ColumnWidth</c> counts it: the number of the default font's digits it shows, 0 to
	/// 255. It beats auto-fit's width and the width of a date's format. <see langword="null"/>, the default, for none. A
	/// width past about 254.29 is written as the widest the file format takes, which Excel shows as about 254.29.
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

	/// <summary>
	/// A dropdown of these values for the column's cells below the header, which Excel applies to what users type there, in
	/// place of the validation of its member's type (<see cref="ExcelSheetOptions.Validate"/>); on any sheet, validated or
	/// not. Each value has 1 to 255 characters, since Excel doesn't match a longer one; a value repeated is listed once;
	/// at most 1,048,576 values. The values are written in the validation when, joined by commas, they have at most 255
	/// characters and none has a comma, or white space at either end, and Excel matches them in their case; else in a very
	/// hidden sheet after the workbook's other sheets, named <c>Lists</c> (<c>Lists2</c> when a sheet has that name), which
	/// columns with the same list share, and Excel matches them ignoring case. <see langword="null"/>, the default, for
	/// none. In a column of General format, Excel enters a value it reads as a number (<c>00123</c>) as one, which a list of
	/// text doesn't match: give such a column a <see cref="Format"/> of <c>@</c>, as <see cref="ExcelSheetOptions.Validate"/>
	/// gives text members' columns. Reading the sheet back doesn't check its cells against the list: a member's type, or its
	/// <see cref="CsvValidateAttribute"/>, says what it takes.
	/// </summary>
	public IReadOnlyList<string>? List { get; init; }

	/// <summary>
	/// Help that Excel shows when a cell of the column below the header is selected, with the column's header name as its
	/// title: 1 to 255 characters, the most Excel opens, line breaks kept; on any sheet. <see langword="null"/>, the
	/// default, for none.
	/// </summary>
	public string? Prompt { get; init; }

	/// <summary>What is wrong with the options, if anything.</summary>
	/// <returns>What is wrong, to follow the column's name in a message, or <see langword="null"/>.</returns>
	internal string? Problem()
	{
		if (List is { } list)
		{
			if (list.Count == 0)
			{
				return "has an empty list: use null for none.";
			}

			if (list.Count > CellReference.MaxRow)
			{
				return $"has {list.Count.ToString("N0", CultureInfo.InvariantCulture)} values in its list, and the lists sheet holds {CellReference.MaxRow.ToString("N0", CultureInfo.InvariantCulture)}, a column's rows.";
			}

			foreach (var value in list)
			{
				if (value is null)
				{
					return "has a null value in its list.";
				}

				if (value.Length is 0 or > MaxListValueLength)
				{
					return $"has a value of {value.Length.ToString(CultureInfo.InvariantCulture)} characters in its list, and Excel matches values of 1 to {MaxListValueLength.ToString(CultureInfo.InvariantCulture)}.";
				}
			}
		}

		if (Prompt is { } prompt && prompt.Length is 0 or > MaxPromptLength)
		{
			return $"has a prompt of {prompt.Length.ToString(CultureInfo.InvariantCulture)} characters, and Excel opens prompts of 1 to {MaxPromptLength.ToString(CultureInfo.InvariantCulture)}: use null for none.";
		}

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
