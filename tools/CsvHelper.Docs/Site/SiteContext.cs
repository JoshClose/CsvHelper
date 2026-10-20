using System;
using System.Collections.Generic;
using System.Linq;
using CsvHelper.Docs.Api;
using CsvHelper.Docs.Html;
using CsvHelper.Docs.Releases;

namespace CsvHelper.Docs.Site;

/// <summary>Everything the page generators share. Paths are relative to the site root, with forward slashes.</summary>
internal sealed class SiteContext
{
	public required SiteConfig Config { get; init; }
	public required string Repo { get; init; }
	public required string OutDir { get; init; }

	/// <summary>The absolute URL of the site root, ending with a slash.</summary>
	public required string BaseUrl { get; init; }

	public required ApiSet Api { get; init; }
	public required Icons Icons { get; init; }

	/// <summary>The CsvHelper logo and its comma, for inlining.</summary>
	public required Logos Logos { get; init; }

	/// <summary>The commit the site is built from.</summary>
	public required string Sha { get; init; }

	public required DateTimeOffset BuildDate { get; init; }
	public required SourceLocator Sources { get; init; }
	public required ReleaseInfo Releases { get; init; }
	public List<SearchDoc> Search { get; } = [];
	public List<string> Pages { get; } = [];

	public string ShortSha => Sha.Length > 7 ? Sha[..7] : Sha;

	/// <summary>A link from page <paramref name="from"/> to site path <paramref name="to"/> (which may have a fragment).</summary>
	public static string Relative(string from, string to)
	{
		var depth = from.Count(c => c == '/');

		return string.Concat(Enumerable.Repeat("../", depth)) + to;
	}

	public static string ApiPath(ApiType type) => "api/" + type.FileName;

	public static string ApiPath(ApiMember member) => ApiPath(member.Type) + "#" + member.Anchor;

	/// <summary>A file of the repository on GitHub, at the commit the site is built from.</summary>
	public string GitHubFile(string path, int? line = null) =>
		$"{Config.Repository}/blob/{Sha}/{path.Replace('\\', '/')}" + (line is { } l ? $"#L{l}" : "");

	/// <summary>A folder or file of the repository on GitHub.</summary>
	public string GitHubPath(string path, bool directory) =>
		$"{Config.Repository}/{(directory ? "tree" : "blob")}/{Config.Branch}/{path.Replace('\\', '/').TrimEnd('/')}";

	public string GitHubEdit(string path) => $"{Config.Repository}/edit/{Config.Branch}/{path.Replace('\\', '/')}";

	public string GitHubCommit(string sha) => $"{Config.Repository}/commit/{sha}";

	public string GitHubCompare(string from, string to) => $"{Config.Repository}/compare/{from}...{to}";

	/// <summary>The badge text for a member or type that is not in every target framework: "net10.0 only".</summary>
	public string? Availability(IReadOnlyList<string> frameworks)
	{
		if (frameworks.Count >= Api.Frameworks.Count) return null;
		var ordered = Api.Frameworks.Where(frameworks.Contains).ToList();

		return ordered.Count == 1 ? $"{ordered[0]} only" : string.Join(", ", ordered);
	}
}

/// <summary>An entry of the search index.</summary>
/// <param name="Title">What the result shows: <c>GetString(int)</c>, <c>Reading records into objects</c>.</param>
/// <param name="Kind">guide, section, class, method, release and so on; it picks the result's icon and group.</param>
/// <param name="Url">The site path, with a fragment for sections and members.</param>
/// <param name="Parent">Where it is: the type of a member, the page of a section.</param>
/// <param name="Summary">One sentence shown under the title.</param>
/// <param name="Text">More text that is searched but not shown.</param>
internal sealed record SearchDoc(string Title, string Kind, string Url, string? Parent, string Summary, string Text);
