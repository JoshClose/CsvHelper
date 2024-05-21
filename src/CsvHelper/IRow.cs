namespace CsvHelper;

/// <summary>
/// Defines functionality for a CSV row.
/// </summary>
public interface IRow
{
	/// <summary>
	/// Gets the number of fields in the row.
	/// </summary>
	public int Count { get; }

	/// <summary>
	/// Gets the field at the given index.
	/// </summary>
	/// <param name="index">The field index.</param>
	/// <returns>The field as a ReadOnlySpan.</returns>
	public ReadOnlySpan<char> this[int index] { get; }

	/// <summary>
	/// Gets the row as a ReadOnlySpan.
	/// </summary>
	public ReadOnlySpan<char> Row { get; }

	/// <summary>
	/// Gets a string representation of the field at the given index.
	/// Use the <see cref="this[int]" /> indexer if possible.
	/// </summary>
	/// <param name="index">The field index.</param>
	/// <returns>The field as a string.</returns>
	public string ToString(int index);
}
