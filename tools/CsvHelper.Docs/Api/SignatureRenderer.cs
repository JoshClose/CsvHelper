using System.Collections.Generic;
using System.Text;
using CsvHelper.Docs.Site;
using Microsoft.CodeAnalysis;
using static CsvHelper.Docs.Html.Html;

namespace CsvHelper.Docs.Api;

/// <summary>
/// Renders declarations as HTML with the highlight.js classes (so one theme colors them and the code samples), and
/// links type names to their API page or to Microsoft Learn.
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
					html.Append($"""<span class="hljs-keyword">{text}</span>""");
					break;
				case SigKind.Type:
					var url = TypeUrl(part.Symbol, page);
					html.Append(url is null
						? $"""<span class="hljs-type">{text}</span>"""
						: $"""<a class="hljs-type" href="{E(url)}">{text}</a>""");
					break;
				case SigKind.Member:
					html.Append($"""<span class="hljs-title function_">{text}</span>""");
					break;
				case SigKind.Parameter:
					html.Append($"""<span class="hljs-variable">{text}</span>""");
					break;
				case SigKind.Number:
					html.Append($"""<span class="hljs-number">{text}</span>""");
					break;
				case SigKind.String:
					html.Append($"""<span class="hljs-string">{text}</span>""");
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

	private string? TypeUrl(ISymbol? symbol, string page)
	{
		if (symbol is not INamedTypeSymbol type) return null;
		var id = type.OriginalDefinition.GetDocumentationCommentId();
		if (id is null) return null;
		if (api.TypesById.TryGetValue(id, out var own)) return SiteContext.Relative(page, SiteContext.ApiPath(own));

		return Links.Learn(id);
	}
}
