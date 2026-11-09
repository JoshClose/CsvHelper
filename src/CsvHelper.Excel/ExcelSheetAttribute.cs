using System;

namespace CsvHelper.Excel;

/// <summary>
/// How a written sheet of a record type's records looks, as <see cref="ExcelSheetOptions"/> says, for the sheets of
/// <see cref="ExcelWorkbookWriter.AddSheet{T}(string, ExcelSheetOptions)"/> and
/// <see cref="ExcelWorkbookWriter.WriteSheet{T}(string, System.Collections.Generic.IEnumerable{T}, ExcelSheetOptions)"/>
/// added without options of their own. Its settings are taken whole, in place of
/// <see cref="ExcelWriterOptions.Sheets"/>. Read by reflection, once per type, from the type or the nearest base type that
/// has one.
/// </summary>
/// <remarks>
/// It has <see cref="ExcelSheetOptions"/>' settings but <see cref="ExcelSheetOptions.Columns"/>, which its members'
/// <see cref="ExcelColumnAttribute"/>s give, and a table's as <see cref="Table"/>, <see cref="TableName"/> and
/// <see cref="TableStyle"/>, with banded rows and filter buttons. A setting Excel wouldn't open throws when the sheet is
/// added. A sheet's name is given when it is added, so the attribute has none.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ExcelSheetAttribute : Attribute
{
	/// <summary>A title above the header (<see cref="ExcelSheetOptions.Title"/>), or <see langword="null"/>, the default, for none.</summary>
	public string? Title { get; set; }

	/// <summary>Whether the header's cells are bold (<see cref="ExcelSheetOptions.BoldHeader"/>). On by default.</summary>
	public bool BoldHeader { get; set; } = true;

	/// <summary>Whether the rows through the header stay in view as the sheet scrolls (<see cref="ExcelSheetOptions.FreezeHeader"/>). On by default.</summary>
	public bool FreezeHeader { get; set; } = true;

	/// <summary>How many columns from <c>A</c> stay in view as the sheet scrolls sideways (<see cref="ExcelSheetOptions.FreezeColumns"/>). 0 by default.</summary>
	public int FreezeColumns { get; set; }

	/// <summary>Whether the header has filter buttons (<see cref="ExcelSheetOptions.AutoFilter"/>). Off by default.</summary>
	public bool AutoFilter { get; set; }

	/// <summary>Whether the header and the rows below it are an Excel table (<see cref="ExcelSheetOptions.Table"/>). Off by default.</summary>
	public bool Table { get; set; }

	/// <summary>
	/// The table's name (<see cref="ExcelTableOptions.Name"/>), or <see langword="null"/>, the default, for the sheet's name
	/// made into one. Only with <see cref="Table"/>.
	/// </summary>
	public string? TableName { get; set; }

	/// <summary>
	/// The table's style (<see cref="ExcelTableOptions.Style"/>): one of Excel's built-in table styles, or
	/// <see langword="null"/> for none. <c>TableStyleMedium2</c> by default.
	/// </summary>
	public string? TableStyle { get; set; } = "TableStyleMedium2";

	/// <summary>How many records after the header auto-fit measures (<see cref="ExcelSheetOptions.AutoFit"/>): 100 by default; 0 for none.</summary>
	public int AutoFit { get; set; } = 100;

	/// <summary>The widest auto-fit makes a column, in characters (<see cref="ExcelSheetOptions.MaxAutoFitWidth"/>): 60 by default.</summary>
	public double MaxAutoFitWidth { get; set; } = 60;

	/// <summary>The attribute's options.</summary>
	/// <returns>The options.</returns>
	internal ExcelSheetOptions ToOptions() => new()
	{
		Title = Title,
		BoldHeader = BoldHeader,
		FreezeHeader = FreezeHeader,
		FreezeColumns = FreezeColumns,
		AutoFilter = AutoFilter,
		Table = Table ? new ExcelTableOptions { Name = TableName, Style = TableStyle } : null,
		AutoFit = AutoFit,
		MaxAutoFitWidth = MaxAutoFitWidth,
	};

	/// <summary>What is wrong with the attribute's settings that its options can't say, if anything.</summary>
	/// <returns>What is wrong, or <see langword="null"/>.</returns>
	internal string? Problem() =>
		!Table && TableName is not null ? $"TableName is \"{TableName}\", and Table is off: set Table = true for a table." : null;
}
