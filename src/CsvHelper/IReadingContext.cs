// Copyright 2009-2017 Josh Close and Contributors
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvHelper
{
	/// <summary>
	/// Context used when reading.
	/// </summary>
    public interface IReadingContext : IDisposable
    {
		/// <summary>
		/// Gets the raw record builder.
		/// </summary>
		StringBuilder RawRecordBuilder { get; }

		/// <summary>
		/// Gets the field builder.
		/// </summary>
		StringBuilder FieldBuilder { get; }

		/// <summary>
		/// Gets the record builder.
		/// </summary>
		RecordBuilder RecordBuilder { get; }

		/// <summary>
		/// Gets the named indexes.
		/// </summary>
		Dictionary<string, List<int>> NamedIndexes { get; }

		/// <summary>
		/// Getse the named indexes cache.
		/// </summary>
		Dictionary<string, Tuple<string, int>> NamedIndexCache { get; }

		/// <summary>
		/// Gets the type converter options cache.
		/// </summary>
		Dictionary<Type, TypeConverterOptions> TypeConverterOptionsCache { get; }

		/// <summary>
		/// Gets the create record functions.
		/// </summary>
		Dictionary<Type, Delegate> CreateRecordFuncs { get; }

		/// <summary>
		/// Gets the hydrate record actions.
		/// </summary>
		Dictionary<Type, Delegate> HydrateRecordActions { get; }

		/// <summary>
		/// Gets the reusable member map data.
		/// </summary>
		MemberMapData ReusableMemberMapData { get; }

		/// <summary>
		/// Gets the <see cref="CsvParser"/> configuration.
		/// </summary>
		IParserConfiguration ParserConfiguration { get; }

		/// <summary>
		/// Gets the <see cref="CsvReader"/> configuration.
		/// </summary>
		IReaderConfiguration ReaderConfiguration { get; }

		/// <summary>
		/// Gets the <see cref="TextReader"/> that is read from.
		/// </summary>
		TextReader Reader { get; }

		/// <summary>
		/// Gets a value indicating if the <see cref="Reader"/>
		/// should be left open when disposing.
		/// </summary>
		bool LeaveOpen { get; set; }

		/// <summary>
		/// Gets the buffer used to store data from the <see cref="Reader"/>.
		/// </summary>
		char[] Buffer { get; set; }

		/// <summary>
		/// Gets all the characters of the record including
		/// quotes, delimeters, and line endings.
		/// </summary>
		string RawRecord { get; }

		/// <summary>
		/// Gets the field.
		/// </summary>
		string Field { get; }

		/// <summary>
		/// Gets the buffer position.
		/// </summary>
		int BufferPosition { get; set; }

		/// <summary>
		/// Gets the field start position.
		/// </summary>
		int FieldStartPosition { get; set; }

		/// <summary>
		/// Gets the field end position.
		/// </summary>
		int FieldEndPosition { get; set; }

		/// <summary>
		/// Gets the raw record start position.
		/// </summary>
		int RawRecordStartPosition { get; set; }

		/// <summary>
		/// Gets the raw record end position.
		/// </summary>
		int RawRecordEndPosition { get; set; }

		/// <summary>
		/// Gets the number of characters read from the <see cref="Reader"/>.
		/// </summary>
		int CharsRead { get; set; }

		/// <summary>
		/// Gets the character position.
		/// </summary>
		long CharPosition { get; set; }

		/// <summary>
		/// Gets the byte position.
		/// </summary>
		long BytePosition { get; set; }

		/// <summary>
		/// Gets a value indicating if the field is bad.
		/// True if the field is bad, otherwise false.
		/// A field is bad if a quote is found in a field
		/// that isn't escaped.
		/// </summary>
		bool IsFieldBad { get; set; }

		/// <summary>
		/// Gets the record.
		/// </summary>
		string[] Record { get; set; }

		/// <summary>
		/// Gets the row of the CSV file that the parser is currently on.
		/// </summary>
		int Row { get; set; }

		/// <summary>
		/// Gets the row of the CSV file that the parser is currently on.
		/// This is the actual file row.
		/// </summary>
		int RawRow { get; set; }

		/// <summary>
		/// Gets the character the field reader is currently on.
		/// </summary>
		int C { get; set; }

		/// <summary>
		/// Gets a value indicating if reading has begun.
		/// </summary>
		bool HasBeenRead { get; set; }

		/// <summary>
		/// Gets the header record.
		/// </summary>
		string[] HeaderRecord { get; set; }

		/// <summary>
		/// Gets the current index.
		/// </summary>
		int CurrentIndex { get; set; }

		/// <summary>
		/// Gets the column count.
		/// </summary>
		int ColumnCount { get; set; }

		/// <summary>
		/// Clears the specified caches.
		/// </summary>
		/// <param name="cache">The caches to clear.</param>
		void ClearCache( Caches cache );
	}
}
