using System;
using System.Globalization;
using System.Linq;
using System.Text;
using CsvHelper.Docs.Content;
using CsvHelper.Docs.Site;
using static CsvHelper.Docs.Html.Html;

namespace CsvHelper.Docs.Pages;

/// <summary>Wraps page content in the site's shell: head, header, sidebar, "On this page", footer and the search dialog.</summary>
internal sealed class Layout
{
	// Runs before the CSS loads, so a dark page never flashes white.
	private const string ThemeScript =
		"(function(){var d=document.documentElement,t=null;try{t=localStorage.getItem('theme')}catch(e){}" +
		"if(t==='dark'||(t!=='light'&&window.matchMedia('(prefers-color-scheme: dark)').matches))d.classList.add('dark')})();";

	private readonly SiteContext site;
	private readonly GuideSet guides;
	private readonly Ui ui;

	public Layout(SiteContext site, GuideSet guides)
	{
		this.site = site;
		this.guides = guides;
		ui = new Ui(site);
	}

	public string Render(Page page)
	{
		var root = page.BaseHref is null ? SiteContext.Relative(page.Path, "") : "";
		var title = page.Path == "index.html" ? $"{site.Config.Title}: {site.Config.Tagline}" : $"{page.Title} · {site.Config.Title}";
		var description = string.IsNullOrWhiteSpace(page.Description) ? site.Config.Description : page.Description;
		var canonical = site.BaseUrl + (page.Path == "index.html" ? "" : page.Path);
		var main = page.Wide
			? $"""<main id="main">{page.Body}</main>"""
			: $"""
			  <div class="layout">
			    <aside id="sidebar" class="sidebar" data-sidebar aria-label="Documentation">{Sidebar(page, root)}</aside>
			    <main id="main" class="layout-main">{Banner(page, root)}{page.Body}</main>
			    <aside class="layout-toc">{Toc(page)}</aside>
			  </div>
			  <div class="sidebar-backdrop" data-sidebar-backdrop hidden></div>
			  """;

		return $"""
			<!DOCTYPE html>
			<html lang="en">
			<head>
			<meta charset="utf-8">
			<meta name="viewport" content="width=device-width, initial-scale=1">
			{(page.BaseHref is null ? "" : $"""<base href="{E(page.BaseHref)}">""")}
			<title>{E(title)}</title>
			<meta name="description" content="{E(description)}">
			<link rel="canonical" href="{E(canonical)}">
			<meta property="og:type" content="website">
			<meta property="og:site_name" content="{E(site.Config.Title)}">
			<meta property="og:title" content="{E(page.Title)}">
			<meta property="og:description" content="{E(description)}">
			<meta property="og:url" content="{E(canonical)}">
			<meta name="theme-color" content="#00A650">
			<link rel="icon" href="{root}favicon.svg" type="image/svg+xml">
			<script>{ThemeScript}</script>
			<link rel="stylesheet" href="{root}assets/site.css">
			<script src="{root}assets/vendor/highlight.min.js" defer></script>
			<script src="{root}assets/vendor/powershell.min.js" defer></script>
			<script src="{root}assets/site.js" defer></script>
			<script src="{root}assets/search.js" defer></script>
			</head>
			<body class="site-body">
			<a href="#main" class="skip-link">Skip to content</a>
			{Header(page, root)}
			{main}
			{Footer(root)}
			{SearchDialog()}
			<template id="icon-copy">{ui.Icon("copy")}</template>
			<template id="icon-check">{ui.Icon("check")}</template>
			<template id="icon-link">{ui.Icon("link", "size-3.5")}</template>
			</body>
			</html>
			""";
	}

	private string Header(Page page, string root)
	{
		var links = new StringBuilder();
		foreach (var (section, label, path) in new[]
		{
			("guides", "Guides", "getting-started.html"),
			("api", "API", "api/index.html"),
			("releases", "Releases", "changelog.html"),
		})
		{
			var current = page.Section == section ? " aria-current=\"true\"" : "";
			links.Append($"""<a href="{root}{path}" class="header-link"{current}>{label}</a>""");
		}

		var menu = page.Wide ? "" : $"""<button type="button" class="icon-button lg:hidden" data-sidebar-open aria-label="Open the navigation" aria-controls="sidebar">{ui.Icon("bars", "size-5")}</button>""";

		return $"""
			<header class="site-header">
			  <div class="site-header-bar"></div>
			  <div class="site-header-inner">
			    {menu}
			    <a href="{root}index.html" class="wordmark" aria-label="{E(site.Config.Title)} home">{site.Logos.Logo("header-logo")}</a>
			    <nav aria-label="Main" class="header-nav">{links}</nav>
			    <div class="header-actions">
			      <button type="button" class="search-button" data-search-open aria-label="Search the documentation">
			        {ui.Icon("magnifying-glass")}<span class="search-button-text">Search docs…</span><kbd class="search-kbd" data-search-kbd>Ctrl K</kbd>
			      </button>
			      <a href="{E(site.Config.Repository)}" class="icon-button" aria-label="{E(site.Config.Title)} on GitHub">{ui.Icon("brands/github", "size-5")}</a>
			      <button type="button" class="icon-button" data-theme-toggle aria-label="Switch between light and dark mode">
			        {ui.Icon("moon", "size-5 dark:hidden")}{ui.Icon("sun", "size-5 hidden dark:block")}
			      </button>
			    </div>
			  </div>
			</header>
			""";
	}

	private string Sidebar(Page page, string root)
	{
		var html = new StringBuilder();
		html.Append($"""
			<div class="sidebar-top lg:hidden">
			  <span class="sidebar-title">Navigation</span>
			  <button type="button" class="icon-button" data-sidebar-close aria-label="Close the navigation">{ui.Icon("xmark", "size-5")}</button>
			</div>
			<nav class="sidebar-nav">
			""");
		html.Append(Group("book", "Guides"));
		foreach (var guide in guides.Pages) html.Append(Link(page, root, guide.Path, guide.Title, guide.Icon));
		html.Append("</ul>");
		html.Append(Group("code", "API reference"));
		html.Append(Link(page, root, "api/index.html", "Overview", "layer-group"));
		foreach (var group in site.Api.Types.GroupBy(t => t.Group).OrderBy(g => GroupOrder(g.Key)))
		{
			html.Append($"""<li class="nav-subheading">{E(group.Key)}</li>""");
			foreach (var type in group) html.Append(Link(page, root, SiteContext.ApiPath(type), type.Name, null));
		}

		html.Append("</ul>");
		html.Append(Group("code-branch", "Releases"));
		var release = page.Path.StartsWith("changelog/", StringComparison.Ordinal) || page.Path.StartsWith("migration/", StringComparison.Ordinal);
		html.Append(Link(page, root, "changelog.html", "Changelog", "clock-rotate-left", release));
		html.Append("</ul></nav>");

		return html.ToString();
	}

	private string Group(string icon, string title) =>
		$"""<p class="nav-heading">{ui.Icon(icon, "size-3.5")}<span>{E(title)}</span></p><ul class="nav-list">""";

	private string Link(Page page, string root, string path, string text, string? icon, bool active = false)
	{
		var current = active || page.Path == path;
		var iconHtml = icon is null ? "" : ui.Icon(icon, "size-3.5 nav-icon");

		return $"""<li><a href="{root}{E(path)}" class="nav-link"{(current ? " aria-current=\"page\"" : "")}>{iconHtml}<span>{E(text)}</span></a></li>""";
	}

	public static int GroupOrder(string group) => Array.IndexOf(["Classes", "Records", "Structs", "Attributes", "Exceptions", "Enums", "Delegates", "Interfaces"], group) switch
	{
		-1 => 99,
		var i => i,
	};

	private string Toc(Page page)
	{
		if (page.Toc.Count == 0) return "";
		var items = new StringBuilder();
		foreach (var entry in page.Toc)
		{
			items.Append($"""<li><a href="#{E(entry.Id)}" class="toc-link{(entry.Level > 2 ? " toc-sub" : "")}" data-toc-link>{E(entry.Text)}</a></li>""");
		}

		return $"""
			<nav class="toc" aria-label="On this page" data-toc>
			  <p class="toc-heading">On this page</p>
			  <ul class="toc-list">{items}</ul>
			  <a href="#main" class="toc-top">{ui.Icon("arrow-turn-up", "size-3")}Back to top</a>
			</nav>
			""";
	}

	/// <summary>On API pages: a note that master has API changes the latest release doesn't.</summary>
	private string Banner(Page page, string root)
	{
		if (page.Section != "api" || !site.Releases.HasUnreleasedApiChanges) return "";

		return $"""
			<div class="banner">
			  {ui.Icon("circle-info", "size-4 shrink-0 mt-0.5")}
			  <p>This reference documents <strong>{E(site.Config.Branch)}</strong>, which has API changes since {E(site.Releases.Latest!.Name)}. <a href="{root}changelog/unreleased.html">See what is unreleased</a>.</p>
			</div>
			""";
	}

	private string Footer(string root)
	{
		var date = site.BuildDate.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture);

		return $"""
			<footer class="site-footer">
			  <div class="site-footer-inner">
			    <div class="footer-brand">
			      <a href="{root}index.html" class="wordmark" aria-label="{E(site.Config.Title)} home">{site.Logos.Logo("footer-logo")}</a>
			      <p>{E(site.Config.Tagline)}</p>
			      <p>Free for commercial use. Dual licensed under <a href="https://opensource.org/licenses/MS-PL">MS-PL</a> and <a href="https://opensource.org/licenses/Apache-2.0">Apache 2.0</a>.</p>
			    </div>
			    <div class="footer-meta">
			      <p>Built from <a href="{E(site.GitHubCommit(site.Sha))}">{E(site.ShortSha)}</a> on {E(date)}.</p>
			      <p>Icons by <a href="https://fontawesome.com">Font Awesome Free</a>, <a href="https://creativecommons.org/licenses/by/4.0/">CC BY 4.0</a>.</p>
			    </div>
			  </div>
			</footer>
			""";
	}

	private string SearchDialog() => $"""
		<dialog class="search-dialog" data-search-dialog aria-label="Search the documentation">
		  <div class="search-panel">
		    <div class="search-field">
		      {ui.Icon("magnifying-glass", "size-4 text-ink-500")}
		      <input type="search" class="search-input" data-search-input placeholder="Search guides, types and members…" autocomplete="off" spellcheck="false"
		             role="combobox" aria-expanded="true" aria-controls="search-results" aria-autocomplete="list">
		      <button type="button" class="search-close" data-search-close aria-label="Close the search"><kbd>Esc</kbd></button>
		    </div>
		    <div id="search-results" class="search-results" role="listbox" aria-label="Results" data-search-results></div>
		    <div class="search-footer">
		      <span><kbd>↑</kbd><kbd>↓</kbd> to move</span><span><kbd>Enter</kbd> to open</span><span><kbd>Esc</kbd> to close</span>
		    </div>
		  </div>
		</dialog>
		""";
}
