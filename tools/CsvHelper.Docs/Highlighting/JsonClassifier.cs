using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CsvHelper.Docs.Highlighting;

/// <summary>Colors JSON: property names, strings, numbers, literals and (JSONC) comments, as VS Code does.</summary>
internal static partial class JsonClassifier
{
	/// <summary>The colored ranges of JSON.</summary>
	/// <param name="code">The JSON.</param>
	/// <returns>The ranges, in order.</returns>
	public static List<CodeSpan> Classify(string code)
	{
		var spans = new List<CodeSpan>();
		foreach (Match match in Token().Matches(code))
		{
			var kind = match.Groups["comment"].Success ? TokenKind.Comment
				: match.Groups["key"].Success ? TokenKind.Variable
				: match.Groups["string"].Success ? TokenKind.String
				: match.Groups["number"].Success ? TokenKind.Number
				: TokenKind.Keyword;
			var group = match.Groups["key"].Success ? match.Groups["key"] : match;
			spans.Add(new CodeSpan(group.Index, group.Length, kind));
		}

		return spans;
	}

	/// <summary>A comment, a property name, a string, a number, or true, false or null.</summary>
	[GeneratedRegex(@"(?<comment>//[^\n]*|/\*.*?(?:\*/|$))|(?<key>""(?:[^""\\\n]|\\.)*"")(?=\s*:)|(?<string>""(?:[^""\\\n]|\\.)*""?)|(?<number>-?\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b)|\b(?:true|false|null)\b", RegexOptions.Singleline)]
	private static partial Regex Token();
}
