using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CsvHelper.Linq;

/// <summary>Chooses how the records of a type are written: by CsvHelper itself, or by a <see cref="ReflectionRecordWriter{T}"/>.</summary>
internal static class RecordWriters
{
	// What ReflectionRecordWriter reads of a type: its public properties, fields and constructors.
	internal const DynamicallyAccessedMemberTypes Members =
		DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicConstructors;

	/// <summary>
	/// The record writer for records of <typeparamref name="T"/> that CsvHelper does not write itself, or
	/// <see langword="null"/> when it does: a <see cref="CsvRecordAttribute"/> type (or one with a registered writer), whose
	/// generated writer says why when its records cannot be written, or a dynamic record or dictionary.
	/// </summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	/// <returns>The record writer, or <see langword="null"/>.</returns>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has no members to write.</exception>
	public static CsvRecordWriter<T>? For<[DynamicallyAccessedMembers(Members)] T>() =>
		CsvRecordWriter.IsRegistered<T>() || IsDynamic(typeof(T)) ? null : Cache<T>.Writer ??= new ReflectionRecordWriter<T>("");

	/// <summary>Whether CsvHelper writes records of a type as dynamic records: <c>object</c>, <see cref="CsvDynamicRecord"/>, and dictionaries keyed by column name.</summary>
	private static bool IsDynamic(Type type) =>
		type == typeof(object) || type == typeof(CsvDynamicRecord) ||
		(!type.IsValueType && (typeof(IDictionary<string, object?>).IsAssignableFrom(type) || typeof(IReadOnlyDictionary<string, string?>).IsAssignableFrom(type) ||
							   typeof(IDictionary<string, string?>).IsAssignableFrom(type)));

	/// <summary>Holds the record writer of a type, which keeps no state of its own.</summary>
	/// <typeparam name="T">The type of the records.</typeparam>
	private static class Cache<T>
	{
		/// <summary>The writer, made on first use.</summary>
		public static CsvRecordWriter<T>? Writer;
	}
}

/// <summary>
/// Writes records of a type that has no generated record writer, such as an anonymous type, a tuple, or an entity of an
/// ORM, from its public members, found with reflection: the members that a public constructor sets first, in its order
/// (as anonymous types and positional records declare them), then the others, base types first, in the order they are
/// declared. Only members of simple types are written (numbers, strings, dates, Booleans, enums, GUIDs, and other
/// formattable values), so references to other objects, such as an entity's navigation properties, are left out.
/// <see cref="CsvIgnoreAttribute"/> leaves a member out, and the name of a <see cref="CsvColumnAttribute"/> names its column.
/// </summary>
/// <typeparam name="T">The type of the records.</typeparam>
internal sealed class ReflectionRecordWriter<[DynamicallyAccessedMembers(RecordWriters.Members)] T> : CsvRecordWriter<T>
{
	private readonly Column[] columns;
	// Writes the fields of a record with one compiled call, where code can be compiled; else the members are read with reflection.
	private readonly Action<CsvWriter, T>? write;

	/// <summary>Creates the writer.</summary>
	/// <param name="columnPrefix">The text in front of each column name; <c>""</c> for none.</param>
	/// <exception cref="InvalidOperationException"><typeparamref name="T"/> has no members to write.</exception>
	public ReflectionRecordWriter(string columnPrefix)
		: base(columnPrefix)
	{
		columns = FindColumns();
		if (columns.Length == 0)
		{
			throw new InvalidOperationException(
				$"{typeof(T)} has no public properties or fields of simple types to write. Mark it [CsvRecord] and partial, or select the values to write.");
		}

		if (DynamicCodeCompiled)
		{
			write = Compile(columns);
		}
	}

	/// <summary>Whether compiled code runs as compiled code, not interpreted (it is interpreted with native AOT).</summary>
#if NET
	private static bool DynamicCodeCompiled => RuntimeFeature.IsDynamicCodeCompiled;
#else
	private static bool DynamicCodeCompiled => true;
#endif

	/// <summary>The number of columns.</summary>
	public override int FieldCount => columns.Length;

	/// <summary>Writes the names of the columns.</summary>
	/// <param name="writer">The writer.</param>
	public override void WriteHeader(CsvWriter writer)
	{
		foreach (var column in columns)
		{
			WriteColumnName(writer, column.Name);
		}
	}

	/// <summary>Writes the values of a record's members, as <see cref="CsvWriter.WriteField(object)"/> writes them.</summary>
	/// <param name="writer">The writer.</param>
	/// <param name="record">The record.</param>
	public override void Write(CsvWriter writer, T record)
	{
		if (write is not null)
		{
			write(writer, record);

			return;
		}

		// A value type is boxed once for all of its members.
		object boxed = record!;
		foreach (var column in columns)
		{
			writer.WriteField(column.Read(boxed));
		}
	}

	private static Column[] FindColumns()
	{
		var type = typeof(T);
		var members = new List<MemberInfo>();
		members.AddRange(Ordered(type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0)));
		members.AddRange(Ordered(type.GetFields(BindingFlags.Public | BindingFlags.Instance)));
		members.RemoveAll(m => m.IsDefined(typeof(CsvIgnoreAttribute), true) || !IsSimple(TypeOf(m)));

		// The members that the widest public constructor sets come first, in its order, as anonymous types and positional
		// records declare them.
		var constructor = type.GetConstructors()
			.Where(c => c.GetParameters().Length > 0 && !(c.GetParameters().Length == 1 && c.GetParameters()[0].ParameterType == type))
			.OrderByDescending(c => c.GetParameters().Length)
			.FirstOrDefault(c => c.GetParameters().All(p => members.Any(m => string.Equals(m.Name, p.Name, StringComparison.OrdinalIgnoreCase))));
		if (constructor is not null)
		{
			var first = constructor.GetParameters().Select(p => members.First(m => string.Equals(m.Name, p.Name, StringComparison.OrdinalIgnoreCase))).ToList();
			members = first.Concat(members.Except(first)).ToList();
		}

		return members.Select(m => new Column(NameOf(m), m)).ToArray();
	}

	/// <summary>Compiles the writing of a record's members: <c>writer.WriteField((object)record.Member)</c> for each column.</summary>
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Called only when dynamic code is compiled.")]
	private static Action<CsvWriter, T> Compile(Column[] columns)
	{
		var writer = Expression.Parameter(typeof(CsvWriter), "writer");
		var record = Expression.Parameter(typeof(T), "record");
		var writeField = typeof(CsvWriter).GetMethod(nameof(CsvWriter.WriteField), [typeof(object)])!;
		var body = Expression.Block(columns.Select(c =>
			Expression.Call(writer, writeField, Expression.Convert(Expression.MakeMemberAccess(record, c.Member), typeof(object)))));

		return Expression.Lambda<Action<CsvWriter, T>>(body, writer, record).Compile();
	}

	/// <summary>Members in the order they are declared, those of base types first.</summary>
	private static IEnumerable<TMember> Ordered<TMember>(IEnumerable<TMember> members) where TMember : MemberInfo =>
		members.OrderBy(m => Depth(m.DeclaringType)).ThenBy(m => m.MetadataToken);

	private static int Depth(Type? type)
	{
		var depth = 0;
		for (var t = type?.BaseType; t is not null; t = t.BaseType)
		{
			depth++;
		}

		return depth;
	}

	private static Type TypeOf(MemberInfo member) => member is PropertyInfo property ? property.PropertyType : ((FieldInfo)member).FieldType;

	private static string NameOf(MemberInfo member) => member.GetCustomAttribute<CsvColumnAttribute>(true)?.Name ?? member.Name;

	/// <summary>Whether values of a type are written as one field: numbers, strings, dates, Booleans, enums, GUIDs and other formattable values.</summary>
	private static bool IsSimple(Type type)
	{
		type = Nullable.GetUnderlyingType(type) ?? type;

		return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(Uri) || typeof(IFormattable).IsAssignableFrom(type);
	}

	/// <summary>A column: its name, and the member it reads.</summary>
	private sealed class Column
	{
		/// <summary>Creates the column.</summary>
		/// <param name="name">The name of the column.</param>
		/// <param name="member">The property or field it reads.</param>
		public Column(string name, MemberInfo member)
		{
			Name = name;
			Member = member;
		}

		/// <summary>The name of the column.</summary>
		public string Name { get; }

		/// <summary>The property or field it reads.</summary>
		public MemberInfo Member { get; }

		/// <summary>Reads the member of a record.</summary>
		/// <param name="record">The record, boxed.</param>
		/// <returns>The value of the member.</returns>
		[UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "The members come from T, which is annotated to keep them.")]
		public object? Read(object record) => Member is PropertyInfo property ? property.GetValue(record) : ((FieldInfo)Member).GetValue(record);
	}
}
