using System;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// Copies a CSV reader's records into a converted sheet's writer
/// (<see cref="ExcelWorkbookWriter.WriteSheet(string, CsvReader, ExcelSheetOptions?, int)"/>): the header, then each field
/// by its column's type, which the first records decide.
/// </summary>
/// <remarks>
/// <para>
/// The records after the header, up to the number that infers types, are held (<see cref="HeldRecords"/>), and each
/// column's type is the narrowest that every non-null field of it among them matches (<see cref="ColumnTypes"/>): text when
/// one doesn't, or when none isn't null, and for a column that no record held reaches. The held records are then written,
/// and the rest as they are read.
/// </para>
/// <para>
/// A null field, empty or one of the reader's null values, is an empty field, which is no cell. A field of a typed column
/// that matches its type is handed to the sink in that type's form, with the type; any other field, a text column's
/// included, is handed as it is, as text.
/// </para>
/// </remarks>
internal static class CsvToSheet
{
	/// <summary>
	/// Writes the reader's header, when its options have <see cref="CsvReaderOptions.HasHeader"/>, then the records it reads
	/// from where it stands. Disposes neither.
	/// </summary>
	/// <param name="csv">The reader.</param>
	/// <param name="sheet">The converted sheet's writer, whose header is the reader's and whose numbers are invariant.</param>
	/// <param name="inferTypes">The records that decide the columns' types, or 0 for text cells.</param>
	/// <param name="options">The workbook's options.</param>
	/// <param name="async">Whether to read and write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read or write.</param>
	/// <returns>A task that completes when the last record is written.</returns>
	public static async ValueTask CopyAsync(CsvReader csv, CsvWriter sheet, int inferTypes, ExcelWriterOptions options, bool async, CancellationToken cancellationToken)
	{
		if (csv.Options.HasHeader)
		{
			// As SheetToCsv reads it: ReadHeaderAsync, once the header has been read, makes a record the reader is on the header.
			if (async && csv.FieldCount == 0)
			{
				await csv.ReadHeaderAsync(cancellationToken).ConfigureAwait(false);
			}

			var names = csv.Headers;
			if (names.Count > 0)
			{
				foreach (var name in names)
				{
					sheet.WriteField(name);
				}

				await EndRecordAsync(sheet, async, cancellationToken).ConfigureAwait(false);
			}
		}

		var types = new ColumnTypes(csv.Options.FormatProvider, options);
		ColumnType[] columns = [];
		if (inferTypes > 0)
		{
			using var held = new HeldRecords();
			while (held.Count < inferTypes && (async ? await csv.ReadAsync(cancellationToken).ConfigureAwait(false) : csv.Read()))
			{
				held.Add(csv);
			}

			columns = Decide(held, csv, types);
			for (var record = 0; record < held.Count; record++)
			{
				WriteHeld(held, record, csv, sheet, columns, types);
				await EndRecordAsync(sheet, async, cancellationToken).ConfigureAwait(false);
			}
		}

		while (async ? await csv.ReadAsync(cancellationToken).ConfigureAwait(false) : csv.Read())
		{
			WriteRecord(csv, sheet, columns, types);
			await EndRecordAsync(sheet, async, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Each column's type: the narrowest that every non-null field of it among the held records matches, and text for a
	/// column whose fields are all null.
	/// </summary>
	/// <param name="held">The held records.</param>
	/// <param name="csv">The reader, whose null values say which fields are null.</param>
	/// <param name="types">The fields' types.</param>
	/// <returns>The types, as many as the widest record held has fields.</returns>
	private static ColumnType[] Decide(HeldRecords held, CsvReader csv, ColumnTypes types)
	{
		// The types every non-null field of a column matches so far, or -1 before its first.
		var sets = new int[held.Width];
		sets.AsSpan().Fill(-1);
		for (var record = 0; record < held.Count; record++)
		{
			var count = held.FieldCount(record);
			for (var i = 0; i < count; i++)
			{
				var field = held.Field(record, i);
				if (sets[i] != 0 && !csv.IsNullSpan(field))
				{
					var matches = types.Matches(field);
					sets[i] = sets[i] < 0 ? matches : sets[i] & matches;
				}
			}
		}

		var columns = new ColumnType[sets.Length];
		for (var i = 0; i < columns.Length; i++)
		{
			columns[i] = sets[i] < 0 ? ColumnType.Text : ColumnTypes.Narrowest(sets[i]);
		}

		return columns;
	}

	/// <summary>Writes a held record's fields, each by its column's type.</summary>
	/// <param name="held">The held records.</param>
	/// <param name="record">The record.</param>
	/// <param name="csv">The reader, whose null values say which fields are null.</param>
	/// <param name="sheet">The sheet's writer.</param>
	/// <param name="columns">The columns' types.</param>
	/// <param name="types">The fields' types.</param>
	private static void WriteHeld(HeldRecords held, int record, CsvReader csv, CsvWriter sheet, ColumnType[] columns, ColumnTypes types)
	{
		Span<byte> room = stackalloc byte[ColumnTypes.MaxFormLength];
		var count = held.FieldCount(record);
		for (var i = 0; i < count; i++)
		{
			WriteField(held.Field(record, i), i < columns.Length ? columns[i] : ColumnType.Text, csv, sheet, types, room);
		}
	}

	/// <summary>Writes the reader's record's fields, each by its column's type.</summary>
	/// <param name="csv">The reader, on a record.</param>
	/// <param name="sheet">The sheet's writer.</param>
	/// <param name="columns">The columns' types; a column past them is text.</param>
	/// <param name="types">The fields' types.</param>
	private static void WriteRecord(CsvReader csv, CsvWriter sheet, ColumnType[] columns, ColumnTypes types)
	{
		Span<byte> room = stackalloc byte[ColumnTypes.MaxFormLength];
		var count = csv.FieldCount;
		for (var i = 0; i < count; i++)
		{
			WriteField(csv.GetSpan(i), i < columns.Length ? columns[i] : ColumnType.Text, csv, sheet, types, room);
		}
	}

	/// <summary>
	/// Writes a field: no cell when it is null; its type's form, with the type, when its column is typed and it matches; and
	/// otherwise its text.
	/// </summary>
	/// <param name="field">The field.</param>
	/// <param name="type">Its column's type.</param>
	/// <param name="csv">The reader, whose null values say whether the field is null.</param>
	/// <param name="sheet">The sheet's writer.</param>
	/// <param name="types">The fields' types.</param>
	/// <param name="room">Room for a form, of <see cref="ColumnTypes.MaxFormLength"/> bytes.</param>
	private static void WriteField(ReadOnlySpan<byte> field, ColumnType type, CsvReader csv, CsvWriter sheet, ColumnTypes types, Span<byte> room)
	{
		if (csv.IsNullSpan(field))
		{
			sheet.WriteField(ReadOnlySpan<byte>.Empty);

			return;
		}

		if (type != ColumnType.Text)
		{
			var length = types.Format(field, type, room);
			if (length >= 0)
			{
				sheet.WriteField(room.Slice(0, length), ColumnTypes.CellType(type));

				return;
			}
		}

		sheet.WriteField(field);
	}

	/// <summary>Ends the writer's record, synchronously or not.</summary>
	/// <param name="sheet">The writer.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when the record is ended.</returns>
	private static ValueTask EndRecordAsync(CsvWriter sheet, bool async, CancellationToken cancellationToken)
	{
		if (async)
		{
			return sheet.EndRecordAsync(cancellationToken);
		}

		sheet.EndRecord();

		return default;
	}
}
