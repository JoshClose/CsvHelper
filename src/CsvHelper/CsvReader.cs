using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;

namespace CsvHelper
{
	public class CsvReader : ICsvReader
	{
		private bool disposed;
		private StreamReader reader;
		private readonly ICsvParser parser;
		private IList<string> previousRecord;
		private IList<string> currentRecord;
		private IList<string> headerRecord;

		public bool HasHeaders { get; set; }

		public CsvReader( ICsvParser parser, StreamReader reader )
		{
			this.reader = reader;
			this.parser = parser;
		}

		public CsvReader( ICsvParser parser, string filePath ) : this( parser, new StreamReader( File.OpenRead( filePath ) ) )
		{
		}

		public bool Read()
		{
			CheckDisposed();

			previousRecord = currentRecord;

			currentRecord = parser.Read();
			if( previousRecord == null && currentRecord != null && HasHeaders )
			{
				headerRecord = currentRecord;
				currentRecord = parser.Read();
			}

			return currentRecord != null;
		}

		public T GetField<T>( int index )
		{
			CheckDisposed();

			var converter = TypeDescriptor.GetConverter( typeof( T ) );
			return (T)converter.ConvertFrom( currentRecord[index] );
		}

		public T GetField<T>( string name )
		{
			CheckDisposed();

			if( !HasHeaders )
			{
				throw new InvalidOperationException( "There is no header record to determine the index by a name." );
			}
			var index = headerRecord.IndexOf( name );
			return GetField<T>( index );
		}

		public List<T> GetRecord<T>()
		{
			throw new NotImplementedException();
		}

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
	}
}
