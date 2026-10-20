using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsvHelper.Docs.Highlighting;

/// <summary>
/// Colors C# as VS Code does with semantic highlighting. Roslyn parses the code and binds its names against the documented
/// libraries and the framework, so a name is colored as what it is: a type, a method, a property. A name that does not
/// bind (a type declared in another code block, an API of an old version) is colored by where it is, as VS Code's grammar
/// colors it: a type where only a type can be, a method when it is called, a variable otherwise.
/// </summary>
internal static partial class CSharpClassifier
{
	/// <summary>The namespaces every code block can use, as in an SDK project with implicit usings.</summary>
	private static readonly string[] ImplicitNamespaces =
	[
		"System", "System.Collections.Generic", "System.Globalization", "System.IO", "System.Linq", "System.Text",
		"System.Threading", "System.Threading.Tasks",
	];

	private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

	private static CSharpCompilation? compilation;

	/// <summary>Gives the code the libraries to bind names against.</summary>
	/// <param name="references">The documented libraries and the framework's reference assemblies.</param>
	/// <param name="namespaces">The namespaces the code can use without a using directive, besides the framework's usual ones.</param>
	public static void UseReferences(IEnumerable<MetadataReference> references, IEnumerable<string> namespaces)
	{
		var usings = string.Join("\n", ImplicitNamespaces.Concat(namespaces).Distinct().Select(n => $"global using {n};"));
		var options = new CSharpCompilationOptions(OutputKind.ConsoleApplication, allowUnsafe: true, nullableContextOptions: NullableContextOptions.Enable);
		compilation = CSharpCompilation.Create("Highlighting", [CSharpSyntaxTree.ParseText(usings, ParseOptions)], references, options);
	}

	/// <summary>The colored ranges of C# code.</summary>
	/// <param name="code">The code: a whole file, statements, or members.</param>
	/// <returns>The ranges, in order.</returns>
	public static List<CodeSpan> Classify(string code)
	{
		var tree = CSharpSyntaxTree.ParseText(code, ParseOptions);
		// Each block is a compilation of its own, so blocks do not see each other's types. The compilations share the
		// references, which Roslyn loads once.
		var model = compilation?.AddSyntaxTrees(tree).GetSemanticModel(tree, ignoreAccessibility: true);
		var spans = new List<CodeSpan>();
		foreach (var token in tree.GetRoot().DescendantTokens())
		{
			AddTrivia(token.LeadingTrivia, spans);
			AddToken(token, model, spans);
			AddTrivia(token.TrailingTrivia, spans);
		}

		return spans;
	}

	/// <summary>Adds the comments, directives and skipped tokens of trivia.</summary>
	/// <param name="trivia">The trivia.</param>
	/// <param name="spans">The ranges to add to.</param>
	private static void AddTrivia(SyntaxTriviaList trivia, List<CodeSpan> spans)
	{
		foreach (var item in trivia)
		{
			switch (item.Kind())
			{
				case SyntaxKind.SingleLineCommentTrivia:
				case SyntaxKind.MultiLineCommentTrivia:
				case SyntaxKind.SingleLineDocumentationCommentTrivia:
				case SyntaxKind.MultiLineDocumentationCommentTrivia:
					spans.Add(new CodeSpan(item.Span.Start, item.Span.Length, TokenKind.Comment));
					break;
				case SyntaxKind.SkippedTokensTrivia:
					// Code the parser could not place (a "..." in an example): its keywords and literals still get colors.
					foreach (var token in item.GetStructure()!.DescendantTokens())
					{
						AddToken(token, null, spans);
					}

					break;
				default:
					if (item.IsDirective)
					{
						AddDirective(item.GetStructure()!, spans);
					}

					break;
			}
		}
	}

	/// <summary>Adds a preprocessor directive: <c>#</c> and its keywords, and its comments.</summary>
	/// <param name="directive">The directive.</param>
	/// <param name="spans">The ranges to add to.</param>
	private static void AddDirective(SyntaxNode directive, List<CodeSpan> spans)
	{
		foreach (var token in directive.DescendantTokens())
		{
			AddTrivia(token.LeadingTrivia, spans);
			var kind = token.Kind();
			if (kind == SyntaxKind.HashToken || SyntaxFacts.IsPreprocessorKeyword(kind) || SyntaxFacts.IsKeywordKind(kind))
			{
				spans.Add(new CodeSpan(token.Span.Start, token.Span.Length, TokenKind.Keyword));
			}

			AddTrivia(token.TrailingTrivia, spans);
		}
	}

	/// <summary>Adds a token: a keyword, a literal or a name. Punctuation and operators have no color.</summary>
	/// <param name="token">The token.</param>
	/// <param name="model">The semantic model to bind names with; null to color them by where they are.</param>
	/// <param name="spans">The ranges to add to.</param>
	private static void AddToken(SyntaxToken token, SemanticModel? model, List<CodeSpan> spans)
	{
		var kind = token.Kind();
		TokenKind? color = null;
		if (kind == SyntaxKind.IdentifierToken)
		{
			color = Identifier(token, model);
		}
		else if (kind == SyntaxKind.NumericLiteralToken)
		{
			color = TokenKind.Number;
		}
		else if (IsString(kind))
		{
			AddString(token, spans);
		}
		else if (SyntaxFacts.IsKeywordKind(kind))
		{
			color = IsControl(token) ? TokenKind.Control : TokenKind.Keyword;
		}

		if (color is { } value)
		{
			spans.Add(new CodeSpan(token.Span.Start, token.Span.Length, value));
		}
	}

	/// <summary>Whether a token is (part of) a string or character literal.</summary>
	/// <param name="kind">The token's kind.</param>
	/// <returns>True for literals and the text, start and end of interpolated strings.</returns>
	private static bool IsString(SyntaxKind kind) => kind is
		SyntaxKind.StringLiteralToken or SyntaxKind.Utf8StringLiteralToken or SyntaxKind.CharacterLiteralToken or
		SyntaxKind.SingleLineRawStringLiteralToken or SyntaxKind.Utf8SingleLineRawStringLiteralToken or
		SyntaxKind.MultiLineRawStringLiteralToken or SyntaxKind.Utf8MultiLineRawStringLiteralToken or
		SyntaxKind.InterpolatedStringStartToken or SyntaxKind.InterpolatedVerbatimStringStartToken or
		SyntaxKind.InterpolatedSingleLineRawStringStartToken or SyntaxKind.InterpolatedMultiLineRawStringStartToken or
		SyntaxKind.InterpolatedStringTextToken or SyntaxKind.InterpolatedStringEndToken or SyntaxKind.InterpolatedRawStringEndToken;

	/// <summary>Adds a string token, with its escape sequences colored apart.</summary>
	/// <param name="token">The token.</param>
	/// <param name="spans">The ranges to add to.</param>
	private static void AddString(SyntaxToken token, List<CodeSpan> spans)
	{
		var text = token.Text;
		var (escape, first, last) = Escapes(token);
		var position = 0;
		if (escape is not null && last > first)
		{
			foreach (Match match in escape.Matches(text[first..last]))
			{
				var start = first + match.Index;
				spans.Add(new CodeSpan(token.SpanStart + position, start - position, TokenKind.String));
				spans.Add(new CodeSpan(token.SpanStart + start, match.Length, TokenKind.Escape));
				position = start + match.Length;
			}
		}

		spans.Add(new CodeSpan(token.SpanStart + position, text.Length - position, TokenKind.String));
	}

	/// <summary>The escape sequences of a string token, and where its content is (inside the quotes).</summary>
	/// <param name="token">The token.</param>
	/// <returns>The pattern of its escapes (null for none) and the range of its text to look in.</returns>
	private static (Regex? Escape, int First, int Last) Escapes(SyntaxToken token)
	{
		var text = token.Text;
		switch (token.Kind())
		{
			case SyntaxKind.StringLiteralToken:
			case SyntaxKind.Utf8StringLiteralToken:
				var verbatim = text.StartsWith('@');
				var suffix = token.IsKind(SyntaxKind.Utf8StringLiteralToken) ? 3 : 1;

				return (verbatim ? VerbatimEscape() : RegularEscape(), verbatim ? 2 : 1, text.Length - suffix);
			case SyntaxKind.CharacterLiteralToken:
				return (RegularEscape(), 1, text.Length - 1);
			case SyntaxKind.InterpolatedStringTextToken:
				var start = token.Parent?.FirstAncestorOrSelf<InterpolatedStringExpressionSyntax>()?.StringStartToken.Kind();
				if (token.Parent is not InterpolatedStringTextSyntax)
				{
					// The format of an interpolation ({total:N2}).
					return (null, 0, 0);
				}

				return start switch
				{
					SyntaxKind.InterpolatedStringStartToken => (InterpolatedEscape(), 0, text.Length),
					SyntaxKind.InterpolatedVerbatimStringStartToken => (InterpolatedVerbatimEscape(), 0, text.Length),
					_ => (null, 0, 0),
				};
			default:
				return (null, 0, 0);
		}
	}

	/// <summary>Whether a keyword changes the flow (VS Code colors these apart).</summary>
	/// <param name="token">The keyword.</param>
	/// <returns>True for if, else, for, foreach, while, do, switch, case, try, catch, finally, goto, break, continue, return, throw, yield, and default, in and when where they are part of one of these.</returns>
	private static bool IsControl(SyntaxToken token) => token.Kind() switch
	{
		SyntaxKind.IfKeyword or SyntaxKind.ElseKeyword or SyntaxKind.WhileKeyword or SyntaxKind.ForKeyword or
			SyntaxKind.ForEachKeyword or SyntaxKind.DoKeyword or SyntaxKind.SwitchKeyword or SyntaxKind.CaseKeyword or
			SyntaxKind.TryKeyword or SyntaxKind.CatchKeyword or SyntaxKind.FinallyKeyword or SyntaxKind.GotoKeyword or
			SyntaxKind.BreakKeyword or SyntaxKind.ContinueKeyword or SyntaxKind.ReturnKeyword or SyntaxKind.ThrowKeyword or
			SyntaxKind.YieldKeyword => true,
		SyntaxKind.DefaultKeyword => token.Parent is DefaultSwitchLabelSyntax or GotoStatementSyntax,
		SyntaxKind.InKeyword => token.Parent is CommonForEachStatementSyntax,
		SyntaxKind.WhenKeyword => token.Parent is WhenClauseSyntax or CatchFilterClauseSyntax,
		_ => false,
	};

	/// <summary>The color of an identifier: by what it declares, or by what it names.</summary>
	/// <param name="token">The identifier.</param>
	/// <param name="model">The semantic model to bind names with; null to color them by where they are.</param>
	/// <returns>The color; null for none (labels).</returns>
	private static TokenKind? Identifier(SyntaxToken token, SemanticModel? model) => token.Parent switch
	{
		BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or TypeParameterSyntax or ConstructorDeclarationSyntax or
			DestructorDeclarationSyntax => TokenKind.Type,
		MethodDeclarationSyntax or LocalFunctionStatementSyntax => TokenKind.Method,
		VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax field } when field.Modifiers.Any(SyntaxKind.ConstKeyword) => TokenKind.Constant,
		VariableDeclaratorSyntax { Parent.Parent: LocalDeclarationStatementSyntax { IsConst: true } } => TokenKind.Constant,
		EnumMemberDeclarationSyntax => TokenKind.Constant,
		PropertyDeclarationSyntax or EventDeclarationSyntax or VariableDeclaratorSyntax or ParameterSyntax or
			SingleVariableDesignationSyntax or CommonForEachStatementSyntax or CatchDeclarationSyntax or QueryClauseSyntax or
			QueryContinuationSyntax or JoinIntoClauseSyntax or TupleElementSyntax => TokenKind.Variable,
		SimpleNameSyntax name => Name(name, model),
		_ => null,
	};

	/// <summary>The color of a name in code: what it binds to, or where it is when it does not bind.</summary>
	/// <param name="name">The name.</param>
	/// <param name="model">The semantic model to bind it with; null to color it by where it is.</param>
	/// <returns>The color.</returns>
	private static TokenKind Name(SimpleNameSyntax name, SemanticModel? model)
	{
		var typeContext = SyntaxFacts.IsInNamespaceOrTypeContext(name);
		if (typeContext && (name.IsVar || name.IsNint || name.IsNuint || name.IsUnmanaged || name.IsNotNull || name.Identifier.ValueText == "dynamic"))
		{
			return TokenKind.Keyword;
		}

		var info = model?.GetSymbolInfo(name);
		var symbol = info?.Symbol ?? info?.CandidateSymbols.FirstOrDefault();
		if (symbol is null && name.Identifier.ValueText == "nameof" && IsCalled(name))
		{
			return TokenKind.Keyword;
		}

		if (symbol is not null && FromSymbol(symbol) is { } bound)
		{
			return bound;
		}

		if (typeContext || name.Parent is AttributeSyntax or NameEqualsSyntax { Parent: UsingDirectiveSyntax })
		{
			return TokenKind.Type;
		}

		if (IsCalled(name))
		{
			return TokenKind.Method;
		}

		// A capitalized name whose member is used (Status.Open, Order.Parse) is a type: locals and parameters are camelCase.
		if (name is IdentifierNameSyntax && name.Parent is MemberAccessExpressionSyntax { Expression: var target } && target == name &&
			char.IsUpper(name.Identifier.ValueText[0]))
		{
			return TokenKind.Type;
		}

		if (name is GenericNameSyntax)
		{
			// Generic and not called: a type whose member is used (List<int>.Empty), or a method group.
			return name.Parent is MemberAccessExpressionSyntax access && access.Expression == name ? TokenKind.Type : TokenKind.Method;
		}

		return TokenKind.Variable;
	}

	/// <summary>The color of what a name binds to.</summary>
	/// <param name="symbol">The symbol.</param>
	/// <returns>The color; null for symbols that have none (labels).</returns>
	private static TokenKind? FromSymbol(ISymbol symbol) => symbol switch
	{
		IAliasSymbol alias => FromSymbol(alias.Target),
		IDynamicTypeSymbol => TokenKind.Keyword,
		INamespaceOrTypeSymbol => TokenKind.Type,
		IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor or MethodKind.Destructor } => TokenKind.Type,
		IMethodSymbol => TokenKind.Method,
		IFieldSymbol { IsConst: true } or ILocalSymbol { IsConst: true } => TokenKind.Constant,
		IPropertySymbol or IFieldSymbol or ILocalSymbol or IParameterSymbol or IEventSymbol or IRangeVariableSymbol or IDiscardSymbol => TokenKind.Variable,
		_ => null,
	};

	/// <summary>Whether a name is called: <c>Name()</c>, <c>x.Name()</c>, <c>x?.Name()</c>.</summary>
	/// <param name="name">The name.</param>
	/// <returns>True when it is the method of an invocation.</returns>
	private static bool IsCalled(SimpleNameSyntax name)
	{
		ExpressionSyntax expression = name.Parent switch
		{
			MemberAccessExpressionSyntax access when access.Name == name => access,
			MemberBindingExpressionSyntax binding => binding,
			_ => name,
		};

		return expression.Parent is InvocationExpressionSyntax invocation && invocation.Expression == expression;
	}

	/// <summary>The escape sequences of regular strings and characters: <c>\n</c>, <c>\"</c>, <c>\u00E9</c>.</summary>
	[GeneratedRegex(@"\\(?:x[0-9A-Fa-f]{1,4}|u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8}|.)", RegexOptions.Singleline)]
	private static partial Regex RegularEscape();

	/// <summary>The escape sequence of verbatim strings: <c>""</c>.</summary>
	[GeneratedRegex("\"\"")]
	private static partial Regex VerbatimEscape();

	/// <summary>The escape sequences of interpolated strings: those of regular strings, and <c>{{</c> and <c>}}</c>.</summary>
	[GeneratedRegex(@"\\(?:x[0-9A-Fa-f]{1,4}|u[0-9A-Fa-f]{4}|U[0-9A-Fa-f]{8}|.)|\{\{|\}\}", RegexOptions.Singleline)]
	private static partial Regex InterpolatedEscape();

	/// <summary>The escape sequences of interpolated verbatim strings: <c>""</c>, <c>{{</c> and <c>}}</c>.</summary>
	[GeneratedRegex("\"\"|\\{\\{|\\}\\}")]
	private static partial Regex InterpolatedVerbatimEscape();
}
