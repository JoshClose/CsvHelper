namespace CsvHelper;

/// <summary>
/// Reads and writes the field of a member of a <see cref="CsvRecordAttribute"/> type that has a
/// <see cref="CsvConverterAttribute"/>, for types that cannot be read otherwise and for formats of your own.
/// </summary>
/// <remarks>
/// A converter needs a public or internal parameterless constructor. Each mapper and record writer creates one instance
/// and calls it for every record; the workers of <see cref="CsvReader.ParallelEnumerate{T}(int)"/> share a mapper, so a
/// converter must be thread-safe (one without fields is).
/// </remarks>
/// <typeparam name="T">The type of the member.</typeparam>
public interface ICsvConverter<T>
{
	/// <summary>Reads the value of field <paramref name="index"/> of the current record.</summary>
	/// <param name="reader">The reader, positioned on the record.</param>
	/// <param name="index">The 0-based index of the member's column.</param>
	/// <returns>The value of the member.</returns>
	public T Read(CsvReader reader, int index);

	/// <summary>
	/// Writes the value of the member as one field of the current record, with one of the writer's <c>WriteField</c>
	/// methods, in a form that <see cref="Read"/> reads back.
	/// </summary>
	/// <param name="writer">The writer, in the middle of the record.</param>
	/// <param name="value">The value of the member.</param>
	public void Write(CsvWriter writer, T value);
}
