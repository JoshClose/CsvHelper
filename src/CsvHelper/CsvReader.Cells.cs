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
// batch in Fill and the parallel batches. The conversions of typed cells are CsvCells'. A source can also give a field a
// style, whose text its CsvCellOptions.Formatter renders whenever the field's text is read (CsvHelper.Excel's displayed
// text); styles are kept only from a source's first, so other sources and CSV never look at one.
public sealed partial class CsvReader
{
	// The fields (typedCells, recordSource, recordBatch, kinds, rowLines, styles, styledText) are declared at the end of
	// CsvReader.cs, after every field of the CSV paths: see there.

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
	/// The style of field <paramref name="column"/> of the current record (<see cref="RecordBatch.CommitField(int, CsvCellKind, int)"/>):
	/// <see cref="NoStyle"/> for a field without one, past the record's end, and for the header (-1), and when the source
	/// gives none.
	/// </summary>
	/// <param name="column">The 0-based index of the field.</param>
	/// <returns>The style of the field, or <see cref="NoStyle"/>.</returns>
	private int CellStyle(int column) => styles is { } s && (uint)column < (uint)fieldCount ? s[fieldBase + column] : NoStyle;

	/// <summary>
	/// The text of field <paramref name="column"/> of the current record in its style, as UTF-8: written into
	/// <paramref name="buffer"/>, or, when it is longer, into a buffer the reader keeps.
	/// </summary>
	/// <param name="utf8">The field's stored value.</param>
	/// <param name="column">The index of the field in the current record.</param>
	/// <param name="buffer">Room for the text.</param>
	/// <param name="text">The text, valid until the next call or read, or until <paramref name="buffer"/> changes.</param>
	/// <returns>
	/// Whether the field has a style whose text the source's formatter gives; <see langword="false"/> when it has none, or
	/// its text is what it would be without one.
	/// </returns>
	/// <exception cref="InvalidOperationException">The formatter needed more room than it asked for.</exception>
	private bool TryStyledText(ReadOnlySpan<byte> utf8, int column, Span<byte> buffer, out ReadOnlySpan<byte> text)
	{
		text = default;
		var style = CellStyle(column);
		if (style < 0 || recordSource!.CellOptions.Formatter is not { } formatter)
		{
			return false;
		}

		var kind = CellKind(column);
		if (formatter.TryFormat(utf8, kind, style, buffer, out var length))
		{
			text = buffer.Slice(0, length);

			return true;
		}

		if (length < 0)
		{
			return false;
		}

		if (styledText is null || styledText.Length < length)
		{
			styledText = new byte[Math.Max(length, styledText is null ? 0 : styledText.Length * 2)];
		}

		if (!formatter.TryFormat(utf8, kind, style, styledText, out length))
		{
			throw CsvCellFormatter.MoreRoom();
		}

		text = styledText.AsSpan(0, length);

		return true;
	}

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
	/// comes here too. Numbers, text and error cells read as their bytes. A field with a style has its text in that style,
	/// from the source's <see cref="CsvCellOptions.Formatter"/>, unless the formatter says it is the field's own text.
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

		return StyledString(utf8, column) ?? CsvCells.Text(utf8, kind, options);
	}

	/// <summary>
	/// The text of field <paramref name="column"/> of the current record in its style, as a string
	/// (<see cref="CsvCellFormatter.Format"/>).
	/// </summary>
	/// <param name="utf8">The field's stored value.</param>
	/// <param name="column">The index of the field in the current record.</param>
	/// <returns>The text, or <see langword="null"/> when the field has no style, or its text is what it would be without one.</returns>
	/// <exception cref="InvalidOperationException">The formatter needed more room than it asked for.</exception>
	private string? StyledString(ReadOnlySpan<byte> utf8, int column)
	{
		var style = CellStyle(column);

		return style >= 0 && recordSource!.CellOptions.Formatter is { } formatter ? formatter.Format(utf8, CellKind(column), style) : null;
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

		return StyledString(utf8, column) ?? CsvCells.Text(utf8, CellKind(column), recordSource!.CellOptions) ?? MakeString(utf8, -1);
	}

	/// <summary>
	/// Gets the text of field <paramref name="index"/> as UTF-8: the text <see cref="GetString(int)"/> gets, without making
	/// a string, for CsvHelper.Excel's conversion of a sheet to CSV. For CSV it is the field's value
	/// (<see cref="GetSpan(int)"/>), and so is a record source's text, number, error and empty cells'. A date, duration or
	/// boolean cell's text (<see cref="CsvCells.TryText"/>) is written into <paramref name="buffer"/>, and so is a number
	/// cell's rounded to the source's <see cref="CsvCellOptions.SignificantDigits"/> when <paramref name="roundNumbers"/> is
	/// set and it has more (<see cref="CsvCells.TryRound"/>). A field with a style has its text in that style, from the
	/// source's <see cref="CsvCellOptions.Formatter"/>: written into <paramref name="buffer"/>, or into a buffer the reader
	/// keeps when it is longer.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <param name="buffer">Room for a typed cell's text, of at least <see cref="CsvCells.MaxTextLength"/> bytes.</param>
	/// <param name="roundNumbers">Whether a number cell is rounded to the source's significant digits.</param>
	/// <returns>The text of the field; empty for an empty field. Valid until the next read, or until <paramref name="buffer"/> changes.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">
	/// <paramref name="index"/> is not less than <see cref="FieldCount"/> and <see cref="CsvReaderOptions.MissingField"/>
	/// is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </exception>
	/// <exception cref="FormatException">The field is an error cell, and the source's error cells fail every read.</exception>
	internal ReadOnlySpan<byte> GetTextUtf8(int index, Span<byte> buffer, bool roundNumbers)
	{
		var utf8 = GetSpan(index);
		if (!typedCells)
		{
			return utf8;
		}

		return CellText(utf8, index, buffer, roundNumbers);
	}

	/// <summary>
	/// The kind of field <paramref name="index"/> of the current record, for CsvHelper.Excel's conversion of a sheet to CSV,
	/// which writes each field with the type its kind is formatted from, so that injection protection passes over a
	/// number cell's minus sign as it does a number's.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The kind of the field: <see cref="CsvCellKind.Text"/> for CSV, and for a field past the record's end.</returns>
	internal CsvCellKind GetCellKind(int index) => typedCells ? CellKind(index) : CsvCellKind.Text;

	/// <summary>The text of a field of a record source as UTF-8, for <see cref="GetTextUtf8(int, Span{byte}, bool)"/>.</summary>
	/// <param name="utf8">The field's stored text.</param>
	/// <param name="column">The index of the field in the current record, for its kind (<see cref="CellKind(int)"/>).</param>
	/// <param name="buffer">Room for a typed cell's text.</param>
	/// <param name="roundNumbers">Whether a number cell is rounded to the source's significant digits.</param>
	/// <returns>The text of the field.</returns>
	/// <exception cref="FormatException">The field is an error cell, and the source's error cells fail every read.</exception>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private ReadOnlySpan<byte> CellText(ReadOnlySpan<byte> utf8, int column, Span<byte> buffer, bool roundNumbers)
	{
		var kind = CellKind(column);
		var options = recordSource!.CellOptions;
		if (kind == CsvCellKind.Error && options.ThrowOnErrorCells)
		{
			throw CsvCells.ErrorRead(utf8, hideValues, Locate(column));
		}

		if (TryStyledText(utf8, column, buffer, out var styled))
		{
			return styled;
		}

		switch (kind)
		{
			case CsvCellKind.Number:
				if (roundNumbers && options.SignificantDigits is { } digits && CsvCells.TryRound(utf8, digits, buffer, out var rounded))
				{
					return buffer.Slice(0, rounded);
				}

				return utf8;
			default:
				return CsvCells.TryText(utf8, kind, options, buffer, out var length) ? buffer.Slice(0, length) : utf8;
		}
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
		if (CellStyle(index) >= 0)
		{
			// Styled text can be any UTF-8: compared as bytes.
			Span<byte> room = stackalloc byte[CsvCellFormatter.StackLength];
			if (CellKind(index) == CsvCellKind.Error && recordSource!.CellOptions.ThrowOnErrorCells)
			{
				throw CsvCells.ErrorRead(field, hideValues, Locate(index));
			}

			if (TryStyledText(field, index, room, out var styled))
			{
				return styled.SequenceEqual(utf8);
			}
		}

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

	/// <summary>
	/// Where the current record, or its field <paramref name="field"/>, is in a record source; <see langword="null"/> for
	/// CSV input. Internal for CsvHelper.Excel's import, which names each field that doesn't read.
	/// </summary>
	/// <param name="field">The 0-based index of the field, or -1 for the whole record.</param>
	/// <returns>The location, such as <c>Orders!C14</c>, or <see langword="null"/>.</returns>
	internal string? Locate(int field) => recordSource?.Locate(lineNumber, field);

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

		/// <summary>Adds a field to the current record, with a style that its text is rendered in (see <see cref="CommitField(int, CsvCellKind, int)"/>).</summary>
		/// <param name="utf8">The value of the field, as UTF-8 (not validated), copied into the reader's buffer.</param>
		/// <param name="kind">What the field holds.</param>
		/// <param name="style">The style of the field, from 0 to <see cref="CsvCellFormatter.MaxStyle"/>.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="style"/> is negative or more than <see cref="CsvCellFormatter.MaxStyle"/>.</exception>
		public void AddField(ReadOnlySpan<byte> utf8, CsvCellKind kind, int style)
		{
			utf8.CopyTo(GetFieldSpan(utf8.Length));
			CommitField(utf8.Length, kind, style);
		}

		/// <summary>
		/// Gets room in the reader's buffer for the next field, for a source that decodes a value straight into it, which
		/// <see cref="CommitField(int, CsvCellKind)"/> or <see cref="CommitField(int, CsvCellKind, int)"/> then adds. The
		/// span is valid until the next call of the batch.
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
			// The kind goes with the field's first entry, the flag on the entry after it; and, when the reader keeps styles,
			// none (CommitField with a style sets it after).
			r.kinds![r.endsCount - 1] = kind;
			if (r.styles is { } styles)
			{
				styles[r.endsCount - 1] = NoStyle;
			}

			var flagged = r.trim || kind.HasText();
			r.ends[r.endsCount++] = (end + 1) | (flagged ? int.MinValue : 0);
			r.dataEnd = end + 1;
			recordFields++;
		}

		/// <summary>
		/// Adds the field that the source wrote at the start of <see cref="GetFieldSpan(int)"/> to the current record, with
		/// a style: the reader asks the source's <see cref="CsvCellOptions.Formatter"/> for the field's text in it whenever
		/// it reads the field's text, and flags the field for <see cref="GetString(int)"/>'s slow path. Typed reads convert
		/// the field by its kind, as without a style. The reader keeps styles from the first one a source gives.
		/// </summary>
		/// <param name="length">The number of bytes of the value.</param>
		/// <param name="kind">What the field holds.</param>
		/// <param name="style">The style of the field, from 0 to <see cref="CsvCellFormatter.MaxStyle"/>, which the source's formatter knows.</param>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="length"/> is negative or more than the span had room for; or <paramref name="style"/> is negative
		/// or more than <see cref="CsvCellFormatter.MaxStyle"/>.
		/// </exception>
		public void CommitField(int length, CsvCellKind kind, int style)
		{
			if ((uint)style > CsvCellFormatter.MaxStyle)
			{
				throw new ArgumentOutOfRangeException(nameof(style), style, $"A style must be from 0 to {CsvCellFormatter.MaxStyle}.");
			}

			if (reader.styles is null)
			{
				StartStyles();
			}

			// The field as one without a style (whose path stays as it was, for sources without styles), then its style, at
			// its first entry, and the flag, on the entry after it.
			CommitField(length, kind);
			var r = reader;
			r.styles![r.endsCount - 2] = style;
			r.ends[r.endsCount - 1] |= int.MinValue;
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
			if (r.styles is { } oldStyles)
			{
				// As long as the kinds, which bound the fields written before the index grows again.
				var styles = ArrayPool<int>.Shared.Rent(kinds.Length);
				oldStyles.AsSpan(0, count).CopyTo(styles);
				ArrayPool<int>.Shared.Return(oldStyles);
				r.styles = styles;
			}
		}

		/// <summary>
		/// Starts keeping styles, at the first field a source gives one: an array at least as long as the kinds (which bound
		/// the fields written before the index grows), where the fields already in the index have none.
		/// </summary>
		private void StartStyles()
		{
			var r = reader;
			var styles = ArrayPool<int>.Shared.Rent(r.kinds!.Length);
			styles.AsSpan(0, r.endsCount).Fill(NoStyle);
			r.styles = styles;
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
