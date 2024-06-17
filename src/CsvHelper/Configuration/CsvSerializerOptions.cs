namespace CsvHelper.Configuration;

/// <summary>
/// A function that is used to determine if a field should get escaped when writing.
/// </summary>
/// <param name="field">The field.</param>
public delegate bool ShouldEscape(ReadOnlySpan<char> field);

/// <summary>
/// Configuration options used for <see cref="CsvSerializer"/>.
/// </summary>
public record CsvSerializerOptions : CsvOptions
{
	/// <summary>
	/// A function that is used to determine if a field should get escaped when writing.
	/// </summary>
	public ShouldEscape? ShouldEscape { get; init; }

	internal CsvModeEscape ModeEscape = ModeRfc4180.Escape;
}
