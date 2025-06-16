namespace CsvHelper;

/// <summary>
/// Represents a single row in a file, providing access to its fields and raw data.
/// </summary>
public interface IParserRow
{
	/// <summary>
	/// Gets the number of fields in the row.
	/// </summary>
	public int Count { get; }

	/// <summary>
	/// Gets the field at the given index.
	/// </summary>
	/// <param name="index">The field index.</param>
	/// <returns>The field as a <see cref="ReadOnlySpan{T}" /> .</returns>
	public ReadOnlySpan<char> this[int index] { get; }

	/// <summary>
	/// Gets the row.
	/// </summary>
	public ReadOnlySpan<char> Row { get; }

	/// <summary>
	/// Gets a string representation of the field at the given index.<br/>
	/// Use the <see cref="this[int]"/> indexer if possible.
	/// </summary>
	/// <param name="index">The field index.</param>
	/// <returns>The field as a string.</returns>
	public string ToString(int index);
}
