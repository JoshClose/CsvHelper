using System;
using System.Collections.Generic;
using System.Globalization;

namespace CsvHelper;

/// <summary>
/// The columns of <see cref="CsvDynamicRecord"/>s, shared by every record of a reader: the header names and their lookup,
/// and the options that turn fields into values. Readers with the same header and options share one, so the
/// <c>dynamic</c> member accesses bound for the records of one file are reused for the next.
/// </summary>
internal sealed class CsvDynamicSchema
{
	// The schemas made so far, by header and options; cleared when it grows past MaxInterned.
	private const int MaxInterned = 256;
	private static readonly Dictionary<(string Names, CsvReaderOptions Options), CsvDynamicSchema> Interned = [];

	private readonly Dictionary<string, int> exact;
	private readonly Dictionary<string, int> loose;
	private readonly Func<string, string>? normalize;
	private readonly byte[][]? nullValues;
	private readonly CsvBooleanText? booleans;

	private CsvDynamicSchema(string[] names, string[] keys, CsvReaderOptions options, byte[][]? nullValues, CsvBooleanText? booleans)
	{
		Names = names;
		HasHeader = options.HasHeader;
		normalize = options.NormalizeHeaderName;
		Provider = options.FormatProvider ?? CultureInfo.InvariantCulture;
		HideValues = !options.IncludeValuesInExceptions;
		ThrowOnMissing = options.MissingField == CsvMissingFieldHandling.Throw;
		Quote = (byte)options.Quote;
		this.nullValues = nullValues;
		this.booleans = booleans;
		exact = new Dictionary<string, int>(keys.Length, StringComparer.Ordinal);
		loose = new Dictionary<string, int>(keys.Length, StringComparer.OrdinalIgnoreCase);
		for (var i = 0; i < keys.Length; i++)
		{
			exact.TryAdd(keys[i], i);
			loose.TryAdd(keys[i].Trim(), i);
		}

		// The dictionary view has one key per name: the first column with it, as in the lookups.
		var seen = new HashSet<string>(StringComparer.Ordinal);
		var keyIndexes = new List<int>(names.Length);
		for (var i = 0; i < names.Length; i++)
		{
			if (seen.Add(names[i]))
			{
				keyIndexes.Add(i);
			}
		}

		KeyIndexes = keyIndexes.ToArray();
	}

	/// <summary>The header names, as the header has them; empty when the input has no header.</summary>
	public string[] Names { get; }

	/// <summary>The columns whose names are the keys of the dictionary view: the first column of each name.</summary>
	public int[] KeyIndexes { get; }

	/// <summary>Whether the input has a header; without one, the fields are called <c>Field1</c>, <c>Field2</c> and so on.</summary>
	public bool HasHeader { get; }

	/// <summary>The format provider of the typed getters.</summary>
	public IFormatProvider Provider { get; }

	/// <summary>Whether exception messages leave values out (<see cref="CsvReaderOptions.IncludeValuesInExceptions"/> is off).</summary>
	public bool HideValues { get; }

	/// <summary>Whether a field past the end of a record throws instead of reading as empty.</summary>
	public bool ThrowOnMissing { get; }

	/// <summary>The quote character, which records remove from their quoted fields when they are read.</summary>
	public byte Quote { get; }

	/// <summary>
	/// The schema of a reader's records: an existing one for the same header and options, else a new one.
	/// </summary>
	/// <param name="names">The header names; empty without a header.</param>
	/// <param name="keys">The names as they are matched (<see cref="CsvReaderOptions.NormalizeHeaderName"/> applied).</param>
	/// <param name="options">The reader's options.</param>
	/// <param name="nullValues">The UTF-8 of <see cref="CsvReaderOptions.NullValues"/>, or <see langword="null"/>.</param>
	/// <param name="booleans">The true and false values of the options, or <see langword="null"/> for the built-in ones.</param>
	/// <returns>The schema.</returns>
	public static CsvDynamicSchema Get(string[] names, string[] keys, CsvReaderOptions options, byte[][]? nullValues, CsvBooleanText? booleans)
	{
		var key = (string.Join("\u0000", names), options);
		lock (Interned)
		{
			if (Interned.TryGetValue(key, out var schema))
			{
				return schema;
			}

			if (Interned.Count >= MaxInterned)
			{
				Interned.Clear();
			}

			schema = new CsvDynamicSchema(names, keys, options, nullValues, booleans);
			Interned.Add(key, schema);

			return schema;
		}
	}

	/// <summary>
	/// The index of the column called <paramref name="name"/>, matched as <see cref="CsvReader.TryGetFieldIndex(string, out int)"/>
	/// matches it (or <c>Field1</c> for the first field when there is no header); -1 when there is none.
	/// </summary>
	/// <param name="name">The name of the column.</param>
	/// <returns>The 0-based index of the column, or -1.</returns>
	public int IndexOf(string name)
	{
		if (!HasHeader)
		{
			return name.Length > 5 && name.StartsWith("Field", StringComparison.Ordinal) &&
				   int.TryParse(name.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0
				? number - 1
				: -1;
		}

		var key = normalize is null ? name : normalize(name) ?? "";
		if (exact.TryGetValue(key, out var index))
		{
			return index;
		}

		return loose.TryGetValue(key.Trim(), out index) ? index : -1;
	}

	/// <summary>The name of the column at <paramref name="index"/>: its header name, or <c>Field1</c> and so on without a header.</summary>
	/// <param name="index">The 0-based index of the column.</param>
	/// <returns>The name, or <see langword="null"/> for a field past the end of the header.</returns>
	public string? NameOf(int index)
	{
		if (!HasHeader)
		{
			return "Field" + (index + 1).ToString(CultureInfo.InvariantCulture);
		}

		return index < Names.Length ? Names[index] : null;
	}

	/// <summary>The value of a field: <see langword="null"/> for one of the null values, else its text.</summary>
	/// <param name="utf8">The field, decoded, as UTF-8.</param>
	/// <returns>The value.</returns>
	public string? Value(ReadOnlySpan<byte> utf8)
	{
		if (nullValues is not null && IsNullValue(utf8))
		{
			return null;
		}

		return utf8.Length == 0 ? string.Empty : CsvReader.Utf8ToString(utf8);
	}

	/// <summary>Parses a field as a <see cref="bool"/> with the options' true and false values.</summary>
	/// <param name="utf8">The field, decoded, as UTF-8.</param>
	/// <returns>The value.</returns>
	/// <exception cref="FormatException">The field is none of the true and false values.</exception>
	public bool Boolean(ReadOnlySpan<byte> utf8) => booleans is null ? CsvParse.Boolean(utf8, HideValues) : booleans.Parse(utf8, HideValues);

	private bool IsNullValue(ReadOnlySpan<byte> utf8)
	{
		foreach (var value in nullValues!)
		{
			if (utf8.SequenceEqual(value))
			{
				return true;
			}
		}

		return false;
	}
}
