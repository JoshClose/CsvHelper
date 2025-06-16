using CsvHelper.Configuration;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;
#endif

namespace CsvHelper;

internal static class StrategyFactory
{
	public static ICsvParsingStrategy Create(CsvParserOptions options)
	{
#if NET8_0_OR_GREATER
		if (options.Strategy.HasValue)
		{
			switch (options.Strategy)
			{
				case ParsingStrategy.Avx2:
					return new Avx2Strategy(options);
				case ParsingStrategy.Vector256:
					return new Vector256Strategy(options);
				case ParsingStrategy.IndexOfAny:
					return new IndexOfAnyStrategy(options);
			}
		}

		if (Avx2.IsSupported)
		{
			return new Avx2Strategy(options);
		}

		if (Vector256.IsHardwareAccelerated)
		{
			return new Vector256Strategy(options);
		}
#endif

		return new IndexOfAnyStrategy(options);
	}
}
