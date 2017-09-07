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
	public class ReadingContext : IReaderContext, IParserContext, IFieldReaderContext, IDisposable
    {
		private bool disposed;
		private TextReader reader;
		private Configuration.Configuration configuration;

		internal virtual StringBuilder RawRecordBuilder { get; } = new StringBuilder();

		internal virtual StringBuilder FieldBuilder { get; } = new StringBuilder();

		internal virtual RecordBuilder RecordBuilder { get; } = new RecordBuilder();

		internal Dictionary<string, List<int>> NamedIndexes { get; } = new Dictionary<string, List<int>>();

		internal Dictionary<string, Tuple<string, int>> NamedIndexCache { get; } = new Dictionary<string, Tuple<string, int>>();

		internal Dictionary<Type, Delegate> RecordFuncs = new Dictionary<Type, Delegate>();

		internal Dictionary<Type, TypeConverterOptions> TypeConverterOptionsCache { get; } = new Dictionary<Type, TypeConverterOptions>();

		internal MemberMapData ReusableMemberMapData { get; } = new MemberMapData( null );

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
		public virtual bool LeaveOpen { get; internal set; }

		/// <summary>
		/// Gets the buffer used to store data from the <see cref="Reader"/>.
		/// </summary>
		public virtual char[] Buffer { get; internal set; }

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
		public virtual int BufferPosition { get; internal set; }

		/// <summary>
		/// Gets the field start position.
		/// </summary>
		public virtual int FieldStartPosition { get; internal set; }

		/// <summary>
		/// Gets the field end position.
		/// </summary>
		public virtual int FieldEndPosition { get; internal set; }

		/// <summary>
		/// Gets the raw record start position.
		/// </summary>
		public virtual int RawRecordStartPosition { get; internal set; }

		/// <summary>
		/// Gets the raw record end position.
		/// </summary>
		public virtual int RawRecordEndPosition { get; internal set; }

		/// <summary>
		/// Gets the number of characters read from the <see cref="Reader"/>.
		/// </summary>
		public virtual int CharsRead { get; internal set; }

		/// <summary>
		/// Gets the character position.
		/// </summary>
		public virtual long CharPosition { get; internal set; }

		/// <summary>
		/// Gets the byte position.
		/// </summary>
		public virtual long BytePosition { get; internal set; }

		/// <summary>
		/// Getsa value indicating if the field is bad.
		/// True if the field is bad, otherwise false.
		/// </summary>
		public virtual bool IsFieldBad { get; internal set; }

		/// <summary>
		/// Gets the record.
		/// </summary>
		public virtual string[] Record { get; internal set; }

		/// <summary>
		/// Gets the row of the CSV file that the parser is currently on.
		/// </summary>
		public virtual int Row { get; internal set; }

		/// <summary>
		/// Gets the row of the CSV file that the parser is currently on.
		/// This is the actual file row.
		/// </summary>
		public virtual int RawRow { get; internal set; }

		/// <summary>
		/// Gets the character the field reader is currently on.
		/// </summary>
		public virtual int C { get; internal set; } = -1;

		/// <summary>
		/// Gets a value indicating if reading has begun.
		/// </summary>
		public virtual bool HasBeenRead { get; internal set; }

		/// <summary>
		/// Gets the header record.
		/// </summary>
		public virtual string[] HeaderRecord { get; internal set; }

		/// <summary>
		/// Gets the current index.
		/// </summary>
		public virtual int CurrentIndex { get; internal set; } = -1;

		/// <summary>
		/// Gets the column count.
		/// </summary>
		public virtual int ColumnCount { get; internal set; }

		internal ReadingContext( TextReader reader, Configuration.Configuration configuration, bool leaveOpen )
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
		public void ClearCache( Caches cache )
		{
			if( cache.HasFlag( Caches.NamedIndex ) )
			{
				NamedIndexCache.Clear();
			}

			if( cache.HasFlag( Caches.ReadRecord ) )
			{
				RecordFuncs.Clear();
			}

			if( cache.HasFlag( Caches.TypeConverterOptions ) )
			{
				TypeConverterOptionsCache.Clear();
			}

			if( cache.HasFlag( Caches.RawRecord ) )
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