using System;
using System.Collections.Generic;
using System.Linq;
using CsvHelper.Docs.Highlighting;
using Markdig.Syntax;
using static CsvHelper.Docs.Html.Html;

namespace CsvHelper.Docs.Content;

/// <summary>One side of a before/after comparison: its label, its code and the code's language.</summary>
/// <param name="Label">The label above the code: <c>Before · v33.1.0</c>.</param>
/// <param name="Code">The code.</param>
/// <param name="Language">The language of the code (csharp, xml); null for plain text.</param>
internal sealed record DiffSide(string Label, string Code, string? Language = "csharp");

/// <summary>The labels of the two sides of before/after comparisons.</summary>
/// <param name="Before">The label of the old code.</param>
/// <param name="After">The label of the new code.</param>
internal sealed record DiffLabels(string Before, string After)
{
	public static DiffLabels Default { get; } = new("Before", "After");
}

/// <summary>
/// The before/after convention of Markdown pages: a fenced block whose info string has the word <c>before</c> (<c>```csharp before</c>)
/// followed by one with <c>after</c> shows as old and new code side by side. On GitHub they are two ordinary code blocks.
/// Other words of the info string are flags, such as <c>nocompile</c> for the snippet check.
/// </summary>
internal static class BeforeAfter
{
	public const string Before = "before";
	public const string After = "after";

	/// <summary>Whether a code block is the old side, the new side, or neither.</summary>
	/// <param name="block">The code block.</param>
	/// <returns><see cref="Before"/>, <see cref="After"/> or null.</returns>
	public static string? Side(FencedCodeBlock block)
	{
		var words = Words(block);

		return words.Contains(Before) ? Before : words.Contains(After) ? After : null;
	}

	/// <summary>The words of a code block's info string after the language, other than before and after.</summary>
	/// <param name="block">The code block.</param>
	/// <returns>The flags, such as <c>nocompile</c>.</returns>
	public static IReadOnlyList<string> Flags(FencedCodeBlock block) => Words(block).Where(w => w is not Before and not After).ToList();

	/// <summary>The code of a block, its lines joined with line feeds.</summary>
	/// <param name="block">The code block.</param>
	/// <returns>The code.</returns>
	public static string Code(FencedCodeBlock block) => block.Lines.ToString();

	/// <summary>The side a code block shows as.</summary>
	/// <param name="block">The code block.</param>
	/// <param name="label">The label above the code.</param>
	/// <returns>The side.</returns>
	public static DiffSide ToSide(FencedCodeBlock block, string label) =>
		new(label, Code(block), block.Info);

	/// <summary>The markup of a comparison (the styles are .diff, .diff-before and .diff-after in docs/assets/site.css).</summary>
	/// <param name="before">The old side, or null when there is only a new one.</param>
	/// <param name="after">The new side, or null when there is only an old one.</param>
	/// <returns>The HTML.</returns>
	public static string Html(DiffSide? before, DiffSide? after) =>
		$"""<div class="diff not-prose">{Pane("diff-before", before)}{Pane("diff-after", after)}</div>""";

	/// <summary>
	/// Warns about code blocks that do not follow the convention: a before or after block that is not at the top level (in
	/// a list or a quote), two before blocks in a row, or a block whose language is before or after.
	/// </summary>
	/// <param name="blocks">The top-level blocks of a page.</param>
	/// <param name="page">The page, for the warnings.</param>
	public static void Check(IReadOnlyList<Block> blocks, string page)
	{
		string? previous = null;
		foreach (var block in blocks)
		{
			if (block is FencedCodeBlock code)
			{
				if (code.Info is Before or After)
				{
					Log.Warn($"{page}: the code block on line {code.Line + 1} has no language: write ```csharp {code.Info}.");
				}

				var side = Side(code);
				if (side == Before && previous == Before)
				{
					Log.Warn($"{page}: two before code blocks in a row (line {code.Line + 1}); put the after block between them.");
				}

				previous = side;
				continue;
			}

			previous = null;
			if (block is ContainerBlock container && container.Descendants<FencedCodeBlock>().FirstOrDefault(c => Side(c) is not null) is { } nested)
			{
				Log.Warn($"{page}: the {Side(nested)} code block on line {nested.Line + 1} is inside a list or quote; before and after blocks must be at the top level.");
			}
		}
	}

	/// <summary>The words of a code block's info string after the language.</summary>
	/// <param name="block">The code block.</param>
	/// <returns>The words.</returns>
	private static string[] Words(FencedCodeBlock block) =>
		(block.Arguments ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

	/// <summary>One side of a comparison: its label and its code.</summary>
	/// <param name="classes">diff-before or diff-after.</param>
	/// <param name="side">The side, or null for none.</param>
	/// <returns>The HTML; empty for no side.</returns>
	private static string Pane(string classes, DiffSide? side)
	{
		if (side is null)
		{
			return "";
		}

		// "Before · v33.1.0": the label is upper case, the version keeps its case.
		var dot = side.Label.IndexOf(" · ", StringComparison.Ordinal);
		var label = dot < 0 ? E(side.Label) : $"""{E(side.Label[..dot])} <span class="diff-version">· {E(side.Label[(dot + 3)..])}</span>""";

		return $"""<div class="{classes}"><span class="diff-label">{label}</span>{Highlighter.Block(side.Code, side.Language)}</div>""";
	}
}
