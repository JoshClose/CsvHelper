namespace CsvHelper;

/// <summary>
/// Defines functionality used to serialize records.
/// </summary>
public interface ISerializer : IDisposable
{
	/// <summary>
	/// Current row number.
	/// </summary>
	int Row { get; }

	/// <summary>
	/// Writes the given field.
	/// </summary>
	/// <param name="field">The field to write.</param>
	void Write(ReadOnlySpan<char> field);

	/// <summary>
	/// Moves to the next record.
	/// </summary>
	void MoveNext();

	/// <summary>
	/// Flushes the buffer to the writer.
	/// </summary>
	void Flush();
}
