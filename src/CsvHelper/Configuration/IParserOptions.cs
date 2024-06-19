namespace CsvHelper.Configuration;

/// <summary>
/// Options for <see cref="CsvParser"/>.
/// </summary>
public interface IParserOptions
{
	/// <summary>
	/// Detect the delimiter instead of using the delimiter from configuration.
	/// Default is <c>false</c>.
	/// </summary>
	public bool DetectDelimiter { get; }
}
