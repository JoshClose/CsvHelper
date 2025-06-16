namespace CsvHelper;

/// <summary>
/// Requirements to implement a parser.
/// </summary>
public interface IParser : IDisposable
{
	/// <summary>
	/// Gets the current row number.
	/// </summary>
	int Row { get; }

	/// <summary>
	/// Gets the current row.
	/// </summary>
	IParserRow Current { get; }

	/// <summary>
	/// Moves the parser to the next row in the file.
	/// </summary>
	/// <returns><c>true</c> if there are more rows, otherwise <c>false</c>.</returns>
	bool MoveNext();

	/// <summary>
	/// Gets the enumerator for the parser.
	/// </summary>
	/// <returns>The enumerator.</returns>
	IParser GetEnumerator();

	/// <summary>
	/// Gets an enumerable that iterates the records asynchronously.
	/// </summary>
	/// <typeparam name="TRecord">The record type.</typeparam>
	/// <param name="createRecord">The function to handle each record.</param>
	/// <returns>An enumerable of records.</returns>
	IEnumerable<TRecord> AsParallel<TRecord>(Func<IParserRow, TRecord> createRecord);
}
