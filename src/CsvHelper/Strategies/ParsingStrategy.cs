namespace CsvHelper;

/// <summary>
/// Strategy used to find special characters in the CSV data.
/// </summary>
public enum ParsingStrategy
{
	/// <summary>
	/// Uses IndexOfAny to find special characters.
	/// </summary>
	IndexOfAny = 0,

#if NET8_0_OR_GREATER
	/// <summary>
	/// Uses Vector256 intrinsics to find special characters.
	/// </summary>
	Vector256 = 1,

	/// <summary>
	/// Uses Avx2 intrinsics to find special characters.
	/// </summary>
	Avx2 = 2,
#endif
}
