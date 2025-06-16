namespace CsvHelper.Configuration;

/// <summary>
/// Options for parsing CSV data.
/// </summary>
public record CsvParserOptions : CsvOptions
{
	/// <summary>
	/// A value indicating whether fields should be cached for improved performance.
	/// </summary>
	public bool CacheFields { get; init; }

	/// <summary>
	/// Strategy use to find special characters in the CSV data.
	/// </summary>
	public ParsingStrategy? Strategy { get; init; }

	/// <summary>
	/// Creates a new instance of <see cref="CsvSerializerOptions"/> using common values from this instance.
	/// </summary>
	public CsvSerializerOptions ToSerializerOptions()
	{
		return (CsvSerializerOptions)CopyTo(new CsvSerializerOptions());
	}

	internal StringCreator StringCreator = (chars, i)
#if NET8_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
		=> new string(chars);
#else
		=> chars.ToString();
#endif

	internal int ParsedRowsSize = 8;

	internal int ParsedFieldsSize = 8 * 32;

	internal CsvModeParse ModeParse = Rfc4180Mode.Parse;

	internal ICsvParsingStrategy ParsingStrategyImplementation = new ThrowStrategy();

	internal char[] ValidSpecialCharsForIntrinsics = Enumerable
		.Range(32, 127 - 32)
		.Select(i => (char)i)
		.ToList()
		.Append('\t')
		.Append('\r')
		.Append('\n')
		.ToArray();

	internal override void Validate()
	{
		base.Validate();

		// IndexOfAny does not use Vector256, so any char is valid for a special char.
		if (Strategy == ParsingStrategy.IndexOfAny)
		{
			return;
		}

		if (!ValidSpecialCharsForIntrinsics.Contains(Delimiter))
		{
			ThrowInvalidSpecialCharsForIntrinsics(nameof(Delimiter));
		}

		if (!ValidSpecialCharsForIntrinsics.Contains(Escape))
		{
			ThrowInvalidSpecialCharsForIntrinsics(nameof(Escape));
		}

		if (NewLine.HasValue && !ValidSpecialCharsForIntrinsics.Contains(NewLine.Value))
		{
			ThrowInvalidSpecialCharsForIntrinsics(nameof(NewLine));
		}
	}

	private void ThrowInvalidSpecialCharsForIntrinsics(string property)
	{
		var validChars = string.Join(", ", ValidSpecialCharsForIntrinsics.Select(c => $"'{c}'"));
		var message = $"{property} must be in the valid range of characters." +
			Environment.NewLine +
			$"You can use {nameof(ReplaceTextReader)} to replace invalid chars with valid chars." +
			Environment.NewLine +
			$"You can also use parsing strategy {nameof(ParsingStrategy.IndexOfAny)} because it does not suffer this limitation, though parsing will be slower." +
			Environment.NewLine +
			$"Valid chars: {validChars}";

		throw new ConfigurationException(message);
	}
}
