using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CsvHelper.Docs.Build;
using CsvHelper.Docs.Html;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CsvHelper.Docs.Api;

/// <summary>
/// Reads the public API from the built assemblies and their XML docs, one Roslyn compilation per target framework
/// (with the exact references the libraries were compiled against), and merges the frameworks. The libraries of one
/// framework (CsvHelper, CsvHelper.Linq) share its compilation, so each one's references to the others resolve.
/// </summary>
internal static partial class ApiExtractor
{
	private sealed record Entry(ISymbol Symbol, string Signature, string? DocXml);

	private sealed class FrameworkApi
	{
		public required string Moniker { get; init; }
		public required Compilation Compilation { get; init; }
		public Dictionary<string, Entry> Types { get; } = [];
		public Dictionary<string, Entry> Members { get; } = [];
	}

	public static ApiSet Extract(IReadOnlyList<BuildOutput> outputs, bool strict)
	{
		var frameworks = outputs.GroupBy(o => o.TargetFramework).Select(g => Load(g.ToList(), strict)).ToList();
		var types = new List<ApiType>();
		var typesById = new Dictionary<string, ApiType>();
		var membersById = new Dictionary<string, ApiMember>();
		var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var id in frameworks.SelectMany(f => f.Types.Keys).Distinct())
		{
			var variants = Variants(frameworks, f => f.Types, id);
			var symbol = (INamedTypeSymbol)variants[0].Symbol;
			var type = new ApiType
			{
				Id = id,
				Name = symbol.ToDisplayString(Signatures.TypeName),
				Namespace = symbol.ContainingNamespace.ToDisplayString(),
				Kind = Kind(symbol),
				Group = Group(symbol),
				FileName = FileName(symbol, fileNames),
				Variants = variants,
			};
			types.Add(type);
			typesById.Add(id, type);
		}

		foreach (var type in types)
		{
			if (type.Symbol.ContainingType is { } containing &&
				typesById.TryGetValue(Id(containing), out var parent))
			{
				type.ContainingType = parent;
				parent.NestedTypes.Add(type);
			}
		}

		foreach (var id in frameworks.SelectMany(f => f.Members.Keys).Distinct())
		{
			var variants = Variants(frameworks, f => f.Members, id);
			var symbol = variants[0].Symbol;
			var type = typesById[Id(symbol.ContainingType)];
			var member = new ApiMember
			{
				Id = id,
				Name = symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } ? symbol.ContainingType.Name : symbol.Name,
				Kind = MemberKind(symbol),
				ShortName = ShortName(symbol),
				Type = type,
				Variants = variants,
			};
			type.Members.Add(member);
			membersById.Add(id, member);
		}

		foreach (var type in types)
		{
			type.Members.Sort(CompareMembers);
			type.NestedTypes.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
			var anchors = new HashSet<string>();
			foreach (var member in type.Members) member.Anchor = Slugs.Unique(member.ShortName, anchors);
		}

		types.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

		return new ApiSet
		{
			Frameworks = frameworks.Select(f => f.Moniker).ToList(),
			Types = types,
			TypesById = typesById,
			MembersById = membersById,
			PrimaryCompilation = frameworks[0].Compilation,
		};
	}

	/// <summary>Reduces the API to what release diffs compare.</summary>
	public static ApiSnapshot Snapshot(ApiSet api)
	{
		var snapshot = new ApiSnapshot { Frameworks = [.. api.Frameworks] };
		foreach (var type in api.Types)
		{
			var item = new SnapshotType { Id = type.Id, Name = type.Name, Kind = type.Kind };
			foreach (var variant in type.Variants)
			{
				var symbol = (INamedTypeSymbol)variant.Symbol;
				var facets = new TypeFacets
				{
					Header = variant.Signature,
					Modifiers = Signatures.Modifiers(symbol),
					BaseType = Signatures.BaseType(symbol)?.ToDisplayString(Signatures.TypeName),
					Interfaces = Signatures.Interfaces(symbol).Select(i => i.ToDisplayString(Signatures.TypeName)).ToList(),
					Inheritable = symbol.TypeKind == TypeKind.Class && !symbol.IsSealed && !symbol.IsStatic &&
								  symbol.InstanceConstructors.Any(c => c.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal),
					Obsolete = Attributes.ObsoleteInfo(symbol),
				};
				foreach (var framework in variant.Frameworks) item.Frameworks[framework] = facets;
			}

			snapshot.Types.Add(item);
			foreach (var member in type.Members)
			{
				var memberItem = new SnapshotMember
				{
					Id = member.Id,
					TypeId = type.Id,
					Name = member.Name,
					Kind = member.Kind.ToString(),
					ShortName = member.ShortName,
				};
				foreach (var variant in member.Variants)
				{
					var facets = MemberFacets(variant);
					foreach (var framework in variant.Frameworks) memberItem.Frameworks[framework] = facets;
				}

				snapshot.Members.Add(memberItem);
			}
		}

		return snapshot;
	}

	/// <summary>The documentation comment ID of a declared symbol: <c>T:CsvHelper.CsvReader</c>.</summary>
	public static string Id(ISymbol symbol) =>
		symbol.GetDocumentationCommentId() ?? throw new InvalidOperationException($"{symbol} has no documentation ID.");

	/// <summary>Reads the API of the libraries built for one target framework, in one compilation.</summary>
	/// <param name="outputs">The builds of the libraries for the framework.</param>
	/// <param name="strict">Whether to warn about undocumented and unresolved members.</param>
	private static FrameworkApi Load(IReadOnlyList<BuildOutput> outputs, bool strict)
	{
		var moniker = outputs[0].TargetFramework;
		var libraries = new List<(BuildOutput Output, MetadataReference Reference)>();
		foreach (var output in outputs)
		{
			if (!File.Exists(output.XmlPath)) throw new FileNotFoundException("The XML documentation file is missing. Is GenerateDocumentationFile on?", output.XmlPath);
			libraries.Add((output, MetadataReference.CreateFromFile(output.AssemblyPath, documentation: XmlDocs.Load(output.XmlPath))));
		}

		// A library that references another one (CsvHelper.Linq, CsvHelper) gets the documented one, not its reference assembly.
		var names = new HashSet<string>(outputs.Select(o => Path.GetFileName(o.AssemblyPath)), StringComparer.OrdinalIgnoreCase);
		var references = outputs.SelectMany(o => o.References)
			.Where(r => !names.Contains(Path.GetFileName(r)))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Select(r => (MetadataReference)MetadataReference.CreateFromFile(r))
			.Concat(libraries.Select(l => l.Reference));
		var compilation = CSharpCompilation.Create($"ApiDocs.{moniker}", references: references,
			options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
		var api = new FrameworkApi { Moniker = moniker, Compilation = compilation };
		foreach (var (_, library) in libraries)
		{
			var assembly = (IAssemblySymbol)compilation.GetAssemblyOrModuleSymbol(library)!;
			foreach (var type in AllTypes(assembly.GlobalNamespace).Where(IsVisible))
			{
				var id = Id(type);
				var header = Signatures.Text(Signatures.TypeHeader(type));
				var typeDoc = type.GetDocumentationCommentXml();
				api.Types[id] = new Entry(type, header, typeDoc);
				CheckDocumented(type, typeDoc, moniker, strict);
				if (strict) CheckResolved(type, header, moniker);
				foreach (var member in type.GetMembers().Where(IsShown))
				{
					var signature = Signatures.MemberText(member);
					var doc = DocXml(member);
					api.Members[Id(member)] = new Entry(member, signature, doc);
					CheckDocumented(member, doc, moniker, strict);
					if (strict) CheckResolved(member, signature, moniker);
				}
			}
		}

		return api;
	}

	private static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceSymbol ns)
	{
		foreach (var type in ns.GetTypeMembers())
		{
			yield return type;
			foreach (var nested in Nested(type)) yield return nested;
		}

		foreach (var child in ns.GetNamespaceMembers())
		{
			foreach (var type in AllTypes(child)) yield return type;
		}
	}

	private static IEnumerable<INamedTypeSymbol> Nested(INamedTypeSymbol type)
	{
		foreach (var nested in type.GetTypeMembers())
		{
			yield return nested;
			foreach (var deeper in Nested(nested)) yield return deeper;
		}
	}

	private static bool IsVisible(INamedTypeSymbol type)
	{
		for (var t = type; t is not null; t = t.ContainingType)
		{
			if (t.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal)) return false;
			if (!t.CanBeReferencedByName || Attributes.IsCompilerGenerated(t) || Attributes.IsHidden(t)) return false;
		}

		return true;
	}

	/// <summary>
	/// Whether a member is documented: visible outside the library, not written by the compiler (record members,
	/// accessors, the parameterless struct constructor) and not hidden from IntelliSense.
	/// </summary>
	private static bool IsShown(ISymbol member)
	{
		if (member.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal)) return false;
		if (member.IsImplicitlyDeclared || member is ITypeSymbol) return false;
		if (member is IMethodSymbol method)
		{
			if (method.MethodKind is not (MethodKind.Ordinary or MethodKind.Constructor or MethodKind.UserDefinedOperator or MethodKind.Conversion)) return false;
			if (method.ContainingType.TypeKind == TypeKind.Delegate) return false;
			if (method.MethodKind == MethodKind.Ordinary && !method.CanBeReferencedByName) return false;
		}
		else if (!member.CanBeReferencedByName && member is not IPropertySymbol { IsIndexer: true })
		{
			return false;
		}

		return !Attributes.IsCompilerGenerated(member) && !Attributes.IsHidden(member);
	}

	/// <summary>
	/// The doc comment of a member. The constructor the compiler adds to a class without one has no doc comment, so it
	/// gets a standard summary.
	/// </summary>
	private static string? DocXml(ISymbol member)
	{
		var xml = member.GetDocumentationCommentXml();
		if (!string.IsNullOrWhiteSpace(xml) || member is not IMethodSymbol { MethodKind: MethodKind.Constructor, Parameters.Length: 0 } constructor) return xml;
		var type = Id(constructor.ContainingType);
		var summary = constructor.ContainingType.IsAbstract
			? $"Initializes a new instance of a type derived from <see cref=\"{type}\"/>."
			: $"Initializes a new instance of <see cref=\"{type}\"/>.";

		return $"<member name=\"{Id(member)}\"><summary>{summary}</summary></member>";
	}

	private static void CheckDocumented(ISymbol symbol, string? xml, string framework, bool strict)
	{
		if (!strict) return;
		if (string.IsNullOrWhiteSpace(xml) || !xml.Contains("<summary", StringComparison.Ordinal))
			Log.Warn($"{Id(symbol)} ({framework}) has no <summary>.");
	}

	/// <summary>Warns about types the references did not resolve: their signatures would compare and link wrongly.</summary>
	private static void CheckResolved(ISymbol symbol, string signature, string framework)
	{
		var types = symbol switch
		{
			IMethodSymbol m => m.Parameters.Select(p => p.Type).Append(m.ReturnType),
			IPropertySymbol p => p.Parameters.Select(x => x.Type).Append(p.Type),
			IFieldSymbol f => [f.Type],
			IEventSymbol e => [e.Type],
			INamedTypeSymbol t => t.Interfaces.Cast<ITypeSymbol>().Concat(t.BaseType is null ? [] : [t.BaseType]),
			_ => [],
		};
		if (types.Any(ContainsError)) Log.Warn($"Unresolved type in {signature} ({framework}). Is a reference missing?");
	}

	private static bool ContainsError(ITypeSymbol type) => type switch
	{
		IErrorTypeSymbol => true,
		INamedTypeSymbol named => named.TypeArguments.Any(ContainsError),
		IArrayTypeSymbol array => ContainsError(array.ElementType),
		IPointerTypeSymbol pointer => ContainsError(pointer.PointedAtType),
		_ => false,
	};

	/// <summary>Groups the frameworks that have the same signature and docs for <paramref name="id"/>, in framework order.</summary>
	private static List<ApiVariant> Variants(List<FrameworkApi> frameworks, Func<FrameworkApi, Dictionary<string, Entry>> select, string id)
	{
		var variants = new List<ApiVariant>();
		var groups = frameworks.Where(f => select(f).ContainsKey(id))
			.GroupBy(f => (select(f)[id].Signature, Normalize(select(f)[id].DocXml)));
		foreach (var group in groups)
		{
			var entry = select(group.First())[id];
			variants.Add(new ApiVariant
			{
				Frameworks = group.Select(f => f.Moniker).ToList(),
				Symbol = entry.Symbol,
				Signature = entry.Signature,
				DocXml = entry.DocXml,
			});
		}

		return variants;
	}

	private static string Normalize(string? xml) => xml is null ? "" : Whitespace().Replace(xml, " ").Trim();

	private static MemberFacets MemberFacets(ApiVariant variant)
	{
		var symbol = variant.Symbol;
		var accessors = new List<string>();
		if (symbol is IPropertySymbol property)
		{
			if (IsAccessible(property.GetMethod)) accessors.Add("get");
			if (IsAccessible(property.SetMethod)) accessors.Add(property.SetMethod!.IsInitOnly ? "init" : "set");
		}

		return new MemberFacets
		{
			Signature = variant.Signature,
			SignatureWithoutNullability = Signatures.MemberTextWithoutNullability(symbol),
			SignatureWithoutDefaults = Signatures.MemberTextWithoutDefaults(symbol),
			Accessors = accessors,
			ConstantValue = symbol is IFieldSymbol { HasConstantValue: true } field ? Convert.ToString(field.ConstantValue, System.Globalization.CultureInfo.InvariantCulture) : null,
			IsAbstract = symbol.IsAbstract,
			Obsolete = Attributes.ObsoleteInfo(symbol),
		};
	}

	private static bool IsAccessible(IMethodSymbol? accessor) =>
		accessor is { DeclaredAccessibility: Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal };

	private static string Kind(INamedTypeSymbol type) => type.TypeKind switch
	{
		TypeKind.Class when type.IsRecord => "record",
		TypeKind.Class when type.IsStatic => "static class",
		TypeKind.Class => "class",
		TypeKind.Struct when Signatures.IsRecordStruct(type) => "record struct",
		TypeKind.Struct when type.IsRefLikeType => "ref struct",
		TypeKind.Struct => "struct",
		TypeKind.Enum => "enum",
		TypeKind.Delegate => "delegate",
		TypeKind.Interface => "interface",
		_ => type.TypeKind.ToString().ToLowerInvariant(),
	};

	private static string Group(INamedTypeSymbol type)
	{
		if (type.TypeKind == TypeKind.Delegate) return "Delegates";
		if (type.TypeKind == TypeKind.Enum) return "Enums";
		if (type.TypeKind == TypeKind.Interface) return "Interfaces";
		if (Signatures.IsRecord(type)) return "Records";
		if (type.TypeKind == TypeKind.Struct) return "Structs";
		for (var t = type.BaseType; t is not null; t = t.BaseType)
		{
			if (t.ToDisplayString() == "System.Attribute") return "Attributes";
			if (t.ToDisplayString() == "System.Exception") return "Exceptions";
		}

		return "Classes";
	}

	/// <summary>A flat, lowercase file name: <c>csvreader.html</c>, <c>csvreader.enumerator.html</c>, <c>csvrecordmapper-1.html</c>.</summary>
	private static string FileName(INamedTypeSymbol type, HashSet<string> used)
	{
		var names = new List<string>();
		for (var t = type; t is not null; t = t.ContainingType) names.Insert(0, t.Arity > 0 ? $"{t.Name}-{t.Arity}" : t.Name);
		var name = string.Join('.', names).ToLowerInvariant();
		if (!used.Add(name))
		{
			name = $"{type.ContainingNamespace.ToDisplayString().ToLowerInvariant()}.{name}";
			used.Add(name);
		}

		return name + ".html";
	}

	private static ApiMemberKind MemberKind(ISymbol symbol) => symbol switch
	{
		IMethodSymbol { MethodKind: MethodKind.Constructor } => ApiMemberKind.Constructor,
		IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator or MethodKind.Conversion } => ApiMemberKind.Operator,
		IMethodSymbol => ApiMemberKind.Method,
		IPropertySymbol { IsIndexer: true } => ApiMemberKind.Indexer,
		IPropertySymbol => ApiMemberKind.Property,
		IEventSymbol => ApiMemberKind.Event,
		IFieldSymbol { ContainingType.TypeKind: TypeKind.Enum } => ApiMemberKind.EnumValue,
		_ => ApiMemberKind.Field,
	};

	private static string ShortName(ISymbol symbol)
	{
		var text = symbol.ToDisplayString(Signatures.Short);
		// Constructors display as Type.Type(...): keep the second part.
		if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor })
		{
			var open = text.IndexOf('(');
			var dot = text.LastIndexOf('.', open < 0 ? text.Length - 1 : open);
			if (dot >= 0) text = text[(dot + 1)..];
		}

		return text;
	}

	private static int CompareMembers(ApiMember a, ApiMember b)
	{
		if (a.Kind != b.Kind) return a.Kind.CompareTo(b.Kind);
		if (a.Kind == ApiMemberKind.EnumValue)
		{
			var x = Convert.ToInt64(((IFieldSymbol)a.Symbol).ConstantValue, System.Globalization.CultureInfo.InvariantCulture);
			var y = Convert.ToInt64(((IFieldSymbol)b.Symbol).ConstantValue, System.Globalization.CultureInfo.InvariantCulture);
			if (x != y) return x.CompareTo(y);
		}

		var name = string.CompareOrdinal(a.Name, b.Name);
		if (name != 0) return name;
		var count = Parameters(a.Symbol).CompareTo(Parameters(b.Symbol));

		return count != 0 ? count : string.CompareOrdinal(a.ShortName, b.ShortName);
	}

	private static int Parameters(ISymbol symbol) => symbol switch
	{
		IMethodSymbol m => m.Parameters.Length,
		IPropertySymbol p => p.Parameters.Length,
		_ => 0,
	};

	[GeneratedRegex(@"\s+")]
	private static partial Regex Whitespace();
}
