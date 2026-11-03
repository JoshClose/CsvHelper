using System;
using System.Collections.Generic;

namespace CsvHelper.Excel;

/// <summary>
/// The names of the parts of an Open Packaging Conventions package, such as <c>xl/worksheets/sheet1.xml</c>, in the one
/// form everything here compares: no leading <c>/</c>, <c>/</c> between segments (never <c>\</c>), and percent-encoded
/// characters decoded. Names compare ignoring case (<see cref="Comparer"/>), as OPC says.
/// </summary>
internal static class PartName
{
	/// <summary>The content types part, <c>[Content_Types].xml</c>.</summary>
	public const string ContentTypes = "[Content_Types].xml";

	/// <summary>The package's relationships part, <c>_rels/.rels</c>.</summary>
	public const string PackageRelationships = "_rels/.rels";

	/// <summary>The workbook part when the package has no relationships part to name it.</summary>
	public const string DefaultWorkbook = "xl/workbook.xml";

	/// <summary>Compares part names: ordinal, ignoring case.</summary>
	public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

	/// <summary>Puts a zip entry's name or a part name into the form part names compare in.</summary>
	/// <param name="name">The name, such as <c>/xl/workbook.xml</c> or <c>xl\worksheets\sheet%201.xml</c>.</param>
	/// <returns>The name, such as <c>xl/workbook.xml</c> or <c>xl/worksheets/sheet 1.xml</c>.</returns>
	public static string Normalize(string name)
	{
		var normalized = name.Replace('\\', '/').TrimStart('/');

		return normalized.IndexOf('%') < 0 ? normalized : Uri.UnescapeDataString(normalized);
	}

	/// <summary>
	/// Resolves a relationship's target against the part the relationship is from: relative to that part's folder, or
	/// from the package's root when it starts with <c>/</c>. <c>.</c> and <c>..</c> segments are resolved (<c>..</c> stops at
	/// the root), and a fragment is dropped.
	/// </summary>
	/// <param name="source">The part the relationship is from, normalized; empty for the package's own relationships.</param>
	/// <param name="target">The relationship's <c>Target</c>, decoded from its attribute.</param>
	/// <returns>The target's part name, normalized.</returns>
	public static string Resolve(string source, string target)
	{
		var path = target.Replace('\\', '/');
		var fragment = path.IndexOf('#');
		if (fragment >= 0)
		{
			path = path.Substring(0, fragment);
		}

		if (!path.StartsWith("/", StringComparison.Ordinal))
		{
			var folder = source.LastIndexOf('/');
			path = (folder < 0 ? "" : source.Substring(0, folder + 1)) + path;
		}

		var segments = new List<string>();
		foreach (var segment in path.Split('/'))
		{
			if (segment.Length == 0 || segment == ".")
			{
				continue;
			}

			if (segment == "..")
			{
				if (segments.Count > 0)
				{
					segments.RemoveAt(segments.Count - 1);
				}

				continue;
			}

			segments.Add(segment);
		}

		return Normalize(string.Join("/", segments));
	}

	/// <summary>The name of a part's relationships part: <c>xl/_rels/workbook.xml.rels</c> for <c>xl/workbook.xml</c>.</summary>
	/// <param name="partName">The part, normalized.</param>
	/// <returns>The name of its relationships part.</returns>
	public static string RelationshipsOf(string partName)
	{
		var folder = partName.LastIndexOf('/');

		return folder < 0
			? $"_rels/{partName}.rels"
			: $"{partName.Substring(0, folder)}/_rels/{partName.Substring(folder + 1)}.rels";
	}

	/// <summary>The extension of a part's name, without the dot: <c>xml</c> for <c>xl/workbook.xml</c>.</summary>
	/// <param name="partName">The part, normalized.</param>
	/// <returns>The extension, or an empty string when the last segment has none.</returns>
	public static string Extension(string partName)
	{
		var dot = partName.LastIndexOf('.');

		return dot < 0 || dot < partName.LastIndexOf('/') ? "" : partName.Substring(dot + 1);
	}
}
