namespace CsvHelper.Excel;

/// <summary>What text a cell reads as (<see cref="ExcelReaderOptions.Text"/>): its stored value's, or the text Excel shows.</summary>
/// <remarks>
/// Only text follows it: <c>GetString</c>, string members, header names, dynamic records' and <c>CsvDataReader</c>'s
/// text, a conversion's fields and an import error's <see cref="ExcelImportError.Value"/>. Typed reads always read the
/// value: a <see cref="decimal"/> member of a cell shown <c>$1,234.50</c> reads 1234.5 either way.
/// </remarks>
public enum ExcelText
{
	/// <summary>
	/// The stored value's text, whatever the cell's number format: a number as stored (<c>1234.5</c>), a date in ISO 8601
	/// (<c>2024-01-15</c>), a duration as <c>[h]:mm:ss</c>, a boolean as <c>TRUE</c> or <c>FALSE</c>, and text as it is.
	/// </summary>
	Raw,

	/// <summary>
	/// The text Excel shows in the cell's number format, as in a column wide enough: <c>$1,234.50</c>, <c>Jan-24</c>,
	/// <c>3 1/7</c>, <c>12.5%</c>, and text through a format's text section (<c>"Note: "@</c>). General shows at most 11
	/// characters, as Excel does (<c>0.333333333</c>).
	/// </summary>
	/// <remarks>
	/// <para>
	/// The text is in <see cref="CsvReaderOptions.FormatProvider"/>: the invariant culture, the default, is Excel's
	/// en-US. Another culture gives the decimal and group separators, the month and day names and AM and PM (where the
	/// format names no language of its own, as <c>[$-407]</c> does), the short date of the built-in dates, the system long
	/// date and long time, and the built-in currency formats. A format's <c>/</c> and <c>:</c> show as written.
	/// </para>
	/// <para>
	/// A <c>*</c> fill shows nothing, as Excel's own CSV export writes it. A value Excel can't show in its format (it fills
	/// the cell with <c>#</c>), such as a negative date, and a format the reader can't read, read as <see cref="Raw"/>
	/// text. Calendars, numeral systems, <c>[DBNum]</c> and eras aren't shown: dates are Gregorian, digits Western, and
	/// <c>g</c> shows nothing.
	/// </para>
	/// <para>
	/// An error cell reads as <see cref="ExcelReaderOptions.ErrorCells"/> says, whatever its format, as Excel shows errors.
	/// An empty cell, and empty text, read as empty. A formula read as its text
	/// (<see cref="ExcelFormulas.FormulaText"/>) isn't formatted.
	/// </para>
	/// </remarks>
	Formatted,
}
