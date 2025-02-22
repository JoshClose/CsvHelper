// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;

namespace CsvHelper;

/// <summary>
/// Defines methods used the parse a CSV file.
/// </summary>
public interface IParser : IDisposable
{
	/// <summary>
	/// Gets the count of how many bytes have been read.
	/// <see cref="IParserConfiguration.CountBytes"/> needs
	/// to be enabled for this value to be populated.
	/// </summary>
	long ByteCount { get; }

	/// <summary>
	/// Gets the count of how many characters have been read.
	/// </summary>
	long CharCount { get; }

	/// <summary>
	/// Gets the number of fields for the current row.
	/// </summary>
	int Count { get; }

	/// <summary>
	/// Gets the field at the specified index for the current row.
	/// </summary>
	/// <param name="index">The index of the field in the current row.</param>
	/// <returns>A <see cref="string"/> representing the field at the specified index in the current row.</returns>
	string this[int index] { get; }

	/// <summary>
	/// Gets a span over the field at the specified index in the current row.
	/// </summary>
	/// <param name="index">The index of the field in the current row.</param>
	/// <returns>A span representing the field at the specified index in the current row.</returns>
	ReadOnlySpan<char> GetFieldSpan(int index)
#if NET
		=> this[index]
#endif
		;

	/// <summary>
	/// Gets the record for the current row. Note:
	/// It is much more efficient to only get the fields you need. If
	/// you need all fields, then use this.
	/// </summary>
	string[]? Record { get; }

	/// <summary>
	/// Gets the raw record for the current row.
	/// </summary>
	string RawRecord { get; }

	/// <summary>
	/// Gets a span over the raw record for the current row.
	/// </summary>
	ReadOnlySpan<char> RawRecordSpan
#if NET
		=> RawRecord;
#else
			{ get; }
#endif

	/// <summary>
	/// Gets the CSV row the parser is currently on.
	/// </summary>
	int Row { get; }

	/// <summary>
	/// Gets the raw row the parser is currently on.
	/// </summary>
	int RawRow { get; }

	/// <summary>
	/// The delimiter the parser is using.
	/// </summary>
	string Delimiter { get; }

	/// <summary>
	/// Gets the reading context.
	/// </summary>
	CsvContext Context { get; }

	/// <summary>
	/// Gets the configuration.
	/// </summary>
	IParserConfiguration Configuration { get; }

	/// <summary>
	/// Reads a record from the CSV file.
	/// </summary>
	/// <returns>True if there are more records to read, otherwise false.</returns>
	bool Read();

	/// <summary>
	/// Reads a record from the CSV file asynchronously.
	/// </summary>
	/// <returns>True if there are more records to read, otherwise false.</returns>
	Task<bool> ReadAsync();
}
