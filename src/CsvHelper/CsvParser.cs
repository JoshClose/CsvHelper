// Copyright 2009-2012 Josh Close
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
		private int columnCount;
		private string[] record;
		private int currentRow;
		private int currentCharacter;
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
					if( columnCount > 0 && ( columnCount != row.Length || 
#if NET_2_0
						EnumerableHelper.Any( row, field => field == null )
#else
						row.Any( field => field == null ) 
#endif
						) )
					{
						throw ExceptionHelper.GetReaderException<CsvBadDataException>( "An inconsistent number of columns has been detected.", null, this, null, null, null, null );
					}
					columnCount = row.Length;
				}

				return row;
			}
			catch( CsvHelperException )
			{
				// We threw it, so let it go.
				throw;
			}
			catch( Exception ex )
			{
				throw ExceptionHelper.GetReaderException<CsvParserException>( "A parsing error occurred.", ex, this, null, null, null, null );
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
				FieldCount = record.Length;
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
			var inQuotes = false;
			var fieldIsEscaped = false;
			var inComment = false;
			var inDelimeter = false;
			var delimeterPosition = 0;
			var prevCharWasDelimeter = false;
			var recordPosition = 0;
			record = new string[FieldCount];
			currentRow++;
			currentCharacter = 0;

			while( true )
			{
				currentCharacter++;

				if( readerBufferPosition == charsRead )
				{
					// We need to read more of the stream.

					if( fieldStartPosition != readerBufferPosition )
					{
						// The buffer ran out. Take the current
						// text and add it to the field.
						AppendField( ref field, fieldStartPosition, readerBufferPosition - fieldStartPosition );
						UpdateBytePosition( fieldStartPosition, readerBufferPosition - fieldStartPosition );
					}

					charsRead = reader.Read( readerBuffer, 0, readerBuffer.Length );
					readerBufferPosition = 0;
					fieldStartPosition = 0;

					if( charsRead == 0 )
					{
						// The end of the stream has been reached.

						if( c != '\r' && c != '\n' && c != '\0' )
						{
							if( prevCharWasDelimeter )
							{
								// Handle an empty field at the end of the row.
								field = "";
							}

							// Make sure the next time through that we don't end up here again.
							c = '\0';
							
							AddFieldToRecord( ref recordPosition, field );

							return record;
						}

						return null;
					}
				}

				if( c != '\0' )
				{
					cPrev = c;
				}

				c = readerBuffer[readerBufferPosition];
				readerBufferPosition++;
				CharPosition++;

				if( c == configuration.Quote )
				{
					if( !fieldIsEscaped && ( prevCharWasDelimeter || cPrev == '\r' || cPrev == '\n' || cPrev == '\0' ) )
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

				prevCharWasDelimeter = false;

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
				else if( c == configuration.Delimiter[0] || inDelimeter )
				{
					if( !inDelimeter )
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

						inDelimeter = true;
					}
					
					if( delimeterPosition == configuration.Delimiter.Length - 1 )
					{
						// We are done reading the delimeter.

						// Include the delimiter in the byte count.
						UpdateBytePosition( fieldStartPosition, readerBufferPosition - fieldStartPosition );
						inDelimeter = false;
						prevCharWasDelimeter = true;
						delimeterPosition = 0;
						fieldStartPosition = readerBufferPosition;
					}
					else
					{
						delimeterPosition++;
					}
				}
				else if( c == '\r' || c == '\n' )
				{
					if( cPrev == '\r' && c == '\n' )
					{
						// We are still on the same line.

						UpdateBytePosition( fieldStartPosition, readerBufferPosition - fieldStartPosition );

						fieldStartPosition = readerBufferPosition;
						continue;
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
					AppendField( ref field, fieldStartPosition, readerBufferPosition - fieldStartPosition - 1 );
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

			return record;
		}
	}
}
