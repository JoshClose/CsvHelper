// Copyright 2009-2020 Josh Close and Contributors
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
	public class ReadingContext : IDisposable
	{
		private bool disposed;
		private readonly Configuration.CsvConfiguration configuration;

		/// <summary>
		/// Gets the raw record builder.
		/// </summary>
		public StringBuilder RawRecordBuilder = new StringBuilder();

		/// <summary>
		/// Gets the field builder.
		/// </summary>
		public StringBuilder FieldBuilder = new StringBuilder();

		/// <summary>
		/// Gets the record builder.
		/// </summary>
		public RecordBuilder RecordBuilder = new RecordBuilder();

		/// <summary>
		/// Gets the named indexes.
		/// </summary>
		public Dictionary<string, List<int>> NamedIndexes = new Dictionary<string, List<int>>();

		/// <summary>
		/// Gets the named indexes cache.
		/// </summary>
		public Dictionary<string, (string, int)> NamedIndexCache = new Dictionary<string, (string, int)>();

		/// <summary>
		/// Gets the type converter options cache.
		/// </summary>
		public Dictionary<Type, TypeConverterOptions> TypeConverterOptionsCache = new Dictionary<Type, TypeConverterOptions>();

		/// <summary>
		/// Gets the create record functions.
		/// </summary>
		public Dictionary<Type, Delegate> CreateRecordFuncs = new Dictionary<Type, Delegate>();

		/// <summary>
		/// Gets the hydrate record actions.
		/// </summary>
		public Dictionary<Type, Delegate> HydrateRecordActions = new Dictionary<Type, Delegate>();

		/// <summary>
		/// Gets the reusable member map data.
		/// </summary>
		public MemberMapData ReusableMemberMapData = new MemberMapData(null);

		/// <summary>
		/// Gets the <see cref="TextReader"/> that is read from.
		/// </summary>
		public TextReader Reader;

		/// <summary>
		/// Gets a value indicating if the <see cref="Reader"/>
		/// should be left open when disposing.
		/// </summary>
		public bool LeaveOpen;

		/// <summary>
		/// Gets the buffer used to store data from the <see cref="Reader"/>.
		/// </summary>
		public char[] Buffer;

		/// <summary>
		/// Gets the buffer position.
		/// </summary>
		public int BufferPosition;

		/// <summary>
		/// Gets the field start position.
		/// </summary>
		public int FieldStartPosition;

		/// <summary>
		/// Gets the field end position.
		/// </summary>
		public int FieldEndPosition;

		/// <summary>
		/// Gets the raw record start position.
		/// </summary>
		public int RawRecordStartPosition;

		/// <summary>
		/// Gets the raw record end position.
		/// </summary>
		public int RawRecordEndPosition;

		/// <summary>
		/// Gets the number of characters read from the <see cref="Reader"/>.
		/// </summary>
		public int CharsRead;

		/// <summary>
		/// Gets the character position.
		/// </summary>
		public long CharPosition;

		/// <summary>
		/// Gets the byte position.
		/// </summary>
		public long BytePosition;

		/// <summary>
		/// Gets a value indicating if the field is bad.
		/// True if the field is bad, otherwise false.
		/// A field is bad if a quote is found in a field
		/// that isn't escaped.
		/// </summary>
		public bool IsFieldBad;

		/// <summary>
		/// Gets the record.
		/// </summary>
		public string[] Record;

		/// <summary>
		/// Gets the row of the CSV file that the parser is currently on.
		/// </summary>
		public int Row;

		/// <summary>
		/// Gets the row of the CSV file that the parser is currently on.
		/// This is the actual file row.
		/// </summary>
		public int RawRow;

		/// <summary>
		/// Gets a value indicating if reading has begun.
		/// </summary>
		public bool HasBeenRead;

		/// <summary>
		/// Gets the header record.
		/// </summary>
		public string[] HeaderRecord;

		/// <summary>
		/// Gets the current index.
		/// </summary>
		public int CurrentIndex = -1;

		/// <summary>
		/// Gets the column count.
		/// </summary>
		public int ColumnCount;

		/// <summary>
		/// Gets the <see cref="CsvParser"/> configuration.
		/// </summary>
		public IParserConfiguration ParserConfiguration => configuration;

		/// <summary>
		/// Gets the <see cref="CsvReader"/> configuration.
		/// </summary>
		public IReaderConfiguration ReaderConfiguration => configuration;

		/// <summary>
		/// Gets all the characters of the record including
		/// quotes, delimiters, and line endings.
		/// </summary>
		public string RawRecord => RawRecordBuilder.ToString();

		/// <summary>
		/// Gets the field.
		/// </summary>
		public string Field => FieldBuilder.ToString();

		/// <summary>
		/// Initializes a new instance.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="configuration">The configuration.</param>
		/// <param name="leaveOpen">A value indicating if the TextReader should be left open when disposing.</param>
		public ReadingContext(TextReader reader, Configuration.CsvConfiguration configuration, bool leaveOpen)
		{
			Reader = reader ?? throw new ArgumentNullException(nameof(reader));
			this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
			LeaveOpen = leaveOpen;
			Buffer = new char[0];
		}

		/// <summary>
		/// Clears the specified caches.
		/// </summary>
		/// <param name="cache">The caches to clear.</param>
		public virtual void ClearCache(Caches cache)
		{
			if ((cache & Caches.NamedIndex) == Caches.NamedIndex)
			{
				NamedIndexCache.Clear();
			}

			if ((cache & Caches.ReadRecord) == Caches.ReadRecord)
			{
				CreateRecordFuncs.Clear();
			}

			if ((cache & Caches.TypeConverterOptions) == Caches.TypeConverterOptions)
			{
				TypeConverterOptionsCache.Clear();
			}

			if ((cache & Caches.RawRecord) == Caches.RawRecord)
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
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <param name="disposing">True if the instance needs to be disposed of.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposed)
			{
				return;
			}

			if (disposing)
			{
				Reader?.Dispose();
			}

			Reader = null;
			disposed = true;
		}
	}
}