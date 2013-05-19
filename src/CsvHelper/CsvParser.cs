// Copyright 2009-2013 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.IO;
using CsvHelper.Configuration;
#if NET_2_0
using CsvHelper.MissingFrom20;
#endif
#if !NET_2_0
using System.Linq;
#endif

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
		private readonly CsvConfiguration configuration;
		private char cPrev = '\0';
		private char c = '\0';

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
		/// </summary>
		public virtual int Row { get { return currentRow; } }

		/// <summary>
		/// Gets the raw row for the current record that was parsed.
		/// </summary>
		public virtual string RawRecord { get; private set; }

		/// <summary>
		/// Creates a new parser using the given <see cref="StreamReader" />.
		/// </summary>
		/// <param name="reader">The <see cref="StreamReader" /> with the CSV file data.</param>
		public CsvParser( TextReader reader ) : this( reader, new CsvConfiguration() ) {}

		/// <summary>
		/// Creates a new parser using the given <see cref="StreamReader"/>
		/// and <see cref="CsvConfiguration"/>.
		/// </summary>
		/// <param name="reader">The <see cref="StreamReader"/> with teh CSV file data.</param>
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
				var row = ReadLine();

				if( configuration.DetectColumnCountChanges && row != null )
				{
					if( FieldCount > 0 && ( FieldCount != row.Length || 
#if NET_2_0
						EnumerableHelper.Any( row, field => field == null )
#else
						row.Any( field => field == null ) 
#endif
						) )
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
		/// Adds the field to the current record.
		/// </summary>
		/// <param name="recordPosition">The record position to add the field to.</param>
		/// <param name="field">The field to add.</param>
		protected virtual void AddFieldToRecord( ref int recordPosition, string field )
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

			record[recordPosition] = field;
			recordPosition++;
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public virtual void Dispose()
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
			if( !disposed )
			{
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
			var inComment = false;
			var inDelimiter = false;
			var delimiterPosition = 0;
			var prevCharWasDelimiter = false;
			var recordPosition = 0;
			record = new string[FieldCount];
			RawRecord = string.Empty;
			currentRow++;

			while( true )
			{
				cPrev = c;
				var fieldLength = readerBufferPosition - fieldStartPosition;
				var read = GetChar( out c, ref fieldStartPosition, ref rawFieldStartPosition, ref field, prevCharWasDelimiter, ref recordPosition, ref fieldLength );
				if( !read )
				{
					break;
				}
				readerBufferPosition++;
				CharPosition++;

				if( c == configuration.Quote )
				{
					if( !fieldIsEscaped && ( prevCharWasDelimiter || cPrev == '\r' || cPrev == '\n' || cPrev == '\0' ) )
					{
						// The field is escaped only if the first char of
						// the field is a quote.
						fieldIsEscaped = true;
					}

					if( !fieldIsEscaped )
					{
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

					continue;
				}

				prevCharWasDelimiter = false;

				if( fieldIsEscaped && inQuotes )
				{
					// While inside an escaped field,
					// all chars are ignored.
					continue;
				}

				if( cPrev == configuration.Quote )
				{
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
						AppendField( ref field, fieldStartPosition, readerBufferPosition - fieldStartPosition - 1 );
						// Include the comma in the byte count.
						UpdateBytePosition( fieldStartPosition, readerBufferPosition - fieldStartPosition );
						AddFieldToRecord( ref recordPosition, field );
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
						delimiterPosition = 0;
						fieldStartPosition = readerBufferPosition;
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
						GetChar( out cNext, ref fieldStartPosition, ref rawFieldStartPosition, ref field, prevCharWasDelimiter, ref recordPosition, ref fieldLength, true );
						if( cNext == '\n' )
						{
							readerBufferPosition++;
							CharPosition++;
						}
					}

					if( cPrev == '\0' || cPrev == '\r' || cPrev == '\n' || inComment )
					{
						// We have hit a blank line. Ignore it.

						UpdateBytePosition( fieldStartPosition, readerBufferPosition - fieldStartPosition );

						fieldStartPosition = readerBufferPosition;
						inComment = false;
						currentRow++;
						continue;
					}

					// If we hit the end of the record, add 
					// the current field and return the record.
					AppendField( ref field, fieldStartPosition, fieldLength );
					// Include the \r or \n in the byte count.
					UpdateBytePosition( fieldStartPosition, readerBufferPosition - fieldStartPosition );
					AddFieldToRecord( ref recordPosition, field );
					break;
				}
				else if( configuration.AllowComments && c == configuration.Comment && ( cPrev == '\0' || cPrev == '\r' || cPrev == '\n' ) )
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
		/// <param name="isPeek">A value indicating if this call is a peek. If true and the end of the record was found
		/// no record handling will be done.</param>
		/// <returns>A value indicating if read a char was read. True if a char was read, otherwise false.</returns>
		protected bool GetChar( out char ch, ref int fieldStartPosition, ref int rawFieldStartPosition, ref string field, bool prevCharWasDelimiter, ref int recordPosition, ref int fieldLength, bool isPeek = false )
		{
			if( readerBufferPosition == charsRead )
			{
				// We need to read more of the stream.

				// The buffer ran out. Take the current
				// text and add it to the field.
				AppendField( ref field, fieldStartPosition, fieldLength );
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

					if( c != '\r' && c != '\n' && c != '\0' )
					{
						if( prevCharWasDelimiter )
						{
							// Handle an empty field at the end of the row.
							field = "";
						}

						AddFieldToRecord( ref recordPosition, field );
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
	}
}
