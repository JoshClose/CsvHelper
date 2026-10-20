using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CsvHelper.Docs.Highlighting;

/// <summary>Colors XML (project files): tags, attributes, their values and comments, as VS Code does.</summary>
internal static partial class XmlClassifier
{
	/// <summary>The colored ranges of XML.</summary>
	/// <param name="code">The XML.</param>
	/// <returns>The ranges, in order.</returns>
	public static List<CodeSpan> Classify(string code)
	{
		var spans = new List<CodeSpan>();
		foreach (Match match in Markup().Matches(code))
		{
			if (match.Groups["comment"].Success)
			{
				spans.Add(new CodeSpan(match.Index, match.Length, TokenKind.Comment));
				continue;
			}

			if (match.Groups["cdata"].Success)
			{
				spans.Add(new CodeSpan(match.Index, match.Length, TokenKind.String));
				continue;
			}

			Add(spans, match.Groups["open"], TokenKind.Punctuation);
			Add(spans, match.Groups["name"], TokenKind.Tag);
			var attributes = match.Groups["attributes"];
			foreach (Match attribute in Attribute().Matches(attributes.Value))
			{
				spans.Add(new CodeSpan(attributes.Index + attribute.Groups["name"].Index, attribute.Groups["name"].Length, TokenKind.Attribute));
				spans.Add(new CodeSpan(attributes.Index + attribute.Groups["value"].Index, attribute.Groups["value"].Length, TokenKind.Value));
			}

			Add(spans, match.Groups["close"], TokenKind.Punctuation);
		}

		return spans;
	}

	/// <summary>Adds a group of a match, when it matched.</summary>
	/// <param name="spans">The ranges to add to.</param>
	/// <param name="group">The group.</param>
	/// <param name="kind">Its color.</param>
	private static void Add(List<CodeSpan> spans, Group group, TokenKind kind)
	{
		if (group.Success)
		{
			spans.Add(new CodeSpan(group.Index, group.Length, kind));
		}
	}

	/// <summary>A comment, a CDATA section, or a tag (<c>&lt;name attributes&gt;</c>, <c>&lt;/name&gt;</c>, <c>&lt;?xml ?&gt;</c>).</summary>
	[GeneratedRegex(@"(?<comment><!--.*?(?:-->|$))|(?<cdata><!\[CDATA\[.*?(?:\]\]>|$))|(?<open></?|<\?|<!)(?<name>[\w:.-]+)(?<attributes>[^<>]*?)(?<close>/?>|\?>)", RegexOptions.Singleline)]
	private static partial Regex Markup();

	/// <summary>An attribute of a tag: its name and its quoted value.</summary>
	[GeneratedRegex(@"(?<name>[\w:.-]+)\s*=\s*(?<value>""[^""]*""|'[^']*')")]
	private static partial Regex Attribute();
}
