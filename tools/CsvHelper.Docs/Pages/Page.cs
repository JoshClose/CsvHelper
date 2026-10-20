using System.Collections.Generic;
using CsvHelper.Docs.Content;

namespace CsvHelper.Docs.Pages;

/// <summary>A page of the site before the layout wraps it.</summary>
internal sealed class Page
{
	/// <summary>The path from the site root: <c>api/csvreader.html</c>.</summary>
	public required string Path { get; init; }

	public required string Title { get; init; }
	public string Description { get; init; } = "";

	/// <summary>home, guides, api or releases: picks the active header link.</summary>
	public required string Section { get; init; }

	/// <summary>The main content.</summary>
	public required string Body { get; init; }

	public IReadOnlyList<TocEntry> Toc { get; init; } = [];

	/// <summary>A full-width page without sidebars (the home page).</summary>
	public bool Wide { get; init; }

	/// <summary>For 404.html, which GitHub Pages serves at any missing path: links resolve against the site root.</summary>
	public string? BaseHref { get; init; }
}
