using System;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// Copies a reader's records into a CSV writer, for <see cref="ExcelConvert"/>'s conversions of a sheet to CSV: the header,
/// then each field as the text <see cref="CsvReader.GetString(int)"/> gets, as UTF-8 and without a string.
/// </summary>
/// <remarks>
/// <para>
/// Each field is core's <see cref="CsvReader.GetTextUtf8(int, Span{byte}, bool)"/>, with numbers rounded to the source's
/// significant digits: the reader's bytes for text, a number of as many digits or fewer, and an error cell's text, and
/// otherwise text formatted into a buffer on the stack. It is written with the type its cell's kind is formatted from
/// (<see cref="Types"/>), so that injection protection and <see cref="CsvWriterOptions.ShouldQuote"/> take a number cell
/// for a number, as they do a <see cref="double"/> field, and <c>-5</c> isn't escaped.
/// </para>
/// <para>
/// A sheet read under <see cref="ExcelText.Formatted"/> has its fields' shown text (<c>$1,234.50</c>, <c>Jan-24</c>), which
/// is written as text whatever its cell's kind: a number format can show a number as anything, <c>=1+1</c> among them,
/// so injection protection checks every field, and a negative number shown <c>-5</c> is escaped as text is.
/// </para>
/// <para>
/// A record of one empty field, which is what a reader gives for an empty row when it keeps them, is written as wide as
/// the first record, with an empty field for each column. Empty rows before the first record with a value are counted
/// until its width is known, so that they are as wide as it too; when no record has one, each is one empty field, which
/// the writer quotes (<c>""</c>). Other records are written as they are, a longer one longer, so the writer's
/// field-count check must be off.
/// </para>
/// </remarks>
internal static class SheetToCsv
{
	/// <summary>
	/// The room for a field's text that isn't its bytes: a date's (23 bytes), a duration's, a boolean's, or a number rounded
	/// to 28 digits or fewer in its own notation, whose text <see cref="CsvCells.TryRound"/> writes only when it fits.
	/// </summary>
	private const int TextRoom = 64;

	/// <summary>
	/// The type each kind of cell is formatted from, by <see cref="CsvCellKind"/>: <see cref="string"/> for text, error and
	/// empty cells, and for CSV.
	/// </summary>
	private static readonly Type[] Types = [typeof(string), typeof(double), typeof(DateTime), typeof(bool), typeof(string), typeof(string), typeof(TimeSpan)];

	/// <summary>The type each kind of cell is formatted from under <see cref="ExcelText.Formatted"/>: <see cref="string"/>, as each is shown text.</summary>
	private static readonly Type[] TextTypes = [typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string)];

	/// <summary>
	/// Writes the reader's header, when its options have <see cref="CsvReaderOptions.HasHeader"/>, then the records it
	/// reads from where it stands, as <see cref="CsvReader.GetRecords{T}"/> reads them: a record it is on has been read.
	/// Disposes neither.
	/// </summary>
	/// <param name="sheet">The reader, of a sheet or of CSV.</param>
	/// <param name="csv">The writer, whose field-count check is off unless the records are all as wide.</param>
	/// <param name="async">Whether to read and write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read or write.</param>
	/// <returns>A task that completes when the last record is written.</returns>
	public static async ValueTask CopyAsync(CsvReader sheet, CsvWriter csv, bool async, CancellationToken cancellationToken)
	{
		// The width of the first record with a value, and the empty rows before it.
		var width = -1;
		var types = sheet.CellFormatter is null ? Types : TextTypes;
		var pending = 0;
		if (sheet.Options.HasHeader)
		{
			// Headers reads the header when it hasn't been. ReadHeaderAsync does so asynchronously, and once it has, makes the
			// record the reader is on the header: so it is called only when the reader is on none, where it does nothing then.
			if (async && sheet.FieldCount == 0)
			{
				await sheet.ReadHeaderAsync(cancellationToken).ConfigureAwait(false);
			}

			var names = sheet.Headers;
			if (names.Count == 1 && names[0].Length == 0)
			{
				pending++;
			}
			else if (names.Count > 0)
			{
				width = names.Count;
				foreach (var name in names)
				{
					csv.WriteField(name);
				}

				await EndRecordAsync(csv, async, cancellationToken).ConfigureAwait(false);
			}
		}

		while (async ? await sheet.ReadAsync(cancellationToken).ConfigureAwait(false) : sheet.Read())
		{
			if (sheet.FieldCount == 1 && sheet.GetSpan(0).IsEmpty)
			{
				if (width < 0)
				{
					pending++;

					continue;
				}

				WriteEmpty(csv, width);
			}
			else
			{
				if (width < 0)
				{
					width = sheet.FieldCount;
					for (; pending > 0; pending--)
					{
						WriteEmpty(csv, width);
						await EndRecordAsync(csv, async, cancellationToken).ConfigureAwait(false);
					}
				}

				WriteFields(sheet, csv, types);
			}

			await EndRecordAsync(csv, async, cancellationToken).ConfigureAwait(false);
		}

		for (; pending > 0; pending--)
		{
			WriteEmpty(csv, 1);
			await EndRecordAsync(csv, async, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>Writes the fields of the reader's record, each as its text.</summary>
	/// <param name="sheet">The reader, on a record.</param>
	/// <param name="csv">The writer.</param>
	/// <param name="types">The type each kind of cell is written as (<see cref="Types"/>, or <see cref="TextTypes"/> under <see cref="ExcelText.Formatted"/>).</param>
	/// <exception cref="FormatException">A field is an error cell, and the sheet's error cells fail every read.</exception>
	private static void WriteFields(CsvReader sheet, CsvWriter csv, Type[] types)
	{
		Span<byte> room = stackalloc byte[TextRoom];
		var count = sheet.FieldCount;
		for (var i = 0; i < count; i++)
		{
			var text = sheet.GetTextUtf8(i, room, roundNumbers: true);
			csv.WriteField(text, types[(int)sheet.GetCellKind(i)]);
		}
	}

	/// <summary>Writes a record's empty fields.</summary>
	/// <param name="csv">The writer.</param>
	/// <param name="width">The number of fields.</param>
	private static void WriteEmpty(CsvWriter csv, int width)
	{
		for (var i = 0; i < width; i++)
		{
			csv.WriteField(ReadOnlySpan<byte>.Empty);
		}
	}

	/// <summary>Ends the writer's record, synchronously or not.</summary>
	/// <param name="csv">The writer.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when the record is ended.</returns>
	private static ValueTask EndRecordAsync(CsvWriter csv, bool async, CancellationToken cancellationToken)
	{
		if (async)
		{
			return csv.EndRecordAsync(cancellationToken);
		}

		csv.EndRecord();

		return default;
	}
}
