namespace CsvHelper.Configuration;

/// <summary>
/// Configuration options used for <see cref="CsvParser"/>.
/// </summary>
public record CsvParserOptions : CsvOptions
{
	/// <summary>
	/// Cache fields that are created when parsing.
	/// Default is false.
	/// </summary>
	public bool CacheFields { get; init; }

	/// <summary>
	/// Strategy used for parsing.
	/// Defaults to the highest performance your framework and CPU supports.
	/// </summary>
	public ParsingStrategy? ParsingStrategy { get; init; }

	/// <summary>
	/// Detect the delimiter instead of using the delimiter from configuration.
	/// Default is <c>false</c>.
	/// </summary>
	public bool DetectDelimiter { get; init; }

	/// <summary>
	/// The function that is called when <see cref="DetectDelimiter"/> is enabled.
	/// </summary>
	public GetDelimiter GetDelimiter { get; set; } = ConfigurationFunctions.GetDelimiter;

	internal StringCreator StringCreator = (chars, i)
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
		=> new string(chars);
#else
		=> chars.ToString();
#endif

	internal int ParsedRowsSize = 8;

	internal int ParsedFieldsSize = 8 * 32;

	internal CsvModeParse ModeParse = ModeRfc4180.Parse;

	internal ICsvParsingStrategy ParsingStrategyImplementation = new ThrowParsingStrategy();

	internal char[] ValidSpecialCharsForIntrinsics = Enumerable
		.Range(32, 127 - 32)
		.Select(i => (char)i)
		.ToList()
		.Append('\t')
		.Append('\r')
		.Append('\n')
		.ToArray();
}
