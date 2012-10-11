// Copyright 2009-2012 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
using System;
using System.Collections.Generic;
using System.IO;
using CsvHelper.Configuration;

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
				return ReadLine();
			}
			catch( Exception ex )
			{
				throw new CsvParserException( string.Format( "A parsing error occurred. Line: {0} Character: {1}", currentRow, currentCharacter ), ex );
			}
		}

		/// <summary>
		/// Adds the field to the current record.
		/// </summary>
		/// <param name="recordPosition">The record position to add the field to.</param>
		/// <param name="field">The field to add.</param>
		/// <param name="hasQuotes">True if the field is quoted, otherwise false.</param>
		protected virtual void AddFieldToRecord( ref int recordPosition, string field, bool hasQuotes )
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
			if( configuration.CountBytes )
			{
				// Get the full string.
				var s = new string( readerBuffer, fieldStartPosition, readerBufferPosition - fieldStartPosition );
				BytePosition += configuration.Encoding.GetByteCount( s );
			}

			field += new string( readerBuffer, fieldStartPosition, length );
		}

		/// <summary>
		/// Reads the line.
		/// </summary>
		/// <returns></returns>
		protected virtual string[] ReadLine()
		{
			string field = null;
			var fieldStartPosition = readerBufferPosition;
			var inQuotes = false;
			var hasQuotes = false;
			var inComment = false;
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
					}

					charsRead = reader.Read( readerBuffer, 0, readerBuffer.Length );
					readerBufferPosition = 0;
					fieldStartPosition = 0;

					if( charsRead == 0 )
					{
						// The end of the stream has been reached.

						if( c != '\r' && c != '\n' && c != '\0' )
						{
							if( c == configuration.Delimiter )
							{
								// Handle an empty field at the end of the row.
								field = "";
							}

							// Make sure the next time through that we don't end up here again.
							c = '\0';
							
							AddFieldToRecord( ref recordPosition, field, hasQuotes );

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

				if( inComment && c != '\r' && c != '\n' )
				{
					// We are on a commented line.
					// Ignore the character.

					if( configuration.CountBytes )
					{
						BytePosition += configuration.Encoding.GetByteCount( new[] { c } );
					}

					continue;
				}
				if( !inQuotes && c == configuration.Delimiter )
				{
					// If we hit the delimiter, we are
					// done reading the field and can
					// add it to the record.
					AppendField( ref field, fieldStartPosition, readerBufferPosition - fieldStartPosition - 1 );
					AddFieldToRecord( ref recordPosition, field, hasQuotes );
					fieldStartPosition = readerBufferPosition;

					field = null;
					hasQuotes = false;
				}
				else if( !inQuotes && ( c == '\r' || c == '\n' ) )
				{
					if( cPrev == '\r' && c == '\n' )
					{
						// We are still on the same line.

						if( configuration.CountBytes )
						{
							BytePosition += configuration.Encoding.GetByteCount( new[] { c } );
						}

						fieldStartPosition = readerBufferPosition;
						continue;
					}

					if( cPrev == '\0' || cPrev == '\r' || cPrev == '\n' || inComment )
					{
						// We have hit a blank line. Ignore it.

						if( configuration.CountBytes )
						{
							BytePosition += configuration.Encoding.GetByteCount( new[] { c } );
						}

						fieldStartPosition = readerBufferPosition;
						inComment = false;
						currentRow++;
						continue;
					}

					// If we hit the end of the record, add 
					// the current field and return the record.
					AppendField( ref field, fieldStartPosition, readerBufferPosition - fieldStartPosition - 1 );
					AddFieldToRecord( ref recordPosition, field, hasQuotes );
					break;
				}
				else if( c == configuration.Quote )
				{
					hasQuotes = true;
					inQuotes = !inQuotes;

					if( fieldStartPosition != readerBufferPosition - 1 )
					{
						// Grab all the field chars before the
						// quote if there are any.
						AppendField( ref field, fieldStartPosition, readerBufferPosition - fieldStartPosition - 1 );
						fieldStartPosition = readerBufferPosition;
					}
					if( cPrev != configuration.Quote || !inQuotes )
					{
						// Set the new field start position to
						// the char after the quote.

						if( configuration.CountBytes )
						{
							BytePosition += configuration.Encoding.GetByteCount( new[] { c } );
						}

						fieldStartPosition = readerBufferPosition;
					}
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
