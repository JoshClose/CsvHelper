using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsvHelper.Docs.Releases;

/// <summary>One parameter of a <see cref="MemberShape"/>.</summary>
/// <param name="Modifiers"><c>ref</c>, <c>out</c>, <c>in</c>, <c>params</c>, <c>this</c>, or empty.</param>
/// <param name="Type">The parameter's type, as written.</param>
/// <param name="Name">The parameter's name.</param>
/// <param name="Default">The default value, as written, or null.</param>
internal sealed record MemberParameter(string Modifiers, string Type, string Name, string? Default);

/// <summary>
/// The parts of a member declaration (a snapshot's signature string, parsed with Roslyn), so that two declarations of the
/// same member can be compared part by part: modifiers, return type, parameters, type parameters and constraints.
/// </summary>
internal sealed class MemberShape
{
	/// <summary>The modifiers that only matter to derived classes.</summary>
	public static readonly IReadOnlySet<string> InheritanceModifiers = new HashSet<string> { "virtual", "override", "abstract", "sealed", "new" };

	/// <summary>method, constructor, property, indexer, operator, event or field.</summary>
	public required string Kind { get; init; }

	/// <summary>Every modifier: <c>public</c>, <c>virtual</c>, <c>static</c> and so on.</summary>
	public required IReadOnlySet<string> Modifiers { get; init; }

	/// <summary>The return type (or the type of a property, field or event); empty for constructors.</summary>
	public required string ReturnType { get; init; }

	public required IReadOnlyList<MemberParameter> Parameters { get; init; }

	/// <summary>The type parameter list, such as <c>&lt;T&gt;</c>; empty when there is none.</summary>
	public required string TypeParameters { get; init; }

	/// <summary>The constraint clauses, such as <c>where T : class</c>; empty when there are none.</summary>
	public required string Constraints { get; init; }

	/// <summary>The accessors of a property, indexer or event, such as <c>{ get; set; }</c>; empty for other members.</summary>
	public required string Accessors { get; init; }

	/// <summary>Whether the member can be overridden: virtual, abstract or override, and not sealed.</summary>
	public bool Overridable => (Has("virtual") || Has("abstract") || Has("override")) && !Has("sealed");

	/// <summary>The modifiers other than <see cref="InheritanceModifiers"/>.</summary>
	public IEnumerable<string> CoreModifiers => Modifiers.Where(m => !InheritanceModifiers.Contains(m));

	/// <summary>Whether the member has a modifier.</summary>
	/// <param name="modifier">The modifier: <c>virtual</c>, <c>static</c>.</param>
	/// <returns>Whether it has it.</returns>
	public bool Has(string modifier) => Modifiers.Contains(modifier);

	/// <summary>Parses a signature as written in an API snapshot (<c>public virtual bool Read()</c>).</summary>
	/// <param name="signature">The signature, without a body or a semicolon.</param>
	/// <returns>The shape, or null when the signature does not parse as a member declaration.</returns>
	public static MemberShape? TryParse(string signature)
	{
		var text = signature.TrimEnd();
		if (!text.EndsWith('}'))
		{
			text += ";";
		}

		if (SyntaxFactory.ParseMemberDeclaration(text) is not { } member || member.ContainsDiagnostics)
		{
			return null;
		}

		var modifiers = member.Modifiers.Select(m => m.Text).ToHashSet();

		return member switch
		{
			MethodDeclarationSyntax method => Create("method", modifiers, method.ReturnType, method.ParameterList.Parameters, method.TypeParameterList, method.ConstraintClauses, null),
			ConstructorDeclarationSyntax constructor => Create("constructor", modifiers, null, constructor.ParameterList.Parameters, null, default, null),
			PropertyDeclarationSyntax property => Create("property", modifiers, property.Type, default, null, default, property.AccessorList),
			IndexerDeclarationSyntax indexer => Create("indexer", modifiers, indexer.Type, indexer.ParameterList.Parameters, null, default, indexer.AccessorList),
			OperatorDeclarationSyntax op => Create("operator", modifiers, op.ReturnType, op.ParameterList.Parameters, null, default, null),
			ConversionOperatorDeclarationSyntax conversion => Create("operator", modifiers, conversion.Type, conversion.ParameterList.Parameters, null, default, null),
			EventDeclarationSyntax @event => Create("event", modifiers, @event.Type, default, null, default, @event.AccessorList),
			EventFieldDeclarationSyntax eventField => Create("event", modifiers, eventField.Declaration.Type, default, null, default, null),
			FieldDeclarationSyntax field => Create("field", modifiers, field.Declaration.Type, default, null, default, null),
			_ => null,
		};
	}

	/// <summary>The shape of a parsed member, with every part normalized (whitespace, as Roslyn formats it).</summary>
	/// <param name="kind">method, constructor, property, indexer, operator, event or field.</param>
	/// <param name="modifiers">The modifiers.</param>
	/// <param name="returnType">The return type or the member's type; null for constructors.</param>
	/// <param name="parameters">The parameters.</param>
	/// <param name="typeParameters">The type parameter list, or null.</param>
	/// <param name="constraints">The constraint clauses.</param>
	/// <param name="accessors">The accessor list, or null.</param>
	/// <returns>The shape.</returns>
	private static MemberShape Create(string kind, HashSet<string> modifiers, TypeSyntax? returnType, SeparatedSyntaxList<ParameterSyntax> parameters,
		TypeParameterListSyntax? typeParameters, SyntaxList<TypeParameterConstraintClauseSyntax> constraints, AccessorListSyntax? accessors)
	{
		var list = parameters
			.Select(p => new MemberParameter(string.Join(" ", p.Modifiers.Select(m => m.Text)), Normalize(p.Type), p.Identifier.Text, p.Default is { } d ? Normalize(d.Value) : null))
			.ToList();

		return new MemberShape
		{
			Kind = kind,
			Modifiers = modifiers,
			ReturnType = Normalize(returnType),
			Parameters = list,
			TypeParameters = Normalize(typeParameters),
			Constraints = string.Join(" ", constraints.Select(c => Normalize(c))),
			Accessors = Normalize(accessors),
		};
	}

	/// <summary>A syntax node as text with normalized whitespace, so that formatting differences are not changes.</summary>
	/// <param name="node">The node, or null.</param>
	/// <returns>The text; empty for null.</returns>
	private static string Normalize(SyntaxNode? node) => node?.NormalizeWhitespace().ToString() ?? "";
}
