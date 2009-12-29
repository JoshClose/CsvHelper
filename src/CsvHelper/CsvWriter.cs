#region License
// Copyright 2009 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
#endregion
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace CsvHelper
{
	public class CsvWriter : ICsvWriter
	{
		private bool disposed;
		private char delimiter = ',';
		private readonly List<string> currentRecord = new List<string>();
		private StreamWriter writer;

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
		/// Creates a new CSV writer using the given <see cref="StreamWriter" />.
		/// </summary>
		/// <param name="writer">The writer used to write the CSV file.</param>
		public CsvWriter( StreamWriter writer )
		{
			this.writer = writer;
		}

		/// <summary>
		/// Creates a new CSV writer using the given file path.
		/// </summary>
		/// <param name="filePath">The file path used to write the CSV file.</param>
		public CsvWriter( string filePath ) : this( new StreamWriter( filePath ) ){}

		/// <summary>
		/// Writes the field to the CSV file.
		/// When all fields are written for a record,
		/// <see cref="ICsvWriter.NextRecord" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <typeparam name="T">The type of the field.</typeparam>
		/// <param name="field">The field to write.</param>
		public void WriteField<T>( T field )
		{
			CheckDisposed();

			var converter = TypeDescriptor.GetConverter( typeof( T ) );
			WriteField( field, converter );
		}

		/// <summary>
		/// Writes the field to the CSV file.
		/// When all fields are written for a record,
		/// <see cref="ICsvWriter.NextRecord" /> must be called
		/// to complete writing of the current record.
		/// </summary>
		/// <typeparam name="T">The type of the field.</typeparam>
		/// <param name="field">The field to write.</param>
		/// <param name="converter">The converter used to convert the field into a string.</param>
		public void WriteField<T>( T field, TypeConverter converter )
		{
			CheckDisposed();

			var fieldString = converter.ConvertToString( field );
			if( fieldString.Contains( "\"" ) )
			{
				// All quotes must be doubled.
				fieldString = fieldString.Replace( "\"", "\"\"" );
			}
			if( fieldString.StartsWith( " " ) ||
				fieldString.EndsWith( " " ) ||
				fieldString.Contains( "\"" ) ||
				fieldString.Contains( delimiter.ToString() ) ||
				fieldString.Contains( "\n" ) )
			{
				// Surround the field in double quotes.
				fieldString = string.Format( "\"{0}\"", fieldString );
			}

			currentRecord.Add( fieldString );
		}

		/// <summary>
		/// Ends writing of the current record
		/// and starts a new record. This is used
		/// when manually writing records with <see cref="ICsvWriter.WriteField{T}"/>
		/// </summary>
		public void NextRecord()
		{
			CheckDisposed();

			var record = string.Join( delimiter.ToString(), currentRecord.ToArray() );
			writer.WriteLine( record );
			writer.Flush();
			currentRecord.Clear();
		}

		/// <summary>
		/// Writes the record to the CSV file.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="record">The record to write.</param>
		public void WriteRecord<T>( T record )
		{
			CheckDisposed();

			throw new NotImplementedException();
		}

		/// <summary>
		/// Writes the list of records to the CSV file.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <param name="records">The list of records to write.</param>
		public void WriteRecords<T>( IList<T> records )
		{
			CheckDisposed();

			throw new NotImplementedException();
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
					if( writer != null )
					{
						writer.Dispose();
					}
				}

				disposed = true;
				writer = null;
			}
		}

		private void CheckDisposed()
		{
			if( disposed )
			{
				throw new ObjectDisposedException( GetType().ToString() );
			}
		}
	}
}
