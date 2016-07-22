// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.IO;
using CsvHelper.Configuration;
using System.Linq;

namespace CsvHelper
{
	/// <summary>
	/// Parses a CSV file.
	/// </summary>
	public class CsvParser : ICsvParser
	{
		private bool disposed;
		private TextReader reader;
		private readonly char[] readerBuffer;
		private int readerBufferPosition;
		private int charsRead;
		private string[] record;
		private int currentRow;
		private int currentRawRow;
		private readonly CsvConfiguration configuration;
		private char? cPrev;
		private char c = '\0';
		private bool read;
		private bool hasExcelSeparatorBeenRead;

		/// <summary>
		/// Gets the configuration.
		/// </summary>
		public virtual CsvConfiguration Configuration
		{
			get { return configuration; }
		}

		/// <summary>
		/// Gets the field count.
		/// </summary>
		public virtual int FieldCount { get; protected set; }

		/// <summary>
		/// Gets the character position that the parser is currently on.
		/// </summary>
		public virtual long CharPosition { get; protected set; }

		/// <summary>
		/// Gets the byte position that the parser is currently on.
		/// </summary>
		public virtual long BytePosition { get; protected set; }

		/// <summary>
		/// Gets the row of the CSV file that the parser is currently on.
		/// This is the logical CSV row.
		/// </summary>
		public virtual int Row { get { return currentRow; } }

		/// <summary>
		/// Gets the row of the CSV file that the parser is currently on.
		/// This is the actual file row.
		/// </summary>
		public virtual int RawRow { get { return currentRawRow; } }

		/// <summary>
		/// Gets the raw row for the current record that was parsed.
		/// </summary>
		public virtual string RawRecord { get; private set; }

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader" />.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader" /> with the CSV file data.</param>
		public CsvParser( TextReader reader ) : this( reader, new CsvConfiguration() ) {}

		/// <summary>
		/// Creates a new parser using the given <see cref="TextReader"/>
		/// and <see cref="CsvConfiguration"/>.
		/// </summary>
		/// <param name="reader">The <see cref="TextReader"/> with the CSV file data.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvParser( TextReader reader, CsvConfiguration configuration )
		{
			if( reader == null )
			{
				throw new ArgumentNullException( "reader" );
			}

			if( configuration == null )
			{
				throw new ArgumentNullException( "configuration" );
			}

			this.reader = reader;
			this.configuration = configuration;

			readerBuffer = new char[configuration.BufferSize];
		}

		/// <summary>
		/// Reads a record from the CSV file.
		/// </summary>
		/// <returns>A <see cref="List{String}" /> of fields for the record read.
		/// If there are no more records, null is returned.</returns>
		public virtual string[] Read()
		{
			CheckDisposed();

			try
			{
				if( configuration.HasExcelSeparator && !hasExcelSeparatorBeenRead )
				{
					ReadExcelSeparator();
				}

				var row = ReadLine();

				if( configuration.DetectColumnCountChanges && row != null )
				{
					if( FieldCount > 0 && ( FieldCount != row.Length || row.Any( field => field == null ) ) )
					{
						throw new CsvBadDataException( "An inconsistent number of columns has been detected." );
					}
				}

				return row;
			}
			catch( Exception ex )
			{
				ExceptionHelper.AddExceptionDataMessage( ex, this, null, null, null, null );
				throw;
			}
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public void Dispose()
		{
			Dispose( true );
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
				if( reader != null )
				{
					reader.Dispose();
				}
			}

			disposed = true;
			reader = null;
		}

		/// <summary>
		/// Checks if the instance has been disposed of.
		/// </summary>
		/// <exception cref="ObjectDisposedException" />
		protected virtual void CheckDisposed()
		{
			if( disposed )
			{
				throw new ObjectDisposedException( GetType().ToString() );
			}
		}

		/// <summary>
		/// Adds the field to the current record.
		/// </summary>
		/// <param name="recordPosition">The record position to add the field to.</param>
		/// <param name="field">The field to add.</param>
		protected virtual void AddFieldToRecord( ref int recordPosition, string field, ref bool fieldIsBad )
		{
			if( record.Length < recordPosition + 1 )
			{
				// Resize record if it's too small.
				Array.Resize( ref record, recordPosition + 1 );

				// Set the field count. If there is a header
				// record, then we can go by the number of
				// headers there is. If there is no header
				// record, then we can go by the first row.
				// Either way, we're using the first row.
				if( currentRow == 1 )
				{
					FieldCount = record.Length;
				}
			}

			if( fieldIsBad && configuration.ThrowOnBadData )
			{
				throw new CsvBadDataException( string.Format( "Field: '{0}'", field ) );
			}

			if( fieldIsBad && configuration.BadDataCallback != null )
			{
				configuration.BadDataCallback( field );
			}

			fieldIsBad = false;

			record[recordPosition] = field;
			recordPosition++;
		}

		/// <summary>
		/// Appends the current buffer data to the field.
		/// </summary>
		/// <param name="field">The field to append the current buffer to.</param>
		/// <param name="fieldStartPosition">The start position in the buffer that the .</param>
		/// <param name="length">The length.</param>
		protected virtual void AppendField( ref string field, int fieldStartPosition, int length )
		{
			field += new string( readerBuffer, fieldStartPosition, length );
		}

		/// <summary>
		/// Updates the byte position using the data from the reader buffer.
		/// </summary>
		/// <param name="fieldStartPosition">The field start position.</param>
		/// <param name="length">The length.</param>
		protected virtual void UpdateBytePosition( int fieldStartPosition, int length )
		{
			if( configuration.CountBytes )
			{
				BytePosition += configuration.Encoding.GetByteCount( readerBuffer, fieldStartPosition, length );
			}
		}

		/// <summary>
		/// Reads the next line.
		/// </summary>
		/// <returns>The line separated into fields.</returns>
		protected virtual string[] ReadLine()
		{
			string field = null;
			var fieldStartPosition = readerBufferPosition;
			var rawFieldStartPosition = readerBufferPosition;
			var inQuotes = false;
			var fieldIsEscaped = false;
			var fieldIsBad = false;
			var inComment = false;
			var inDelimiter = false;
			var inExcelLeadingZerosFormat = false;
			var delimiterPosition = 0;
			var prevCharWasDelimiter = false;
			var recordPosition = 0;
			record = new string[FieldCount];
			RawRecord = string.Empty;
			currentRow++;
			currentRawRow++;

			while( true )
			{
				if( read )
				{
					cPrev = c;
				}

				var fieldLength = readerBufferPosition - fieldStartPosition;
				read = GetChar( out c, ref fieldStartPosition, ref rawFieldStartPosition, ref field, ref fieldIsBad, 
					prevCharWasDelimiter, ref recordPosition, ref fieldLength, inComment, inDelimiter, inQuotes, false );
				if( !read )
				{
					break;
				}
				readerBufferPosition++;
				CharPosition++;

				#region Use Excel Leading Zeros Format for Numerics
				// This needs to get in the way and parse things completely different
				// from how a normal CSV field works. This horribly ugly.
				if( configuration.UseExcelLeadingZerosFormatForNumerics )
				{
					if( c == '=' && !inExcelLeadingZerosFormat && ( prevCharWasDelimiter || cPrev == '\r' || cPrev == '\n' || cPrev == null ) )
					{
						// The start of the leading zeros format has been hit.

						fieldLength = readerBufferPosition - fieldStartPosition;
						char cNext;
						GetChar( out cNext, ref fieldStartPosition, ref rawFieldStartPosition, ref field, ref fieldIsBad, 
							prevCharWasDelimiter, ref recordPosition, ref fieldLength, inComment, inDelimiter, inQuotes, true );
						if( cNext == '"' )
						{
							inExcelLeadingZerosFormat = true;
							continue;
						}
					}
					else if( inExcelLeadingZerosFormat )
					{
						if( c == '"' && cPrev == '=' || char.IsDigit( c ) )
						{
							// Inside of the field.
						}
						else if( c == '"' )
						{
							// The end of the field has been hit.

							char cNext;
							var peekRead = GetChar( out cNext, ref fieldStartPosition, ref rawFieldStartPosition, ref field, ref fieldIsBad, prevCharWasDelimiter, ref recordPosition, ref fieldLength, inComment, inDelimiter, inQuotes, true );
							if( cNext == configuration.Delimiter[0] || cNext == '\r' || cNext == '\n' || cNext == '\0' )
							{
								AppendField( ref field, fieldStartPosition, readerBufferPosition - fieldStartPosition );
								UpdateBytePosition( fieldStartPosition, readerBufferPosition - fieldStartPosition );
								field = field.Trim( '=', '"' );
								fieldStartPosition = readerBufferPosition;

								if( !peekRead )
								{
									AddFieldToRecord( ref recordPosition, field, ref fieldIsBad );
								}

								inExcelLeadingZerosFormat = false;
							}
						}
						else
						{
							inExcelLeadingZerosFormat = false;
						}

						continue;
					}
				}
				#endregion

				if( c == configuration.Quote && !configuration.IgnoreQuotes )
				{
					if( !fieldIsEscaped && ( prevCharWasDelimiter || cPrev == '\r' || cPrev == '\n' || cPrev == null ) )
					{
						// The field is escaped only if the first char of
						// the field is a quote.
						fieldIsEscaped = true;
					}

					if( !fieldIsEscaped )
					{
						fieldIsBad = true;

						// If the field isn't escaped, the quote
						// is like any other char and we should
						// just ignore it.
						continue;
					}

					inQuotes = !inQuotes;

					if( fieldStartPosition != readerBufferPosition - 1 )
					{
						// Grab all the field chars before the
						// quote if there are any.
						AppendField( ref field, fieldStartPosition, readerBufferPosition - fieldStartPosition - 1 );
						// Include the quote in the byte count.
						UpdateBytePosition( fieldStartPosition, readerBufferPosition - fieldStartPosition );
					}

					if( cPrev != configuration.Quote || !inQuotes )
					{
						if( inQuotes || cPrev == configuration.Quote || readerBufferPosition == 1 )
						{
							// The quote will be uncounted and needs to be catered for if:
							// 1. It's the opening quote
							// 2. It's the closing quote on an empty field ("")
							// 3. It's the closing quote and has appeared as the first character in the buffer
							UpdateBytePosition( fieldStartPosition, readerBufferPosition - fieldStartPosition );
						}

						// Set the new field start position to
						// the char after the quote.
						fieldStartPosition = readerBufferPosition;
					}

					prevCharWasDelimiter = false;

					continue;
				}

				prevCharWasDelimiter = false;

				if( fieldIsEscaped && inQuotes )
				{
					if( c == '\r' || ( c == '\n' && cPrev != '\r' ) )
					{
						currentRawRow++;
					}

					// While inside an escaped field,
					// all chars are ignored.
					continue;
				}

				if( cPrev == configuration.Quote && !configuration.IgnoreQuotes )
				{
					if( c != configuration.Delimiter[0] && c != '\r' && c != '\n' )
					{
						fieldIsBad = true;
					}

					// If we're not in quotes and the
					// previous char was a quote, the
					// field is no longer escaped.
					fieldIsEscaped = false;
				}

				if( inComment && c != '\r' && c != '\n' )
				{
					// We are on a commented line.
					// Ignore the character.
				}
				else if( c == configuration.Delimiter[0] || inDelimiter )
				{
					if( !inDelimiter )
					{
						// If we hit the delimiter, we are
						// done reading the field and can
						// add it to the record.
						delimiterPosition = 0;
						AppendField( ref field, fieldStartPosition, readerBufferPosition - fieldStartPosition - 1 );
						// Include the comma in the byte count.
						UpdateBytePosition( fieldStartPosition, readerBufferPosition - fieldStartPosition );
						AddFieldToRecord( ref recordPosition, field, ref fieldIsBad );
						fieldStartPosition = readerBufferPosition;
						field = null;

						inDelimiter = true;
					}
					
					if( delimiterPosition == configuration.Delimiter.Length - 1 )
					{
						// We are done reading the delimeter.

						// Include the delimiter in the byte count.
						UpdateBytePosition( fieldStartPosition, readerBufferPosition - fieldStartPosition );
						inDelimiter = false;
						prevCharWasDelimiter = true;
						fieldStartPosition = readerBufferPosition;
					}
					else if( configuration.Delimiter[delimiterPosition] != c )
					{
						// We're not actually in a delimiter. Reset things back
						// to the previous field.
						recordPosition--;
						fieldStartPosition -= ( delimiterPosition + 1 );
						inDelimiter = false;
					}
					else
					{
						delimiterPosition++;
					}
				}
				else if( c == '\r' || c == '\n' )
				{
					fieldLength = readerBufferPosition - fieldStartPosition - 1;
					if( c == '\r' )
					{
						char cNext;
						GetChar( out cNext, ref fieldStartPosition, ref rawFieldStartPosition, ref field, ref fieldIsBad, prevCharWasDelimiter, ref recordPosition, ref fieldLength, inComment, inDelimiter, inQuotes, true );
						if( cNext == '\n' )
						{
							readerBufferPosition++;
							CharPosition++;
						}
					}

					if( cPrev == '\r' || cPrev == '\n' || inComment || cPrev == null )
					{
						// We have hit a blank line. Ignore it.

						UpdateBytePosition( fieldStartPosition, readerBufferPosition - fieldStartPosition );

						fieldStartPosition = readerBufferPosition;
						inComment = false;

						if( !configuration.IgnoreBlankLines )
						{
							break;
						}

						// If blank lines are being ignored, we need to add
						// to the row count because we're skipping the row
						// and it won't get added normally.
						currentRow++;

						continue;
					}

					// If we hit the end of the record, add 
					// the current field and return the record.
					AppendField( ref field, fieldStartPosition, fieldLength );
					// Include the \r or \n in the byte count.
					UpdateBytePosition( fieldStartPosition, readerBufferPosition - fieldStartPosition );
					AddFieldToRecord( ref recordPosition, field, ref fieldIsBad );
					break;
				}
				else if( configuration.AllowComments && c == configuration.Comment && ( cPrev == '\r' || cPrev == '\n' || cPrev == null ) )
				{
					inComment = true;
				}
			}

			if( record != null )
			{
				RawRecord += new string( readerBuffer, rawFieldStartPosition, readerBufferPosition - rawFieldStartPosition );
			}

			return record;
		}

		/// <summary>
		/// Gets the current character from the buffer while
		/// advancing the buffer if it ran out.
		/// </summary>
		/// <param name="ch">The char that gets the read char set to.</param>
		/// <param name="fieldStartPosition">The start position of the current field.</param>
		/// <param name="rawFieldStartPosition">The start position of the raw field.</param>
		/// <param name="field">The field.</param>
		/// <param name="prevCharWasDelimiter">A value indicating if the previous char read was a delimiter.</param>
		/// <param name="recordPosition">The position in the record we are currently at.</param>
		/// <param name="fieldLength">The length of the field in the buffer.</param>
		/// <param name="inComment">A value indicating if the row is current a comment row.</param>
		/// <param name="isPeek">A value indicating if this call is a peek. If true and the end of the record was found
		/// no record handling will be done.</param>
		/// <returns>A value indicating if read a char was read. True if a char was read, otherwise false.</returns>
		protected bool GetChar( out char ch, ref int fieldStartPosition, ref int rawFieldStartPosition, ref string field, ref bool fieldIsBad, bool prevCharWasDelimiter, ref int recordPosition, ref int fieldLength, bool inComment, bool inDelimiter, bool inQuotes, bool isPeek )
		{
			if( readerBufferPosition == charsRead )
			{
				// We need to read more of the stream.

				if( !inDelimiter && !inComment )
				{
					// The buffer ran out. Take the current
					// text and add it to the field.
					AppendField( ref field, fieldStartPosition, fieldLength );
				}

				UpdateBytePosition( fieldStartPosition, readerBufferPosition - fieldStartPosition );
				fieldLength = 0;

				RawRecord += new string( readerBuffer, rawFieldStartPosition, readerBufferPosition - rawFieldStartPosition );

				charsRead = reader.Read( readerBuffer, 0, readerBuffer.Length );
				readerBufferPosition = 0;
				fieldStartPosition = 0;
				rawFieldStartPosition = 0;

				if( charsRead == 0 )
				{
					// The end of the stream has been reached.

					if( isPeek )
					{
						// Don't do any record handling because we're just looking ahead
						// and not actually getting the next char to use.
						ch = '\0';
						return false;
					}

					if( ( c != '\r' && c != '\n' && c != '\0' && !inComment ) || inQuotes )
					{
						// If we're in quotes and have reached the end of the file, record the
						// rest of the record and field.

						if( prevCharWasDelimiter )
						{
							// Handle an empty field at the end of the row.
							field = "";
						}

						AddFieldToRecord( ref recordPosition, field, ref fieldIsBad );
					}
					else
					{
						RawRecord = null;
						record = null;
					}

					ch = '\0';
					return false;
				}
			}

			ch = readerBuffer[readerBufferPosition];
			return true;
		}

		/// <summary>
		/// Reads the Excel seperator and sets it to the delimiter.
		/// </summary>
		protected virtual void ReadExcelSeparator()
		{
			// sep=delimiter
			var sepLine = reader.ReadLine();
			if( sepLine != null )
			{
				configuration.Delimiter = sepLine.Substring( 4 );
			}

			hasExcelSeparatorBeenRead = true;
		}
	}
}
