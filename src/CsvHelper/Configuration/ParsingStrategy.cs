namespace CsvHelper.Configuration;

public enum ParsingStrategy
{
	IndexOfAny = 0,
#if NET7_0_OR_GREATER
	Vector256 = 1,
#endif
#if NET6_0_OR_GREATER
	Avx2 = 2,
#endif
}
