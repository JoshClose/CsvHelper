using System;

namespace CsvHelper.Excel;

/// <summary>
/// A cell that doesn't read as its member's type, a record that doesn't read, or a column that the header lacks, found by
/// <see cref="ExcelReader.Import{T}(string, ExcelReaderOptions?, int)"/>: where it is, what it holds and what is wrong, for
/// telling users which cells to fix.
/// </summary>
/// <remarks>
/// <para>
/// Three kinds of error, told apart by what they have:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>A cell:</b> a member whose field doesn't read, such as <c>abc</c> in a number's column, or a value that a
/// <see cref="CsvValidateAttribute"/> refuses. It has the cell's <see cref="Location"/> (<c>Orders!C14</c>), its
/// <see cref="Column"/>, <see cref="Member"/> and <see cref="Value"/>.
/// </description></item>
/// <item><description>
/// <b>A record:</b> one that doesn't read though each of its members' fields reads on its own, such as when its constructor
/// throws, or a nested record or a collection that fails as a whole. Its <see cref="Location"/> is the record's row
/// (<c>Orders!14:14</c>), and it has no <see cref="Column"/> or <see cref="Value"/>; a nested record or a collection names
/// its <see cref="Member"/>.
/// </description></item>
/// <item><description>
/// <b>A column the header lacks:</b> its <see cref="Location"/> is the header's row, with the <see cref="Member"/> and the
/// <see cref="Column"/> it looks for, and no <see cref="Value"/>. Then no record is read. A header row that isn't found
/// gives such errors too: at the row <see cref="ExcelReaderOptions.HeaderRow"/> names, when it has no value, or at the
/// closest row a header search tried.
/// </description></item>
/// </list>
/// <para>
/// <see cref="Message"/> is the cause's message, and so follows <see cref="CsvReaderOptions.IncludeValuesInExceptions"/>;
/// <see cref="Value"/> is the cell's text whatever that option says, since it is what users see in the cell.
/// </para>
/// </remarks>
public sealed class ExcelImportError
{
	/// <summary>Creates an error.</summary>
	/// <param name="location">The cell, the record's row or the header's row, or <see langword="null"/>.</param>
	/// <param name="row">The sheet's row, or for CSV the record's number.</param>
	/// <param name="column">The header's name of the column, or <see langword="null"/>.</param>
	/// <param name="member">The member, or <see langword="null"/>.</param>
	/// <param name="value">The cell's text, or <see langword="null"/>.</param>
	/// <param name="message">What is wrong.</param>
	/// <param name="exception">The cause.</param>
	internal ExcelImportError(string? location, long row, string? column, string? member, string? value, string message, Exception exception)
	{
		Location = location;
		Row = row;
		Column = column;
		Member = member;
		Value = value;
		Message = message;
		Exception = exception;
	}

	/// <summary>
	/// Where the error is, as Excel writes it in a formula: the cell, such as <c>Orders!C14</c>; the record's row, such as
	/// <c>Orders!14:14</c>, when no one cell is at fault; or the header's row for a column it lacks (the row a header row
	/// option names, or the closest row its search tried). The sheet's name is quoted when Excel would quote it
	/// (<c>'Q1 Orders'!C14</c>). <see langword="null"/> for CSV, whose place is <see cref="Row"/>, and for the columns of a
	/// sheet without rows, or whose header search found no row with a value, which have no header row to name.
	/// </summary>
	public string? Location { get; }

	/// <summary>
	/// The 1-based row of the sheet that the error is in. For CSV, the record's number (<see cref="CsvReader.RowNumber"/>),
	/// the header not counted, and 0 for the header; 0 too for the columns of a sheet without a header row to name.
	/// </summary>
	public long Row { get; }

	/// <summary>
	/// The header's name of the cell's column, such as <c>Price</c>; for a column the header lacks, the name looked for (the
	/// first, when the member has several). <see langword="null"/> for a record's error, and without a header.
	/// </summary>
	public string? Column { get; }

	/// <summary>
	/// The member whose field doesn't read, or whose column the header lacks, as error messages name it (<c>Order.Price</c>);
	/// for a member of a nested record, the member of the nested type. <see langword="null"/> for a record's error with no
	/// member at fault, such as when its constructor throws.
	/// </summary>
	public string? Member { get; }

	/// <summary>
	/// The cell's text, as <see cref="ExcelConvert.ToCsv(string, string, ExcelToCsvOptions?)"/> writes it: a number to the
	/// reader's <see cref="ExcelReaderOptions.SignificantDigits"/>, as Excel shows it (<c>1E+29</c>, where Excel stores
	/// <c>9.9999999999999991E+28</c>), a date's ISO 8601 text (<c>2024-01-15</c>), a boolean's <c>TRUE</c> or <c>FALSE</c>,
	/// an error's own text (<c>#N/A</c>), and <c>""</c> for an empty cell; for CSV, the field's text.
	/// <see langword="null"/> for a record's error and a column the header lacks.
	/// </summary>
	public string? Value { get; }

	/// <summary>
	/// What is wrong: the message of the cause, such as <c>'abc' is not a valid Int32.</c> or a
	/// <see cref="CsvValidateAttribute"/>'s message; for a column the header lacks, <c>The header has no column for
	/// Order.Price ('Price').</c>
	/// </summary>
	public string Message { get; }

	/// <summary>
	/// The cause: a <see cref="FormatException"/> or <see cref="OverflowException"/> for a field that doesn't parse, a
	/// <see cref="CsvValidationException"/>, a converter's or a constructor's exception, or for a column the header lacks the
	/// <see cref="System.Collections.Generic.KeyNotFoundException"/> that the record mapper threw, which names every column
	/// missing.
	/// </summary>
	public Exception Exception { get; }

	/// <summary>
	/// The error's place and message, such as <c>Orders!C14: 'abc' is not a valid Int32.</c>, or for CSV <c>Record 14: …</c>;
	/// the message alone without a place, as for a column that a CSV file's header lacks.
	/// </summary>
	/// <returns>The text.</returns>
	public override string ToString() => Location is not null ? $"{Location}: {Message}" : Row > 0 ? $"Record {Row}: {Message}" : Message;
}
