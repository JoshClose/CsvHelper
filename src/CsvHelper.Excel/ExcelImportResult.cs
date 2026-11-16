using System.Collections.Generic;

namespace CsvHelper.Excel;

/// <summary>
/// What <see cref="ExcelReader.Import{T}(string, ExcelReaderOptions?, int)"/> read: the records that read, and every cell,
/// record or column that didn't, by its place.
/// </summary>
/// <remarks>
/// A record that doesn't read is left out of <see cref="Records"/>, and gives an error for each of its cells at fault, so that
/// users can fix every cell of an upload at once. Reading stops at the import's <c>maxErrors</c>, and then
/// <see cref="Complete"/> is <see langword="false"/>: the records after the last error weren't read, and may have errors of
/// their own.
/// </remarks>
/// <typeparam name="T">The type of the records.</typeparam>
public sealed class ExcelImportResult<T>
{
	/// <summary>Creates a result.</summary>
	/// <param name="records">The records that read.</param>
	/// <param name="errors">The errors.</param>
	/// <param name="complete">Whether every record was read.</param>
	internal ExcelImportResult(List<T> records, List<ExcelImportError> errors, bool complete)
	{
		Records = records.AsReadOnly();
		Errors = errors.AsReadOnly();
		Complete = complete;
	}

	/// <summary>The records that read, in the sheet's order; empty when the header lacks a column.</summary>
	public IReadOnlyList<T> Records { get; }

	/// <summary>
	/// Every cell that didn't read, by row, then column: a record's errors with no one cell at fault, such as its
	/// constructor's, before its cells'. At most the import's <c>maxErrors</c>. For a header that lacks columns, an error for
	/// each, at the header's row.
	/// </summary>
	public IReadOnlyList<ExcelImportError> Errors { get; }

	/// <summary>Whether <see cref="Errors"/> has any: <see langword="false"/> when every record read.</summary>
	public bool HasErrors => Errors.Count > 0;

	/// <summary>
	/// Whether every record was read, so that <see cref="Errors"/> has every error there is. <see langword="false"/> when
	/// reading stopped at the import's <c>maxErrors</c> errors, after which no record was read, and when the header lacks
	/// a column, so that no record was.
	/// </summary>
	public bool Complete { get; }
}
