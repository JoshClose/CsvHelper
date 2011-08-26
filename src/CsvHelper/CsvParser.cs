#region License
// Copyright 2009-2011 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
// http://csvhelper.com
#endregion
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
		private int currentLine;
		private int currentCharacter;
		private readonly CsvConfiguration configuration;

		/// <summary>
		/// Gets or sets the configuration.
		/// </summary>
		public virtual CsvConfiguration Configuration
		{
			get { return configuration; }
		}

		public int FieldCount { get; protected set; }

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
		internal CsvParser( TextReader reader, CsvConfiguration configuration )
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
			catch( Exception ex)
			{
				throw new CsvParserException( string.Format( "A parsing error occurred. Line: {0} Character: {1}", currentLine, currentCharacter ), ex );
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

		protected string[] ReadLine()
		{
			string field = null;
			var fieldStartPosition = readerBufferPosition;
			var inQuotes = false;
			var hasQuotes = false;
			var inComment = false;
			var recordPosition = 0;
			var c = '\0';
			record = new string[FieldCount];
			currentLine++;
			currentCharacter = 0;

			while( true )
			{
				var cPrev = c;
				currentCharacter++;

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

						if( c != '\r' && c != '\n' && c != '\0' && c != configuration.Delimiter )
						{
							AddFieldToRecord( ref recordPosition, field, hasQuotes );
							return record;
						}
                        else if (c == configuration.Delimiter)
						{
						    AddFieldToRecord( ref recordPosition, "", hasQuotes );
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
				if( !inQuotes && c == configuration.Delimiter )
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
				else if (c == configuration.Quote)
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
					if (cPrev != configuration.Quote || !inQuotes)
					{
						// Set the new field start position to
						// the char after the quote.
						fieldStartPosition = readerBufferPosition;
					}
				}
				else if (configuration.AllowComments && c == configuration.Comment && (cPrev == '\0' || cPrev == '\r' || cPrev == '\n'))
				{
					inComment = true;
				}
			}

			return record;
		}
	}
}
