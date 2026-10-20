using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CsvHelper.Docs;

/// <summary>The site settings in docs/site.json.</summary>
internal sealed class SiteConfig
{
	public string Title { get; init; } = "";
	public string Tagline { get; init; } = "";
	public string Description { get; init; } = "";
	public string Repository { get; init; } = "";
	public string Branch { get; init; } = "master";
	public string BaseUrl { get; init; } = "";
	public string Project { get; init; } = "";
	public string Package { get; init; } = "";

	/// <summary>
	/// Other projects whose public API the reference documents with that of <see cref="Project"/>, such as
	/// CsvHelper.Linq; a release whose tag lacks one documents the others.
	/// </summary>
	public List<string> Projects { get; init; } = [];
	public string Readme { get; init; } = "README.md";
	public List<TargetFrameworkConfig> TargetFrameworks { get; init; } = [];
	public List<GuideConfig> Guides { get; init; } = [];
	public HomeConfig Home { get; init; } = new();

	/// <summary>
	/// Addresses of the old site (a folder such as <c>examples/reading/get-class-records</c>) and the page that replaced
	/// each, relative to the site root with an optional anchor (<c>records.html#reading-records</c>).
	/// </summary>
	public Dictionary<string, string> Redirects { get; init; } = [];

	public IReadOnlyList<string> Monikers => TargetFrameworks.Select(t => t.Moniker).ToList();

	public string FrameworkLabel(string moniker) => TargetFrameworks.FirstOrDefault(t => t.Moniker == moniker)?.Label ?? moniker;

	public static SiteConfig Load(string path)
	{
		var options = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
		};
		var config = JsonSerializer.Deserialize<SiteConfig>(File.ReadAllText(path), options)
			?? throw new InvalidOperationException($"{path} is empty.");
		if (config.TargetFrameworks.Count == 0) throw new InvalidOperationException($"{path} lists no target frameworks.");

		return config;
	}
}

internal sealed class TargetFrameworkConfig
{
	public string Moniker { get; init; } = "";
	public string Label { get; init; } = "";
}

/// <summary>A guide page: README sections (<see cref="Headings"/>) or a Markdown file (<see cref="File"/>).</summary>
internal sealed class GuideConfig
{
	public string Slug { get; init; } = "";
	public string? Title { get; init; }
	public string? Icon { get; init; }
	public string? Description { get; init; }
	public List<string> Headings { get; init; } = [];
	public string? File { get; init; }
}

internal sealed class HomeConfig
{
	public List<SampleConfig> Samples { get; init; } = [];
	public List<StatConfig> Stats { get; init; } = [];
	public List<FeatureConfig> Features { get; init; } = [];

	/// <summary>The "Get help" cards: where to ask questions and report bugs.</summary>
	public List<FeatureConfig> Help { get; init; } = [];

	/// <summary>The "Sponsors" cards: how to donate, then the sponsors with their logos.</summary>
	public List<FeatureConfig> Sponsors { get; init; } = [];

	/// <summary>The "Contributors" section, or <see langword="null"/> for none.</summary>
	public ContributorsConfig? Contributors { get; init; }
}

/// <summary>The "Contributors" section of the home page: how to contribute, then the contributors' avatars.</summary>
internal sealed class ContributorsConfig
{
	/// <summary>The sentence before the guidelines.</summary>
	public string Intro { get; init; } = "";

	/// <summary>The guidelines for contributions, in order, as plain text.</summary>
	public List<string> Guidelines { get; init; } = [];

	/// <summary>The absolute URL of an image of the contributors' avatars (not checked by the link checker).</summary>
	public string Image { get; init; } = "";

	/// <summary>Where the image links to, such as the repository's contributors graph.</summary>
	public string Link { get; init; } = "";
}

/// <summary>
/// A code sample on the home page: code block <see cref="Index"/> of the section <see cref="Heading"/>, in the README or
/// in the guide page <see cref="Page"/>.
/// </summary>
internal sealed class SampleConfig
{
	public string Title { get; init; } = "";
	public string Icon { get; init; } = "";

	/// <summary>
	/// The heading of the section, such as <c>## Reading</c>. With <see cref="Page"/>, empty means the page's
	/// introduction (the blocks before its first heading).
	/// </summary>
	public string Heading { get; init; } = "";

	/// <summary>The slug of the guide page that has the section, or <see langword="null"/> for the README.</summary>
	public string? Page { get; init; }

	public int Index { get; init; }
}

internal sealed class StatConfig
{
	public string Value { get; init; } = "";
	public string Label { get; init; } = "";
}

/// <summary>A card on the home page: a feature, a place to get help or a sponsor.</summary>
internal sealed class FeatureConfig
{
	/// <summary>A Font Awesome icon (solid, or <c>brands/…</c>), shown when the card has no <see cref="Image"/>.</summary>
	public string Icon { get; init; } = "";

	/// <summary>The title of the card.</summary>
	public string Title { get; init; } = "";

	/// <summary>One or two sentences of plain text.</summary>
	public string Text { get; init; } = "";

	/// <summary>
	/// Where the card goes: a guide's slug with an optional anchor (<c>reading#parsing-modes</c>), which the link checker
	/// checks, or an absolute URL, which it does not.
	/// </summary>
	public string Link { get; init; } = "";

	/// <summary>A file in docs/assets/images shown instead of the icon, such as a sponsor's logo, or <see langword="null"/>.</summary>
	public string? Image { get; init; }
}
