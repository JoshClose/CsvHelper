using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CsvHelper.Docs.Content;
using CsvHelper.Docs.Site;
using static CsvHelper.Docs.Html.Html;

namespace CsvHelper.Docs.Pages;

/// <summary>
/// The landing page: a hero with the logo and code samples, numbers, features, the README intro, where to go next, where to
/// get help and the sponsors.
/// </summary>
internal sealed class HomePage
{
	private const string Path = "index.html";

	private readonly SiteContext site;
	private readonly GuideSet guides;
	private readonly Ui ui;

	public HomePage(SiteContext site, GuideSet guides)
	{
		this.site = site;
		this.guides = guides;
		ui = new Ui(site);
	}

	public Page Build()
	{
		var config = site.Config;
		var tabs = new StringBuilder();
		var panels = new StringBuilder();
		foreach (var (sample, code, language) in guides.Samples)
		{
			var key = Html.Slugs.Slug(sample.Title);
			tabs.Append($"""<button type="button" role="tab" class="sample-tab" data-sample-tab="{key}">{ui.Icon(sample.Icon, "size-3.5")}{E(sample.Title)}</button>""");
			panels.Append($"""<div class="sample-panel" role="tabpanel" data-sample="{key}"><pre><code class="language-{E(language)}">{E(code)}</code></pre></div>""");
		}

		var stats = string.Concat(config.Home.Stats.Select(s => $"""<div class="stat"><dt class="stat-label">{E(s.Label)}</dt><dd class="stat-value">{E(s.Value)}</dd></div>"""));
		var install = site.Releases.Released
			? $"dotnet add package {config.Package}"
			: $"git clone {config.Repository}.git";
		var installNote = site.Releases.Released
			? $"The latest release is {site.Releases.Latest!.Name}."
			: "Not on NuGet yet: build it from source.";
		var next = string.Concat(guides.Pages.Take(4).Select(g => $"""
			<a href="{E(g.Path)}" class="next-card">
			  <span class="next-icon">{ui.Icon(g.Icon, "size-4")}</span>
			  <span><span class="next-title">{E(g.Title)}</span><span class="next-text">{E(g.Description)}</span></span>
			</a>
			"""));
		var body = $"""
			<section class="hero">
			  <div class="hero-mark" aria-hidden="true">{site.Logos.Comma("size-full")}</div>
			  <div class="hero-inner">
			    <div class="hero-copy">
			      {UpgradeCallout()}
			      <p class="hero-eyebrow">{ui.Icon("bolt", "size-3.5")}SIMD CSV for .NET</p>
			      <h1 class="hero-title"><span class="sr-only">{E(config.Title)}</span>{site.Logos.Logo("hero-logo")}</h1>
			      <p class="hero-tagline">{E(config.Tagline)}</p>
			      <p class="hero-text">{E(config.Description)}</p>
			      <div class="hero-actions">
			        <a href="getting-started.html" class="button-primary">{ui.Icon("rocket", "size-4")}Get started</a>
			        <a href="api/index.html" class="button-secondary">{ui.Icon("code", "size-4")}API reference</a>
			      </div>
			      <div class="install">
			        <div class="install-command"><span class="install-prompt">&gt;</span><code data-copy-text>{E(install)}</code></div>
			        <p class="install-note">{E(installNote)}</p>
			      </div>
			    </div>
			    <div class="hero-samples" data-samples>
			      <div class="sample-tabs" role="tablist" aria-label="Code samples">{tabs}</div>
			      {panels}
			    </div>
			  </div>
			</section>
			<section class="home-section">
			  <dl class="stats">{stats}</dl>
			</section>
			<section class="home-section">
			  <h2 class="home-heading">Why CsvHelper</h2>
			  <div class="feature-grid">{Cards(config.Home.Features)}</div>
			</section>
			<section class="home-section home-intro">
			  <h2 class="home-heading">At a glance</h2>
			  <div class="max-w-4xl"><div class="{Ui.Prose} prose-guide">{guides.IntroHtml}</div></div>
			</section>
			<section class="home-section">
			  <h2 class="home-heading">Start reading</h2>
			  <div class="next-grid">{next}</div>
			</section>
			{Section("get-help", "Get help", config.Home.Help)}
			{Section("sponsors", "Sponsors", config.Home.Sponsors)}
			{Contributors(config.Home.Contributors)}
			""";
		site.Search.Add(new SearchDoc(config.Title, "page", Path, null, config.Tagline, Text(guides.IntroHtml)));

		return new Page { Path = Path, Title = config.Title, Description = config.Description, Section = "home", Body = body, Wide = true };
	}

	/// <summary>
	/// A link to the migration guide of the current major release when it has a hand-written one, for the people who come
	/// to the site after upgrading; nothing otherwise (also while the branch has an unreleased major).
	/// </summary>
	/// <returns>The HTML, or an empty string.</returns>
	private string UpgradeCallout()
	{
		if (site.Releases.CurrentMajor is not { MigrationNotes: not null, Previous: { } previous } major)
		{
			return "";
		}

		return $"""
			<a href="{E(major.MigrationPath)}" class="hero-callout">
			  {ui.Icon("right-left", "size-3.5")}<span>Upgrading from CsvHelper {previous.Version.Major}? Read the {E(major.Name)} migration guide</span>{ui.Icon("chevron-right", "size-3")}
			</a>
			""";
	}

	/// <summary>A section of cards, or nothing when site.json lists none.</summary>
	/// <param name="id">The id of the section, for links to it.</param>
	/// <param name="heading">The heading of the section.</param>
	/// <param name="cards">The cards from site.json.</param>
	/// <returns>The section's HTML.</returns>
	private string Section(string id, string heading, List<FeatureConfig> cards)
	{
		if (cards.Count == 0)
		{
			return "";
		}

		return $"""
			<section class="home-section" id="{id}">
			  <h2 class="home-heading">{E(heading)}</h2>
			  <div class="feature-grid">{Cards(cards)}</div>
			</section>
			""";
	}

	/// <summary>
	/// The guidelines for contributing, then the contributors' avatars, which load lazily (the image is large and at the
	/// bottom of the page); nothing when site.json has no contributors section.
	/// </summary>
	/// <param name="contributors">The section from site.json, or <see langword="null"/>.</param>
	/// <returns>The section's HTML.</returns>
	private static string Contributors(ContributorsConfig? contributors)
	{
		if (contributors is null)
		{
			return "";
		}

		var guidelines = string.Concat(contributors.Guidelines.Select(g => $"<li>{E(g)}</li>"));

		return $"""
			<section class="home-section" id="contributors">
			  <h2 class="home-heading">Contributors</h2>
			  <div class="contributors">
			    <div class="{Ui.Prose} prose-guide">
			      <p>{E(contributors.Intro)}</p>
			      <ol>{guidelines}</ol>
			    </div>
			    <a href="{E(contributors.Link)}" class="contributors-avatars">
			      <img src="{E(contributors.Image)}" alt="The avatars of CsvHelper's contributors" loading="lazy" decoding="async">
			    </a>
			  </div>
			</section>
			""";
	}

	/// <summary>
	/// The cards of features, places to get help or sponsors: an icon, or an image such as a sponsor's logo, then the title
	/// (marked when the link leaves the site) and the text.
	/// </summary>
	/// <param name="cards">The cards from site.json.</param>
	/// <returns>The cards' HTML.</returns>
	private string Cards(IEnumerable<FeatureConfig> cards) => string.Concat(cards.Select(card =>
	{
		var visual = card.Image is null
			? $"""<span class="feature-icon">{ui.Icon(card.Icon, "size-5")}</span>"""
			: $"""<img src="assets/images/{E(card.Image)}" alt="" width="200" height="200" class="feature-image">""";
		var external = IsExternal(card.Link) ? ui.Icon("up-right-from-square", "size-3 feature-external") : "";

		return $"""
			<a href="{E(Href(card.Link))}" class="feature-card">
			  {visual}
			  <span class="feature-title">{E(card.Title)}{external}</span>
			  <span class="feature-text">{E(card.Text)}</span>
			</a>
			""";
	}));

	/// <summary>
	/// The href of a card: an absolute URL as it is, or a guide's slug with an optional anchor
	/// (<c>reading#parsing-modes</c>) as the guide's page.
	/// </summary>
	/// <param name="link">The link from site.json.</param>
	/// <returns>The href.</returns>
	private static string Href(string link)
	{
		if (IsExternal(link))
		{
			return link;
		}

		var hash = link.IndexOf('#');

		return hash < 0 ? link + ".html" : link[..hash] + ".html" + link[hash..];
	}

	/// <summary>Whether a card's link leaves the site (the link checker does not check those).</summary>
	/// <param name="link">The link from site.json.</param>
	/// <returns>Whether the link is an absolute URL.</returns>
	private static bool IsExternal(string link) => link.Contains("://", StringComparison.Ordinal);
}
