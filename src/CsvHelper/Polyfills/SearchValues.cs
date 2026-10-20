#if !NET
namespace System.Buffers
{
	/// <summary>The part of .NET's <c>System.Buffers.SearchValues</c> that the library uses: a small set of bytes.</summary>
	internal static class SearchValues
	{
		/// <summary>Creates a set of bytes to search for; stands in for <c>SearchValues.Create(ReadOnlySpan&lt;byte&gt;)</c>.</summary>
		/// <param name="values">The bytes of the set, which are copied.</param>
		/// <returns>The set, whose bytes the polyfill extensions (<c>ContainsAny</c>) search for with <c>IndexOfAny</c>.</returns>
		public static SearchValues<byte> Create(ReadOnlySpan<byte> values) => new(values.ToArray());
	}

	internal sealed class SearchValues<T>(T[] values) where T : IEquatable<T>
	{
		/// <summary>The values of the set, searched for with <c>IndexOfAny</c>.</summary>
		public readonly T[] Values = values;
	}
}
#endif
