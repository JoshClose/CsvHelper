using Microsoft.CodeAnalysis;

namespace CsvHelper.Generators;

/// <summary>The diagnostics the [CsvRecord] generator reports, CSVH001 to CSVH012.</summary>
internal static class Diagnostics
{
	private const string Category = "CsvHelper";

	/// <summary>CSVH001: a [CsvRecord] type, or a type that contains it, is not <c>partial</c>. Arguments: the type name.</summary>
	public static readonly DiagnosticDescriptor NotPartial = new(
		"CSVH001", "CSV record types must be partial",
		"'{0}' must be partial, and so must every type that contains it, for its [CsvRecord] code to be generated",
		Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

	/// <summary>
	/// CSVH002: a member's type cannot be read from a field (no typed getter, enum, <c>Parse</c> method, converter or
	/// record type). Arguments: the member and its type.
	/// </summary>
	public static readonly DiagnosticDescriptor UnsupportedMemberType = new(
		"CSVH002", "Unsupported CSV member type",
		"'{0}' has type '{1}', which cannot be read from a CSV field; use a type with a static Parse method or add [CsvIgnore]",
		Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

	/// <summary>
	/// CSVH003: no constructor can create the records (the type is abstract or static, has no public or internal
	/// constructor, or the choice is ambiguous). Arguments: the type name and the reason.
	/// </summary>
	public static readonly DiagnosticDescriptor NoConstructor = new(
		"CSVH003", "No constructor for CSV records",
		"Cannot create '{0}' records: {1}",
		Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

	/// <summary>
	/// CSVH004: the type cannot be a record type (generic, a <c>ref struct</c>, file-local, not accessible from its whole
	/// assembly, or it contains itself through nested records). Arguments: the type name and the reason.
	/// </summary>
	public static readonly DiagnosticDescriptor UnsupportedRecordType = new(
		"CSVH004", "Unsupported CSV record type",
		"'{0}' cannot be a [CsvRecord] type: {1}",
		Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

	/// <summary>
	/// CSVH005 (warning): a member has [CsvColumn] but cannot be set and no constructor parameter sets it, so it is not
	/// read. Arguments: the member.
	/// </summary>
	public static readonly DiagnosticDescriptor NotSettable = new(
		"CSVH005", "CSV column member cannot be set",
		"'{0}' has [CsvColumn] but cannot be set and is not a constructor parameter, so it is not read",
		Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

	/// <summary>
	/// CSVH006: a <c>required</c> member has [CsvIgnore] and the constructor is not marked
	/// <c>[SetsRequiredMembers]</c>, so nothing would set it. Arguments: the member.
	/// </summary>
	public static readonly DiagnosticDescriptor RequiredIgnored = new(
		"CSVH006", "Required member cannot be ignored",
		"'{0}' is required, so it cannot have [CsvIgnore]",
		Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

	/// <summary>
	/// CSVH007: the project uses C# 8 or earlier, which cannot compile the generated code (module initializers, static
	/// lambdas). Arguments: the type name.
	/// </summary>
	public static readonly DiagnosticDescriptor LanguageVersionTooLow = new(
		"CSVH007", "C# 9 or later needed",
		"The code generated for [CsvRecord] or [CsvConverter] type '{0}' needs C# 9 or later (LangVersion)",
		Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

	/// <summary>
	/// CSVH008: the widths of the members do not make a fixed-width layout (a width without an index or below zero, a
	/// column with no width or two, or nested records in the type). Arguments: the type name and the reason.
	/// </summary>
	public static readonly DiagnosticDescriptor InvalidFixedWidthLayout = new(
		"CSVH008", "Invalid fixed-width layout",
		"The fixed-width layout of '{0}' is not valid: {1}",
		Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

	/// <summary>
	/// CSVH009: the type named by a [CsvConverter] cannot be used: on a member, it is not an <c>ICsvConverter&lt;T&gt;</c>
	/// of the member's type with a parameterless constructor; on a type, it is not a <c>CsvRecordMapper&lt;T&gt;</c> of
	/// that type with a suitable constructor, or the type also has [CsvRecord]. Arguments: the member or type, the
	/// converter type and the reason.
	/// </summary>
	public static readonly DiagnosticDescriptor InvalidConverter = new(
		"CSVH009", "Invalid CSV converter",
		"'{0}' cannot use the converter '{1}': {2}",
		Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

	/// <summary>
	/// CSVH010 (warning): an attribute does not apply to its member, such as [CsvFormat] on an <c>int</c>, and is
	/// ignored. Arguments: the member, the attribute and the reason.
	/// </summary>
	public static readonly DiagnosticDescriptor AttributeDoesNotApply = new(
		"CSVH010", "CSV attribute does not apply",
		"'{0}' has {1}, which does not apply: {2}",
		Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

	/// <summary>
	/// CSVH011: an attribute argument is not valid, such as a [CsvDefault] value that does not convert to the member's
	/// type, an empty list of values, or an enum name that names two members. Arguments: the member, the attribute and
	/// the reason.
	/// </summary>
	public static readonly DiagnosticDescriptor InvalidAttributeArgument = new(
		"CSVH011", "Invalid CSV attribute argument",
		"'{0}' has {1} with an invalid argument: {2}",
		Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

	/// <summary>
	/// CSVH012: a [CsvRecordMap] cannot map its type (the type cannot be a record type, already has a mapper, or is not
	/// accessible), or a member of the map class stands in for no member of the type, or for one of another type.
	/// Arguments: the map class, the mapped type and the reason.
	/// </summary>
	public static readonly DiagnosticDescriptor InvalidRecordMap = new(
		"CSVH012", "Invalid CSV record map",
		"'{0}' cannot map '{1}': {2}",
		Category, DiagnosticSeverity.Error, isEnabledByDefault: true);
}
