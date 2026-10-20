using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CsvHelper.Docs.Pages;
using CsvHelper.Docs.Site;
using static CsvHelper.Docs.Html.Html;

namespace CsvHelper.Docs.Output;

/// <summary>
/// The files around the pages: 404, sitemap, robots.txt, favicon, search index, scripts, images, vendor files and the
/// files copied as they are to the site root.
/// </summary>
internal static class SiteExtras
{
	public static Page NotFound(SiteContext site)
	{
		var ui = new Ui(site);
		var body = $"""
			{ui.Header("signs-post", "404", "Page not found", "<p>The page you asked for does not exist. It may have moved when the API or the guides changed.</p>")}
			<div class="not-found-actions">
			  <a href="index.html" class="button-primary">{ui.Icon("arrow-left", "size-4")}Home</a>
			  <button type="button" class="button-secondary" data-search-open>{ui.Icon("magnifying-glass", "size-4")}Search the docs</button>
			</div>
			""";

		// GitHub Pages serves this file at any missing path, so its links resolve against the site root.
		return new Page { Path = "404.html", Title = "Page not found", Section = "none", Body = body, BaseHref = new Uri(site.BaseUrl).AbsolutePath };
	}

	public static void Write(SiteContext site, string nodeModules)
	{
		var output = site.OutDir;
		var lastModified = site.BuildDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		var sitemap = new StringBuilder("""<?xml version="1.0" encoding="UTF-8"?>""" + "\n" + """<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""" + "\n");
		foreach (var page in site.Pages.Where(p => p != "404.html").Order(StringComparer.Ordinal))
		{
			var url = site.BaseUrl + (page == "index.html" ? "" : page);
			sitemap.Append($"  <url><loc>{E(url)}</loc><lastmod>{lastModified}</lastmod></url>\n");
		}

		File.WriteAllText(Path.Combine(output, "sitemap.xml"), sitemap.Append("</urlset>\n").ToString());
		File.WriteAllText(Path.Combine(output, "robots.txt"), $"User-agent: *\nAllow: /\nSitemap: {site.BaseUrl}sitemap.xml\n");
		File.WriteAllText(Path.Combine(output, "favicon.svg"), site.Logos.Favicon());
		WriteSearchIndex(site);

		var assets = Path.Combine(output, "assets");
		var vendor = Path.Combine(assets, "vendor");
		Directory.CreateDirectory(vendor);
		foreach (var file in new[] { "site.js", "search.js" }) File.Copy(Path.Combine(site.Repo, "docs", "assets", file), Path.Combine(assets, file), overwrite: true);
		File.Copy(Path.Combine(nodeModules, "minisearch", "dist", "umd", "index.js"), Path.Combine(vendor, "minisearch.js"), overwrite: true);
		File.Copy(Path.Combine(nodeModules, "@highlightjs", "cdn-assets", "highlight.min.js"), Path.Combine(vendor, "highlight.min.js"), overwrite: true);
		File.Copy(Path.Combine(nodeModules, "@highlightjs", "cdn-assets", "languages", "powershell.min.js"), Path.Combine(vendor, "powershell.min.js"), overwrite: true);

		// Images the pages show, such as the sponsors' logos on the home page (before the link check, which checks src).
		var images = Path.Combine(assets, "images");
		Directory.CreateDirectory(images);
		foreach (var image in Directory.EnumerateFiles(Path.Combine(site.Repo, "docs", "assets", "images")))
		{
			File.Copy(image, Path.Combine(images, Path.GetFileName(image)), overwrite: true);
		}

		// Files that must be at the site root byte for byte, such as Google Search Console's verification file.
		foreach (var file in Directory.EnumerateFiles(Path.Combine(site.Repo, "docs", "assets", "root")))
		{
			var target = Path.Combine(output, Path.GetFileName(file));
			if (File.Exists(target))
			{
				throw new InvalidOperationException($"docs/assets/root/{Path.GetFileName(file)} would replace a file the generator writes.");
			}

			File.Copy(file, target);
		}
	}

	/// <summary>
	/// The search index as a script that sets a global, so the site also searches when opened from disk (file:// pages
	/// cannot fetch JSON). The palette loads it the first time it opens.
	/// </summary>
	private static void WriteSearchIndex(SiteContext site)
	{
		var ui = new Ui(site);
		var index = new
		{
			docs = site.Search.Select((d, i) => new { id = i, title = d.Title, kind = d.Kind, url = d.Url, parent = d.Parent, summary = d.Summary, text = d.Text }),
			icons = Ui.SearchKinds.ToDictionary(k => k, k => ui.Icon(Ui.KindIcon(k), "size-4")),
		};
		var json = JsonSerializer.Serialize(index);
		File.WriteAllText(Path.Combine(site.OutDir, "search-index.js"), $"window.CSVHELPER_SEARCH = {json};\n");
	}
}
