using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// Finds the header row that <see cref="ExcelReaderOptions.HeaderRow"/> asks for, when a reader opens: a scanner of its own
/// reads the rows that may be the header (<see cref="SearchArea"/>), one record per fill, and each with a value is tried
/// as the header of a probe reader (a <see cref="CsvReader"/> over a one-record source of its fields, with the reader's
/// options), as <see cref="CsvReader.GetRecords{T}"/> or <see cref="CsvReader.TryGetFieldIndex(string, out int)"/> would
/// read it. The reader's scanner then reads the rows after the header, from the header's first column with a value to
/// its last, and holds the header as its first record.
/// </summary>
/// <remarks>
/// The search's scanner reads the area's columns with the reader's options (hidden columns, merged cells, formulas), so
/// its fields are the ones the reader's would have; its rows end at the last row tried, so nothing after them is
/// inflated. It skips rows without a value, and hidden rows when they are skipped, unless the row is given
/// (<see cref="ExcelHeaderRow.At"/>), which is read even when hidden.
/// </remarks>
internal static class HeaderSearch
{
	/// <summary>The rows a search's scanner reads: the row given, or the area's first rows that a search tries.</summary>
	/// <param name="headerRow">The header row asked for.</param>
	/// <param name="area">The area read: a range's, a defined name's, or the whole sheet's.</param>
	/// <returns>The rows, with the area's columns.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The row given is not in the area.</exception>
	public static SheetLayout SearchArea(ExcelHeaderRow headerRow, SheetLayout area)
	{
		if (headerRow.Row > 0)
		{
			if (headerRow.Row < area.FirstRow || headerRow.Row > area.LastRow)
			{
				throw new ArgumentOutOfRangeException("options", headerRow.Row, $"The header row, {headerRow.Row}, is not in the range {Describe(area)}.");
			}

			return new SheetLayout(headerRow.Row, headerRow.Row, area.FirstColumn, area.LastColumn);
		}

		return new SheetLayout(area.FirstRow, Math.Min(area.LastRow, area.FirstRow + headerRow.MaxRows - 1), area.FirstColumn, area.LastColumn);
	}

	/// <summary>
	/// Reads the search's scanner up to the header row, and gives what the reader's scanner reads: the rows after it, in
	/// its columns from its first with a value to its last, with it held as the first record.
	/// </summary>
	/// <param name="search">The search's scanner, over <see cref="SearchArea"/>'s rows, which skips rows without a value; the caller disposes it.</param>
	/// <param name="headerRow">The header row asked for.</param>
	/// <param name="area">The area read.</param>
	/// <param name="options">The reader's options, whose <see cref="ExcelReaderOptions.Csv"/> the probe readers read with.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system, for typed cells' texts.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task whose result is the reader's area, and the header to hold.</returns>
	/// <exception cref="KeyNotFoundException">No row tried has the columns a search wants.</exception>
	/// <exception cref="CsvFormatException">The row given has no value, or the sheet's content can't be read.</exception>
	/// <exception cref="InvalidOperationException">The search's record type has no mapper.</exception>
	public static async ValueTask<(SheetLayout Layout, HeldRecord Header)> RunAsync(
		SheetScanner search, ExcelHeaderRow headerRow, SheetLayout area, ExcelReaderOptions options, bool date1904, bool async, CancellationToken cancellationToken)
	{
		var cellOptions = options.CellOptions(date1904);
		var row = new CapturedRow();
		var target = new RowCapture(row);
		long closest = 0;
		IReadOnlyList<string>? closestLacks = null;
		var more = true;
		while (more)
		{
			row.Clear();
			more = async ? await search.FillAsync(target, cancellationToken).ConfigureAwait(false) : search.Fill(target);
			if (!row.Ended || !row.HasValue)
			{
				continue;
			}

			if (headerRow.Row > 0)
			{
				return Found(search.Layout!, row, area);
			}

			var lacks = Lacks(headerRow, row, options.Csv, cellOptions);
			if (lacks.Count == 0)
			{
				return Found(search.Layout!, row, area);
			}

			// The first of the rows that lack the fewest columns.
			if (closestLacks is null || lacks.Count < closestLacks.Count)
			{
				closest = row.Line;
				closestLacks = lacks;
			}
		}

		var location = search.Location;
		if (headerRow.Row > 0)
		{
			var where = location.Row(headerRow.Row);

			throw new CsvFormatException($"The header row, {where}, has no value.", 0, 0)
			{
				LineNumber = headerRow.Row,
				Location = where,
			};
		}

		var rows = $"{location.QuotedName}!{Describe(SearchArea(headerRow, area))}";
		if (closestLacks is null)
		{
			// No row has a value. A probe of an empty header tells whether any column is wanted, and throws for a record
			// type without a mapper, as a row's probe would have.
			if (Lacks(headerRow, null, options.Csv, cellOptions).Count == 0)
			{
				throw new KeyNotFoundException($"No row of {rows} has a value.");
			}

			throw new KeyNotFoundException($"No row of {rows} has {headerRow.Wants}: none of them has a value.");
		}

		var closestRow = closest.ToString(CultureInfo.InvariantCulture);

		throw new KeyNotFoundException($"No row of {rows} has {headerRow.Wants}. The closest, row {closestRow}, lacks {string.Join(", ", closestLacks)}.");
	}

	/// <summary>A block of a layout's rows and columns in Excel's form, such as <c>B5:F200</c>, <c>1:20</c> or <c>B:F</c>.</summary>
	/// <param name="area">The rows and columns, of one row at least.</param>
	/// <returns>The text.</returns>
	private static string Describe(SheetLayout area) =>
		new ExcelRange((int)area.FirstRow, area.FirstColumn + 1, (int)Math.Max(area.LastRow, area.FirstRow), area.LastColumn + 1).ToString();

	/// <summary>Tries a row as the header: the columns that a probe reader whose header is the row lacks.</summary>
	/// <param name="headerRow">The header row asked for.</param>
	/// <param name="row">The row, or <see langword="null"/> for an empty header.</param>
	/// <param name="csv">The reader's options.</param>
	/// <param name="cellOptions">How the row's typed cells convert.</param>
	/// <returns>The columns lacking; empty when the row is the header.</returns>
	/// <exception cref="InvalidOperationException">The search's record type has no mapper.</exception>
	private static IReadOnlyList<string> Lacks(ExcelHeaderRow headerRow, CapturedRow? row, CsvReaderOptions csv, CsvCellOptions cellOptions)
	{
		using var probe = new CsvReader(new HeaderProbe(row, cellOptions), csv);

		return headerRow.Lacks(probe);
	}

	/// <summary>
	/// What the reader reads once the header row is found: the rows of the area after it, in its columns from its first
	/// with a value to its last, and the header in those columns, held to be the first record.
	/// </summary>
	/// <param name="searched">The search's layout, final: its fields are the area's columns, less hidden ones when those are skipped.</param>
	/// <param name="row">The header row.</param>
	/// <param name="area">The area read.</param>
	/// <returns>The reader's area, and the header to hold.</returns>
	private static (SheetLayout Layout, HeldRecord Header) Found(SheetLayout searched, CapturedRow row, SheetLayout area)
	{
		var firstField = 0;
		while (row.Kind(firstField) == SheetCellKind.Empty)
		{
			firstField++;
		}

		var lastField = row.FieldCount - 1;
		while (row.Kind(lastField) == SheetCellKind.Empty)
		{
			lastField--;
		}

		// A field per column: a hidden column between the header's fields gets an empty one, which the reader's layout leaves out.
		var firstColumn = searched.ColumnOf(firstField);
		var lastColumn = searched.ColumnOf(lastField);
		var values = new byte[lastColumn - firstColumn + 1][];
		var kinds = new SheetCellKind[values.Length];
		for (var i = 0; i < values.Length; i++)
		{
			values[i] = [];
		}

		for (var field = firstField; field <= lastField; field++)
		{
			var at = searched.ColumnOf(field) - firstColumn;
			values[at] = row.Value(field).ToArray();
			kinds[at] = row.Kind(field);
		}

		var layout = new SheetLayout(row.Line + 1, area.LastRow, firstColumn, lastColumn);

		return (layout, HeldRecord.Row(values, kinds, firstColumn, row.Line));
	}

	/// <summary>The one record a search's fill writes: its fields' values and kinds, and its row.</summary>
	private sealed class CapturedRow
	{
		private byte[] bytes = new byte[256];
		private int length;
		private int[] starts = new int[16];
		private int[] lengths = new int[16];
		private SheetCellKind[] kinds = new SheetCellKind[16];

		/// <summary>The number of fields.</summary>
		public int FieldCount { get; private set; }

		/// <summary>The record's row in the sheet.</summary>
		public long Line { get; private set; }

		/// <summary>Whether the record is ended: the fill writes no more.</summary>
		public bool Ended { get; private set; }

		/// <summary>Whether a field has a value: one that isn't empty.</summary>
		public bool HasValue
		{
			get
			{
				for (var i = 0; i < FieldCount; i++)
				{
					if (kinds[i] != SheetCellKind.Empty)
					{
						return true;
					}
				}

				return false;
			}
		}

		/// <summary>Forgets the record, for the next fill.</summary>
		public void Clear()
		{
			length = 0;
			FieldCount = 0;
			Line = 0;
			Ended = false;
		}

		/// <summary>A field's value.</summary>
		/// <param name="field">The field.</param>
		/// <returns>The value, as UTF-8.</returns>
		public ReadOnlySpan<byte> Value(int field) => bytes.AsSpan(starts[field], lengths[field]);

		/// <summary>A field's kind.</summary>
		/// <param name="field">The field.</param>
		/// <returns>The kind.</returns>
		public SheetCellKind Kind(int field) => kinds[field];

		/// <summary>Room for the next field's value.</summary>
		/// <param name="sizeHint">The most bytes the value can take.</param>
		/// <returns>At least <paramref name="sizeHint"/> bytes.</returns>
		public Span<byte> GetSpan(int sizeHint)
		{
			if (bytes.Length - length < sizeHint)
			{
				Array.Resize(ref bytes, Math.Max(bytes.Length * 2, length + sizeHint));
			}

			return bytes.AsSpan(length);
		}

		/// <summary>Adds the field whose value was written at the start of <see cref="GetSpan"/>'s room.</summary>
		/// <param name="valueLength">The value's bytes.</param>
		/// <param name="kind">The value's kind.</param>
		public void Commit(int valueLength, SheetCellKind kind)
		{
			if (FieldCount == kinds.Length)
			{
				Array.Resize(ref starts, FieldCount * 2);
				Array.Resize(ref lengths, FieldCount * 2);
				Array.Resize(ref kinds, FieldCount * 2);
			}

			starts[FieldCount] = length;
			lengths[FieldCount] = valueLength;
			kinds[FieldCount] = kind;
			FieldCount++;
			length += valueLength;
		}

		/// <summary>Ends the record; one without fields gets an empty one, as a reader's record does.</summary>
		/// <param name="row">The record's row in the sheet.</param>
		public void End(long row)
		{
			if (FieldCount == 0)
			{
				Commit(0, SheetCellKind.Empty);
			}

			Line = row;
			Ended = true;
		}
	}

	/// <summary>The <see cref="IRecordTarget"/> of a search: full once a record is ended, so that each fill writes one.</summary>
	private readonly struct RowCapture : IRecordTarget
	{
		private readonly CapturedRow row;

		/// <summary>Creates the target.</summary>
		/// <param name="row">Takes the record.</param>
		public RowCapture(CapturedRow row) => this.row = row;

		/// <inheritdoc/>
		public bool IsFull => row.Ended;

		/// <inheritdoc/>
		public void AddField(ReadOnlySpan<byte> utf8, SheetCellKind kind)
		{
			utf8.CopyTo(row.GetSpan(utf8.Length));
			row.Commit(utf8.Length, kind);
		}

		/// <inheritdoc/>
		public Span<byte> GetFieldSpan(int sizeHint) => row.GetSpan(sizeHint);

		/// <inheritdoc/>
		public void CommitField(int length, SheetCellKind kind) => row.Commit(length, kind);

		/// <inheritdoc/>
		public void EndRecord(long row) => this.row.End(row);
	}

	/// <summary>The source of a probe reader: one record, the row tried, which the reader reads as its header.</summary>
	private sealed class HeaderProbe : CsvRecordSource
	{
		private readonly CapturedRow? row;
		private bool written;

		/// <summary>Creates the source.</summary>
		/// <param name="row">The row, or <see langword="null"/> for a record of one empty field.</param>
		/// <param name="cellOptions">How the row's typed cells convert: as the reader's would.</param>
		public HeaderProbe(CapturedRow? row, CsvCellOptions cellOptions)
		{
			this.row = row;
			CellOptions = cellOptions;
		}

		/// <inheritdoc/>
		public override bool Fill(CsvReader.RecordBatch batch)
		{
			if (!written)
			{
				written = true;
				for (var i = 0; i < (row?.FieldCount ?? 0); i++)
				{
					batch.AddField(row!.Value(i), row.Kind(i).ToCsv());
				}

				batch.EndRecord(row?.Line ?? 0);
			}

			return false;
		}

		/// <inheritdoc/>
		public override ValueTask<bool> FillAsync(CsvReader.RecordBatch batch, CancellationToken cancellationToken) => new(Fill(batch));

		/// <inheritdoc/>
		/// <remarks>A probe reads only its header, whose errors need no location.</remarks>
		public override string? Locate(long line, int field) => null;
	}
}
