using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper.Docs.Git;
using CsvHelper.Docs.Releases;
using NuGet.Versioning;

namespace CsvHelper.Docs.Snippets;

/// <summary>
/// Compiles the C# code of every migration guide (<c>--check-snippets</c>): the before blocks of docs/releases/&lt;tag&gt;/migration.md
/// against the CsvHelper package of the release before the tag, and the other C# blocks against the tag's package, or
/// against the working tree for a release that is not tagged yet. Only compiling is checked, not what the code does.
/// </summary>
internal static class SnippetChecker
{
	private sealed record Job(string Guide, string Markdown, bool After, SnippetLibrary Library, List<Snippet> Snippets, string Directory);

	/// <summary>Checks the guides and prints what it found.</summary>
	/// <param name="repo">The repository.</param>
	/// <param name="only">The guides to check, by folder name (v34.0.0); all of them when empty.</param>
	/// <returns>The exit code: 0 when every snippet compiles, 1 otherwise.</returns>
	public static int Run(string repo, IReadOnlyCollection<string> only)
	{
		var releases = ReleaseCatalog.Order(new GitRepo(repo), null, verbose: false);
		var cache = Path.Combine(repo, "docs", ".cache", "snippets");
		Directory.CreateDirectory(cache);
		// Keep the repository's Directory.Build.props and .targets out of the generated projects.
		File.WriteAllText(Path.Combine(cache, "Directory.Build.props"), "<Project />\n");
		File.WriteAllText(Path.Combine(cache, "Directory.Build.targets"), "<Project />\n");

		var problems = new List<string>();
		var jobs = new List<Job>();
		var guides = Directory.EnumerateFiles(Path.Combine(repo, "docs", "releases"), "migration.md", SearchOption.AllDirectories)
			.Select(f => (Folder: Path.GetFileName(Path.GetDirectoryName(f)!), File: f))
			.Where(g => only.Count == 0 || only.Contains(g.Folder, StringComparer.OrdinalIgnoreCase))
			.OrderBy(g => NuGetVersion.TryParse(g.Folder.TrimStart('v', 'V'), out var v) ? v : new NuGetVersion(0, 0, 0));
		foreach (var (folder, file) in guides)
		{
			var markdown = Path.GetRelativePath(repo, file).Replace('\\', '/');
			var libraries = Libraries(folder, releases);
			if (libraries is null)
			{
				problems.Add($"{markdown}: no release before {folder} to migrate from, or {folder} is not a version.");
				continue;
			}

			var (before, after) = libraries.Value;
			var guideProblems = new List<string>();
			var snippets = SnippetSource.Read(file, guideProblems);
			problems.AddRange(guideProblems.Select(p => $"{markdown}: {p}"));
			foreach (var skipped in snippets.Where(s => s.SkipReason is not null))
			{
				Console.WriteLine($"  {markdown}({skipped.Line}): not compiled: {skipped.SkipReason}");
			}

			var compiled = snippets.Where(s => s.SkipReason is null).ToList();
			jobs.Add(new Job(folder, markdown, false, before, compiled.Where(s => !s.After).ToList(), Path.Combine(cache, folder, "before")));
			jobs.Add(new Job(folder, markdown, true, after, compiled.Where(s => s.After).ToList(), Path.Combine(cache, folder, "after")));
		}

		var results = new ConcurrentDictionary<Job, List<string>>();
		var prefix = repo.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
		Parallel.ForEach(jobs.Where(j => j.Snippets.Count > 0), new ParallelOptions { MaxDegreeOfParallelism = 4 }, job =>
		{
			// The compiler opens the files of #line directives, so they get the full path; errors show it relative to the repository.
			var errors = SnippetProject.Build(repo, job.Directory, Path.Combine(repo, job.Markdown), job.Library, job.Snippets);
			results[job] = errors.Select(e => e.Replace(prefix, "", StringComparison.OrdinalIgnoreCase).Replace('\\', '/')).ToList();
		});

		var failed = problems.Count > 0;
		foreach (var guide in jobs.GroupBy(j => j.Guide))
		{
			var parts = guide.Select(j => $"{j.Snippets.Count} {(j.After ? "after" : "before")} ({j.Library})");
			var errors = guide.SelectMany(j => results.GetValueOrDefault(j) ?? []).ToList();
			Console.WriteLine($"{guide.Key}: {string.Join(", ", parts)}: {(errors.Count == 0 ? "ok" : $"{errors.Count} errors")}");
			foreach (var error in errors)
			{
				Console.WriteLine($"  {error}");
			}

			failed |= errors.Count > 0;
		}

		foreach (var problem in problems)
		{
			Console.WriteLine($"error: {problem}");
		}

		return failed ? 1 : 0;
	}

	/// <summary>
	/// What the two sides of a guide compile against: the release before the folder's (before) and the folder's own (after),
	/// or the working tree for a folder whose version is newer than every tag.
	/// </summary>
	/// <param name="folder">The guide's folder name: the tag (v34.0.0), or the version.</param>
	/// <param name="releases">The releases of the tags, oldest first.</param>
	/// <returns>The libraries, or null when the folder names no version, or a release with nothing before it.</returns>
	private static (SnippetLibrary Before, SnippetLibrary After)? Libraries(string folder, List<Release> releases)
	{
		var release = releases.FirstOrDefault(r => ReleaseCatalog.NotesFolders(r).Contains(folder, StringComparer.OrdinalIgnoreCase));
		if (release is not null)
		{
			return release.Previous is { } previous ? (Package(previous.Version), Package(release.Version)) : null;
		}

		if (!NuGetVersion.TryParseStrict(folder.TrimStart('v', 'V'), out var version) || releases.Count == 0
			|| VersionComparer.Default.Compare(version, releases[^1].Version) <= 0)
		{
			return null;
		}

		var latest = releases.LastOrDefault(r => !r.Version.IsPrerelease) ?? releases[^1];

		return (Package(latest.Version), new SnippetLibrary(null, "net10.0"));
	}

	/// <summary>A CsvHelper package; 2.x only has .NET Framework assemblies, so its snippets compile for .NET Framework 4.8.</summary>
	/// <param name="version">The package version.</param>
	/// <returns>The library.</returns>
	private static SnippetLibrary Package(NuGetVersion version) =>
		new(version.ToNormalizedString(), version.Major < 3 ? "net48" : "net10.0");
}
