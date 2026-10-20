using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CsvHelper.Docs.Html;

/// <summary>URL fragments and file names.</summary>
internal static partial class Slugs
{
	/// <summary>Lowercase letters and digits joined by dashes: <c>GetString(int)</c> becomes <c>getstring-int</c>, <c>T?</c> <c>t-nullable</c>.</summary>
	public static string Slug(string text)
	{
		var slug = NonWord().Replace(text.Replace("?", " nullable").ToLowerInvariant(), "-").Trim('-');

		return slug.Length == 0 ? "item" : slug;
	}

	/// <summary>A slug that is not in <paramref name="used"/> yet (a number is appended), which it is added to.</summary>
	public static string Unique(string text, HashSet<string> used)
	{
		var slug = Slug(text);
		var unique = slug;
		for (var i = 2; !used.Add(unique); i++) unique = $"{slug}-{i}";

		return unique;
	}

	/// <summary>A file name for a version: <c>v1.2.0</c>, <c>v2.0.0-beta.1</c>.</summary>
	public static string Version(string tag) => NonFileName().Replace(tag.ToLowerInvariant(), "-");

	[GeneratedRegex(@"[^a-z0-9]+")]
	private static partial Regex NonWord();

	[GeneratedRegex(@"[^a-z0-9.\-]+")]
	private static partial Regex NonFileName();
}
