using System;
using System.Globalization;

namespace CsvHelper.Excel;

/// <summary>
/// A written sheet's Excel table (<see cref="ExcelSheetOptions.Table"/>): the header and the rows below it, with a name, a
/// style, banded rows and filter buttons. Immutable: use <c>with</c> to change a copy.
/// </summary>
/// <remarks>
/// <para>
/// A table's name is unique in the workbook, ignoring case, and is one Excel opens: it starts with a letter, <c>_</c> or
/// <c>\</c>, then has letters, digits, <c>_</c>, <c>.</c>, <c>?</c> and <c>\</c>, at most 255 characters, and it isn't a
/// cell reference (<c>A1</c>, <c>XFD1048576</c>, <c>R1C1</c>), <c>R</c> or <c>C</c> followed by a digit (<c>R2D2</c>),
/// <c>R</c>, <c>C</c>, <c>RC</c>, <c>TRUE</c> or <c>FALSE</c>.
/// </para>
/// <para>
/// A table's header cells are text, unique ignoring case, as Excel's tables need: an empty one is named <c>Column</c> and
/// its number, a number its text, and a repeated one gets a number (<c>Name2</c>), as Excel names them when it makes a
/// table, and the header's cells show those names.
/// </para>
/// </remarks>
public sealed record ExcelTableOptions
{
	/// <summary>The kinds of Excel's built-in table styles, and how many of each there are.</summary>
	private static readonly (string Kind, int Count)[] BuiltInStyles = [("TableStyleLight", 21), ("TableStyleMedium", 28), ("TableStyleDark", 11)];

	/// <summary>
	/// The table's name, which formulas use (<c>=SUM(Orders[Total])</c>), or <see langword="null"/>, the default, for the
	/// sheet's name made into one: <c>_</c> for each character a name can't have, <c>_</c> first when it needs to be
	/// (<c>Q1 Orders</c> gives <c>Q1_Orders</c>, <c>2024</c> gives <c>_2024</c>), and a number after it while the workbook
	/// has a table of that name (<c>Q1_Orders2</c>). A name given that the workbook has, ignoring case, throws when the
	/// sheet is added. The new sheets of <see cref="ExcelRowLimit.NewSheet"/> have the first's name with a number after it
	/// (<c>Orders2</c>, or <c>AB_2</c> where <c>AB2</c> would be a cell reference).
	/// </summary>
	public string? Name { get; init; }

	/// <summary>
	/// The table's style: one of Excel's 60 built-in styles, <c>TableStyleLight1</c> to <c>TableStyleLight21</c>,
	/// <c>TableStyleMedium1</c> to <c>TableStyleMedium28</c> and <c>TableStyleDark1</c> to <c>TableStyleDark11</c>, in any
	/// case; or <see langword="null"/> for none. <c>TableStyleMedium2</c>, Excel's own, by default. Another name throws when
	/// the sheet is added, since Excel would show the table without a style.
	/// </summary>
	public string? Style { get; init; } = "TableStyleMedium2";

	/// <summary>Whether the style's colors band the table's rows. On by default.</summary>
	public bool BandedRows { get; init; } = true;

	/// <summary>Whether the header has filter buttons, which are also the sheet's filter. On by default.</summary>
	public bool FilterButtons { get; init; } = true;

	/// <summary>Whether a style is one of Excel's built-in table styles, in any case.</summary>
	/// <param name="style">The style's name.</param>
	/// <returns>Whether it is.</returns>
	internal static bool IsBuiltInStyle(string style)
	{
		foreach (var (kind, count) in BuiltInStyles)
		{
			if (style.Length > kind.Length && style.StartsWith(kind, StringComparison.OrdinalIgnoreCase))
			{
				var digits = style.Substring(kind.Length);

				return digits[0] != '0' && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 1 && number <= count;
			}
		}

		return false;
	}

	/// <summary>A built-in table style's name as Excel writes it: <c>tablestylemedium2</c> as <c>TableStyleMedium2</c>.</summary>
	/// <param name="style">The style's name, which <see cref="IsBuiltInStyle"/> accepts.</param>
	/// <returns>The name.</returns>
	internal static string BuiltInStyleName(string style)
	{
		foreach (var (kind, _) in BuiltInStyles)
		{
			if (style.StartsWith(kind, StringComparison.OrdinalIgnoreCase))
			{
				return kind + style.Substring(kind.Length);
			}
		}

		return style;
	}

	/// <summary>What is wrong with the options, if anything.</summary>
	/// <returns>What is wrong, to follow "the table" in a message, or <see langword="null"/>.</returns>
	internal string? Problem()
	{
		if (Name is { } name && TableNames.Problem(name) is { } problem)
		{
			return problem;
		}

		if (Style is { } style && !IsBuiltInStyle(style))
		{
			return $"has the style \"{style}\", which isn't one of Excel's built-in table styles (TableStyleLight1 to 21, TableStyleMedium1 to 28, TableStyleDark1 to 11): Excel would show the table without a style.";
		}

		return null;
	}
}
