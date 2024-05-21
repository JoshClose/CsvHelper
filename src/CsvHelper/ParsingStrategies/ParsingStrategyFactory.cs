using CsvHelper.Configuration;
#if NET6_0_OR_GREATER
using System.Runtime.Intrinsics.X86;
#endif
#if NET7_0_OR_GREATER
using System.Runtime.Intrinsics;
#endif

namespace CsvHelper;

internal static class ParsingStrategyFactory
{
	public static ICsvParsingStrategy Create(CsvParserOptions options)
	{
#if NET6_0_OR_GREATER
		if (options.ParsingStrategy.HasValue)
		{
			switch (options.ParsingStrategy)
			{
				case ParsingStrategy.Avx2:
					return new ParseAvx2(options);
#if NET7_0_OR_GREATER
				case ParsingStrategy.Vector256:
					return new ParseVector256(options);
#endif
				case ParsingStrategy.IndexOfAny:
					return new ParseIndexOfAny(options);
			}
		}

		if (Avx2.IsSupported)
		{
			return new ParseAvx2(options);
		}

#if NET7_0_OR_GREATER
		if (Vector256.IsHardwareAccelerated)
		{
			return new ParseVector256(options);
		}
#endif
#endif

		return new ParseIndexOfAny(options);
	}
}
