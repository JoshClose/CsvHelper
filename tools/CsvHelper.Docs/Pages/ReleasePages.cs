using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CsvHelper.Docs.Api;
using CsvHelper.Docs.Content;
using CsvHelper.Docs.Git;
using CsvHelper.Docs.Releases;
using CsvHelper.Docs.Site;
using NuGet.Versioning;
using static CsvHelper.Docs.Html.Html;

namespace CsvHelper.Docs.Pages;

/// <summary>
/// The changelog: an index of the releases, a changelog per release, a migration guide per release that has something to
/// migrate (<see cref="Release.HasMigrationGuide"/>), and the changes on the branch since the last release.
/// </summary>
internal sealed class ReleasePages
{
	private const string ChangelogPath = "changelog.html";
	private const string UnreleasedPath = "changelog/unreleased.html";

	/// <summary>The ids the layout gives elements of every page (docs/Pages/Layout.cs).</summary>
	private static readonly HashSet<string> LayoutIds = ["main", "sidebar", "search-results", "icon-copy", "icon-check", "icon-link"];

	/// <summary>The ids of a changelog page's own sections, which its notes' headings must not have.</summary>
	private static readonly HashSet<string> ChangelogIds = [.. LayoutIds, "notes", "api", "added", "changed", "deprecated", "removed", "commits"];

	private readonly SiteContext site;
	private readonly ReleaseInfo releases;
	private readonly Ui ui;
	private readonly ContentLinker links;
	private readonly ApiChangeGroups groups;

	/// <param name="site">The site.</param>
	/// <param name="links">The links of the guides, which release notes link the same way.</param>
	public ReleasePages(SiteContext site, ContentLinker links)
	{
		this.site = site;
		this.links = links;
		releases = site.Releases;
		ui = new Ui(site);
		groups = new ApiChangeGroups(site, ui);
	}

	public IEnumerable<Page> Build()
	{
		yield return ChangelogIndex();
		yield return Unreleased();
		foreach (var release in releases.Releases)
		{
			yield return Changelog(release);
			if (release.HasMigrationGuide)
			{
				yield return Migration(release);
			}
		}
	}

	private Page ChangelogIndex()
	{
		var body = new StringBuilder(ui.Header("clock-rotate-left", "Releases", "Changelog",
			"<p>What changed in each release: the API additions, changes and removals, and every commit. A release with breaking " +
			"changes, deprecations or upgrade notes also has a migration guide. Each version tag (<code>v1.2.3</code>) gets an " +
			"entry here automatically.</p>"));
		body.Append(UnreleasedCard());
		if (releases.Releases.Count == 0)
		{
			body.Append(ui.Empty("code-branch", "No releases yet",
				"The first git tag that names a version, such as <code>v1.0.0</code>, appears here with its changelog."));
		}
		else
		{
			body.Append("""<ol class="timeline">""");
			foreach (var release in Enumerable.Reverse(releases.Releases)) body.Append(ReleaseCard(release));
			body.Append("</ol>");
		}

		site.Search.Add(new SearchDoc("Changelog", "page", ChangelogPath, null, "What changed in each release, and how to migrate to it.",
			"changelog release notes versions history migration upgrade breaking changes"));

		return new Page { Path = ChangelogPath, Title = "Changelog", Section = "releases", Body = body.ToString(), Description = "What changed in each release of CsvHelper, and how to upgrade to it." };
	}

	private string UnreleasedCard()
	{
		var commits = releases.UnreleasedCommits.Count;
		if (commits == 0 && !releases.HasUnreleasedApiChanges) return "";
		var since = releases.Latest is { } latest ? $"since {E(latest.Name)}" : "so far";
		var api = releases.UnreleasedDiff is { } diff ? $" · {Counts(diff)}" : "";

		return $"""
			<a href="{UnreleasedPath}" class="unreleased-card">
			  <span class="unreleased-icon">{ui.Icon("code-branch", "size-4")}</span>
			  <span>
			    <span class="unreleased-title">Unreleased</span>
			    <span class="unreleased-text">{commits} commit{(commits == 1 ? "" : "s")} on {E(site.Config.Branch)} {since}{api}</span>
			  </span>
			  {ui.Icon("chevron-right", "size-4 ml-auto text-ink-400")}
			</a>
			""";
	}

	private string ReleaseCard(Release release)
	{
		var badges = new List<string>();
		if (release.Version.IsPrerelease) badges.Add(Ui.Badge("Prerelease", "warn"));
		if (release == releases.Latest) badges.Add(Ui.Badge("Latest", "brand"));
		if (release.IsPreview) badges.Add(Ui.Badge("Preview", "ink", "Not tagged yet: built with --preview"));
		if (release.Diff is { } diff && diff.Breaking.Any()) badges.Add(Ui.Badge($"{diff.Breaking.Count()} breaking", "danger"));
		var summary = release.Diff is { } d ? Counts(d) : release.Previous is null ? "First release" : "API diff unavailable";
		var changelog = E(SiteContext.Relative(ChangelogPath, release.ChangelogPath));
		var migration = release.HasMigrationGuide
			? $"""<a href="{E(SiteContext.Relative(ChangelogPath, release.MigrationPath))}">{ui.Icon("right-left", "size-3")}Migration guide</a>"""
			: "";

		return $"""
			<li class="timeline-item">
			  <span class="timeline-dot"></span>
			  <div class="release-card">
			    <div class="release-head">
			      <a href="{changelog}" class="release-name">{E(release.Name)}</a>
			      <span class="badges">{string.Concat(badges)}</span>
			      <time class="release-date" datetime="{release.Tag.Date:yyyy-MM-dd}">{Date(release.Tag.Date)}</time>
			    </div>
			    <p class="release-summary">{release.Commits.Count} commit{(release.Commits.Count == 1 ? "" : "s")} · {E(summary)}</p>
			    <p class="release-links">
			      <a href="{changelog}">{ui.Icon("clock-rotate-left", "size-3")}Changelog</a>
			      {migration}
			    </p>
			  </div>
			</li>
			""";
	}

	private Page Unreleased()
	{
		var latest = releases.Latest;
		var lead = latest is null
			? $"<p>Nothing has been released yet. These are the commits on <code>{E(site.Config.Branch)}</code> so far.</p>"
			: $"<p>What is on <code>{E(site.Config.Branch)}</code> but not in {E(latest.Name)} yet.</p>";
		var body = new StringBuilder(ui.Header("code-branch", "Changelog", "Unreleased", lead));
		var toc = new List<TocEntry>();
		if (latest is null)
		{
			toc.Add(new TocEntry(2, "api", "API"));
			body.Append(Section("api", "API", $"<p>{InitialApi(ApiExtractor.Snapshot(site.Api))}</p>"));
		}
		else if (releases.UnreleasedDiff is { } diff)
		{
			body.Append(Changes(diff, UnreleasedPath, toc));
		}
		else
		{
			body.Append(Section("api", "API", $"<p>{E(latest.SnapshotError ?? "API diff unavailable.")}</p>"));
		}

		toc.Add(new TocEntry(2, "commits", "Commits"));
		body.Append(Commits(releases.UnreleasedCommits, latest is null ? null : site.GitHubCompare(latest.Name, site.Sha)));

		return new Page { Path = UnreleasedPath, Title = "Unreleased changes", Section = "releases", Body = body.ToString(), Toc = toc };
	}

	private Page Changelog(Release release)
	{
		var path = release.ChangelogPath;
		var badges = new List<string>();
		if (release.Version.IsPrerelease) badges.Add(Ui.Badge("Prerelease", "warn"));
		if (release == releases.Latest) badges.Add(Ui.Badge("Latest", "brand"));
		if (release.IsPreview) badges.Add(Ui.Badge("Preview", "ink", "Not tagged yet: built with --preview"));
		var previous = release.Previous is { } p ? $""" Previous release: <a href="{E(SiteContext.Relative(path, p.ChangelogPath))}">{E(p.Name)}</a>.""" : "";
		var migration = release.HasMigrationGuide ? $""" <a href="{E(SiteContext.Relative(path, release.MigrationPath))}">Migration guide</a>.""" : "";
		var lead = $"""<p>Released {Date(release.Tag.Date)}.{previous}{migration}</p>""";
		var body = new StringBuilder(ui.Header("clock-rotate-left", "Changelog", release.Name, lead, badges));
		var toc = new List<TocEntry>();
		if (release.ChangelogNotes is { } notes)
		{
			toc.Add(new TocEntry(2, "notes", "Notes"));
			var rendered = RenderNotes(release, notes, release.ChangelogNotesPath ?? "", path, ChangelogIds);
			body.Append(Section("notes", "Notes", $"""<div class="{Ui.Prose} prose-guide">{rendered.Html}</div>"""));
		}

		if (release.Previous is null)
		{
			toc.Add(new TocEntry(2, "api", "API"));
			var api = release.Snapshot is { } snapshot ? InitialApi(snapshot) : E(release.SnapshotError ?? "API unavailable.");
			body.Append(Section("api", "API", $"""<p>{api} <a href="{E(SiteContext.Relative(path, "api/index.html"))}">Browse the API reference</a>.</p>"""));
		}
		else if (release.Diff is { } diff)
		{
			body.Append(Changes(diff, path, toc));
		}
		else
		{
			toc.Add(new TocEntry(2, "api", "API"));
			body.Append(Section("api", "API", $"<p>{E(release.SnapshotError ?? release.Previous.SnapshotError ?? "API diff unavailable.")}</p>"));
		}

		toc.Add(new TocEntry(2, "commits", "Commits"));
		// A preview has no tag on GitHub yet: compare with the commit it was built from instead.
		var to = release.IsPreview ? site.Sha : release.Name;
		body.Append(Commits(release.Commits, release.Previous is { } before ? site.GitHubCompare(before.Name, to) : null));
		site.Search.Add(new SearchDoc($"{release.Name} changelog", "release", path, "Changelog", $"Released {Date(release.Tag.Date)}.",
			Text(release.ChangelogNotes ?? "") + " " + string.Join(' ', release.Commits.Select(c => c.Subject))));

		return new Page { Path = path, Title = $"{release.Name} changelog", Section = "releases", Body = body.ToString(), Toc = toc };
	}

	/// <summary>
	/// The migration guide of a release. With hand-written notes, the notes are the guide and every API change follows as an
	/// appendix; without, the guide lists the breaking changes, the deprecations and other changes worth a look.
	/// </summary>
	private Page Migration(Release release)
	{
		var path = release.MigrationPath;
		var title = $"Migrating to {release.Name}";
		var from = release.Previous is { } previous ? $" from {previous.Name}" : "";
		var lead = $"""<p>Moving to {E(release.Name)}{E(from)}. <a href="{E(SiteContext.Relative(path, release.ChangelogPath))}">Changelog</a>.</p>""";
		var body = new StringBuilder(ui.Header("right-left", "Migration guide", title, lead));
		var toc = new List<TocEntry>();
		var breakingCount = release.Diff?.Breaking.Count() ?? 0;
		var summary = $"{breakingCount} breaking change{(breakingCount == 1 ? "" : "s")}.";
		var breakingTitles = string.Join(' ', release.Diff?.Breaking.Select(c => c.Title) ?? []);
		if (release.MigrationNotes is { } notes)
		{
			var rendered = RenderNotes(release, notes, release.MigrationNotesPath ?? "", path, LayoutIds);
			toc.AddRange(rendered.Toc);
			body.Append($"""<div class="{Ui.Prose} prose-guide">{rendered.Html}</div>""");
			body.Append(Appendix(release, path, toc));
			body.Append($"""
				<div class="page-end">
				  <a href="{E(site.GitHubEdit(release.MigrationNotesPath ?? ""))}" class="edit-link">{ui.Icon("pen-to-square", "size-3.5")}Edit this guide on GitHub</a>
				</div>
				""");
			SearchSections.Add(site, title, "migration", path, "Changelog", summary, rendered.Blocks, breakingTitles);
		}
		else
		{
			body.Append(Generated(release, path, toc));
			site.Search.Add(new SearchDoc(title, "migration", path, "Changelog", summary, breakingTitles));
		}

		return new Page { Path = path, Title = title, Section = "releases", Body = body.ToString(), Toc = toc };
	}

	/// <summary>A migration guide without notes: the breaking changes, the deprecations and other changes worth a look, grouped by type.</summary>
	/// <param name="release">The release.</param>
	/// <param name="path">The site path of the page.</param>
	/// <param name="toc">The page's "On this page" entries, which get the sections.</param>
	/// <returns>The HTML.</returns>
	private string Generated(Release release, string path, List<TocEntry> toc)
	{
		if (release.Previous is null)
		{
			return ui.Empty("flag", "The first release", "There is nothing to migrate from.");
		}

		if (release.Diff is not { } diff)
		{
			return ui.Empty("circle-info", "API diff unavailable", E(release.SnapshotError ?? release.Previous.SnapshotError ?? ""));
		}

		var html = new StringBuilder();
		var breaking = diff.Changes.Where(c => c.Breaking).ToList();
		var deprecated = diff.Changes.Where(c => c.Kind == ChangeKind.Deprecated && !c.Breaking).ToList();
		var notesOnly = diff.Changes.Where(c => !c.Breaking && c.Kind == ChangeKind.Changed && c.Note is not null).ToList();
		var frameworks = diff.FrameworksRemoved.Count == 0
			? ""
			: $"""<p class="api-lead">{E(release.Name)} no longer targets {E(string.Join(", ", diff.FrameworksRemoved))}.</p>""";
		toc.Add(new TocEntry(2, "breaking-changes", "Breaking changes"));
		html.Append(Section("breaking-changes", "Breaking changes", breaking.Count == 0 && frameworks.Length == 0
			? ui.Empty("circle-check", "No breaking changes", $"Code written against {E(release.Previous.Name)} compiles and runs against {E(release.Name)}.")
			: frameworks + groups.Render(breaking, path, "breaking-changes", toc, breaking.Count <= 15)));
		if (deprecated.Count > 0)
		{
			toc.Add(new TocEntry(2, "deprecated", "Deprecated"));
			html.Append(Section("deprecated", "Deprecated", "<p>These still work, but will be removed in a later release.</p>" +
				groups.Render(deprecated, path, "deprecated", toc, deprecated.Count <= 15)));
		}

		if (notesOnly.Count > 0)
		{
			toc.Add(new TocEntry(2, "other-changes", "Other changes"));
			html.Append(Section("other-changes", "Other changes", "<p>Not breaking, but worth a look.</p>" +
				groups.Render(notesOnly, path, "other-changes", toc, notesOnly.Count <= 15)));
		}

		return html.ToString();
	}

	/// <summary>Every API change of a release, grouped by namespace and type: the appendix of a hand-written migration guide.</summary>
	/// <param name="release">The release.</param>
	/// <param name="path">The site path of the page.</param>
	/// <param name="toc">The page's "On this page" entries, which get the appendix and its namespaces.</param>
	/// <returns>The HTML.</returns>
	private string Appendix(Release release, string path, List<TocEntry> toc)
	{
		// Generated ids have a dot, which ids of Markdown headings never have.
		const string Id = "api.changes";
		toc.Add(new TocEntry(2, Id, "All API changes"));
		string content;
		if (release.Previous is null)
		{
			content = Lead("The first release: there is nothing to migrate from.");
		}
		else if (release.Diff is not { } diff)
		{
			var changelog = E(SiteContext.Relative(path, release.ChangelogPath));
			content = $"""
				<p class="api-lead">The API of {E(release.Previous.Name)} or {E(release.Name)} could not be read, so there is no list of every
				change. The <a href="{changelog}">{E(release.Name)} changelog</a> has the commits.</p>
				""";
		}
		else if (diff.Changes.Count == 0 && diff.FrameworksAdded.Count == 0 && diff.FrameworksRemoved.Count == 0)
		{
			content = Lead("No changes to the public API.");
		}
		else
		{
			var breaking = diff.Breaking.Count();
			var text = $"Every change to the public API since {release.Previous.Name}: {Counts(diff)}" +
				$"{(breaking > 0 ? $" ({breaking} breaking)" : "")}.{FrameworkChanges(diff)} Open a type to see its changes.";
			content = Lead(text) + groups.Render(diff.Changes, path, "api", toc, false);
		}

		return Section(Id, "All API changes", content);
	}

	/// <summary>The paragraph under the appendix's heading.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The HTML.</returns>
	private static string Lead(string text) => $"""<p class="api-lead">{E(text)}</p>""";

	/// <summary>The target frameworks a diff adds or drops, as sentences that start with a space; empty when none.</summary>
	/// <param name="diff">The diff.</param>
	/// <returns>The sentences.</returns>
	private static string FrameworkChanges(ApiDiff diff)
	{
		var text = "";
		if (diff.FrameworksAdded.Count > 0)
		{
			text += $" New target framework{(diff.FrameworksAdded.Count == 1 ? "" : "s")}: {string.Join(", ", diff.FrameworksAdded)}.";
		}

		if (diff.FrameworksRemoved.Count > 0)
		{
			text += $" No longer targets {string.Join(", ", diff.FrameworksRemoved)}.";
		}

		return text;
	}

	/// <summary>Renders a release's notes for one of its pages.</summary>
	/// <param name="release">The release.</param>
	/// <param name="text">The notes' Markdown.</param>
	/// <param name="sourcePath">The notes' file, relative to the repository; empty for a tag's message.</param>
	/// <param name="page">The site path of the page.</param>
	/// <param name="reservedIds">The ids the page uses itself.</param>
	/// <returns>The rendered notes.</returns>
	private RenderedNotes RenderNotes(Release release, string text, string sourcePath, string page, IReadOnlySet<string> reservedIds)
	{
		var labels = new DiffLabels(release.Previous is { } previous ? $"Before · {previous.Name}" : "Before", $"After · {release.Name}");
		// The API reference documents the current API: only notes of the current major or later link code to it.
		var linkCode = releases.CurrentMajor is { } current && VersionComparer.Default.Compare(release.Version, current.Version) >= 0;

		return ReleaseNotes.Render(text, sourcePath, page, !release.IsPreview, links, linkCode, labels, reservedIds);
	}

	/// <summary>The API changes of a diff, grouped by kind, with breaking ones marked.</summary>
	private string Changes(ApiDiff diff, string path, List<TocEntry> toc)
	{
		if (diff.Changes.Count == 0 && diff.FrameworksAdded.Count == 0 && diff.FrameworksRemoved.Count == 0)
		{
			toc.Add(new TocEntry(2, "api", "API"));

			return Section("api", "API", "<p>No changes to the public API.</p>");
		}

		var html = new StringBuilder();
		foreach (var (kind, title, icon) in new[]
		{
			(ChangeKind.Added, "Added", "circle-plus"),
			(ChangeKind.Changed, "Changed", "pen-to-square"),
			(ChangeKind.Deprecated, "Deprecated", "triangle-exclamation"),
			(ChangeKind.Removed, "Removed", "circle-minus"),
		})
		{
			var changes = diff.Changes.Where(c => c.Kind == kind).ToList();
			var frameworks = kind switch
			{
				ChangeKind.Added => diff.FrameworksAdded,
				ChangeKind.Removed => diff.FrameworksRemoved,
				_ => [],
			};
			if (changes.Count == 0 && frameworks.Count == 0)
			{
				continue;
			}

			var id = title.ToLowerInvariant();
			toc.Add(new TocEntry(2, id, title));
			var items = new StringBuilder();
			if (frameworks.Count > 0)
			{
				var what = kind == ChangeKind.Added ? "New target framework" : "No longer targets";
				items.Append($"""<p class="api-lead">{what}{(kind == ChangeKind.Added && frameworks.Count > 1 ? "s" : "")}: {E(string.Join(", ", frameworks))}.</p>""");
			}

			items.Append("""<ul class="change-list">""");
			foreach (var change in changes)
			{
				items.Append($"""
					<li class="change">
					  <span class="change-icon change-{id}">{ui.Icon(icon, "size-3.5")}</span>
					  <div class="change-body">
					    <p class="change-title">{groups.ChangeTitle(change, path)}{(change.Breaking ? Ui.Badge("Breaking", "danger") : "")}{ApiChangeGroups.FrameworkBadges(change)}</p>
					    {(change.Note is null ? "" : $"""<p class="change-note">{E(change.Note)}</p>""")}
					  </div>
					</li>
					""");
			}

			html.Append(Section(id, title, items.Append("</ul>").ToString()));
		}

		return html.ToString();
	}

	private string Commits(IReadOnlyList<GitCommit> commits, string? compare)
	{
		var html = new StringBuilder();
		if (commits.Count == 0)
		{
			html.Append("<p>No commits.</p>");
		}
		else
		{
			html.Append("""<ul class="commit-list">""");
			foreach (var commit in commits)
			{
				html.Append($"""
					<li class="commit">
					  <a href="{E(site.GitHubCommit(commit.Sha))}" class="commit-sha">{E(commit.ShortSha)}</a>
					  <span class="commit-subject">{E(commit.Subject)}</span>
					  <span class="commit-meta">{E(commit.Author)} · <time datetime="{commit.Date:yyyy-MM-dd}">{Date(commit.Date)}</time></span>
					</li>
					""");
			}

			html.Append("</ul>");
		}

		if (compare is not null) html.Append($"""<p class="mt-4">{ui.External(compare, "Compare on GitHub", "brands/github")}</p>""");

		return Section("commits", "Commits", html.ToString());
	}

	private static string Section(string id, string title, string content) =>
		$"""<section><h2 id="{id}" class="section-title">{E(title)}</h2>{content}</section>""";

	private static string InitialApi(ApiSnapshot snapshot) =>
		$"The initial API: {snapshot.Types.Count} types and {snapshot.Members.Count} members.";

	private static string Counts(ApiDiff diff)
	{
		if (diff.Changes.Count == 0) return "no API changes";
		var parts = new List<string>();
		foreach (var kind in new[] { ChangeKind.Added, ChangeKind.Changed, ChangeKind.Deprecated, ChangeKind.Removed })
		{
			if (diff.Count(kind) is > 0 and var count) parts.Add($"{count} {kind.ToString().ToLowerInvariant()}");
		}

		return string.Join(", ", parts);
	}

	private static string Date(DateTimeOffset date) => date.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);
}
