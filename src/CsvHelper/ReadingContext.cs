// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CsvHelper
{
	/// <summary>
	/// CSV reading state.
	/// </summary>
	public class ReadingContext : IReadingContext, IDisposable
	{
		private bool disposed;
		private TextReader reader;
		private Configuration.Configuration configuration;

		/// <summary>
		/// Gets the raw record builder.
		/// </summary>
		public virtual StringBuilder RawRecordBuilder { get; } = new StringBuilder();

		/// <summary>
		/// Gets the field builder.
		/// </summary>
		public virtual StringBuilder FieldBuilder { get; } = new StringBuilder();

		/// <summary>
		/// Gets the record builder.
		/// </summary>
		public virtual RecordBuilder RecordBuilder { get; } = new RecordBuilder();

		/// <summary>
		/// Gets the named indexes.
		/// </summary>
		public virtual Dictionary<string, List<int>> NamedIndexes { get; } = new Dictionary<string, List<int>>();

		/// <summary>
		/// Getse the named indexes cache.
		/// </summary>
		public virtual Dictionary<string, Tuple<string, int>> NamedIndexCache { get; } = new Dictionary<string, Tuple<string, int>>();

		/// <summary>
		/// Gets the type converter options cache.
		/// </summary>
		public virtual Dictionary<Type, TypeConverterOptions> TypeConverterOptionsCache { get; } = new Dictionary<Type, TypeConverterOptions>();

		/// <summary>
		/// Gets the create record functions.
		/// </summary>
		public virtual Dictionary<Type, Delegate> CreateRecordFuncs { get; } = new Dictionary<Type, Delegate>();

		/// <summary>
		/// Gets the hydrate record actions.
		/// </summary>
		public virtual Dictionary<Type, Delegate> HydrateRecordActions { get; } = new Dictionary<Type, Delegate>();

		/// <summary>
		/// Gets the reusable member map data.
		/// </summary>
		public virtual MemberMapData ReusableMemberMapData { get; } = new MemberMapData( null );

		/// <summary>
		/// Gets the <see cref="CsvParser"/> configuration.
		/// </summary>
		public virtual IParserConfiguration ParserConfiguration => configuration;

		/// <summary>
		/// Gets the <see cref="CsvReader"/> configuration.
		/// </summary>
		public virtual IReaderConfiguration ReaderConfiguration => configuration;

		/// <summary>
		/// Gets the <see cref="TextReader"/> that is read from.
		/// </summary>
		public virtual TextReader Reader => reader;

		/// <summary>
		/// Gets a value indicating if the <see cref="Reader"/>
		/// should be left open when disposing.
		/// </summary>
		public virtual bool LeaveOpen { get; set; }

		/// <summary>
		/// Gets the buffer used to store data from the <see cref="Reader"/>.
		/// </summary>
		public virtual char[] Buffer { get; set; }

		/// <summary>
		/// Gets all the characters of the record including
		/// quotes, delimeters, and line endings.
		/// </summary>
		public virtual string RawRecord => RawRecordBuilder.ToString();

		/// <summary>
		/// Gets the field.
		/// </summary>
		public virtual string Field => FieldBuilder.ToString();

		/// <summary>
		/// Gets the buffer position.
		/// </summary>
		public virtual int BufferPosition { get; set; }

		/// <summary>
		/// Gets the field start position.
		/// </summary>
		public virtual int FieldStartPosition { get; set; }

		/// <summary>
		/// Gets the field end position.
		/// </summary>
		public virtual int FieldEndPosition { get; set; }

		/// <summary>
		/// Gets the raw record start position.
		/// </summary>
		public virtual int RawRecordStartPosition { get; set; }

		/// <summary>
		/// Gets the raw record end position.
		/// </summary>
		public virtual int RawRecordEndPosition { get; set; }

		/// <summary>
		/// Gets the number of characters read from the <see cref="Reader"/>.
		/// </summary>
		public virtual int CharsRead { get; set; }

		/// <summary>
		/// Gets the character position.
		/// </summary>
		public virtual long CharPosition { get; set; }

		/// <summary>
		/// Gets the byte position.
		/// </summary>
		public virtual long BytePosition { get; set; }

		/// <summary>
		/// Gets a value indicating if the field is bad.
		/// True if the field is bad, otherwise false.
		/// A field is bad if a quote is found in a field
		/// that isn't escaped.
		/// </summary>
		public virtual bool IsFieldBad { get; set; }

		/// <summary>
		/// Gets the record.
		/// </summary>
		public virtual string[] Record { get; set; }

		/// <summary>
		/// Gets the row of the CSV file that the parser is currently on.
		/// </summary>
		public virtual int Row { get; set; }

		/// <summary>
		/// Gets the row of the CSV file that the parser is currently on.
		/// This is the actual file row.
		/// </summary>
		public virtual int RawRow { get; set; }

		/// <summary>
		/// Gets the character the field reader is currently on.
		/// </summary>
		public virtual int C { get; set; } = -1;

		/// <summary>
		/// Gets a value indicating if reading has begun.
		/// </summary>
		public virtual bool HasBeenRead { get; set; }

		/// <summary>
		/// Gets the header record.
		/// </summary>
		public virtual string[] HeaderRecord { get; set; }

		/// <summary>
		/// Gets the current index.
		/// </summary>
		public virtual int CurrentIndex { get; set; } = -1;

		/// <summary>
		/// Gets the column count.
		/// </summary>
		public virtual int ColumnCount { get; set; }

		/// <summary>
		/// Initializes a new instance.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="configuration">The configuration.</param>
		/// <param name="leaveOpen">A value indicating if the TextReader should be left open when disposing.</param>
		public ReadingContext( TextReader reader, Configuration.Configuration configuration, bool leaveOpen )
		{
			this.reader = reader ?? throw new ArgumentNullException( nameof( reader ) );
			this.configuration = configuration ?? throw new ArgumentNullException( nameof( configuration ) );
			LeaveOpen = leaveOpen;
			Buffer = new char[0];
		}

		/// <summary>
		/// Clears the specified caches.
		/// </summary>
		/// <param name="cache">The caches to clear.</param>
		public virtual void ClearCache( Caches cache )
		{
			if( ( cache & Caches.NamedIndex ) == Caches.NamedIndex )
			{
				NamedIndexCache.Clear();
			}

			if( ( cache & Caches.ReadRecord ) == Caches.ReadRecord )
			{
				CreateRecordFuncs.Clear();
			}

			if( ( cache & Caches.TypeConverterOptions ) == Caches.TypeConverterOptions )
			{
				TypeConverterOptionsCache.Clear();
			}

			if( ( cache & Caches.RawRecord ) == Caches.RawRecord )
			{
				RawRecordBuilder.Clear();
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
				reader?.Dispose();
			}

			reader = null;
			disposed = true;
		}
	}
}