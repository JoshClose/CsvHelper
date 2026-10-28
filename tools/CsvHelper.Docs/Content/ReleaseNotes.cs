using System.Collections.Generic;
using System.Linq;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;

namespace CsvHelper.Docs.Content;

/// <summary>Release notes rendered for a page: the HTML, the "On this page" entries of their headings, and their blocks (for the search).</summary>
/// <param name="Html">The HTML.</param>
/// <param name="Toc">The h2 and h3 headings.</param>
/// <param name="Blocks">The top-level blocks that were rendered.</param>
internal sealed record RenderedNotes(string Html, List<TocEntry> Toc, List<Block> Blocks);

/// <summary>
/// Renders the hand-written notes of a release (docs/releases/&lt;tag&gt;/migration.md and changelog.md) the way guides are
/// rendered: conditional blocks, links that work on the site, API links for inline code, before/after comparisons and
/// headings for "On this page".
/// </summary>
internal static class ReleaseNotes
{
	/// <summary>Renders release notes for a page.</summary>
	/// <param name="text">The Markdown.</param>
	/// <param name="sourcePath">The file the notes come from, relative to the repository (relative links resolve against its folder); empty for a tag's message.</param>
	/// <param name="page">The site path of the page the notes are on.</param>
	/// <param name="released">Whether the release is out (for <c>&lt;!-- if:released --&gt;</c>); false for a preview.</param>
	/// <param name="links">The links of the site.</param>
	/// <param name="linkCode">Whether inline code that names an API symbol links to the API reference (which documents the current API).</param>
	/// <param name="labels">The labels of before/after comparisons.</param>
	/// <param name="reservedIds">The ids the page already uses: a heading with one of them is a warning.</param>
	/// <returns>The rendered notes.</returns>
	public static RenderedNotes Render(string text, string sourcePath, string page, bool released, ContentLinker links, bool linkCode,
		DiffLabels labels, IReadOnlySet<string> reservedIds)
	{
		var blocks = Markdown.Publishable(Markdown.Parse(text), released).Where(b => b is not YamlFrontMatterBlock).ToList();
		// The title is for GitHub; the page has its own.
		if (blocks.FirstOrDefault() is HeadingBlock { Level: 1 })
		{
			blocks.RemoveAt(0);
		}

		links.RewriteLinks(blocks, page, sourcePath);
		if (linkCode)
		{
			links.LinkCode(blocks, page);
		}

		BeforeAfter.Check(blocks, page);
		foreach (var heading in blocks.OfType<HeadingBlock>().Where(h => reservedIds.Contains(Markdown.HeadingId(h))))
		{
			Log.Warn($"{page}: the heading '{Markdown.InlineText(heading.Inline)}' has the id '{Markdown.HeadingId(heading)}', which the page uses already; rename the heading.");
		}

		return new RenderedNotes(Markdown.Render(blocks, labels), Markdown.Toc(blocks), blocks);
	}
}
