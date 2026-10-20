using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace CsvHelper.Docs.Html;

/// <summary>
/// The CsvHelper logo and its comma (docs/assets/logo.svg and comma.svg), inlined so that site.css can color their parts
/// (<c>logo-comma</c>, <c>logo-csv</c>, <c>logo-helper</c>) for light mode, dark mode and the hero, also from file://.
/// </summary>
internal sealed class Logos
{
	private readonly string directory;
	private readonly Dictionary<string, (string ViewBox, string Body)> cache = [];

	/// <summary>Creates the loader of the logo files.</summary>
	/// <param name="directory">The folder that has logo.svg and comma.svg (docs/assets).</param>
	public Logos(string directory)
	{
		this.directory = directory;
	}

	/// <summary>The full logo as an inline SVG, hidden from screen readers: the link or heading around it names it.</summary>
	/// <param name="classes">The classes of the svg element, which size it.</param>
	/// <returns>The svg element.</returns>
	public string Logo(string classes) => Get("logo", classes);

	/// <summary>The comma alone as an inline SVG, hidden from screen readers.</summary>
	/// <param name="classes">The classes of the svg element, which size it.</param>
	/// <returns>The svg element.</returns>
	public string Comma(string classes) => Get("comma", classes);

	/// <summary>The comma in its own green, centered in a square, as a standalone document: favicon.svg.</summary>
	/// <returns>The SVG document.</returns>
	public string Favicon()
	{
		var (viewBox, body) = Load("comma");
		var box = viewBox.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
		var size = Math.Max(box[2], box[3]);
		var x = box[0] - (size - box[2]) / 2;
		var y = box[1] - (size - box[3]) / 2;

		return string.Create(CultureInfo.InvariantCulture,
			$"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="{x:0.###} {y:0.###} {size:0.###} {size:0.###}">{body}</svg>""");
	}

	/// <summary>An inline svg element of a logo file.</summary>
	/// <param name="name">The file name without .svg.</param>
	/// <param name="classes">The classes of the svg element.</param>
	/// <returns>The svg element.</returns>
	private string Get(string name, string classes)
	{
		var (viewBox, body) = Load(name);

		return $"""<svg class="{classes}" viewBox="{viewBox}" aria-hidden="true" focusable="false">{body}</svg>""";
	}

	/// <summary>
	/// Reads a logo file once: its viewBox and its content without namespaces. The logo is inlined several times per page
	/// and colored by site.css, so an id (the link checker fails on repeated ids) or a style attribute (it would beat the
	/// stylesheet) is an error.
	/// </summary>
	/// <param name="name">The file name without .svg.</param>
	/// <returns>The viewBox and the markup inside the svg element.</returns>
	private (string ViewBox, string Body) Load(string name)
	{
		if (cache.TryGetValue(name, out var logo))
		{
			return logo;
		}

		var file = Path.Combine(directory, name + ".svg");
		var svg = XElement.Load(file);
		foreach (var element in svg.DescendantsAndSelf().ToList())
		{
			if (element.Attribute("id") is not null || element.Attribute("style") is not null)
			{
				throw new InvalidOperationException(
					$"{file}: a <{element.Name.LocalName}> has an id or a style attribute. The logo is inlined several times per page and colored by site.css, so it can have neither.");
			}

			element.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
			element.Name = element.Name.LocalName;
		}

		var viewBox = svg.Attribute("viewBox")?.Value ?? throw new InvalidOperationException($"{file} has no viewBox.");
		logo = (viewBox, string.Concat(svg.Elements().Select(e => e.ToString(SaveOptions.DisableFormatting))));
		cache[name] = logo;

		return logo;
	}
}
