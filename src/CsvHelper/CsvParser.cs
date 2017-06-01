using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration;

namespace CsvHelper
{
	/// <summary>
	/// Parses a CSV file.
	/// </summary>
	public class CsvParser : ICsvParser
	{
		private readonly bool leaveOpen;
		private readonly RecordBuilder record = new RecordBuilder();
		private FieldReader reader;
		private bool disposed;
		private int currentRow;
		private int currentRawRow;
		private int c = -1;
		private readonly ICsvParserConfiguration configuration;

		/// <summary>
		/// Gets the <see cref="ICsvParser.TextReader"/>.
		/// </summary>
		public virtual TextReader TextReader => reader.Reader;

		/// <summary>
		/// Gets the configuration.
		/// </summary>
		public virtual ICsvParserConfiguration Configuration => configuration;

		/// <summary>
		/// Gets the character position that the parser is currently on.
		/// </summary>
		public virtual long CharPosition => reader.CharPosition;

		/// <summary>
		/// Gets the byte position that the parser is currently on.
		/// </summary>
		public virtual long BytePosition => reader.BytePosition;

		/// <summary>
		/// Gets the row of the CSV file that the parser is currently on.
		/// </summary>
		public virtual int Row => currentRow;

		/// <summary>
		/// Gets the row of the CSV file that the parser is currently on.
		/// This is the actual file row.
		/// </summary>
		public virtual int RawRow => currentRawRow;

		/// <summary>
		/// Gets the raw row for the current record that was parsed.
		/// </summary>
		public virtual string RawRecord => reader.RawRecord;

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader" />.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader" /> with the CSV file data.</param>
		public CsvParser( TextReader reader ) : this( reader, new CsvConfiguration(), false ) { }

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader" />.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader" /> with the CSV file data.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvParser( TextReader reader, bool leaveOpen ) : this( reader, new CsvConfiguration(), false ) { }

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader"/> and <see cref="CsvConfiguration"/>.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader"/> with the CSV file data.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvParser( TextReader reader, ICsvParserConfiguration configuration ) : this( reader, configuration, false ) { }

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader"/> and <see cref="CsvConfiguration"/>.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader"/> with the CSV file data.</param>
		/// <param name="configuration">The configuration.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvParser( TextReader reader, ICsvParserConfiguration configuration, bool leaveOpen )
		{
			if( reader == null )
			{
				throw new ArgumentNullException( nameof( reader ) );
			}

			if( configuration == null )
			{
				throw new ArgumentNullException( nameof( configuration ) );
			}

			this.reader = new FieldReader( reader, configuration );
			this.configuration = configuration;
			this.leaveOpen = leaveOpen;
		}

		/// <summary>
		/// Reads a record from the CSV file.
		/// </summary>
		/// <returns>A <see cref="T:String[]" /> of fields for the record read.</returns>
		public virtual string[] Read()
		{
			try
			{
				reader.ClearRawRecord();

				var row = ReadLine();

				return row;
			}
			catch( Exception ex )
			{
				var csvHelperException = ex as CsvHelperException ?? new CsvParserException( "An unexpected error occurred.", ex );
				ExceptionHelper.AddExceptionData( csvHelperException, Row, null, null, null, record.ToArray() );

				throw csvHelperException;
			}
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public virtual void Dispose()
		{
			Dispose( !leaveOpen );
			GC.SuppressFinalize( this );
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <param name="disposing">True if the instance needs to be disposed of.</param>
		protected virtual void Dispose( bool disposing )
		{
			if( disposed )
			{
				return;
			}

			if( disposing )
			{
				reader?.Dispose();
			}

			disposed = true;
			reader = null;
		}

		/// <summary>
		/// Reads a line of the CSV file.
		/// </summary>
		/// <returns>The CSV line.</returns>
		protected virtual string[] ReadLine()
		{
			record.Clear();
			currentRow++;
			currentRawRow++;

			while( true )
			{
				c = reader.GetChar();

				if( c == -1 )
				{
					// We have reached the end of the file.
					if( record.Length > 0 )
					{
						// There was no line break at the end of the file.
						// We need to return the last record first.
						record.Add( reader.GetField() );
						return record.ToArray();
					}

					return null;
				}

				if( record.Length == 0 && ( ( c == configuration.Comment && configuration.AllowComments ) || c == '\r' || c == '\n' ) )
				{
					ReadBlankLine();
					if( !configuration.IgnoreBlankLines )
					{
						break;
					}

					continue;
				}

				if( c == configuration.Quote && !configuration.IgnoreQuotes )
				{
					if( ReadQuotedField() )
					{
						break;
					}
				}
				else
				{
					if( ReadField() )
					{
						break;
					}
				}
			}

			return record.ToArray();
		}

		/// <summary>
		/// Reads a blank line. This accounts for empty lines
		/// and commented out lines.
		/// </summary>
		protected virtual void ReadBlankLine()
		{
			if( configuration.IgnoreBlankLines )
			{
				currentRow++;
			}

			while( true )
			{
				if( c == '\r' || c == '\n' )
				{
					ReadLineEnding();
					reader.SetFieldStart();
					return;
				}

				if( c == -1 )
				{
					return;
				}

				// If the buffer runs, it appends the current data to the field.
				// We don't want to capture any data on a blank line, so we
				// need to set the field start every char.
				reader.SetFieldStart();
				c = reader.GetChar();
			}
		}

		/// <summary>
		/// Reads until a delimiter or line ending is found.
		/// </summary>
		/// <returns>True if the end of the line was found, otherwise false.</returns>
		protected virtual bool ReadField()
		{
			if( c != configuration.Delimiter[0] && c != '\r' && c != '\n' )
			{
				c = reader.GetChar();
			}

			while( true )
			{
				if( c == configuration.Quote && !configuration.IgnoreQuotes)
				{
					reader.IsFieldBad = true;
				}

				if( c == configuration.Delimiter[0] )
				{
					reader.SetFieldEnd( -1 );

					// End of field.
					if( ReadDelimiter() )
					{
						// Set the end of the field to the char before the delimiter.
						record.Add( reader.GetField() );
						return false;
					}
				}
				else if( c == '\r' || c == '\n' )
				{
					// End of line.
					reader.SetFieldEnd( -1 );
					var offset = ReadLineEnding();
					reader.SetRawRecordEnd( offset );
					record.Add( reader.GetField() );

					reader.SetFieldStart( offset );

					return true;
				}
				else if( c == -1 )
				{
					// End of file.
					reader.SetFieldEnd();
					record.Add( reader.GetField() );
					return true;
				}

				c = reader.GetChar();
			}
		}

		/// <summary>
		/// Reads until the field is not quoted and a delimeter is found.
		/// </summary>
		/// <returns>True if the end of the line was found, otherwise false.</returns>
		protected virtual bool ReadQuotedField()
		{
			var inQuotes = true;
			// Set the start of the field to after the quote.
			reader.SetFieldStart();

			while( true )
			{
				// 1,"2" ,3

				var cPrev = c;
				c = reader.GetChar();
				if( c == configuration.Quote )
				{
					inQuotes = !inQuotes;

					if( !inQuotes )
					{
						// Add an offset for the quote.
						reader.SetFieldEnd( -1 );
						reader.AppendField();
						reader.SetFieldStart();
					}

					continue;
				}

				if( inQuotes )
				{
					if( c == '\r' || ( c == '\n' && cPrev != '\r' ) )
					{
						// Inside a quote \r\n is just another character to absorb.
						currentRawRow++;
					}

					if( c == -1 )
					{
						reader.SetFieldEnd();
						record.Add( reader.GetField() );
						return true;
					}
				}

				if( !inQuotes )
				{
					if( c == configuration.Delimiter[0] )
					{
						reader.SetFieldEnd( -1 );

						if( ReadDelimiter() )
						{
							// Add an extra offset because of the end quote.
							record.Add( reader.GetField() );
							return false;
						}
					}
					else if( c == '\r' || c == '\n' )
					{
						reader.SetFieldEnd( -1 );
						var offset = ReadLineEnding();
						reader.SetRawRecordEnd( offset );
						record.Add( reader.GetField() );
						reader.SetFieldStart( offset );
						return true;
					}
					else if( cPrev == configuration.Quote )
					{
						// We're out of quotes. Read the reset of
						// the field like a normal field.
						return ReadField();
					}
				}
			}
		}

		/// <summary>
		/// Reads until the delimeter is done.
		/// </summary>
		/// <returns>True if a delimiter was read. False if the sequence of
		/// chars ended up not being the delimiter.</returns>
		protected virtual bool ReadDelimiter()
		{
			if( c != configuration.Delimiter[0] )
			{
				throw new InvalidOperationException( "Tried reading a delimiter when the first delimiter char didn't match the current char." );
			}

			if( configuration.Delimiter.Length == 1 )
			{
				return true;
			}

			for( var i = 1; i < configuration.Delimiter.Length; i++ )
			{
				c = reader.GetChar();
				if( c != configuration.Delimiter[i] )
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Reads until the line ending is done.
		/// </summary>
		/// <returns>True if more chars were read, otherwise false.</returns>
		protected virtual int ReadLineEnding()
		{
			var fieldStartOffset = 0;
			if( c == '\r' )
			{
				c = reader.GetChar();
				if( c != '\n' && c != -1 )
				{
					// The start needs to be moved back.
					fieldStartOffset--;
				}
			}

			return fieldStartOffset;
		}
	}
}
