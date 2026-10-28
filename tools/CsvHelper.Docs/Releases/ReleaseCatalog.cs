using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using CsvHelper.Docs.Api;
using CsvHelper.Docs.Build;
using CsvHelper.Docs.Git;
using CsvHelper.Docs.Html;
using NuGet.Versioning;

namespace CsvHelper.Docs.Releases;

/// <summary>A tag that names a version.</summary>
internal sealed class Release
{
	public required GitTag Tag { get; init; }
	public required NuGetVersion Version { get; init; }
	public Release? Previous { get; set; }
	public IReadOnlyList<GitCommit> Commits { get; set; } = [];

	/// <summary>The API of the release; null when the tag could not be built.</summary>
	public ApiSnapshot? Snapshot { get; set; }

	public string? SnapshotError { get; set; }

	/// <summary>The changes from <see cref="Previous"/>; null for the first release or when a snapshot is missing.</summary>
	public ApiDiff? Diff { get; set; }

	/// <summary>Hand-written notes: docs/releases/&lt;tag&gt;/changelog.md, or the annotated tag's message.</summary>
	public string? ChangelogNotes { get; set; }

	/// <summary>The file of <see cref="ChangelogNotes"/>, relative to the repository; null when there is none (or they are the tag's message).</summary>
	public string? ChangelogNotesPath { get; set; }

	/// <summary>Hand-written notes: docs/releases/&lt;tag&gt;/migration.md.</summary>
	public string? MigrationNotes { get; set; }

	/// <summary>The file of <see cref="MigrationNotes"/>, relative to the repository; null when there is none.</summary>
	public string? MigrationNotesPath { get; set; }

	/// <summary>Whether the release has a migration guide: hand-written notes, or API changes to act on (<see cref="ApiDiff.HasMigration"/>).</summary>
	public bool HasMigrationGuide => MigrationNotes is not null || Diff?.HasMigration == true;

	/// <summary>Whether this is the working tree shown as a release that is not tagged yet (<c>--preview</c>).</summary>
	public bool IsPreview { get; init; }

	public string Name => Tag.Name;
	public string Slug => Slugs.Version(Tag.Name);
	public string ChangelogPath => $"changelog/{Slug}.html";
	public string MigrationPath => $"migration/{Slug}.html";
}

/// <summary>The releases, oldest first, and what changed on the branch since the last one.</summary>
internal sealed class ReleaseInfo
{
	public List<Release> Releases { get; init; } = [];
	public IReadOnlyList<GitCommit> UnreleasedCommits { get; init; } = [];

	/// <summary>HEAD against the last release; null when there is no release, or its snapshot is missing.</summary>
	public ApiDiff? UnreleasedDiff { get; init; }

	/// <summary>The release that first had each type or member, by documentation ID.</summary>
	public Dictionary<string, Release> AddedIn { get; } = [];

	public Release? Latest => Releases.Count > 0 ? Releases[^1] : null;
	public bool Released => Releases.Count > 0;

	/// <summary>Whether "Added in" and "New" badges can be trusted: the last release has a snapshot.</summary>
	public bool HasHistory => Latest?.Snapshot is not null;

	public bool HasUnreleasedApiChanges => UnreleasedDiff is { Changes.Count: > 0 };

	/// <summary>
	/// The newest major release (x.0.0, not a prerelease): its migration guide is the one to point people to, and its API is
	/// the one the reference documents. Null while the branch has breaking changes that no release has (a new major is coming).
	/// </summary>
	public Release? CurrentMajor => UnreleasedDiff?.Breaking.Any() == true
		? null
		: Releases.LastOrDefault(r => !r.Version.IsPrerelease && r.Version is { Minor: 0, Patch: 0 });

	public static ReleaseInfo Empty { get; } = new();
}

/// <summary>
/// Reads the releases from the git tags. The API of each tag is built once from a clean copy of the tag (git archive, not
/// a worktree, so nothing of the repository leaks in), extracted with the current extractor, and cached by commit and
/// snapshot schema in docs/.cache/api. A tag that does not build only loses its API diff.
/// </summary>
internal static class ReleaseCatalog
{
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

	/// <summary>Reads the releases from the tags, and what changed on the branch since the last one.</summary>
	/// <param name="git">The repository.</param>
	/// <param name="config">The site configuration.</param>
	/// <param name="head">The API of the working tree.</param>
	/// <param name="buildTags">Whether to build the tags whose API snapshot is not cached yet.</param>
	/// <param name="cacheDirectory">The folder of the cached API snapshots.</param>
	/// <param name="preview">
	/// The version to show the working tree as, released today (<c>--preview</c>), to see a release before tagging it;
	/// null for none.
	/// </param>
	/// <returns>The releases, oldest first.</returns>
	public static ReleaseInfo Load(GitRepo git, SiteConfig config, ApiSnapshot head, bool buildTags, string cacheDirectory, string? preview)
	{
		var releases = Order(git, preview);
		Directory.CreateDirectory(cacheDirectory);
		var usedCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var release in releases)
		{
			release.Commits = git.Commits(release.Previous?.Tag.Sha, release.Tag.Sha);
			release.Snapshot = release.IsPreview ? head : LoadSnapshot(git, config, release, buildTags, cacheDirectory, usedCache);
			if (Notes(git, release, "changelog.md") is { } changelog)
			{
				(release.ChangelogNotesPath, release.ChangelogNotes) = changelog;
			}
			else
			{
				release.ChangelogNotes = release.Tag.Message;
			}

			if (Notes(git, release, "migration.md") is { } migration)
			{
				(release.MigrationNotesPath, release.MigrationNotes) = migration;
			}

			if (release.Previous?.Snapshot is { } before && release.Snapshot is { } after)
			{
				release.Diff = ApiDiff.Compare(before, after);
			}
		}

		foreach (var file in Directory.EnumerateFiles(cacheDirectory, "*.json").Where(f => !usedCache.Contains(Path.GetFileName(f))))
		{
			File.Delete(file);
		}

		var latest = releases.LastOrDefault();
		var info = new ReleaseInfo
		{
			Releases = releases,
			UnreleasedCommits = git.Commits(latest?.Tag.Sha, "HEAD"),
			UnreleasedDiff = latest?.Snapshot is { } last ? ApiDiff.Compare(last, head) : null,
		};
		foreach (var release in releases.Where(r => r.Snapshot is not null))
		{
			foreach (var id in release.Snapshot!.Types.Select(t => t.Id).Concat(release.Snapshot.Members.Select(m => m.Id)))
			{
				info.AddedIn.TryAdd(id, release);
			}
		}

		return info;
	}

	/// <summary>
	/// The tags that name versions, oldest first, each with the release it follows (<see cref="Release.Previous"/>), plus
	/// the working tree as <paramref name="preview"/> when it is set. Nothing is built or read besides the tags.
	/// </summary>
	/// <param name="git">The repository.</param>
	/// <param name="preview">The version to show the working tree as (<c>--preview</c>); null for none.</param>
	/// <param name="verbose">Whether to log the tags that are not versions.</param>
	/// <returns>The releases, oldest first.</returns>
	public static List<Release> Order(GitRepo git, string? preview, bool verbose = true)
	{
		var releases = new List<Release>();
		foreach (var tag in git.Tags())
		{
			var text = tag.Name.StartsWith('v') || tag.Name.StartsWith('V') ? tag.Name[1..] : tag.Name;
			if (!NuGetVersion.TryParseStrict(text, out var version))
			{
				if (verbose)
				{
					Log.Info($"  ignoring tag {tag.Name}: not a version");
				}

				continue;
			}

			if (releases.Any(r => r.Version == version))
			{
				Log.Warn($"Tags {releases.First(r => r.Version == version).Name} and {tag.Name} are the same version; using the first.");
				continue;
			}

			releases.Add(new Release { Tag = tag, Version = version });
		}

		if (preview is not null)
		{
			releases.Add(Preview(git, releases, preview));
		}

		releases.Sort((a, b) => VersionComparer.Default.Compare(a.Version, b.Version));
		for (var i = 0; i < releases.Count; i++)
		{
			// A stable release follows the previous stable one; a prerelease follows whatever came just before it.
			releases[i].Previous = releases[i].Version.IsPrerelease
				? (i > 0 ? releases[i - 1] : null)
				: releases.Take(i).LastOrDefault(r => !r.Version.IsPrerelease);
		}

		return releases;
	}

	/// <summary>The folders of docs/releases/ that hold a release's notes: the tag's name, and the version with or without "v".</summary>
	/// <param name="release">The release.</param>
	/// <returns>The folder names, most specific first.</returns>
	public static IEnumerable<string> NotesFolders(Release release)
	{
		var version = release.Version.ToNormalizedString();

		return new[] { release.Name, version, "v" + version }.Distinct();
	}

	/// <summary>
	/// The working tree as a release that is not tagged yet: HEAD's commits, the API of the working tree (uncommitted changes
	/// included), today's date and the notes in docs/releases/, so that its changelog and migration guide show as they will
	/// once it is tagged.
	/// </summary>
	/// <param name="git">The repository.</param>
	/// <param name="releases">The releases of the tags.</param>
	/// <param name="name">The version, with or without the "v" of the tags.</param>
	/// <returns>The release, named like the tags (v1.2.0).</returns>
	private static Release Preview(GitRepo git, List<Release> releases, string name)
	{
		var text = name.StartsWith('v') || name.StartsWith('V') ? name[1..] : name;
		if (!NuGetVersion.TryParseStrict(text, out var version))
		{
			throw new ArgumentException($"--preview {name} is not a version, such as 1.2.0 or 1.2.0-beta.1.");
		}

		if (releases.FirstOrDefault(r => r.Version == version) is { } tagged)
		{
			throw new ArgumentException($"--preview {name}: the tag {tagged.Name} already released it.");
		}

		var latest = releases.MaxBy(r => r.Version, VersionComparer.Default);
		if (latest is not null && VersionComparer.Default.Compare(version, latest.Version) < 0)
		{
			throw new ArgumentException($"--preview {name} comes before the latest release, {latest.Name}; preview a later version.");
		}

		var tag = new GitTag($"v{text}", git.HeadSha(), DateTimeOffset.Now, null);
		Log.Info($"  previewing {tag.Name}: the working tree, released today");

		return new Release { Tag = tag, Version = version, IsPreview = true };
	}

	private static ApiSnapshot? LoadSnapshot(GitRepo git, SiteConfig config, Release release, bool buildTags, string cacheDirectory, HashSet<string> usedCache)
	{
		var name = $"{release.Tag.Sha}.v{ApiSnapshot.CurrentSchema}.json";
		var path = Path.Combine(cacheDirectory, name);
		usedCache.Add(name);
		if (File.Exists(path)) return JsonSerializer.Deserialize<ApiSnapshot>(File.ReadAllText(path), JsonOptions);
		if (!buildTags)
		{
			release.SnapshotError = "The tag was not built (--no-tag-builds).";

			return null;
		}

		Log.Info($"  building {release.Name} for its API");
		var directory = Path.Combine(Path.GetTempPath(), "csvhelper-docs", release.Tag.Sha[..12]);
		try
		{
			DeleteDirectory(directory);
			git.Extract(release.Tag.Sha, directory);
			var project = Path.Combine(directory, config.Project);
			if (!File.Exists(project)) throw new InvalidOperationException($"{config.Project} does not exist in {release.Name}.");
			var frameworks = ProjectBuilder.TargetFrameworks(project).Intersect(config.Monikers).ToList();
			if (frameworks.Count == 0) throw new InvalidOperationException($"{release.Name} targets none of {string.Join(", ", config.Monikers)}.");
			var outputs = ProjectBuilder.BuildAll(project, frameworks, lenient: true, TimeSpan.FromMinutes(10)).ToList();
			// The other projects that the release has (CsvHelper.Linq came after the first releases).
			foreach (var other in config.Projects.Select(p => Path.Combine(directory, p)).Where(File.Exists))
			{
				outputs.AddRange(ProjectBuilder.BuildAll(other, ProjectBuilder.TargetFrameworks(other).Intersect(frameworks).ToList(), lenient: true,
					TimeSpan.FromMinutes(10)));
			}

			var snapshot = ApiExtractor.Snapshot(ApiExtractor.Extract(outputs, strict: false));
			File.WriteAllText(path, JsonSerializer.Serialize(snapshot, JsonOptions));

			return snapshot;
		}
		catch (Exception ex)
		{
			// A tag that cannot be built (an old SDK, a missing package) must not stop the site.
			Log.Info($"  {release.Name}: API diff unavailable: {ex.Message}");
			release.SnapshotError = $"The API of {release.Name} could not be built: {ex.Message.Split('\n')[0].Trim()}";
			usedCache.Remove(name);

			return null;
		}
		finally
		{
			DeleteDirectory(directory);
		}
	}

	/// <summary>A notes file of a release, in one of its <see cref="NotesFolders"/>.</summary>
	/// <param name="git">The repository.</param>
	/// <param name="release">The release.</param>
	/// <param name="file">changelog.md or migration.md.</param>
	/// <returns>The file's path relative to the repository (with forward slashes) and its text; null when there is none.</returns>
	private static (string Path, string Text)? Notes(GitRepo git, Release release, string file)
	{
		foreach (var folder in NotesFolders(release))
		{
			var relative = $"docs/releases/{folder}/{file}";
			if (git.PathExists(relative))
			{
				return (relative, File.ReadAllText(git.FullPath(relative)));
			}
		}

		return null;
	}

	private static void DeleteDirectory(string directory)
	{
		for (var attempt = 0; Directory.Exists(directory); attempt++)
		{
			try
			{
				foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
				Directory.Delete(directory, recursive: true);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				if (attempt == 5)
				{
					Log.Info($"  could not delete {directory}: {ex.Message}");

					return;
				}

				Thread.Sleep(500 * (attempt + 1));
			}
		}
	}
}
