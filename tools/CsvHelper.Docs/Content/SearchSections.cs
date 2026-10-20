using System.Collections.Generic;
using CsvHelper.Docs.Site;
using Markdig.Syntax;
using static CsvHelper.Docs.Html.Html;

namespace CsvHelper.Docs.Content;

/// <summary>Adds a Markdown page to the search: an entry for the page, and one for each of its sections.</summary>
internal static class SearchSections
{
	/// <summary>
	/// Adds an entry for the page (with the text before its first section) and one entry, of kind <c>section</c>, for each
	/// top-level h2 and h3 section, whose parent is the page.
	/// </summary>
	/// <param name="site">The site, whose search index gets the entries.</param>
	/// <param name="title">The page's title.</param>
	/// <param name="kind">The page entry's kind: guide, migration and so on.</param>
	/// <param name="path">The page's site path.</param>
	/// <param name="parent">Where the page is (shown with its entry), or null.</param>
	/// <param name="summary">One sentence about the page.</param>
	/// <param name="blocks">The page's Markdown blocks.</param>
	/// <param name="extraText">More text to search the page entry by.</param>
	public static void Add(SiteContext site, string title, string kind, string path, string? parent, string summary, IReadOnlyList<Block> blocks,
		string extraText = "")
	{
		var intro = new List<Block>();
		var sections = new List<(HeadingBlock Heading, List<Block> Blocks)>();
		foreach (var block in blocks)
		{
			if (block is HeadingBlock { Level: 2 or 3 } heading)
			{
				sections.Add((heading, []));
			}
			else if (sections.Count > 0)
			{
				sections[^1].Blocks.Add(block);
			}
			else
			{
				intro.Add(block);
			}
		}

		var introText = $"{Text(Markdown.Render(intro))} {extraText}".Trim();
		site.Search.Add(new SearchDoc(title, kind, path, parent, summary, Truncate(introText, 1500)));
		foreach (var (heading, sectionBlocks) in sections)
		{
			var text = Text(Markdown.Render(sectionBlocks));
			site.Search.Add(new SearchDoc(Markdown.InlineText(heading.Inline), "section", $"{path}#{Markdown.HeadingId(heading)}", title,
				Truncate(FirstSentence(text), 180), Truncate(text, 1500)));
		}
	}
}
