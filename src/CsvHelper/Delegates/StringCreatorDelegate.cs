namespace CsvHelper;

/// <summary>
/// Represents a method that creates a string based on a span of characters and a column index.
/// </summary>
/// <param name="chars">A read-only span of characters to be used in the string creation process.</param>
/// <param name="columnIndex">The zero-based index of the column associated with the string creation.</param>
/// <returns>A string created based on the provided characters and column index.</returns>
public delegate string StringCreator(ReadOnlySpan<char> chars, int columnIndex);
