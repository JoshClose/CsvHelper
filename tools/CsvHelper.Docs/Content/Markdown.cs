using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CsvHelper.Docs.Content;

/// <summary>Markdig with GitHub's heading IDs, and the HTML clean-ups every Markdown page gets.</summary>
internal static partial class Markdown
{
	public static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
		.UsePipeTables()
		.UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
		.UseAutoLinks()
		.UseEmphasisExtras()
		.UseTaskLists()
		.UseYamlFrontMatter()
		.Build();

	public static MarkdownDocument Parse(string text) => Markdig.Markdown.Parse(text, Pipeline);

	/// <summary>
	/// The top-level blocks to publish. Blocks between <c>&lt;!-- docs:exclude --&gt;</c> and <c>&lt;!-- /docs:exclude --&gt;</c>
	/// are left out (README content meant for GitHub only), and so are blocks between <c>&lt;!-- if:released --&gt;</c> or
	/// <c>&lt;!-- if:unreleased --&gt;</c> and <c>&lt;!-- endif --&gt;</c> whose condition is false.
	/// </summary>
	public static List<Block> Publishable(MarkdownDocument document, bool released)
	{
		var blocks = new List<Block>();
		var skipping = false;
		foreach (var block in document)
		{
			if (block is HtmlBlock html && Marker(html) is { } marker)
			{
				skipping = marker switch
				{
					"docs:exclude" => true,
					"/docs:exclude" or "endif" => false,
					"if:released" => !released,
					"if:unreleased" => released,
					_ => skipping,
				};

				continue;
			}

			if (!skipping) blocks.Add(block);
		}

		return blocks;
	}

	/// <summary>The inlines of type <typeparamref name="T"/> anywhere in a block (paragraphs, lists, tables, quotes).</summary>
	public static IEnumerable<T> Inlines<T>(Block block) where T : Inline
	{
		if (block is LeafBlock { Inline: { } inline })
		{
			foreach (var item in Walk(inline).OfType<T>()) yield return item;
		}
		else if (block is ContainerBlock container)
		{
			foreach (var child in container)
			{
				foreach (var item in Inlines<T>(child)) yield return item;
			}
		}
	}

	private static IEnumerable<Inline> Walk(ContainerInline container)
	{
		foreach (var child in container)
		{
			yield return child;
			if (child is not ContainerInline nested) continue;
			foreach (var item in Walk(nested)) yield return item;
		}
	}

	/// <summary>The text of inline content (headings, link text), without formatting.</summary>
	public static string InlineText(ContainerInline? inline)
	{
		if (inline is null) return "";
		var text = new StringBuilder();
		foreach (var item in Walk(inline))
		{
			switch (item)
			{
				case LiteralInline literal:
					text.Append(literal.Content);
					break;
				case CodeInline code:
					text.Append(code.Content);
					break;
				case LineBreakInline:
					text.Append(' ');
					break;
			}
		}

		return text.ToString();
	}

	public static string HeadingId(HeadingBlock heading) => heading.GetAttributes().Id ?? "";

	/// <summary>The "On this page" entries of a page: its top-level h2 and h3 headings.</summary>
	/// <param name="blocks">The top-level blocks of the page.</param>
	/// <returns>The entries, in order.</returns>
	public static List<TocEntry> Toc(IEnumerable<Block> blocks) =>
		blocks.OfType<HeadingBlock>().Where(h => h.Level is 2 or 3).Select(h => new TocEntry(h.Level, HeadingId(h), InlineText(h.Inline))).ToList();

	/// <summary>
	/// Renders blocks of a parsed document, then applies <see cref="PostProcess"/>. A top-level before code block and the
	/// after block that follows it show side by side (<see cref="BeforeAfter"/>).
	/// </summary>
	/// <param name="blocks">The top-level blocks.</param>
	/// <param name="labels">The labels of before/after comparisons; <see cref="DiffLabels.Default"/> when null.</param>
	/// <returns>The HTML.</returns>
	public static string Render(IEnumerable<Block> blocks, DiffLabels? labels = null)
	{
		labels ??= DiffLabels.Default;
		var list = blocks as IReadOnlyList<Block> ?? blocks.ToList();
		var writer = new StringWriter();
		var renderer = new HtmlRenderer(writer);
		Pipeline.Setup(renderer);
		for (var i = 0; i < list.Count; i++)
		{
			if (list[i] is FencedCodeBlock code && BeforeAfter.Side(code) is { } side)
			{
				var after = side == BeforeAfter.Before && i + 1 < list.Count && list[i + 1] is FencedCodeBlock next && BeforeAfter.Side(next) == BeforeAfter.After
					? next
					: null;
				var html = side == BeforeAfter.Before
					? BeforeAfter.Html(BeforeAfter.ToSide(code, labels.Before), after is null ? null : BeforeAfter.ToSide(after, labels.After))
					: BeforeAfter.Html(null, BeforeAfter.ToSide(code, labels.After));
				renderer.EnsureLine();
				renderer.WriteLine(html);
				if (after is not null)
				{
					i++;
				}

				continue;
			}

			renderer.Render(list[i]);
		}

		writer.Flush();

		return PostProcess(writer.ToString());
	}

	/// <summary>Renders a whole Markdown text (release notes).</summary>
	public static string Render(string markdown) => PostProcess(Markdig.Markdown.ToHtml(markdown, Pipeline));

	/// <summary>
	/// Wraps tables for horizontal scrolling, marks code blocks without a language as plain text (highlight.js would
	/// guess), and gives rows of diagnostic tables (a first cell like CSVH001) an id.
	/// </summary>
	public static string PostProcess(string html)
	{
		html = html.Replace("<pre><code>", """<pre><code class="language-plaintext">""", StringComparison.Ordinal);
		html = html.Replace("<table>", """<div class="table-wrap"><table>""", StringComparison.Ordinal)
				   .Replace("</table>", "</table></div>", StringComparison.Ordinal);

		return DiagnosticRow().Replace(html, m => $"""<tr id="{m.Groups[1].Value.ToLowerInvariant()}">{m.Groups[0].Value[4..]}""");
	}

	private static string? Marker(HtmlBlock block)
	{
		var text = block.Lines.ToString().Trim();
		var match = MarkerComment().Match(text);

		return match.Success ? match.Groups[1].Value : null;
	}

	[GeneratedRegex(@"^<!--\s*(/?docs:exclude|if:released|if:unreleased|endif)\s*-->$")]
	private static partial Regex MarkerComment();

	[GeneratedRegex(@"<tr>\s*<td(?: [^>]*)?>([A-Z]{2,}\d{3})</td>")]
	private static partial Regex DiagnosticRow();
}
