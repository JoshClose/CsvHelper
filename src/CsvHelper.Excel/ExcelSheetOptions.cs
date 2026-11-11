using System;
using System.Collections.Generic;
using System.Globalization;

namespace CsvHelper.Excel;

/// <summary>
/// How a written sheet looks: a title above the header, a bold and frozen header, a filter or an Excel table, columns as
/// wide as their text, and each column's width, number format, wrap and visibility. Immutable: use <c>with</c> to change a
/// copy.
/// </summary>
/// <remarks>
/// <para>
/// A sheet's options are those given when it is added; else, for a sheet of records of a type with an
/// <see cref="ExcelSheetAttribute"/>, the attribute's; else <see cref="ExcelWriterOptions.Sheets"/>. Each is taken whole,
/// not merged with the others. Options that Excel wouldn't open throw <see cref="ArgumentException"/> when the sheet is
/// added.
/// </para>
/// <para>
/// The header is the sheet's first record, with <see cref="CsvWriterOptions.HasHeader"/>. Without it there is none:
/// <see cref="BoldHeader"/> and <see cref="FreezeHeader"/> do nothing, and <see cref="AutoFilter"/> and
/// <see cref="Table"/> throw.
/// </para>
/// </remarks>
public sealed record ExcelSheetOptions
{
	/// <summary>The longest title: Excel's longest text.</summary>
	internal const int MaxTitleLength = 32767;

	/// <summary>The most records auto-fit measures: a sheet's rows but the header's.</summary>
	internal const int MaxAutoFit = 1048575;

	/// <summary>The most columns that can be frozen: every column but Excel's last.</summary>
	internal const int MaxFreezeColumns = 16383;

	/// <summary>
	/// The default options: a bold, frozen header, and columns auto-fitted over the header and the first 100 records, at
	/// most 60 characters wide; no title, filter or table.
	/// </summary>
	public static ExcelSheetOptions Default { get; } = new();

	/// <summary>
	/// A title, in row 1 above the header, in bold 14-point text, merged across the header's columns (or the first record's,
	/// without a header). The header and the records follow from row 2; the sheet's <see cref="CsvWriter"/> doesn't see the
	/// title's row, so its first record is still the header. 1 to 32,767 characters, or <see langword="null"/>, the
	/// default, for none. With <see cref="ExcelRowLimit.NewSheet"/>, each new sheet repeats it.
	/// </summary>
	public string? Title { get; init; }

	/// <summary>
	/// Whether the header's cells are bold. On by default. Not in a table with a style, whose style sets its header.
	/// </summary>
	public bool BoldHeader { get; init; } = true;

	/// <summary>
	/// Whether the rows through the header (the title's too) stay in view as the sheet scrolls. On by default.
	/// </summary>
	public bool FreezeHeader { get; init; } = true;

	/// <summary>
	/// How many columns from <c>A</c> stay in view as the sheet scrolls sideways: 0, the default, for none, to 16,383.
	/// </summary>
	public int FreezeColumns { get; init; }

	/// <summary>
	/// Whether the header has filter buttons, over the header's columns from it down to the last row written, as Excel's
	/// filter is, with Excel's hidden <c>_FilterDatabase</c> name for it. Off by default. With a <see cref="Table"/>, the
	/// table's buttons are the filter, since Excel opens no sheet with both. With <see cref="ExcelRowLimit.NewSheet"/>, each
	/// new sheet has its own.
	/// </summary>
	public bool AutoFilter { get; init; }

	/// <summary>
	/// The header and the rows below it as an Excel table, with a name, a style, banded rows and filter buttons; or
	/// <see langword="null"/>, the default, for none. The table spans the header's columns, down to the last row written,
	/// and one row below the header at least. Its header's cells are text, named as Excel names a table's columns: an
	/// empty field is <c>Column</c> and a number, and a name a column to its left has gets a number (<c>Name2</c>), so
	/// that the table's columns' names are unique ignoring case; a field of more than 255 characters throws. No range may be
	/// merged in its columns at or below its header. With <see cref="ExcelRowLimit.NewSheet"/>, each new sheet has its own
	/// table, with a number after the first's name.
	/// </summary>
	public ExcelTableOptions? Table { get; init; }

	/// <summary>
	/// How many records after the header auto-fit measures: the header and these records are held until their widths are
	/// known, and each column is as wide as its widest cell's text among them, as Excel shows it in Calibri 11: the header
	/// in bold, with its filter button when it has one; text on one line, or by its widest line when its column wraps;
	/// numbers in General or in their column's format, dates and times in their formats. A formula's column is at least
	/// Excel's default, since its value isn't known; the title counts nothing. A column can come out narrower than Excel's
	/// default, as with Excel's own AutoFit, and a later record's wider cell isn't measured. 100 by default; 0 for none,
	/// when only the first data record's dates, times and cells in a column's format, and the columns' own widths, set
	/// widths. The sheets that <see cref="ExcelRowLimit.NewSheet"/> starts take the first sheet's widths.
	/// </summary>
	public int AutoFit { get; init; } = 100;

	/// <summary>
	/// The widest auto-fit makes a column for its text, in characters: 1 to 255; 60 by default. Longer text is cut at the
	/// column's edge, or spills into the next cell. Numbers, booleans, dates and times get the width they need, up to 255,
	/// since Excel shows one too narrow as <c>########</c>.
	/// </summary>
	public double MaxAutoFitWidth { get; init; } = 60;

	/// <summary>
	/// The columns' options, by their header names, matched ordinally; or <see langword="null"/>, the default, for none. A
	/// column's options here beat its member's <see cref="ExcelColumnAttribute"/>, whole. Every column of a name gets its
	/// options. A name the header doesn't have throws when the header record ends, which discards it, as any record that
	/// fails. Without <see cref="CsvWriterOptions.HasHeader"/>, the names are those that the record type's record writer
	/// writes in its header.
	/// </summary>
	public IReadOnlyDictionary<string, ExcelColumnOptions>? Columns { get; init; }

	/// <summary>What is wrong with the options, if anything: each setting on its own, and the columns' options.</summary>
	/// <param name="columns">The columns' options to check, which are <see cref="Columns"/> or a copy of them; or <see langword="null"/>.</param>
	/// <returns>What is wrong, or <see langword="null"/>.</returns>
	internal string? Problem(IReadOnlyDictionary<string, ExcelColumnOptions>? columns)
	{
		if (Title is { } title && title.Length is 0 or > MaxTitleLength)
		{
			return $"The title has {title.Length.ToString(CultureInfo.InvariantCulture)} characters, and a title has 1 to {MaxTitleLength.ToString("N0", CultureInfo.InvariantCulture)}: use null for none.";
		}

		if (FreezeColumns is < 0 or > MaxFreezeColumns)
		{
			return $"FreezeColumns is {FreezeColumns.ToString(CultureInfo.InvariantCulture)}, and is 0 to {MaxFreezeColumns.ToString("N0", CultureInfo.InvariantCulture)}.";
		}

		if (AutoFit is < 0 or > MaxAutoFit)
		{
			return $"AutoFit is {AutoFit.ToString(CultureInfo.InvariantCulture)}, and is 0 (none) to {MaxAutoFit.ToString("N0", CultureInfo.InvariantCulture)} records.";
		}

		if (!(MaxAutoFitWidth is >= 1 and <= ExcelColumnOptions.MaxWidth))
		{
			return $"MaxAutoFitWidth is {MaxAutoFitWidth.ToString(CultureInfo.InvariantCulture)}, and is 1 to 255 characters.";
		}

		if (Table?.Problem() is { } table)
		{
			return "The table " + table;
		}

		if (columns is not null)
		{
			foreach (var column in columns)
			{
				if (column.Key is null)
				{
					return "Columns has a null name.";
				}

				if (column.Value is null)
				{
					return $"Columns has no options for the column \"{column.Key}\": leave it out for none.";
				}

				if (column.Value.Problem() is { } problem)
				{
					return $"The column \"{column.Key}\" {problem}";
				}
			}
		}

		return null;
	}

	/// <summary>What is wrong with the options for a sheet with or without a header, if anything.</summary>
	/// <param name="hasHeader">Whether the sheet's first record is its header (<see cref="CsvWriterOptions.HasHeader"/>).</param>
	/// <returns>What is wrong, or <see langword="null"/>.</returns>
	internal string? HeaderProblem(bool hasHeader) =>
		hasHeader ? null :
		AutoFilter ? "AutoFilter needs a header, and CsvWriterOptions.HasHeader is off." :
		Table is not null ? "A table needs a header, and CsvWriterOptions.HasHeader is off." :
		null;
}
