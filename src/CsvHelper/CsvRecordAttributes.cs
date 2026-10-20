using System;
using System.Collections.Generic;

namespace CsvHelper;

/// <summary>
/// Marks a <see langword="partial"/> class, struct or record that <see cref="CsvReader.GetRecords{T}"/> can read.
/// The CsvHelper source generator writes its mapping code at compile time, so no reflection is involved and the
/// type works with trimming and native AOT.
/// </summary>
/// <remarks>
/// Public and internal settable properties and fields are mapped, and the parameters of the constructor (the
/// one marked <see cref="CsvConstructorAttribute"/>, else the parameterless one, else the one with the most
/// parameters). A member reads the header column with its name (exact match first, then ignoring case and
/// surrounding whitespace) unless <see cref="CsvColumnAttribute"/> says otherwise.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class CsvRecordAttribute : Attribute
{
}

/// <summary>Chooses the column of a member or constructor parameter of a <see cref="CsvRecordAttribute"/> type.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CsvColumnAttribute : Attribute
{
	/// <summary>Reads the column named after the member, with <see cref="Index"/> or <see cref="Optional"/> set.</summary>
	public CsvColumnAttribute()
	{
	}

	/// <summary>Reads the header column called <paramref name="name"/>.</summary>
	/// <param name="name">
	/// The header name of the column. It is matched like the other names: exactly first, then ignoring case and
	/// surrounding whitespace.
	/// </param>
	public CsvColumnAttribute(string name) => Name = name;

	/// <summary>
	/// Reads the header column called <paramref name="name"/>, or else the first of <paramref name="alternateNames"/>
	/// that the header has, for files whose header changed between versions.
	/// </summary>
	/// <param name="name">The header name of the column, tried first.</param>
	/// <param name="alternateNames">Other names of the column, tried in order when the header lacks <paramref name="name"/>.</param>
	public CsvColumnAttribute(string name, params string[] alternateNames)
	{
		Name = name;
		AlternateNames = alternateNames ?? [];
	}

	/// <summary>The header name of the column, or <see langword="null"/> for the member's name.</summary>
	public string? Name { get; }

	/// <summary>
	/// Other header names of the column, tried in order when the header lacks <see cref="Name"/>. Each name is
	/// matched exactly first, then ignoring case and surrounding whitespace, before the next one is tried.
	/// </summary>
	public IReadOnlyList<string> AlternateNames { get; } = [];

	/// <summary>
	/// The 0-based index of the column, used instead of the header; needed when the input has no header.
	/// -1 (the default) finds the column by name. For a collection member, the first of its columns (see
	/// <see cref="EndIndex"/>).
	/// </summary>
	public int Index { get; set; } = -1;

	/// <summary>
	/// For a collection member (an array, <see cref="List{T}"/>, <see cref="IEnumerable{T}"/>, <see cref="IList{T}"/>,
	/// <see cref="IReadOnlyList{T}"/> and the like, or a dictionary with <see cref="string"/> keys) with an
	/// <see cref="Index"/>: the 0-based index of its last column, so that it reads the fields from <see cref="Index"/> to
	/// <see cref="EndIndex"/>. -1 (the default) reads to the last field of each record (for a dictionary, to the last
	/// column of the header). A collection member without an index reads every header column with its name instead.
	/// </summary>
	public int EndIndex { get; set; } = -1;

	/// <summary>
	/// Which of several header columns with the same name the member reads: 0 (the default) for the first, 1 for the
	/// second, and so on. The record writer writes the columns with the same name in this order.
	/// </summary>
	public int NameIndex { get; set; }

	/// <summary>
	/// Whether the column may be missing from the header. A missing optional member keeps its default value;
	/// a missing required one makes <see cref="CsvReader.GetRecords{T}"/> throw. Constructor parameters with a
	/// default value are always optional.
	/// </summary>
	public bool Optional { get; set; }

	/// <summary>
	/// The width of the column in <see cref="CsvMode.FixedWidth"/> files, in characters (UTF-16 code units, as
	/// <see cref="string.Length"/> counts them), or 0 (the default) for none. A member with a width needs an
	/// <see cref="Index"/>: the widths of a type's members, ordered by their indexes, are its layout, which
	/// <see cref="CsvRecordMapper.GetFieldWidths{T}"/> returns for <see cref="CsvReaderOptions.FieldWidths"/>.
	/// </summary>
	public int Width { get; set; }

	/// <summary>
	/// For a member whose type is itself a record type (a <see cref="CsvRecordAttribute"/> type, or a type with a
	/// <see cref="CsvConverterAttribute"/>): the text put in front of each header name that the member's type looks
	/// up, such as <c>"Ship "</c> for the columns <c>Ship Street</c> and <c>Ship City</c>. The prefixes of records
	/// nested in nested records add up. <see langword="null"/> (the default) adds nothing.
	/// </summary>
	public string? Prefix { get; set; }
}

/// <summary>Excludes a property or field of a <see cref="CsvRecordAttribute"/> type from reading.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class CsvIgnoreAttribute : Attribute
{
}

/// <summary>Chooses the constructor that creates the records of a <see cref="CsvRecordAttribute"/> type.</summary>
[AttributeUsage(AttributeTargets.Constructor)]
public sealed class CsvConstructorAttribute : Attribute
{
}

/// <summary>
/// Reads a member of a <see cref="CsvRecordAttribute"/> type with an exact format, such as <c>"yyyyMMdd"</c>,
/// instead of the formats that the format provider accepts.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DateTime"/> and <see cref="DateTimeOffset"/> members are read with
/// <see cref="CsvReader.GetDateTime(int, string, IFormatProvider)"/> and
/// <see cref="CsvReader.GetDateTimeOffset(int, string, IFormatProvider)"/>; members of other types, such as
/// <c>DateOnly</c>, <c>TimeOnly</c> and <see cref="TimeSpan"/>, with their static
/// <c>ParseExact(string, string, IFormatProvider)</c> method.
/// </para>
/// <para>
/// With several formats, a field may have any of them, tried in order (<c>ParseExact</c> with a format array), and the
/// record writer writes the first.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CsvFormatAttribute : Attribute
{
	/// <summary>Reads the member with the given format.</summary>
	/// <param name="format">The exact format of the field, as <see cref="DateTime.ParseExact(string, string, IFormatProvider)"/> takes it.</param>
	public CsvFormatAttribute(string format)
	{
		Format = format;
		Formats = [format];
	}

	/// <summary>
	/// Reads the member with whichever of the formats the field has, tried in order, for files that mix them, such as
	/// <c>[CsvFormat("yyyy-MM-dd", "dd/MM/yyyy")]</c>. The record writer writes <paramref name="format"/>.
	/// </summary>
	/// <param name="format">The first format, which the record writer writes.</param>
	/// <param name="alternateFormats">The other formats the field may have.</param>
	public CsvFormatAttribute(string format, params string[] alternateFormats)
	{
		Format = format;
		Formats = [format, .. alternateFormats ?? []];
	}

	/// <summary>The exact format of the field, or the first of <see cref="Formats"/>.</summary>
	public string Format { get; }

	/// <summary>Every format the field may have, in the order they are tried.</summary>
	public IReadOnlyList<string> Formats { get; }
}

/// <summary>
/// On a member of a <see cref="CsvRecordAttribute"/> type, reads the member with an <see cref="ICsvConverter{T}"/>.
/// On an assembly (<c>[assembly: CsvConverter(typeof(MoneyConverter))]</c>), reads with it every member of the type it
/// converts. On a class or struct, makes a hand-written <see cref="CsvRecordMapper{T}"/> the mapper of the type, which the
/// source generator registers so that <see cref="CsvReader.GetRecords{T}"/> finds it.
/// </summary>
/// <remarks>
/// <para>
/// The source generator creates the converter with <see langword="new"/> and calls it directly, so no reflection is
/// involved and converters work with trimming and native AOT.
/// </para>
/// <para>
/// On a member, the converter type implements <see cref="ICsvConverter{T}"/> for the member's type (or, for a
/// <see cref="Nullable{T}"/> member, for its underlying type, and an empty field reads as <see langword="null"/>)
/// and has a public or internal parameterless constructor. Each mapper creates one instance, which the workers of
/// <see cref="CsvReader.ParallelEnumerate{T}(int)"/> share, so a converter must be thread-safe.
/// </para>
/// <para>
/// On an assembly, the converter applies to every member, in the record types of that assembly, whose type is one that
/// it implements <see cref="ICsvConverter{T}"/> for (or a <see cref="Nullable{T}"/> of it), built-in types included, so
/// <c>[assembly: CsvConverter(typeof(UkDateConverter))]</c> reads every <see cref="DateTime"/> member with it. A member's
/// own <see cref="CsvConverterAttribute"/>, <see cref="CsvFormatAttribute"/>, <see cref="CsvBooleanValuesAttribute"/>
/// or <see cref="CsvCultureAttribute"/> takes precedence. The generated code is the same as with the attribute on each
/// member, so it costs nothing per field. Two converters of the assembly cannot convert the same type.
/// </para>
/// <para>
/// On a type, the converter type derives from <see cref="CsvRecordMapper{T}"/> of that type and has a public or
/// internal constructor that takes a <see cref="CsvReader"/>, or a <see cref="CsvReader"/> and the column prefix
/// (a <see cref="string"/>, passed to the base constructor) for use as a nested record with
/// <see cref="CsvColumnAttribute.Prefix"/>. The type does not need to be <see langword="partial"/> and must not
/// have <see cref="CsvRecordAttribute"/>.
/// </para>
/// </remarks>
[AttributeUsage(
	AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly,
	Inherited = false, AllowMultiple = true)]
public sealed class CsvConverterAttribute : Attribute
{
	/// <summary>Reads the member, the members of a type, or the whole record, with <paramref name="converterType"/>.</summary>
	/// <param name="converterType">
	/// An <see cref="ICsvConverter{T}"/> for a member or an assembly, or a <see cref="CsvRecordMapper{T}"/> for a type.
	/// </param>
	public CsvConverterAttribute(Type converterType) => ConverterType = converterType;

	/// <summary>The type of the converter or mapper.</summary>
	public Type ConverterType { get; }
}

/// <summary>
/// Reads the given values of a field as <see langword="null"/>, for a member of a <see cref="CsvRecordAttribute"/>
/// type whose type is a reference type or a <see cref="Nullable{T}"/>, such as <c>NULL</c> or <c>\N</c> in
/// database exports.
/// </summary>
/// <remarks>
/// The field must equal a value exactly (ordinal comparison, after the trimming the reader options ask for). An
/// empty field still reads as <see langword="null"/> for a <see cref="Nullable{T}"/> and as <c>""</c> for a
/// <see cref="string"/>; list <c>""</c> to make empty strings <see langword="null"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CsvNullValuesAttribute : Attribute
{
	/// <summary>Reads <paramref name="value"/> as <see langword="null"/>.</summary>
	/// <param name="value">The value that means <see langword="null"/>.</param>
	public CsvNullValuesAttribute(string value) => Values = value is null ? [] : [value];

	/// <summary>Reads the given values as <see langword="null"/>.</summary>
	/// <param name="values">The values that mean <see langword="null"/>.</param>
	public CsvNullValuesAttribute(params string[] values) => Values = values ?? [];

	/// <summary>The values that mean <see langword="null"/>.</summary>
	public IReadOnlyList<string> Values { get; }
}

/// <summary>
/// Reads a <see cref="bool"/> member of a <see cref="CsvRecordAttribute"/> type with the given true and false
/// values, such as <c>Ja</c> and <c>Nein</c>, instead of those of <see cref="CsvReader.GetBoolean(int)"/>.
/// </summary>
/// <remarks>
/// The values are matched ignoring ASCII case and surrounding spaces and tabs, as <see cref="CsvReader.GetBoolean(int)"/>
/// matches its own. Any other value throws a <see cref="FormatException"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CsvBooleanValuesAttribute : Attribute
{
	/// <summary>Reads <paramref name="trueValue"/> as <see langword="true"/> and <paramref name="falseValue"/> as <see langword="false"/>.</summary>
	/// <param name="trueValue">The value that means <see langword="true"/>.</param>
	/// <param name="falseValue">The value that means <see langword="false"/>.</param>
	public CsvBooleanValuesAttribute(string trueValue, string falseValue)
	{
		TrueValues = [trueValue];
		FalseValues = [falseValue];
	}

	/// <summary>Reads each of <paramref name="trueValues"/> as <see langword="true"/> and each of <paramref name="falseValues"/> as <see langword="false"/>.</summary>
	/// <param name="trueValues">The values that mean <see langword="true"/>.</param>
	/// <param name="falseValues">The values that mean <see langword="false"/>.</param>
	public CsvBooleanValuesAttribute(string[] trueValues, string[] falseValues)
	{
		TrueValues = trueValues ?? [];
		FalseValues = falseValues ?? [];
	}

	/// <summary>The values that mean <see langword="true"/>.</summary>
	public IReadOnlyList<string> TrueValues { get; }

	/// <summary>The values that mean <see langword="false"/>.</summary>
	public IReadOnlyList<string> FalseValues { get; }
}

/// <summary>
/// Gives a member of an enum other names that the members of <see cref="CsvRecordAttribute"/> types read as it,
/// such as <c>"on hold"</c> for <c>OnHold</c>. Its own name still matches.
/// </summary>
/// <remarks>
/// The names are matched like the members' own names: ignoring ASCII case, unless two names of the enum differ only
/// in case. They match whole fields only, not the parts of a list of flags.
/// </remarks>
[AttributeUsage(AttributeTargets.Field)]
public sealed class CsvEnumNameAttribute : Attribute
{
	/// <summary>Reads <paramref name="name"/> as this enum member.</summary>
	/// <param name="name">The other name of the enum member.</param>
	public CsvEnumNameAttribute(string name) => Names = name is null ? [] : [name];

	/// <summary>Reads each of <paramref name="names"/> as this enum member.</summary>
	/// <param name="names">The other names of the enum member.</param>
	public CsvEnumNameAttribute(params string[] names) => Names = names ?? [];

	/// <summary>The other names of the enum member.</summary>
	public IReadOnlyList<string> Names { get; }
}

/// <summary>
/// The value that an empty field gives a member of a <see cref="CsvRecordAttribute"/> type, instead of a
/// <see cref="FormatException"/> for a number, <see langword="null"/> for a <see cref="Nullable{T}"/> or <c>""</c>
/// for a <see cref="string"/>.
/// </summary>
/// <remarks>
/// The value must be a constant that converts implicitly to the member's type, or <see langword="null"/> for a
/// reference type or a <see cref="Nullable{T}"/>. A column that is missing from the header is a different case,
/// which <see cref="CsvColumnAttribute.Optional"/> handles.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CsvDefaultAttribute : Attribute
{
	/// <summary>Gives the member <paramref name="value"/> when its field is empty.</summary>
	/// <param name="value">The value of an empty field.</param>
	public CsvDefaultAttribute(object? value) => Value = value;

	/// <summary>The value of an empty field.</summary>
	public object? Value { get; }
}

/// <summary>
/// Reads a member of a <see cref="CsvRecordAttribute"/> type with the given culture instead of the reader's
/// <see cref="CsvReaderOptions.FormatProvider"/>, for files whose columns use different number or date formats.
/// </summary>
/// <remarks>
/// The culture is looked up with <see cref="System.Globalization.CultureInfo.GetCultureInfo(string)"/> when a mapper
/// is created, so an unknown name (or any name but the invariant culture's in an app that runs in globalization
/// invariant mode) throws a <see cref="System.Globalization.CultureNotFoundException"/> there.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CsvCultureAttribute : Attribute
{
	/// <summary>Reads the member with the culture called <paramref name="name"/>.</summary>
	/// <param name="name">The name of the culture, such as <c>"de-DE"</c>; <c>""</c> is the invariant culture.</param>
	public CsvCultureAttribute(string name) => Name = name;

	/// <summary>The name of the culture.</summary>
	public string Name { get; }
}

/// <summary>
/// Gives a member of a <see cref="CsvRecordAttribute"/> type a fixed value instead of reading a column: every record
/// read gets <see cref="Value"/>, and the record writer writes it in the member's column, whatever the member holds.
/// </summary>
/// <remarks>
/// The value must be a constant that converts implicitly to the member's type, or <see langword="null"/> for a reference
/// type or a <see cref="Nullable{T}"/>. The header does not need the column. The column's other attributes, such as its
/// name and <see cref="CsvColumnAttribute.Index"/>, still say where the record writer writes it.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CsvConstantAttribute : Attribute
{
	/// <summary>Gives the member <paramref name="value"/> in every record.</summary>
	/// <param name="value">The value of the member.</param>
	public CsvConstantAttribute(object? value) => Value = value;

	/// <summary>The value of the member.</summary>
	public object? Value { get; }
}

/// <summary>
/// Checks the value of a member of a <see cref="CsvRecordAttribute"/> type with a static method of the type, such as
/// <c>[CsvValidate(nameof(IsValidAge))]</c>: a value that the method rejects makes the record fail with a
/// <see cref="CsvValidationException"/>, which the record methods of <see cref="CsvReader"/> report in a
/// <see cref="CsvFormatException"/> that names the record, the field and the member.
/// </summary>
/// <remarks>
/// The method is a static method of the type (of the map class, for a <see cref="CsvRecordMapAttribute"/>), of any
/// accessibility, that takes one parameter of the member's type and returns <see cref="bool"/>: <see langword="true"/>
/// for a valid value. It runs after the field is read, so it sees the value as the member gets it (<see langword="null"/>
/// for an empty field of a nullable member, the value of <see cref="CsvDefaultAttribute"/>). It does not run when the
/// column of an optional member is missing, nor when records are written.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CsvValidateAttribute : Attribute
{
	/// <summary>Checks the member's value with the static method called <paramref name="methodName"/>.</summary>
	/// <param name="methodName">The name of the method, such as <c>nameof(IsValidAge)</c>.</param>
	public CsvValidateAttribute(string methodName) => MethodName = methodName;

	/// <summary>The name of the method that checks the value.</summary>
	public string MethodName { get; }

	/// <summary>
	/// The message of the <see cref="CsvValidationException"/>, such as <c>"Age must be between 0 and 150."</c>, or
	/// <see langword="null"/> (the default) for one that names the member and, when
	/// <see cref="CsvReaderOptions.IncludeValuesInExceptions"/> is on, the value.
	/// </summary>
	public string? Message { get; set; }
}

/// <summary>
/// Reads and writes only the members that a <see cref="CsvRecordAttribute"/> class declares itself, not those of its base
/// classes.
/// </summary>
/// <remarks>
/// The constructor's parameters are still read, whichever class declares the members they set.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CsvIgnoreBaseAttribute : Attribute
{
}

/// <summary>
/// Maps a type that you cannot annotate, such as a type of another library or generated code: on a
/// <see langword="partial"/> class, makes the source generator write the mapper and record writer of
/// <see cref="RecordType"/> as if it were a <see cref="CsvRecordAttribute"/> type, with the attributes that the members of
/// this class carry.
/// </summary>
/// <remarks>
/// <para>
/// Each property or field of the map class stands in for the member of <see cref="RecordType"/> with the same name, and
/// must have the same type; it only carries attributes (<see cref="CsvColumnAttribute"/>, <see cref="CsvIgnoreAttribute"/>,
/// <see cref="CsvFormatAttribute"/> and the others), and the map class is never created. Members of the record type
/// without a stand-in are mapped as usual, and so are the parameters of its constructor, which take the attributes of the
/// stand-in with their name. Attributes of the record type itself, such as <see cref="CsvOptionsAttribute"/> and
/// <see cref="CsvIgnoreBaseAttribute"/>, go on the map class, and so do the methods of <see cref="CsvValidateAttribute"/>.
/// </para>
/// <para>
/// The generated code uses only what the map class can access: the public (or, in the same assembly, internal) members
/// and constructors of the record type. <see cref="CsvReader.GetRecords{T}"/> and the other record methods find the
/// mapper of the record type as they find those of <see cref="CsvRecordAttribute"/> types. A mapped type cannot be a
/// nested record of another record type.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CsvRecordMapAttribute : Attribute
{
	/// <summary>Maps <paramref name="recordType"/> with the attributes of this class's members.</summary>
	/// <param name="recordType">The type whose records are read and written.</param>
	public CsvRecordMapAttribute(Type recordType) => RecordType = recordType;

	/// <summary>The type whose records are read and written.</summary>
	public Type RecordType { get; }
}

/// <summary>
/// Keeps the dialect of a <see cref="CsvRecordAttribute"/> type's files next to the type: the settings that
/// <see cref="CsvReaderOptions.For{T}"/> and <see cref="CsvWriterOptions.For{T}"/> put in the options they return, such
/// as <c>[CsvOptions(Separator = ';', HasHeader = false)]</c>.
/// </summary>
/// <remarks>
/// Only the properties that are set change the options; the others keep the defaults. The source generator turns the
/// attribute into code, so no reflection is involved. A <see cref="CsvMode.FixedWidth"/> mode also sets the options'
/// field widths to the type's layout (<see cref="CsvRecordMapper.GetFieldWidths{T}"/>). The options are checked when a
/// reader or writer is created with them.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class CsvOptionsAttribute : Attribute
{
	/// <summary>The field separator (<see cref="CsvReaderOptions.Separator"/>, <see cref="CsvWriterOptions.Separator"/>).</summary>
	public char Separator { get; set; } = ',';

	/// <summary>How fields are delimited and escaped (<see cref="CsvReaderOptions.Mode"/>, <see cref="CsvWriterOptions.Mode"/>).</summary>
	public CsvMode Mode { get; set; }

	/// <summary>
	/// The escape character of <see cref="CsvMode.Escape"/> mode (<see cref="CsvReaderOptions.Escape"/>,
	/// <see cref="CsvWriterOptions.Escape"/>).
	/// </summary>
	public char Escape { get; set; } = '\\';

	/// <summary>
	/// Whether the files have a header (<see cref="CsvReaderOptions.HasHeader"/>, <see cref="CsvWriterOptions.HasHeader"/>).
	/// </summary>
	public bool HasHeader { get; set; } = true;

	/// <summary>
	/// The character that starts comment lines (<see cref="CsvReaderOptions.CommentPrefix"/>,
	/// <see cref="CsvWriterOptions.CommentPrefix"/>).
	/// </summary>
	public char CommentPrefix { get; set; }

	/// <summary>Whether spaces and tabs around values are removed when reading (<see cref="CsvReaderOptions.TrimWhitespace"/>).</summary>
	public bool TrimWhitespace { get; set; }

	/// <summary>The line ending written after each record (<see cref="CsvWriterOptions.NewLine"/>).</summary>
	public string? NewLine { get; set; }

	/// <summary>
	/// The name of the culture that numbers and dates are read and written with, such as <c>"de-DE"</c>
	/// (<see cref="CsvReaderOptions.FormatProvider"/>, <see cref="CsvWriterOptions.FormatProvider"/>); <c>""</c> is the
	/// invariant culture. It is looked up when the options are created.
	/// </summary>
	public string? Culture { get; set; }

	/// <summary>
	/// The field values that mean <see langword="null"/> (<see cref="CsvReaderOptions.NullValues"/>); the record writer
	/// writes the first (<see cref="CsvWriterOptions.NullValue"/>).
	/// </summary>
	public string[]? NullValues { get; set; }

	/// <summary>
	/// The values that read as <see langword="true"/> (<see cref="CsvReaderOptions.TrueValues"/>); the writer writes the
	/// first (<see cref="CsvWriterOptions.TrueValue"/>).
	/// </summary>
	public string[]? TrueValues { get; set; }

	/// <summary>
	/// The values that read as <see langword="false"/> (<see cref="CsvReaderOptions.FalseValues"/>); the writer writes the
	/// first (<see cref="CsvWriterOptions.FalseValue"/>).
	/// </summary>
	public string[]? FalseValues { get; set; }
}
