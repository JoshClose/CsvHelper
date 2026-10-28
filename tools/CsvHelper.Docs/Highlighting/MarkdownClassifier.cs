using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CsvHelper.Docs.Highlighting;

/// <summary>
/// Colors Markdown as VS Code does: headings, list markers, inline code and comments, and the code of fenced blocks in
/// its own language.
/// </summary>
internal static partial class MarkdownClassifier
{
	/// <summary>The colored ranges of Markdown.</summary>
	/// <param name="code">The Markdown.</param>
	/// <returns>The ranges, in order.</returns>
	public static List<CodeSpan> Classify(string code)
	{
		var spans = new List<CodeSpan>();
		var lines = Line().Matches(code);
		for (var i = 0; i < lines.Count; i++)
		{
			var line = lines[i];
			var fence = Fence().Match(line.Value);
			if (fence.Success)
			{
				// The fence lines are dimmed; the code between them is colored in the language of the info string.
				spans.Add(new CodeSpan(line.Index, line.Length, TokenKind.Muted));
				var marker = fence.Groups["marker"].Value;
				var close = i + 1;
				while (close < lines.Count && !lines[close].Value.TrimStart().StartsWith(marker, System.StringComparison.Ordinal))
				{
					close++;
				}

				if (close > i + 1)
				{
					var start = lines[i + 1].Index;
					var end = lines[close - 1].Index + lines[close - 1].Length;
					foreach (var span in Highlighter.Classify(code[start..end], fence.Groups["language"].Value))
					{
						spans.Add(span with { Start = start + span.Start });
					}
				}

				if (close < lines.Count)
				{
					spans.Add(new CodeSpan(lines[close].Index, lines[close].Length, TokenKind.Muted));
				}

				i = close;
				continue;
			}

			if (Heading().IsMatch(line.Value))
			{
				spans.Add(new CodeSpan(line.Index, line.Length, TokenKind.Heading));
				continue;
			}

			if (ListMarker().Match(line.Value) is { Success: true } list)
			{
				spans.Add(new CodeSpan(line.Index + list.Groups["marker"].Index, list.Groups["marker"].Length, TokenKind.ListMarker));
			}

			foreach (Match inline in Inline().Matches(line.Value))
			{
				spans.Add(new CodeSpan(line.Index + inline.Index, inline.Length, inline.Value.StartsWith('<') ? TokenKind.Comment : TokenKind.String));
			}
		}

		return spans;
	}

	/// <summary>A line, without its line break.</summary>
	[GeneratedRegex(@"^[^\n]*", RegexOptions.Multiline)]
	private static partial Regex Line();

	/// <summary>The opening line of a fenced code block: its marker (three or more backticks or tildes) and its language.</summary>
	[GeneratedRegex(@"^\s*(?<marker>`{3,}|~{3,})\s*(?<language>[^\s`]*)")]
	private static partial Regex Fence();

	/// <summary>A heading line.</summary>
	[GeneratedRegex(@"^#{1,6}\s")]
	private static partial Regex Heading();

	/// <summary>The marker of a list item: <c>-</c>, <c>*</c>, <c>+</c> or <c>1.</c>.</summary>
	[GeneratedRegex(@"^\s*(?<marker>[-*+]|\d+[.)])\s")]
	private static partial Regex ListMarker();

	/// <summary>Inline code and HTML comments.</summary>
	[GeneratedRegex(@"`[^`]+`|<!--.*?-->")]
	private static partial Regex Inline();
}
