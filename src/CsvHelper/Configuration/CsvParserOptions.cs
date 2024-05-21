namespace CsvHelper.Configuration;

public record CsvParserOptions : CsvOptions
{
	public CsvMode Mode { get; init; }

	public bool CacheFields { get; init; }

	public ParsingStrategy? ParsingStrategy { get; init; }

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
