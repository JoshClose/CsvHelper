namespace CsvHelper.Excel;

/// <summary>The type of a column of CSV converted to a sheet, which its first records decide (<see cref="ColumnTypes"/>).</summary>
internal enum ColumnType : byte
{
	/// <summary>Text cells: a column of fields of no one type below, or of null fields only.</summary>
	Text,

	/// <summary>Number cells.</summary>
	Number,

	/// <summary>Date cells of dates alone, <c>yyyy-MM-dd</c>.</summary>
	Date,

	/// <summary>Date cells of dates and times, and of dates alone among them.</summary>
	DateTime,

	/// <summary>Cells of dates and times with an offset, as <see cref="ExcelWriterOptions.DateTimeOffsets"/> says.</summary>
	DateTimeOffset,

	/// <summary>Boolean cells.</summary>
	Bool,
}
