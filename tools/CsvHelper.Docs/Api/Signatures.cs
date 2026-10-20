using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CsvHelper.Docs.Api;

internal enum SigKind
{
	Text,
	Keyword,
	Type,
	Member,
	Parameter,
	Number,
	String,
	LineBreak,
}

/// <summary>One token of a rendered declaration. <see cref="Symbol"/> is set for names that can be linked.</summary>
internal readonly record struct SigPart(string Text, SigKind Kind, ISymbol? Symbol = null);

/// <summary>C# declarations of types and members, as tokens that render as text (for diffs) or HTML (with links).</summary>
internal static class Signatures
{
	private const SymbolDisplayMiscellaneousOptions Misc =
		SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
		SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
		SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
		SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral;

	private const SymbolDisplayParameterOptions ParameterOptions =
		SymbolDisplayParameterOptions.IncludeType |
		SymbolDisplayParameterOptions.IncludeName |
		SymbolDisplayParameterOptions.IncludeParamsRefOut |
		SymbolDisplayParameterOptions.IncludeDefaultValue |
		SymbolDisplayParameterOptions.IncludeExtensionThis;

	/// <summary>A member declaration: <c>public string GetString(int index)</c>.</summary>
	public static readonly SymbolDisplayFormat Declaration = new(
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints | SymbolDisplayGenericsOptions.IncludeVariance,
		memberOptions: SymbolDisplayMemberOptions.IncludeAccessibility | SymbolDisplayMemberOptions.IncludeModifiers | SymbolDisplayMemberOptions.IncludeType |
			SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeConstantValue | SymbolDisplayMemberOptions.IncludeRef,
		kindOptions: SymbolDisplayKindOptions.IncludeMemberKeyword,
		propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
		parameterOptions: ParameterOptions,
		miscellaneousOptions: Misc);

	private static readonly SymbolDisplayFormat DeclarationWithoutNullability =
		Declaration.RemoveMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

	private static readonly SymbolDisplayFormat DeclarationWithoutDefaults =
		Declaration.RemoveParameterOptions(SymbolDisplayParameterOptions.IncludeDefaultValue);

	/// <summary>A member heading: <c>GetString(int)</c>, <c>Parse&lt;T&gt;(int)</c>, <c>this[string]</c>.</summary>
	public static readonly SymbolDisplayFormat Short = new(
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
		memberOptions: SymbolDisplayMemberOptions.IncludeParameters,
		parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

	/// <summary>A type name: <c>CsvRecordMapper&lt;T&gt;</c>, <c>CsvReader.Enumerator</c>.</summary>
	public static readonly SymbolDisplayFormat TypeName = new(
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

	private static readonly SymbolDisplayFormat TypeParameters = new(
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
		miscellaneousOptions: Misc);

	private static readonly SymbolDisplayFormat Parameter = new(
		globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
		parameterOptions: ParameterOptions,
		miscellaneousOptions: Misc);

	public static List<SigPart> Member(ISymbol member) => FromDisplay(member.ToDisplayParts(Declaration));

	public static string MemberText(ISymbol member) => member.ToDisplayString(Declaration);

	public static string MemberTextWithoutNullability(ISymbol member) => member.ToDisplayString(DeclarationWithoutNullability);

	public static string MemberTextWithoutDefaults(ISymbol member) => member.ToDisplayString(DeclarationWithoutDefaults);

	/// <summary>A type reference, such as a base type or a parameter type.</summary>
	public static List<SigPart> TypeReference(ITypeSymbol type) => FromDisplay(type.ToDisplayParts(Declaration));

	public static string Text(IEnumerable<SigPart> parts)
	{
		var text = new StringBuilder();
		foreach (var part in parts) text.Append(part.Kind == SigKind.LineBreak ? "\n" : part.Text);

		return text.ToString();
	}

	/// <summary>The full declaration of a type: attributes, modifiers, kind, name, positional parameters, base list and constraints.</summary>
	public static List<SigPart> TypeHeader(INamedTypeSymbol type)
	{
		var parts = new List<SigPart>();
		foreach (var attribute in type.GetAttributes().Where(a => ShownAttributes.Contains(a.AttributeClass?.ToDisplayString() ?? "")))
		{
			AppendAttribute(parts, attribute);
			parts.Add(new SigPart("\n", SigKind.LineBreak));
		}

		foreach (var word in Modifiers(type).Split(' ', StringSplitOptions.RemoveEmptyEntries))
		{
			parts.Add(new SigPart(word, SigKind.Keyword));
			parts.Add(Space);
		}

		if (type.TypeKind == TypeKind.Delegate)
		{
			var invoke = type.DelegateInvokeMethod!;
			parts.AddRange(TypeReference(invoke.ReturnType));
			parts.Add(Space);
			AppendName(parts, type);
			AppendParameters(parts, invoke.Parameters);
			AppendConstraints(parts, type.TypeParameters);

			return parts;
		}

		AppendName(parts, type);
		if (PrimaryConstructor(type) is { } primary) AppendParameters(parts, primary.Parameters);
		var bases = new List<List<SigPart>>();
		if (type.TypeKind == TypeKind.Enum)
		{
			if (type.EnumUnderlyingType is { SpecialType: not SpecialType.System_Int32 } underlying) bases.Add(TypeReference(underlying));
		}
		else
		{
			if (BaseType(type) is { } baseType) bases.Add(TypeReference(baseType));
			foreach (var item in Interfaces(type)) bases.Add(TypeReference(item));
		}

		if (bases.Count > 0)
		{
			parts.Add(new SigPart(" : ", SigKind.Text));
			for (var i = 0; i < bases.Count; i++)
			{
				if (i > 0) parts.Add(new SigPart(", ", SigKind.Text));
				parts.AddRange(bases[i]);
			}
		}

		AppendConstraints(parts, type.TypeParameters);

		return parts;
	}

	/// <summary>Accessibility, modifiers and kind: <c>public sealed class</c>, <c>public readonly record struct</c>.</summary>
	public static string Modifiers(INamedTypeSymbol type)
	{
		var words = new List<string> { Accessibility(type.DeclaredAccessibility) };
		switch (type.TypeKind)
		{
			case TypeKind.Class:
				if (type.IsStatic) words.Add("static");
				else if (type.IsAbstract) words.Add("abstract");
				else if (type.IsSealed) words.Add("sealed");
				words.Add(type.IsRecord ? "record" : "class");
				break;
			case TypeKind.Struct:
				if (type.IsReadOnly) words.Add("readonly");
				if (type.IsRefLikeType) words.Add("ref");
				if (IsRecordStruct(type)) words.Add("record");
				words.Add("struct");
				break;
			case TypeKind.Interface:
				words.Add("interface");
				break;
			case TypeKind.Enum:
				words.Add("enum");
				break;
			case TypeKind.Delegate:
				words.Add("delegate");
				break;
		}

		return string.Join(' ', words);
	}

	/// <summary>The base type shown in the declaration: none for object, ValueType, Enum and MulticastDelegate.</summary>
	public static INamedTypeSymbol? BaseType(INamedTypeSymbol type) =>
		type.TypeKind == TypeKind.Class && type.BaseType is { SpecialType: not SpecialType.System_Object } baseType ? baseType : null;

	/// <summary>The interfaces declared on the type, without the <c>IEquatable&lt;T&gt;</c> that records get.</summary>
	public static IEnumerable<INamedTypeSymbol> Interfaces(INamedTypeSymbol type) =>
		type.Interfaces.Where(i => !(IsRecord(type) && i.Name == "IEquatable" && i.TypeArguments.Length == 1 &&
									 SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], type)));

	public static bool IsRecord(INamedTypeSymbol type) => type.IsRecord || IsRecordStruct(type);

	/// <summary>
	/// Whether a struct is a record struct. Metadata does not mark them (<see cref="ITypeSymbol.IsRecord"/> is false), but
	/// the compiler generates their <c>ToString</c>.
	/// </summary>
	public static bool IsRecordStruct(INamedTypeSymbol type) =>
		type.TypeKind == TypeKind.Struct &&
		type.GetMembers("ToString").OfType<IMethodSymbol>().Any(m => m.Parameters.Length == 0 && Attributes.IsCompilerGenerated(m));

	/// <summary>The primary constructor of a positional record: the one whose parameters match the generated <c>Deconstruct</c>.</summary>
	public static IMethodSymbol? PrimaryConstructor(INamedTypeSymbol type)
	{
		if (!IsRecord(type)) return null;
		var deconstruct = type.GetMembers("Deconstruct").OfType<IMethodSymbol>().FirstOrDefault(Attributes.IsCompilerGenerated);
		if (deconstruct is null) return null;

		return type.InstanceConstructors.FirstOrDefault(c =>
			c.Parameters.Length == deconstruct.Parameters.Length &&
			c.Parameters.Zip(deconstruct.Parameters).All(p => p.First.Name == p.Second.Name && SymbolEqualityComparer.Default.Equals(p.First.Type, p.Second.Type)));
	}

	public static string Accessibility(Accessibility accessibility) => accessibility switch
	{
		Microsoft.CodeAnalysis.Accessibility.Public => "public",
		Microsoft.CodeAnalysis.Accessibility.Protected => "protected",
		Microsoft.CodeAnalysis.Accessibility.ProtectedOrInternal => "protected internal",
		Microsoft.CodeAnalysis.Accessibility.ProtectedAndInternal => "private protected",
		Microsoft.CodeAnalysis.Accessibility.Internal => "internal",
		_ => "private",
	};

	private static readonly HashSet<string> ShownAttributes =
	[
		"System.AttributeUsageAttribute",
		"System.FlagsAttribute",
		"System.ObsoleteAttribute",
	];

	private static readonly SigPart Space = new(" ", SigKind.Text);

	private static void AppendName(List<SigPart> parts, INamedTypeSymbol type)
	{
		parts.Add(new SigPart(type.Name, SigKind.Type, type));
		if (type.TypeParameters.Length == 0) return;
		parts.Add(new SigPart("<", SigKind.Text));
		for (var i = 0; i < type.TypeParameters.Length; i++)
		{
			if (i > 0) parts.Add(new SigPart(", ", SigKind.Text));
			parts.AddRange(FromDisplay(type.TypeParameters[i].ToDisplayParts(TypeParameters)));
		}

		parts.Add(new SigPart(">", SigKind.Text));
	}

	private static void AppendParameters(List<SigPart> parts, IEnumerable<IParameterSymbol> parameters)
	{
		parts.Add(new SigPart("(", SigKind.Text));
		var first = true;
		foreach (var parameter in parameters)
		{
			if (!first) parts.Add(new SigPart(", ", SigKind.Text));
			parts.AddRange(FromDisplay(parameter.ToDisplayParts(Parameter)));
			first = false;
		}

		parts.Add(new SigPart(")", SigKind.Text));
	}

	private static void AppendConstraints(List<SigPart> parts, IEnumerable<ITypeParameterSymbol> typeParameters)
	{
		foreach (var parameter in typeParameters)
		{
			var items = new List<List<SigPart>>();
			if (parameter.HasReferenceTypeConstraint) items.Add([new SigPart("class", SigKind.Keyword)]);
			if (parameter.HasUnmanagedTypeConstraint) items.Add([new SigPart("unmanaged", SigKind.Keyword)]);
			else if (parameter.HasValueTypeConstraint) items.Add([new SigPart("struct", SigKind.Keyword)]);
			if (parameter.HasNotNullConstraint) items.Add([new SigPart("notnull", SigKind.Keyword)]);
			foreach (var constraint in parameter.ConstraintTypes) items.Add(TypeReference(constraint));
			if (parameter.HasConstructorConstraint) items.Add([new SigPart("new", SigKind.Keyword), new SigPart("()", SigKind.Text)]);
			if (parameter.AllowsRefLikeType) items.Add([new SigPart("allows", SigKind.Keyword), Space, new SigPart("ref", SigKind.Keyword), Space, new SigPart("struct", SigKind.Keyword)]);
			if (items.Count == 0) continue;
			parts.Add(new SigPart("\n", SigKind.LineBreak));
			parts.Add(new SigPart("    ", SigKind.Text));
			parts.Add(new SigPart("where", SigKind.Keyword));
			parts.Add(Space);
			parts.Add(new SigPart(parameter.Name, SigKind.Type, parameter));
			parts.Add(new SigPart(" : ", SigKind.Text));
			for (var i = 0; i < items.Count; i++)
			{
				if (i > 0) parts.Add(new SigPart(", ", SigKind.Text));
				parts.AddRange(items[i]);
			}
		}
	}

	private static void AppendAttribute(List<SigPart> parts, AttributeData attribute)
	{
		var type = attribute.AttributeClass!;
		var name = type.Name.EndsWith("Attribute", StringComparison.Ordinal) ? type.Name[..^"Attribute".Length] : type.Name;
		parts.Add(new SigPart("[", SigKind.Text));
		parts.Add(new SigPart(name, SigKind.Type, type));
		var arguments = attribute.ConstructorArguments.Select(Constant).ToList();
		foreach (var (argumentName, value) in attribute.NamedArguments)
		{
			arguments.Add([new SigPart(argumentName, SigKind.Member), new SigPart(" = ", SigKind.Text), .. Constant(value)]);
		}

		if (arguments.Count > 0)
		{
			parts.Add(new SigPart("(", SigKind.Text));
			for (var i = 0; i < arguments.Count; i++)
			{
				if (i > 0) parts.Add(new SigPart(", ", SigKind.Text));
				parts.AddRange(arguments[i]);
			}

			parts.Add(new SigPart(")", SigKind.Text));
		}

		parts.Add(new SigPart("]", SigKind.Text));
	}

	private static List<SigPart> Constant(TypedConstant constant)
	{
		switch (constant.Kind)
		{
			case TypedConstantKind.Enum:
				return EnumConstant((INamedTypeSymbol)constant.Type!, constant.Value);
			case TypedConstantKind.Type:
				return [new SigPart("typeof", SigKind.Keyword), new SigPart("(", SigKind.Text), .. TypeReference((ITypeSymbol)constant.Value!), new SigPart(")", SigKind.Text)];
			case TypedConstantKind.Array:
				var items = new List<SigPart> { new("new", SigKind.Keyword), new("[] { ", SigKind.Text) };
				for (var i = 0; i < constant.Values.Length; i++)
				{
					if (i > 0) items.Add(new SigPart(", ", SigKind.Text));
					items.AddRange(Constant(constant.Values[i]));
				}

				items.Add(new SigPart(" }", SigKind.Text));

				return items;
			default:
				return constant.Value switch
				{
					null => [new SigPart("null", SigKind.Keyword)],
					bool b => [new SigPart(b ? "true" : "false", SigKind.Keyword)],
					string s => [new SigPart(SymbolDisplay.FormatLiteral(s, quote: true), SigKind.String)],
					char c => [new SigPart(SymbolDisplay.FormatLiteral(c, quote: true), SigKind.String)],
					IFormattable f => [new SigPart(f.ToString(null, CultureInfo.InvariantCulture), SigKind.Number)],
					var other => [new SigPart(other.ToString() ?? "", SigKind.Text)],
				};
		}
	}

	/// <summary>An enum value as C#: <c>AttributeTargets.Class | AttributeTargets.Struct</c> for flags.</summary>
	private static List<SigPart> EnumConstant(INamedTypeSymbol enumType, object? value)
	{
		var number = Convert.ToInt64(value, CultureInfo.InvariantCulture);
		var fields = enumType.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue)
			.Select(f => (Field: f, Value: Convert.ToInt64(f.ConstantValue, CultureInfo.InvariantCulture))).ToList();
		var names = fields.Where(f => f.Value == number).Take(1).ToList();
		if (names.Count == 0)
		{
			// Flags: the largest values that fit, shown in ascending order.
			var rest = number;
			foreach (var field in fields.Where(f => f.Value != 0).OrderByDescending(f => f.Value))
			{
				if ((rest & field.Value) != field.Value) continue;
				names.Add(field);
				rest &= ~field.Value;
			}

			if (rest != 0 || names.Count == 0)
			{
				return [new SigPart("(", SigKind.Text), .. TypeReference(enumType), new SigPart(")", SigKind.Text), new SigPart(number.ToString(CultureInfo.InvariantCulture), SigKind.Number)];
			}

			names.Reverse();
		}

		var parts = new List<SigPart>();
		for (var i = 0; i < names.Count; i++)
		{
			if (i > 0) parts.Add(new SigPart(" | ", SigKind.Text));
			parts.Add(new SigPart(enumType.Name, SigKind.Type, enumType));
			parts.Add(new SigPart(".", SigKind.Text));
			parts.Add(new SigPart(names[i].Field.Name, SigKind.Member, names[i].Field));
		}

		return parts;
	}

	private static List<SigPart> FromDisplay(IEnumerable<SymbolDisplayPart> display)
	{
		var parts = new List<SigPart>();
		foreach (var part in display)
		{
			var kind = part.Kind switch
			{
				SymbolDisplayPartKind.Keyword => SigKind.Keyword,
				SymbolDisplayPartKind.ClassName or SymbolDisplayPartKind.StructName or SymbolDisplayPartKind.InterfaceName or
					SymbolDisplayPartKind.EnumName or SymbolDisplayPartKind.DelegateName or SymbolDisplayPartKind.RecordClassName or
					SymbolDisplayPartKind.RecordStructName or SymbolDisplayPartKind.ErrorTypeName or SymbolDisplayPartKind.TypeParameterName => SigKind.Type,
				SymbolDisplayPartKind.MethodName or SymbolDisplayPartKind.PropertyName or SymbolDisplayPartKind.FieldName or
					SymbolDisplayPartKind.EventName or SymbolDisplayPartKind.EnumMemberName or SymbolDisplayPartKind.ExtensionMethodName or
					SymbolDisplayPartKind.ConstantName => SigKind.Member,
				SymbolDisplayPartKind.ParameterName => SigKind.Parameter,
				SymbolDisplayPartKind.NumericLiteral => SigKind.Number,
				SymbolDisplayPartKind.StringLiteral => SigKind.String,
				SymbolDisplayPartKind.LineBreak => SigKind.LineBreak,
				_ => SigKind.Text,
			};
			// Roslyn shows enum default values without their type (fileOptions = SequentialScan).
			if (part.Kind == SymbolDisplayPartKind.EnumMemberName && part.Symbol is IFieldSymbol { ContainingType: { } enumType } &&
				parts.LastOrDefault(p => p.Text != " ").Text == "=")
			{
				parts.Add(new SigPart(enumType.Name, SigKind.Type, enumType));
				parts.Add(new SigPart(".", SigKind.Text));
			}

			parts.Add(new SigPart(part.ToString(), kind, part.Symbol));
		}

		return parts;
	}
}
