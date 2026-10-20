using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace CsvHelper;

/// <summary>
/// A record read without a record type, by <c>GetRecords&lt;dynamic&gt;()</c>: its fields are members named after the
/// header (<c>record.Name</c>), or <c>Field1</c>, <c>Field2</c> and so on without a header. It is also a read-only
/// dictionary of the values by name, and has typed getters.
/// </summary>
/// <remarks>
/// <para>
/// A record holds the bytes of its fields, not strings: a value becomes a string when it is read, each time it is read,
/// so the fields you never read cost nothing more. The values are strings (<see langword="null"/> for one of the
/// reader's <see cref="CsvReaderOptions.NullValues"/>); the typed getters, such as <see cref="GetInt32(string)"/>,
/// parse them as the reader's getters do.
/// </para>
/// <para>
/// The records of a reader share its schema, so <c>record.Name</c> is bound once to the column's index, and later reads
/// of it, on any record of the same header, go straight to the field. A column takes precedence over a member of this
/// class with the same name: with a column <c>FieldCount</c>, <c>record.FieldCount</c> is its value.
/// </para>
/// <para>
/// Records keep their bytes in blocks shared with the records read after them (or, for input in a byte array, in the
/// input itself), so keeping one record keeps its block in memory; <see cref="Detach"/> makes a copy that holds only its
/// own bytes. Records do not change, so they can be used on any thread.
/// </para>
/// </remarks>
public sealed class CsvDynamicRecord : IDynamicMetaObjectProvider, IReadOnlyDictionary<string, string?>
{
	private readonly CsvDynamicSchema schema;
	private readonly byte[] data;
	// Field k is data[offsets[first + k] .. offsets[first + k + 1] - 1), masked with int.MaxValue: as in the reader's
	// index, the high bit of entry k + 1 says that field k is quoted, and it is unquoted when it is read.
	private readonly int[] offsets;
	private readonly int first;
	private readonly int count;

	internal CsvDynamicRecord(CsvDynamicSchema schema, byte[] data, int[] offsets, int first, int count)
	{
		this.schema = schema;
		this.data = data;
		this.offsets = offsets;
		this.first = first;
		this.count = count;
	}

	/// <summary>The number of fields of the record.</summary>
	public int FieldCount => count;

	/// <summary>
	/// The names of the columns: the header names, or <c>Field1</c>, <c>Field2</c> and so on for each field of the record
	/// when the input has no header.
	/// </summary>
	public IReadOnlyList<string> Names
	{
		get
		{
			if (schema.HasHeader)
			{
				return schema.Names;
			}

			var names = new string[count];
			for (var i = 0; i < names.Length; i++)
			{
				names[i] = schema.NameOf(i)!;
			}

			return names;
		}
	}

	/// <summary>The schema of the record, which the records of a reader share.</summary>
	internal CsvDynamicSchema Schema => schema;

	/// <summary>
	/// Gets field <paramref name="index"/> as a string, or <see langword="null"/> when it is one of the reader's
	/// <see cref="CsvReaderOptions.NullValues"/>. A field past the end of the record reads as empty, unless the reader's
	/// <see cref="CsvReaderOptions.MissingField"/> is <see cref="CsvMissingFieldHandling.Throw"/>.
	/// </summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The value of the field.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">The record has no such field and missing fields throw.</exception>
	public string? this[int index] => schema.Value(GetSpan(index));

	/// <summary>Gets the field of the column called <paramref name="name"/>. See <see cref="this[int]"/>.</summary>
	/// <param name="name">The name of the column, matched as by <see cref="CsvReader.TryGetFieldIndex(string, out int)"/>.</param>
	/// <returns>The value of the field.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="KeyNotFoundException">No column has that name.</exception>
	/// <exception cref="IndexOutOfRangeException">The record has no field in that column and missing fields throw.</exception>
	public string? this[string name] => this[IndexOrThrow(name)];

	/// <summary>The number of names: of the header, or of the fields without a header.</summary>
	int IReadOnlyCollection<KeyValuePair<string, string?>>.Count => schema.HasHeader ? schema.KeyIndexes.Length : count;

	/// <summary>The names of the columns, each once.</summary>
	IEnumerable<string> IReadOnlyDictionary<string, string?>.Keys
	{
		get
		{
			foreach (var pair in this)
			{
				yield return pair.Key;
			}
		}
	}

	/// <summary>The values of the columns, in the order of <see cref="IReadOnlyDictionary{TKey, TValue}.Keys"/>.</summary>
	IEnumerable<string?> IReadOnlyDictionary<string, string?>.Values
	{
		get
		{
			foreach (var pair in this)
			{
				yield return pair.Value;
			}
		}
	}

	/// <summary>Gets field <paramref name="index"/> as UTF-8 bytes, decoded (without quotes or escape characters).</summary>
	/// <param name="index">The 0-based index of the field.</param>
	/// <returns>The value of the field; empty for an empty field, and for a field past the end of the record.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
	/// <exception cref="IndexOutOfRangeException">The record has no such field and missing fields throw.</exception>
	public ReadOnlySpan<byte> GetSpan(int index)
	{
		if ((uint)index >= (uint)count)
		{
			return Missing(index);
		}

		var start = offsets[first + index] & int.MaxValue;
		var next = offsets[first + index + 1];
		var field = new ReadOnlySpan<byte>(data, start, (next & int.MaxValue) - 1 - start);

		return next < 0 ? Unquote(field, schema.Quote) : field;
	}

	/// <summary>Whether the record has a column called <paramref name="name"/>.</summary>
	/// <param name="name">The name of the column, matched as by <see cref="CsvReader.TryGetFieldIndex(string, out int)"/>.</param>
	/// <returns><see langword="true"/> when the header has the column (without a header, when the record has the field).</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	public bool ContainsKey(string name) => IndexOf(name) >= 0;

	/// <summary>Gets the field of the column called <paramref name="name"/>, if there is one. See <see cref="this[int]"/>.</summary>
	/// <param name="name">The name of the column, matched as by <see cref="CsvReader.TryGetFieldIndex(string, out int)"/>.</param>
	/// <param name="value">The value of the field, or <see langword="null"/> when there is no such column.</param>
	/// <returns>Whether the record has the column.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="IndexOutOfRangeException">The record has no field in that column and missing fields throw.</exception>
	public bool TryGetValue(string name, out string? value)
	{
		var index = IndexOf(name);
		if (index < 0)
		{
			value = null;

			return false;
		}

		value = this[index];

		return true;
	}

	/// <summary>Parses the field of the column called <paramref name="name"/> as an <see cref="int"/>, as <see cref="CsvReader.GetInt32(int)"/> does.</summary>
	/// <param name="name">The name of the column, matched as by <see cref="CsvReader.TryGetFieldIndex(string, out int)"/>.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="KeyNotFoundException">No column has that name.</exception>
	/// <exception cref="FormatException">The field is not an integer.</exception>
	/// <exception cref="OverflowException">The field is outside the range of <see cref="int"/>.</exception>
	public int GetInt32(string name) => CsvParse.Int32(GetSpan(IndexOrThrow(name)), schema.Provider, schema.HideValues);

	/// <summary>Parses the field of the column called <paramref name="name"/> as a <see cref="long"/>, as <see cref="CsvReader.GetInt64(int)"/> does.</summary>
	/// <param name="name">The name of the column, matched as by <see cref="CsvReader.TryGetFieldIndex(string, out int)"/>.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="KeyNotFoundException">No column has that name.</exception>
	/// <exception cref="FormatException">The field is not an integer.</exception>
	/// <exception cref="OverflowException">The field is outside the range of <see cref="long"/>.</exception>
	public long GetInt64(string name) => CsvParse.Int64(GetSpan(IndexOrThrow(name)), schema.Provider, schema.HideValues);

	/// <summary>Parses the field of the column called <paramref name="name"/> as a <see cref="double"/>, as <see cref="CsvReader.GetDouble(int)"/> does.</summary>
	/// <param name="name">The name of the column, matched as by <see cref="CsvReader.TryGetFieldIndex(string, out int)"/>.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="KeyNotFoundException">No column has that name.</exception>
	/// <exception cref="FormatException">The field is not a number.</exception>
	public double GetDouble(string name) => CsvParse.Double(GetSpan(IndexOrThrow(name)), schema.Provider, schema.HideValues);

	/// <summary>Parses the field of the column called <paramref name="name"/> as a <see cref="decimal"/>, as <see cref="CsvReader.GetDecimal(int)"/> does.</summary>
	/// <param name="name">The name of the column, matched as by <see cref="CsvReader.TryGetFieldIndex(string, out int)"/>.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="KeyNotFoundException">No column has that name.</exception>
	/// <exception cref="FormatException">The field is not a number.</exception>
	/// <exception cref="OverflowException">The field is outside the range of <see cref="decimal"/>.</exception>
	public decimal GetDecimal(string name) => CsvParse.Decimal(GetSpan(IndexOrThrow(name)), schema.Provider, schema.HideValues);

	/// <summary>
	/// Parses the field of the column called <paramref name="name"/> as a <see cref="bool"/>, with the reader's
	/// <see cref="CsvReaderOptions.TrueValues"/> and <see cref="CsvReaderOptions.FalseValues"/>, as <see cref="CsvReader.GetBoolean(int)"/> does.
	/// </summary>
	/// <param name="name">The name of the column, matched as by <see cref="CsvReader.TryGetFieldIndex(string, out int)"/>.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="KeyNotFoundException">No column has that name.</exception>
	/// <exception cref="FormatException">The field is none of the true and false values.</exception>
	public bool GetBoolean(string name) => schema.Boolean(GetSpan(IndexOrThrow(name)));

	/// <summary>Parses the field of the column called <paramref name="name"/> as a <see cref="DateTime"/>, as <see cref="CsvReader.GetDateTime(int)"/> does.</summary>
	/// <param name="name">The name of the column, matched as by <see cref="CsvReader.TryGetFieldIndex(string, out int)"/>.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
	/// <exception cref="KeyNotFoundException">No column has that name.</exception>
	/// <exception cref="FormatException">The field is not a date and time.</exception>
	public DateTime GetDateTime(string name) => CsvParse.DateTime(GetSpan(IndexOrThrow(name)), schema.Provider, schema.HideValues);

	/// <summary>Copies the values into a new dictionary, which you can change, keyed by column name.</summary>
	/// <returns>The values by name, in column order.</returns>
	public Dictionary<string, string?> ToDictionary()
	{
		var dictionary = new Dictionary<string, string?>(((IReadOnlyCollection<KeyValuePair<string, string?>>)this).Count);
		foreach (var pair in this)
		{
			dictionary.Add(pair.Key, pair.Value);
		}

		return dictionary;
	}

	/// <summary>
	/// Makes a copy of the record that holds only its own bytes, for a record that you keep while dropping the others:
	/// a record read from a stream or a file keeps its whole block of records in memory, and one read from a byte array
	/// keeps the array.
	/// </summary>
	/// <returns>A record with the same values.</returns>
	public CsvDynamicRecord Detach()
	{
		var start = offsets[first] & int.MaxValue;
		var end = offsets[first + count] & int.MaxValue;
		var bytes = new byte[Math.Max(end - start, 0)];
		Array.Copy(data, start, bytes, 0, bytes.Length);
		var own = new int[count + 1];
		for (var i = 0; i <= count; i++)
		{
			// Moved to the start of the copy, with the quoted flag kept.
			own[i] = offsets[first + i] - start;
		}

		own[0] &= int.MaxValue;

		return new CsvDynamicRecord(schema, bytes, own, 0, count);
	}

	/// <summary>The names and values of the record, such as <c>Name: Ann, Count: 3</c>, for debugging.</summary>
	/// <returns>The names and values.</returns>
	public override string ToString()
	{
		var text = new StringBuilder();
		foreach (var pair in this)
		{
			if (text.Length > 0)
			{
				text.Append(", ");
			}

			text.Append(pair.Key).Append(": ").Append(pair.Value);
		}

		return text.ToString();
	}

	/// <summary>The names and values of the columns, in column order: each name once, with its first column.</summary>
	/// <returns>An enumerator of the names and values.</returns>
	public IEnumerator<KeyValuePair<string, string?>> GetEnumerator()
	{
		if (!schema.HasHeader)
		{
			for (var i = 0; i < count; i++)
			{
				yield return new KeyValuePair<string, string?>(schema.NameOf(i)!, this[i]);
			}

			yield break;
		}

		foreach (var index in schema.KeyIndexes)
		{
			yield return new KeyValuePair<string, string?>(schema.Names[index], this[index]);
		}
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	/// <summary>Gives the dynamic binder the object that binds <c>record.Name</c> to its column.</summary>
	/// <param name="parameter">The expression of the record in the binding.</param>
	/// <returns>The meta-object of the record.</returns>
	DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter) => new MetaObject(parameter, this);

	/// <summary>
	/// Gets the field of the column called <paramref name="name"/> as UTF-8 bytes, for writing: empty when the record has
	/// no field there.
	/// </summary>
	internal bool TryGetSpan(string name, out ReadOnlySpan<byte> span)
	{
		var index = IndexOf(name);
		span = (uint)index < (uint)count ? GetSpan(index) : default;

		return index >= 0;
	}

	/// <summary>The value that <c>record.Name</c> gets, for the rules of the binder.</summary>
	private object? GetMemberValue(int index) => this[index];

	private int IndexOf(string name)
	{
		ArgumentNullException.ThrowIfNull(name);
		var index = schema.IndexOf(name);

		// Without a header, Field5 exists only for records with five fields.
		return schema.HasHeader || index < count ? index : -1;
	}

	private int IndexOrThrow(string name)
	{
		var index = IndexOf(name);

		return index >= 0 ? index : throw new KeyNotFoundException($"The record has no column named '{name}'.");
	}

	/// <summary>
	/// The value of a quoted field, as the reader decodes it: without the quotes, with each doubled quote made one, and
	/// with any text after the closing quote (malformed) kept. A field that is never closed runs to its end.
	/// </summary>
	private static ReadOnlySpan<byte> Unquote(ReadOnlySpan<byte> raw, byte quote)
	{
		// Common case: "value" with no quotes inside, which needs no copy.
		var n = raw.Length;
		if (n >= 2 && raw[n - 1] == quote)
		{
			var middle = raw.Slice(1, n - 2);
			if (!CsvReader.ContainsByte(middle, quote))
			{
				return middle;
			}
		}

		var inner = raw.Slice(1);
		var q = inner.IndexOf(quote);
		if (q < 0)
		{
			return inner;
		}

		if (q == inner.Length - 1)
		{
			return inner.Slice(0, q);
		}

		// Doubled quotes, or text after the closing quote: a copy without the escapes.
		var output = new byte[inner.Length];
		inner.Slice(0, q).CopyTo(output);
		int i = q, d = q;
		while (true)
		{
			// inner[i] is a quote.
			i++;
			if (i >= inner.Length || inner[i] != quote)
			{
				inner.Slice(i).CopyTo(output.AsSpan(d));
				d += inner.Length - i;
				break;
			}

			output[d++] = quote;
			i++;
			while (i < inner.Length && inner[i] != quote)
			{
				output[d++] = inner[i++];
			}

			if (i == inner.Length)
			{
				break;
			}
		}

		return output.AsSpan(0, d);
	}

	private ReadOnlySpan<byte> Missing(int index)
	{
		if (index < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(index), index, "Field index cannot be negative.");
		}

		if (schema.ThrowOnMissing)
		{
			throw new IndexOutOfRangeException($"The record has {count} fields; field {index} does not exist.");
		}

		return default;
	}

	/// <summary>
	/// Binds <c>record.Name</c> to the column's index, with a rule that holds for every record of the same schema, so the
	/// binder's cache answers later reads without looking the name up again. Other operations (methods, indexers) are
	/// bound as for any object.
	/// </summary>
	private sealed class MetaObject : DynamicMetaObject
	{
		private static readonly MethodInfo GetValue =
			typeof(CsvDynamicRecord).GetMethod(nameof(GetMemberValue), BindingFlags.NonPublic | BindingFlags.Instance)!;
		private static readonly FieldInfo SchemaField =
			typeof(CsvDynamicRecord).GetField(nameof(schema), BindingFlags.NonPublic | BindingFlags.Instance)!;

		public MetaObject(Expression expression, CsvDynamicRecord record)
			: base(expression, BindingRestrictions.Empty, record)
		{
		}

		public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
		{
			var record = (CsvDynamicRecord)Value!;
			var self = Expression.Convert(Expression, typeof(CsvDynamicRecord));
			// The column of a name depends on the schema alone, so the rule holds for every record of this schema.
			var restrictions = BindingRestrictions.GetTypeRestriction(Expression, typeof(CsvDynamicRecord))
				.Merge(BindingRestrictions.GetExpressionRestriction(
					Expression.ReferenceEqual(Expression.Field(self, SchemaField), Expression.Constant(record.schema))));
			var index = record.schema.IndexOf(binder.Name);
			if (index < 0)
			{
				// A member of this class, such as FieldCount, or an error. The C# binder's rule checks only the type, and
				// its binders are shared by call sites, so the schema check is added back: a schema with a column of that
				// name must bind again.
				var fallback = binder.FallbackGetMember(new DynamicMetaObject(Expression, restrictions, record));

				return new DynamicMetaObject(fallback.Expression, fallback.Restrictions.Merge(restrictions));
			}

			return new DynamicMetaObject(Expression.Call(self, GetValue, Expression.Constant(index)), restrictions);
		}

		public override IEnumerable<string> GetDynamicMemberNames()
		{
			foreach (var pair in (CsvDynamicRecord)Value!)
			{
				yield return pair.Key;
			}
		}
	}
}
