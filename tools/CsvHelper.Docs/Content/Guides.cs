using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CsvHelper.Docs.Api;
using CsvHelper.Docs.Site;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using static CsvHelper.Docs.Html.Html;

namespace CsvHelper.Docs.Content;

internal sealed record TocEntry(int Level, string Id, string Text);

/// <summary>A guide page, made of README sections or of a Markdown file in docs/pages.</summary>
internal sealed class GuidePage
{
	public required string Slug { get; init; }
	public required string Title { get; init; }
	public required string Icon { get; init; }
	public required string Description { get; init; }

	/// <summary>The Markdown file the page comes from, relative to the repository (for "Edit on GitHub").</summary>
	public required string SourcePath { get; init; }

	public string Path => Slug + ".html";
	public List<Block> Blocks { get; } = [];
	public string Html { get; set; } = "";
	public List<TocEntry> Toc { get; } = [];
}

/// <summary>The guide pages, the README intro for the home page and the home page's code samples.</summary>
internal sealed class GuideSet
{
	public required List<GuidePage> Pages { get; init; }
	public required string IntroHtml { get; init; }
	public required List<(SampleConfig Config, string Code, string? Language)> Samples { get; init; }

	/// <summary>Rewrites links and links code in other Markdown content (release notes) the way the guides get it.</summary>
	public required ContentLinker Links { get; init; }
}

/// <summary>
/// Builds the guides: README sections sliced into pages by the page map in docs/site.json (the README stays the single
/// source), plus the pages in docs/pages. Links are rewritten for the site, and inline code that names an API
/// type or member links to its page.
/// </summary>
internal static partial class Guides
{
	public static GuideSet Load(SiteContext site, bool released)
	{
		var config = site.Config;
		var readmePath = Path.Combine(site.Repo, config.Readme);
		var readme = Markdown.Publishable(Markdown.Parse(File.ReadAllText(readmePath)), released);
		var start = readme.Count > 0 && readme[0] is HeadingBlock { Level: 1 } ? 1 : 0;
		var firstSection = readme.FindIndex(start, b => b is HeadingBlock { Level: <= 2 });
		if (firstSection < 0) firstSection = readme.Count;
		var intro = readme.GetRange(start, firstSection - start);
		var headings = Enumerable.Range(firstSection, readme.Count - firstSection)
			.Where(i => readme[i] is HeadingBlock)
			.ToDictionary(i => i, i => (HeadingBlock)readme[i]);

		// Which page each claimed README heading belongs to.
		var claims = new Dictionary<int, GuideConfig>();
		foreach (var guide in config.Guides)
		{
			foreach (var text in guide.Headings)
			{
				var matches = FindHeadings(headings, text);
				if (matches.Count != 1)
					throw new InvalidOperationException($"site.json: page '{guide.Slug}' maps README heading '{text}', which the README has {matches.Count} times.");
				if (!claims.TryAdd(matches[0], guide))
					throw new InvalidOperationException($"site.json: README heading '{text}' is mapped by two pages.");
			}
		}

		var unmapped = headings.Where(h => h.Value.Level == 2 && !claims.ContainsKey(h.Key)).Select(h => Markdown.InlineText(h.Value.Inline)).FirstOrDefault();
		if (unmapped is not null) throw new InvalidOperationException($"README heading '{unmapped}' is not mapped to a page in site.json.");

		var pages = new List<GuidePage>();
		var anchors = new Dictionary<string, string>();
		foreach (var guide in config.Guides)
		{
			var page = guide.File is null
				? ReadmePage(guide, readme, headings, claims, config.Readme, anchors)
				: FilePage(guide, site.Repo, released, anchors);
			pages.Add(page);
		}

		var links = new ContentLinker(site, anchors, PagePaths(site, pages), SourcePages(site, pages));
		foreach (var page in pages)
		{
			links.RewriteLinks(page.Blocks, page.Path, page.SourcePath);
			links.LinkCode(page.Blocks, page.Path);
			BeforeAfter.Check(page.Blocks, page.Path);
			page.Html = Markdown.Render(page.Blocks);
			page.Toc.AddRange(Markdown.Toc(page.Blocks));
			IndexForSearch(site, page);
		}

		links.RewriteLinks(intro, "index.html", config.Readme);
		links.LinkCode(intro, "index.html");
		var samples = new List<(SampleConfig, string, string?)>();
		foreach (var sample in config.Home.Samples)
		{
			var code = SampleBlocks(sample, readme, headings, pages).OfType<FencedCodeBlock>().ElementAtOrDefault(sample.Index);
			if (code is null)
			{
				var source = sample.Page is null ? "the README" : $"page '{sample.Page}'";
				throw new InvalidOperationException($"site.json: home sample '{sample.Title}' has no code block {sample.Index} under '{sample.Heading}' in {source}.");
			}

			samples.Add((sample, code.Lines.ToString(), code.Info));
		}

		return new GuideSet { Pages = pages, IntroHtml = Markdown.Render(intro), Samples = samples, Links = links };
	}

	/// <summary>The site paths that Markdown may link to directly: guides, indexes, API pages and release pages.</summary>
	/// <param name="site">The site.</param>
	/// <param name="pages">The guide pages.</param>
	/// <returns>The site paths.</returns>
	private static IEnumerable<string> PagePaths(SiteContext site, List<GuidePage> pages) =>
		pages.Select(p => p.Path)
			.Concat(["index.html", "api/index.html", "changelog.html", "changelog/unreleased.html"])
			.Concat(site.Api.Types.Select(SiteContext.ApiPath))
			.Concat(site.Releases.Releases.Select(r => r.ChangelogPath))
			.Concat(site.Releases.Releases.Where(r => r.HasMigrationGuide).Select(r => r.MigrationPath));

	/// <summary>
	/// The Markdown files of the repository that became pages, so that links to them (which work on GitHub) go to the page
	/// on the site: the files of docs/pages, and the release notes in docs/releases.
	/// </summary>
	/// <param name="site">The site.</param>
	/// <param name="pages">The guide pages.</param>
	/// <returns>The repository path of each file (forward slashes) and the site path of its page.</returns>
	private static Dictionary<string, string> SourcePages(SiteContext site, List<GuidePage> pages)
	{
		var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var page in pages.Where(p => !string.Equals(p.SourcePath, site.Config.Readme, StringComparison.OrdinalIgnoreCase)))
		{
			sources[page.SourcePath] = page.Path;
		}

		foreach (var release in site.Releases.Releases)
		{
			if (release.ChangelogNotesPath is { } changelog)
			{
				sources[changelog] = release.ChangelogPath;
			}

			if (release.MigrationNotesPath is { } migration)
			{
				sources[migration] = release.MigrationPath;
			}
		}

		return sources;
	}

	private static GuidePage ReadmePage(GuideConfig guide, List<Block> readme, Dictionary<int, HeadingBlock> headings,
		Dictionary<int, GuideConfig> claims, string readmePath, Dictionary<string, string> anchors)
	{
		var page = new GuidePage
		{
			Slug = guide.Slug,
			Title = guide.Title ?? guide.Slug,
			Icon = guide.Icon ?? "book",
			Description = guide.Description ?? "",
			SourcePath = readmePath,
		};
		var single = guide.Headings.Count == 1;
		foreach (var text in guide.Headings)
		{
			var index = FindHeadings(headings, text)[0];
			var heading = headings[index];
			// One section: its heading becomes the page title and its subsections the page's sections.
			// Several: each keeps its heading, as a section of the page.
			var shift = heading.Level - (single ? 1 : 2);
			var end = SectionEnd(readme, headings, index);
			for (var i = single ? index + 1 : index; i < end; i++)
			{
				if (i > index && claims.ContainsKey(i))
				{
					i = SectionEnd(readme, headings, i) - 1;
					continue;
				}

				var block = readme[i];
				if (block is HeadingBlock sub)
				{
					anchors[Markdown.HeadingId(sub)] = $"{page.Path}#{Markdown.HeadingId(sub)}";
					sub.Level = Math.Max(2, sub.Level - shift);
				}

				page.Blocks.Add(block);
			}

			anchors[Markdown.HeadingId(heading)] = single ? page.Path : $"{page.Path}#{Markdown.HeadingId(heading)}";
		}

		return page;
	}

	private static GuidePage FilePage(GuideConfig guide, string repo, bool released, Dictionary<string, string> anchors)
	{
		var path = Path.Combine(repo, guide.File!);
		var document = Markdown.Parse(File.ReadAllText(path));
		var front = FrontMatter(document);
		var blocks = Markdown.Publishable(document, released).Where(b => b is not YamlFrontMatterBlock).ToList();
		var title = front.GetValueOrDefault("title");
		if (blocks.FirstOrDefault() is HeadingBlock { Level: 1 } h1)
		{
			title ??= Markdown.InlineText(h1.Inline);
			blocks.RemoveAt(0);
		}

		var page = new GuidePage
		{
			Slug = guide.Slug,
			Title = guide.Title ?? title ?? guide.Slug,
			Icon = guide.Icon ?? front.GetValueOrDefault("icon") ?? "book",
			Description = guide.Description ?? front.GetValueOrDefault("description") ?? "",
			SourcePath = guide.File!.Replace('\\', '/'),
		};
		page.Blocks.AddRange(blocks);
		foreach (var heading in blocks.OfType<HeadingBlock>()) anchors.TryAdd(Markdown.HeadingId(heading), $"{page.Path}#{Markdown.HeadingId(heading)}");

		return page;
	}

	private static Dictionary<string, string> FrontMatter(MarkdownDocument document)
	{
		var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (document.Descendants<YamlFrontMatterBlock>().FirstOrDefault() is not { } block) return values;
		foreach (var line in block.Lines.ToString().Split('\n'))
		{
			var colon = line.IndexOf(':');
			if (colon <= 0) continue;
			values[line[..colon].Trim()] = line[(colon + 1)..].Trim().Trim('"', '\'');
		}

		return values;
	}

	/// <summary>The README headings with this text. <c>## Reading</c> also requires the level.</summary>
	private static List<int> FindHeadings(Dictionary<int, HeadingBlock> headings, string spec)
	{
		var level = spec.TakeWhile(c => c == '#').Count();
		var text = spec[level..].Trim();

		return headings.Where(h => (level == 0 || h.Value.Level == level) && Markdown.InlineText(h.Value.Inline).Trim() == text)
			.Select(h => h.Key).Order().ToList();
	}

	/// <summary>The index after the last block of the section that starts at heading <paramref name="index"/>.</summary>
	private static int SectionEnd(List<Block> blocks, Dictionary<int, HeadingBlock> headings, int index)
	{
		var level = headings[index].Level;
		var next = headings.Keys.Where(i => i > index && headings[i].Level <= level).DefaultIfEmpty(blocks.Count).Min();

		return next;
	}

	private static IEnumerable<Block> Section(List<Block> blocks, Dictionary<int, HeadingBlock> headings, int index) =>
		blocks.Skip(index + 1).Take(SectionEnd(blocks, headings, index) - index - 1);

	/// <summary>
	/// The blocks that a home sample takes its code from: the section of its heading in the README or, with
	/// <see cref="SampleConfig.Page"/>, in that guide page, where an empty heading means the page's introduction.
	/// </summary>
	/// <param name="sample">The sample of docs/site.json.</param>
	/// <param name="readme">The publishable blocks of the README.</param>
	/// <param name="headings">The headings of the README's sections, by block index.</param>
	/// <param name="pages">The guide pages.</param>
	/// <returns>The blocks of the section; none when the heading does not exist.</returns>
	/// <exception cref="InvalidOperationException">The sample names a page that is not a guide.</exception>
	private static IEnumerable<Block> SampleBlocks(SampleConfig sample, List<Block> readme, Dictionary<int, HeadingBlock> headings,
		List<GuidePage> pages)
	{
		var blocks = readme;
		if (sample.Page is not null)
		{
			var page = pages.Find(p => p.Slug == sample.Page)
				?? throw new InvalidOperationException($"site.json: home sample '{sample.Title}' names the page '{sample.Page}', which is not a guide.");
			blocks = page.Blocks;
			if (sample.Heading.Length == 0) return blocks.TakeWhile(b => b is not HeadingBlock);
			headings = blocks.Select((block, index) => (block, index)).Where(b => b.block is HeadingBlock)
				.ToDictionary(b => b.index, b => (HeadingBlock)b.block);
		}

		var heading = FindHeadings(headings, sample.Heading).FirstOrDefault(-1);

		return heading < 0 ? [] : Section(blocks, headings, heading);
	}

	/// <summary>Adds a guide to the search: the page, its sections, and the rows of its diagnostic tables (CSVH001).</summary>
	private static void IndexForSearch(SiteContext site, GuidePage page)
	{
		SearchSections.Add(site, page.Title, "guide", page.Path, null, page.Description, page.Blocks);
		foreach (Match row in DiagnosticRow().Matches(page.Html))
		{
			var cells = Text(row.Groups[2].Value);
			site.Search.Add(new SearchDoc(row.Groups[1].Value.ToUpperInvariant(), "diagnostic", $"{page.Path}#{row.Groups[1].Value}", page.Title,
				Truncate(cells[(row.Groups[1].Value.Length + 1)..].Trim(), 180), cells));
		}
	}

	[GeneratedRegex(@"<tr id=""([a-z]{2,}\d{3})"">(.*?)</tr>", RegexOptions.Singleline)]
	private static partial Regex DiagnosticRow();
}
