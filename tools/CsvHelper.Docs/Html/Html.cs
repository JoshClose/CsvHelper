using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace CsvHelper.Docs.Html;

/// <summary>HTML text helpers. Everything that is not markup goes through <see cref="E"/>.</summary>
internal static partial class Html
{
	/// <summary>Encodes text for an element or attribute value.</summary>
	public static string E(string? text) => WebUtility.HtmlEncode(text ?? "");

	/// <summary>
	/// The text of an HTML fragment, without tags, entities decoded and whitespace collapsed. Block boundaries become
	/// spaces; inline tags (code, links) are dropped without one, so punctuation after them stays attached.
	/// </summary>
	public static string Text(string html)
	{
		var text = Tags().Replace(BlockBoundary().Replace(html, " "), "");

		return Whitespace().Replace(WebUtility.HtmlDecode(text), " ").Trim();
	}

	/// <summary>The first sentence of a plain text, for summaries in lists and search results.</summary>
	public static string FirstSentence(string text)
	{
		var end = text.IndexOf(". ", System.StringComparison.Ordinal);

		return end < 0 ? text : text[..(end + 1)];
	}

	public static string Truncate(string text, int length) => text.Length <= length ? text : text[..text.LastIndexOf(' ', length)] + "…";

	/// <summary>The ids of the elements in an HTML fragment, for the link checker.</summary>
	public static IEnumerable<string> Ids(string html)
	{
		foreach (Match match in IdAttribute().Matches(html)) yield return WebUtility.HtmlDecode(match.Groups[1].Value);
	}

	[GeneratedRegex(@"<[^>]+>")]
	private static partial Regex Tags();

	[GeneratedRegex(@"<(?:br|/?(?:p|div|li|ul|ol|tr|td|th|table|h[1-6]|pre|dt|dd|section|article))\b[^>]*>", RegexOptions.IgnoreCase)]
	private static partial Regex BlockBoundary();

	[GeneratedRegex(@"\s+")]
	private static partial Regex Whitespace();

	[GeneratedRegex(@"\sid=""([^""]+)""")]
	private static partial Regex IdAttribute();
}
