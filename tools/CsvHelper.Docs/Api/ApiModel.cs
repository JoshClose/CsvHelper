using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CsvHelper.Docs.Api;

internal enum ApiMemberKind
{
	Constructor,
	Property,
	Indexer,
	Method,
	Operator,
	Event,
	Field,
	EnumValue,
}

/// <summary>The public API of the library across its target frameworks, with the symbols to render it.</summary>
internal sealed class ApiSet
{
	public required IReadOnlyList<string> Frameworks { get; init; }
	public required IReadOnlyList<ApiType> Types { get; init; }
	public required IReadOnlyDictionary<string, ApiType> TypesById { get; init; }
	public required IReadOnlyDictionary<string, ApiMember> MembersById { get; init; }

	/// <summary>The compilation of the first target framework, used to resolve crefs to the BCL.</summary>
	public required Compilation PrimaryCompilation { get; init; }

	public IEnumerable<ApiMember> Members => Types.SelectMany(t => t.Members);
}

/// <summary>One version of a type or member: the frameworks where it has this signature and these docs.</summary>
internal sealed class ApiVariant
{
	public required IReadOnlyList<string> Frameworks { get; init; }
	public required ISymbol Symbol { get; init; }
	public required string Signature { get; init; }
	public required string? DocXml { get; init; }
}

internal sealed class ApiType
{
	public required string Id { get; init; }

	/// <summary>The display name: <c>CsvRecordMapper&lt;T&gt;</c>, <c>CsvReader.Enumerator</c>.</summary>
	public required string Name { get; init; }

	public required string Namespace { get; init; }

	/// <summary>class, static class, record, struct, record struct, ref struct, enum, delegate, interface.</summary>
	public required string Kind { get; init; }

	/// <summary>The sidebar group: Classes, Records, Structs, Enums, Delegates, Attributes, Interfaces.</summary>
	public required string Group { get; init; }

	public required string FileName { get; init; }
	public required IReadOnlyList<ApiVariant> Variants { get; init; }
	public List<ApiMember> Members { get; } = [];
	public List<ApiType> NestedTypes { get; } = [];
	public ApiType? ContainingType { get; set; }

	public INamedTypeSymbol Symbol => (INamedTypeSymbol)Variants[0].Symbol;

	public IReadOnlyList<string> Frameworks => Variants.SelectMany(v => v.Frameworks).ToList();
}

internal sealed class ApiMember
{
	public required string Id { get; init; }
	public required string Name { get; init; }
	public required ApiMemberKind Kind { get; init; }

	/// <summary>The heading: <c>GetString(int)</c>, <c>this[string]</c>, <c>FieldCount</c>.</summary>
	public required string ShortName { get; init; }

	public required ApiType Type { get; init; }
	public required IReadOnlyList<ApiVariant> Variants { get; init; }
	public string Anchor { get; set; } = "";

	public ISymbol Symbol => Variants[0].Symbol;

	public IReadOnlyList<string> Frameworks => Variants.SelectMany(v => v.Frameworks).ToList();
}

/// <summary>
/// The API of one version, reduced to what release diffs compare. Snapshots of tags are cached as JSON, so bump
/// <see cref="CurrentSchema"/> whenever the content changes (it is part of the cache file name).
/// </summary>
internal sealed class ApiSnapshot
{
	public const int CurrentSchema = 1;

	public int Schema { get; init; } = CurrentSchema;
	public List<string> Frameworks { get; init; } = [];
	public List<SnapshotType> Types { get; init; } = [];
	public List<SnapshotMember> Members { get; init; } = [];
}

internal sealed class SnapshotType
{
	public string Id { get; init; } = "";
	public string Name { get; init; } = "";
	public string Kind { get; init; } = "";

	/// <summary>The facets of the type for each framework that has it.</summary>
	public Dictionary<string, TypeFacets> Frameworks { get; init; } = [];
}

internal sealed class TypeFacets
{
	/// <summary>The declaration, as shown on the type page.</summary>
	public string Header { get; init; } = "";

	/// <summary>Kind and modifiers: <c>sealed class</c>, <c>readonly ref struct</c>.</summary>
	public string Modifiers { get; init; } = "";

	public string? BaseType { get; init; }
	public List<string> Interfaces { get; init; } = [];

	/// <summary>Whether code outside the library can derive from it (not sealed or static, with an accessible constructor).</summary>
	public bool Inheritable { get; init; }

	public ObsoleteInfo? Obsolete { get; init; }
}

internal sealed class SnapshotMember
{
	public string Id { get; init; } = "";
	public string TypeId { get; init; } = "";
	public string Name { get; init; } = "";
	public string Kind { get; init; } = "";
	public string ShortName { get; init; } = "";
	public Dictionary<string, MemberFacets> Frameworks { get; init; } = [];
}

internal sealed class MemberFacets
{
	public string Signature { get; init; } = "";

	/// <summary>The signature without nullable annotations, to tell nullability-only changes apart.</summary>
	public string SignatureWithoutNullability { get; init; } = "";

	/// <summary>The signature without default values, to tell default-value changes apart.</summary>
	public string SignatureWithoutDefaults { get; init; } = "";

	/// <summary>The accessors of a property or indexer: <c>get</c>, <c>set</c>, <c>init</c>.</summary>
	public List<string> Accessors { get; init; } = [];

	public string? ConstantValue { get; init; }
	public bool IsAbstract { get; init; }
	public ObsoleteInfo? Obsolete { get; init; }
}

internal sealed class ObsoleteInfo
{
	public string? Message { get; init; }
	public bool IsError { get; init; }
}
