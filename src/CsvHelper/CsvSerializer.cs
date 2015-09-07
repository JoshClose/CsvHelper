// Copyright 2009-2015 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// http://csvhelper.com
using System;
using System.IO;
using CsvHelper.Configuration;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used to serialize data into a CSV file.
	/// </summary>
	public class CsvSerializer : ICsvSerializer
	{
		private bool disposed;
		private readonly CsvConfiguration configuration;
		private TextWriter writer;

		/// <summary>
		/// Gets the configuration.
		/// </summary>
		public CsvConfiguration Configuration
		{
			get { return configuration; }
		}

		/// <summary>
		/// Creates a new serializer using the given <see cref="TextWriter"/>.
		/// </summary>
		/// <param name="writer">The <see cref="TextWriter"/> to write the CSV file data to.</param>
		public CsvSerializer( TextWriter writer ) : this( writer, new CsvConfiguration() ) {}

		/// <summary>
		/// Creates a new serializer using the given <see cref="TextWriter"/>
		/// and <see cref="CsvConfiguration"/>.
		/// </summary>
		/// <param name="writer">The <see cref="TextWriter"/> to write the CSV file data to.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvSerializer( TextWriter writer, CsvConfiguration configuration )
		{
			if( writer == null )
			{
				throw new ArgumentNullException( "writer" );
			}

			if( configuration == null )
			{
				throw new ArgumentNullException( "configuration" );
			}

			this.writer = writer;
			this.configuration = configuration;
		}

		/// <summary>
		/// Writes a data to the CSV file.
		/// </summary>
		/// <param name="data">The record to write.</param>
		public void Write(string data)
		{
			CheckDisposed();

			writer.WriteLine(data);
		}

		/// <summary>
		/// Writes a record to the CSV file.
		/// </summary>
		/// <param name="record">The record to write.</param>
		public void Write( string[] record )
		{
			CheckDisposed();

			var recordString = string.Join( configuration.Delimiter, record );
			writer.WriteLine( recordString );
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
				if( writer != null )
				{
					writer.Dispose();
				}
			}

			disposed = true;
			writer = null;
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
	}
}
