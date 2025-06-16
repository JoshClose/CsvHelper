namespace CsvHelper;

internal delegate void CsvModeParse(CsvParserState state, ref char bufferRef, ref int mask);

internal delegate void CsvModeEscape(CsvSerializerState state, ReadOnlySpan<char> field);
