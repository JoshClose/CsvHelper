using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsvHelper.Docs.Site;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CsvHelper.Docs.Content;

/// <summary>
/// Makes the links of Markdown content work on the site, wherever the page is: <c>#anchor</c> links go to the guide page
/// that has the heading, links to a site page are made relative to the page they are on, links to a Markdown file of the
/// repository that became a page of the site (a guide, a release's migration guide) go to that page, and other links to
/// the repository go to GitHub. Inline code that names an API symbol links to its page.
/// </summary>
internal sealed class ContentLinker
{
	private readonly SiteContext site;
	private readonly IReadOnlyDictionary<string, string> anchors;
	private readonly HashSet<string> pagePaths;
	private readonly Dictionary<string, string> sourcePages;
	private readonly CodeLinker code;

	/// <summary>Creates the linker once every guide is loaded.</summary>
	/// <param name="site">The site.</param>
	/// <param name="anchors">The headings of the guides: the heading's id and the site path of the heading (<c>page.html#id</c>).</param>
	/// <param name="pagePaths">The site paths that Markdown may link to directly (<c>reading.html</c>, <c>api/index.html</c>).</param>
	/// <param name="sourcePages">The Markdown files of the repository (forward slashes) that became pages, and the site path of each.</param>
	public ContentLinker(SiteContext site, IReadOnlyDictionary<string, string> anchors, IEnumerable<string> pagePaths,
		IReadOnlyDictionary<string, string> sourcePages)
	{
		this.site = site;
		this.anchors = anchors;
		this.pagePaths = pagePaths.ToHashSet();
		this.sourcePages = new Dictionary<string, string>(sourcePages, StringComparer.OrdinalIgnoreCase);
		code = new CodeLinker(site.Api);
	}

	/// <summary>Rewrites the links of <paramref name="blocks"/> for the page they are rendered on.</summary>
	/// <param name="blocks">The blocks.</param>
	/// <param name="page">The site path of the page: links are made relative to it.</param>
	/// <param name="sourcePath">The Markdown file the blocks come from, relative to the repository: relative links resolve against its folder.</param>
	public void RewriteLinks(IReadOnlyList<Block> blocks, string page, string sourcePath)
	{
		var sourceDirectory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') ?? "";
		// Pages may share heading names (Escape, Errors), so a page's own heading wins.
		var own = blocks.OfType<HeadingBlock>().Select(Markdown.HeadingId).ToHashSet();
		foreach (var link in blocks.SelectMany(Markdown.Inlines<LinkInline>).ToList())
		{
			var url = link.Url;
			if (string.IsNullOrEmpty(url) || url.Contains("://", StringComparison.Ordinal) || url.StartsWith("mailto:", StringComparison.Ordinal))
			{
				continue;
			}

			if (url.StartsWith('#'))
			{
				if (own.Contains(url[1..]))
				{
					continue;
				}

				if (anchors.TryGetValue(url[1..], out var target))
				{
					link.Url = SiteContext.Relative(page, target);
				}
				else
				{
					Log.Warn($"{page}: no heading for the link {url}.");
				}

				continue;
			}

			var hash = url.IndexOf('#');
			var file = hash < 0 ? url : url[..hash];
			var fragment = hash < 0 ? "" : url[hash..];
			if (pagePaths.Contains(file))
			{
				link.Url = SiteContext.Relative(page, url);
				continue;
			}

			var repoPath = Path.GetFullPath(Path.Combine(site.Repo, sourceDirectory, file));
			var relative = Path.GetRelativePath(site.Repo, repoPath).Replace('\\', '/');
			if (sourcePages.TryGetValue(relative, out var sitePage))
			{
				link.Url = SiteContext.Relative(page, sitePage) + fragment;
				continue;
			}

			// The README is sliced into several pages: a heading of it is on the page that has the heading now.
			if (string.Equals(relative, site.Config.Readme, StringComparison.OrdinalIgnoreCase) && fragment.Length > 1
				&& anchors.TryGetValue(fragment[1..], out var section))
			{
				link.Url = SiteContext.Relative(page, section);
				continue;
			}

			if (!File.Exists(repoPath) && !Directory.Exists(repoPath))
			{
				Log.Warn($"{page}: the link {url} names no file of the repository.");
			}

			link.Url = site.GitHubPath(relative, Directory.Exists(repoPath)) + fragment;
		}
	}

	/// <summary>Links the inline code of <paramref name="blocks"/> that names API symbols to their pages.</summary>
	/// <param name="blocks">The blocks.</param>
	/// <param name="page">The site path of the page the blocks are rendered on.</param>
	public void LinkCode(IReadOnlyList<Block> blocks, string page) => code.Link(blocks, page);
}
