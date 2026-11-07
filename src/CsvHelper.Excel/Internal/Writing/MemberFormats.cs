using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace CsvHelper.Excel;

/// <summary>
/// A member's <see cref="CsvFormatAttribute"/> and <see cref="CsvCultureAttribute"/>, and the column they are for.
/// </summary>
/// <param name="Name">The column's header name, after the prefixes of the records it is nested in.</param>
/// <param name="NameIndex">Which of the columns with that name it is (<see cref="CsvColumnAttribute.NameIndex"/>).</param>
/// <param name="Index">The column's index (<see cref="CsvColumnAttribute.Index"/>), or -1 to find it by its name.</param>
/// <param name="Format">The format the record writer writes (the attribute's first), or <see langword="null"/>.</param>
/// <param name="Culture">The culture, or <see langword="null"/> for the writer's.</param>
internal sealed record MemberFormat(string Name, int NameIndex, int Index, string? Format, CultureInfo? Culture);

/// <summary>
/// The formats and cultures of a record type's members, read by reflection once per type, for the sheets that
/// <see cref="ExcelWorkbookWriter.AddSheet{T}(string)"/> adds: a date written with its member's format, or a number with
/// its member's culture, arrives as that text (Phase 0), which the sink reads back with them.
/// </summary>
/// <remarks>
/// <para>
/// The members are found as CsvHelper's source generator finds them, for <see cref="CsvRecordAttribute"/> types only:
/// the parameters of the constructor it uses (with the attributes of the properties they set), then the properties and
/// fields it reads, the base types' included unless <see cref="CsvIgnoreBaseAttribute"/> says otherwise, and the members
/// of nested records, after their prefixes. A member read by a <see cref="CsvConverterAttribute"/> has no format, and
/// neither has any member of a type that a <see cref="CsvRecordMapAttribute"/> or a hand-written mapper maps, whose
/// attributes are elsewhere.
/// </para>
/// <para>
/// A member's column is found by its header name, its <see cref="CsvColumnAttribute"/> name or its own, ordinally, and
/// the <see cref="CsvColumnAttribute.NameIndex"/>th column of that name; or by its <see cref="CsvColumnAttribute.Index"/>,
/// which is where the record writer writes it, and the columns of a collection from its index to its end index. The names
/// are the header record's, or those that the record writer writes in its header (<see cref="HeaderCapture"/>).
/// </para>
/// </remarks>
internal sealed class MemberFormats
{
	/// <summary>The members of a record type that reading its formats needs: its properties, fields and constructors.</summary>
	public const DynamicallyAccessedMemberTypes Members =
		DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties |
		DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields |
		DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors;

	/// <summary>How deep records nest, at most, as the record writers allow.</summary>
	private const int MaxDepth = 64;

	private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

	/// <summary>The formats of each type read so far: none for a type whose members have none.</summary>
	private static readonly ConcurrentDictionary<Type, MemberFormats> Cache = new();

	/// <summary>Creates the formats of a type.</summary>
	/// <param name="members">The members with a format or a culture.</param>
	private MemberFormats(MemberFormat[] members) => Entries = members;

	/// <summary>The members with a format or a culture, in the order they were found.</summary>
	public IReadOnlyList<MemberFormat> Entries { get; }

	/// <summary>The formats of a record type's members.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <returns>The formats, or <see langword="null"/> when no member has one.</returns>
	public static MemberFormats? For<[DynamicallyAccessedMembers(Members)] T>() => For(typeof(T));

	/// <summary>The formats of a record type's members.</summary>
	/// <param name="type">The record type.</param>
	/// <returns>The formats, or <see langword="null"/> when no member has one.</returns>
	public static MemberFormats? For([DynamicallyAccessedMembers(Members)] Type type)
	{
		if (!Cache.TryGetValue(type, out var formats))
		{
			var members = new List<MemberFormat>();
			Add(type, "", members, 0);
			formats = Cache.GetOrAdd(type, new MemberFormats([.. members]));
		}

		return formats.Entries.Count == 0 ? null : formats;
	}

	/// <summary>Each column's format and culture, for the names of a sheet's header.</summary>
	/// <param name="names">The header's names, in column order.</param>
	/// <param name="writerProvider">The writer's format provider, for members without a culture.</param>
	/// <returns>Each column's format, or <see langword="null"/> for a column whose member has none.</returns>
	public ColumnFormat?[] Columns(IReadOnlyList<string> names, IFormatProvider writerProvider)
	{
		var count = names.Count;
		foreach (var member in Entries)
		{
			count = Math.Max(count, member.Index + 1);
		}

		var columns = new ColumnFormat?[count];
		foreach (var member in Entries)
		{
			var at = member.Index >= 0 ? member.Index : Find(names, member.Name, member.NameIndex);
			if (at >= 0)
			{
				columns[at] ??= new ColumnFormat(member.Format, member.Culture, writerProvider);
			}
		}

		return columns;
	}

	/// <summary>The column of the <paramref name="nameIndex"/>th of the names that are <paramref name="name"/>, ordinally.</summary>
	/// <param name="names">The names.</param>
	/// <param name="name">The name.</param>
	/// <param name="nameIndex">Which of them: 0 for the first.</param>
	/// <returns>The column, or -1 when the names have fewer.</returns>
	private static int Find(IReadOnlyList<string> names, string name, int nameIndex)
	{
		for (var i = 0; i < names.Count; i++)
		{
			if (string.Equals(names[i], name, StringComparison.Ordinal) && nameIndex-- == 0)
			{
				return i;
			}
		}

		return -1;
	}

	/// <summary>Adds the formats of a record type's members, and those of the records nested in it.</summary>
	/// <param name="type">The type.</param>
	/// <param name="prefix">The prefix of its columns' names: those of the records it is nested in.</param>
	/// <param name="members">The formats found.</param>
	/// <param name="depth">How deep it is nested.</param>
	[UnconditionalSuppressMessage("Trimming", "IL2075", Justification =
		"The members of a record type's base types that the generated record writer reads are kept by it, and only those are looked at.")]
	private static void Add([DynamicallyAccessedMembers(Members)] Type type, string prefix, List<MemberFormat> members, int depth)
	{
		if (depth > MaxDepth || !type.IsDefined(typeof(CsvRecordAttribute), false))
		{
			return;
		}

		// The constructor's parameters, with the attributes of the properties they set.
		var parameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (Constructor(type) is { } constructor)
		{
			foreach (var parameter in constructor.GetParameters())
			{
				var name = parameter.Name ?? "";
				parameters.Add(name);
				AddMember(parameter.ParameterType, name, parameter, SetBy(type, name), prefix, members, depth);
			}
		}

		// The properties and fields, a derived type's first: a member it hides or overrides comes once.
		var seen = new HashSet<string>(StringComparer.Ordinal);
		var ignoreBase = type.IsDefined(typeof(CsvIgnoreBaseAttribute), false);
		for (var t = type; t is not null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType)
		{
			foreach (var property in t.GetProperties(Declared))
			{
				if (property.GetIndexParameters().Length == 0 && Reads(property, Accessibility(property.GetMethod, property.SetMethod), type, t, seen, parameters) &&
					property.SetMethod is { } setter && IsAccessible(Accessibility(setter, null), type, t) &&
					(IsVisible(Accessibility(setter, null)) || property.IsDefined(typeof(CsvColumnAttribute), false)))
				{
					AddMember(property.PropertyType, property.Name, property, null, prefix, members, depth);
				}
			}

			foreach (var field in t.GetFields(Declared))
			{
				if (Reads(field, Accessibility(field), type, t, seen, parameters) && !field.IsInitOnly && !field.IsLiteral)
				{
					AddMember(field.FieldType, field.Name, field, null, prefix, members, depth);
				}
			}

			if (ignoreBase)
			{
				break;
			}
		}
	}

	/// <summary>
	/// Whether the generator reads a property or field: one it can name, the first of its name, accessible from the record
	/// type, visible or with a <see cref="CsvColumnAttribute"/>, not ignored, and not set by the constructor.
	/// </summary>
	/// <param name="member">The member.</param>
	/// <param name="accessibility">Its accessibility.</param>
	/// <param name="type">The record type.</param>
	/// <param name="declaring">The type that declares the member: the record type or a base type.</param>
	/// <param name="seen">The names of the members found before it.</param>
	/// <param name="parameters">The names of the constructor's parameters.</param>
	/// <returns>Whether it reads it.</returns>
	private static bool Reads(MemberInfo member, Access accessibility, Type type, Type declaring, HashSet<string> seen, HashSet<string> parameters) =>
		// Backing fields (<Name>k__BackingField) and explicit interface implementations can't be named.
		member.Name.IndexOfAny(['<', '.']) < 0 && seen.Add(member.Name) && IsAccessible(accessibility, type, declaring) &&
		(IsVisible(accessibility) || member.IsDefined(typeof(CsvColumnAttribute), false)) &&
		!member.IsDefined(typeof(CsvIgnoreAttribute), false) && !parameters.Contains(member.Name);

	/// <summary>Adds a member's format and culture, or the formats of the record it nests.</summary>
	/// <param name="memberType">The member's type.</param>
	/// <param name="name">Its name: the property's or field's, or the constructor parameter's.</param>
	/// <param name="attributes">Where its attributes are.</param>
	/// <param name="fallback">For a constructor parameter, the property or field it sets, whose attributes it takes too; or <see langword="null"/>.</param>
	/// <param name="prefix">The prefix of the columns' names.</param>
	/// <param name="members">The formats found.</param>
	/// <param name="depth">How deep the member's record is nested.</param>
	[UnconditionalSuppressMessage("Trimming", "IL2067", Justification =
		"A nested record's members are kept by the generated record writer that reads them, which is what the sink reads the attributes of.")]
	[UnconditionalSuppressMessage("Trimming", "IL2072", Justification =
		"A nested record's members are kept by the generated record writer that reads them, which is what the sink reads the attributes of.")]
	private static void AddMember(Type memberType, string name, ICustomAttributeProvider attributes, ICustomAttributeProvider? fallback, string prefix, List<MemberFormat> members, int depth)
	{
		T? Find<T>()
			where T : Attribute =>
			(T?)attributes.GetCustomAttributes(typeof(T), false).FirstOrDefault() ?? (T?)fallback?.GetCustomAttributes(typeof(T), false).FirstOrDefault();

		var column = Find<CsvColumnAttribute>();
		var format = Find<CsvFormatAttribute>();
		var culture = Find<CsvCultureAttribute>();
		// A converter writes the member itself, and a constant isn't a nested record.
		if (Find<CsvConverterAttribute>() is not null)
		{
			return;
		}

		var underlying = Nullable.GetUnderlyingType(memberType) ?? memberType;
		if (Find<CsvConstantAttribute>() is null &&
			(underlying.IsDefined(typeof(CsvRecordAttribute), false) || underlying.IsDefined(typeof(CsvConverterAttribute), false)))
		{
			// A hand-written mapper's record writer is its own; a generated one's members have their own formats.
			Add(underlying, prefix + (column?.Prefix ?? ""), members, depth + 1);

			return;
		}

		if (format is null && culture is null)
		{
			return;
		}

		CultureInfo? info = null;
		if (culture is not null)
		{
			try
			{
				info = CultureInfo.GetCultureInfo(culture.Name);
			}
			catch (CultureNotFoundException)
			{
				// The record writer can't be created either.
				return;
			}
		}

		// A collection is written from its index to its end index, each column with the member's attributes.
		if (memberType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(memberType))
		{
			if (column is { Index: >= 0 } && column.EndIndex >= column.Index)
			{
				for (var i = column.Index; i <= column.EndIndex; i++)
				{
					members.Add(new MemberFormat(prefix + (column.Name ?? name), 0, i, format?.Format, info));
				}
			}

			return;
		}

		members.Add(new MemberFormat(prefix + (column?.Name ?? name), column?.NameIndex ?? 0, column?.Index ?? -1, format?.Format, info));
	}

	/// <summary>
	/// The constructor the generator uses: the one with <see cref="CsvConstructorAttribute"/>, else the public or internal
	/// parameterless one, else the public or internal one with the most parameters, if only one has that many.
	/// </summary>
	/// <param name="type">The record type.</param>
	/// <returns>The constructor, or <see langword="null"/> when there is none (whose record writer the generator doesn't write).</returns>
	private static ConstructorInfo? Constructor([DynamicallyAccessedMembers(Members)] Type type)
	{
		var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		var marked = constructors.Where(c => c.IsDefined(typeof(CsvConstructorAttribute), false)).ToList();
		if (marked.Count > 0)
		{
			return marked.Count == 1 ? marked[0] : null;
		}

		// Not a copy constructor, which takes the type itself.
		var candidates = constructors.Where(c => IsVisible(Accessibility(c)) && !(c.GetParameters() is { Length: 1 } only && only[0].ParameterType == type)).ToList();
		var parameterless = candidates.FirstOrDefault(c => c.GetParameters().Length == 0);
		if (parameterless is not null)
		{
			return parameterless;
		}

		var most = candidates.GroupBy(c => c.GetParameters().Length).OrderByDescending(g => g.Key).FirstOrDefault();

		return most is not null && most.Count() == 1 ? most.First() : null;
	}

	/// <summary>The property or field that a constructor parameter sets: the first with its name, ignoring case, from the type up.</summary>
	/// <param name="type">The record type.</param>
	/// <param name="name">The parameter's name.</param>
	/// <returns>The member, or <see langword="null"/>.</returns>
	[UnconditionalSuppressMessage("Trimming", "IL2075", Justification =
		"The members of a record type's base types that the generated record writer reads are kept by it, and only those are looked at.")]
	private static MemberInfo? SetBy([DynamicallyAccessedMembers(Members)] Type type, string name)
	{
		for (var t = type; t is not null; t = t.BaseType)
		{
			var property = t.GetProperties(Declared).FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
			if (property is not null)
			{
				return property;
			}

			var field = t.GetFields(Declared).FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
			if (field is not null)
			{
				return field;
			}
		}

		return null;
	}

	/// <summary>Whether a member of a type is accessible from the record type: any of its own, and a base type's but private ones (and internal ones of another assembly).</summary>
	/// <param name="accessibility">The member's accessibility.</param>
	/// <param name="type">The record type.</param>
	/// <param name="declaring">The type that declares the member.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsAccessible(Access accessibility, Type type, Type declaring) =>
		declaring == type || accessibility switch
		{
			Access.Private => false,
			Access.Internal or Access.PrivateProtected => declaring.Assembly == type.Assembly,
			_ => true,
		};

	/// <summary>Whether an accessibility is one the generator reads without a <see cref="CsvColumnAttribute"/>: public, internal or protected internal.</summary>
	/// <param name="accessibility">The accessibility.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsVisible(Access accessibility) => accessibility is Access.Public or Access.Internal or Access.ProtectedInternal;

	/// <summary>The accessibility of a property, the widest of its accessors', or of one accessor.</summary>
	/// <param name="first">An accessor, or <see langword="null"/>.</param>
	/// <param name="second">The other, or <see langword="null"/>.</param>
	/// <returns>The accessibility.</returns>
	private static Access Accessibility(MethodBase? first, MethodBase? second)
	{
		var a = first is null ? Access.Private : Accessibility(first);
		var b = second is null ? Access.Private : Accessibility(second);

		// Protected and internal accessors make a protected internal property.
		return (a, b) is (Access.Protected, Access.Internal) or (Access.Internal, Access.Protected) ? Access.ProtectedInternal : a > b ? a : b;
	}

	/// <summary>The accessibility of a method or constructor.</summary>
	/// <param name="method">The method.</param>
	/// <returns>Its accessibility.</returns>
	private static Access Accessibility(MethodBase method) =>
		method.IsPublic ? Access.Public : method.IsFamilyOrAssembly ? Access.ProtectedInternal : method.IsAssembly ? Access.Internal :
		method.IsFamily ? Access.Protected : method.IsFamilyAndAssembly ? Access.PrivateProtected : Access.Private;

	/// <summary>The accessibility of a field.</summary>
	/// <param name="field">The field.</param>
	/// <returns>Its accessibility.</returns>
	private static Access Accessibility(FieldInfo field) =>
		field.IsPublic ? Access.Public : field.IsFamilyOrAssembly ? Access.ProtectedInternal : field.IsAssembly ? Access.Internal :
		field.IsFamily ? Access.Protected : field.IsFamilyAndAssembly ? Access.PrivateProtected : Access.Private;

	/// <summary>A member's accessibility, from the narrowest.</summary>
	private enum Access
	{
		/// <summary>Private.</summary>
		Private,

		/// <summary>Private protected.</summary>
		PrivateProtected,

		/// <summary>Protected.</summary>
		Protected,

		/// <summary>Internal.</summary>
		Internal,

		/// <summary>Protected internal.</summary>
		ProtectedInternal,

		/// <summary>Public.</summary>
		Public,
	}
}
