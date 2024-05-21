namespace CsvHelper.Configuration;

public delegate string StringCreator(ReadOnlySpan<char> chars, int columnIndex);
