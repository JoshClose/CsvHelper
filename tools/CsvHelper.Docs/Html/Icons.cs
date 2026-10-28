using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace CsvHelper.Docs.Html;

/// <summary>
/// Font Awesome Free icons, inlined as SVG from the npm package, so pages need no icon font or script. Names are
/// <c>solid</c> icons unless prefixed with <c>brands/</c> or <c>regular/</c>.
/// </summary>
internal sealed class Icons
{
	private readonly string directory;
	private readonly Dictionary<string, (string ViewBox, string Body)> cache = [];

	public Icons(string nodeModules)
	{
		directory = Path.Combine(nodeModules, "@fortawesome", "fontawesome-free", "svgs");
		if (!Directory.Exists(directory))
			throw new DirectoryNotFoundException($"Font Awesome is not installed ({directory}). Run npm ci in docs/ (docs/build.ps1 does).");
	}

	/// <summary>An inline SVG, decorative (hidden from screen readers), sized with <paramref name="classes"/>.</summary>
	public string Get(string name, string classes = "size-4")
	{
		var (viewBox, body) = Load(name);

		return $"""<svg class="icon {classes}" viewBox="{viewBox}" fill="currentColor" aria-hidden="true" focusable="false">{body}</svg>""";
	}

	private (string ViewBox, string Body) Load(string name)
	{
		if (cache.TryGetValue(name, out var icon)) return icon;
		var path = name.Contains('/') ? name : "solid/" + name;
		var file = Path.Combine(directory, path + ".svg");
		if (!File.Exists(file)) throw new FileNotFoundException($"Font Awesome has no icon '{name}'.", file);
		var svg = XElement.Load(file);
		var body = string.Concat(svg.Elements().Select(e =>
		{
			e.Attribute("fill")?.Remove();
			var clean = new XElement(e.Name.LocalName, e.Attributes());

			return clean.ToString(SaveOptions.DisableFormatting);
		}));
		icon = (svg.Attribute("viewBox")?.Value ?? "0 0 512 512", body);
		cache[name] = icon;

		return icon;
	}
}
