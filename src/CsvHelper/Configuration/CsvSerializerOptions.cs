namespace CsvHelper.Configuration;

/// <summary>
/// Options for serializing CSV data.
/// </summary>
public record CsvSerializerOptions : CsvOptions
{
	/// <summary>
	/// The function used to determine if a field should be escaped.<br/>
	/// Default is <c>null</c>, which means fields will be escaped according to the RFC 4180 standard.
	/// </summary>
	public ShouldEscape? ShouldEscape { get; init; }

	internal CsvModeEscape ModeEscape = Rfc4180Mode.Escape;
}
