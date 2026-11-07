namespace CsvHelper.Excel;

/// <summary>
/// How a written workbook writes a <see cref="System.DateTimeOffset"/> (<see cref="ExcelWriterOptions.DateTimeOffsets"/>):
/// a cell holds a date and time without an offset, so either the offset or the clock time is lost as a date.
/// </summary>
public enum ExcelDateTimeOffsets
{
	/// <summary>A text cell of its ISO 8601 text (<c>"O"</c>), which keeps both. The default.</summary>
	Text,

	/// <summary>A date cell of its time in UTC (<see cref="System.DateTimeOffset.UtcDateTime"/>).</summary>
	Utc,

	/// <summary>A date cell of its clock time, the time at its own offset (<see cref="System.DateTimeOffset.DateTime"/>).</summary>
	ClockTime,
}
