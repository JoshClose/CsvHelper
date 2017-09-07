// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System;
using System.Collections.Generic;
using System.IO;

namespace CsvHelper
{
	/// <summary>
	/// CSV writing state.
	/// </summary>
	public class WritingContext : IWriterContext, ISerializerContext, IDisposable
    {
		private bool disposed;
		private TextWriter writer;
		private Configuration.Configuration configuration;

		internal Dictionary<Type, Delegate> TypeActions { get; } = new Dictionary<Type, Delegate>();

		internal Dictionary<Type, TypeConverterOptions> TypeConverterOptionsCache { get; } = new Dictionary<Type, TypeConverterOptions>();

		internal MemberMapData ReusableMemberMapData { get; } = new MemberMapData( null );

		/// <summary>
		/// Gets the writer configuration.
		/// </summary>
		public virtual IWriterConfiguration WriterConfiguration => configuration;

		/// <summary>
		/// Gets the serializer configuration.
		/// </summary>
		public virtual ISerializerConfiguration SerializerConfiguration => configuration;

		/// <summary>
		/// Gets the <see cref="TextWriter"/>.
		/// </summary>
		public virtual TextWriter Writer => writer;

		/// <summary>
		/// Gets a value indicating if the <see cref="Writer"/>
		/// should be left open when disposing.
		/// </summary>
		public virtual bool LeaveOpen { get; internal set; }

		/// <summary>
		/// Gets the current row.
		/// </summary>
		public virtual int Row { get; internal set; } = 1;

		/// <summary>
		/// Get the current record;
		/// </summary>
		public virtual List<string> Record { get; } = new List<string>();

		/// <summary>
		/// Gets a value indicating if the header has been written.
		/// </summary>
		public virtual bool HasHeaderBeenWritten { get; internal set; }

		/// <summary>
		/// Gets a value indicating if a record has been written.
		/// </summary>
		public virtual bool HasRecordBeenWritten { get; internal set; }

		internal WritingContext( TextWriter writer, Configuration.Configuration configuration, bool leaveOpen )
		{
			this.writer = writer ?? throw new ArgumentNullException( nameof( writer ) );
			this.configuration = configuration ?? throw new ArgumentNullException( nameof( configuration ) );
			LeaveOpen = leaveOpen;
		}

		/// <summary>
		/// Clears the specified caches.
		/// </summary>
		/// <param name="cache">The caches to clear.</param>
		public void ClearCache( Caches cache )
		{
			if( cache.HasFlag( Caches.TypeConverterOptions ) )
			{
				TypeConverterOptionsCache.Clear();
			}

			if( cache.HasFlag( Caches.WriteRecord ) )
			{
				TypeActions.Clear();
			}
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
			if( disposed )
			{
				return;
			}

			if( disposing )
			{
				writer?.Dispose();
			}

			writer = null;
			disposed = true;
		}
	}
}
