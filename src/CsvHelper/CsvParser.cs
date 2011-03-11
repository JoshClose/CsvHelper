#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
using System;
using System.Collections.Generic;
using System.IO;

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

		/// <summary>
		/// Gets the delimiter used to
		/// separate the fields of the CSV records.
		/// </summary>
		public virtual char Delimiter { get; private set; }

		/// <summary>
		/// Gets the quote used to quote fields.
		/// </summary>
		public virtual char Quote { get; private set; }

		/// <summary>
		/// Gets the size of the buffer
		/// used when reading the stream and
		/// creating the fields.
		/// </summary>
		public virtual int BufferSize { get; private set; }

		/// <summary>
		/// Gets the field count.
		/// </summary>
		public virtual int FieldCount { get; private set; }

		/// <summary>
		/// Gets a value indicating if '#'
		/// can be used at the beginning of
		/// a line to denote a line that is
		/// commented out.
		/// </summary>
		public virtual bool AllowComments { get; private set; }

		/// <summary>
		/// Creates a new parser using the given <see cref="StreamReader" />.
		/// </summary>
		/// <param name="reader">The <see cref="StreamReader" /> with the CSV file data.</param>
		public CsvParser( TextReader reader ) : this( reader, new CsvParserOptions()){}

		/// <summary>
		/// Creates a new parser using the given <see cref="StreamReader" />
		/// and <see cref="CsvParserOptions" />.
		/// </summary>
		/// <param name="reader">The <see cref="StreamReader" /> with teh CSV file data.</param>
		/// <param name="options">The <see cref="CsvParserOptions" /> used for parsing the CSV file.</param>
		public CsvParser( TextReader reader, CsvParserOptions options )
		{
			this.reader = reader;
			BufferSize = options.BufferSize;
			Delimiter = options.Delimiter;
			Quote = options.Quote;
			FieldCount = options.FieldCount;
			AllowComments = options.AllowComments;
            
			readerBuffer = new char[options.BufferSize];
		}

		/// <summary>
		/// Reads a record from the CSV file.
		/// </summary>
		/// <returns>A <see cref="List{String}" /> of fields for the record read.
		/// If there are no more records, null is returned.</returns>
		public virtual string[] Read()
		{
			CheckDisposed();

			string field = null;
			var fieldStartPosition = readerBufferPosition;
			var inQuotes = false;
			var hasQuotes = false;
			var inComment = false;
			var recordPosition = 0;
			var c = '\0';
			record = new string[FieldCount];

			while( true )
			{
				var cPrev = c;

				if( readerBufferPosition == charsRead )
				{
					if( fieldStartPosition != readerBufferPosition )
					{
						// The buffer ran out. Take the current
						// text and add it to the field.
						field += new string( readerBuffer, fieldStartPosition, readerBufferPosition - fieldStartPosition );
					}

					charsRead = reader.Read( readerBuffer, 0, readerBuffer.Length );
					readerBufferPosition = 0;
					fieldStartPosition = 0;

					if( charsRead == 0 )
					{
						// The end of the stream has been reached.

						if( !string.IsNullOrEmpty( field ) )
						{
							AddFieldToRecord( ref recordPosition, field, hasQuotes );
							return record;
						}

						return null;
					}
				}

				c = readerBuffer[readerBufferPosition];
				readerBufferPosition++;

				if( inComment && c != '\r' && c != '\n' )
				{
					// We are on a commented line.
					// Ignore the character.
					continue;
				}
				if( !inQuotes && c == Delimiter )
				{
					// If we hit the delimiter, we are
					// done reading the field and can
					// add it to the record.
					field += new string( readerBuffer, fieldStartPosition, readerBufferPosition - fieldStartPosition - 1 );
					AddFieldToRecord( ref recordPosition, field, hasQuotes );
					fieldStartPosition = readerBufferPosition;

					field = null;
					hasQuotes = false;
				}
				else if( !inQuotes && ( c == '\r' || c == '\n' ) )
				{
					if( cPrev == '\0' || cPrev == '\r' || cPrev == '\n' || inComment )
					{
						// We have hit a blank line. Ignore it.
						fieldStartPosition = readerBufferPosition;
						inComment = false;
						continue;
					}

					// If we hit the end of the record, add 
					// the current field and return the record.
					field += new string( readerBuffer, fieldStartPosition, readerBufferPosition - fieldStartPosition - 1 );
					AddFieldToRecord( ref recordPosition, field, hasQuotes );
					break;
				}
				else if( c == Quote )
				{
					hasQuotes = true;
					inQuotes = !inQuotes;

					if( fieldStartPosition != readerBufferPosition - 1 )
					{
						// Grab all the field chars before the
						// quote if there are any.
						field += new string( readerBuffer, fieldStartPosition, readerBufferPosition - fieldStartPosition - 1 );
						fieldStartPosition = readerBufferPosition;
					}
					if( cPrev != Quote || !inQuotes )
					{
						// Set the new field start position to
						// the char after the quote.
						fieldStartPosition = readerBufferPosition;
					}
				}
				else if( AllowComments && c == '#' && ( cPrev == '\0' || cPrev == '\r' || cPrev == '\n' ) )
				{
					inComment = true;
				}
			}

			return record;
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
		/// Checks if the instance has been disposed of.
		/// </summary>
		/// <exception cref="ObjectDisposedException" />
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
		/// Checks if the reader has been read yet.
		/// </summary>
		/// <exception cref="ObjectDisposedException" />
		protected virtual void CheckDisposed()
		{
			if( disposed )
			{
				throw new ObjectDisposedException( GetType().ToString() );
			}
		}
	}
}
