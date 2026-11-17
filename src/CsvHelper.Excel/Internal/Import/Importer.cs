using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// The import (<see cref="ExcelReader.Import{T}(CsvReader, int)"/> and its overloads): a reader's records read with the
/// record type's mapper, and every cell, record or column that doesn't read, by its place.
/// </summary>
/// <remarks>
/// <para>
/// Each record is read with the mapper as <see cref="CsvReader.GetRecords{T}"/> reads it, so a record that reads costs what it
/// costs there and a place in the list. Only a record that fails is read again, member by member, by
/// <see cref="CsvRecordMapper{T}.FindFailingMembers"/>, and each failing member's cell comes from the reader's record
/// source (<see cref="CsvReader.Locate"/>), as the reader's own errors name theirs.
/// </para>
/// <para>
/// <see cref="CsvReaderOptions.OnRecordError"/> isn't called: every error is in the result. What the reader itself throws,
/// such as for a part of the workbook that can't be read, is thrown.
/// </para>
/// </remarks>
internal static class Importer
{
	/// <summary>The texts that end a header name in a missing column's text: the parenthesis, another name, or a number.</summary>
	private static readonly string[] ClosingTexts = ["')", "' or '", "' number "];

	/// <summary>Checks the number of errors after which the import stops.</summary>
	/// <param name="maxErrors">The number.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxErrors"/> is less than 1.</exception>
	public static void CheckMaxErrors(int maxErrors)
	{
		if (maxErrors < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(maxErrors), maxErrors, "maxErrors must be 1 or more.");
		}
	}

	/// <summary>
	/// Reads the reader's records from where it stands, reading its header first when it hasn't, synchronously or not.
	/// </summary>
	/// <typeparam name="T">A <see cref="CsvRecordAttribute"/> type.</typeparam>
	/// <param name="reader">The reader, which isn't disposed.</param>
	/// <param name="maxErrors">The errors after which reading stops; 1 or more.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task whose result is the records that read and the errors.</returns>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has no record mapper.</exception>
	/// <exception cref="KeyNotFoundException">
	/// The reader has no header and a required member of <typeparamref name="T"/> has no column index.
	/// </exception>
	public static async ValueTask<ExcelImportResult<T>> ImportAsync<T>(CsvReader reader, int maxErrors, bool async, CancellationToken cancellationToken)
	{
		var records = new List<T>();
		var errors = new List<ExcelImportError>();

		// As CsvToSheet reads it: ReadHeaderAsync, once the header has been read, makes a record the reader is on the header.
		if (async && reader.Options.HasHeader && reader.FieldCount == 0)
		{
			await reader.ReadHeaderAsync(cancellationToken).ConfigureAwait(false);
		}

		CsvRecordMapper<T> mapper;
		try
		{
			mapper = CsvRecordMapper.Create<T>(reader);
		}
		catch (KeyNotFoundException e) when (reader.Options.HasHeader && e.Data[CsvRecordMapper.MissingColumnsKey] is string[] columns)
		{
			// The reader stands on its header, since the mapper is created before a record is read.
			var location = reader.Locate(-1);
			AddMissingColumns(location, location is null ? reader.RowNumber : reader.LineNumber, columns, e, errors, maxErrors);

			return new ExcelImportResult<T>(records, errors, false);
		}

		var failing = new List<CsvMemberError>();
		while (async ? await reader.ReadAsync(cancellationToken).ConfigureAwait(false) : reader.Read())
		{
			T record;
			try
			{
				record = mapper.Read(reader);
			}
			catch (Exception e) when (e is not NotSupportedException)
			{
				if (!AddRecordErrors(reader, mapper, e, failing, errors, maxErrors))
				{
					return new ExcelImportResult<T>(records, errors, false);
				}

				continue;
			}

			records.Add(record);
		}

		return new ExcelImportResult<T>(records, errors, true);
	}

	/// <summary>
	/// What a sheet whose header row isn't found imports: no records, and an error for each column of
	/// <typeparamref name="T"/> that the row the search names lacks, at that row (<see cref="ExcelReaderOptions.HeaderRow"/>'s
	/// row, or the closest row a search tried), or without a place when no row tried has a value; or the search's own error
	/// at that row when the row has every column of <typeparamref name="T"/>, as a search for other names can find.
	/// </summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="notFound">The row the search names.</param>
	/// <param name="exception">What opening the reader threw.</param>
	/// <param name="maxErrors">The errors after which the import stops.</param>
	/// <returns>The result, which isn't complete.</returns>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has no record mapper.</exception>
	public static ExcelImportResult<T> HeaderNotFound<T>(HeaderSearch.NotFound notFound, Exception exception, int maxErrors)
	{
		var errors = new List<ExcelImportError>();
		var lacks = notFound.Lacks(ExcelHeaderRow.Find<T>());
		if (lacks.Count == 0)
		{
			errors.Add(new ExcelImportError(notFound.Location, notFound.Line, null, null, null, exception.Message, exception));
		}
		else
		{
			AddMissingColumns(notFound.Location, notFound.Line, lacks, exception, errors, maxErrors);
		}

		return new ExcelImportResult<T>([], errors, false);
	}

	/// <summary>Adds an error for each column the header lacks, at the header's row.</summary>
	/// <param name="location">The header row's place, or <see langword="null"/>.</param>
	/// <param name="row">The header row's number, or for CSV 0.</param>
	/// <param name="columns">The missing columns, as the mapper names them: <c>Order.Price ('Price')</c>.</param>
	/// <param name="exception">What the mapper or the header search threw, which names them all.</param>
	/// <param name="errors">The errors.</param>
	/// <param name="maxErrors">The errors after which the import stops.</param>
	private static void AddMissingColumns(string? location, long row, IReadOnlyList<string> columns, Exception exception, List<ExcelImportError> errors, int maxErrors)
	{
		foreach (var missing in columns)
		{
			if (errors.Count == maxErrors)
			{
				break;
			}

			var (member, column) = SplitMissing(missing);
			errors.Add(new ExcelImportError(location, row, column, member, null, $"The header has no column for {missing}.", exception));
		}
	}

	/// <summary>
	/// The member and the first header name of a missing column as the mapper names it: <c>Order.Price ('Price')</c>,
	/// <c>Order.Price ('Price' or 'Cost')</c>, or <c>Order.Price ('Price' number 2)</c> for a name the header has once
	/// where the member reads its second column.
	/// </summary>
	/// <param name="missing">The missing column, as the mapper names it.</param>
	/// <returns>The member, and the header name, or <see langword="null"/> when the text has none.</returns>
	internal static (string Member, string? Column) SplitMissing(string missing)
	{
		var open = missing.IndexOf(" ('", StringComparison.Ordinal);
		if (open <= 0)
		{
			return (missing, null);
		}

		var start = open + 3;
		var end = -1;
		foreach (var close in ClosingTexts)
		{
			var at = missing.IndexOf(close, start, StringComparison.Ordinal);
			if (at >= 0 && (end < 0 || at < end))
			{
				end = at;
			}
		}

		return end < 0 ? (missing, null) : (missing.Substring(0, open), missing.Substring(start, end - start));
	}

	/// <summary>
	/// Adds the errors of a record that didn't read: each member whose field doesn't read on its own, by its field, or the
	/// record's when none does, until <paramref name="errors"/> has <paramref name="maxErrors"/>.
	/// </summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="reader">The reader, on the record.</param>
	/// <param name="mapper">The mapper that failed.</param>
	/// <param name="thrown">What the mapper threw.</param>
	/// <param name="failing">A list to find the members in, cleared first.</param>
	/// <param name="errors">The errors.</param>
	/// <param name="maxErrors">The errors after which the import stops.</param>
	/// <returns>Whether the import goes on: <see langword="false"/> when <paramref name="errors"/> has <paramref name="maxErrors"/>.</returns>
	private static bool AddRecordErrors<T>(CsvReader reader, CsvRecordMapper<T> mapper, Exception thrown, List<CsvMemberError> failing, List<ExcelImportError> errors, int maxErrors)
	{
		failing.Clear();
		try
		{
			mapper.FindFailingMembers(reader, failing);
		}
		catch (Exception)
		{
			// A hand-written mapper's search failed itself: the record is all that is known, as CsvReader's own errors say.
			failing.Clear();
		}

		if (failing.Count == 0)
		{
			errors.Add(Error(reader, null, -1, thrown));
		}
		else
		{
			SortByField(failing);
			foreach (var member in failing)
			{
				if (errors.Count == maxErrors)
				{
					break;
				}

				errors.Add(Error(reader, member.Member, member.FieldIndex, member.Exception));
			}
		}

		return errors.Count < maxErrors;
	}

	/// <summary>
	/// Sorts a record's failing members by their fields, which are in the sheet's order of columns, keeping the order the
	/// mapper read them in for the same field; those of no one field (-1) first, as the record's row comes before its cells.
	/// </summary>
	/// <param name="failing">The members, a few: an insertion sort, which is stable.</param>
	private static void SortByField(List<CsvMemberError> failing)
	{
		for (var i = 1; i < failing.Count; i++)
		{
			var item = failing[i];
			var j = i - 1;
			while (j >= 0 && failing[j].FieldIndex > item.FieldIndex)
			{
				failing[j + 1] = failing[j];
				j--;
			}

			failing[j + 1] = item;
		}
	}

	/// <summary>An error of the record the reader is on: of its field <paramref name="field"/>, or of the record for -1.</summary>
	/// <param name="reader">The reader, on the record.</param>
	/// <param name="member">The member at fault, or <see langword="null"/>.</param>
	/// <param name="field">The member's field, or -1.</param>
	/// <param name="exception">What reading it threw.</param>
	/// <returns>The error.</returns>
	private static ExcelImportError Error(CsvReader reader, string? member, int field, Exception exception)
	{
		var location = reader.Locate(field);
		var row = location is null ? reader.RowNumber : reader.LineNumber;
		var headers = reader.Headers;
		var column = field >= 0 && field < headers.Count ? headers[field] : null;

		return new ExcelImportError(location, row, column, member, Text(reader, field), exception.Message, exception);
	}

	/// <summary>
	/// The text of a field as <see cref="ExcelConvert.ToCsv(string, string, ExcelToCsvOptions?)"/> writes it
	/// (<see cref="CsvReader.GetTextUtf8(int, Span{byte}, bool)"/>): a number rounded to the reader's significant digits, as
	/// Excel shows it, where <see cref="CsvReader.GetString(int)"/> gives the digits Excel stored (<c>1E+29</c> typed is
	/// stored as <c>9.9999999999999991E+28</c>); a date's ISO 8601 text; a boolean's <c>TRUE</c> or <c>FALSE</c>; and an
	/// error cell's own text, also when the reader's error cells fail every read (<see cref="ExcelErrorCells.Error"/>).
	/// Under <see cref="ExcelText.Formatted"/>, the text Excel shows. For CSV, the field's text.
	/// </summary>
	/// <param name="reader">The reader, on the record.</param>
	/// <param name="field">The field, or -1 for none.</param>
	/// <returns>The text; <see langword="null"/> for -1, and for a field the record lacks when the reader throws for one.</returns>
	private static string? Text(CsvReader reader, int field)
	{
		if (field < 0)
		{
			return null;
		}

		Span<byte> room = stackalloc byte[CsvCells.MaxTextLength];
		try
		{
			return CsvReader.Utf8ToString(reader.GetTextUtf8(field, room, roundNumbers: true));
		}
		catch (FormatException)
		{
			return CsvReader.Utf8ToString(reader.GetSpan(field));
		}
		catch (IndexOutOfRangeException)
		{
			return null;
		}
	}
}
