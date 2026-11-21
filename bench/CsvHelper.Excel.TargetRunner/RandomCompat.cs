#if !NET
using System;

// In the namespace of the linked OrderRecord.cs, whose Generate calls it.
namespace CsvHelper.Benchmarks;

/// <summary>
/// .NET's <c>Random.NextInt64(long, long)</c>, which .NET Framework lacks, for <see cref="OrderRecord.Generate"/>: the
/// algorithm of .NET's seeded <see cref="Random"/>, so that every target generates the same orders from the same seed
/// (the write command prints a checksum of them).
/// </summary>
internal static class RandomCompat
{
	/// <summary>Returns a random integer from <paramref name="minValue"/> up to <paramref name="maxValue"/>, excluded.</summary>
	/// <param name="random">The generator, seeded.</param>
	/// <param name="minValue">The least value returned.</param>
	/// <param name="maxValue">The bound, more than <paramref name="minValue"/>, which is never returned.</param>
	/// <returns>The integer.</returns>
	/// <remarks>
	/// As .NET's <c>Net5CompatSeedImpl.NextInt64</c>: the smallest power of two that holds the range, then 64 random bits from
	/// three <see cref="Random.Next(int)"/> calls (22, 22 and 20 bits), their top bits taken, until one is in the range.
	/// </remarks>
	public static long NextInt64(this Random random, long minValue, long maxValue)
	{
		var range = (ulong)(maxValue - minValue);
		if (range <= 1)
		{
			return minValue;
		}

		var bits = 0;
		while (bits < 64 && (1UL << bits) < range)
		{
			bits++;
		}

		while (true)
		{
			var value = NextUInt64(random) >> (64 - bits);
			if (value < range)
			{
				return (long)value + minValue;
			}
		}
	}

	/// <summary>Returns 64 random bits, as .NET's seeded <see cref="Random"/> makes them.</summary>
	/// <param name="random">The generator.</param>
	/// <returns>The bits.</returns>
	private static ulong NextUInt64(Random random)
	{
		return (ulong)(uint)random.Next(1 << 22)
			| ((ulong)(uint)random.Next(1 << 22) << 22)
			| ((ulong)(uint)random.Next(1 << 20) << 44);
	}
}
#endif
