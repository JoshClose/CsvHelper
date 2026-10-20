using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsvHelper.Docs.Content;
using Markdig.Syntax;

namespace CsvHelper.Docs.Snippets;

/// <summary>A C# code block of a migration guide, to compile against the version of CsvHelper it shows.</summary>
/// <param name="Line">The Markdown file's line of the block's first line of code (1-based).</param>
/// <param name="Code">The code.</param>
/// <param name="After">Whether it is code for the newer version: an after block, or a C# block that is neither before nor after.</param>
/// <param name="SkipReason">Why it is not compiled (<c>```csharp before nocompile &lt;reason&gt;</c>), or null.</param>
internal sealed record Snippet(int Line, string Code, bool After, string? SkipReason);

/// <summary>Reads the C# code blocks of a migration guide.</summary>
internal static class SnippetSource
{
	private const string NoCompile = "nocompile";

	/// <summary>The C# code blocks of a Markdown file, wherever they are (lists and quotes too).</summary>
	/// <param name="path">The Markdown file.</param>
	/// <param name="problems">Gets a message for each block that does not follow the rules (a <c>nocompile</c> without a reason).</param>
	/// <returns>The snippets, in order.</returns>
	public static List<Snippet> Read(string path, List<string> problems)
	{
		var snippets = new List<Snippet>();
		foreach (var block in Markdown.Parse(File.ReadAllText(path)).Descendants<FencedCodeBlock>())
		{
			if (block.Info is not ("csharp" or "cs" or "c#"))
			{
				continue;
			}

			var flags = BeforeAfter.Flags(block).ToList();
			var skip = flags.IndexOf(NoCompile);
			string? reason = null;
			if (skip >= 0)
			{
				reason = string.Join(" ", flags.Skip(skip + 1));
				if (reason.Length == 0)
				{
					problems.Add($"line {block.Line + 1}: {NoCompile} needs a reason, as in ```csharp before {NoCompile} needs a database");
					reason = "(no reason)";
				}
			}

			// Block.Line is the fence's line, counted from 0.
			snippets.Add(new Snippet(block.Line + 2, BeforeAfter.Code(block), BeforeAfter.Side(block) != BeforeAfter.Before, reason));
		}

		return snippets;
	}
}
