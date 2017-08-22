// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using System;
using System.IO;
using CsvHelper.Configuration;

namespace CsvHelper
{
	/// <summary>
	/// Defines methods used to serialize data into a CSV file.
	/// </summary>
	public class CsvSerializer : ISerializer
	{
		private WritingContext context;
		private bool disposed;

		/// <summary>
		/// Gets the writing context.
		/// </summary>
		public virtual WritingContext Context => context;

		/// <summary>
		/// Gets the configuration.
		/// </summary>
		public virtual ICsvSerializerConfiguration Configuration => context.SerializerConfiguration;

		/// <summary>
		/// Creates a new serializer using the given <see cref="TextWriter"/>.
		/// </summary>
		/// <param name="writer">The <see cref="TextWriter"/> to write the CSV file data to.</param>
		public CsvSerializer( TextWriter writer ) : this( writer, new CsvConfiguration(), false ) { }

		/// <summary>
		/// Creates a new serializer using the given <see cref="TextWriter"/>.
		/// </summary>
		/// <param name="writer">The <see cref="TextWriter"/> to write the CSV file data to.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvSerializer( TextWriter writer, bool leaveOpen ) : this( writer, new CsvConfiguration(), leaveOpen ) { }

		/// <summary>
		/// Creates a new serializer using the given <see cref="TextWriter"/>
		/// and <see cref="CsvConfiguration"/>.
		/// </summary>
		/// <param name="writer">The <see cref="TextWriter"/> to write the CSV file data to.</param>
		/// <param name="configuration">The configuration.</param>
		public CsvSerializer( TextWriter writer, CsvConfiguration configuration ) : this( writer, configuration, false ) { }

		/// <summary>
		/// Creates a new serializer using the given <see cref="TextWriter"/>
		/// and <see cref="CsvConfiguration"/>.
		/// </summary>
		/// <param name="writer">The <see cref="TextWriter"/> to write the CSV file data to.</param>
		/// <param name="configuration">The configuration.</param>
		/// <param name="leaveOpen">true to leave the reader open after the CsvReader object is disposed, otherwise false.</param>
		public CsvSerializer( TextWriter writer, CsvConfiguration configuration, bool leaveOpen )
		{
			context = new WritingContext( writer, configuration, leaveOpen );
		}

		/// <summary>
		/// Writes a record to the CSV file.
		/// </summary>
		/// <param name="record">The record to write.</param>
		public virtual void Write( string[] record )
		{
			for( var i = 0; i < record.Length; i++ )
			{
				if( i > 0 )
				{
					context.Writer.Write( context.SerializerConfiguration.Delimiter );
				}

				context.Writer.Write( record[i] );
			}
		}

		/// <summary>
		/// Writes a new line to the CSV file.
		/// </summary>
		public virtual void WriteLine()
		{
			context.Writer.WriteLine();
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public virtual void Dispose()
		{
			Dispose( !context.LeaveOpen );
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
				context.Dispose();
			}

			context = null;
			disposed = true;
		}
	}
}
