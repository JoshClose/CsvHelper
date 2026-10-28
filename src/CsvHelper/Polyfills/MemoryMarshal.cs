#if !NET
using System;
using System.Runtime.CompilerServices;

namespace CsvHelper;

/// <summary>
/// Stands in for <c>System.Runtime.InteropServices.MemoryMarshal</c> inside the library: types of the CsvHelper
/// namespace are found before imported ones, so the code calls the same members on every target. Adds
/// GetArrayDataReference, which .NET Standard lacks. There is deliberately no CreateSpan/CreateReadOnlySpan: without
/// runtime support a span made from a ref would hold a pointer the GC does not track.
/// </summary>
internal static class MemoryMarshal
{
	/// <summary>
	/// A reference to the first element of an array; stands in for <c>MemoryMarshal.GetArrayDataReference</c>, which .NET
	/// Standard lacks.
	/// </summary>
	/// <typeparam name="T">The type of the elements.</typeparam>
	/// <param name="array">The array.</param>
	/// <returns>
	/// A reference to element 0, or, for an empty array, the reference of its empty span (where element 0 would be),
	/// which must not be read.
	/// </returns>
	// The element reference is cheaper than a span here (no type check for arrays of value types); the hot paths do not
	// use it for arrays of references, whose element references cost a type check either way.
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref T GetArrayDataReference<T>(T[] array) =>
		ref array.Length != 0 ? ref array[0] : ref System.Runtime.InteropServices.MemoryMarshal.GetReference(array.AsSpan());

	/// <summary>A reference to the first element of a span; forwards to <c>MemoryMarshal.GetReference(Span&lt;T&gt;)</c>.</summary>
	/// <typeparam name="T">The type of the elements.</typeparam>
	/// <param name="span">The span.</param>
	/// <returns>A reference to element 0 of <paramref name="span"/>, which must not be read when the span is empty.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref T GetReference<T>(Span<T> span) => ref System.Runtime.InteropServices.MemoryMarshal.GetReference(span);

	/// <summary>A reference to the first element of a span; forwards to <c>MemoryMarshal.GetReference(ReadOnlySpan&lt;T&gt;)</c>.</summary>
	/// <typeparam name="T">The type of the elements.</typeparam>
	/// <param name="span">The span.</param>
	/// <returns>A reference to element 0 of <paramref name="span"/>, which must not be read when the span is empty.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref T GetReference<T>(ReadOnlySpan<T> span) => ref System.Runtime.InteropServices.MemoryMarshal.GetReference(span);

	/// <summary>Gets the array behind a memory; forwards to <c>MemoryMarshal.TryGetArray</c>.</summary>
	/// <typeparam name="T">The type of the elements.</typeparam>
	/// <param name="memory">The memory.</param>
	/// <param name="segment">The part of the array that <paramref name="memory"/> covers, when it is backed by an array.</param>
	/// <returns>Whether <paramref name="memory"/> is backed by an array.</returns>
	public static bool TryGetArray<T>(ReadOnlyMemory<T> memory, out ArraySegment<T> segment) =>
		System.Runtime.InteropServices.MemoryMarshal.TryGetArray(memory, out segment);
}
#endif
