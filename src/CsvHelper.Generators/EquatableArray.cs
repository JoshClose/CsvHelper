using System;
using System.Collections;
using System.Collections.Generic;

namespace CsvHelper.Generators;

/// <summary>An array compared by its elements, so models that hold one can be cached by the incremental pipeline.</summary>
/// <typeparam name="T">The type of the elements, compared with <see cref="IEquatable{T}.Equals(T)"/>.</typeparam>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T> where T : IEquatable<T>
{
	private readonly T[]? items;

	/// <summary>Wraps <paramref name="items"/>, which must not change afterwards.</summary>
	/// <param name="items">The elements.</param>
	public EquatableArray(T[] items) => this.items = items;

	/// <summary>An array without elements.</summary>
	public static EquatableArray<T> Empty => new([]);

	/// <summary>The number of elements; 0 for the <see langword="default"/> value too.</summary>
	public int Length => items?.Length ?? 0;

	/// <summary>Gets the element at <paramref name="index"/>.</summary>
	/// <param name="index">The 0-based index of the element.</param>
	/// <returns>The element.</returns>
	/// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is negative or not less than <see cref="Length"/>.</exception>
	public T this[int index] => items![index];

	/// <summary>Whether the two arrays have the same length and equal elements in the same order.</summary>
	/// <param name="other">The array to compare with.</param>
	/// <returns><see langword="true"/> when every element equals the one at the same index of <paramref name="other"/>.</returns>
	public bool Equals(EquatableArray<T> other)
	{
		if (Length != other.Length) return false;
		for (var i = 0; i < Length; i++)
		{
			if (!items![i].Equals(other.items![i])) return false;
		}

		return true;
	}

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

	/// <summary>A hash code combined from the hash codes of the elements, so equal arrays have equal hash codes.</summary>
	/// <returns>The hash code.</returns>
	public override int GetHashCode()
	{
		var hash = 17;
		for (var i = 0; i < Length; i++) hash = hash * 31 + items![i].GetHashCode();

		return hash;
	}

	/// <inheritdoc/>
	public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(items ?? [])).GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Creates <see cref="EquatableArray{T}"/> values.</summary>
internal static class EquatableArray
{
	/// <summary>Copies the elements of a list into an <see cref="EquatableArray{T}"/>.</summary>
	/// <typeparam name="T">The type of the elements.</typeparam>
	/// <param name="items">The list to copy.</param>
	/// <returns>An array with the elements of <paramref name="items"/>, which later changes to the list do not affect.</returns>
	public static EquatableArray<T> ToEquatableArray<T>(this List<T> items) where T : IEquatable<T> => new(items.ToArray());
}
