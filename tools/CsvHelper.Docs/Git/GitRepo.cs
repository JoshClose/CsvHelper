using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CsvHelper.Docs.Git;

/// <summary>A tag. <see cref="Sha"/> is the commit it points to (annotated tags are peeled).</summary>
internal sealed record GitTag(string Name, string Sha, DateTimeOffset Date, string? Message);

internal sealed record GitCommit(string Sha, string ShortSha, string Subject, string Author, DateTimeOffset Date);

/// <summary>The git commands the generator needs.</summary>
internal sealed class GitRepo
{
	private const char FieldSeparator = '\u001f';
	private const char RecordSeparator = '\u001e';

	private readonly string root;

	public GitRepo(string root) => this.root = root;

	public bool IsRepository => ProcessRunner.Run("git", ["rev-parse", "--is-inside-work-tree"], root).ExitCode == 0;

	public string HeadSha() => ProcessRunner.Output("git", ["rev-parse", "HEAD"], root).Trim();

	public DateTimeOffset HeadDate() =>
		DateTimeOffset.Parse(ProcessRunner.Output("git", ["log", "-1", "--format=%cI", "HEAD"], root).Trim(), CultureInfo.InvariantCulture);

	/// <summary>Every tag, with its commit, date (the tag's for annotated tags) and message (annotated tags only).</summary>
	public IReadOnlyList<GitTag> Tags()
	{
		const string Format = "%(refname:strip=2)%1f%(objecttype)%1f%(objectname)%1f%(*objectname)%1f%(creatordate:iso-strict)%1f%(contents)%1e";
		var output = ProcessRunner.Output("git", ["for-each-ref", "refs/tags", $"--format={Format}"], root);
		var tags = new List<GitTag>();
		foreach (var record in output.Split(RecordSeparator))
		{
			var fields = record.TrimStart('\n', '\r').Split(FieldSeparator);
			if (fields.Length < 6) continue;
			var annotated = fields[1] == "tag";
			var sha = annotated ? fields[3] : fields[2];
			var message = annotated ? fields[5].Trim() : null;
			var date = DateTimeOffset.Parse(fields[4], CultureInfo.InvariantCulture);
			tags.Add(new GitTag(fields[0], sha, date, string.IsNullOrEmpty(message) ? null : message));
		}

		return tags;
	}

	/// <summary>The commits reachable from <paramref name="to"/> but not from <paramref name="from"/> (all of them when it is null), newest first, merges excluded.</summary>
	public IReadOnlyList<GitCommit> Commits(string? from, string to)
	{
		var range = from is null ? to : $"{from}..{to}";
		var output = ProcessRunner.Output("git", ["log", "--no-merges", "--format=%H%x1f%h%x1f%s%x1f%an%x1f%cI%x1e", range], root);
		var commits = new List<GitCommit>();
		foreach (var record in output.Split(RecordSeparator))
		{
			var fields = record.Trim('\n', '\r').Split(FieldSeparator);
			if (fields.Length < 5) continue;
			commits.Add(new GitCommit(fields[0], fields[1], fields[2], fields[3], DateTimeOffset.Parse(fields[4], CultureInfo.InvariantCulture)));
		}

		return commits;
	}

	/// <summary>Writes the files of commit <paramref name="sha"/> to <paramref name="directory"/>, without touching the repository.</summary>
	public void Extract(string sha, string directory)
	{
		var tar = ProcessRunner.RunBinary("git", ["archive", "--format=tar", sha], root, TimeSpan.FromMinutes(5));
		Directory.CreateDirectory(directory);
		using var stream = new MemoryStream(tar);
		TarFile.ExtractToDirectory(stream, directory, overwriteFiles: true);
	}

	public bool PathExists(string relativePath) => File.Exists(FullPath(relativePath)) || Directory.Exists(FullPath(relativePath));

	public string FullPath(string relativePath) => Path.Combine(root, relativePath);

	public bool IsDirectory(string relativePath) => Directory.Exists(Path.Combine(root, relativePath));
}
