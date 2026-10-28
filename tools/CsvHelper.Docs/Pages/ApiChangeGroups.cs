using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CsvHelper.Docs.Content;
using CsvHelper.Docs.Releases;
using CsvHelper.Docs.Site;
using static CsvHelper.Docs.Html.Html;

namespace CsvHelper.Docs.Pages;

/// <summary>
/// Renders API changes grouped by namespace and type: a collapsible box per changed type with its changes inside, and
/// the types added or removed as a whole as compact lists, so that hundreds of changes stay readable.
/// </summary>
internal sealed class ApiChangeGroups
{
	private readonly SiteContext site;
	private readonly Ui ui;

	/// <summary>Creates the renderer.</summary>
	/// <param name="site">The site, for the API pages that changes link to.</param>
	/// <param name="ui">The markup helpers.</param>
	public ApiChangeGroups(SiteContext site, Ui ui)
	{
		this.site = site;
		this.ui = ui;
	}

	/// <summary>Renders changes grouped by namespace (a heading each, when there are several) and type.</summary>
	/// <param name="changes">The changes.</param>
	/// <param name="page">The site path of the page, for links.</param>
	/// <param name="idPrefix">The start of the namespace headings' ids; it contains no dot, and the ids add one, so they never match a Markdown heading's.</param>
	/// <param name="toc">The page's "On this page" entries, which get the namespace headings; null for none.</param>
	/// <param name="open">Whether the boxes of the types start open.</param>
	/// <returns>The HTML.</returns>
	public string Render(IReadOnlyList<ApiChange> changes, string page, string idPrefix, List<TocEntry>? toc, bool open)
	{
		var html = new StringBuilder();
		var namespaces = changes.GroupBy(c => c.Namespace).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
		foreach (var ns in namespaces)
		{
			if (namespaces.Count > 1)
			{
				var title = ns.Key.Length == 0 ? "Global namespace" : ns.Key;
				var id = $"{idPrefix}.{(ns.Key.Length == 0 ? "global" : ns.Key.ToLowerInvariant())}";
				html.Append($"""<h3 id="{E(id)}" class="api-namespace-title">{E(title)}</h3>""");
				toc?.Add(new TocEntry(3, id, title));
			}

			var addedTypes = new List<ApiChange>();
			var removedTypes = new List<ApiChange>();
			var boxes = new StringBuilder();
			foreach (var type in ns.GroupBy(c => c.TypeId).OrderBy(g => g.First().TypeName, StringComparer.Ordinal))
			{
				var list = type.ToList();
				// A type added or removed as a whole has no member changes (they are covered by the type's entry).
				if (list is [{ IsType: true, Frameworks.Count: 0, Kind: ChangeKind.Added or ChangeKind.Removed } whole])
				{
					(whole.Kind == ChangeKind.Added ? addedTypes : removedTypes).Add(whole);
					continue;
				}

				boxes.Append(TypeBox(list, page, open));
			}

			if (boxes.Length > 0)
			{
				html.Append($"""<div class="api-types">{boxes}</div>""");
			}

			if (addedTypes.Count > 0)
			{
				html.Append(Chips($"Added types ({addedTypes.Count})", addedTypes, page, true, "api-type-list"));
			}

			if (removedTypes.Count > 0)
			{
				html.Append(Chips($"Removed types ({removedTypes.Count})", removedTypes, page, false, "api-type-list"));
			}
		}

		return html.ToString();
	}

	/// <summary>The changed type or member, linked to its page when it still exists.</summary>
	/// <param name="change">The change.</param>
	/// <param name="page">The site path of the page, for the link.</param>
	/// <param name="text">The text to show; the change's title when null.</param>
	/// <returns>The HTML.</returns>
	public string ChangeTitle(ApiChange change, string page, string? text = null)
	{
		var title = $"<code>{E(text ?? change.Title)}</code>";

		return Url(change) is { } url ? $"""<a href="{E(SiteContext.Relative(page, url))}">{title}</a>""" : title;
	}

	/// <summary>A badge with the target frameworks a change applies to, when it does not apply to all of them.</summary>
	/// <param name="change">The change.</param>
	/// <returns>The badge's HTML, or an empty string.</returns>
	public static string FrameworkBadges(ApiChange change) => change.Frameworks.Count == 0 ? "" : Ui.Badge(string.Join(", ", change.Frameworks), "warn");

	/// <summary>The API page of a change's type or member, when it exists now and was not removed.</summary>
	/// <param name="change">The change.</param>
	/// <returns>The site path of the page, or null.</returns>
	private string? Url(ApiChange change)
	{
		if (change.Kind == ChangeKind.Removed && change.Frameworks.Count == 0)
		{
			return null;
		}

		if (site.Api.MembersById.TryGetValue(change.Id, out var member))
		{
			return SiteContext.ApiPath(member);
		}

		return site.Api.TypesById.TryGetValue(change.Id, out var type) ? SiteContext.ApiPath(type) : null;
	}

	/// <summary>A collapsible box with the changes to one type and its members.</summary>
	/// <param name="changes">The changes to the type and its members.</param>
	/// <param name="page">The site path of the page, for links.</param>
	/// <param name="open">Whether the box starts open.</param>
	/// <returns>The HTML.</returns>
	private string TypeBox(List<ApiChange> changes, string page, bool open)
	{
		var first = changes[0];
		var members = changes.Where(c => !c.IsType).ToList();
		var body = new StringBuilder();
		foreach (var change in changes.Where(c => c.IsType))
		{
			body.Append(Row(change, page, change.Title));
		}

		foreach (var change in members.Where(c => c.Breaking && c.Kind != ChangeKind.Removed))
		{
			body.Append(Row(change, page, ShortTitle(change)));
		}

		foreach (var change in members.Where(c => c.Kind == ChangeKind.Deprecated && !c.Breaking))
		{
			body.Append(Row(change, page, ShortTitle(change), diff: false));
		}

		var removed = members.Where(c => c.Kind == ChangeKind.Removed).ToList();
		var plainRemoved = removed.Where(c => c.Note is null && c.Frameworks.Count == 0).ToList();
		if (plainRemoved.Count > 0)
		{
			body.Append(Chips("Removed", plainRemoved, page, false, null));
		}

		// Changes that share a note (a renamed parameter, no longer virtual) are one row with the members as chips.
		var noted = removed.Except(plainRemoved)
			.Concat(members.Where(c => !c.Breaking && c.Kind == ChangeKind.Changed))
			.Concat(members.Where(c => c.Kind == ChangeKind.Added && !c.Breaking && c.Note is not null));
		foreach (var group in noted.GroupBy(c => c.Note ?? ""))
		{
			body.Append($"""<div class="api-row"><p class="change-note">{E(group.Key)}</p>{ChipList(group, page, true)}</div>""");
		}

		var added = members.Where(c => c.Kind == ChangeKind.Added && !c.Breaking && c.Note is null).ToList();
		if (added.Count > 0)
		{
			body.Append(Chips("Added", added, page, true, null));
		}

		if (site.Api.TypesById.TryGetValue(first.TypeId, out var current))
		{
			body.Append($"""<p class="api-type-link"><a href="{E(SiteContext.Relative(page, SiteContext.ApiPath(current)))}">{E(current.Name)} in the API reference</a></p>""");
		}

		return $"""
			<details class="api-type"{(open ? " open" : "")}>
			  <summary>{ui.Icon("chevron-right", "size-3 api-chevron")}<code>{E(first.TypeName)}</code><span class="api-type-kind">{E(first.TypeKind)}</span><span class="badges">{Badges(changes)}</span></summary>
			  <div class="api-type-body">{body}</div>
			</details>
			""";
	}

	/// <summary>One change: its title, badges and note, and the old and new declarations.</summary>
	/// <param name="change">The change.</param>
	/// <param name="page">The site path of the page, for links.</param>
	/// <param name="title">The text of the title.</param>
	/// <param name="diff">Whether to show the old and new declarations.</param>
	/// <returns>The HTML.</returns>
	private string Row(ApiChange change, string page, string title, bool diff = true)
	{
		var note = change.Note ?? (change.Kind == ChangeKind.Removed ? "Removed." : null);
		var declarations = diff && (change.Before is not null || change.After is not null)
			? BeforeAfter.Html(change.Before is null ? null : new DiffSide("Before", change.Before), change.After is null ? null : new DiffSide("After", change.After))
			: "";

		return $"""
			<div class="api-row">
			  <p class="change-title">{ChangeTitle(change, page, title)}{(change.Breaking ? Ui.Badge("Breaking", "danger") : "")}{FrameworkBadges(change)}</p>
			  {(note is null ? "" : $"""<p class="change-note">{E(note)}</p>""")}
			  {declarations}
			</div>
			""";
	}

	/// <summary>A labeled list of compact changes.</summary>
	/// <param name="label">The label above the list.</param>
	/// <param name="changes">The changes.</param>
	/// <param name="page">The site path of the page, for links.</param>
	/// <param name="link">Whether the changes link to their API pages.</param>
	/// <param name="classes">More classes of the list, or null.</param>
	/// <returns>The HTML.</returns>
	private string Chips(string label, IEnumerable<ApiChange> changes, string page, bool link, string? classes) =>
		$"""<div class="api-group {classes}"><p class="api-group-label">{E(label)}</p>{ChipList(changes, page, link)}</div>""";

	/// <summary>Changes as compact chips, with the old or new declaration as their tooltip.</summary>
	/// <param name="changes">The changes.</param>
	/// <param name="page">The site path of the page, for links.</param>
	/// <param name="link">Whether the changes link to their API pages.</param>
	/// <returns>The HTML.</returns>
	private string ChipList(IEnumerable<ApiChange> changes, string page, bool link)
	{
		var html = new StringBuilder("""<div class="api-chips">""");
		foreach (var change in changes)
		{
			var text = change.IsType ? change.Title : ShortTitle(change);
			var tip = change.Before ?? change.After ?? change.TypeKind;
			if (link && Url(change) is { } url)
			{
				html.Append($"""<a href="{E(SiteContext.Relative(page, url))}" class="api-chip" title="{E(tip)}">{E(text)}</a>""");
			}
			else
			{
				html.Append($"""<span class="api-chip" title="{E(tip)}">{E(text)}</span>""");
			}
		}

		return html.Append("</div>").ToString();
	}

	/// <summary>A member's title without its type's name, which the box already shows.</summary>
	/// <param name="change">The change to the member.</param>
	/// <returns>The title.</returns>
	private static string ShortTitle(ApiChange change) =>
		change.Title.StartsWith(change.TypeName + ".", StringComparison.Ordinal) ? change.Title[(change.TypeName.Length + 1)..] : change.Title;

	/// <summary>The counts of a type's changes, as badges.</summary>
	/// <param name="changes">The changes to the type and its members.</param>
	/// <returns>The badges' HTML.</returns>
	private static string Badges(List<ApiChange> changes)
	{
		var badges = new StringBuilder();
		var breaking = changes.Count(c => c.Breaking);
		if (breaking > 0)
		{
			badges.Append(Ui.Badge($"{breaking} breaking", "danger"));
		}

		foreach (var (kind, tone) in new[] { (ChangeKind.Removed, "ink"), (ChangeKind.Changed, "ink"), (ChangeKind.Deprecated, "warn"), (ChangeKind.Added, "brand") })
		{
			var count = changes.Count(c => c.Kind == kind);
			if (count > 0)
			{
				badges.Append(Ui.Badge($"{count} {kind.ToString().ToLowerInvariant()}", tone));
			}
		}

		return badges.ToString();
	}
}
