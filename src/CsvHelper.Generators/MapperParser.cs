using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsvHelper.Generators;

/// <summary>
/// Turns a type with a [CsvConverter] into the registration of the hand-written mapper it names, reporting what
/// keeps the mapper from being registered.
/// </summary>
internal static class MapperParser
{
	/// <summary>
	/// Reads the [CsvConverter] of a type and checks that the mapper it names can be registered for the type: a
	/// <c>CsvRecordMapper&lt;T&gt;</c> of that type, not abstract or open generic, with an accessible constructor that
	/// takes a <c>CsvReader</c> (and the column prefix, preferred when it exists).
	/// </summary>
	/// <param name="context">The type with the attribute, its syntax and its semantic model.</param>
	/// <param name="cancellationToken">Cancels the work when the compilation changes.</param>
	/// <returns>
	/// The registration of the mapper, or a model without one and the diagnostics that explain why. Types that also
	/// have [CsvRecord] get neither: <see cref="RecordParser"/> reports them.
	/// </returns>
	public static MapperModel Parse(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
	{
		var compilation = context.SemanticModel.Compilation;
		var type = (INamedTypeSymbol)context.TargetSymbol;
		var syntax = (TypeDeclarationSyntax)context.TargetNode;
		var attribute = context.Attributes[0];
		var location = attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation() ?? syntax.Identifier.GetLocation();
		var diagnostics = new List<DiagnosticInfo>();

		MapperModel Fail(DiagnosticDescriptor descriptor, params string[] arguments)
		{
			diagnostics.Add(new DiagnosticInfo(descriptor, LocationInfo.From(location), new EquatableArray<string>(arguments)));

			return new MapperModel(null, diagnostics.ToEquatableArray());
		}

		// A [CsvRecord] type reports the conflict itself.
		var recordAttribute = compilation.GetTypeByMetadataName("CsvHelper.CsvRecordAttribute");
		if (type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, recordAttribute)))
			return new MapperModel(null, EquatableArray<DiagnosticInfo>.Empty);
		if (((CSharpParseOptions)syntax.SyntaxTree.Options).LanguageVersion < LanguageVersion.CSharp9) return Fail(Diagnostics.LanguageVersionTooLow, type.Name);
		if (type.IsRefLikeType) return Fail(Diagnostics.UnsupportedRecordType, type.Name, "ref structs cannot be type arguments");
		if (type.IsStatic) return Fail(Diagnostics.UnsupportedRecordType, type.Name, "static types cannot be type arguments");
		for (var t = type; t is not null; t = t.ContainingType)
		{
			if (t.IsGenericType) return Fail(Diagnostics.UnsupportedRecordType, type.Name, "generic types and types nested in them are not supported");
			if (t.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal))
				return Fail(Diagnostics.UnsupportedRecordType, type.Name, "the type must be accessible from its whole assembly (public or internal)");
		}

		// typeof of a type that does not exist is already a compile error.
		if (attribute.ConstructorArguments.FirstOrDefault().Value is not INamedTypeSymbol mapper || mapper.TypeKind == TypeKind.Error)
			return new MapperModel(null, EquatableArray<DiagnosticInfo>.Empty);
		// [CsvConverter] allows several attributes for the assembly-level ones; a type takes one.
		if (context.Attributes.Length > 1) return Fail(Diagnostics.InvalidConverter, type.Name, mapper.ToDisplayString(), "a type can have only one [CsvConverter]");
		var mapperBase = compilation.GetTypeByMetadataName("CsvHelper.CsvRecordMapper`1");
		var reader = compilation.GetTypeByMetadataName("CsvHelper.CsvReader");
		var constructors = mapper.InstanceConstructors.Where(c => compilation.IsSymbolAccessibleWithin(c, compilation.Assembly)).ToList();
		var withPrefix = constructors.FirstOrDefault(c => c.Parameters.Length == 2 && Is(c.Parameters[0].Type, reader) &&
														  c.Parameters[1].Type.SpecialType == SpecialType.System_String);
		var withReader = constructors.FirstOrDefault(c => c.Parameters.Length == 1 && Is(c.Parameters[0].Type, reader));
		string? problem = null;
		if (mapper.IsAbstract) problem = "it is abstract";
		else if (mapper.IsUnboundGenericType || mapper.TypeArguments.Any(a => a is ITypeParameterSymbol)) problem = "it is an open generic type";
		else if (!DerivesFrom(mapper, mapperBase, type)) problem = $"a type-level converter must derive from CsvRecordMapper<{type.ToDisplayString()}>";
		else if (!compilation.IsSymbolAccessibleWithin(mapper, compilation.Assembly)) problem = "it must be accessible from its whole assembly (public or internal)";
		else if (withPrefix is null && withReader is null)
			problem = "it needs a public or internal constructor that takes a CsvReader, or a CsvReader and a string (the column prefix)";
		if (problem is not null) return Fail(Diagnostics.InvalidConverter, type.Name, mapper.ToDisplayString(), problem);

		var name = mapper.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var factory = withPrefix is not null
			? $"static (reader, columnPrefix) => new {name}(reader, columnPrefix)"
			: $"static reader => new {name}(reader)";

		return new MapperModel(
			new RegistrationModel(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), factory, EquatableArray<int>.Empty, null),
			diagnostics.ToEquatableArray());
	}

	private static bool DerivesFrom(INamedTypeSymbol mapper, INamedTypeSymbol? mapperBase, INamedTypeSymbol record)
	{
		for (var t = mapper.BaseType; t is not null; t = t.BaseType)
		{
			if (Is(t.OriginalDefinition, mapperBase) && SymbolEqualityComparer.Default.Equals(t.TypeArguments[0], record)) return true;
		}

		return false;
	}

	private static bool Is(ISymbol? symbol, ISymbol? other) => other is not null && SymbolEqualityComparer.Default.Equals(symbol, other);
}
