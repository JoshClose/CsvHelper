#region License
// Copyright 2009 Josh Close
// This file is a part of CsvHelper and is licensed under the MS-PL
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html
#endregion
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace CsvHelper
{
	public class CsvReader : ICsvReader
	{
		private bool disposed;
		private bool hasBeenRead;
		private IList<string> previousRecord;
		private IList<string> currentRecord;
		private IList<string> headerRecord;
		private ICsvParser parser;

		/// <summary>
		/// A <see cref="bool" /> value indicating if the CSV file has a header record.
		/// </summary>
		public bool HasHeaderRecord { get; set; }

		/// <summary>
		/// Gets the field headers.
		/// </summary>
		public IList<string> FieldHeaders
		{
			get
			{
				CheckDisposed();
				CheckHasBeenRead();

				return headerRecord;
			}
		}

		/// <summary>
		/// Creates a new CSV reader using the given parser.
		/// </summary>
		/// <param name="parser">The <see cref="ICsvParser" /> use to parse the CSV file.</param>
		public CsvReader( ICsvParser parser )
		{
			this.parser = parser;
		}

		/// <summary>
		/// Advances the reader to the next record.
		/// </summary>
		/// <returns>True if there are more records, otherwise false.</returns>
		public bool Read()
		{
			CheckDisposed();

			hasBeenRead = true;

			previousRecord = currentRecord;

			currentRecord = parser.Read();
			if( previousRecord == null && currentRecord != null && HasHeaderRecord )
			{
				headerRecord = currentRecord;
				currentRecord = parser.Read();
			}

			return currentRecord != null;
		}

		/// <summary>
		/// Gets the field converted to type T at index.
		/// </summary>
		/// <typeparam name="T">The type of the field.</typeparam>
		/// <param name="index">The index of the field.</param>
		/// <returns>The field converted to type T.</returns>
		public T GetField<T>( int index )
		{
			CheckDisposed();
			CheckHasBeenRead();

			var converter = TypeDescriptor.GetConverter( typeof( T ) );
			return (T)converter.ConvertFrom( currentRecord[index] );
		}

		/// <summary>
		/// Gets the field converted to type T at name.
		/// </summary>
		/// <typeparam name="T">The type of the field.</typeparam>
		/// <param name="name">The named index of the field.</param>
		/// <returns>The field converted to type T.</returns>
		public T GetField<T>( string name )
		{
			CheckDisposed();
			CheckHasBeenRead();

			if( !HasHeaderRecord )
			{
				throw new InvalidOperationException( "There is no header record to determine the index by a name." );
			}

			var index = headerRecord.IndexOf( name );
			return GetField<T>( index );
		}

		/// <summary>
		/// Gets the record converted into type T.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <returns>The record converted to type T.</returns>
		public T GetRecord<T>()
		{
			CheckDisposed();
			CheckHasBeenRead();

			throw new NotImplementedException();
		}

		/// <summary>
		/// Gets all the records in the CSV file and
		/// converts each to type T.
		/// </summary>
		/// <typeparam name="T">The type of the record.</typeparam>
		/// <returns>An <see cref="IList{T}" /> of records.</returns>
		public IList<T> GetRecords<T>()
		{
			CheckDisposed();
			CheckHasBeenRead();

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
					if( parser != null )
					{
						parser.Dispose();
					}
				}

				disposed = true;
				parser = null;
			}
		}

		protected void CheckDisposed()
		{
			if( disposed )
			{
				throw new ObjectDisposedException( GetType().ToString() );
			}
		}

		protected void CheckHasBeenRead()
		{
			if( !hasBeenRead )
			{
				throw new InvalidOperationException( "You must call read on the reader before accessing its data." );
			}
		}
	}
}
