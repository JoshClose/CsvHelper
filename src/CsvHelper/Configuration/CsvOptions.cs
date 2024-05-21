namespace CsvHelper.Configuration;

public abstract record CsvOptions
{
	public char Delimiter { get; init; } = ',';

	public char Escape { get; init; } = '\"';

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
