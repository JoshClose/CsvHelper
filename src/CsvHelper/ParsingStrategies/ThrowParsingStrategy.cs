namespace CsvHelper;

internal class ThrowParsingStrategy : ICsvParsingStrategy
{
	public void Parse(CsvParserState state)
	{
		throw new InvalidOperationException($"No {nameof(ParsingStrategy)} is set.");
	}
}
