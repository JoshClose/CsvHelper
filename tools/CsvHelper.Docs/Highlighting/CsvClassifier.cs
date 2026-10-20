using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CsvHelper.Docs.Highlighting;

/// <summary>
/// Colors CSV as RFC 4180 reads it: quoted fields (a quote opens one only at the start of a field, and one never closed
/// runs to the end of the text, as it does for a reader), numbers, and dimmed separators. The separator is the most common
/// of comma, semicolon, pipe and tab in the first line.
/// </summary>
internal static partial class CsvClassifier
{
	private static readonly char[] Separators = [',', ';', '|', '\t'];

	/// <summary>The colored ranges of CSV.</summary>
	/// <param name="code">The CSV.</param>
	/// <returns>The ranges, in order.</returns>
	public static List<CodeSpan> Classify(string code)
	{
		var spans = new List<CodeSpan>();
		var firstLine = code.Split('\n')[0];
		var separator = Separators.OrderByDescending(s => firstLine.Count(c => c == s)).First();
		var i = 0;
		while (i <= code.Length)
		{
			// i is at the start of a field.
			if (i < code.Length && code[i] == '"')
			{
				var end = ClosingQuote(code, i);
				AddQuoted(code, i, end, spans);
				i = end;
			}

			var start = i;
			while (i < code.Length && code[i] != separator && code[i] != '\n' && code[i] != '\r')
			{
				i++;
			}

			if (i > start && Number().IsMatch(code[start..i]))
			{
				spans.Add(new CodeSpan(start, i - start, TokenKind.Number));
			}

			if (i < code.Length && code[i] == separator)
			{
				spans.Add(new CodeSpan(i, 1, TokenKind.Muted));
			}

			i++;
		}

		return spans;
	}

	/// <summary>The end of a quoted field: the index after its closing quote, or the end of the text when it is never closed.</summary>
	/// <param name="code">The CSV.</param>
	/// <param name="open">The index of the opening quote.</param>
	/// <returns>The index after the closing quote.</returns>
	private static int ClosingQuote(string code, int open)
	{
		var i = open + 1;
		while (i < code.Length)
		{
			if (code[i] == '"')
			{
				if (i + 1 < code.Length && code[i + 1] == '"')
				{
					i += 2;
					continue;
				}

				return i + 1;
			}

			i++;
		}

		return code.Length;
	}

	/// <summary>Adds a quoted field, with its escaped quotes (<c>""</c>) colored apart.</summary>
	/// <param name="code">The CSV.</param>
	/// <param name="start">The index of the opening quote.</param>
	/// <param name="end">The index after the closing quote.</param>
	/// <param name="spans">The ranges to add to.</param>
	private static void AddQuoted(string code, int start, int end, List<CodeSpan> spans)
	{
		var position = start;
		var escape = code.IndexOf("\"\"", start + 1, System.StringComparison.Ordinal);
		while (escape >= 0 && escape + 2 < end)
		{
			spans.Add(new CodeSpan(position, escape - position, TokenKind.String));
			spans.Add(new CodeSpan(escape, 2, TokenKind.Escape));
			position = escape + 2;
			escape = code.IndexOf("\"\"", position, System.StringComparison.Ordinal);
		}

		spans.Add(new CodeSpan(position, end - position, TokenKind.String));
	}

	/// <summary>A number: an integer or a decimal, with an optional sign.</summary>
	[GeneratedRegex(@"^[+-]?(?:\d+(?:\.\d*)?|\.\d+)$")]
	private static partial Regex Number();
}
