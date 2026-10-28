using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace CsvHelper.Docs.Highlighting;

/// <summary>Renders Markdown code blocks colored by <see cref="Highlighter"/>, in place of Markdig's plain ones.</summary>
internal sealed class HighlightedCodeBlockRenderer : HtmlObjectRenderer<CodeBlock>
{
	/// <summary>Writes a code block.</summary>
	/// <param name="renderer">The renderer.</param>
	/// <param name="block">The code block: fenced (with a language) or indented.</param>
	protected override void Write(HtmlRenderer renderer, CodeBlock block)
	{
		var language = block is FencedCodeBlock fenced ? fenced.Info : null;
		renderer.EnsureLine();
		renderer.WriteLine(Highlighter.Block(block.Lines.ToString(), language));
	}
}
