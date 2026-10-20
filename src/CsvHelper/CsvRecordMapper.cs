using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace CsvHelper;

/// <summary>
/// Reads records of type <typeparamref name="T"/> from the columns of one reader's header. The CsvHelper source
/// generator writes one for each <see cref="CsvRecordAttribute"/> type; <see cref="CsvRecordMapper.Create{T}(CsvReader)"/>
/// creates it.
/// </summary>
/// <remarks>
/// <para>
/// A mapper resolves its columns when it is created and does not change afterwards, so it can read records of
/// any reader with the same columns, on any thread (the workers of <see cref="CsvReader.ParallelEnumerate{T}(int)"/>
/// share one).
/// </para>
/// <para>
/// To write one by hand, find the columns in the constructor with <see cref="Column(CsvReader, string, int, bool, string)"/>,
/// call <see cref="ThrowIfColumnsMissing"/>, and read the fields with the reader's typed getters in <see cref="Read(CsvReader)"/>.
/// Register it with <see cref="CsvRecordMapper.Register{T}(Func{CsvReader, CsvRecordMapper{T}})"/>, or put a
/// <see cref="CsvConverterAttribute"/> naming it on <typeparamref name="T"/>.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the records.</typeparam>
public abstract class CsvRecordMapper<T>
{
	// Required columns that the header lacks, collected while the constructor resolves its columns.
	private List<string>? missing;

	/// <summary>Creates a mapper whose columns are looked up by their own names.</summary>
	protected CsvRecordMapper()
		: this("")
	{
	}

	/// <summary>Creates a mapper that puts <paramref name="columnPrefix"/> in front of the names it looks up.</summary>
	/// <param name="columnPrefix">
	/// The text in front of each header name, such as <c>"Ship "</c>, for a record read as a member of another record
	/// (<see cref="CsvColumnAttribute.Prefix"/>); <c>""</c> for none.
	/// </param>
	/// <exception cref="ArgumentNullException"><paramref name="columnPrefix"/> is <see langword="null"/>.</exception>
	protected CsvRecordMapper(string columnPrefix)
	{
		ArgumentNullException.ThrowIfNull(columnPrefix);
		ColumnPrefix = columnPrefix;
	}

	/// <summary>The text that <see cref="Column(CsvReader, string, int, bool, string)"/> puts in front of each header name; <c>""</c> for none.</summary>
	protected string ColumnPrefix { get; }

	/// <summary>Reads the current record of <paramref name="reader"/>.</summary>
	/// <param name="reader">
	/// A reader positioned on a record, whose columns are those of the reader the mapper was created for.
	/// </param>
	/// <returns>A new <typeparamref name="T"/> with the values of the record.</returns>
	/// <exception cref="FormatException">A field cannot be parsed as the type of its member.</exception>
	/// <exception cref="OverflowException">A numeric field is outside the range of the type of its member.</exception>
	public abstract T Read(CsvReader reader);

	/// <summary>
	/// Reads the current record of <paramref name="reader"/> into <paramref name="record"/>, an existing instance, for
	/// <see cref="CsvReader.EnumerateRecords{T}(T)"/>. Generated mappers implement it for classes whose members can all
	/// be set after construction; this implementation throws.
	/// </summary>
	/// <param name="reader">
	/// A reader positioned on a record, whose columns are those of the reader the mapper was created for.
	/// </param>
	/// <param name="record">The instance whose members are set.</param>
	/// <exception cref="NotSupportedException">The mapper cannot refill an instance.</exception>
	/// <exception cref="FormatException">A field cannot be parsed as the type of its member.</exception>
	/// <exception cref="OverflowException">A numeric field is outside the range of the type of its member.</exception>
	public virtual void Read(CsvReader reader, T record) =>
		throw new NotSupportedException(
			$"{typeof(T)} cannot be read into an existing instance: its members are set by its constructor or by init accessors, " +
			"or its mapper is hand-written and does not override Read(CsvReader, T).");

	/// <summary>
	/// After <see cref="Read(CsvReader)"/> threw for the current record, finds the member whose field made it throw, by
	/// reading the members again one at a time. The record methods of <see cref="CsvReader"/> call it to say where a record
	/// failed (<see cref="CsvFormatException.Member"/>), so reading costs nothing more until a record fails. Generated
	/// mappers implement it; this implementation finds nothing.
	/// </summary>
	/// <param name="reader">The reader, still positioned on the record that failed.</param>
	/// <param name="fieldIndex">
	/// The 0-based index of the field of the member, or -1 when it is not known or no member failed.
	/// </param>
	/// <returns>
	/// The member, as error messages name it (<c>Order.Price</c>), or <see langword="null"/> when every member reads on
	/// its own, such as when the constructor or a setter threw.
	/// </returns>
	public virtual string? FindFailingMember(CsvReader reader, out int fieldIndex)
	{
		fieldIndex = -1;

		return null;
	}

	/// <summary>
	/// Finds the column of a member: <paramref name="index"/> when it is not negative, else the header column
	/// called <see cref="ColumnPrefix"/> followed by <paramref name="name"/>. Missing required columns are reported
	/// together by <see cref="ThrowIfColumnsMissing"/>.
	/// </summary>
	/// <param name="reader">The reader whose header is searched.</param>
	/// <param name="name">The header name of the column, matched as by <see cref="CsvReader.TryGetFieldIndex(string, out int)"/>.</param>
	/// <param name="index">The 0-based index of the column, or -1 to find it by <paramref name="name"/>.</param>
	/// <param name="optional">Whether the column may be missing.</param>
	/// <param name="member">The name of the member or parameter, for the error message.</param>
	/// <returns>The 0-based index of the column, or -1 when the header has no such column.</returns>
	protected int Column(CsvReader reader, string name, int index, bool optional, string member)
	{
		if (index >= 0) return index;
		var prefixed = Prefixed(name);
		if (reader.TryGetFieldIndex(prefixed, out var found)) return found;
		if (!optional) (missing ??= []).Add(reader.Options.HasHeader ? $"{member} ('{prefixed}')" : member);

		return -1;
	}

	/// <summary>
	/// Finds the column of a member that can have several names: <paramref name="index"/> when it is not negative,
	/// else the header column called <see cref="ColumnPrefix"/> followed by the first of <paramref name="names"/> that
	/// the header has. Missing required columns are reported together by <see cref="ThrowIfColumnsMissing"/>.
	/// </summary>
	/// <param name="reader">The reader whose header is searched.</param>
	/// <param name="names">The header names of the column, tried in order, each matched as by <see cref="CsvReader.TryGetFieldIndex(string, out int)"/>.</param>
	/// <param name="index">The 0-based index of the column, or -1 to find it by <paramref name="names"/>.</param>
	/// <param name="optional">Whether the column may be missing.</param>
	/// <param name="member">The name of the member or parameter, for the error message.</param>
	/// <returns>The 0-based index of the column, or -1 when the header has none of the names.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="names"/> is <see langword="null"/>.</exception>
	protected int Column(CsvReader reader, string[] names, int index, bool optional, string member)
	{
		ArgumentNullException.ThrowIfNull(names);
		if (index >= 0) return index;
		foreach (var name in names)
		{
			if (reader.TryGetFieldIndex(Prefixed(name), out var found)) return found;
		}

		if (!optional)
		{
			if (!reader.Options.HasHeader)
			{
				(missing ??= []).Add(member);

				return -1;
			}

			var text = new StringBuilder(member).Append(" (");
			for (var i = 0; i < names.Length; i++) text.Append(i == 0 ? "'" : " or '").Append(Prefixed(names[i])).Append('\'');
			(missing ??= []).Add(text.Append(')').ToString());
		}

		return -1;
	}

	/// <summary>
	/// Finds the column of a member among several columns with the same name: <paramref name="index"/> when it is not
	/// negative, else the one at <paramref name="nameIndex"/> among the header columns called <see cref="ColumnPrefix"/>
	/// followed by one of <paramref name="names"/> (the first name that the header has that many times). Missing required
	/// columns are reported together by <see cref="ThrowIfColumnsMissing"/>.
	/// </summary>
	/// <param name="reader">The reader whose header is searched.</param>
	/// <param name="names">The header names of the column, tried in order, each matched as by <see cref="CsvReader.TryGetFieldIndex(string, int, out int)"/>.</param>
	/// <param name="nameIndex">0 for the first column with the name, 1 for the second, and so on.</param>
	/// <param name="index">The 0-based index of the column, or -1 to find it by <paramref name="names"/>.</param>
	/// <param name="optional">Whether the column may be missing.</param>
	/// <param name="member">The name of the member or parameter, for the error message.</param>
	/// <returns>The 0-based index of the column, or -1 when the header has none of the names that many times.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="names"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="nameIndex"/> is negative.</exception>
	protected int Column(CsvReader reader, string[] names, int nameIndex, int index, bool optional, string member)
	{
		ArgumentNullException.ThrowIfNull(names);
		if (nameIndex < 0) throw new ArgumentOutOfRangeException(nameof(nameIndex), nameIndex, "The name index must not be negative.");
		if (index >= 0) return index;
		foreach (var name in names)
		{
			if (reader.TryGetFieldIndex(Prefixed(name), nameIndex, out var found)) return found;
		}

		if (!optional)
		{
			if (!reader.Options.HasHeader)
			{
				(missing ??= []).Add(member);

				return -1;
			}

			var text = new StringBuilder(member).Append(" (");
			for (var i = 0; i < names.Length; i++) text.Append(i == 0 ? "'" : " or '").Append(Prefixed(names[i])).Append('\'');
			if (nameIndex > 0) text.Append(" number ").Append(nameIndex + 1);
			(missing ??= []).Add(text.Append(')').ToString());
		}

		return -1;
	}

	/// <summary>
	/// Finds the columns of a collection member: every header column called <see cref="ColumnPrefix"/> followed by the
	/// first of <paramref name="names"/> that the header has, in order, for files that repeat a column name. Without any,
	/// a required member is reported by <see cref="ThrowIfColumnsMissing"/>.
	/// </summary>
	/// <param name="reader">The reader whose header is searched.</param>
	/// <param name="names">The header names of the columns, tried in order, each matched as by <see cref="CsvReader.TryGetFieldIndex(string, int, out int)"/>.</param>
	/// <param name="optional">Whether the columns may be missing, which reads an empty collection.</param>
	/// <param name="member">The name of the member, for the error message.</param>
	/// <returns>The 0-based indexes of the columns, in header order; empty when the header has none of the names.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="names"/> is <see langword="null"/>.</exception>
	protected int[] Columns(CsvReader reader, string[] names, bool optional, string member)
	{
		ArgumentNullException.ThrowIfNull(names);
		foreach (var name in names)
		{
			var prefixed = Prefixed(name);
			var found = new List<int>();
			while (reader.TryGetFieldIndex(prefixed, found.Count, out var index))
			{
				found.Add(index);
			}

			if (found.Count > 0)
			{
				return found.ToArray();
			}
		}

		if (!optional)
		{
			Column(reader, names, -1, false, member);
		}

		return [];
	}

	/// <summary>
	/// The header names of the columns from <paramref name="start"/> to <paramref name="end"/>, the keys of a dictionary
	/// member that reads them.
	/// </summary>
	/// <param name="reader">The reader whose header names the columns.</param>
	/// <param name="start">The 0-based index of the first column.</param>
	/// <param name="end">The 0-based index of the last column, or -1 for the last column of the header.</param>
	/// <param name="member">The name of the member, for the error message.</param>
	/// <returns>The names of the columns that the header has, in order.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> is negative.</exception>
	/// <exception cref="InvalidOperationException">The input has no header.</exception>
	protected string[] ColumnNames(CsvReader reader, int start, int end, string member)
	{
		ArgumentNullException.ThrowIfNull(reader);
		if (start < 0) throw new ArgumentOutOfRangeException(nameof(start), start, "The first column must not be negative.");
		var headers = reader.Headers;
		if (!reader.Options.HasHeader)
		{
			throw new InvalidOperationException($"{member} is a dictionary whose keys are header names, but the CSV has no header.");
		}

		var last = end < 0 ? headers.Count - 1 : Math.Min(end, headers.Count - 1);
		var names = new string[Math.Max(last - start + 1, 0)];
		for (var i = 0; i < names.Length; i++)
		{
			names[i] = headers[start + i];
		}

		return names;
	}

	/// <summary>
	/// Creates the mapper of a member whose type is itself a record type, which reads its columns from the same
	/// records, with <see cref="ColumnPrefix"/> and then <paramref name="prefix"/> in front of their names. Its missing
	/// required columns are reported with this mapper's by <see cref="ThrowIfColumnsMissing"/>.
	/// </summary>
	/// <typeparam name="TNested">The type of the member.</typeparam>
	/// <param name="reader">The reader whose header is searched.</param>
	/// <param name="prefix">The text in front of the nested record's header names (<see cref="CsvColumnAttribute.Prefix"/>); <c>""</c> for none.</param>
	/// <returns>
	/// The mapper of <typeparamref name="TNested"/>, or <see langword="null"/> when it lacks required columns (then
	/// <see cref="ThrowIfColumnsMissing"/> throws).
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// <typeparamref name="TNested"/> has no mapper, or its mapper does not support column prefixes and the prefix is
	/// not empty.
	/// </exception>
	protected CsvRecordMapper<TNested> NestedMapper<TNested>(CsvReader reader, string prefix)
	{
		ArgumentNullException.ThrowIfNull(prefix);
		try
		{
			return CsvRecordMapper.Create<TNested>(reader, ColumnPrefix + prefix);
		}
		catch (KeyNotFoundException e) when (e.Data[CsvRecordMapper.MissingColumnsKey] is string[] columns)
		{
			(missing ??= []).AddRange(columns);

			return null!;
		}
	}

	/// <summary>
	/// Throws a <see cref="KeyNotFoundException"/> naming every required column that <see cref="Column(CsvReader, string, int, bool, string)"/>
	/// and <see cref="NestedMapper{TNested}"/> did not find, if there are any.
	/// </summary>
	/// <param name="reader">The reader whose header was searched.</param>
	/// <exception cref="KeyNotFoundException">
	/// The header lacks a required column, or the input has no header and a required member has no column index.
	/// </exception>
	protected void ThrowIfColumnsMissing(CsvReader reader)
	{
		if (missing is null) return;
		var columns = missing.ToArray();
		missing = null;
		var members = string.Join(", ", columns);
		var exception = new KeyNotFoundException(reader.Options.HasHeader
			? $"The CSV header has no column for {members}."
			: $"The CSV has no header, so these members need a column index ([CsvColumn(Index = n)]): {members}.");
		// The mapper of a record with nested records reports their missing columns with its own (NestedMapper).
		exception.Data[CsvRecordMapper.MissingColumnsKey] = columns;
		throw exception;
	}

	private string Prefixed(string name) => ColumnPrefix.Length == 0 ? name : ColumnPrefix + name;
}

/// <summary>Finds the generated <see cref="CsvRecordMapper{T}"/> of a <see cref="CsvRecordAttribute"/> type.</summary>
public static class CsvRecordMapper
{
	// The key of the missing columns in the Data of the KeyNotFoundException that ThrowIfColumnsMissing throws.
	internal const string MissingColumnsKey = "CsvHelper.MissingColumns";

	/// <summary>
	/// Registers the mapper factory of <typeparamref name="T"/>, such as a hand-written <see cref="CsvRecordMapper{T}"/>.
	/// A later registration for the same type replaces an earlier one.
	/// </summary>
	/// <remarks>
	/// The mapper cannot be used as a nested record with a <see cref="CsvColumnAttribute.Prefix"/>; register a factory
	/// that takes the prefix (<see cref="Register{T}(Func{CsvReader, string, CsvRecordMapper{T}}, IReadOnlyList{int})"/>)
	/// for that.
	/// </remarks>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="factory">Creates a mapper for the header of the given reader.</param>
	/// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
	public static void Register<T>(Func<CsvReader, CsvRecordMapper<T>> factory)
	{
		ArgumentNullException.ThrowIfNull(factory);
		Factory<T>.Current = new Registration<T>((reader, prefix) => prefix.Length == 0
			? factory(reader)
			: throw new InvalidOperationException(
				$"The mapper of {typeof(T)} does not support column prefixes, so it cannot read a nested record with the prefix '{prefix}'."), null);
	}

	/// <summary>
	/// Registers the mapper factory of <typeparamref name="T"/>, and its fixed-width layout. Code generated for
	/// <see cref="CsvRecordAttribute"/> types calls this when their assembly is loaded. A later registration for the
	/// same type replaces an earlier one.
	/// </summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="factory">
	/// Creates a mapper for the header of the given reader, which puts the given text in front of the header names
	/// it looks up (<see cref="CsvRecordMapper{T}.ColumnPrefix"/>).
	/// </param>
	/// <param name="fieldWidths">
	/// The widths of the columns, which <see cref="GetFieldWidths{T}"/> returns, or <see langword="null"/> when the type
	/// has no fixed-width layout.
	/// </param>
	/// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
	public static void Register<T>(Func<CsvReader, string, CsvRecordMapper<T>> factory, IReadOnlyList<int>? fieldWidths = null)
	{
		ArgumentNullException.ThrowIfNull(factory);
		IReadOnlyList<int>? widths = null;
		if (fieldWidths is not null)
		{
			var copy = new int[fieldWidths.Count];
			for (var i = 0; i < copy.Length; i++) copy[i] = fieldWidths[i];
			widths = new ReadOnlyCollection<int>(copy);
		}

		Factory<T>.Current = new Registration<T>(factory, widths);
	}

	/// <summary>
	/// Creates a mapper of <typeparamref name="T"/> for the header of <paramref name="reader"/>, which is read
	/// now if it has not been yet.
	/// </summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="reader">The reader whose header names the columns.</param>
	/// <returns>A mapper that reads records of <paramref name="reader"/>, and of any reader with the same columns.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> is not a <see cref="CsvRecordAttribute"/> type.</exception>
	/// <exception cref="KeyNotFoundException">
	/// The header lacks a column that <typeparamref name="T"/> requires, or the input has no header and a required
	/// member has no column index.
	/// </exception>
	public static CsvRecordMapper<T> Create<T>(CsvReader reader) => Create<T>(reader, "");

	/// <summary>
	/// Creates a mapper of <typeparamref name="T"/> for the header of <paramref name="reader"/> that puts
	/// <paramref name="columnPrefix"/> in front of the header names it looks up. The header is read now if it has not
	/// been yet.
	/// </summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="reader">The reader whose header names the columns.</param>
	/// <param name="columnPrefix">The text in front of each header name, such as <c>"Ship "</c>; <c>""</c> for none.</param>
	/// <returns>A mapper that reads records of <paramref name="reader"/>, and of any reader with the same columns.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="reader"/> or <paramref name="columnPrefix"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// <typeparamref name="T"/> has no mapper, or its mapper does not support column prefixes and
	/// <paramref name="columnPrefix"/> is not empty.
	/// </exception>
	/// <exception cref="KeyNotFoundException">
	/// The header lacks a column that <typeparamref name="T"/> requires, or the input has no header and a required
	/// member has no column index.
	/// </exception>
	public static CsvRecordMapper<T> Create<T>(CsvReader reader, string columnPrefix)
	{
		ArgumentNullException.ThrowIfNull(reader);
		ArgumentNullException.ThrowIfNull(columnPrefix);
		var registration = Factory<T>.Current ?? Load<T>();

		return registration.Create(reader, columnPrefix);
	}

	/// <summary>
	/// Gets the fixed-width layout of <typeparamref name="T"/>: the <see cref="CsvColumnAttribute.Width"/> of each
	/// column, ordered by <see cref="CsvColumnAttribute.Index"/>, for <see cref="CsvReaderOptions.FieldWidths"/> and
	/// <see cref="CsvWriterOptions.FieldWidths"/>.
	/// </summary>
	/// <typeparam name="T">A <see cref="CsvRecordAttribute"/> type whose members have widths.</typeparam>
	/// <returns>The widths of the columns, in characters.</returns>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has no mapper, or its members have no widths.</exception>
	public static IReadOnlyList<int> GetFieldWidths<T>()
	{
		var registration = Factory<T>.Current ?? Load<T>();

		return registration.FieldWidths ?? throw new InvalidOperationException(
			$"{typeof(T)} has no fixed-width layout. Give its members a width and an index: [CsvColumn(Index = n, Width = w)].");
	}

	/// <summary>
	/// Registers the options of the <see cref="CsvOptionsAttribute"/> of <typeparamref name="T"/>, which
	/// <see cref="CsvReaderOptions.For{T}"/> and <see cref="CsvWriterOptions.For{T}"/> return. Code generated for the
	/// attribute calls this when the type's assembly is loaded. A later registration for the same type replaces an earlier
	/// one.
	/// </summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <param name="readerOptions">Creates the reader options, on the first call of <see cref="CsvReaderOptions.For{T}"/>.</param>
	/// <param name="writerOptions">Creates the writer options, on the first call of <see cref="CsvWriterOptions.For{T}"/>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="readerOptions"/> or <paramref name="writerOptions"/> is <see langword="null"/>.</exception>
	public static void RegisterOptions<T>(Func<CsvReaderOptions> readerOptions, Func<CsvWriterOptions> writerOptions)
	{
		ArgumentNullException.ThrowIfNull(readerOptions);
		ArgumentNullException.ThrowIfNull(writerOptions);
		Options<T>.Current = new OptionsRegistration(readerOptions, writerOptions);
	}

	/// <summary>The reader options registered for <typeparamref name="T"/>, or the defaults.</summary>
	internal static CsvReaderOptions ReaderOptions<T>() => (Options<T>.Current ?? LoadOptions<T>()).Reader;

	/// <summary>The writer options registered for <typeparamref name="T"/>, or the defaults.</summary>
	internal static CsvWriterOptions WriterOptions<T>() => (Options<T>.Current ?? LoadOptions<T>()).Writer;

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static OptionsRegistration LoadOptions<T>()
	{
		// As for mappers: the generated registration runs once code of the type's assembly runs.
		RuntimeHelpers.RunModuleConstructor(typeof(T).Module.ModuleHandle);

		// A type without [CsvOptions] registers nothing: it gets the defaults, without loading again.
		return Options<T>.Current ??= OptionsRegistration.None;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Registration<T> Load<T>()
	{
		// GetRecords<dynamic>() and GetRecords<CsvDynamicRecord>() read CsvDynamicRecords.
		if (typeof(T) == typeof(object) || typeof(T) == typeof(CsvDynamicRecord))
		{
			return Factory<T>.Current = new Registration<T>(DynamicMapper<T>, null);
		}

		// The generated registration is a module initializer, which the runtime runs once code of that assembly
		// runs: not yet when T comes from an assembly that has done nothing else so far.
		RuntimeHelpers.RunModuleConstructor(typeof(T).Module.ModuleHandle);

		return Factory<T>.Current ?? throw new InvalidOperationException(
			$"{typeof(T)} has no CSV mapper. Mark it [CsvRecord] and partial, and reference the CsvHelper source generator.");
	}

	/// <summary>Creates the mapper of dynamic records, for <typeparamref name="T"/> <see cref="object"/> or <see cref="CsvDynamicRecord"/>.</summary>
	private static CsvRecordMapper<T> DynamicMapper<T>(CsvReader reader, string columnPrefix)
	{
		if (columnPrefix.Length > 0)
		{
			throw new InvalidOperationException("Dynamic records cannot be nested records with a column prefix.");
		}

		return typeof(T) == typeof(object)
			? (CsvRecordMapper<T>)(object)new DynamicRecordMapper<object>(reader)
			: (CsvRecordMapper<T>)(object)new DynamicRecordMapper<CsvDynamicRecord>(reader);
	}

	/// <summary>Reads each record as a <see cref="CsvDynamicRecord"/>, with the schema of the reader's header.</summary>
	/// <typeparam name="T"><see cref="object"/> (<c>dynamic</c>) or <see cref="CsvDynamicRecord"/>.</typeparam>
	private sealed class DynamicRecordMapper<T> : CsvRecordMapper<T>
		where T : class
	{
		private readonly CsvDynamicSchema schema;

		/// <summary>Creates the mapper for the header of <paramref name="reader"/>.</summary>
		/// <param name="reader">The reader whose header names the columns.</param>
		public DynamicRecordMapper(CsvReader reader) => schema = reader.DynamicSchema();

		/// <inheritdoc/>
		public override T Read(CsvReader reader) => Unsafe.As<T>(reader.ReadDynamicRecord(schema));
	}

	/// <summary>Holds the registration of <typeparamref name="T"/>: one static field per type, without a dictionary lookup.</summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	private static class Factory<T>
	{
		/// <summary>The registration made last, or <see langword="null"/> before the first.</summary>
		public static Registration<T>? Current;
	}

	/// <summary>Holds the options registered for <typeparamref name="T"/>.</summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	private static class Options<T>
	{
		/// <summary>The registration made last, or <see langword="null"/> before the type's assembly has been loaded.</summary>
		public static OptionsRegistration? Current;
	}

	/// <summary>The factories of a type's options, and the options they created, which are created once.</summary>
	private sealed class OptionsRegistration
	{
		private readonly Func<CsvReaderOptions> createReader;
		private readonly Func<CsvWriterOptions> createWriter;
		private CsvReaderOptions? reader;
		private CsvWriterOptions? writer;

		/// <summary>Creates a registration.</summary>
		/// <param name="createReader">Creates the reader options.</param>
		/// <param name="createWriter">Creates the writer options.</param>
		public OptionsRegistration(Func<CsvReaderOptions> createReader, Func<CsvWriterOptions> createWriter)
		{
			this.createReader = createReader;
			this.createWriter = createWriter;
		}

		/// <summary>The registration of a type without options: the defaults.</summary>
		public static OptionsRegistration None { get; } = new(static () => CsvReaderOptions.Default, static () => CsvWriterOptions.Default);

		/// <summary>The reader options, created on the first use; every thread gets the same instance.</summary>
		public CsvReaderOptions Reader
		{
			get
			{
				if (Volatile.Read(ref reader) is { } existing)
				{
					return existing;
				}

				var created = createReader();

				return Interlocked.CompareExchange(ref reader, created, null) ?? created;
			}
		}

		/// <summary>The writer options, created on the first use; every thread gets the same instance.</summary>
		public CsvWriterOptions Writer
		{
			get
			{
				if (Volatile.Read(ref writer) is { } existing)
				{
					return existing;
				}

				var created = createWriter();

				return Interlocked.CompareExchange(ref writer, created, null) ?? created;
			}
		}
	}

	/// <summary>
	/// A registered mapper factory and fixed-width layout, replaced as a whole so that a reader never sees the factory
	/// of one registration with the layout of another.
	/// </summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	private sealed class Registration<T>
	{
		/// <summary>Creates a registration.</summary>
		/// <param name="create">Creates a mapper for a reader and a column prefix.</param>
		/// <param name="fieldWidths">The read-only fixed-width layout, or <see langword="null"/> for none.</param>
		public Registration(Func<CsvReader, string, CsvRecordMapper<T>> create, IReadOnlyList<int>? fieldWidths)
		{
			Create = create;
			FieldWidths = fieldWidths;
		}

		/// <summary>Creates a mapper for the header of the given reader, with the given column prefix.</summary>
		public Func<CsvReader, string, CsvRecordMapper<T>> Create { get; }

		/// <summary>The widths of the columns that <see cref="GetFieldWidths{T}"/> returns, or <see langword="null"/> for none.</summary>
		public IReadOnlyList<int>? FieldWidths { get; }
	}
}
