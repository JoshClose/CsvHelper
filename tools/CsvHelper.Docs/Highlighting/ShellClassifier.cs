using System.Collections.Generic;

namespace CsvHelper.Docs.Highlighting;

/// <summary>
/// Colors shell commands (bash, PowerShell, cmd): the command of each line or pipeline stage, strings, variables and
/// comments.
/// </summary>
internal static class ShellClassifier
{
	/// <summary>The colored ranges of shell commands.</summary>
	/// <param name="code">The commands.</param>
	/// <returns>The ranges, in order.</returns>
	public static List<CodeSpan> Classify(string code)
	{
		var spans = new List<CodeSpan>();
		var command = true;
		var i = 0;
		while (i < code.Length)
		{
			var c = code[i];
			if (c == '\n' || c == '|' || c == ';' || c == '&')
			{
				command = true;
				i++;
				continue;
			}

			if (char.IsWhiteSpace(c))
			{
				i++;
				continue;
			}

			// A comment starts a line or follows a space: a # in a word (a URL, an issue number) is part of it.
			if (c == '#' && (i == 0 || char.IsWhiteSpace(code[i - 1])))
			{
				var end = code.IndexOf('\n', i);
				end = end < 0 ? code.Length : end;
				spans.Add(new CodeSpan(i, end - i, TokenKind.Comment));
				i = end;
				continue;
			}

			if (c == '"' || c == '\'')
			{
				var end = code.IndexOf(c, i + 1);
				end = end < 0 ? code.Length : end + 1;
				spans.Add(new CodeSpan(i, end - i, TokenKind.String));
				command = false;
				i = end;
				continue;
			}

			var start = i;
			while (i < code.Length && !char.IsWhiteSpace(code[i]) && code[i] is not ('|' or ';' or '"' or '\''))
			{
				i++;
			}

			var word = code[start..i];
			if (word.StartsWith('$'))
			{
				spans.Add(new CodeSpan(start, i - start, TokenKind.Variable));
			}
			else if (command)
			{
				spans.Add(new CodeSpan(start, i - start, TokenKind.Method));
			}

			command = false;
		}

		return spans;
	}
}
