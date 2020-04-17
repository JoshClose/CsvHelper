// Copyright 2009-2020 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.IO;
using CsvHelper.Configuration;
using System.Threading.Tasks;
using System.Globalization;

// This file is generated from a T4 template.
// Modifying it directly won't do you any good.

namespace CsvHelper
{
	/// <summary>
	/// Parses a CSV file.
	/// </summary>
	public partial class CsvParser : IParser
	{
		private ReadingContext context;
		private IFieldReader fieldReader;
		private bool disposed;
		private int c = -1;

		/// <summary>
		/// Gets the reading context.
		/// </summary>
		public virtual ReadingContext Context => context;

		/// <summary>
		/// Gets the configuration.
		/// </summary>
		public virtual IParserConfiguration Configuration => context.ParserConfiguration;

		/// <summary>
		/// Gets the <see cref="FieldReader"/>.
		/// </summary>
		public virtual IFieldReader FieldReader => fieldReader;

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader" />.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader" /> with the CSV file data.</param>
		/// <param name="culture">The culture.</param>
		public CsvParser(TextReader reader, CultureInfo culture) : this(new CsvFieldReader(reader, new Configuration.CsvConfiguration(culture), false)) { }

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader" />.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader" /> with the CSV file data.</param>
		/// <param name="culture">The culture.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvParser(TextReader reader, CultureInfo culture, bool leaveOpen) : this(new CsvFieldReader(reader, new Configuration.CsvConfiguration(culture), leaveOpen)) { }

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader"/> and <see cref="Configuration"/>.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader"/> with the CSV file data.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvParser(TextReader reader, Configuration.CsvConfiguration configuration) : this(new CsvFieldReader(reader, configuration, false)) { }

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader"/> and <see cref="Configuration"/>.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader"/> with the CSV file data.</param>
		/// <param name="configuration">The configuration.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvParser(TextReader reader, Configuration.CsvConfiguration configuration, bool leaveOpen) : this(new CsvFieldReader(reader, configuration, leaveOpen)) { }

		/// <summary>
		/// Creates a new parser using the given <see cref="FieldReader"/>.
		/// </summary>
		/// <param name="fieldReader">The field reader.</param>
		public CsvParser(IFieldReader fieldReader)
		{
			this.fieldReader = fieldReader ?? throw new ArgumentNullException(nameof(fieldReader));
			context = fieldReader.Context as ReadingContext ?? throw new InvalidOperationException($"For {nameof(FieldReader)} to be used in {nameof(CsvParser)}, {nameof(FieldReader.Context)} must also implement {nameof(ReadingContext)}.");
		}
			
		/// <summary>
		/// Reads a record from the CSV file.
		/// </summary>
		/// <returns>A <see cref="T:String[]" /> of fields for the record read.</returns>
		public virtual string[] Read()
		{
			try
			{
				context.ClearCache(Caches.RawRecord);

				var row = ReadLine();

				return row;
			}
			catch (Exception ex)
			{
				throw ex as CsvHelperException ?? new ParserException(context, "An unexpected error occurred.", ex);
			}
		}
			
		/// <summary>
		/// Reads a record from the CSV file asynchronously.
		/// </summary>
		/// <returns>A <see cref="T:String[]" /> of fields for the record read.</returns>
		public virtual async Task<string[]> ReadAsync()
		{
			try
			{
				context.ClearCache(Caches.RawRecord);

				var row = await ReadLineAsync().ConfigureAwait(false);

				return row;
			}
			catch (Exception ex)
			{
				throw ex as CsvHelperException ?? new ParserException(context, "An unexpected error occurred.", ex);
			}
		}
			
		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public virtual void Dispose()
		{
			Dispose(!context?.LeaveOpen ?? true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <param name="disposing">True if the instance needs to be disposed of.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposed)
			{
				return;
			}

			if (disposing)
			{
				fieldReader?.Dispose();
			}

			fieldReader = null;
			context = null;
			disposed = true;
		}
			
		/// <summary>
		/// Reads a line of the CSV file.
		/// </summary>
		/// <returns>The CSV line.</returns>
		protected virtual string[] ReadLine()
		{
			context.RecordBuilder.Clear();
			context.Row++;
			context.RawRow++;

			while (true)
			{
				if (fieldReader.IsBufferEmpty && !fieldReader.FillBuffer())
				{
					// End of file.
					if (context.RecordBuilder.Length > 0)
					{
						// There was no line break at the end of the file.
						// We need to return the last record first.
						context.RecordBuilder.Add(fieldReader.GetField());
						return context.RecordBuilder.ToArray();
					}

					return null;
				}

				c = fieldReader.GetChar();

				if (context.RecordBuilder.Length == 0 && ((c == context.ParserConfiguration.Comment && context.ParserConfiguration.AllowComments) || c == '\r' || c == '\n'))
				{
					ReadBlankLine();
					if (!context.ParserConfiguration.IgnoreBlankLines)
					{
						break;
					}

					continue;
				}

				// Trim start outside of quotes.
				if (c == ' ' && (context.ParserConfiguration.TrimOptions & TrimOptions.Trim) == TrimOptions.Trim)
				{
					ReadSpaces();
					fieldReader.SetFieldStart(-1);
				}

				if (c == context.ParserConfiguration.Quote && !context.ParserConfiguration.IgnoreQuotes)
				{
					if (ReadQuotedField())
					{
						break;
					}
				}
				else
				{
					if (ReadField())
					{
						break;
					}
				}
			}

			return context.RecordBuilder.ToArray();
		}
			
		/// <summary>
		/// Reads a line of the CSV file.
		/// </summary>
		/// <returns>The CSV line.</returns>
		protected virtual async Task<string[]> ReadLineAsync()
		{
			context.RecordBuilder.Clear();
			context.Row++;
			context.RawRow++;

			while (true)
			{
				if (fieldReader.IsBufferEmpty && !await fieldReader.FillBufferAsync().ConfigureAwait(false))
				{
					// End of file.
					if (context.RecordBuilder.Length > 0)
					{
						// There was no line break at the end of the file.
						// We need to return the last record first.
						context.RecordBuilder.Add(fieldReader.GetField());
						return context.RecordBuilder.ToArray();
					}

					return null;
				}

				c = fieldReader.GetChar();

				if (context.RecordBuilder.Length == 0 && ((c == context.ParserConfiguration.Comment && context.ParserConfiguration.AllowComments) || c == '\r' || c == '\n'))
				{
					await ReadBlankLineAsync().ConfigureAwait(false);
					if (!context.ParserConfiguration.IgnoreBlankLines)
					{
						break;
					}

					continue;
				}

				// Trim start outside of quotes.
				if (c == ' ' && (context.ParserConfiguration.TrimOptions & TrimOptions.Trim) == TrimOptions.Trim)
				{
					await ReadSpacesAsync().ConfigureAwait(false);
					fieldReader.SetFieldStart(-1);
				}

				if (c == context.ParserConfiguration.Quote && !context.ParserConfiguration.IgnoreQuotes)
				{
					if (await ReadQuotedFieldAsync().ConfigureAwait(false))
					{
						break;
					}
				}
				else
				{
					if (await ReadFieldAsync().ConfigureAwait(false))
					{
						break;
					}
				}
			}

			return context.RecordBuilder.ToArray();
		}
			
		/// <summary>
		/// Reads a blank line. This accounts for empty lines
		/// and commented out lines.
		/// </summary>
		protected virtual void ReadBlankLine()
		{
			if (context.ParserConfiguration.IgnoreBlankLines)
			{
				context.Row++;
			}

			while (true)
			{
				if (c == '\r' || c == '\n')
				{
					ReadLineEnding();
					fieldReader.SetFieldStart();
					return;
				}

				// If the buffer runs, it appends the current data to the field.
				// We don't want to capture any data on a blank line, so we
				// need to set the field start every char.
				fieldReader.SetFieldStart();

				if (fieldReader.IsBufferEmpty && !fieldReader.FillBuffer())
				{
					// End of file.
					return;
				}

				c = fieldReader.GetChar();
			}
		}
			
		/// <summary>
		/// Reads a blank line. This accounts for empty lines
		/// and commented out lines.
		/// </summary>
		protected virtual async Task ReadBlankLineAsync()
		{
			if (context.ParserConfiguration.IgnoreBlankLines)
			{
				context.Row++;
			}

			while (true)
			{
				if (c == '\r' || c == '\n')
				{
					await ReadLineEndingAsync().ConfigureAwait(false);
					fieldReader.SetFieldStart();
					return;
				}

				// If the buffer runs, it appends the current data to the field.
				// We don't want to capture any data on a blank line, so we
				// need to set the field start every char.
				fieldReader.SetFieldStart();

				if (fieldReader.IsBufferEmpty && !await fieldReader.FillBufferAsync().ConfigureAwait(false))
				{
					// End of file.
					return;
				}

				c = fieldReader.GetChar();
			}
		}
			
		/// <summary>
		/// Reads until a delimiter or line ending is found.
		/// </summary>
		/// <returns>True if the end of the line was found, otherwise false.</returns>
		protected virtual bool ReadField()
		{
			if (c != context.ParserConfiguration.Delimiter[0] && c != '\r' && c != '\n')
			{
				if (fieldReader.IsBufferEmpty && !fieldReader.FillBuffer())
				{
					// End of file.
					fieldReader.SetFieldEnd();

					if (c == ' ' && (context.ParserConfiguration.TrimOptions & TrimOptions.Trim) == TrimOptions.Trim)
					{
						fieldReader.SetFieldStart();
					}

					context.RecordBuilder.Add(fieldReader.GetField());
					return true;
				}

				c = fieldReader.GetChar();
			}

			var inSpaces = false;
			while (true)
			{
				if (c == context.ParserConfiguration.Quote && !context.ParserConfiguration.IgnoreQuotes)
				{
					context.IsFieldBad = true;
				}

				// Trim end outside of quotes.
				if (!inSpaces && c == ' ' && (context.ParserConfiguration.TrimOptions & TrimOptions.Trim) == TrimOptions.Trim)
				{
					inSpaces = true;
					fieldReader.SetFieldEnd(-1);
					fieldReader.AppendField();
					fieldReader.SetFieldStart(-1);
				}
				else if (inSpaces && c != ' ')
				{
					// Hit a non-space char.
					// Need to determine if it's the end of the field or another char.
					inSpaces = false;
					if (c == context.ParserConfiguration.Delimiter[0] || c == '\r' || c == '\n')
					{
						fieldReader.SetFieldStart(-1);
					}
				}

				if (c == context.ParserConfiguration.Delimiter[0])
				{
					fieldReader.SetFieldEnd(-1);

					// End of field.
					if (ReadDelimiter())
					{
						// Set the end of the field to the char before the delimiter.
						context.RecordBuilder.Add(fieldReader.GetField());

						return false;
					}
				}
				else if (c == '\r' || c == '\n')
				{
					// End of line.
					fieldReader.SetFieldEnd(-1);
					var offset = ReadLineEnding();
					fieldReader.SetRawRecordEnd(offset);
					context.RecordBuilder.Add(fieldReader.GetField());

					fieldReader.SetFieldStart(offset);
					fieldReader.SetBufferPosition(offset);

					return true;
				}

				if (fieldReader.IsBufferEmpty && !fieldReader.FillBuffer())
				{
					// End of file.
					fieldReader.SetFieldEnd();

					if (c == ' ' && (context.ParserConfiguration.TrimOptions & TrimOptions.Trim) == TrimOptions.Trim)
					{
						fieldReader.SetFieldStart();
					}

					context.RecordBuilder.Add(fieldReader.GetField());

					return true;
				}

				c = fieldReader.GetChar();
			}
		}
			
		/// <summary>
		/// Reads until a delimiter or line ending is found.
		/// </summary>
		/// <returns>True if the end of the line was found, otherwise false.</returns>
		protected virtual async Task<bool> ReadFieldAsync()
		{
			if (c != context.ParserConfiguration.Delimiter[0] && c != '\r' && c != '\n')
			{
				if (fieldReader.IsBufferEmpty && !await fieldReader.FillBufferAsync().ConfigureAwait(false))
				{
					// End of file.
					fieldReader.SetFieldEnd();

					if (c == ' ' && (context.ParserConfiguration.TrimOptions & TrimOptions.Trim) == TrimOptions.Trim)
					{
						fieldReader.SetFieldStart();
					}

					context.RecordBuilder.Add(fieldReader.GetField());
					return true;
				}

				c = fieldReader.GetChar();
			}

			var inSpaces = false;
			while (true)
			{
				if (c == context.ParserConfiguration.Quote && !context.ParserConfiguration.IgnoreQuotes)
				{
					context.IsFieldBad = true;
				}

				// Trim end outside of quotes.
				if (!inSpaces && c == ' ' && (context.ParserConfiguration.TrimOptions & TrimOptions.Trim) == TrimOptions.Trim)
				{
					inSpaces = true;
					fieldReader.SetFieldEnd(-1);
					fieldReader.AppendField();
					fieldReader.SetFieldStart(-1);
				}
				else if (inSpaces && c != ' ')
				{
					// Hit a non-space char.
					// Need to determine if it's the end of the field or another char.
					inSpaces = false;
					if (c == context.ParserConfiguration.Delimiter[0] || c == '\r' || c == '\n')
					{
						fieldReader.SetFieldStart(-1);
					}
				}

				if (c == context.ParserConfiguration.Delimiter[0])
				{
					fieldReader.SetFieldEnd(-1);

					// End of field.
					if (await ReadDelimiterAsync().ConfigureAwait(false))
					{
						// Set the end of the field to the char before the delimiter.
						context.RecordBuilder.Add(fieldReader.GetField());

						return false;
					}
				}
				else if (c == '\r' || c == '\n')
				{
					// End of line.
					fieldReader.SetFieldEnd(-1);
					var offset = await ReadLineEndingAsync().ConfigureAwait(false);
					fieldReader.SetRawRecordEnd(offset);
					context.RecordBuilder.Add(fieldReader.GetField());

					fieldReader.SetFieldStart(offset);
					fieldReader.SetBufferPosition(offset);

					return true;
				}

				if (fieldReader.IsBufferEmpty && !await fieldReader.FillBufferAsync().ConfigureAwait(false))
				{
					// End of file.
					fieldReader.SetFieldEnd();

					if (c == ' ' && (context.ParserConfiguration.TrimOptions & TrimOptions.Trim) == TrimOptions.Trim)
					{
						fieldReader.SetFieldStart();
					}

					context.RecordBuilder.Add(fieldReader.GetField());

					return true;
				}

				c = fieldReader.GetChar();
			}
		}
			
		/// <summary>
		/// Reads until the field is not quoted and a delimiter is found.
		/// </summary>
		/// <returns>True if the end of the line was found, otherwise false.</returns>
		protected virtual bool ReadQuotedField()
		{
			var inQuotes = true;
			var quoteCount = 1;
			// Set the start of the field to after the quote.
			fieldReader.SetFieldStart();

			while (true)
			{
				var cPrev = c;

				if (fieldReader.IsBufferEmpty && !fieldReader.FillBuffer())
				{
					// End of file.
					fieldReader.SetFieldEnd();
					context.RecordBuilder.Add(fieldReader.GetField());

					return true;
				}

				c = fieldReader.GetChar();

				// Trim start inside quotes.
				if (quoteCount == 1 && c == ' ' && cPrev == context.ParserConfiguration.Quote && (context.ParserConfiguration.TrimOptions & TrimOptions.InsideQuotes) == TrimOptions.InsideQuotes)
				{
					ReadSpaces();
					cPrev = ' ';
					fieldReader.SetFieldStart(-1);
				}

				// Trim end inside quotes.
				if (inQuotes && c == ' ' && (context.ParserConfiguration.TrimOptions & TrimOptions.InsideQuotes) == TrimOptions.InsideQuotes)
				{
					fieldReader.SetFieldEnd(-1);
					fieldReader.AppendField();
					fieldReader.SetFieldStart(-1);
					ReadSpaces();
					cPrev = ' ';

					if (c == context.ParserConfiguration.Escape || c == context.ParserConfiguration.Quote)
					{
						inQuotes = !inQuotes;
						quoteCount++;

						cPrev = c;

						if (fieldReader.IsBufferEmpty && !fieldReader.FillBuffer())
						{
							// End of file.
							fieldReader.SetFieldStart();
							fieldReader.SetFieldEnd();
							context.RecordBuilder.Add(fieldReader.GetField());

							return true;
						}

						c = fieldReader.GetChar();

						if (c == context.ParserConfiguration.Quote)
						{
							// If we find a second quote, this isn't the end of the field.
							// We need to keep the spaces in this case.

							inQuotes = !inQuotes;
							quoteCount++;

							fieldReader.SetFieldEnd(-1);
							fieldReader.AppendField();
							fieldReader.SetFieldStart();

							continue;
						}
						else
						{
							// If there isn't a second quote, this is the end of the field.
							// We need to ignore the spaces.
							fieldReader.SetFieldStart(-1);
						}
					}
				}

				if (inQuotes && c == context.ParserConfiguration.Escape || c == context.ParserConfiguration.Quote)
				{
					inQuotes = !inQuotes;
					quoteCount++;

					if (!inQuotes)
					{
						// Add an offset for the quote.
						fieldReader.SetFieldEnd(-1);
						fieldReader.AppendField();
						fieldReader.SetFieldStart();
					}

					continue;
				}

				if (inQuotes)
				{
					if (c == '\r' || (c == '\n' && cPrev != '\r'))
					{
						if (context.ParserConfiguration.LineBreakInQuotedFieldIsBadData)
						{
							context.ParserConfiguration.BadDataFound?.Invoke(context);
						}

						// Inside a quote \r\n is just another character to absorb.
						context.RawRow++;
					}
				}

				if (!inQuotes)
				{
					// Trim end outside of quotes.
					if (c == ' ' && (context.ParserConfiguration.TrimOptions & TrimOptions.Trim) == TrimOptions.Trim)
					{
						ReadSpaces();
						fieldReader.SetFieldStart(-1);
					}

					if (c == context.ParserConfiguration.Delimiter[0])
					{
						fieldReader.SetFieldEnd(-1);

						if (ReadDelimiter())
						{
							// Add an extra offset because of the end quote.
							context.RecordBuilder.Add(fieldReader.GetField());

							return false;
						}
					}
					else if (c == '\r' || c == '\n')
					{
						fieldReader.SetFieldEnd(-1);
						var offset = ReadLineEnding();
						fieldReader.SetRawRecordEnd(offset);
						context.RecordBuilder.Add(fieldReader.GetField());

						fieldReader.SetFieldStart(offset);
						fieldReader.SetBufferPosition(offset);

						return true;
					}
					else if (cPrev == context.ParserConfiguration.Quote)
					{
						// We're out of quotes. Read the reset of
						// the field like a normal field.
						return ReadField();
					}
				}
			}
		}
			
		/// <summary>
		/// Reads until the field is not quoted and a delimiter is found.
		/// </summary>
		/// <returns>True if the end of the line was found, otherwise false.</returns>
		protected virtual async Task<bool> ReadQuotedFieldAsync()
		{
			var inQuotes = true;
			var quoteCount = 1;
			// Set the start of the field to after the quote.
			fieldReader.SetFieldStart();

			while (true)
			{
				var cPrev = c;

				if (fieldReader.IsBufferEmpty && !await fieldReader.FillBufferAsync().ConfigureAwait(false))
				{
					// End of file.
					fieldReader.SetFieldEnd();
					context.RecordBuilder.Add(fieldReader.GetField());

					return true;
				}

				c = fieldReader.GetChar();

				// Trim start inside quotes.
				if (quoteCount == 1 && c == ' ' && cPrev == context.ParserConfiguration.Quote && (context.ParserConfiguration.TrimOptions & TrimOptions.InsideQuotes) == TrimOptions.InsideQuotes)
				{
					await ReadSpacesAsync().ConfigureAwait(false);
					cPrev = ' ';
					fieldReader.SetFieldStart(-1);
				}

				// Trim end inside quotes.
				if (inQuotes && c == ' ' && (context.ParserConfiguration.TrimOptions & TrimOptions.InsideQuotes) == TrimOptions.InsideQuotes)
				{
					fieldReader.SetFieldEnd(-1);
					fieldReader.AppendField();
					fieldReader.SetFieldStart(-1);
					ReadSpaces();
					cPrev = ' ';

					if (c == context.ParserConfiguration.Escape || c == context.ParserConfiguration.Quote)
					{
						inQuotes = !inQuotes;
						quoteCount++;

						cPrev = c;

						if (fieldReader.IsBufferEmpty && !await fieldReader.FillBufferAsync().ConfigureAwait(false))
						{
							// End of file.
							fieldReader.SetFieldStart();
							fieldReader.SetFieldEnd();
							context.RecordBuilder.Add(fieldReader.GetField());

							return true;
						}

						c = fieldReader.GetChar();

						if (c == context.ParserConfiguration.Quote)
						{
							// If we find a second quote, this isn't the end of the field.
							// We need to keep the spaces in this case.

							inQuotes = !inQuotes;
							quoteCount++;

							fieldReader.SetFieldEnd(-1);
							fieldReader.AppendField();
							fieldReader.SetFieldStart();

							continue;
						}
						else
						{
							// If there isn't a second quote, this is the end of the field.
							// We need to ignore the spaces.
							fieldReader.SetFieldStart(-1);
						}
					}
				}

				if (inQuotes && c == context.ParserConfiguration.Escape || c == context.ParserConfiguration.Quote)
				{
					inQuotes = !inQuotes;
					quoteCount++;

					if (!inQuotes)
					{
						// Add an offset for the quote.
						fieldReader.SetFieldEnd(-1);
						fieldReader.AppendField();
						fieldReader.SetFieldStart();
					}

					continue;
				}

				if (inQuotes)
				{
					if (c == '\r' || (c == '\n' && cPrev != '\r'))
					{
						if (context.ParserConfiguration.LineBreakInQuotedFieldIsBadData)
						{
							context.ParserConfiguration.BadDataFound?.Invoke(context);
						}

						// Inside a quote \r\n is just another character to absorb.
						context.RawRow++;
					}
				}

				if (!inQuotes)
				{
					// Trim end outside of quotes.
					if (c == ' ' && (context.ParserConfiguration.TrimOptions & TrimOptions.Trim) == TrimOptions.Trim)
					{
						await ReadSpacesAsync().ConfigureAwait(false);
						fieldReader.SetFieldStart(-1);
					}

					if (c == context.ParserConfiguration.Delimiter[0])
					{
						fieldReader.SetFieldEnd(-1);

						if (await ReadDelimiterAsync().ConfigureAwait(false))
						{
							// Add an extra offset because of the end quote.
							context.RecordBuilder.Add(fieldReader.GetField());

							return false;
						}
					}
					else if (c == '\r' || c == '\n')
					{
						fieldReader.SetFieldEnd(-1);
						var offset = await ReadLineEndingAsync().ConfigureAwait(false);
						fieldReader.SetRawRecordEnd(offset);
						context.RecordBuilder.Add(fieldReader.GetField());

						fieldReader.SetFieldStart(offset);
						fieldReader.SetBufferPosition(offset);

						return true;
					}
					else if (cPrev == context.ParserConfiguration.Quote)
					{
						// We're out of quotes. Read the reset of
						// the field like a normal field.
						return await ReadFieldAsync().ConfigureAwait(false);
					}
				}
			}
		}
			
		/// <summary>
		/// Reads until the delimiter is done.
		/// </summary>
		/// <returns>True if a delimiter was read. False if the sequence of
		/// chars ended up not being the delimiter.</returns>
		protected virtual bool ReadDelimiter()
		{
			if (c != context.ParserConfiguration.Delimiter[0])
			{
				throw new InvalidOperationException("Tried reading a delimiter when the first delimiter char didn't match the current char.");
			}

			if (context.ParserConfiguration.Delimiter.Length == 1)
			{
				return true;
			}

			var originalC = c;
			var charsRead = 0;
			for (var i = 1; i < context.ParserConfiguration.Delimiter.Length; i++)
			{
				if (fieldReader.IsBufferEmpty && !fieldReader.FillBuffer())
				{
					// End of file.
					return false;
				}

				c = fieldReader.GetChar();
				charsRead++;
				if (c != context.ParserConfiguration.Delimiter[i])
				{
					c = originalC;
					fieldReader.SetBufferPosition(-charsRead);

					return false;
				}
			}

			return true;
		}
			
		/// <summary>
		/// Reads until the delimiter is done.
		/// </summary>
		/// <returns>True if a delimiter was read. False if the sequence of
		/// chars ended up not being the delimiter.</returns>
		protected virtual async Task<bool> ReadDelimiterAsync()
		{
			if (c != context.ParserConfiguration.Delimiter[0])
			{
				throw new InvalidOperationException("Tried reading a delimiter when the first delimiter char didn't match the current char.");
			}

			if (context.ParserConfiguration.Delimiter.Length == 1)
			{
				return true;
			}

			var originalC = c;
			var charsRead = 0;
			for (var i = 1; i < context.ParserConfiguration.Delimiter.Length; i++)
			{
				if (fieldReader.IsBufferEmpty && !await fieldReader.FillBufferAsync().ConfigureAwait(false))
				{
					// End of file.
					return false;
				}

				c = fieldReader.GetChar();
				charsRead++;
				if (c != context.ParserConfiguration.Delimiter[i])
				{
					c = originalC;
					fieldReader.SetBufferPosition(-charsRead);

					return false;
				}
			}

			return true;
		}
			
		/// <summary>
		/// Reads until the line ending is done.
		/// </summary>
		/// <returns>The field start offset.</returns>
		protected virtual int ReadLineEnding()
		{
			if (c != '\r' && c != '\n')
			{
				throw new InvalidOperationException("Tried reading a line ending when the current char is not a \\r or \\n.");
			}

			var fieldStartOffset = 0;
			if (c == '\r')
			{
				if (fieldReader.IsBufferEmpty && !fieldReader.FillBuffer())
				{
					// End of file.
					return fieldStartOffset;
				}

				c = fieldReader.GetChar();
				if (c != '\n' && c != -1)
				{
					// The start needs to be moved back.
					fieldStartOffset--;
				}
			}

			return fieldStartOffset;
		}
			
		/// <summary>
		/// Reads until the line ending is done.
		/// </summary>
		/// <returns>The field start offset.</returns>
		protected virtual async Task<int> ReadLineEndingAsync()
		{
			if (c != '\r' && c != '\n')
			{
				throw new InvalidOperationException("Tried reading a line ending when the current char is not a \\r or \\n.");
			}

			var fieldStartOffset = 0;
			if (c == '\r')
			{
				if (fieldReader.IsBufferEmpty && !await fieldReader.FillBufferAsync().ConfigureAwait(false))
				{
					// End of file.
					return fieldStartOffset;
				}

				c = fieldReader.GetChar();
				if (c != '\n' && c != -1)
				{
					// The start needs to be moved back.
					fieldStartOffset--;
				}
			}

			return fieldStartOffset;
		}
			
		/// <summary>
		/// Reads until a non-space character is found.
		/// </summary>
		/// <returns>True if there is more data to read.
		/// False if the end of the file has been reached.</returns>
		protected virtual bool ReadSpaces()
		{
			while (true)
			{
				if (c != ' ')
				{
					break;
				}

				if (fieldReader.IsBufferEmpty && !fieldReader.FillBuffer())
				{
					// End of file.
					return false;
				}

				c = fieldReader.GetChar();
			}

			return true;
		}
		
		/// <summary>
		/// Reads until a non-space character is found.
		/// </summary>
		/// <returns>True if there is more data to read.
		/// False if the end of the file has been reached.</returns>
		protected virtual async Task<bool> ReadSpacesAsync()
		{
			while (true)
			{
				if (c != ' ')
				{
					break;
				}

				if (fieldReader.IsBufferEmpty && !await fieldReader.FillBufferAsync().ConfigureAwait(false))
				{
					// End of file.
					return false;
				}

				c = fieldReader.GetChar();
			}

			return true;
		}
	}
}
