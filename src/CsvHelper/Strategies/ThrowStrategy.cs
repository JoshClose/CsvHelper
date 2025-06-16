namespace CsvHelper;

internal class ThrowStrategy : ICsvParsingStrategy
{
	public void Parse(CsvParserState state)
	{
		throw new InvalidOperationException($"No {nameof(ParsingStrategy)} is set.");
	}
}
