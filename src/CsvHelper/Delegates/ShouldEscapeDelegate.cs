namespace CsvHelper;

/// <summary>
/// Represents a method that determines whether a given field should be escaped.
/// </summary>
/// <param name="field">A read-only span of characters representing the field to evaluate.</param>
/// <returns><see langword="true"/> if the field should be escaped; otherwise, <see langword="false"/>.</returns>
public delegate bool ShouldEscape(ReadOnlySpan<char> field);
