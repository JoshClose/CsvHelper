using System;

namespace CsvHelper.Excel;

/// <summary>
/// How a written sheet shows a member's column: its width, its number format, whether its text wraps, and whether it is
/// hidden; and a dropdown of values and a prompt for what users type into it, as <see cref="ExcelColumnOptions"/> says.
/// Read by reflection, once per type, for the sheets of
/// <see cref="ExcelWorkbookWriter.AddSheet{T}(string, ExcelSheetOptions)"/> and
/// <see cref="ExcelWorkbookWriter.WriteSheet{T}(string, System.Collections.Generic.IEnumerable{T}, ExcelSheetOptions)"/>.
/// </summary>
/// <remarks>
/// <para>
/// It applies to the column the member's record writer writes, by its header name or its index, as
/// <see cref="CsvFormatAttribute"/>s do; to each column of a collection, from its index to its end index; and on a
/// constructor parameter, to the column of the parameter (and the attribute of the property it sets, when the parameter has
/// none, as for <see cref="CsvFormatAttribute"/>). A member that nests a record has its own members' columns, and the
/// attribute on it throws when the sheet is added. So does a width or a format Excel doesn't open.
/// </para>
/// <para>
/// A column's entry in <see cref="ExcelSheetOptions.Columns"/>, by its header name, beats the attribute, whole.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ExcelColumnAttribute : Attribute
{
	/// <summary>
	/// The column's width, as Excel's <c>ColumnWidth</c> counts it: the number of the default font's digits it shows, up to
	/// 255. 0, the default, for none: auto-fit or a date's format sets it. A width past about 254.29 is written as the widest
	/// the file format takes, which Excel shows as about 254.29.
	/// </summary>
	public double Width { get; set; }

	/// <summary>
	/// The column's number format, in Excel's format code (<c>#,##0.00</c>, <c>0.0%</c>), as
	/// <see cref="ExcelColumnOptions.Format"/> says; or <see langword="null"/>, the default, for none.
	/// </summary>
	public string? Format { get; set; }

	/// <summary>Whether the column's text wraps at its width. Off by default.</summary>
	public bool Wrap { get; set; }

	/// <summary>Whether the column is hidden. Off by default.</summary>
	public bool Hidden { get; set; }

	/// <summary>
	/// A dropdown of these values for the column's cells below the header, in place of the validation of the member's type,
	/// as <see cref="ExcelColumnOptions.List"/> says; or <see langword="null"/>, the default, for none.
	/// </summary>
	public string[]? List { get; set; }

	/// <summary>
	/// Help that Excel shows when a cell of the column below the header is selected, as <see cref="ExcelColumnOptions.Prompt"/>
	/// says; or <see langword="null"/>, the default, for none.
	/// </summary>
	public string? Prompt { get; set; }

	/// <summary>The attribute's options.</summary>
	/// <returns>The options, with no width for a width of 0, and a copy of the list.</returns>
	internal ExcelColumnOptions ToOptions() => new()
	{
		Width = Width == 0 ? null : Width,
		Format = Format,
		Wrap = Wrap,
		Hidden = Hidden,
		List = List is null ? null : [.. List],
		Prompt = Prompt,
	};
}
