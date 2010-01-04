#region License
// Copyright 2009-2010 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
#endregion
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CsvHelper
{
	public class CsvParser : ICsvParser
	{
		private bool disposed;
		private char delimiter = ',';
		private StreamReader reader;

		/// <summary>
		/// Creates a new parser using the given <see cref="StreamReader" />.
		/// </summary>
		/// <param name="reader">The <see cref="StreamReader" /> with the CSV file data.</param>
		public CsvParser( StreamReader reader )
		{
			this.reader = reader;
		}

		/// <summary>
		/// Creates a new parser using the given file.
		/// </summary>
		/// <param name="filePath">Path to the CSV file.</param>
		public CsvParser( string filePath ) : this( new StreamReader( filePath ) ){}

		/// <summary>
		/// Gets or sets the delimiter used to
		/// separate the fields of the CSV records.
		/// </summary>
		public char Delimiter
		{
			get { return delimiter; }
			set { delimiter = value; }
		}

		/// <summary>
		/// Reads a record from the CSV file.
		/// </summary>
		/// <returns>A <see cref="List{String}" /> of fields for the record read.
		/// If there are no more records, null is returned.</returns>
		public IList<string> Read()
		{
			CheckDisposed();

			var token = new StringBuilder();
			var inQuotes = false;
			List<string> record = null;
			while( true )
			{
				var c = reader.Read();

				if( c == -1 )
				{
					// The end of the stream has been reached.
					return null;
				}

				if( c == '"' )
				{
					inQuotes = !inQuotes;
				}

				if( c == delimiter && !inQuotes )
				{
					// Add this field to the current record.
					AddField( ref record, token.ToString() );

					// Reset field values.
					token = new StringBuilder();
				}
				else if( c == '\n' && !inQuotes )
				{
					if( record == null && token.ToString().Trim().Length == 0 )
					{
						// Skip the record if there are no fields.
						continue;
					}
					AddField( ref record, token.ToString() );
					break;
				}
				else
				{
					token.Append( (char)c );
				}
			}

			return record;
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

		private void CheckDisposed()
		{
			if( disposed )
			{
				throw new ObjectDisposedException( GetType().ToString() );
			}
		}

		private static void AddField( ref List<string> record, string token )
		{
			if( record == null )
			{
				record = new List<string>();
			}

			// CSV fields don't have leading/trailing 
			// whitespace so we trim it before adding.
			token = token.Trim();

			// If this is a quoted field, remove the quotes.
			if( token.StartsWith( "\"" ) && token.EndsWith( "\"" ) )
			{
				token = token.Substring( 1, token.Length - 2 );
			}

			// Quotes are doubled so we need to remove a set.
			token = token.Replace( "\"\"", "\"" );

			// Add the field to the record.
			record.Add( token );
		}
	}
}
