using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using CsvHelper.Docs.Api;
using CsvHelper.Docs.Content;
using CsvHelper.Docs.Site;
using Microsoft.CodeAnalysis;
using static CsvHelper.Docs.Html.Html;

namespace CsvHelper.Docs.Pages;

/// <summary>The API reference: an overview of the types, and a page per type with all its members.</summary>
internal sealed class ApiPages
{
	private static readonly (ApiMemberKind Kind, string Title)[] Sections =
	[
		(ApiMemberKind.Constructor, "Constructors"),
		(ApiMemberKind.Property, "Properties"),
		(ApiMemberKind.Indexer, "Indexers"),
		(ApiMemberKind.Method, "Methods"),
		(ApiMemberKind.Operator, "Operators"),
		(ApiMemberKind.Event, "Events"),
		(ApiMemberKind.Field, "Fields"),
	];

	private readonly SiteContext site;
	private readonly DocRenderer docs;
	private readonly SignatureRenderer signatures;
	private readonly Ui ui;

	public ApiPages(SiteContext site)
	{
		this.site = site;
		docs = new DocRenderer(site.Api);
		signatures = new SignatureRenderer(site.Api);
		ui = new Ui(site);
	}

	public IEnumerable<Page> Build()
	{
		yield return Index();
		foreach (var type in site.Api.Types) yield return TypePage(type);
	}

	private Page Index()
	{
		const string Path = "api/index.html";
		var body = new StringBuilder();
		var frameworks = string.Join(", ", site.Api.Frameworks.Select(f => $"<code>{E(f)}</code>"));
		body.Append(ui.Header("code", "API reference", "API reference",
			$"<p>Every public type of the library, read from the {frameworks} builds. Members that only some builds have are marked, " +
			"and where a member differs between builds, both versions are shown.</p>"));
		var toc = new List<TocEntry>();
		foreach (var group in site.Api.Types.GroupBy(t => t.Group).OrderBy(g => Layout.GroupOrder(g.Key)))
		{
			var id = Html.Slugs.Slug(group.Key);
			toc.Add(new TocEntry(2, id, group.Key));
			body.Append($"""<section class="api-group"><h2 id="{id}" class="section-title">{E(group.Key)}</h2><div class="type-grid">""");
			foreach (var type in group)
			{
				var summary = FirstSentence(docs.Plain(MemberDoc.Parse(type.Variants[0].DocXml).Summary));
				var availability = site.Availability(type.Frameworks);
				body.Append($"""
					<a href="{E(SiteContext.Relative(Path, SiteContext.ApiPath(type)))}" class="type-card">
					  <span class="type-card-icon">{ui.Icon(Ui.KindIcon(SearchKind(type)), "size-4")}</span>
					  <span class="type-card-body">
					    <span class="type-card-name">{E(type.Name)}</span>
					    <span class="type-card-kind">{E(KindLabel(type))}{(availability is null ? "" : " · " + E(availability))}</span>
					    <span class="type-card-summary">{E(summary)}</span>
					  </span>
					</a>
					""");
			}

			body.Append("</div></section>");
		}

		site.Search.Add(new SearchDoc("API reference", "page", Path, null, "Every public type of the library.", string.Join(' ', site.Api.Types.Select(t => t.Name))));

		return new Page { Path = Path, Title = "API reference", Section = "api", Body = body.ToString(), Toc = toc, Description = "The public API of CsvHelper." };
	}

	private Page TypePage(ApiType type)
	{
		var path = SiteContext.ApiPath(type);
		var scope = new DocScope(path, type);
		var symbol = type.Symbol;
		var doc = MemberDoc.Parse(type.Variants[0].DocXml);
		var body = new StringBuilder();
		var toc = new List<TocEntry>();
		var badges = new List<string>();
		if (site.Availability(type.Frameworks) is { } availability) badges.Add(Ui.Badge(availability, "warn", "Not in every build"));
		badges.AddRange(HistoryBadges(type.Id));
		if (Attributes.ObsoleteInfo(symbol) is { } obsolete) badges.Add(Ui.Badge("Obsolete", "danger", obsolete.Message));
		body.Append(ui.Header(Ui.KindIcon(SearchKind(type)), $"{KindLabel(type)} in {type.Namespace}", type.Name, docs.Block(doc.Summary, scope), badges));
		body.Append(Variants(type.Variants, path, scope, isType: true, showDocs: type.Variants.Count > 1));
		body.Append(Facts(type, path));
		if (doc.Remarks is not null)
		{
			toc.Add(new TocEntry(2, "remarks", "Remarks"));
			body.Append($"""<section><h2 id="remarks" class="section-title">Remarks</h2><div class="{Ui.Prose} prose-doc">{docs.Block(doc.Remarks, scope)}</div></section>""");
		}

		if (symbol.TypeKind == TypeKind.Enum)
		{
			toc.Add(new TocEntry(2, "values", "Values"));
			body.Append(EnumValues(type, scope));
		}

		foreach (var (kind, title) in Sections)
		{
			var members = type.Members.Where(m => m.Kind == kind).ToList();
			if (members.Count == 0) continue;
			var id = Html.Slugs.Slug(title);
			toc.Add(new TocEntry(2, id, title));
			body.Append($"""<section><h2 id="{id}" class="section-title">{E(title)}</h2>""");
			foreach (var member in members)
			{
				toc.Add(new TocEntry(3, member.Anchor, member.ShortName));
				body.Append(Member(member, path));
			}

			body.Append("</section>");
		}

		var plain = docs.Plain(doc.Summary);
		site.Search.Add(new SearchDoc(type.Name, SearchKind(type), path, type.Namespace, Truncate(FirstSentence(plain), 180),
			Truncate($"{plain} {docs.Plain(doc.Remarks)}", 1500)));

		return new Page
		{
			Path = path,
			Title = $"{type.Name} {KindLabel(type).ToLowerInvariant()}",
			Description = Truncate(plain, 300),
			Section = "api",
			Body = body.ToString(),
			Toc = toc,
		};
	}

	private string Member(ApiMember member, string path)
	{
		var scope = new DocScope(path, member.Type);
		var badges = new List<string>();
		if (site.Availability(member.Frameworks) is { } availability) badges.Add(Ui.Badge(availability, "warn", "Not in every build"));
		badges.AddRange(HistoryBadges(member.Id));
		if (member.Symbol.IsStatic && member.Kind != ApiMemberKind.Constructor) badges.Add(Ui.Badge("static"));
		if (member.Symbol.IsAbstract && member.Type.Symbol.TypeKind != TypeKind.Interface) badges.Add(Ui.Badge("abstract"));
		if (Attributes.ObsoleteInfo(member.Symbol) is { } obsolete) badges.Add(Ui.Badge("Obsolete", "danger", obsolete.Message));
		var summary = FirstSentence(docs.Plain(MemberDoc.Parse(member.Variants[0].DocXml).Summary));
		site.Search.Add(new SearchDoc(member.ShortName, SearchKind(member), $"{path}#{member.Anchor}", member.Type.Name, Truncate(summary, 180),
			$"{member.Type.Name}.{member.Name} {docs.Plain(MemberDoc.Parse(member.Variants[0].DocXml).Summary)}"));

		return $"""
			<article id="{E(member.Anchor)}" class="member">
			  <div class="member-head">
			    <h3 class="member-title"><a href="#{E(member.Anchor)}">{E(member.ShortName)}</a></h3>
			    <div class="badges">{string.Concat(badges)}</div>
			  </div>
			  {Variants(member.Variants, path, scope, isType: false, showDocs: true)}
			  {SourceLink(member.Id)}
			</article>
			""";
	}

	/// <summary>
	/// The declaration and docs of a type or member. Builds whose declaration or docs differ get a tab each (without
	/// script, the versions are shown one after the other, each with its label).
	/// </summary>
	private string Variants(IReadOnlyList<ApiVariant> variants, string path, DocScope scope, bool isType, bool showDocs)
	{
		if (variants.Count == 1) return Variant(variants[0], path, scope, isType, showDocs);
		var tabs = new StringBuilder("""<div class="variant-tabs" role="tablist" aria-label="Builds">""");
		var panels = new StringBuilder();
		foreach (var variant in variants)
		{
			var key = string.Join(" ", variant.Frameworks);
			var label = string.Join(", ", variant.Frameworks);
			tabs.Append($"""<button type="button" role="tab" class="variant-tab" data-variant-tab="{E(key)}">{E(label)}</button>""");
			panels.Append($"""<div class="variant" role="tabpanel" data-variant="{E(key)}"><p class="variant-label">{ui.Icon("layer-group", "size-3")}{E(label)}</p>{Variant(variant, path, scope, isType, showDocs)}</div>""");
		}

		return $"""<div class="variants" data-variants>{tabs}</div>{panels}</div>""";
	}

	private string Variant(ApiVariant variant, string path, DocScope scope, bool isType, bool showDocs)
	{
		var parts = isType ? Signatures.TypeHeader((INamedTypeSymbol)variant.Symbol) : Signatures.Member(variant.Symbol);
		var html = new StringBuilder($"""<div class="signature"><pre><code>{signatures.Render(parts, path)}</code></pre></div>""");
		if (!showDocs) return html.ToString();
		var doc = MemberDoc.Parse(variant.DocXml);
		if (!isType) html.Append($"""<div class="{Ui.Prose} prose-doc">{docs.Block(doc.Summary, scope)}</div>""");
		var symbol = variant.Symbol;
		var invoke = (symbol as INamedTypeSymbol)?.DelegateInvokeMethod;
		var typeParameters = symbol switch
		{
			IMethodSymbol method => method.TypeParameters,
			INamedTypeSymbol named when !isType => named.TypeParameters,
			_ => ImmutableArray<ITypeParameterSymbol>.Empty,
		};
		if (typeParameters.Length > 0)
		{
			html.Append(DocList("Type parameters", typeParameters.Select(p =>
				($"""<code class="param">{E(p.Name)}</code>""", docs.Inline(doc.TypeParameters.FirstOrDefault(t => t.Name == p.Name).Text, scope)))));
		}

		var parameters = symbol switch
		{
			IMethodSymbol method => method.Parameters,
			IPropertySymbol property => property.Parameters,
			_ => invoke?.Parameters ?? ImmutableArray<IParameterSymbol>.Empty,
		};
		if (parameters.Length > 0)
		{
			html.Append(DocList("Parameters", parameters.Select(p =>
				($"""<code class="param">{E(p.Name)}</code><span class="param-type">{signatures.Render(Signatures.TypeReference(p.Type), path)}</span>""",
				 docs.Inline(doc.Parameters.FirstOrDefault(d => d.Name == p.Name).Text, scope)))));
		}

		var returns = symbol switch
		{
			IMethodSymbol { MethodKind: not MethodKind.Constructor, ReturnsVoid: false } method => method.ReturnType,
			_ => invoke is { ReturnsVoid: false } ? invoke.ReturnType : null,
		};
		if (returns is not null) html.Append(Returns("Returns", returns, doc.Returns, path, scope));
		if (symbol is IPropertySymbol propertySymbol) html.Append(Returns("Property value", propertySymbol.Type, doc.Value, path, scope));
		if (doc.Exceptions.Count > 0)
		{
			html.Append(DocList("Exceptions", doc.Exceptions.Select(e => (docs.Cref(e.Cref, null, scope), docs.Inline(e.Text, scope)))));
		}

		if (!isType && doc.Remarks is not null)
		{
			html.Append($"""<h4 class="doc-heading">Remarks</h4><div class="{Ui.Prose} prose-doc">{docs.Block(doc.Remarks, scope)}</div>""");
		}

		foreach (var example in doc.Examples)
		{
			html.Append($"""<h4 class="doc-heading">Example</h4><div class="{Ui.Prose} prose-doc">{docs.Block(example, scope)}</div>""");
		}

		if (doc.SeeAlso.Count > 0)
		{
			var links = doc.SeeAlso.Select(s => s.Attribute("cref") is { } cref ? docs.Cref(cref.Value, NonEmpty(s.Value), scope)
				: s.Attribute("href") is { } href ? $"""<a href="{E(href.Value)}" class="external">{E(NonEmpty(s.Value) ?? href.Value)}</a>""" : "");
			html.Append($"""<h4 class="doc-heading">See also</h4><ul class="see-also">{string.Concat(links.Select(l => $"<li>{l}</li>"))}</ul>""");
		}

		return html.ToString();
	}

	private string Returns(string title, ITypeSymbol type, System.Xml.Linq.XElement? doc, string path, DocScope scope) =>
		$"""
		<h4 class="doc-heading">{E(title)}</h4>
		<div class="returns"><span class="param-type">{signatures.Render(Signatures.TypeReference(type), path)}</span><div class="doc-text">{docs.Inline(doc, scope)}</div></div>
		""";

	private static string DocList(string title, IEnumerable<(string Term, string Description)> items)
	{
		var html = new StringBuilder($"""<h4 class="doc-heading">{E(title)}</h4><dl class="doc-list">""");
		foreach (var (term, description) in items) html.Append($"""<div class="doc-item"><dt>{term}</dt><dd>{description}</dd></div>""");

		return html.Append("</dl>").ToString();
	}

	private string EnumValues(ApiType type, DocScope scope)
	{
		var rows = new StringBuilder();
		foreach (var member in type.Members.Where(m => m.Kind == ApiMemberKind.EnumValue))
		{
			var field = (IFieldSymbol)member.Symbol;
			var value = Convert.ToString(field.ConstantValue, System.Globalization.CultureInfo.InvariantCulture);
			var summary = MemberDoc.Parse(member.Variants[0].DocXml).Summary;
			rows.Append($"""<tr id="{E(member.Anchor)}"><td><code>{E(member.Name)}</code></td><td class="text-right tabular-nums">{E(value)}</td><td>{docs.Inline(summary, scope)}</td></tr>""");
			site.Search.Add(new SearchDoc($"{type.Name}.{member.Name}", "value", $"{SiteContext.ApiPath(member)}", type.Name,
				Truncate(FirstSentence(docs.Plain(summary)), 180), docs.Plain(summary)));
		}

		return $"""
			<section>
			  <h2 id="values" class="section-title">Values</h2>
			  <div class="table-wrap"><table class="api-table"><thead><tr><th>Name</th><th class="text-right">Value</th><th>Description</th></tr></thead><tbody>{rows}</tbody></table></div>
			</section>
			""";
	}

	/// <summary>Namespace, assembly, inheritance, interfaces, nesting, builds and source of a type.</summary>
	private string Facts(ApiType type, string path)
	{
		var symbol = type.Symbol;
		var facts = new List<(string Label, string Html)>
		{
			("Namespace", $"<code>{E(type.Namespace)}</code>"),
			("Assembly", $"<code>{E(symbol.ContainingAssembly.Name)}.dll</code>"),
		};
		if (symbol.TypeKind == TypeKind.Class && !symbol.IsStatic)
		{
			var chain = new List<INamedTypeSymbol>();
			for (var t = symbol.BaseType; t is not null; t = t.BaseType) chain.Insert(0, t);
			if (chain.Count > 1 || symbol.IsRecord)
			{
				var items = chain.Select(t => signatures.Render(Signatures.TypeReference(t), path)).Append($"<strong>{E(type.Name)}</strong>");
				facts.Add(("Inheritance", string.Join($" {ui.Icon("angle-right", "size-3 text-ink-400")} ", items)));
			}
		}

		var interfaces = Signatures.Interfaces(symbol).ToList();
		if (interfaces.Count > 0) facts.Add(("Implements", string.Join(", ", interfaces.Select(i => signatures.Render(Signatures.TypeReference(i), path)))));
		if (type.ContainingType is { } parent)
			facts.Add(("Declared in", $"""<a href="{E(SiteContext.Relative(path, SiteContext.ApiPath(parent)))}">{E(parent.Name)}</a>"""));
		if (type.NestedTypes.Count > 0)
			facts.Add(("Nested types", string.Join(", ", type.NestedTypes.Select(n => $"""<a href="{E(SiteContext.Relative(path, SiteContext.ApiPath(n)))}">{E(n.Name)}</a>"""))));
		facts.Add(("Builds", string.Join(" ", site.Api.Frameworks.Select(f => type.Frameworks.Contains(f) ? Ui.Badge(f, "brand") : Ui.Badge(f, "muted", "Not in this build")))));
		if (site.Sources.Find(type.Id) is { } source)
			facts.Add(("Source", ui.External(site.GitHubFile(source.Path, source.Line), $"{source.Path.Split('/')[^1]}", "brands/github")));
		var html = new StringBuilder("""<dl class="facts">""");
		foreach (var (label, value) in facts) html.Append($"""<div class="fact"><dt>{E(label)}</dt><dd>{value}</dd></div>""");

		return html.Append("</dl>").ToString();
	}

	private string SourceLink(string id) => site.Sources.Find(id) is { } source
		? $"""<p class="member-source">{ui.External(site.GitHubFile(source.Path, source.Line), "View source", "brands/github")}</p>"""
		: "";

	private IEnumerable<string> HistoryBadges(string id)
	{
		var releases = site.Releases;
		if (!releases.HasHistory) yield break;
		var first = releases.Releases.First(r => r.Snapshot is not null);
		if (!releases.AddedIn.TryGetValue(id, out var release)) yield return Ui.Badge("Unreleased", "brand", $"Not in {releases.Latest!.Name} or earlier");
		else if (release != first) yield return Ui.Badge($"Added in {release.Name}", "ink");
	}

	private static string? NonEmpty(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

	public static string KindLabel(ApiType type) => type.Group switch
	{
		"Attributes" => "Attribute",
		"Exceptions" => "Exception",
		_ => char.ToUpperInvariant(type.Kind[0]) + type.Kind[1..],
	};

	public static string SearchKind(ApiType type) => type.Group switch
	{
		"Attributes" => "attribute",
		"Exceptions" => "exception",
		_ => type.Kind switch
		{
			"static class" => "class",
			"record struct" => "record",
			"ref struct" => "struct",
			var kind => kind,
		},
	};

	private static string SearchKind(ApiMember member) => member.Kind switch
	{
		ApiMemberKind.EnumValue => "value",
		var kind => kind.ToString().ToLowerInvariant(),
	};
}
