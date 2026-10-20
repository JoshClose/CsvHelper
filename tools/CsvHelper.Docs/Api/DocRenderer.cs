using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using CsvHelper.Docs.Site;
using Microsoft.CodeAnalysis;
using static CsvHelper.Docs.Html.Html;

namespace CsvHelper.Docs.Api;

/// <summary>Where doc text is rendered: the page (for relative links) and the type (crefs to its members drop the type name).</summary>
internal readonly record struct DocScope(string Page, ApiType? Type);

/// <summary>The sections of a doc comment.</summary>
internal sealed class MemberDoc
{
	public XElement? Summary { get; private init; }
	public XElement? Remarks { get; private init; }
	public XElement? Returns { get; private init; }
	public XElement? Value { get; private init; }
	public List<XElement> Examples { get; } = [];
	public List<(string Name, XElement Text)> Parameters { get; } = [];
	public List<(string Name, XElement Text)> TypeParameters { get; } = [];
	public List<(string Cref, XElement Text)> Exceptions { get; } = [];
	public List<XElement> SeeAlso { get; } = [];

	public static MemberDoc Parse(string? xml)
	{
		if (string.IsNullOrWhiteSpace(xml)) return new MemberDoc();
		XElement root;
		try
		{
			root = XElement.Parse(xml, LoadOptions.PreserveWhitespace);
		}
		catch (XmlException ex)
		{
			Log.Warn($"Malformed doc comment: {ex.Message}");

			return new MemberDoc();
		}

		var doc = new MemberDoc
		{
			Summary = root.Element("summary"),
			Remarks = root.Element("remarks"),
			Returns = root.Element("returns"),
			Value = root.Element("value"),
		};
		doc.Examples.AddRange(root.Elements("example"));
		doc.SeeAlso.AddRange(root.Elements("seealso"));
		foreach (var element in root.Elements("param")) doc.Parameters.Add(((string?)element.Attribute("name") ?? "", element));
		foreach (var element in root.Elements("typeparam")) doc.TypeParameters.Add(((string?)element.Attribute("name") ?? "", element));
		foreach (var element in root.Elements("exception")) doc.Exceptions.Add(((string?)element.Attribute("cref") ?? "", element));

		return doc;
	}
}

/// <summary>Renders doc comment XML as HTML, linking crefs to API pages or to Microsoft Learn.</summary>
internal sealed partial class DocRenderer
{
	private readonly ApiSet api;

	public DocRenderer(ApiSet api) => this.api = api;

	/// <summary>Block content (summary, remarks, returns, a parameter): paragraphs, lists and code.</summary>
	public string Block(XElement? element, DocScope scope)
	{
		if (element is null) return "";
		var html = new StringBuilder();
		var paragraph = new StringBuilder();
		foreach (var node in element.Nodes())
		{
			if (node is XText text)
			{
				paragraph.Append(E(Collapse(text.Value)));
				continue;
			}

			if (node is not XElement child) continue;
			switch (child.Name.LocalName)
			{
				case "para":
					Flush(html, paragraph);
					html.Append(Block(child, scope));
					break;
				case "list":
					Flush(html, paragraph);
					html.Append(List(child, scope));
					break;
				case "code":
					Flush(html, paragraph);
					html.Append($"""<pre><code class="language-{(string?)child.Attribute("language") ?? "csharp"}">{E(Dedent(child.Value))}</code></pre>""");
					break;
				default:
					paragraph.Append(InlineElement(child, scope));
					break;
			}
		}

		Flush(html, paragraph);

		return html.ToString();
	}

	/// <summary>Content for one line, such as a table cell: a single paragraph loses its <c>&lt;p&gt;</c>.</summary>
	public string Inline(XElement? element, DocScope scope)
	{
		var html = Block(element, scope);
		if (html.StartsWith("<p>", StringComparison.Ordinal) && html.IndexOf("<p>", 3, StringComparison.Ordinal) < 0 &&
			html.EndsWith("</p>", StringComparison.Ordinal))
		{
			return html[3..^4];
		}

		return html;
	}

	/// <summary>The text of doc content, for search and short summaries.</summary>
	public string Plain(XElement? element) => element is null ? "" : Text(Block(element, new DocScope("", null)));

	/// <summary>A link for a cref: an API page, Microsoft Learn, or plain code when neither applies.</summary>
	public string Cref(string cref, string? text, DocScope scope)
	{
		if (cref.StartsWith("!:", StringComparison.Ordinal)) return $"<code>{E(text ?? cref[2..])}</code>";
		if (api.TypesById.TryGetValue(cref, out var type))
			return $"""<a href="{E(SiteContext.Relative(scope.Page, SiteContext.ApiPath(type)))}">{E(text ?? type.Name)}</a>""";
		if (api.MembersById.TryGetValue(cref, out var member))
			return $"""<a href="{E(SiteContext.Relative(scope.Page, SiteContext.ApiPath(member)))}">{E(text ?? MemberText(member, scope))}</a>""";
		var symbol = DocumentationCommentId.GetFirstSymbolForDeclarationId(cref, api.PrimaryCompilation);
		var display = text ?? (symbol is null ? cref[(cref.IndexOf(':') + 1)..] : SymbolText(symbol, scope));
		// A member of the library that is not documented (hidden from IntelliSense) gets no link.
		var own = cref.Contains(":CsvHelper.", StringComparison.Ordinal);
		if (own && (symbol is null || !Attributes.IsHidden(symbol))) Log.Warn($"{scope.Page}: the cref {cref} names nothing in the documented API.");
		var url = own ? null : Links.Learn(cref);

		return url is null ? $"<code>{E(display)}</code>" : $"""<a href="{E(url)}" class="external">{E(display)}</a>""";
	}

	/// <summary>How a cref to a member reads: <c>GetString(int)</c> on its own type's page, else <c>CsvReader.GetString(int)</c>.</summary>
	public static string MemberText(ApiMember member, DocScope scope)
	{
		var overloaded = member.Type.Members.Count(m => m.Name == member.Name) > 1;
		var name = overloaded ? member.ShortName : member.Name;

		return scope.Type == member.Type ? name : $"{member.Type.Name}.{name}";
	}

	private static string SymbolText(ISymbol symbol, DocScope scope)
	{
		if (symbol is INamedTypeSymbol type) return type.ToDisplayString(Signatures.TypeName);
		var container = symbol.ContainingType;
		var overloaded = container?.GetMembers(symbol.Name).Length > 1;
		var name = overloaded ? symbol.ToDisplayString(Signatures.Short) : symbol.Name;
		if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor }) name = container!.Name + name[name.IndexOf('(')..];

		return container is null ? name : $"{container.ToDisplayString(Signatures.TypeName)}.{name}";
	}

	private string InlineElement(XElement element, DocScope scope)
	{
		switch (element.Name.LocalName)
		{
			case "see":
			case "seealso":
				if (element.Attribute("cref") is { } cref) return Cref(cref.Value, NonEmpty(element.Value), scope);
				if (element.Attribute("langword") is { } word) return $"<code>{E(word.Value)}</code>";
				if (element.Attribute("href") is { } href) return $"""<a href="{E(href.Value)}" class="external">{E(NonEmpty(element.Value) ?? href.Value)}</a>""";

				return InlineChildren(element, scope);
			case "paramref":
			case "typeparamref":
				return $"""<code class="param">{E((string?)element.Attribute("name"))}</code>""";
			case "c":
				return $"<code>{E(Collapse(element.Value).Trim())}</code>";
			case "b":
			case "strong":
				return $"<strong>{InlineChildren(element, scope)}</strong>";
			case "i":
			case "em":
				return $"<em>{InlineChildren(element, scope)}</em>";
			case "br":
				return "<br>";
			case "a" when element.Attribute("href") is { } link:
				return $"""<a href="{E(link.Value)}" class="external">{InlineChildren(element, scope)}</a>""";
			default:
				return InlineChildren(element, scope);
		}
	}

	private string InlineChildren(XElement element, DocScope scope)
	{
		var html = new StringBuilder();
		foreach (var node in element.Nodes())
		{
			if (node is XText text) html.Append(E(Collapse(text.Value)));
			else if (node is XElement child) html.Append(InlineElement(child, scope));
		}

		return html.ToString();
	}

	private string List(XElement list, DocScope scope)
	{
		var type = (string?)list.Attribute("type") ?? "bullet";
		var items = list.Elements("item").ToList();
		if (type == "table")
		{
			var html = new StringBuilder("""<div class="table-wrap"><table>""");
			if (list.Element("listheader") is { } header)
				html.Append($"<thead><tr><th>{Inline(header.Element("term"), scope)}</th><th>{Inline(header.Element("description"), scope)}</th></tr></thead>");
			html.Append("<tbody>");
			foreach (var item in items) html.Append($"<tr><td>{Inline(item.Element("term"), scope)}</td><td>{Inline(item.Element("description") ?? item, scope)}</td></tr>");

			return html.Append("</tbody></table></div>").ToString();
		}

		var tag = type == "number" ? "ol" : "ul";
		var body = new StringBuilder($"<{tag}>");
		foreach (var item in items)
		{
			var term = item.Element("term");
			var description = item.Element("description") ?? (term is null ? item : null);
			body.Append("<li>");
			if (term is not null) body.Append($"<strong>{Inline(term, scope)}</strong>{(description is null ? "" : " – ")}");
			if (description is not null) body.Append(Inline(description, scope));
			body.Append("</li>");
		}

		return body.Append($"</{tag}>").ToString();
	}

	private static void Flush(StringBuilder html, StringBuilder paragraph)
	{
		var text = paragraph.ToString().Trim();
		if (text.Length > 0) html.Append("<p>").Append(text).Append("</p>");
		paragraph.Clear();
	}

	private static string? NonEmpty(string text) => string.IsNullOrWhiteSpace(text) ? null : Collapse(text).Trim();

	private static string Collapse(string text) => WhitespaceRun().Replace(text, " ");

	/// <summary>Removes the indentation that a <c>&lt;code&gt;</c> block shares with the comment around it.</summary>
	private static string Dedent(string code)
	{
		var lines = code.Replace("\r\n", "\n").Split('\n').ToList();
		while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0])) lines.RemoveAt(0);
		while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1])) lines.RemoveAt(lines.Count - 1);
		var indent = lines.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();

		return string.Join('\n', lines.Select(l => l.Length >= indent ? l[indent..] : l.TrimStart()));
	}

	[GeneratedRegex(@"\s+")]
	private static partial Regex WhitespaceRun();
}
