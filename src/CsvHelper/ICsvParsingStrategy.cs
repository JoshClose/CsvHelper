namespace CsvHelper;

internal interface ICsvParsingStrategy
{
	void Parse(CsvParserState state);
}
