using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CsvHelper.Docs.Api;
using CsvHelper.Docs.Site;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CsvHelper.Docs.Content;

/// <summary>
/// Links inline code in guides to the API reference when it names a type (<c>CsvReader</c>, <c>[CsvRecord]</c>), a
/// member with its type (<c>CsvQuoting.Always</c>) or a member whose name no other type uses (<c>GetFieldIndex</c>).
/// </summary>
internal sealed partial class CodeLinker
{
	private readonly Dictionary<string, string> targets = new(StringComparer.Ordinal);

	public CodeLinker(ApiSet api)
	{
		foreach (var type in api.Types.Where(t => t.ContainingType is null).OrderBy(t => t.Symbol.Arity))
		{
			var name = type.Symbol.Name;
			targets.TryAdd(name, SiteContext.ApiPath(type));
			if (type.Group == "Attributes" && name.EndsWith("Attribute", StringComparison.Ordinal))
				targets.TryAdd(name[..^"Attribute".Length], SiteContext.ApiPath(type));
		}

		foreach (var member in api.Members.Where(m => m.Kind != ApiMemberKind.Constructor))
		{
			targets.TryAdd($"{member.Type.Symbol.Name}.{member.Name}", SiteContext.ApiPath(member));
		}

		foreach (var group in api.Members.Where(m => m.Kind != ApiMemberKind.Constructor).GroupBy(m => m.Name))
		{
			if (group.Select(m => m.Type).Distinct().Count() == 1) targets.TryAdd(group.Key, SiteContext.ApiPath(group.First()));
		}
	}

	/// <summary>The API page for a piece of inline code, or null.</summary>
	public string? Find(string code)
	{
		var text = code.Trim();
		if (text.StartsWith('[') && text.EndsWith(']')) text = text[1..^1];
		var paren = text.IndexOf('(');
		if (paren > 0) text = text[..paren];
		var angle = text.IndexOf('<');
		if (angle > 0) text = text[..angle];

		return IdentifierPath().IsMatch(text) ? targets.GetValueOrDefault(text) : null;
	}

	/// <summary>Wraps the inline code of <paramref name="blocks"/> that names API symbols in links (not in headings or links).</summary>
	public void Link(IEnumerable<Block> blocks, string page)
	{
		foreach (var block in blocks.Where(b => b is not HeadingBlock))
		{
			foreach (var code in Markdown.Inlines<CodeInline>(block).ToList())
			{
				if (code.FindParentOfType<LinkInline>().Any() || Find(code.Content) is not { } target) continue;
				var link = new LinkInline(SiteContext.Relative(page, target), "");
				code.ReplaceBy(link);
				link.AppendChild(code);
			}
		}
	}

	[GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$")]
	private static partial Regex IdentifierPath();
}
