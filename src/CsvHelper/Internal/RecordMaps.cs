using System;
using System.Linq;
using System.Threading;

namespace CsvHelper;

/// <summary>
/// The <see cref="CsvRecordMapAttribute"/> classes registered for <typeparamref name="T"/>, and the lock under which the
/// registrations of <typeparamref name="T"/> change. While one map maps the type, its mapper, record writer and options
/// are the type's defaults; with several, the type has none, and the lookups that would use them throw, naming the maps.
/// </summary>
/// <typeparam name="T">The type of the records.</typeparam>
internal static class RecordMaps<T>
{
	// Replaced as a whole under Gate, so that readers see a complete array without taking the lock.
	private static Type[] maps = [];

	/// <summary>
	/// The lock of every registration of <typeparamref name="T"/>: its maps, and its default mapper, record writer and
	/// options, so that the defaults always follow from the maps and from the explicit registrations.
	/// </summary>
	public static readonly object Gate = new();

	/// <summary>Whether several maps are registered, so that <typeparamref name="T"/> has no default from a map.</summary>
	public static bool Several => Volatile.Read(ref maps).Length > 1;

	/// <summary>Whether any map is registered.</summary>
	public static bool Any => Volatile.Read(ref maps).Length > 0;

	/// <summary>
	/// Adds a map class, unless it is registered already. The caller holds <see cref="Gate"/>.
	/// </summary>
	/// <param name="map">The map class.</param>
	/// <returns>Whether <paramref name="map"/> is the only map of <typeparamref name="T"/>.</returns>
	public static bool Add(Type map)
	{
		var current = maps;
		if (Array.IndexOf(current, map) < 0)
		{
			Volatile.Write(ref maps, [.. current, map]);
		}

		return maps.Length == 1;
	}

	/// <summary>
	/// The exception of a lookup of <typeparamref name="T"/>'s default when several maps map it.
	/// </summary>
	/// <param name="what">What the type has no default of, such as <c>"mapper"</c>.</param>
	/// <param name="register">The method that registers one explicitly, such as <c>"CsvRecordMapper.Register"</c>.</param>
	/// <returns>The exception, which names the maps.</returns>
	public static InvalidOperationException Ambiguous(string what, string register)
	{
		var names = string.Join(", ", Volatile.Read(ref maps).Select(m => m.ToString()));

		return new InvalidOperationException(
			$"{typeof(T)} is mapped by several [CsvRecordMap] classes ({names}), so it has no default {what}. " +
			$"Keep one map class for the type, or register the {what} with {register}.");
	}
}
