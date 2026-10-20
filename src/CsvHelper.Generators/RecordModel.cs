using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CsvHelper.Generators;

/// <summary>
/// Everything the emitter needs for one [CsvRecord] type, as plain values (no symbols or syntax), so the
/// incremental pipeline only regenerates a type when something that affects its code changed.
/// </summary>
/// <param name="Namespace">The namespace, escaped, or null for the global namespace.</param>
/// <param name="Containers">Declarations of the containing types, outermost first, such as <c>partial class Outer</c>.</param>
/// <param name="Declaration">The declaration of the type itself, such as <c>partial record Order</c>.</param>
/// <param name="Name">The name of the record type, for documentation comments and messages.</param>
/// <param name="FullName">The fully qualified name of the record type, with <c>global::</c>.</param>
/// <param name="HostFullName">
/// The fully qualified name of the type the code is generated in: the record type itself, or the [CsvRecordMap] class
/// that maps it.
/// </param>
/// <param name="HintName">The name of the generated file.</param>
/// <param name="Columns">The columns the mapper resolves; column <c>i</c> is stored in field <c>column{i}</c>.</param>
/// <param name="Arguments">The constructor arguments, as expressions.</param>
/// <param name="Initializers">Members set in the object initializer.</param>
/// <param name="Assignments">Optional members set after construction when their column exists.</param>
/// <param name="Steps">How each member is read again on its own, to find the one that made a record fail.</param>
/// <param name="Enums">Enum types that need a parse method.</param>
/// <param name="NullMatchers">Methods that match the [CsvNullValues] of members.</param>
/// <param name="Booleans">Methods that parse the [CsvBooleanValues] of members.</param>
/// <param name="Converters">The [CsvConverter] instances of members.</param>
/// <param name="Cultures">The [CsvCulture] format providers of members.</param>
/// <param name="Nested">The mappers of members whose type is a record type.</param>
/// <param name="FormatArrays">The format lists of members whose [CsvFormat] has several formats.</param>
/// <param name="Validators">The methods that check members with a [CsvValidate].</param>
/// <param name="Collections">The members that read several columns into a collection.</param>
/// <param name="FieldWidths">The fixed-width layout, or empty when no member has a width.</param>
/// <param name="NeedsProvider">Whether a member parses with the reader's format provider.</param>
/// <param name="Refillable">
/// Whether the mapper can read a record into an existing instance: a class whose constructor takes no parameters and
/// whose members can all be set after construction (no <c>init</c> accessors).
/// </param>
/// <param name="Options">The [CsvOptions] of the type, or <see langword="null"/> when it has none.</param>
/// <param name="Writer">How the generated record writer writes the type's records.</param>
/// <param name="Diagnostics">Problems found; no code is generated when one of them is an error.</param>
internal sealed record RecordModel(
	string? Namespace,
	EquatableArray<string> Containers,
	string Declaration,
	string Name,
	string FullName,
	string HostFullName,
	string HintName,
	EquatableArray<ColumnModel> Columns,
	EquatableArray<string> Arguments,
	EquatableArray<AssignmentModel> Initializers,
	EquatableArray<AssignmentModel> Assignments,
	EquatableArray<StepModel> Steps,
	EquatableArray<EnumModel> Enums,
	EquatableArray<NullMatcherModel> NullMatchers,
	EquatableArray<BooleanModel> Booleans,
	EquatableArray<ConverterModel> Converters,
	EquatableArray<CultureModel> Cultures,
	EquatableArray<NestedModel> Nested,
	EquatableArray<FormatArrayModel> FormatArrays,
	EquatableArray<ValidatorModel> Validators,
	EquatableArray<CollectionModel> Collections,
	EquatableArray<int> FieldWidths,
	bool NeedsProvider,
	bool Refillable,
	OptionsModel? Options,
	WriterModel Writer,
	EquatableArray<DiagnosticInfo> Diagnostics)
{
	/// <summary>
	/// Whether no code is generated although no error was reported, such as for a [CsvRecordMap] whose type does not exist
	/// (which the compiler reports).
	/// </summary>
	public bool Skip { get; init; }

	/// <summary>Whether a diagnostic is an error, or <see cref="Skip"/> is set, in which case no code is generated for the type.</summary>
	public bool HasErrors => Skip || Diagnostics.Any(d => d.Descriptor.DefaultSeverity == DiagnosticSeverity.Error);
}

/// <summary>
/// How a member is read again on its own, in <c>FindFailingMember</c>: its read expression, the method that tries each
/// field of a collection, or its nested record's mapper.
/// </summary>
/// <param name="Member">The member, as named in error messages (<c>Order.Price</c>).</param>
/// <param name="Value">The expression that reads the member, or <see langword="null"/> for a collection or nested record.</param>
/// <param name="Column">The index of the member's column in <see cref="RecordModel.Columns"/>, or -1.</param>
/// <param name="Nested">The field of the nested record's mapper, or <see langword="null"/>.</param>
/// <param name="Finder">The method that finds the failing field of a collection, or <see langword="null"/>.</param>
internal sealed record StepModel(string Member, string? Value, int Column, string? Nested, string? Finder);

/// <summary>A static field <paramref name="Field"/> holding the formats of a [CsvFormat] with several of them.</summary>
/// <param name="Field">The name of the mapper's field.</param>
/// <param name="Formats">The formats, unescaped, in the order they are tried.</param>
internal sealed record FormatArrayModel(string Field, EquatableArray<string> Formats);

/// <summary>A method that passes the value of a member with a [CsvValidate] to its check, and throws when it fails.</summary>
/// <param name="Method">The name of the generated method.</param>
/// <param name="Type">The type of the member, fully qualified, with its nullable annotation.</param>
/// <param name="Check">The check, fully qualified, such as <c>global::Order.IsValidAge</c>.</param>
/// <param name="Message">The message of the [CsvValidate], or <see langword="null"/> for the default one.</param>
/// <param name="Member">The member, as named in error messages (<c>Order.Age</c>).</param>
internal sealed record ValidatorModel(string Method, string Type, string Check, string? Message, string Member);

/// <summary>A member that reads several columns into a collection, with a generated method.</summary>
/// <param name="Method">The name of the generated method that reads it (and of the one that finds a failing field, with <c>Find</c> in front).</param>
/// <param name="Type">The type of the member, fully qualified, with nullable annotations.</param>
/// <param name="Element">The type of the elements (the values of a dictionary), fully qualified, with nullable annotations.</param>
/// <param name="Kind">
/// <c>"array"</c> (also for <c>IEnumerable&lt;T&gt;</c> and the read-only interfaces), <c>"list"</c> (also for
/// <c>IList&lt;T&gt;</c> and <c>ICollection&lt;T&gt;</c>) or <c>"dictionary"</c>.
/// </param>
/// <param name="Field">The field holding the column indexes (by name) or the keys (dictionaries), or <see langword="null"/> for a range.</param>
/// <param name="Names">The header names of a collection read by name, unescaped; empty otherwise.</param>
/// <param name="Optional">Whether a collection read by name may have no column.</param>
/// <param name="Start">The first column of a range or dictionary; -1 for a collection read by name.</param>
/// <param name="End">The last column of a range or dictionary, or -1 for the last field of the record (or header).</param>
/// <param name="Value">The expression that reads one element from the field at index <c>i</c>.</param>
/// <param name="Member">The member, as named in error messages (<c>Order.Values</c>).</param>
internal sealed record CollectionModel(
	string Method, string Type, string Element, string Kind, string? Field, EquatableArray<string> Names, bool Optional, int Start, int End,
	string Value, string Member);

/// <summary>The settings of a type's [CsvOptions], as object initializers of the reader and writer options.</summary>
/// <param name="Reader">The assignments of the reader options, such as <c>Separator = ';'</c>.</param>
/// <param name="Writer">The assignments of the writer options.</param>
internal sealed record OptionsModel(EquatableArray<string> Reader, EquatableArray<string> Writer);

/// <summary>
/// How the generated record writer writes a type's records. It uses the converters and nested records of the
/// <see cref="RecordModel"/>, which every member that is read also writes.
/// </summary>
/// <param name="Problem">
/// Why the records cannot be written (such as a constructor parameter without a property to write it from), or
/// <see langword="null"/>. The type then gets a writer factory that throws with this reason, and no diagnostic: a type
/// that is only read has nothing to fix.
/// </param>
/// <param name="Columns">The columns, in the order they are written.</param>
/// <param name="Enums">Enum types that need a format method.</param>
/// <param name="Cultures">The [CsvCulture] format providers that members are formatted with.</param>
internal sealed record WriterModel(
	string? Problem,
	EquatableArray<WriteColumnModel> Columns,
	EquatableArray<EnumFormatModel> Enums,
	EquatableArray<CultureModel> Cultures)
{
	/// <summary>A writer without columns, the starting point of a model.</summary>
	public static WriterModel Empty { get; } =
		new(null, EquatableArray<WriteColumnModel>.Empty, EquatableArray<EnumFormatModel>.Empty, EquatableArray<CultureModel>.Empty);
}

/// <summary>
/// A column of the written records: a member's field, an empty field where no member has the column, or the columns
/// of a nested record.
/// </summary>
/// <param name="Name">The header name, unescaped; <c>""</c> for an empty column; <see langword="null"/> for a nested record.</param>
/// <param name="Nested">The field holding the nested record's writer, or <see langword="null"/>.</param>
/// <param name="Lines">The statements that write the field (or fields), with <c>writer</c> and <c>record</c> in scope.</param>
internal sealed record WriteColumnModel(string? Name, string? Nested, EquatableArray<string> Lines);

/// <summary>An enum type whose members are written by a generated method.</summary>
/// <param name="Method">The name of the format method.</param>
/// <param name="Type">The fully qualified enum type.</param>
/// <param name="Cases">The text written for each value: the first member that has it.</param>
internal sealed record EnumFormatModel(string Method, string Type, EquatableArray<EnumNameModel> Cases);

/// <summary>A column a member reads: by <paramref name="Index"/> when it is not negative, else by header name.</summary>
/// <param name="Name">The header name of the column, unescaped.</param>
/// <param name="AlternateNames">Other header names, tried in order when the header lacks <paramref name="Name"/>.</param>
/// <param name="Index">The 0-based index of the column, or -1 to find it by name.</param>
/// <param name="Optional">Whether the column may be missing from the header.</param>
/// <param name="Member">The member, as named in error messages (<c>Order.Price</c>).</param>
/// <param name="NameIndex">Which of several header columns with the name the member reads; 0 for the first.</param>
internal sealed record ColumnModel(string Name, EquatableArray<string> AlternateNames, int Index, bool Optional, string Member, int NameIndex);

/// <summary>An assignment <c>Member = Value</c>; <paramref name="Column"/> is the index into the columns.</summary>
/// <param name="Member">The member to set, escaped.</param>
/// <param name="Value">The expression that reads its value.</param>
/// <param name="Column">The index of the member's column in <see cref="RecordModel.Columns"/>, or -1 for a nested record.</param>
internal sealed record AssignmentModel(string Member, string Value, int Column);

/// <summary>An enum type whose names are matched by a generated method.</summary>
/// <param name="Method">The name of the parse method.</param>
/// <param name="Type">The fully qualified enum type.</param>
/// <param name="DisplayName">The name of the enum type in error messages, without <c>global::</c>.</param>
/// <param name="Names">The texts that match its members: their names and their [CsvEnumName]s, unescaped.</param>
internal sealed record EnumModel(string Method, string Type, string DisplayName, EquatableArray<EnumNameModel> Names);

/// <summary>A text that matches the enum member <paramref name="Member"/> (its unescaped name).</summary>
/// <param name="Text">The text of a field that reads as the member.</param>
/// <param name="Member">The name of the enum member, unescaped.</param>
internal sealed record EnumNameModel(string Text, string Member);

/// <summary>A method that tells whether a field is one of <paramref name="Values"/> (exact bytes).</summary>
/// <param name="Method">The name of the method.</param>
/// <param name="Values">The [CsvNullValues] of a member.</param>
internal sealed record NullMatcherModel(string Method, EquatableArray<string> Values);

/// <summary>A method that parses a field as true when it is one of <paramref name="TrueValues"/>, false for <paramref name="FalseValues"/>.</summary>
/// <param name="Method">The name of the method.</param>
/// <param name="TrueValues">The values that read as <see langword="true"/>, without surrounding spaces and tabs.</param>
/// <param name="FalseValues">The values that read as <see langword="false"/>, without surrounding spaces and tabs.</param>
internal sealed record BooleanModel(string Method, EquatableArray<string> TrueValues, EquatableArray<string> FalseValues);

/// <summary>A field <paramref name="Field"/> holding an instance of the converter <paramref name="Type"/> (fully qualified).</summary>
/// <param name="Field">The name of the mapper's field.</param>
/// <param name="Type">The converter type, fully qualified with <c>global::</c>.</param>
internal sealed record ConverterModel(string Field, string Type);

/// <summary>A field <paramref name="Field"/> holding the culture called <paramref name="Name"/>.</summary>
/// <param name="Field">The name of the mapper's field.</param>
/// <param name="Name">The name of the culture, as [CsvCulture] gives it.</param>
internal sealed record CultureModel(string Field, string Name);

/// <summary>A field <paramref name="Field"/> holding the mapper of the record type <paramref name="Type"/>, created with <paramref name="Prefix"/>.</summary>
/// <param name="Field">The name of the mapper's field.</param>
/// <param name="Type">The nested record type, fully qualified with <c>global::</c>.</param>
/// <param name="Prefix">The [CsvColumn] prefix of the member, unescaped; <c>""</c> for none.</param>
internal sealed record NestedModel(string Field, string Type, string Prefix);

/// <summary>
/// A mapper the module initializer registers for <paramref name="FullName"/>: <paramref name="Factory"/> is the
/// expression passed to <c>CsvRecordMapper.Register</c>, with <paramref name="FieldWidths"/> when not empty, and
/// <paramref name="WriterFactory"/> the one passed to <c>CsvRecordWriter.Register</c>.
/// </summary>
/// <param name="FullName">The record type, fully qualified with <c>global::</c>.</param>
/// <param name="Factory">The C# expression that creates its mapper from a reader (and a column prefix).</param>
/// <param name="FieldWidths">The fixed-width layout of the type, or empty when it has none.</param>
/// <param name="WriterFactory">
/// The C# expression that creates its record writer from a column prefix, or <see langword="null"/> for a hand-written
/// mapper, whose writer is registered by hand.
/// </param>
/// <param name="OptionsHost">
/// The type whose <c>CreateCsvReaderOptions</c> and <c>CreateCsvWriterOptions</c> create the options of the record type's
/// [CsvOptions], or <see langword="null"/> when it has none.
/// </param>
internal sealed record RegistrationModel(string FullName, string Factory, EquatableArray<int> FieldWidths, string? WriterFactory, string? OptionsHost = null);

/// <summary>A type with a [CsvConverter] that names its hand-written mapper: its registration, unless it has errors.</summary>
/// <param name="Registration">The registration of the mapper, or <see langword="null"/> when it cannot be registered.</param>
/// <param name="Diagnostics">The problems found with the type or the mapper.</param>
internal sealed record MapperModel(RegistrationModel? Registration, EquatableArray<DiagnosticInfo> Diagnostics);

/// <summary>A diagnostic, with its location as plain values.</summary>
/// <param name="Descriptor">The kind of diagnostic.</param>
/// <param name="Location">Where it is reported, or <see langword="null"/> for no source location.</param>
/// <param name="Arguments">The values for the placeholders of the descriptor's message.</param>
internal sealed record DiagnosticInfo(DiagnosticDescriptor Descriptor, LocationInfo? Location, EquatableArray<string> Arguments)
{
	/// <summary>Creates the Roslyn diagnostic to report.</summary>
	/// <returns>The diagnostic, at its location and with its message arguments.</returns>
	public Diagnostic ToDiagnostic() =>
		Diagnostic.Create(Descriptor, Location?.ToLocation(), Arguments.Cast<object>().ToArray());
}

/// <summary>A source location as plain values, which, unlike <see cref="Microsoft.CodeAnalysis.Location"/>, can be cached by the incremental pipeline.</summary>
/// <param name="FilePath">The path of the source file.</param>
/// <param name="Span">The span of the location in the file's text.</param>
/// <param name="LineSpan">The same span as lines and columns.</param>
internal sealed record LocationInfo(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
{
	/// <summary>Captures a location in source.</summary>
	/// <param name="location">The location, which may be <see langword="null"/> or not in source (in metadata).</param>
	/// <returns>Its values, or <see langword="null"/> when it is not in source.</returns>
	public static LocationInfo? From(Location? location) =>
		location is { IsInSource: true } ? new LocationInfo(location.SourceTree!.FilePath, location.SourceSpan, location.GetLineSpan().Span) : null;

	/// <summary>Creates a Roslyn location from the values.</summary>
	/// <returns>A location in the file at <see cref="FilePath"/>.</returns>
	public Location ToLocation() => Location.Create(FilePath, Span, LineSpan);
}
