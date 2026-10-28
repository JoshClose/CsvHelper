using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.Globalization;

namespace CsvHelper;

/// <summary>
/// An <see cref="IDataReader"/> over a <see cref="CsvReader"/>, for APIs that read data readers: <c>DataTable.Load</c>,
/// <c>SqlBulkCopy.WriteToServer</c> and the like. Every column is a <see cref="string"/> column named after the header
/// (<c>Column1</c>, <c>Column2</c> and so on without one); the typed getters (<see cref="GetInt32"/> and the others)
/// parse fields as the reader's <see cref="CsvReader.Parse{T}(int)"/> does.
/// </summary>
/// <remarks>
/// A field is <see cref="DBNull"/> (<see cref="IsDBNull"/>) when it is one of the reader's
/// <see cref="CsvReaderOptions.NullValues"/>; an empty field is an empty string unless <c>""</c> is one of them. The
/// columns are those of the header, or, without a header, of the first record.
/// </remarks>
public sealed class CsvDataReader : DbDataReader
{
	private readonly CsvReader reader;
	private readonly bool leaveOpen;
	private readonly string[] names;
	private bool pending;   // without a header, the first record was read to count the columns and is not returned yet
	private bool closed;

	/// <summary>Creates a data reader over <paramref name="reader"/>, which it reads from its current position.</summary>
	/// <param name="reader">The CSV reader. Its header is read now, or, without one, its first record.</param>
	/// <param name="leaveOpen">Whether <paramref name="reader"/> stays open when the data reader is closed.</param>
	/// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
	/// <exception cref="CsvFormatException">The header is malformed and the options say to throw.</exception>
	public CsvDataReader(CsvReader reader, bool leaveOpen = false)
	{
		ArgumentNullException.ThrowIfNull(reader);
		this.reader = reader;
		this.leaveOpen = leaveOpen;
		var headers = reader.Headers;
		if (headers.Count > 0)
		{
			names = [.. headers];

			return;
		}

		pending = reader.Read();
		names = new string[pending ? reader.FieldCount : 0];
		for (var i = 0; i < names.Length; i++)
		{
			names[i] = "Column" + (i + 1).ToString(CultureInfo.InvariantCulture);
		}
	}

	/// <summary>Always 0: CSV is not nested.</summary>
	public override int Depth => 0;

	/// <summary>The number of columns: those of the header, or of the first record.</summary>
	public override int FieldCount => names.Length;

	/// <summary><see langword="true"/> while the data reader is open; whether records remain is only known by reading.</summary>
	public override bool HasRows => !closed;

	/// <summary>Whether <see cref="Close"/> has been called.</summary>
	public override bool IsClosed => closed;

	/// <summary>Always -1: reading changes no records.</summary>
	public override int RecordsAffected => -1;

	/// <summary>Gets the value of column <paramref name="ordinal"/>. See <see cref="GetValue"/>.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	public override object this[int ordinal] => GetValue(ordinal);

	/// <summary>Gets the value of the column called <paramref name="name"/>. See <see cref="GetValue"/>.</summary>
	/// <param name="name">The name of the column.</param>
	public override object this[string name] => GetValue(GetOrdinal(name));

	/// <summary>Advances to the next record. See <see cref="CsvReader.Read"/>.</summary>
	/// <returns>Whether the data reader is positioned on a record.</returns>
	/// <exception cref="InvalidOperationException">The data reader is closed.</exception>
	public override bool Read()
	{
		ThrowIfClosed();
		if (pending)
		{
			pending = false;

			return true;
		}

		return reader.Read();
	}

	/// <summary>Always <see langword="false"/>: a CSV file has one result set.</summary>
	/// <returns><see langword="false"/>.</returns>
	public override bool NextResult() => false;

	/// <summary>Gets the name of column <paramref name="ordinal"/>.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <returns>The header name, or <c>Column</c> and the 1-based number without a header.</returns>
	/// <exception cref="IndexOutOfRangeException"><paramref name="ordinal"/> is not a column.</exception>
	public override string GetName(int ordinal) => names[ordinal];

	/// <summary>Gets the index of the column called <paramref name="name"/>, matched as by <see cref="CsvReader.GetFieldIndex(string)"/>.</summary>
	/// <param name="name">The name of the column.</param>
	/// <returns>The 0-based index of the column.</returns>
	/// <exception cref="IndexOutOfRangeException">No column has that name.</exception>
	public override int GetOrdinal(string name)
	{
		ArgumentNullException.ThrowIfNull(name);
		if (reader.Headers.Count > 0 && reader.TryGetFieldIndex(name, out var index))
		{
			return index;
		}

		var position = Array.IndexOf(names, name);

		return position >= 0 ? position : throw new IndexOutOfRangeException($"No column is named '{name}'.");
	}

	/// <summary>Always <c>String</c>.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <returns><c>String</c>.</returns>
	public override string GetDataTypeName(int ordinal) => "String";

	/// <summary>Always <see cref="string"/>: CSV fields are text.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <returns>The type <see cref="string"/>.</returns>
#if NET
	[return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
	public override Type GetFieldType(int ordinal) => typeof(string);

	/// <summary>Gets the value of column <paramref name="ordinal"/>: its text, or <see cref="DBNull.Value"/> when <see cref="IsDBNull"/> says so.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <returns>A <see cref="string"/>, or <see cref="DBNull.Value"/>.</returns>
	public override object GetValue(int ordinal) => (object?)reader.GetStringOrNull(ordinal) ?? DBNull.Value;

	/// <summary>Copies the values of the current record into <paramref name="values"/>. See <see cref="GetValue"/>.</summary>
	/// <param name="values">Receives the values, up to its length.</param>
	/// <returns>The number of values copied.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
	public override int GetValues(object[] values)
	{
		ArgumentNullException.ThrowIfNull(values);
		var count = Math.Min(values.Length, names.Length);
		for (var i = 0; i < count; i++)
		{
			values[i] = GetValue(i);
		}

		return count;
	}

	/// <summary>Whether the field is one of the reader's <see cref="CsvReaderOptions.NullValues"/>.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <returns><see langword="true"/> for a null value.</returns>
	public override bool IsDBNull(int ordinal) => reader.GetStringOrNull(ordinal) is null;

	/// <summary>Gets the field as a string. See <see cref="CsvReader.GetString(int)"/>.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <returns>The value of the field.</returns>
	public override string GetString(int ordinal) => reader.GetString(ordinal);

	/// <summary>Parses the field as a <see cref="bool"/>. See <see cref="CsvReader.Parse{T}(int)"/>.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The field is not a boolean.</exception>
	public override bool GetBoolean(int ordinal) => reader.Parse<bool>(ordinal);

	/// <summary>Parses the field as a <see cref="byte"/>, in the reader's format provider.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The field is not a number.</exception>
	/// <exception cref="OverflowException">The field is outside the range of <see cref="byte"/>.</exception>
	public override byte GetByte(int ordinal) => checked((byte)reader.Parse<int>(ordinal));

	/// <summary>Copies the UTF-8 bytes of the field into <paramref name="buffer"/>.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <param name="dataOffset">The offset in the field to start copying from.</param>
	/// <param name="buffer">Receives the bytes, or <see langword="null"/> to get the length of the field.</param>
	/// <param name="bufferOffset">The offset in <paramref name="buffer"/> to copy to.</param>
	/// <param name="length">The largest number of bytes to copy.</param>
	/// <returns>The number of bytes copied, or the length of the field when <paramref name="buffer"/> is <see langword="null"/>.</returns>
	public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
	{
		var field = reader.GetSpan(ordinal);
		if (buffer is null)
		{
			return field.Length;
		}

		var count = (int)Math.Max(0, Math.Min(length, field.Length - dataOffset));
		field.Slice((int)dataOffset, count).CopyTo(buffer.AsSpan(bufferOffset));

		return count;
	}

	/// <summary>Gets the first character of the field.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <returns>The first character.</returns>
	/// <exception cref="InvalidCastException">The field is empty.</exception>
	public override char GetChar(int ordinal)
	{
		var value = reader.GetString(ordinal);

		return value.Length > 0 ? value[0] : throw new InvalidCastException("The field is empty.");
	}

	/// <summary>Copies the characters of the field into <paramref name="buffer"/>.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <param name="dataOffset">The offset in the field to start copying from.</param>
	/// <param name="buffer">Receives the characters, or <see langword="null"/> to get the length of the field.</param>
	/// <param name="bufferOffset">The offset in <paramref name="buffer"/> to copy to.</param>
	/// <param name="length">The largest number of characters to copy.</param>
	/// <returns>The number of characters copied, or the length of the field when <paramref name="buffer"/> is <see langword="null"/>.</returns>
	public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
	{
		var value = reader.GetString(ordinal);
		if (buffer is null)
		{
			return value.Length;
		}

		var count = (int)Math.Max(0, Math.Min(length, value.Length - dataOffset));
		value.CopyTo((int)dataOffset, buffer, bufferOffset, count);

		return count;
	}

	/// <summary>Parses the field as a <see cref="DateTime"/>. See <see cref="CsvReader.Parse{T}(int)"/>.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The field is not a date and time.</exception>
	public override DateTime GetDateTime(int ordinal) => reader.Parse<DateTime>(ordinal);

	/// <summary>Parses the field as a <see cref="decimal"/>. See <see cref="CsvReader.Parse{T}(int)"/>.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The field is not a number.</exception>
	public override decimal GetDecimal(int ordinal) => reader.Parse<decimal>(ordinal);

	/// <summary>Parses the field as a <see cref="double"/>. See <see cref="CsvReader.Parse{T}(int)"/>.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The field is not a number.</exception>
	public override double GetDouble(int ordinal) => reader.Parse<double>(ordinal);

	/// <summary>Parses the field as a <see cref="float"/>. See <see cref="CsvReader.Parse{T}(int)"/>.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The field is not a number.</exception>
	public override float GetFloat(int ordinal) => reader.Parse<float>(ordinal);

	/// <summary>Parses the field as a <see cref="Guid"/>. See <see cref="CsvReader.Parse{T}(int)"/>.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The field is not a GUID.</exception>
	public override Guid GetGuid(int ordinal) => reader.Parse<Guid>(ordinal);

	/// <summary>Parses the field as a <see cref="short"/>, in the reader's format provider.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The field is not a number.</exception>
	/// <exception cref="OverflowException">The field is outside the range of <see cref="short"/>.</exception>
	public override short GetInt16(int ordinal) => checked((short)reader.Parse<int>(ordinal));

	/// <summary>Parses the field as an <see cref="int"/>. See <see cref="CsvReader.Parse{T}(int)"/>.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The field is not a number.</exception>
	public override int GetInt32(int ordinal) => reader.Parse<int>(ordinal);

	/// <summary>Parses the field as a <see cref="long"/>. See <see cref="CsvReader.Parse{T}(int)"/>.</summary>
	/// <param name="ordinal">The 0-based index of the column.</param>
	/// <returns>The parsed value.</returns>
	/// <exception cref="FormatException">The field is not a number.</exception>
	public override long GetInt64(int ordinal) => reader.Parse<long>(ordinal);

	/// <summary>Returns an enumerator of the records, as <see cref="IDataRecord"/>s.</summary>
	/// <returns>An enumerator that calls <see cref="Read"/>.</returns>
	public override IEnumerator GetEnumerator() => new DbEnumerator(this, closeReader: false);

	/// <summary>
	/// Describes the columns for <c>DataTable.Load</c> and other consumers: their names, ordinals and type
	/// (<see cref="string"/>), all of them nullable.
	/// </summary>
	/// <returns>A table with a row per column.</returns>
#if NET
	// The DataType column holds typeof(string), which is always kept; DbDataReader implementations build the table so.
	[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "The DataType column only holds typeof(string).")]
#endif
	public override DataTable GetSchemaTable()
	{
		var table = new DataTable("SchemaTable") { Locale = CultureInfo.InvariantCulture };
		table.Columns.Add(SchemaTableColumn.ColumnName, typeof(string));
		table.Columns.Add(SchemaTableColumn.ColumnOrdinal, typeof(int));
		table.Columns.Add(SchemaTableColumn.ColumnSize, typeof(int));
		table.Columns.Add(SchemaTableColumn.NumericPrecision, typeof(short));
		table.Columns.Add(SchemaTableColumn.NumericScale, typeof(short));
		table.Columns.Add(SchemaTableColumn.DataType, typeof(Type));
		table.Columns.Add(SchemaTableColumn.ProviderType, typeof(int));
		table.Columns.Add(SchemaTableColumn.IsLong, typeof(bool));
		table.Columns.Add(SchemaTableColumn.AllowDBNull, typeof(bool));
		table.Columns.Add(SchemaTableOptionalColumn.IsReadOnly, typeof(bool));
		table.Columns.Add(SchemaTableOptionalColumn.IsRowVersion, typeof(bool));
		table.Columns.Add(SchemaTableColumn.IsUnique, typeof(bool));
		table.Columns.Add(SchemaTableColumn.IsKey, typeof(bool));
		table.Columns.Add(SchemaTableOptionalColumn.IsAutoIncrement, typeof(bool));
		table.Columns.Add(SchemaTableColumn.BaseColumnName, typeof(string));
		for (var i = 0; i < names.Length; i++)
		{
			table.Rows.Add(names[i], i, -1, (short)0, (short)0, typeof(string), (int)DbType.String, false, true, true, false, false, false, false, names[i]);
		}

		return table;
	}

	/// <summary>Closes the data reader, and disposes the CSV reader unless it was created with <c>leaveOpen</c>.</summary>
	public override void Close()
	{
		if (closed)
		{
			return;
		}

		closed = true;
		if (!leaveOpen)
		{
			reader.Dispose();
		}
	}

	/// <summary>Closes the data reader. See <see cref="Close"/>.</summary>
	/// <param name="disposing">Whether the call comes from <see cref="IDisposable.Dispose"/>.</param>
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			Close();
		}

		base.Dispose(disposing);
	}

	private void ThrowIfClosed()
	{
		if (closed)
		{
			throw new InvalidOperationException("The data reader is closed.");
		}
	}
}
