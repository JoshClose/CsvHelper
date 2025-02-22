// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Delegates;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace CsvHelper;

/// <summary>
/// Parses a CSV file.
/// </summary>
public class CsvParser : IParser, IDisposable
{
	private readonly IParserConfiguration configuration;
	private readonly FieldCache fieldCache = new FieldCache();
	private readonly TextReader reader;
	private readonly char quote;
	private readonly char escape;
	private readonly bool countBytes;
	private readonly Encoding encoding;
	private readonly bool ignoreBlankLines;
	private readonly char comment;
	private readonly bool allowComments;
	private readonly BadDataFound? badDataFound;
	private readonly bool lineBreakInQuotedFieldIsBadData;
	private readonly TrimOptions trimOptions;
	private readonly char[] whiteSpaceChars;
	private readonly bool leaveOpen;
	private readonly CsvMode mode;
	private readonly string newLine;
	private readonly char newLineFirstChar;
	private readonly bool isNewLineSet;
	private readonly bool cacheFields;
	private readonly string[] delimiterValues;
	private readonly bool detectDelimiter;
	private readonly double maxFieldSize;

	private string delimiter;
	private char delimiterFirstChar;
	private char[] buffer;
	private int bufferSize;
	private int charsRead;
	private int bufferPosition;
	private int rowStartPosition;
	private int fieldStartPosition;
	private int row;
	private int rawRow;
	private long charCount;
	private long byteCount;
	private bool inQuotes;
	private bool inEscape;
	private Field[] fields;
	private Memory<char>[] processedFields;
	private int fieldsPosition;
	private bool disposed;
	private int quoteCount;
	private char[] processFieldBuffer;
	private int processFieldBufferUsedLength;
	private ParserState state;
	private int delimiterPosition = 1;
	private int newLinePosition = 1;
	private bool fieldIsBadData;
	private bool fieldIsQuoted;
	private bool isProcessingField;
	private bool isRecordProcessed;
	private string[] record = [];

	/// <summary>
	///  Gets a span representing the currently unused space in <see cref="processFieldBuffer"/>.
	/// </summary>
	private Span<char> AvailableProcessFieldBuffer => processFieldBuffer.AsSpan(processFieldBufferUsedLength);

	/// <inheritdoc/>
	public long CharCount => charCount;

	/// <inheritdoc/>
	public long ByteCount => byteCount;

	/// <inheritdoc/>
	public int Row => row;

	/// <inheritdoc/>
	public string[]? Record
	{
		get
		{
			if (isRecordProcessed == true)
			{
				return this.record;
			}

			if (fieldsPosition == 0)
			{
				return null;
			}

			var record = new string[fieldsPosition];

			for (var i = 0; i < record.Length; i++)
			{
				record[i] = this[i];
			}

			this.record = record;
			isRecordProcessed = true;

			return this.record;
		}
	}

	/// <inheritdoc/>
	public string RawRecord => new string(buffer, rowStartPosition, bufferPosition - rowStartPosition);

	/// <inheritdoc/>
	/// <remarks>
	/// WARNING: This is an advanced API. The underlying memory of the returned span may
	/// change upon subsequent calls to <see cref="Read"/>, resulting in undefined behavior.
	/// If this is unclear or you want a consistent view of the raw record at this
	/// point in time, use <see cref="RawRecord"/> instead.
	/// </remarks>
	public ReadOnlySpan<char> RawRecordSpan => buffer.AsSpan(rowStartPosition, bufferPosition - rowStartPosition);

	/// <inheritdoc/>
	public int Count => fieldsPosition;

	/// <inheritdoc/>
	public int RawRow => rawRow;

	/// <inheritdoc/>
	public string Delimiter => delimiter;

	/// <inheritdoc/>
	public CsvContext Context { get; private set; }

	/// <inheritdoc/>
	public IParserConfiguration Configuration => configuration;

	/// <inheritdoc/>
	/// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is negative or greater than or equal to <see cref="Count"/>.</exception>
	public string this[int index]
	{
		get
		{
			ReadOnlySpan<char> fieldSpan = GetFieldSpan(index);

			return cacheFields ? fieldCache.GetField(fieldSpan) : fieldSpan.ToString();
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CsvParser"/> class.
	/// </summary>
	/// <param name="reader">The reader.</param>
	/// <param name="culture">The culture.</param>
	/// <param name="leaveOpen">if set to <c>true</c> [leave open].</param>
	public CsvParser(TextReader reader, CultureInfo culture, bool leaveOpen = false) : this(reader, new CsvConfiguration(culture), leaveOpen) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="CsvParser"/> class.
	/// </summary>
	/// <param name="reader">The reader.</param>
	/// <param name="configuration">The configuration.</param>
	/// <param name="leaveOpen">if set to <c>true</c> [leave open].</param>
	public CsvParser(TextReader reader, IParserConfiguration configuration, bool leaveOpen = false)
	{
		this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
		this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

		configuration.Validate();

		Context = new CsvContext(this);

		allowComments = configuration.AllowComments;
		badDataFound = configuration.BadDataFound;
		bufferSize = configuration.BufferSize;
		cacheFields = configuration.CacheFields;
		comment = configuration.Comment;
		countBytes = configuration.CountBytes;
		delimiter = configuration.Delimiter;
		delimiterFirstChar = configuration.Delimiter[0];
		delimiterValues = configuration.DetectDelimiterValues;
		detectDelimiter = configuration.DetectDelimiter;
		encoding = configuration.Encoding;
		escape = configuration.Escape;
		ignoreBlankLines = configuration.IgnoreBlankLines;
		isNewLineSet = configuration.IsNewLineSet;
		this.leaveOpen = leaveOpen;
		lineBreakInQuotedFieldIsBadData = configuration.LineBreakInQuotedFieldIsBadData;
		maxFieldSize = configuration.MaxFieldSize;
		newLine = configuration.NewLine;
		newLineFirstChar = configuration.NewLine[0];
		mode = configuration.Mode;
		quote = configuration.Quote;
		whiteSpaceChars = configuration.WhiteSpaceChars;
		trimOptions = configuration.TrimOptions;

		buffer = new char[bufferSize];
		processFieldBuffer = new char[configuration.ProcessFieldBufferSize];
		fields = new Field[128];
		processedFields = new Memory<char>[128];
	}

	/// <inheritdoc/>
	public bool Read()
	{
		isRecordProcessed = false;
		rowStartPosition = bufferPosition;
		fieldStartPosition = rowStartPosition;
		fieldsPosition = 0;
		quoteCount = 0;
		processFieldBufferUsedLength = 0;
		row++;
		rawRow++;
		var c = '\0';
		var cPrev = c;

		while (true)
		{
			if (bufferPosition >= charsRead)
			{
				if (!FillBuffer())
				{
					return ReadEndOfFile();
				}

				if (row == 1 && detectDelimiter)
				{
					DetectDelimiter();
				}
			}

			if (ReadLine(ref c, ref cPrev) == ReadLineResult.Complete)
			{
				return true;
			}
		}
	}

	/// <inheritdoc/>
	public async Task<bool> ReadAsync()
	{
		isRecordProcessed = false;
		rowStartPosition = bufferPosition;
		fieldStartPosition = rowStartPosition;
		fieldsPosition = 0;
		quoteCount = 0;
		processFieldBufferUsedLength = 0;
		row++;
		rawRow++;
		var c = '\0';
		var cPrev = c;

		while (true)
		{
			if (bufferPosition >= charsRead)
			{
				if (!await FillBufferAsync().ConfigureAwait(false))
				{
					return ReadEndOfFile();
				}

				if (row == 1 && detectDelimiter)
				{
					DetectDelimiter();
				}
			}

			if (ReadLine(ref c, ref cPrev) == ReadLineResult.Complete)
			{
				return true;
			}
		}
	}

	private void DetectDelimiter()
	{
		var text = new string(buffer, 0, charsRead);
		var newDelimiter = configuration.GetDelimiter(new GetDelimiterArgs(text, configuration));
		if (newDelimiter != null)
		{
			delimiter = newDelimiter;
			delimiterFirstChar = newDelimiter[0];
			configuration.Validate();
		}
	}

	private ReadLineResult ReadLine(ref char c, ref char cPrev)
	{
		while (bufferPosition < charsRead)
		{
			if (state != ParserState.None)
			{
				// Continue the state before doing anything else.
				ReadLineResult result;
				switch (state)
				{
					case ParserState.Spaces:
						result = ReadSpaces(ref c);
						break;
					case ParserState.BlankLine:
						result = ReadBlankLine(ref c);
						break;
					case ParserState.Delimiter:
						result = ReadDelimiter(ref c);
						break;
					case ParserState.LineEnding:
						result = ReadLineEnding(ref c);
						break;
					case ParserState.NewLine:
						result = ReadNewLine(ref c);
						break;
					default:
						throw new InvalidOperationException($"Parser state '{state}' is not valid.");
				}

				var shouldReturn =
					// Buffer needs to be filled.
					result == ReadLineResult.Incomplete ||
					// Done reading row.
					result == ReadLineResult.Complete && (state == ParserState.LineEnding || state == ParserState.NewLine)
				;

				if (result == ReadLineResult.Complete)
				{
					state = ParserState.None;
				}

				if (shouldReturn)
				{
					return result;
				}
			}

			cPrev = c;
			c = buffer[bufferPosition];
			bufferPosition++;
			charCount++;

			if (countBytes)
			{
				byteCount += encoding.GetByteCount(new char[] { c });
			}

			if (maxFieldSize > 0 && bufferPosition - fieldStartPosition - 1 > maxFieldSize)
			{
				throw new MaxFieldSizeException(Context);
			}

			var isFirstCharOfRow = rowStartPosition == bufferPosition - 1;
			if (isFirstCharOfRow && (allowComments && c == comment || ignoreBlankLines && ((c == '\r' || c == '\n') && !isNewLineSet || c == newLineFirstChar && isNewLineSet)))
			{
				state = ParserState.BlankLine;
				var result = ReadBlankLine(ref c);
				if (result == ReadLineResult.Complete)
				{
					state = ParserState.None;

					continue;
				}
				else
				{
					return ReadLineResult.Incomplete;
				}
			}

			if (mode == CsvMode.RFC4180)
			{
				var isFirstCharOfField = fieldStartPosition == bufferPosition - 1;
				if (isFirstCharOfField)
				{
					if ((trimOptions & TrimOptions.Trim) == TrimOptions.Trim && ArrayHelper.Contains(whiteSpaceChars, c))
					{
						// Skip through whitespace. This is so we can process the field later.
						var result = ReadSpaces(ref c);
						if (result == ReadLineResult.Incomplete)
						{
							fieldStartPosition = bufferPosition;
							return result;
						}
					}

					// Fields are only quoted if the first character is a quote.
					// If not, read until a delimiter or newline is found.
					fieldIsQuoted = c == quote;
				}

				if (fieldIsQuoted)
				{
					if (c == quote || c == escape)
					{
						quoteCount++;

						if (!inQuotes && !isFirstCharOfField && cPrev != escape)
						{
							fieldIsBadData = true;
						}
						else if (!fieldIsBadData)
						{
							// Don't process field quotes after bad data has been detected.
							inQuotes = !inQuotes;
						}
					}

					if (inQuotes)
					{
						// If we are in quotes we don't want to do any special
						// processing (e.g. of delimiters) until we hit the ending
						// quote. But the newline logic may vary.

						if (!(c == '\r' || (c == '\n' && cPrev != '\r')))
						{
							// We are not at (the beginning of) a newline,
							// so just keep reading.
							continue;
						}

						rawRow++;

						if (lineBreakInQuotedFieldIsBadData)
						{
							// This newline is not valid within the field.
							// We will consume the newline and then end the
							// field (and the row).
							// This avoids growing the field (and the buffer)
							// until another quote is found.
							fieldIsBadData = true;
						}
						else
						{
							// We are at a newline but it is considered valid
							// within a (quoted) field. We keep reading until
							// we find the closing quote.
							continue;
						}
					}
				}
				else
				{
					if (c == quote || c == escape)
					{
						// If the field isn't quoted but contains a
						// quote or escape, it's has bad data.
						fieldIsBadData = true;
					}
				}
			}
			else if (mode == CsvMode.Escape)
			{
				if (inEscape)
				{
					inEscape = false;

					continue;
				}

				if (c == escape)
				{
					inEscape = true;

					continue;
				}
			}

			if (c == delimiterFirstChar)
			{
				state = ParserState.Delimiter;
				var result = ReadDelimiter(ref c);
				if (result == ReadLineResult.Incomplete)
				{
					return result;
				}

				state = ParserState.None;

				continue;
			}

			if (!isNewLineSet && (c == '\r' || c == '\n'))
			{
				state = ParserState.LineEnding;
				var result = ReadLineEnding(ref c);
				if (result == ReadLineResult.Complete)
				{
					state = ParserState.None;
				}

				return result;
			}

			if (isNewLineSet && c == newLineFirstChar)
			{
				state = ParserState.NewLine;
				var result = ReadNewLine(ref c);
				if (result == ReadLineResult.Complete)
				{
					state = ParserState.None;
				}

				return result;
			}
		}

		return ReadLineResult.Incomplete;
	}

	private ReadLineResult ReadSpaces(ref char c)
	{
		while (ArrayHelper.Contains(whiteSpaceChars, c))
		{
			if (bufferPosition >= charsRead)
			{
				return ReadLineResult.Incomplete;
			}

			c = buffer[bufferPosition];
			bufferPosition++;
			charCount++;
			if (countBytes)
			{
				byteCount += encoding.GetByteCount(new char[] { c });
			}
		}

		return ReadLineResult.Complete;
	}

	private ReadLineResult ReadBlankLine(ref char c)
	{
		while (bufferPosition < charsRead)
		{
			if (c == '\r' || c == '\n')
			{
				var result = ReadLineEnding(ref c);
				if (result == ReadLineResult.Complete)
				{
					rowStartPosition = bufferPosition;
					fieldStartPosition = rowStartPosition;
					row++;
					rawRow++;
				}

				return result;
			}

			c = buffer[bufferPosition];
			bufferPosition++;
			charCount++;
			if (countBytes)
			{
				byteCount += encoding.GetByteCount(new char[] { c });
			}
		}

		return ReadLineResult.Incomplete;
	}

	private ReadLineResult ReadDelimiter(ref char c)
	{
		for (var i = delimiterPosition; i < delimiter.Length; i++)
		{
			if (bufferPosition >= charsRead)
			{
				return ReadLineResult.Incomplete;
			}

			delimiterPosition++;

			c = buffer[bufferPosition];
			if (c != delimiter[i])
			{
				c = buffer[bufferPosition - 1];
				delimiterPosition = 1;

				return ReadLineResult.Complete;
			}

			bufferPosition++;
			charCount++;
			if (countBytes)
			{
				byteCount += encoding.GetByteCount(new[] { c });
			}

			if (bufferPosition >= charsRead)
			{
				return ReadLineResult.Incomplete;
			}
		}

		AddField(fieldStartPosition, bufferPosition - fieldStartPosition - delimiter.Length);

		fieldStartPosition = bufferPosition;
		delimiterPosition = 1;
		fieldIsBadData = false;

		return ReadLineResult.Complete;
	}

	private ReadLineResult ReadLineEnding(ref char c)
	{
		var lessChars = 1;

		if (c == '\r')
		{
			if (bufferPosition >= charsRead)
			{
				return ReadLineResult.Incomplete;
			}

			c = buffer[bufferPosition];

			if (c == '\n')
			{
				lessChars++;
				bufferPosition++;
				charCount++;
				if (countBytes)
				{
					byteCount += encoding.GetByteCount(new char[] { c });
				}
			}
		}

		if (state == ParserState.LineEnding)
		{
			AddField(fieldStartPosition, bufferPosition - fieldStartPosition - lessChars);
		}

		fieldIsBadData = false;

		return ReadLineResult.Complete;
	}

	private ReadLineResult ReadNewLine(ref char c)
	{
		for (var i = newLinePosition; i < newLine.Length; i++)
		{
			if (bufferPosition >= charsRead)
			{
				return ReadLineResult.Incomplete;
			}

			newLinePosition++;

			c = buffer[bufferPosition];
			if (c != newLine[i])
			{
				c = buffer[bufferPosition - 1];
				newLinePosition = 1;

				return ReadLineResult.Complete;
			}

			bufferPosition++;
			charCount++;
			if (countBytes)
			{
				byteCount += encoding.GetByteCount(new[] { c });
			}

			if (bufferPosition >= charsRead)
			{
				return ReadLineResult.Incomplete;
			}
		}

		AddField(fieldStartPosition, bufferPosition - fieldStartPosition - newLine.Length);

		fieldStartPosition = bufferPosition;
		newLinePosition = 1;
		fieldIsBadData = false;

		return ReadLineResult.Complete;
	}

	private bool ReadEndOfFile()
	{
		var state = this.state;
		this.state = ParserState.None;

		if (state == ParserState.BlankLine)
		{
			return false;
		}

		if (state == ParserState.Delimiter)
		{
			AddField(fieldStartPosition, bufferPosition - fieldStartPosition - delimiter.Length);

			fieldStartPosition = bufferPosition;

			AddField(fieldStartPosition, bufferPosition - fieldStartPosition);

			return true;
		}

		if (state == ParserState.LineEnding)
		{
			AddField(fieldStartPosition, bufferPosition - fieldStartPosition - 1);

			return true;
		}

		if (state == ParserState.NewLine)
		{
			AddField(fieldStartPosition, bufferPosition - fieldStartPosition - newLine.Length);

			return true;
		}

		if (rowStartPosition < bufferPosition)
		{
			AddField(fieldStartPosition, bufferPosition - fieldStartPosition);
		}

		return fieldsPosition > 0;
	}

	private void AddField(int start, int length)
	{
		if (fieldsPosition >= fields.Length)
		{
			var newSize = fields.Length * 2;
			Array.Resize(ref fields, newSize);
			Array.Resize(ref processedFields, newSize);
		}

		ref var field = ref fields[fieldsPosition];
		field.Start = start - rowStartPosition;
		field.Length = length;
		field.QuoteCount = quoteCount;
		field.IsBad = fieldIsBadData;
		field.IsProcessed = false;

		fieldsPosition++;
		quoteCount = 0;
	}

	private bool FillBuffer()
	{
		// Don't forget the async method below.

		if (rowStartPosition == 0 && charCount > 0 && charsRead == bufferSize)
		{
			// The record is longer than the memory buffer. Increase the buffer.
			bufferSize *= 2;
			var tempBuffer = new char[bufferSize];
			buffer.CopyTo(tempBuffer, 0);
			buffer = tempBuffer;
		}

		var charsLeft = Math.Max(charsRead - rowStartPosition, 0);

		Array.Copy(buffer, rowStartPosition, buffer, 0, charsLeft);

		fieldStartPosition -= rowStartPosition;
		rowStartPosition = 0;
		bufferPosition = charsLeft;

		charsRead = reader.Read(buffer, charsLeft, buffer.Length - charsLeft);
		if (charsRead == 0)
		{
			return false;
		}

		charsRead += charsLeft;

		return true;
	}

	private async Task<bool> FillBufferAsync()
	{
		if (rowStartPosition == 0 && charCount > 0 && charsRead == bufferSize)
		{
			// The record is longer than the memory buffer. Increase the buffer.
			bufferSize *= 2;
			var tempBuffer = new char[bufferSize];
			buffer.CopyTo(tempBuffer, 0);
			buffer = tempBuffer;
		}

		var charsLeft = Math.Max(charsRead - rowStartPosition, 0);

		Array.Copy(buffer, rowStartPosition, buffer, 0, charsLeft);

		fieldStartPosition -= rowStartPosition;
		rowStartPosition = 0;
		bufferPosition = charsLeft;

		charsRead = await reader.ReadAsync(buffer, charsLeft, buffer.Length - charsLeft).ConfigureAwait(false);
		if (charsRead == 0)
		{
			return false;
		}

		charsRead += charsLeft;

		return true;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// WARNING: This is an advanced API. The underlying memory of the returned span may
	/// change upon subsequent calls to <see cref="Read"/>, resulting in undefined behavior.
	/// If this is unclear or you want a consistent view of the field at this point
	/// in time, use <see cref="this[int]"/> instead.
	/// </remarks>
	/// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is negative or greater than or equal to <see cref="Count"/>.</exception>
	public ReadOnlySpan<char> GetFieldSpan(int index)
	{
		if (index >= fieldsPosition)
		{
			throw new IndexOutOfRangeException($"Index was out of range. Must be non-negative and less than {nameof(Count)}");
		}

		ref var field = ref fields[index];

		if (field.Length == 0)
		{
			return ReadOnlySpan<char>.Empty;
		}

		if (field.IsProcessed)
		{
			return processedFields[index].Span;
		}

		if (isProcessingField)
		{
			// This check is to guard against stack overflow in the case that
			// someone tries to access a (bad and unprocessed) field from
			// within BadDataFound, and that access calls BadDataFound,
			// which then tries to access... (and so on until the process crashes).

			var message =
				$"You can't access {nameof(IParser)}[int], {nameof(IParser)}.{nameof(IParser.GetFieldSpan)} or " +
				$"{nameof(IParser)}.{nameof(IParser.Record)} inside of the {nameof(BadDataFound)} callback. " +
				$"Use {nameof(BadDataFoundArgs)}.{nameof(BadDataFoundArgs.Field)} and {nameof(BadDataFoundArgs)}.{nameof(BadDataFoundArgs.RawRecord)} instead.";

			throw new ParserException(Context, message);
		}

		isProcessingField = true;

		var start = field.Start + rowStartPosition;
		var length = field.Length;
		var quoteCount = field.QuoteCount;

		Memory<char> processedField;
		switch (mode)
		{
			case CsvMode.RFC4180:
				processedField = field.IsBad
					? ProcessRFC4180BadField(start, length)
					: ProcessRFC4180Field(start, length, quoteCount);
				break;
			case CsvMode.Escape:
				processedField = ProcessEscapeField(start, length);
				break;
			case CsvMode.NoEscape:
				processedField = ProcessNoEscapeField(start, length);
				break;
			default:
				throw new InvalidOperationException($"ParseMode '{mode}' is not handled.");
		}

		processedFields[index] = processedField;
		field.IsProcessed = true;

		isProcessingField = false;

		return processedField.Span;
	}

	/// <summary>
	/// Processes a field that complies with RFC4180.
	/// </summary>
	/// <param name="start">The start index of the field.</param>
	/// <param name="length">The length of the field.</param>
	/// <param name="quoteCount">The number of counted quotes.</param>
	/// <returns>The processed field.</returns>
	protected Memory<char> ProcessRFC4180Field(int start, int length, int quoteCount)
	{
		var newStart = start;
		var newLength = length;

		if ((trimOptions & TrimOptions.Trim) == TrimOptions.Trim)
		{
			ArrayHelper.Trim(buffer, ref newStart, ref newLength, whiteSpaceChars);
		}

		if (quoteCount == 0)
		{
			// Not quoted.
			// No processing needed.
			return buffer.AsMemory(newStart, newLength);
		}

		if (buffer[newStart] != quote || buffer[newStart + newLength - 1] != quote || newLength == 1 && buffer[newStart] == quote)
		{
			// If the field doesn't have quotes on the ends, or the field is a single quote char, it's bad data.
			return ProcessRFC4180BadField(start, length);
		}

		// Remove the quotes from the ends.
		newStart += 1;
		newLength -= 2;

		if ((trimOptions & TrimOptions.InsideQuotes) == TrimOptions.InsideQuotes)
		{
			ArrayHelper.Trim(buffer, ref newStart, ref newLength, whiteSpaceChars);
		}

		if (quoteCount == 2)
		{
			// The only quotes are the ends of the field.
			// No more processing is needed.
			return buffer.AsMemory(newStart, newLength);
		}

		EnsureAvailableProcessFieldBuffer(newLength);

		// Remove escapes.
		var inEscape = false;
		var position = 0;
		for (var i = newStart; i < newStart + newLength; i++)
		{
			var c = buffer[i];

			if (inEscape)
			{
				inEscape = false;
			}
			else if (c == escape)
			{
				inEscape = true;

				continue;
			}

			AvailableProcessFieldBuffer[position] = c;
			position++;
		}

		int fieldStartIndex = processFieldBufferUsedLength;
		processFieldBufferUsedLength += position;

		return processFieldBuffer.AsMemory(fieldStartIndex, position);
	}

	/// <summary>
	/// Processes a field that does not comply with RFC4180.
	/// </summary>
	/// <param name="start">The start index of the field.</param>
	/// <param name="length">The length of the field.</param>
	/// <returns>The processed field.</returns>
	protected Memory<char> ProcessRFC4180BadField(int start, int length)
	{
		// If field is already known to be bad, different rules can be applied.
		badDataFound?.Invoke(new BadDataFoundArgs(new string(buffer, start, length), RawRecord, Context));

		var newStart = start;
		var newLength = length;

		if ((trimOptions & TrimOptions.Trim) == TrimOptions.Trim)
		{
			ArrayHelper.Trim(buffer, ref newStart, ref newLength, whiteSpaceChars);
		}

		if (buffer[newStart] != quote)
		{
			// If the field doesn't start with a quote, don't process it.
			return buffer.AsMemory(newStart, newLength);
		}

		EnsureAvailableProcessFieldBuffer(newLength);

		// Remove escapes until the last quote is found.
		var inEscape = false;
		var position = 0;
		var c = '\0';
		var doneProcessing = false;
		for (var i = newStart + 1; i < newStart + newLength; i++)
		{
			var cPrev = c;
			c = buffer[i];

			// a,"b",c
			// a,"b "" c",d
			// a,"b "c d",e

			if (inEscape)
			{
				inEscape = false;

				if (c == quote)
				{
					// Ignore the quote after an escape.
					continue;
				}
				else if (cPrev == quote)
				{
					// The escape and quote are the same character.
					// This is the end of the field.
					// Don't process escapes for the rest of the field.
					doneProcessing = true;
				}
			}

			if (c == escape && !doneProcessing)
			{
				inEscape = true;

				continue;
			}

			AvailableProcessFieldBuffer[position] = c;
			position++;
		}

		int fieldStartIndex = processFieldBufferUsedLength;
		processFieldBufferUsedLength += position;

		return processFieldBuffer.AsMemory(fieldStartIndex, position);
	}

	/// <summary>
	/// Processes an escaped field.
	/// </summary>
	/// <param name="start">The start index of the field.</param>
	/// <param name="length">The length of the field.</param>
	/// <returns>The processed field.</returns>
	protected Memory<char> ProcessEscapeField(int start, int length)
	{
		var newStart = start;
		var newLength = length;

		if ((trimOptions & TrimOptions.Trim) == TrimOptions.Trim)
		{
			ArrayHelper.Trim(buffer, ref newStart, ref newLength, whiteSpaceChars);
		}

		EnsureAvailableProcessFieldBuffer(newLength);

		// Remove escapes.
		var inEscape = false;
		var position = 0;
		for (var i = newStart; i < newStart + newLength; i++)
		{
			var c = buffer[i];

			if (inEscape)
			{
				inEscape = false;
			}
			else if (c == escape)
			{
				inEscape = true;
				continue;
			}

			AvailableProcessFieldBuffer[position] = c;
			position++;
		}

		int fieldStartIndex = processFieldBufferUsedLength;
		processFieldBufferUsedLength += position;

		return processFieldBuffer.AsMemory(fieldStartIndex, position);
	}

	/// <inheritdoc/>
	/// <summary>
	/// Processes an non-escaped field.
	/// </summary>
	/// <param name="start">The start index of the field.</param>
	/// <param name="length">The length of the field.</param>
	/// <returns>The processed field.</returns>
	protected Memory<char> ProcessNoEscapeField(int start, int length)
	{
		var newStart = start;
		var newLength = length;

		if ((trimOptions & TrimOptions.Trim) == TrimOptions.Trim)
		{
			ArrayHelper.Trim(buffer, ref newStart, ref newLength, whiteSpaceChars);
		}

		return buffer.AsMemory(newStart, newLength);
	}

	private void EnsureAvailableProcessFieldBuffer(int length)
	{
		if (AvailableProcessFieldBuffer.Length < length)
		{
			// We iteratively double the buffer size (rather than sizing it exactly
			// to "length") to reduce the number of resizes for subsequent fields.

			int newProcessFieldBufferLength;
			checked
			{
				int minimumSize = processFieldBufferUsedLength + length;

				newProcessFieldBufferLength = 1 + processFieldBufferUsedLength * 2;

				while (newProcessFieldBufferLength < minimumSize)
				{
					newProcessFieldBufferLength *= 2;
				}
			}

			Debug.Assert(newProcessFieldBufferLength > processFieldBuffer.Length);

			Array.Resize(ref processFieldBuffer, newProcessFieldBufferLength);

			Debug.Assert(AvailableProcessFieldBuffer.Length >= length);
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes the object.
	/// </summary>
	/// <param name="disposing">Indicates if the object is being disposed.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (disposed)
		{
			return;
		}

		if (disposing)
		{
			// Dispose managed state (managed objects)

			if (!leaveOpen)
			{
				reader?.Dispose();
			}
		}

		// Free unmanaged resources (unmanaged objects) and override finalizer
		// Set large fields to null

		disposed = true;
	}

	private enum ReadLineResult
	{
		None = 0,
		Complete,
		Incomplete,
	}

	private enum ParserState
	{
		None = 0,
		Spaces,
		BlankLine,
		Delimiter,
		LineEnding,
		NewLine,
	}

	[DebuggerDisplay("Start = {Start}, Length = {Length}, QuoteCount = {QuoteCount}, IsBad = {IsBad}")]
	private struct Field
	{
		/// <summary>
		/// Starting position of the field.
		/// This is an offset from <see cref="rowStartPosition"/>.
		/// </summary>
		public int Start;

		public int Length;

		public int QuoteCount;

		public bool IsBad;

		public bool IsProcessed;
	}
}
