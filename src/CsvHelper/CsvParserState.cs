using CsvHelper.Configuration;
using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CsvHelper;

[DebuggerDisplay("Row = {parsedRowsPosition}, Count = {parsedRowsCount}, CharsRead = {charsRead}")]
internal class CsvParserState : IDisposable, IRow
{
	internal ICsvParsingStrategy parsingStrategy;
	internal StringCreator stringCreator;
	internal bool isDisposed;

	internal CsvParserOptions options;
	internal TextReader reader;

	internal char[] buffer;
	internal int bufferPosition;
	internal int charsRead = -1;

	internal bool parsingInEscape;
	internal bool parsingFieldIsInvalid;
	internal int parsingFieldPrevEscapeIndex = -2;
	internal int parsingEscapeMask;
	internal int parsingRowStart;
	internal int parsingFieldStart;
	internal int parsingFieldsCount;

	internal CsvRow[] parsedRows;
	internal int prevRowFieldCount;
	internal int parsedRowsCount;
	internal int parsedRowsPosition = -1;
	internal CsvField[] parsedFields;
	internal int parsedFieldsCount;
	internal int parsedFieldsPosition;
	internal int rowFieldsCount;

	public int Count => rowFieldsCount;

	public ReadOnlySpan<char> this[int index] => GetField(index);

	public ReadOnlySpan<char> Row => GetRow();

	public CsvParserState(TextReader reader, CsvParserOptions options)
	{
		this.options = options;
		this.reader = reader;

		buffer = ArrayPool<char>.Shared.Rent(options.BufferSize);
		parsedRows = ArrayPool<CsvRow>.Shared.Rent(options.ParsedRowsSize);
		parsedFields = ArrayPool<CsvField>.Shared.Rent(options.ParsedFieldsSize);

		parsingStrategy = options.ParsingStrategyImplementation;
		stringCreator = options.StringCreator;
	}

	public bool NextRow()
	{
		if (charsRead == 0)
		{
			return false;
		}

		if (parsedRowsPosition + 1 == parsedRowsCount)
		{
			return false;
		}

		var fieldCount = prevRowFieldCount;
		parsedRowsPosition++;
		parsedFieldsPosition += fieldCount;

		ref var row = ref parsedRows[parsedRowsPosition];
		rowFieldsCount = row.FieldCount;
		prevRowFieldCount = row.FieldCount;

		return true;
	}

	public int CopyRemainingFrom(CsvParserState state)
	{
		Debug.Assert(buffer.Length == state.buffer.Length);
		Debug.Assert(parsedRows.Length == state.parsedRows.Length);
		Debug.Assert(parsedFields.Length == state.parsedFields.Length);

		var fromBuffer = state.buffer;
		var fromCharsRead = state.charsRead;
		var fromParsingRowStart = state.parsingRowStart;
		var fromBufferPosition = state.bufferPosition;
		var fromParsingEscapeMask = state.parsingEscapeMask;
		var fromParsingFieldStart = state.parsingFieldStart;
		var fromParsingFieldsCount = state.parsingFieldsCount;
		var fromParsedFields = state.parsedFields;
		var fromParsedFieldsCount = state.parsedFieldsCount;

		var charsLeft = Math.Max(fromCharsRead - fromParsingRowStart, 0);
		Array.Copy(fromBuffer, fromParsingRowStart, buffer, 0, charsLeft);
		charsRead = buffer.Fill(reader, charsLeft);
		charsRead += charsLeft;
		bufferPosition = fromParsingFieldStart - fromParsingRowStart;

		prevRowFieldCount = 0;

		parsingEscapeMask = fromParsingEscapeMask;
		parsingFieldStart = fromParsingFieldStart - fromParsingRowStart;
		parsingFieldsCount = fromParsingFieldsCount;
		parsingRowStart = 0;

		parsedFieldsCount = fromParsingFieldsCount;
		parsedFieldsPosition = 0;
		parsedRowsCount = 0;
		parsedRowsPosition = -1;

		var fromCopyStart = fromParsedFieldsCount - fromParsingFieldsCount;
		var copyLength = fromParsingFieldsCount;
		Array.Copy(fromParsedFields, fromCopyStart, parsedFields, 0, copyLength);

#if DEBUG
		// Clear out the unused portion of the array.
		// Renting an array may have values in it.
		// This makes it easier when debugging.
		Array.Clear(parsedRows, 0, parsedRows.Length);
		Array.Clear(parsedFields, copyLength, parsedFields.Length - copyLength);
#endif

#if NET6_0_OR_GREATER
		ref var parsedFieldsRef = ref MemoryMarshal.GetArrayDataReference(parsedFields);
#else
		ref var parsedFieldsRef = ref parsedFields[0];
#endif
		for (var i = 0; i < copyLength; i++)
		{
			ref var field = ref Unsafe.Add(ref parsedFieldsRef, i);
			field.Start -= fromParsingRowStart;
		}

		return charsRead;
	}

	public void Parse()
	{
		do
		{
			parsingStrategy.Parse(this);

			if (parsedRowsCount == 0)
			{
				if (charsRead <= bufferPosition)
				{
					// There was only a single small row with no line ending.
					AddField(charsRead);
					AddRow();
				}
				else
				{
					// Buffer must hold at least one row.
					ArrayExtensions.Resize(ref buffer);
					charsRead += buffer.Fill(reader, charsRead);
				}
			}
		}
		while (parsedRowsCount < 1 && charsRead > 0);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void AddField(int charIndex)
	{
		if (parsedFieldsCount == parsedFields.Length)
		{
			// Hold a full buffer worth of fields.
			ArrayExtensions.Resize(ref parsedFields);
		}

		ref var field = ref parsedFields[parsedFieldsCount];
		field.Start = parsingFieldStart;
		field.Length = charIndex - parsingFieldStart;
		field.EscapeMask = parsingEscapeMask;
		field.EscapeLength = 0;
		field.IsInvalid = parsingFieldIsInvalid;

		parsingInEscape = false;
		parsingFieldIsInvalid = false;
		parsingFieldPrevEscapeIndex = -2;
		parsingEscapeMask = 0;
		parsingFieldStart = charIndex + 1;
		parsingFieldsCount++;
		parsedFieldsCount++;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal void AddRow()
	{
		if (parsedRowsCount == parsedRows.Length)
		{
			// Hold a full buffer worth of rows.
			ArrayExtensions.Resize(ref parsedRows);
		}

		ref var row = ref parsedRows[parsedRowsCount];
		row.Start = parsingRowStart;
		row.Length = parsingFieldStart - parsingRowStart;
		row.FieldCount = parsingFieldsCount;

		parsingEscapeMask = 0; // TODO: Can this be removed?
		parsingFieldsCount = 0;
		parsingRowStart = parsingFieldStart;
		parsedRowsCount++;
	}

	public string ToString(int index)
	{
		ref var field = ref parsedFields[parsedFieldsPosition + index];
		if (field.Length == 0)
		{
			return string.Empty;
		}

		var span = field.EscapeMask > 0
			? EscapeField(buffer, ref field)
			: new Span<char>(buffer, field.Start, field.Length);

		return stringCreator(span, index);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Span<char> GetField(int index)
	{
		ref var field = ref parsedFields[parsedFieldsPosition + index];

		var span = field.EscapeMask > 0
			? EscapeField(buffer, ref field)
			: new Span<char>(buffer, field.Start, field.Length);

		return span;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Span<char> GetRow()
	{
		ref var row = ref parsedRows[parsedRowsPosition];

		return new Span<char>(buffer, row.Start, row.Length);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static Span<char> EscapeField(char[] buffer, ref CsvField field)
	{
		if (field.EscapeLength > 0)
		{
			return new Span<char>(field.EscapeBuffer, 0, field.EscapeLength);
		}

		var fieldStart = field.Start;
		var fieldLength = field.Length;
		var fieldMask = field.EscapeMask;
		if (field.EscapeBuffer == null || field.EscapeBuffer.Length < fieldLength)
		{
			if (field.EscapeBuffer != null)
			{
				ArrayPool<char>.Shared.Return(field.EscapeBuffer);
			}

			field.EscapeBuffer = ArrayPool<char>.Shared.Rent(fieldLength);
		}

		var fieldEscapeBufferSpan = new Span<char>(field.EscapeBuffer, 0, fieldLength);
		var fieldSpan = new Span<char>(buffer, fieldStart, fieldLength);

		int length;
		int prevIndex = -1;
		int index;
		var charCount = 0;
		do
		{
			index = BitOperations.TrailingZeroCount(fieldMask);
			fieldMask = fieldMask & (fieldMask - 1);

			length = index - prevIndex - 1;
			fieldSpan.Slice(prevIndex + 1, length).CopyTo(fieldEscapeBufferSpan.Slice(charCount, length));
			charCount += length;

			prevIndex = index;
		}
		while (fieldMask != 0);

		// Copy the rest of the field.
		length = fieldLength - prevIndex - 1;
		fieldSpan.Slice(prevIndex + 1, length).CopyTo(fieldEscapeBufferSpan.Slice(charCount, length));
		charCount += length;

		field.EscapeLength = charCount;

		return fieldEscapeBufferSpan.Slice(0, charCount);
	}

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(isDisposing: true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool isDisposing)
	{
		if (isDisposed)
		{
			return;
		}

		if (isDisposing)
		{
			// Dispose managed state (managed objects)
			ArrayPool<char>.Shared.Return(buffer);
			ArrayPool<CsvRow>.Shared.Return(parsedRows);
			ArrayPool<CsvField>.Shared.Return(parsedFields);
		}

		// Free unmanaged resources (unmanaged objects) and override finalizer
		// Set large fields to null
		isDisposed = true;
	}
}
