using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CsvHelper.Docs.Releases;

/// <summary>
/// Writes the API diff of every release as text, one sorted line per change (<c>--dump-diffs</c>), so that changes to
/// <see cref="ApiDiff"/> can be compared with <c>git diff --no-index</c>, and guide authors can read what changed.
/// </summary>
internal static class DiffDump
{
	/// <summary>Writes <c>&lt;tag&gt;.txt</c> for each release, and <c>unreleased.txt</c>, to <paramref name="directory"/>.</summary>
	/// <param name="releases">The releases.</param>
	/// <param name="directory">The folder to write to; it is created when missing.</param>
	public static void Write(ReleaseInfo releases, string directory)
	{
		Directory.CreateDirectory(directory);
		foreach (var release in releases.Releases)
		{
			var text = new StringBuilder();
			text.AppendLine($"{release.Name} from {release.Previous?.Name ?? "nothing"}; migration guide: {release.HasMigrationGuide}");
			Append(text, release.Diff, release.SnapshotError ?? release.Previous?.SnapshotError);
			File.WriteAllText(Path.Combine(directory, release.Name + ".txt"), text.ToString());
		}

		var unreleased = new StringBuilder();
		unreleased.AppendLine($"unreleased from {releases.Latest?.Name ?? "nothing"}");
		Append(unreleased, releases.UnreleasedDiff, releases.Latest?.SnapshotError);
		File.WriteAllText(Path.Combine(directory, "unreleased.txt"), unreleased.ToString());
	}

	/// <summary>Appends one line per change, sorted, or why there is no diff.</summary>
	/// <param name="text">The text to append to.</param>
	/// <param name="diff">The diff, or null.</param>
	/// <param name="error">Why the diff is missing, when it is.</param>
	private static void Append(StringBuilder text, ApiDiff? diff, string? error)
	{
		if (diff is null)
		{
			text.AppendLine($"no API diff: {error ?? "first release"}");

			return;
		}

		if (diff.FrameworksAdded.Count > 0)
		{
			text.AppendLine($"frameworks added: {string.Join(", ", diff.FrameworksAdded)}");
		}

		if (diff.FrameworksRemoved.Count > 0)
		{
			text.AppendLine($"frameworks removed: {string.Join(", ", diff.FrameworksRemoved)}");
		}

		var lines = diff.Changes
			.Select(c => $"{c.Kind} | {(c.Breaking ? "breaking" : "-")} | {c.Title} | {string.Join(",", c.Frameworks)} | {c.Note}")
			.Order(StringComparer.Ordinal);
		foreach (var line in lines)
		{
			text.AppendLine(line);
		}
	}
}
