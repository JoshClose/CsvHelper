using System;
using System.Runtime.CompilerServices;

namespace CsvHelper;

/// <summary>
/// Writes records of type <typeparamref name="T"/> as fields of a <see cref="CsvWriter"/>, in the columns that a
/// <see cref="CsvRecordMapper{T}"/> reads. The CsvHelper source generator writes one for each
/// <see cref="CsvRecordAttribute"/> type; <see cref="CsvWriter.WriteRecords{T}(System.Collections.Generic.IEnumerable{T})"/>
/// and the other record methods of the writer use it.
/// </summary>
/// <remarks>
/// <para>
/// A record writer does not depend on the <see cref="CsvWriter"/> it writes to and does not change after it is created,
/// so it can be used with any writer, on any thread.
/// </para>
/// <para>
/// To write one by hand, write the column names in <see cref="WriteHeader"/> (with <see cref="WriteColumnName"/>, which
/// puts <see cref="ColumnPrefix"/> in front) and the fields of a record in <see cref="Write"/>, as many as
/// <see cref="FieldCount"/> says, with the writer's <c>WriteField</c> methods. Neither ends the record. Register it with
/// <see cref="CsvRecordWriter.Register{T}(Func{string, CsvRecordWriter{T}})"/>.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the records.</typeparam>
public abstract class CsvRecordWriter<T>
{
	/// <summary>Creates a record writer whose columns have their own names.</summary>
	protected CsvRecordWriter()
		: this("")
	{
	}

	/// <summary>Creates a record writer that puts <paramref name="columnPrefix"/> in front of the names of its columns.</summary>
	/// <param name="columnPrefix">
	/// The text in front of each column name, such as <c>"Ship "</c>, for a record written as a member of another record
	/// (<see cref="CsvColumnAttribute.Prefix"/>); <c>""</c> for none.
	/// </param>
	/// <exception cref="ArgumentNullException"><paramref name="columnPrefix"/> is <see langword="null"/>.</exception>
	protected CsvRecordWriter(string columnPrefix)
	{
		ArgumentNullException.ThrowIfNull(columnPrefix);
		ColumnPrefix = columnPrefix;
	}

	/// <summary>The text that <see cref="WriteColumnName"/> puts in front of each column name; <c>""</c> for none.</summary>
	protected string ColumnPrefix { get; }

	/// <summary>The number of fields that <see cref="Write"/> and <see cref="WriteHeader"/> write.</summary>
	public abstract int FieldCount { get; }

	/// <summary>Writes the names of the columns as fields of the current record, without ending it.</summary>
	/// <param name="writer">The writer.</param>
	public abstract void WriteHeader(CsvWriter writer);

	/// <summary>Writes the values of a record as fields of the current record, without ending it.</summary>
	/// <param name="writer">The writer.</param>
	/// <param name="record">The record; not <see langword="null"/>.</param>
	public abstract void Write(CsvWriter writer, T record);

	/// <summary>
	/// Writes <see cref="FieldCount"/> empty fields, for a nested record that is <see langword="null"/>, without ending the
	/// record.
	/// </summary>
	/// <param name="writer">The writer.</param>
	/// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
	public virtual void WriteEmpty(CsvWriter writer)
	{
		ArgumentNullException.ThrowIfNull(writer);
		for (var i = 0; i < FieldCount; i++)
		{
			writer.WriteField(ReadOnlySpan<char>.Empty);
		}
	}

	/// <summary>Writes <see cref="ColumnPrefix"/> followed by <paramref name="name"/> as a field: a column name of the header.</summary>
	/// <param name="writer">The writer.</param>
	/// <param name="name">The name of the column.</param>
	/// <exception cref="ArgumentNullException"><paramref name="writer"/> or <paramref name="name"/> is <see langword="null"/>.</exception>
	protected void WriteColumnName(CsvWriter writer, string name)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(name);
		writer.WriteField(ColumnPrefix.Length == 0 ? name : ColumnPrefix + name);
	}

	/// <summary>
	/// Creates the record writer of a member whose type is itself a record type, which writes its columns in the same
	/// records, with <see cref="ColumnPrefix"/> and then <paramref name="prefix"/> in front of their names.
	/// </summary>
	/// <typeparam name="TNested">The type of the member.</typeparam>
	/// <param name="prefix">The text in front of the nested record's column names (<see cref="CsvColumnAttribute.Prefix"/>); <c>""</c> for none.</param>
	/// <returns>The record writer of <typeparamref name="TNested"/>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException"><typeparamref name="TNested"/> has no record writer, or its records cannot be written.</exception>
	protected CsvRecordWriter<TNested> NestedWriter<TNested>(string prefix)
	{
		ArgumentNullException.ThrowIfNull(prefix);

		return CsvRecordWriter.Create<TNested>(ColumnPrefix + prefix);
	}
}

/// <summary>Finds the generated <see cref="CsvRecordWriter{T}"/> of a <see cref="CsvRecordAttribute"/> type.</summary>
public static class CsvRecordWriter
{
	/// <summary>
	/// Registers the record writer factory of <typeparamref name="T"/>, such as a hand-written
	/// <see cref="CsvRecordWriter{T}"/>. Code generated for <see cref="CsvRecordAttribute"/> types calls this when their
	/// assembly is loaded. A later registration for the same type replaces an earlier one.
	/// </summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="factory">
	/// Creates a record writer that puts the given text in front of its column names (<see cref="CsvRecordWriter{T}.ColumnPrefix"/>).
	/// </param>
	/// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
	public static void Register<T>(Func<string, CsvRecordWriter<T>> factory)
	{
		ArgumentNullException.ThrowIfNull(factory);
		Factory<T>.Current = factory;
	}

	/// <summary>Creates the record writer of <typeparamref name="T"/>.</summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <returns>A record writer, which can be used with any <see cref="CsvWriter"/>.</returns>
	/// <exception cref="InvalidOperationException">
	/// <typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type and has no registered record writer, or
	/// its records cannot be written (the message says why).
	/// </exception>
	public static CsvRecordWriter<T> Create<T>() => Create<T>("");

	/// <summary>Creates the record writer of <typeparamref name="T"/> that puts <paramref name="columnPrefix"/> in front of its column names.</summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="columnPrefix">The text in front of each column name, such as <c>"Ship "</c>; <c>""</c> for none.</param>
	/// <returns>A record writer, which can be used with any <see cref="CsvWriter"/>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="columnPrefix"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// <typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type and has no registered record writer, or
	/// its records cannot be written (the message says why).
	/// </exception>
	public static CsvRecordWriter<T> Create<T>(string columnPrefix)
	{
		ArgumentNullException.ThrowIfNull(columnPrefix);
		var factory = Factory<T>.Current ?? Load<T>();

		return factory(columnPrefix);
	}

	/// <summary>
	/// Whether <typeparamref name="T"/> has a record writer, generated or registered, so that <see cref="Create{T}()"/>
	/// does not throw for want of one. (A generated writer may still throw when the type's records cannot be written,
	/// with the reason.)
	/// </summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <returns><see langword="true"/> when a factory is registered for the type.</returns>
	public static bool IsRegistered<T>()
	{
		if (Factory<T>.Current is not null)
		{
			return true;
		}

		RuntimeHelpers.RunModuleConstructor(typeof(T).Module.ModuleHandle);

		return Factory<T>.Current is not null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Func<string, CsvRecordWriter<T>> Load<T>()
	{
		// As for mappers: the generated registration runs once code of the type's assembly runs.
		RuntimeHelpers.RunModuleConstructor(typeof(T).Module.ModuleHandle);

		return Factory<T>.Current ?? throw new InvalidOperationException(
			$"{typeof(T)} has no CSV record writer. Mark it [CsvRecord] and partial, and reference the CsvHelper source generator, " +
			"or register one with CsvRecordWriter.Register.");
	}

	/// <summary>Holds the registered factory of <typeparamref name="T"/>: one static field per type, without a dictionary lookup.</summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	private static class Factory<T>
	{
		/// <summary>The factory registered last, or <see langword="null"/> before the first.</summary>
		public static Func<string, CsvRecordWriter<T>>? Current;
	}
}
