#if !NET
using System.Runtime.CompilerServices;
using System.Text;

namespace CsvHelper.Tests;

/// <summary>.NET members the tests use that .NET Framework 4.8 lacks.</summary>
internal static class TestCompat
{
	/// <summary>Like .NET's Random.NextInt64(): a non-negative 63-bit value.</summary>
	public static long NextInt64(this Random rng) => (long)(NextUInt64(rng) >> 1);

	public static long NextInt64(this Random rng, long maxValue) => (long)(NextUInt64(rng) % (ulong)maxValue);

	private static ulong NextUInt64(Random rng) => ((ulong)rng.Next() << 33) ^ ((ulong)rng.Next() << 2) ^ (ulong)rng.Next(4);

	public static IOrderedEnumerable<T> Order<T>(this IEnumerable<T> source) => source.OrderBy(x => x);

	public static IEnumerable<(TFirst First, TSecond Second)> Zip<TFirst, TSecond>(this IEnumerable<TFirst> first, IEnumerable<TSecond> second) =>
		first.Zip(second, (a, b) => (a, b));

	extension(double)
	{
		public static bool IsFinite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);
	}

	extension(Encoding)
	{
		public static Encoding Latin1 => Encoding.GetEncoding(28591);
	}
}

/// <summary>.NET's ReferenceEqualityComparer.</summary>
internal sealed class ReferenceEqualityComparer : IEqualityComparer<object?>
{
	public static ReferenceEqualityComparer Instance { get; } = new();

	public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

	public int GetHashCode(object? obj) => RuntimeHelpers.GetHashCode(obj!);
}
#endif
