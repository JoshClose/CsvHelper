// Copyright 2009-2022 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.Delegates;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Parses a CSV file.
	/// </summary>
	public class CsvParser : IParser, IDisposable
	{
		private readonly FieldCache fieldCache = new();
		private readonly TextReader reader;
		private readonly char quote;
		private readonly char escape;
		private readonly bool countBytes;
		private readonly Encoding encoding;
		private readonly bool ignoreBlankLines;
		private readonly char comment;
		private readonly bool allowComments;
		private readonly BadDataFound badDataFound;
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
		private readonly int? maxProcessFieldBufferSize;
		private readonly int? maxBufferSize;
		private readonly bool processBadDataFields;

		private char delimiterFirstChar;
		private char[] buffer;
		private int bufferSize;
		private int charsRead;
		private int bufferPosition;
		private int rowStartPosition;
		private int fieldStartPosition;
		private bool inQuotes;
		private bool inEscape;
		private Field[] fields;
		private string[] processedFields;
		private bool disposed;
		private int quoteCount;
		private char[] processFieldBuffer;
		private int processFieldBufferSize;
		private ParserState state;
		private int delimiterPosition = 1;
		private int newLinePosition = 1;
		private bool fieldIsBadData;
		private bool fieldIsQuoted;
		private bool isProcessingField;
		private bool isRecordProcessed;
		private string[]? record;

		/// <inheritdoc />
		public bool TooLongRow { get; private set; }

		/// <inheritdoc />
		public long CharCount { get; private set; }

		/// <inheritdoc />
		public long ByteCount { get; private set; }

		/// <inheritdoc />
		public int Row { get; private set; }

		/// <inheritdoc />
		public string[]? Record
		{
			get
			{
				if (isRecordProcessed)
				{
					return this.record;
				}

				if (Count == 0)
				{
					return null;
				}

				var record = new string[Count];

				for (var i = 0; i < record.Length; i++)
				{
					record[i] = this[i];
				}

				this.record = record;
				isRecordProcessed = true;

				return this.record;
			}
		}

		/// <inheritdoc />
		public string RawRecord => new(buffer, rowStartPosition, bufferPosition - rowStartPosition);

		/// <inheritdoc />
		public int Count { get; private set; }

		/// <inheritdoc />
		public int RawRow { get; private set; }

		/// <inheritdoc />
		public string Delimiter { get; private set; }

		/// <inheritdoc />
		public CsvContext Context { get; }

		/// <inheritdoc />
		public IParserConfiguration Configuration { get; }

		/// <inheritdoc />
		public string this[int index]
		{
			get
			{
				if (isProcessingField)
				{
					var message =
							$"You can't access {nameof(IParser)}[int] or {nameof(IParser)}.{nameof(IParser.Record)} inside of the {nameof(BadDataFound)} callback. " +
							$"Use {nameof(BadDataFoundArgs)}.{nameof(BadDataFoundArgs.Field)} and {nameof(BadDataFoundArgs)}.{nameof(BadDataFoundArgs.RawRecord)} instead."
						;

					throw new ParserException(Context, message);
				}

				isProcessingField = true;

				var field = GetField(index);

				isProcessingField = false;

				return field;
			}
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="CsvParser" /> class.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="culture">The culture.</param>
		/// <param name="leaveOpen">if set to <c>true</c> [leave open].</param>
		public CsvParser(TextReader reader, CultureInfo culture, bool leaveOpen = false) : this(
			reader,
			new CsvConfiguration(culture),
			leaveOpen
		)
		{
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="CsvParser" /> class.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="configuration">The configuration.</param>
		/// <param name="leaveOpen">if set to <c>true</c> [leave open].</param>
		public CsvParser(TextReader reader, IParserConfiguration configuration, bool leaveOpen = false)
		{
			this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
			Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

			configuration.Validate();

			Context = new CsvContext(this);

			allowComments = configuration.AllowComments;
			badDataFound = configuration.BadDataFound;
			bufferSize = configuration.BufferSize;
			cacheFields = configuration.CacheFields;
			comment = configuration.Comment;
			countBytes = configuration.CountBytes;
			Delimiter = configuration.Delimiter;
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
			maxProcessFieldBufferSize = configuration.MaxProcessFieldBufferSize;
			maxBufferSize = configuration.MaxBufferSize;
			processBadDataFields = configuration.ProcessBadDataFields;
			newLine = configuration.NewLine;
			newLineFirstChar = configuration.NewLine[0];
			mode = configuration.Mode;
			processFieldBufferSize = configuration.ProcessFieldBufferSize;
			quote = configuration.Quote;
			whiteSpaceChars = configuration.WhiteSpaceChars;
			trimOptions = configuration.TrimOptions;

			buffer = new char[bufferSize];
			processFieldBuffer = new char[processFieldBufferSize];
			fields = new Field[128];
			processedFields = new string[128];
		}

		/// <inheritdoc />
		public bool Read()
		{
			isRecordProcessed = false;
			rowStartPosition = bufferPosition;
			fieldStartPosition = rowStartPosition;
			Count = 0;
			TooLongRow = false;
			quoteCount = 0;
			Row++;
			RawRow++;
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

					if (Row == 1 && detectDelimiter)
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

		private bool FillBuffer()
		{
			if (IsBufferSizeNotExceeded() && rowStartPosition == 0 && CharCount > 0 && charsRead == bufferSize)
			{
				// The record is longer than the memory buffer. Increase the buffer.
				bufferSize *= 2;

				SetBufferToMaxValueIfExceeded();

				var tempBuffer = new char[bufferSize];
				buffer.CopyTo(tempBuffer, 0);
				buffer = tempBuffer;
			}

			var charsLeft = Math.Max(charsRead - rowStartPosition, 0);

			Array.Copy(buffer, rowStartPosition, buffer, 0, charsLeft);

			fieldStartPosition -= rowStartPosition;
			rowStartPosition = 0;
			bufferPosition = charsLeft;

			var lengthToReadFromStream = buffer.Length - charsLeft;
			var startIndexToPlaceInBuffer = charsLeft;

			charsLeft = SetupParserIfTooLongRow(charsLeft, ref lengthToReadFromStream, ref startIndexToPlaceInBuffer);

			charsRead = reader.Read(buffer, startIndexToPlaceInBuffer, lengthToReadFromStream);

			if (charsRead == 0)
			{
				return false;
			}

			charsRead += charsLeft;

			return true;
		}

		private bool IsBufferSizeNotExceeded()
		{
			if (!maxBufferSize.HasValue)
			{
				return true;
			}

			return bufferSize < maxBufferSize;
		}

		private void SetBufferToMaxValueIfExceeded()
		{
			if (maxBufferSize.HasValue && bufferSize > maxBufferSize)
			{
				bufferSize = maxBufferSize.Value;
			}
		}

		private int SetupParserIfTooLongRow(
			int charsLeft,
			ref int lengthToReadFromStream,
			ref int startIndexToPlaceInBuffer
		)
		{
			// Buffer was stuffed during last read and could not be resized because limit set thus whole row can not be loaded to buffer.
			// Continue reading to seek to next row but current row could not be correctly parsed because too long to fit in the buffer.
			if (maxBufferSize.HasValue && buffer.Length == charsLeft && buffer.Length == maxBufferSize)
			{
				lengthToReadFromStream = maxBufferSize.Value;
				startIndexToPlaceInBuffer = 0;
				charsLeft = 0;
				bufferPosition = 0;
				fieldStartPosition = 0;
				TooLongRow = true;
			}

			return charsLeft;
		}

		/// <inheritdoc />
		public async Task<bool> ReadAsync()
		{
			isRecordProcessed = false;
			rowStartPosition = bufferPosition;
			fieldStartPosition = rowStartPosition;
			Count = 0;
			TooLongRow = false;
			quoteCount = 0;
			Row++;
			RawRow++;
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

					if (Row == 1 && detectDelimiter)
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

		private async Task<bool> FillBufferAsync()
		{
			if (IsBufferSizeNotExceeded() && rowStartPosition == 0 && CharCount > 0 && charsRead == bufferSize)
			{
				// The record is longer than the memory buffer. Increase the buffer.
				bufferSize *= 2;

				SetBufferToMaxValueIfExceeded();

				var tempBuffer = new char[bufferSize];
				buffer.CopyTo(tempBuffer, 0);
				buffer = tempBuffer;
			}

			var charsLeft = Math.Max(charsRead - rowStartPosition, 0);

			Array.Copy(buffer, rowStartPosition, buffer, 0, charsLeft);

			fieldStartPosition -= rowStartPosition;
			rowStartPosition = 0;
			bufferPosition = charsLeft;

			var lengthToReadFromStream = buffer.Length - charsLeft;
			var startIndexToPlaceInBuffer = charsLeft;

			charsLeft = SetupParserIfTooLongRow(charsLeft, ref lengthToReadFromStream, ref startIndexToPlaceInBuffer);

			charsRead = await reader.ReadAsync(buffer, startIndexToPlaceInBuffer, lengthToReadFromStream)
				.ConfigureAwait(false);

			if (charsRead == 0)
			{
				return false;
			}

			charsRead += charsLeft;

			return true;
		}

		private void DetectDelimiter()
		{
			var text = new string(buffer, 0, charsRead);
			var newDelimiter = Configuration.GetDelimiter(new GetDelimiterArgs(text, Configuration));

			if (newDelimiter != null)
			{
				Delimiter = newDelimiter;
				delimiterFirstChar = newDelimiter[0];
				Configuration.Validate();
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
							(result == ReadLineResult.Complete &&
							 (state == ParserState.LineEnding || state == ParserState.NewLine))
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
				CharCount++;

				if (countBytes)
				{
					ByteCount += encoding.GetByteCount(new[] { c });
				}

				if (maxFieldSize > 0 && bufferPosition - fieldStartPosition - 1 > maxFieldSize)
				{
					throw new MaxFieldSizeException(Context);
				}

				var isFirstCharOfRow = rowStartPosition == bufferPosition - 1;

				if (isFirstCharOfRow &&
				    ((allowComments && c == comment) ||
				     (ignoreBlankLines &&
				      (((c == '\r' || c == '\n') && !isNewLineSet) || (c == newLineFirstChar && isNewLineSet)))))
				{
					state = ParserState.BlankLine;
					var result = ReadBlankLine(ref c);

					if (result == ReadLineResult.Complete)
					{
						state = ParserState.None;

						continue;
					}

					return ReadLineResult.Incomplete;
				}

				if (mode == CsvMode.RFC4180)
				{
					var isFirstCharOfField = fieldStartPosition == bufferPosition - 1;

					if (isFirstCharOfField)
					{
						if ((trimOptions & TrimOptions.Trim) == TrimOptions.Trim &&
						    ArrayHelper.Contains(whiteSpaceChars, c))
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
							if (c == '\r' || (c == '\n' && cPrev != '\r'))
							{
								RawRow++;
							}

							// We don't care about anything else if we're in quotes.
							continue;
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
				CharCount++;

				if (countBytes)
				{
					ByteCount += encoding.GetByteCount(new[] { c });
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
						Row++;
						RawRow++;
					}

					return result;
				}

				c = buffer[bufferPosition];
				bufferPosition++;
				CharCount++;

				if (countBytes)
				{
					ByteCount += encoding.GetByteCount(new[] { c });
				}
			}

			return ReadLineResult.Incomplete;
		}

		private ReadLineResult ReadDelimiter(ref char c)
		{
			for (var i = delimiterPosition; i < Delimiter.Length; i++)
			{
				if (bufferPosition >= charsRead)
				{
					return ReadLineResult.Incomplete;
				}

				delimiterPosition++;

				c = buffer[bufferPosition];

				if (c != Delimiter[i])
				{
					c = buffer[bufferPosition - 1];
					delimiterPosition = 1;

					return ReadLineResult.Complete;
				}

				bufferPosition++;
				CharCount++;

				if (countBytes)
				{
					ByteCount += encoding.GetByteCount(new[] { c });
				}

				if (bufferPosition >= charsRead)
				{
					return ReadLineResult.Incomplete;
				}
			}

			AddField(fieldStartPosition, bufferPosition - fieldStartPosition - Delimiter.Length);

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
					CharCount++;

					if (countBytes)
					{
						ByteCount += encoding.GetByteCount(new[] { c });
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
				CharCount++;

				if (countBytes)
				{
					ByteCount += encoding.GetByteCount(new[] { c });
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
				AddField(fieldStartPosition, bufferPosition - fieldStartPosition - Delimiter.Length);

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

			return Count > 0;
		}

		private void AddField(int start, int length)
		{
			if (Count >= fields.Length)
			{
				var newSize = fields.Length * 2;
				Array.Resize(ref fields, newSize);
				Array.Resize(ref processedFields, newSize);
			}

			ref var field = ref fields[Count];
			field.Start = start - rowStartPosition;
			field.Length = length;
			field.QuoteCount = quoteCount;
			field.IsBad = fieldIsBadData;
			field.IsProcessed = false;

			Count++;
			quoteCount = 0;
		}

		private string GetField(int index)
		{
			if (index > Count)
			{
				throw new IndexOutOfRangeException();
			}

			ref var field = ref fields[index];

			if (field.Length == 0)
			{
				return string.Empty;
			}

			if (field.IsProcessed)
			{
				return processedFields[index];
			}

			var start = field.Start + rowStartPosition;
			var length = field.Length;
			var quoteCount = field.QuoteCount;

			ProcessedField processedField;

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

			var value = cacheFields
				? fieldCache.GetField(processedField.Buffer, processedField.Start, processedField.Length)
				: new string(processedField.Buffer, processedField.Start, processedField.Length);

			processedFields[index] = value;
			field.IsProcessed = true;

			return value;
		}

		/// <inheritdoc />
		protected ProcessedField ProcessRFC4180Field(int start, int length, int quoteCount)
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

				return new ProcessedField(newStart, GetActualFieldLength(newLength), buffer);
			}

			if (buffer[newStart] != quote ||
			    buffer[newStart + newLength - 1] != quote ||
			    (newLength == 1 && buffer[newStart] == quote))
			{
				// If the field doesn't have quotes on the ends, or the field is a single quote char, it's bad data.
				return ProcessRFC4180BadField(start, length);
			}

			if (lineBreakInQuotedFieldIsBadData)
			{
				for (var i = newStart; i < newStart + newLength; i++)
				{
					if (buffer[i] == '\r' || buffer[i] == '\n')
					{
						return ProcessRFC4180BadField(start, length);
					}
				}
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
				return new ProcessedField(newStart, GetActualFieldLength(newLength), buffer);
			}

			IncreaseProcessFieldBufferLengthIfNecessaryAndAble(newLength);

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

				if (maxProcessFieldBufferSize.HasValue && position == processFieldBufferSize)
				{
					//NOTE: Too long field, too short field buffer, skip it since it wouldn't be processed.
					//Warning: field value would be truncated.
					break;
				}

				processFieldBuffer[position] = c;
				position++;
			}

			return new ProcessedField(0, position, processFieldBuffer);
		}

		/// <summary>
		/// If <see cref="maxProcessFieldBufferSize"/> set, this method takes care not to create longer field's string than field buffer.
		/// </summary>
		/// <param name="newLength"></param>
		/// <returns></returns>
		private int GetActualFieldLength(int newLength)
		{
			var fieldLength = newLength;

			if (maxProcessFieldBufferSize.HasValue && newLength > maxProcessFieldBufferSize)
			{
				fieldLength = maxProcessFieldBufferSize.Value;
			}

			return fieldLength;
		}

		private void IncreaseProcessFieldBufferLengthIfNecessaryAndAble(int newLength)
		{
			if (IsProcessFieldBufferSizeNotExceeded() && newLength > processFieldBuffer.Length)
			{
				// Make sure the field processing buffer is large enough.
				while (newLength > processFieldBufferSize && IsProcessFieldBufferSizeNotExceeded())
				{
					processFieldBufferSize *= 2;
				}

				if (maxProcessFieldBufferSize.HasValue && processFieldBufferSize > maxProcessFieldBufferSize)
				{
					processFieldBufferSize = maxProcessFieldBufferSize.Value;
				}

				processFieldBuffer = new char[processFieldBufferSize];
			}
		}

		private bool IsProcessFieldBufferSizeNotExceeded()
		{
			if (!maxProcessFieldBufferSize.HasValue)
			{
				return true;
			}

			return processFieldBufferSize < maxProcessFieldBufferSize;
		}

		/// <inheritdoc />
		protected ProcessedField ProcessRFC4180BadField(int start, int length)
		{
			// If field is already known to be bad, different rules can be applied.

			var args = new BadDataFoundArgs(new string(buffer, start, length), RawRecord, Context);
			badDataFound?.Invoke(args);

			//NOTE: return empty processed field since this value does not matter
			if (!processBadDataFields)
			{
				return new ProcessedField(0, 0, new[] { '\0' });
			}

			var newStart = start;
			var newLength = length;

			if ((trimOptions & TrimOptions.Trim) == TrimOptions.Trim)
			{
				ArrayHelper.Trim(buffer, ref newStart, ref newLength, whiteSpaceChars);
			}

			if (buffer[newStart] != quote)
			{
				// If the field doesn't start with a quote, don't process it.
				return new ProcessedField(newStart, GetActualFieldLength(newLength), buffer);
			}

			IncreaseProcessFieldBufferLengthIfNecessaryAndAble(newLength);

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

					if (cPrev == quote)
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

				if (maxProcessFieldBufferSize.HasValue && position == processFieldBufferSize)
				{
					//NOTE: Too long field, too short field buffer, skip it since it wouldn't be processed.
					//Warning: field value would be truncated.
					break;
				}

				processFieldBuffer[position] = c;
				position++;
			}

			return new ProcessedField(0, position, processFieldBuffer);
		}

		/// <inheritdoc />
		protected ProcessedField ProcessEscapeField(int start, int length)
		{
			var newStart = start;
			var newLength = length;

			if ((trimOptions & TrimOptions.Trim) == TrimOptions.Trim)
			{
				ArrayHelper.Trim(buffer, ref newStart, ref newLength, whiteSpaceChars);
			}

			IncreaseProcessFieldBufferLengthIfNecessaryAndAble(newLength);

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

				if (maxProcessFieldBufferSize.HasValue && position == processFieldBufferSize)
				{
					//NOTE: Too long field, too short field buffer, skip it since it wouldn't be processed.
					//Warning: field value would be truncated.
					break;
				}

				processFieldBuffer[position] = c;
				position++;
			}

			return new ProcessedField(0, position, processFieldBuffer);
		}

		/// <inheritdoc />
		protected ProcessedField ProcessNoEscapeField(int start, int length)
		{
			var newStart = start;
			var newLength = length;

			if ((trimOptions & TrimOptions.Trim) == TrimOptions.Trim)
			{
				ArrayHelper.Trim(buffer, ref newStart, ref newLength, whiteSpaceChars);
			}

			return new ProcessedField(newStart, GetActualFieldLength(newLength), buffer);
		}

		/// <inheritdoc />
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <inheritdoc />
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

		/// <summary>
		///     Processes a raw field based on configuration.
		///     This will remove quotes, remove escapes, and trim if configured to.
		/// </summary>
		[DebuggerDisplay("Start = {Start}, Length = {Length}, Buffer.Length = {Buffer.Length}")]
		protected readonly struct ProcessedField
		{
			/// <summary>
			///     The start of the field in the buffer.
			/// </summary>
			public readonly int Start;

			/// <summary>
			///     The length of the field in the buffer.
			/// </summary>
			public readonly int Length;

			/// <summary>
			///     The buffer that contains the field.
			/// </summary>
			public readonly char[] Buffer;

			/// <summary>
			///     Creates a new instance of ProcessedField.
			/// </summary>
			/// <param name="start">The start of the field in the buffer.</param>
			/// <param name="length">The length of the field in the buffer.</param>
			/// <param name="buffer">The buffer that contains the field.</param>
			public ProcessedField(int start, int length, char[] buffer)
			{
				Start = start;
				Length = length;
				Buffer = buffer;
			}
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
			///     Starting position of the field.
			///     This is an offset from <see cref="rowStartPosition" />.
			/// </summary>
			public int Start;

			public int Length;

			public int QuoteCount;

			public bool IsBad;

			public bool IsProcessed;
		}
	}
}
