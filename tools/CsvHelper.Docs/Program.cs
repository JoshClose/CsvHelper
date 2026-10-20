using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CsvHelper.Docs.Api;
using CsvHelper.Docs.Build;
using CsvHelper.Docs.Content;
using CsvHelper.Docs.Git;
using CsvHelper.Docs.Highlighting;
using CsvHelper.Docs.Html;
using CsvHelper.Docs.Output;
using CsvHelper.Docs.Pages;
using CsvHelper.Docs.Releases;
using CsvHelper.Docs.Site;
using CsvHelper.Docs.Snippets;

namespace CsvHelper.Docs;

/// <summary>
/// Generates the documentation site into docs/_site. docs/build.ps1 runs it after <c>npm ci</c> (it needs the Font
/// Awesome icons from docs/node_modules) and builds the CSS after it (Tailwind scans the generated pages).
/// </summary>
internal static class Program
{
	public static int Main(string[] args)
	{
		var stopwatch = Stopwatch.StartNew();
		try
		{
			var options = Options.Parse(args);
			if (options.Help)
			{
				Console.WriteLine(Options.Usage);

				return 0;
			}

			if (options.CheckSnippets)
			{
				return SnippetChecker.Run(options.Repo, options.Only);
			}

			var config = SiteConfig.Load(Path.Combine(options.Repo, "docs", "site.json"));
			var project = Path.Combine(options.Repo, config.Project);
			var outDir = options.Out ?? Path.Combine(options.Repo, "docs", "_site");
			var nodeModules = Path.Combine(options.Repo, "docs", "node_modules");

			Log.Info("Building the library");
			var outputs = ProjectBuilder.BuildAll(project, config.Monikers, lenient: false, TimeSpan.FromMinutes(15)).ToList();
			foreach (var other in config.Projects)
			{
				outputs.AddRange(ProjectBuilder.BuildAll(Path.Combine(options.Repo, other), config.Monikers, lenient: false, TimeSpan.FromMinutes(15)));
			}

			Log.Info("Reading the API");
			var api = ApiExtractor.Extract(outputs, options.Strict);
			Highlighter.UseReferences(api.PrimaryCompilation.References, api.Types.Select(t => t.Namespace));
			if (options.DumpApi)
			{
				Dump(api);

				return 0;
			}

			var git = new GitRepo(options.Repo);
			var isRepository = git.IsRepository;
			if (!isRepository) Log.Warn("Not a git repository: there are no releases, commits or source links.");
			if (!isRepository && options.Preview is not null)
			{
				throw new InvalidOperationException("--preview needs a git repository: the release follows its tags.");
			}

			Log.Info("Reading the releases");
			var releases = isRepository
				? ReleaseCatalog.Load(git, config, ApiExtractor.Snapshot(api), !options.NoTagBuilds, Path.Combine(options.Repo, "docs", ".cache", "api"), options.Preview)
				: ReleaseInfo.Empty;
			if (options.DumpDiffs is not null)
			{
				DiffDump.Write(releases, options.DumpDiffs);

				return 0;
			}

			Log.Info("Locating the sources");
			var site = new SiteContext
			{
				Config = config,
				Repo = options.Repo,
				OutDir = outDir,
				BaseUrl = BaseUrl(options.BaseUrl ?? config.BaseUrl),
				Api = api,
				Icons = new Icons(nodeModules),
				Logos = new Logos(Path.Combine(options.Repo, "docs", "assets")),
				Sha = isRepository ? git.HeadSha() : config.Branch,
				BuildDate = isRepository ? git.HeadDate() : DateTimeOffset.UtcNow,
				Sources = SourceLocator.Create(outputs, options.Repo),
				Releases = releases,
			};

			Log.Info("Writing the pages");
			var guides = Guides.Load(site, releases.Released);
			var pages = new[] { new HomePage(site, guides).Build() }
				.Concat(new GuidePages(site, guides).Build())
				.Concat(new ApiPages(site).Build())
				.Concat(new ReleasePages(site, guides.Links).Build())
				.Append(SiteExtras.NotFound(site))
				.ToList();
			Clean(outDir);
			var layout = new Layout(site, guides);
			foreach (var page in pages)
			{
				var file = Path.Combine(outDir, page.Path);
				Directory.CreateDirectory(Path.GetDirectoryName(file)!);
				File.WriteAllText(file, layout.Render(page));
				site.Pages.Add(page.Path);
			}

			var redirects = Redirects.Write(site);
			SiteExtras.Write(site, nodeModules);
			var broken = LinkChecker.Check(outDir);
			Log.Info($"Wrote {pages.Count} pages, {redirects} redirects and {site.Search.Count} search entries to {outDir} in {stopwatch.Elapsed.TotalSeconds:0.0} s" +
					 (broken > 0 ? $" ({broken} broken links)" : ""));
			if (options.Strict && Log.Warnings.Count > 0)
			{
				Console.Error.WriteLine($"error: {Log.Warnings.Count} warnings in strict mode.");

				return 1;
			}

			return 0;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"error: {ex.Message}");
			Console.Error.WriteLine(ex.StackTrace);

			return 1;
		}
	}

	private static string BaseUrl(string url) => url.EndsWith('/') ? url : url + "/";

	/// <summary>Empties the output folder, keeping the folder itself (a preview server may be watching it).</summary>
	private static void Clean(string outDir)
	{
		Directory.CreateDirectory(outDir);
		foreach (var directory in Directory.EnumerateDirectories(outDir)) Directory.Delete(directory, recursive: true);
		foreach (var file in Directory.EnumerateFiles(outDir)) File.Delete(file);
	}

	private static void Dump(ApiSet api)
	{
		foreach (var type in api.Types)
		{
			foreach (var variant in type.Variants) Console.WriteLine($"[{string.Join(",", variant.Frameworks)}] {variant.Signature}    -> {type.FileName} ({type.Group}, {type.Kind})");
			foreach (var member in type.Members)
			{
				foreach (var variant in member.Variants)
					Console.WriteLine($"    [{string.Join(",", variant.Frameworks)}] {member.Kind} #{member.Anchor} {member.ShortName} :: {variant.Signature}");
			}
		}
	}
}

internal sealed class Options
{
	/// <summary>The text of <c>--help</c>.</summary>
	public const string Usage = """
		Generates the documentation site into docs/_site. docs/build.ps1 runs it and then builds the CSS.

		Usage: dotnet run --project tools/CsvHelper.Docs -- [options]

		Options:
		  --repo <path>        The repository (default: the first folder up from the current one with docs/site.json).
		  --out <path>         Where to write the site (default: docs/_site).
		  --base-url <url>     The absolute URL the site is published at (default: baseUrl in docs/site.json).
		  --preview <version>  Show the working tree as release <version>, released today, with its notes from
		                       docs/releases/<version>/: the changelog and migration guide as they will be once tagged.
		  --no-tag-builds      Don't build release tags whose API snapshot is not cached yet (their API diffs are missing).
		  --strict             Fail on any warning.
		  --dump-api           Print the API that was read, and stop.
		  --dump-diffs <dir>   Write the API diff of every release to <dir> (one .txt per release), and stop.
		  --check-snippets     Compile the C# code of the migration guides (docs/releases/*/migration.md) against the
		                       CsvHelper versions they show, and stop. With --only <version> (repeatable), only those guides.
		  -h, --help           Show this help.
		""";

	public string Repo { get; private set; } = "";
	public string? Out { get; private set; }
	public string? BaseUrl { get; private set; }
	public bool NoTagBuilds { get; private set; }
	public bool Strict { get; private set; }
	public bool DumpApi { get; private set; }

	/// <summary>The version to show the working tree as (<c>--preview</c>); null for none.</summary>
	public string? Preview { get; private set; }

	/// <summary>Whether to print <see cref="Usage"/> and stop (<c>-h</c>, <c>--help</c>).</summary>
	public bool Help { get; private set; }

	/// <summary>The folder to write every release's API diff to, and stop (<c>--dump-diffs</c>); null for none.</summary>
	public string? DumpDiffs { get; private set; }

	/// <summary>Whether to compile the code of the migration guides, and stop (<c>--check-snippets</c>).</summary>
	public bool CheckSnippets { get; private set; }

	/// <summary>The guides <c>--check-snippets</c> checks (<c>--only v34.0.0</c>); all when empty.</summary>
	public List<string> Only { get; } = [];

	public static Options Parse(string[] args)
	{
		var options = new Options();
		for (var i = 0; i < args.Length; i++)
		{
			switch (args[i])
			{
				case "--repo": options.Repo = Path.GetFullPath(Value(args, ref i)); break;
				case "--out": options.Out = Path.GetFullPath(Value(args, ref i)); break;
				case "--base-url":
					var url = Value(args, ref i);
					options.BaseUrl = string.IsNullOrWhiteSpace(url) ? null : url;
					break;
				case "--preview": options.Preview = Value(args, ref i); break;
				case "--no-tag-builds": options.NoTagBuilds = true; break;
				case "--strict": options.Strict = true; break;
				case "--dump-api": options.DumpApi = true; break;
				case "--dump-diffs": options.DumpDiffs = Path.GetFullPath(Value(args, ref i)); break;
				case "--check-snippets": options.CheckSnippets = true; break;
				case "--only": options.Only.Add(Value(args, ref i)); break;
				case "-h" or "--help": options.Help = true; break;
				default: throw new ArgumentException($"Unknown argument {args[i]}; --help lists the options.");
			}
		}

		if (options.Help)
		{
			return options;
		}

		if (options.Repo.Length == 0) options.Repo = FindRepo();

		return options;
	}

	/// <summary>The value of the option at <paramref name="index"/>, which moves past it.</summary>
	/// <param name="args">The command line.</param>
	/// <param name="index">The index of the option; the value's index afterwards.</param>
	/// <returns>The value.</returns>
	private static string Value(string[] args, ref int index)
	{
		if (index + 1 >= args.Length)
		{
			throw new ArgumentException($"{args[index]} needs a value; --help lists the options.");
		}

		index++;

		return args[index];
	}

	/// <summary>The folder above the current one that has docs/site.json.</summary>
	private static string FindRepo()
	{
		for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
		{
			if (File.Exists(Path.Combine(directory.FullName, "docs", "site.json"))) return directory.FullName;
		}

		throw new InvalidOperationException("docs/site.json not found above the current folder; pass --repo.");
	}
}
