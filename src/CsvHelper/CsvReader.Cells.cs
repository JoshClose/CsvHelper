using System;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper;

// Records from a CsvRecordSource (CsvHelper.Excel's sheets) instead of CSV text. The source writes decoded fields into
// the same buffer and index the scanner fills (see CsvReader.Scan.cs), each with a CsvCellKind, so everything that reads
// the index works unchanged. What differs is behind typedCells, which is false for CSV and is tested only off the hot
// paths: in ParseSpan, TryParseSpan and MakeString, in GetChars, the ParseExact overloads and header capture, and per
// batch in Fill and the parallel batches. The conversions of typed cells are CsvCells'.
public sealed partial class CsvReader
{
	// The fields (typedCells, recordSource, recordBatch, kinds, rowLines) are declared at the end of CsvReader.cs, after
	// every field of the CSV paths: see there.

	/// <summary>
	/// Creates a reader over the records of <paramref name="source"/>, which it owns: disposing the reader disposes the
	/// source. See <see cref="CsvRecordSource"/> for how the options apply.
	/// </summary>
	/// <param name="source">The source of the records.</param>
	/// <param name="options">The options, or <see langword="null"/> for <see cref="CsvReaderOptions.Default"/>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="options"/> sets a character that is not ASCII, or uses the same character for two purposes.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException"><see cref="CsvReaderOptions.BufferSize"/> is less than 256.</exception>
	internal CsvReader(CsvRecordSource source, CsvReaderOptions? options)
		: this(options, cells: true)
	{
		ArgumentNullException.ThrowIfNull(source);
		recordSource = source;
		recordBatch = new RecordBatch(this);
		sourceKind = SourceKind.Records;
		kinds = ArrayPool<CsvCellKind>.Shared.Rent(ends.Length);
		rowLines = ArrayPool<long>.Shared.Rent(rowEnds.Length);
		InitOwnedBuffer(this.options.BufferSize);
		// No byte order mark, encoding or separator to find: the fields are decoded already.
		encodingChecked = true;
		hintChecked = true;
		initialized = true;
	}

	/// <summary>Reads the next batch of records from the source, in place of the records already returned.</summary>
	/// <returns>Whether the index holds a record.</returns>
	private bool FillFromSource()
	{
		StartSourceBatch();
		while (!eof && rowCount == rowIndex)
		{
			if (!recordSource!.Fill(recordBatch!))
			{
				eof = finished = true;
			}

			recordBatch!.EndFill();
		}

		scanPos = dataEnd;

		return rowCount > rowIndex;
	}

	/// <summary>Asynchronously reads the next batch of records from the source. See <see cref="FillFromSource"/>.</summary>
	/// <param name="cancellationToken">Cancels reading from the source.</param>
	/// <returns>Whether the index holds a record.</returns>
	private async ValueTask<bool> FillFromSourceAsync(CancellationToken cancellationToken)
	{
		StartSourceBatch();
		while (!eof && rowCount == rowIndex)
		{
			if (!await recordSource!.FillAsync(recordBatch!, cancellationToken).ConfigureAwait(false))
			{
				eof = finished = true;
			}

			recordBatch!.EndFill();
		}

		scanPos = dataEnd;

		return rowCount > rowIndex;
	}

	/// <summary>
	/// Appends the next batch of records from the source to the index, for <see cref="ParallelEnumerate{T}(Func{CsvReader, T}, int)"/>:
	/// the records before it belong to the batches the workers process, which take them out of the buffer.
	/// </summary>
	private void AppendFromSource()
	{
		while (!eof && rowCount == rowIndex)
		{
			if (!recordSource!.Fill(recordBatch!))
			{
				eof = finished = true;
			}

			recordBatch!.EndFill();
		}

		scanPos = dataEnd;
	}

	/// <summary>
	/// Empties the buffer and the index for the next batch. Fill runs only when every record of the index has been
	/// returned, and a source writes whole records, so nothing in them is still needed.
	/// </summary>
	private void StartSourceBatch()
	{
		// A fill that threw can have left a record half written.
		recordBatch!.Reset();
		discarded += dataEnd;
		dataStart = dataEnd = scanPos = 0;
		ends[0] = 0;
		endsCount = 1;
		rowBase = fieldBase = 0;
		rowCount = rowIndex = 0;
	}

	/// <summary>The kind of field <paramref name="column"/> of the current record: text for a field past its end, and for the header (-1).</summary>
	/// <param name="column">The 0-based index of the field.</param>
	/// <returns>The kind of the field.</returns>
	private CsvCellKind CellKind(int column) => (uint)column < (uint)fieldCount ? kinds![fieldBase + column] : CsvCellKind.Text;

	/// <summary>
	/// Parses a field of a record source as <typeparamref name="T"/>. A text or empty field parses as
	/// <see cref="ParseSpan{T}(ReadOnlySpan{byte}, int, IFormatProvider)"/> parses CSV text. A typed field converts as
	/// <see cref="CsvCells"/> says (serial dates for date targets, rounding for decimal and integer targets, error cells
	/// fail), and otherwise its stored text, which is in invariant form, parses with the invariant culture and the built-in
	/// boolean values, whatever <paramref name="provider"/> is.
	/// </summary>
	/// <typeparam name="T">The type to parse the field as.</typeparam>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="column">The index of the field in the current record, for its kind.</param>
	/// <param name="provider">The format provider of text fields.</param>
	/// <returns>The parsed value.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private T ParseCell<T>(ReadOnlySpan<byte> s, int column, IFormatProvider provider)
#if NET
		where T : ISpanParsable<T>
#endif
	{
		var booleans = this.booleans;
		var kind = CellKind(column);
		if (kind.IsTyped())
		{
			if (CsvCells.Parse(s, kind, hideValues, recordSource!.CellOptions, out T value))
			{
				return value;
			}

			provider = CultureInfo.InvariantCulture;
			booleans = null;
		}

		if (CsvValue.IsBuiltIn<T>())
		{
			return CsvValue.Parse<T>(s, provider, hideValues, booleans);
		}

#if NET
		return CsvValue.ParseChars<T>(FieldChars(s), provider, hideValues);
#else
		throw CsvValue.NotSupported(typeof(T));
#endif
	}

	/// <summary>Tries to parse a field of a record source as <typeparamref name="T"/>, as <see cref="ParseCell{T}"/> parses it.</summary>
	/// <typeparam name="T">The type to parse the field as.</typeparam>
	/// <param name="s">The UTF-8 text of the field.</param>
	/// <param name="column">The index of the field in the current record, for its kind.</param>
	/// <param name="value">The parsed value, or the default of <typeparamref name="T"/> when parsing fails.</param>
	/// <returns>Whether the field could be parsed.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private bool TryParseCell<T>(ReadOnlySpan<byte> s, int column, out T value)
#if NET
		where T : ISpanParsable<T>
#endif
	{
		var provider = this.provider;
		var booleans = this.booleans;
		var kind = CellKind(column);
		if (kind.IsTyped())
		{
			if (CsvCells.TryParse(s, kind, recordSource!.CellOptions, out value) is { } converted)
			{
				return converted;
			}

			provider = CultureInfo.InvariantCulture;
			booleans = null;
		}

		if (CsvValue.IsBuiltIn<T>())
		{
			return CsvValue.TryParse(s, provider, booleans, out value);
		}

#if NET
		return T.TryParse(FieldChars(s), provider, out value!);
#else
		throw CsvValue.NotSupported(typeof(T));
#endif
	}

	/// <summary>
	/// The text of a field of a record source when it is not the field's bytes, for <see cref="MakeString(ReadOnlySpan{byte}, int)"/>:
	/// ISO 8601 for a date, <c>[h]:mm:ss</c> for a duration, <c>TRUE</c> or <c>FALSE</c> for a boolean (see
	/// <see cref="CsvCells.Text"/>). The <see cref="RecordBatch"/> flags those fields, so that <see cref="GetString(int)"/>
	/// comes here too. Numbers, text and error cells read as their bytes.
	/// </summary>
	/// <param name="utf8">The UTF-8 text of the field.</param>
	/// <param name="column">The index of the field in the current record, for its kind (<see cref="CellKind(int)"/>).</param>
	/// <returns>The text of the field, or <see langword="null"/> when it is the field's bytes.</returns>
	/// <exception cref="FormatException">The field is an error cell, and the source's error cells fail every read.</exception>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private string? CellString(ReadOnlySpan<byte> utf8, int column)
	{
		var kind = CellKind(column);
		var options = recordSource!.CellOptions;
		if (kind == CsvCellKind.Error && options.ThrowOnErrorCells)
		{
			throw CsvCells.ErrorRead(utf8, hideValues, Locate(column));
		}

		return CsvCells.Text(utf8, kind, options);
	}

	/// <summary>
	/// The header name of field <paramref name="column"/> of a record source: its text, so that a date names its column
	/// <c>2024-01-15</c>. An error cell names its column with its error's text even when the source's error cells fail
	/// every read (<see cref="CsvCellOptions.ThrowOnErrorCells"/>): that policy is for values, and a header is names.
	/// </summary>
	/// <param name="column">The index of the field in the header.</param>
	/// <returns>The name.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private string CellHeader(int column)
	{
		var utf8 = GetSpan(column);

		return CsvCells.Text(utf8, CellKind(column), recordSource!.CellOptions) ?? MakeString(utf8, -1);
	}

	/// <summary>Copies the text of field <paramref name="index"/> of a record source into <paramref name="destination"/>, for <see cref="GetChars(int, Span{char})"/>.</summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="destination">Receives the text of the field.</param>
	/// <returns>The number of chars written to <paramref name="destination"/>.</returns>
	/// <exception cref="ArgumentException"><paramref name="destination"/> is too small for the text.</exception>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private int CellChars(int index, Span<char> destination)
	{
		var utf8 = GetSpan(index);
		if (CellString(utf8, index) is not { } text)
		{
			return Encoding.UTF8.GetChars(utf8, destination);
		}

		text.AsSpan().CopyTo(destination);

		return text.Length;
	}

	/// <summary>Whether the text of field <paramref name="index"/> of a record source is <paramref name="utf8"/>, for <see cref="FieldEquals(int, ReadOnlySpan{byte})"/>.</summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="utf8">The text to compare with, as UTF-8.</param>
	/// <returns>Whether the text of the field is <paramref name="utf8"/>.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private bool CellEquals(int index, ReadOnlySpan<byte> utf8)
	{
		var field = GetSpan(index);
		if (CellString(field, index) is not { } text)
		{
			return field.SequenceEqual(utf8);
		}

		// The text of a typed cell is ASCII (CsvCells.Text), so each char is one byte, and a byte of more than one is unequal.
		if (text.Length != utf8.Length)
		{
			return false;
		}

		for (var i = 0; i < text.Length; i++)
		{
			if (text[i] != utf8[i])
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Parses field <paramref name="index"/> of a record source in exactly a format, for the <c>ParseExact</c> overloads:
	/// a number, date or duration cell converts as <see cref="Parse{T}(int)"/> converts it, without the format, which
	/// applies to text cells.
	/// </summary>
	/// <typeparam name="T">A type that <c>ParseExact</c> parses.</typeparam>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="format">The format, or <see langword="null"/> for <paramref name="formats"/>.</param>
	/// <param name="formats">The formats, when <paramref name="format"/> is <see langword="null"/>.</param>
	/// <param name="provider">The format provider of text cells.</param>
	/// <returns>The parsed value.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private T ParseExactCell<T>(int index, string? format, string[]? formats, IFormatProvider provider)
	{
		var s = GetSpan(index);
		var kind = CellKind(index);
		if (CsvValue.ParsesExact<T>() && kind.IsTyped() && CsvCells.Parse(s, kind, hideValues, recordSource!.CellOptions, out T value))
		{
			return value;
		}

		return format is not null
			? CsvValue.ParseExact<T>(s, format, provider, hideValues)
			: CsvValue.ParseExact<T>(s, formats!, provider, hideValues);
	}

	/// <summary>Where the current record, or its field <paramref name="field"/>, is in a record source; <see langword="null"/> for CSV input.</summary>
	/// <param name="field">The 0-based index of the field, or -1 for the whole record.</param>
	/// <returns>The location, such as <c>Orders!C14</c>, or <see langword="null"/>.</returns>
	private string? Locate(int field) => recordSource?.Locate(lineNumber, field);

	/// <summary>
	/// Where a <see cref="CsvRecordSource"/> writes its records: the reader's buffer and index, which this appends whole
	/// records of decoded fields to, as the CSV scanner would. Each field is followed by one byte (the separator, or LF
	/// after the last field of a record), so that <see cref="RawRecord"/> reads as the fields with separators between them.
	/// </summary>
	/// <remarks>
	/// Date, duration, boolean and error fields carry the index's needs-decoding flag, as every field does when the reader
	/// trims: <see cref="GetString(int)"/> then leaves its fast path for <see cref="MakeString(ReadOnlySpan{byte}, int)"/>,
	/// whose typed-cell path gives them their text. Decoding a flagged field of a source only trims it.
	/// </remarks>
	internal sealed class RecordBatch
	{
		private readonly CsvReader reader;
		// The fields of the record being written.
		private int recordFields;

		/// <summary>Creates the batch of <paramref name="reader"/>.</summary>
		/// <param name="reader">The reader whose buffer and index the source writes.</param>
		internal RecordBatch(CsvReader reader) => this.reader = reader;

		/// <summary>
		/// Whether the batch holds as many bytes (<see cref="CsvReaderOptions.BufferSize"/>), fields or records as the reader
		/// takes at once: the source should end the batch after the record it is writing.
		/// </summary>
		public bool IsFull => reader.dataEnd >= reader.options.BufferSize || reader.endsCount >= IndexCapacity ||
							  reader.rowCount >= IndexCapacity;

		/// <summary>The number of fields written to the current record so far.</summary>
		public int FieldCount => recordFields;

		/// <summary>Adds a field to the current record.</summary>
		/// <param name="utf8">The value of the field, as UTF-8 (not validated), copied into the reader's buffer.</param>
		/// <param name="kind">What the field holds.</param>
		public void AddField(ReadOnlySpan<byte> utf8, CsvCellKind kind)
		{
			utf8.CopyTo(GetFieldSpan(utf8.Length));
			CommitField(utf8.Length, kind);
		}

		/// <summary>
		/// Gets room in the reader's buffer for the next field, for a source that decodes a value straight into it, which
		/// <see cref="CommitField"/> then adds. The span is valid until the next call of the batch.
		/// </summary>
		/// <param name="sizeHint">The number of bytes the value can take at most.</param>
		/// <returns>At least <paramref name="sizeHint"/> bytes.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="sizeHint"/> is negative.</exception>
		public Span<byte> GetFieldSpan(int sizeHint)
		{
			if (sizeHint < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(sizeHint), sizeHint, "The size cannot be negative.");
			}

			var r = reader;
			// The value and the byte after it.
			if (r.buffer.Length - r.dataEnd <= sizeHint)
			{
				GrowBuffer(sizeHint + 1);
			}

			return r.buffer.AsSpan(r.dataEnd, r.buffer.Length - r.dataEnd - 1);
		}

		/// <summary>Adds the field that the source wrote at the start of <see cref="GetFieldSpan(int)"/> to the current record.</summary>
		/// <param name="length">The number of bytes of the value.</param>
		/// <param name="kind">What the field holds.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative or more than the span had room for.</exception>
		public void CommitField(int length, CsvCellKind kind)
		{
			var r = reader;
			if ((uint)length >= (uint)(r.buffer.Length - r.dataEnd))
			{
				throw new ArgumentOutOfRangeException(nameof(length), length, "The field is longer than the room that GetFieldSpan gave.");
			}

			if (r.ends.Length - EndsSlack <= r.endsCount || r.kinds!.Length <= r.endsCount)
			{
				GrowIndex();
			}

			var end = r.dataEnd + length;
			r.buffer[end] = r.sepByte;
			// The kind goes with the field's first entry, the flag on the entry after it.
			r.kinds![r.endsCount - 1] = kind;
			var flagged = r.trim || kind.HasText();
			r.ends[r.endsCount++] = (end + 1) | (flagged ? int.MinValue : 0);
			r.dataEnd = end + 1;
			recordFields++;
		}

		/// <summary>Ends the current record. A record without fields gets one empty field, as an empty line of CSV has.</summary>
		/// <param name="line">
		/// The 1-based line of the record in the source, such as its row number in a sheet: the reader's
		/// <see cref="LineNumber"/>, and what <see cref="CsvRecordSource.Locate(long, int)"/> gets.
		/// </param>
		public void EndRecord(long line)
		{
			if (recordFields == 0)
			{
				AddField(default, CsvCellKind.Empty);
			}

			var r = reader;
			if (r.rowEnds.Length <= r.rowCount || r.rowLines!.Length <= r.rowCount)
			{
				GrowRows();
			}

			// LF after the last field, as at the end of a line of CSV.
			r.buffer[r.dataEnd - 1] = (byte)'\n';
			r.rowEnds[r.rowCount] = r.endsCount - 1;
			r.rowLines![r.rowCount] = line;
			r.rowCount++;
			recordFields = 0;
		}

		/// <summary>Forgets the fields of a record that a fill started and did not end (it threw), as the buffer starts over.</summary>
		internal void Reset() => recordFields = 0;

		/// <summary>Checks, after the source's Fill, that it left no record unfinished.</summary>
		/// <exception cref="InvalidOperationException">The source wrote fields without ending their record.</exception>
		internal void EndFill()
		{
			if (recordFields > 0)
			{
				recordFields = 0;

				throw new InvalidOperationException("A record source must end every record it starts in the same Fill (EndRecord).");
			}
		}

		private void GrowBuffer(int needed)
		{
			var r = reader;
			var size = Math.Max((long)r.buffer.Length * 2, (long)r.dataEnd + needed);
			if (size > Array.MaxLength)
			{
				throw new CsvFormatException("A single record is too large to buffer.");
			}

			var bigger = ArrayPool<byte>.Shared.Rent((int)size);
			Buffer.BlockCopy(r.buffer, 0, bigger, 0, r.dataEnd);
			if (r.bufferRented)
			{
				ArrayPool<byte>.Shared.Return(r.buffer);
			}

			r.buffer = bigger;
			r.bufferRented = true;
		}

		private void GrowIndex()
		{
			var r = reader;
			var count = r.endsCount;
			var ends = ArrayPool<int>.Shared.Rent(Math.Max(r.ends.Length * 2, count + 1 + EndsSlack));
			r.ends.AsSpan(0, count).CopyTo(ends);
			ArrayPool<int>.Shared.Return(r.ends);
			r.ends = ends;
			var old = r.kinds!;
			var kinds = ArrayPool<CsvCellKind>.Shared.Rent(ends.Length);
			old.AsSpan(0, count).CopyTo(kinds);
			ArrayPool<CsvCellKind>.Shared.Return(old);
			r.kinds = kinds;
		}

		private void GrowRows()
		{
			var r = reader;
			var count = r.rowCount;
			var rowEnds = ArrayPool<int>.Shared.Rent(Math.Max(r.rowEnds.Length * 2, count + 1));
			r.rowEnds.AsSpan(0, count).CopyTo(rowEnds);
			ArrayPool<int>.Shared.Return(r.rowEnds);
			r.rowEnds = rowEnds;
			var old = r.rowLines!;
			var lines = ArrayPool<long>.Shared.Rent(rowEnds.Length);
			old.AsSpan(0, count).CopyTo(lines);
			ArrayPool<long>.Shared.Return(old);
			r.rowLines = lines;
		}
	}
}
