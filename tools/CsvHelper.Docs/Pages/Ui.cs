using System.Collections.Generic;
using System.Linq;
using CsvHelper.Docs.Site;
using static CsvHelper.Docs.Html.Html;

namespace CsvHelper.Docs.Pages;

/// <summary>Small pieces of markup that several page kinds use. The classes are defined in docs/assets/site.css.</summary>
internal sealed class Ui
{
	/// <summary>The typography classes for Markdown and doc comment content.</summary>
	public const string Prose = "prose prose-ink dark:prose-invert max-w-none";

	private readonly SiteContext site;

	public Ui(SiteContext site) => this.site = site;

	public string Icon(string name, string classes = "size-4") => site.Icons.Get(name, classes);

	/// <summary>A pill. Tones: brand, ink, warn, danger.</summary>
	public static string Badge(string text, string tone = "ink", string? title = null) =>
		$"""<span class="badge badge-{tone}"{(title is null ? "" : $" title=\"{E(title)}\"")}>{E(text)}</span>""";

	/// <summary>The title block of a page: a small label with an icon, the title, badges and a lead paragraph (HTML).</summary>
	public string Header(string eyebrowIcon, string eyebrow, string title, string? leadHtml, IEnumerable<string>? badges = null)
	{
		var badgeHtml = string.Concat(badges ?? []);

		return $"""
			<header class="page-header">
			  <p class="eyebrow">{Icon(eyebrowIcon, "size-3.5")}<span>{E(eyebrow)}</span></p>
			  <h1 class="page-title">{E(title)}</h1>
			  {(badgeHtml.Length == 0 ? "" : $"""<div class="badges mt-3">{badgeHtml}</div>""")}
			  {(string.IsNullOrWhiteSpace(leadHtml) ? "" : $"""<div class="lead">{leadHtml}</div>""")}
			</header>
			""";
	}

	/// <summary>A placeholder for lists that have nothing in them yet.</summary>
	public string Empty(string icon, string title, string textHtml) => $"""
		<div class="empty-state">
		  <div class="empty-icon">{Icon(icon, "size-6")}</div>
		  <p class="empty-title">{E(title)}</p>
		  <p class="empty-text">{textHtml}</p>
		</div>
		""";

	/// <summary>A link that opens outside the site, marked with an icon.</summary>
	public string External(string url, string text, string icon = "up-right-from-square") =>
		$"""<a href="{E(url)}" class="external-link">{E(text)}{Icon(icon, "size-3")}</a>""";

	/// <summary>The icon of an API type kind or search result kind.</summary>
	public static string KindIcon(string kind) => kind switch
	{
		"class" or "static class" => "cube",
		"record" or "record struct" => "box",
		"struct" or "ref struct" => "cubes",
		"enum" => "list",
		"delegate" => "code",
		"interface" => "plug",
		"attribute" => "tag",
		"exception" => "triangle-exclamation",
		"constructor" => "hammer",
		"property" => "sliders",
		"indexer" => "table-cells",
		"method" => "bolt",
		"field" => "hashtag",
		"value" => "list-ol",
		"event" => "bell",
		"operator" => "plus-minus",
		"guide" => "book-open",
		"section" => "hashtag",
		"release" => "code-branch",
		"migration" => "right-left",
		"diagnostic" => "triangle-exclamation",
		_ => "file-lines",
	};

	public static IEnumerable<string> SearchKinds => new[]
	{
		"class", "record", "struct", "enum", "delegate", "interface", "attribute", "exception", "constructor", "property", "indexer",
		"method", "field", "value", "event", "operator", "guide", "section", "release", "migration", "diagnostic", "page",
	}.Distinct();
}
