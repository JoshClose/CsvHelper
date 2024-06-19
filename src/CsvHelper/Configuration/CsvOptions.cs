using System.Globalization;

namespace CsvHelper.Configuration;

/// <summary>
/// A function that is used to determine if a field should get escaped when writing.
/// </summary>
/// <param name="field">The field.</param>
public delegate bool ShouldEscape(ReadOnlySpan<char> field);

/// <summary>
/// Common configuration options for reading and writing CSV files.
/// </summary>
public record CsvOptions : IReadingOptions, IWritingOptions
{
	/// <summary>
	/// Cache fields that are created when parsing.
	/// Default is false.
	/// </summary>
	public bool CacheFields { get; init; }

	/// <summary>
	/// The delimiter used to separate fields.
	/// Default is ,.
	/// If you need a multi-character delimiter, use <see cref="ReplaceTextReader"/> to replace your newline with a single character.
	/// </summary>
	public char Delimiter { get; internal set; } = ',';

	/// <summary>
	/// Detect the delimiter instead of using the delimiter from configuration.
	/// Default is <c>false</c>.
	/// </summary>
	public bool DetectDelimiter { get; init; }

	/// <summary>
	/// The character used to escape characters.
	/// Default is '"'.
	/// </summary>
	public char Escape { get; init; } = '\"';

	/// <summary>
	/// The function that is called when <see cref="DetectDelimiter"/> is enabled.
	/// </summary>
	public GetDelimiter GetDelimiter { get; set; } = ConfigurationFunctions.GetDelimiter;

	/// <summary>
	/// The mode.
	/// See <see cref="CsvMode"/> for more details.
	/// </summary>
	public CsvMode Mode { get; init; }

	/// <summary>
	/// The newline character to use.
	/// If not set, the parser uses one of \r\n, \r, or \n.
	/// If you need a multi-character newline, use <see cref="ReplaceTextReader"/> to replace your newline with a single character.
	/// </summary>
	public char? NewLine { get; init; }

	/// <summary>
	/// Strategy used for parsing.
	/// Defaults to the highest performance your framework and CPU supports.
	/// </summary>
	public ParsingStrategy? ParsingStrategy { get; init; }

	/// <summary>
	/// A function that is used to determine if a field should get escaped when writing.
	/// </summary>
	public ShouldEscape? ShouldEscape { get; init; }

	internal int BufferSize = 0x1000;

	internal CsvModeEscape ModeEscape = ModeRfc4180.Escape;

	internal CsvModeParse ModeParse = ModeRfc4180.Parse;

	internal int ParsedFieldsSize = 8 * 32;

	internal int ParsedRowsSize = 8;

	internal ICsvParsingStrategy ParsingStrategyImplementation = new ThrowParsingStrategy();

	internal StringCreator StringCreator = (chars, i)
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
		=> new string(chars);
#else
		=> chars.ToString();
#endif

	internal char[] ValidSpecialCharsForIntrinsics = Enumerable
		.Range(32, 127 - 32)
		.Select(i => (char)i)
		.ToList()
		.Append('\t')
		.Append('\r')
		.Append('\n')
		.ToArray();

	internal void Validate()
	{
		if (Delimiter == Escape)
		{
			throw new ConfigurationException($"{nameof(Delimiter)} and {nameof(Escape)} cannot be the same.");
		}

		if (Delimiter == NewLine)
		{
			throw new ConfigurationException($"{nameof(Delimiter)} and {nameof(NewLine)} cannot be the same.");
		}

		if (Escape == NewLine)
		{
			throw new ConfigurationException($"{nameof(Escape)} and {nameof(NewLine)} cannot be the same.");
		}

		if (Delimiter < 1)
		{
			throw new ConfigurationException($"{nameof(Delimiter)} must be greater than 0.");
		}

		if (Escape < 1)
		{
			throw new ConfigurationException($"{nameof(Escape)} must be greater than 0.");
		}

		if (NewLine < 1)
		{
			throw new ConfigurationException($"{nameof(NewLine)} must be greater than 0.");
		}
	}
}
