namespace CsvHelper.Configuration;

/// <summary>
/// Common configuration options for reading and writing CSV files.
/// </summary>
public abstract record CsvOptions
{
	/// <summary>
	/// The mode.
	/// See <see cref="CsvMode"/> for more details.
	/// </summary>
	public CsvMode Mode { get; init; }

	/// <summary>
	/// The delimiter used to separate fields.
	/// Default is ,.
	/// If you need a multi-character delimiter, use <see cref="ReplaceTextReader"/> to replace your newline with a single character.
	/// </summary>
	public char Delimiter { get; internal set; } = ',';

	/// <summary>
	/// The character used to escape characters.
	/// Default is '"'.
	/// </summary>
	public char Escape { get; init; } = '\"';

	/// <summary>
	/// The newline character to use.
	/// If not set, the parser uses one of \r\n, \r, or \n.
	/// If you need a multi-character newline, use <see cref="ReplaceTextReader"/> to replace your newline with a single character.
	/// </summary>
	public char? NewLine { get; init; }

	internal int BufferSize = 0x1000;

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
