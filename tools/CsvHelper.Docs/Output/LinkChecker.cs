using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace CsvHelper.Docs.Output;

/// <summary>Checks that every internal link of the generated site points to an existing file and, if it has a fragment, an existing id.</summary>
internal static partial class LinkChecker
{
	public static int Check(string outDir)
	{
		var files = Directory.EnumerateFiles(outDir, "*.html", SearchOption.AllDirectories).ToList();
		var ids = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
		var texts = new Dictionary<string, string>();
		foreach (var file in files)
		{
			var html = File.ReadAllText(file);
			texts[file] = html;
			var set = new HashSet<string>(StringComparer.Ordinal);
			foreach (var id in Html.Html.Ids(html))
			{
				if (!set.Add(id)) Log.Warn($"{Relative(outDir, file)}: the id '{id}' is used twice.");
			}

			ids[Path.GetFullPath(file)] = set;
		}

		var problems = 0;
		foreach (var (file, html) in texts)
		{
			// 404.html resolves links against <base href>, which is the site root.
			var directory = Path.GetFileName(file) == "404.html" ? outDir : Path.GetDirectoryName(file)!;
			foreach (Match match in Link().Matches(html))
			{
				var url = WebUtility.HtmlDecode(match.Groups[2].Value);
				if (url.Length == 0 || url.Contains("://", StringComparison.Ordinal) || url.StartsWith("mailto:", StringComparison.Ordinal) ||
					url.StartsWith("data:", StringComparison.Ordinal) || url.StartsWith("javascript:", StringComparison.Ordinal) ||
					url.StartsWith('/')) continue;
				var hash = url.IndexOf('#');
				var path = hash < 0 ? url : url[..hash];
				var fragment = hash < 0 ? null : url[(hash + 1)..];
				var target = path.Length == 0 ? Path.GetFullPath(file) : Path.GetFullPath(Path.Combine(directory, path));
				if (!File.Exists(target))
				{
					// site.css is built by Tailwind after the generator runs.
					if (target.EndsWith("site.css", StringComparison.Ordinal)) continue;
					Log.Warn($"{Relative(outDir, file)}: broken link {url}");
					problems++;
				}
				else if (fragment is { Length: > 0 } && ids.TryGetValue(target, out var targetIds) && !targetIds.Contains(fragment))
				{
					Log.Warn($"{Relative(outDir, file)}: no id '{fragment}' in {Relative(outDir, target)}");
					problems++;
				}
			}
		}

		return problems;
	}

	private static string Relative(string root, string file) => Path.GetRelativePath(root, file).Replace('\\', '/');

	[GeneratedRegex(@"\s(href|src)=""([^""]*)""")]
	private static partial Regex Link();
}
