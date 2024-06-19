// Copyright 2009-2024 Josh Close
// This file is a part of CsvHelper and is dual licensed under MS-PL and Apache 2.0.
// See LICENSE.txt for details or visit http://www.opensource.org/licenses/ms-pl.html for MS-PL and http://opensource.org/licenses/Apache-2.0 for Apache 2.0.
// https://github.com/JoshClose/CsvHelper
using CsvHelper.Configuration;

namespace CsvHelper;

/// <summary>
/// Defines functionality used the parse records.
/// </summary>
public interface IParser : IDisposable
{
	/// <summary>
	/// Gets the field at the given index.
	/// </summary>
	/// <param name="index"></param>
	/// <returns></returns>
	ReadOnlySpan<char> this[int index] { get; }

	/// <summary>
	/// Gets the row number the parser is currently on.
	/// </summary>
	int Row { get; }

	/// <summary>
	/// Gets the current row.
	/// </summary>
	IRow Current { get; }

	/// <summary>
	/// Moves to the next record.
	/// </summary>
	/// <returns><c>true</c> if there are more records to read, otherwise <c>false</c>.</returns>
	bool MoveNext();

	/// <summary>
	/// Gets the enumerator.
	/// </summary>
	CsvParser GetEnumerator();

	/// <summary>
	/// Gets an enumerable that will parse the CSV asynchronously.
	/// </summary>
	/// <typeparam name="TRecord"></typeparam>
	/// <param name="createRecord"></param>
	/// <returns></returns>
	IEnumerable<TRecord> AsParallel<TRecord>(Func<IRow, TRecord> createRecord);
}
