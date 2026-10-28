using System.Collections.Generic;
using System.Text;
using CsvHelper.Docs.Highlighting;
using CsvHelper.Docs.Site;
using Microsoft.CodeAnalysis;
using static CsvHelper.Docs.Html.Html;

namespace CsvHelper.Docs.Api;

/// <summary>
/// Renders declarations as HTML with the classes of <see cref="Highlighter"/> (so they are colored as the code samples
/// are), and links type names to their API page or to Microsoft Learn.
/// </summary>
internal sealed class SignatureRenderer
{
	private readonly ApiSet api;

	public SignatureRenderer(ApiSet api) => this.api = api;

	public string Render(IEnumerable<SigPart> parts, string page)
	{
		var html = new StringBuilder();
		foreach (var part in parts)
		{
			var text = E(part.Text);
			switch (part.Kind)
			{
				case SigKind.Keyword:
					html.Append(Span(TokenKind.Keyword, text));
					break;
				case SigKind.Type:
					var url = TypeUrl(part.Symbol, page);
					html.Append(url is null
						? Span(TokenKind.Type, text)
						: $"""<a class="{Highlighter.Class(TokenKind.Type)}" href="{E(url)}">{text}</a>""");
					break;
				case SigKind.Member:
					html.Append(Span(MemberKind(part.Symbol), text));
					break;
				case SigKind.Parameter:
					html.Append(Span(TokenKind.Variable, text));
					break;
				case SigKind.Number:
					html.Append(Span(TokenKind.Number, text));
					break;
				case SigKind.String:
					html.Append(Span(TokenKind.String, text));
					break;
				case SigKind.LineBreak:
					html.Append('\n');
					break;
				default:
					html.Append(text);
					break;
			}
		}

		return html.ToString();
	}

	/// <summary>A colored token.</summary>
	/// <param name="kind">Its color.</param>
	/// <param name="html">Its HTML.</param>
	/// <returns>The span.</returns>
	private static string Span(TokenKind kind, string html) => $"""<span class="{Highlighter.Class(kind)}">{html}</span>""";

	/// <summary>The color of a member's name, as code colors it: methods, constants and enum members, and the others (properties, fields, events).</summary>
	/// <param name="symbol">The member; null for the property of an attribute's named argument.</param>
	/// <returns>The color.</returns>
	private static TokenKind MemberKind(ISymbol? symbol) => symbol switch
	{
		IMethodSymbol => TokenKind.Method,
		IFieldSymbol { IsConst: true } => TokenKind.Constant,
		_ => TokenKind.Variable,
	};

	private string? TypeUrl(ISymbol? symbol, string page)
	{
		if (symbol is not INamedTypeSymbol type) return null;
		var id = type.OriginalDefinition.GetDocumentationCommentId();
		if (id is null) return null;
		if (api.TypesById.TryGetValue(id, out var own)) return SiteContext.Relative(page, SiteContext.ApiPath(own));

		return Links.Learn(id);
	}
}
