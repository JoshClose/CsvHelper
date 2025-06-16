using System.Globalization;

namespace CsvHelper.Configuration;

/// <summary>
/// Options common to both parsing and serialization of CSV data.
/// </summary>
public abstract record CsvOptions
{
	/// <summary>
	/// The character used to separate fields in the CSV data.<br/>
	/// Defaults to <see cref="TextInfo.ListSeparator"/>.
	/// </summary>
	public char Delimiter { get; init; } = ',';

	/// <summary>
	/// The character used to escape special characters in the CSV data.<br/>
	/// Defaults to <c>"</c> (double quote).
	/// </summary>
	public char Escape { get; init; } = '\"';

	/// <summary>
	/// The character used to indicate a new line in the CSV data.<br/>
	/// If not set, \r\n will be used.<br/>
	/// Defaults to <c>null</c>.
	/// </summary>
	public char? NewLine { get; init; }

	/// <summary>
	/// The mode used for parsing or serializing CSV data.<br/>
	/// Defaults to <see cref="CsvMode.RFC4180"/>.
	/// </summary>
	public CsvMode Mode { get; init; }

	internal int BufferSize = 0x1000;

	internal virtual void Validate()
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

	/// <summary>
	/// Copies the current options to a new instance of <see cref="CsvOptions"/>.<br/>
	/// </summary>
	/// <param name="options">The new options to copy values to.</param>
	/// <returns>A new options with values copied to it.</returns>
	protected CsvOptions CopyTo(CsvOptions options)
	{
		return options with
		{
			Delimiter = Delimiter,
			Escape = Escape,
			NewLine = NewLine,
			Mode = Mode,
			BufferSize = BufferSize
		};
	}
}
