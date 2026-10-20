using System;
using System.IO;
using CsvHelper.Docs.Site;
using static CsvHelper.Docs.Html.Html;

namespace CsvHelper.Docs.Output;

/// <summary>
/// Pages at the old site's addresses (folders such as <c>getting-started/</c>) that send the browser on to the page that
/// replaced them, so that links to the old site from Stack Overflow and elsewhere keep working. They come from
/// <see cref="SiteConfig.Redirects"/>, stay out of the sitemap and the search, and their link to the new page goes through
/// the link checker.
/// </summary>
internal static class Redirects
{
	/// <summary>Writes a redirect page for each entry of <see cref="SiteConfig.Redirects"/>, after the site's own pages.</summary>
	/// <param name="site">The site.</param>
	/// <returns>The number of redirect pages.</returns>
	public static int Write(SiteContext site)
	{
		foreach (var (from, to) in site.Config.Redirects)
		{
			if (to.Length == 0 || to.StartsWith('/') || to.Contains("://", StringComparison.Ordinal))
			{
				throw new InvalidOperationException($"The redirect from {from} must go to a page of the site, relative to its root, not '{to}'.");
			}

			var path = from.Trim('/') + "/index.html";
			var file = Path.Combine(site.OutDir, path);
			if (File.Exists(file))
			{
				throw new InvalidOperationException($"The redirect from {from} would replace the page {path}.");
			}

			Directory.CreateDirectory(Path.GetDirectoryName(file)!);
			File.WriteAllText(file, Page(site, path, to));
		}

		return site.Config.Redirects.Count;
	}

	/// <summary>
	/// A page that sends the browser to <paramref name="to"/> at once (a meta refresh, which also works without scripts
	/// and from disk), tells search engines where the page went, and links to it in case the refresh does not happen.
	/// </summary>
	/// <param name="site">The site.</param>
	/// <param name="path">The redirect page's own path, which the link is relative to.</param>
	/// <param name="to">The new page, relative to the site root, with an optional anchor.</param>
	/// <returns>The page's HTML.</returns>
	private static string Page(SiteContext site, string path, string to)
	{
		var href = E(SiteContext.Relative(path, to));
		var url = E(site.BaseUrl + to);

		return $"""
			<!DOCTYPE html>
			<html lang="en">
			<head>
			<meta charset="utf-8">
			<title>Moved · {E(site.Config.Title)}</title>
			<meta name="robots" content="noindex">
			<link rel="canonical" href="{url}">
			<meta http-equiv="refresh" content="0; url={href}">
			</head>
			<body>
			<p>This page has moved to <a href="{href}">{url}</a>.</p>
			</body>
			</html>
			""";
	}
}
