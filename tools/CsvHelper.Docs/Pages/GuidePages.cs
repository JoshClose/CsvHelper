using System.Collections.Generic;
using System.Linq;
using CsvHelper.Docs.Content;
using CsvHelper.Docs.Site;
using static CsvHelper.Docs.Html.Html;

namespace CsvHelper.Docs.Pages;

/// <summary>The guide pages, with "Edit on GitHub" and previous/next links.</summary>
internal sealed class GuidePages
{
	private readonly SiteContext site;
	private readonly GuideSet guides;
	private readonly Ui ui;

	public GuidePages(SiteContext site, GuideSet guides)
	{
		this.site = site;
		this.guides = guides;
		ui = new Ui(site);
	}

	public IEnumerable<Page> Build()
	{
		var pages = guides.Pages;
		for (var i = 0; i < pages.Count; i++)
		{
			var page = pages[i];
			var previous = i > 0 ? pages[i - 1] : null;
			var next = i < pages.Count - 1 ? pages[i + 1] : null;
			var lead = string.IsNullOrWhiteSpace(page.Description) ? null : $"<p>{E(page.Description)}</p>";
			var body = $"""
				{ui.Header(page.Icon, "Guide", page.Title, lead)}
				<div class="{Ui.Prose} prose-guide">{page.Html}</div>
				<div class="page-end">
				  <a href="{E(site.GitHubEdit(page.SourcePath))}" class="edit-link">{ui.Icon("pen-to-square", "size-3.5")}Edit this page on GitHub</a>
				  <nav class="pager" aria-label="Guides">
				    {Pager(page, previous, "Previous", "chevron-left", "pager-previous")}
				    {Pager(page, next, "Next", "chevron-right", "pager-next")}
				  </nav>
				</div>
				""";

			yield return new Page
			{
				Path = page.Path,
				Title = page.Title,
				Description = page.Description,
				Section = "guides",
				Body = body,
				Toc = page.Toc,
			};
		}
	}

	private string Pager(GuidePage from, GuidePage? to, string label, string icon, string classes)
	{
		if (to is null) return "<span></span>";

		return $"""
			<a href="{E(SiteContext.Relative(from.Path, to.Path))}" class="pager-link {classes}">
			  <span class="pager-label">{(classes == "pager-previous" ? ui.Icon(icon, "size-3") : "")}{E(label)}{(classes == "pager-next" ? ui.Icon(icon, "size-3") : "")}</span>
			  <span class="pager-title">{E(to.Title)}</span>
			</a>
			""";
	}
}
