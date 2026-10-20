using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using static CsvHelper.Docs.Html.Html;

namespace CsvHelper.Docs.Highlighting;

/// <summary>What a piece of code is, which sets its color: the <c>hl-*</c> classes of docs/assets/site.css.</summary>
internal enum TokenKind
{
	/// <summary>A keyword: <c>var</c>, <c>new</c>, <c>true</c>.</summary>
	Keyword,

	/// <summary>A keyword that changes the flow: <c>if</c>, <c>return</c>, <c>foreach</c>.</summary>
	Control,

	/// <summary>A type or a namespace.</summary>
	Type,

	/// <summary>A method, or a shell command.</summary>
	Method,

	/// <summary>A local, parameter, field or property.</summary>
	Variable,

	/// <summary>A constant or an enum member.</summary>
	Constant,

	/// <summary>A string or character literal, or a quoted CSV field.</summary>
	String,

	/// <summary>An escape sequence in a string.</summary>
	Escape,

	/// <summary>A number.</summary>
	Number,

	/// <summary>A comment.</summary>
	Comment,

	/// <summary>The brackets of an XML tag.</summary>
	Punctuation,

	/// <summary>The name of an XML tag.</summary>
	Tag,

	/// <summary>The name of an XML attribute.</summary>
	Attribute,

	/// <summary>The value of an XML attribute.</summary>
	Value,

	/// <summary>A Markdown heading.</summary>
	Heading,

	/// <summary>A Markdown list marker.</summary>
	ListMarker,

	/// <summary>Separators and other marks that only structure the text: CSV separators, Markdown code fences.</summary>
	Muted,
}

/// <summary>A colored range of code.</summary>
/// <param name="Start">The index of its first character.</param>
/// <param name="Length">Its length.</param>
/// <param name="Kind">What it is.</param>
internal readonly record struct CodeSpan(int Start, int Length, TokenKind Kind);

/// <summary>
/// Colors code when the site is generated, so pages need no highlighting script: C# through Roslyn (the names are bound,
/// as VS Code colors them), and XML, JSON, shell commands, CSV and Markdown with small tokenizers. The colors are VS
/// Code's Light+ and Dark+ themes, which switch with the site's theme (the <c>--hl-*</c> variables of docs/assets/site.css).
/// </summary>
internal static class Highlighter
{
	private static readonly ConcurrentDictionary<(string Language, string Code), string> Cache = new();

	/// <summary>
	/// Gives C# the libraries to bind names against: until it is called, C# names are colored by where they are only.
	/// </summary>
	/// <param name="references">The documented libraries and the framework's reference assemblies.</param>
	/// <param name="namespaces">The namespaces the code can use without a using directive (the documented libraries').</param>
	public static void UseReferences(IEnumerable<MetadataReference> references, IEnumerable<string> namespaces)
	{
		CSharpClassifier.UseReferences(references, namespaces);
		Cache.Clear();
	}

	/// <summary>A code block: <c>&lt;pre&gt;&lt;code class="language-csharp"&gt;</c> and the colored code.</summary>
	/// <param name="code">The code.</param>
	/// <param name="language">Its language, as in a Markdown info string (csharp, xml, shell, csv); null or empty for plain text.</param>
	/// <returns>The HTML.</returns>
	public static string Block(string code, string? language)
	{
		var open = string.IsNullOrEmpty(language) ? "<pre><code>" : $"""<pre><code class="language-{E(language)}">""";

		return $"{open}{Html(code, language)}</code></pre>";
	}

	/// <summary>Colored code: the code, HTML encoded, with spans around the parts that have a color.</summary>
	/// <param name="code">The code.</param>
	/// <param name="language">Its language, as in a Markdown info string (csharp, xml, shell, csv); null or empty for plain text.</param>
	/// <returns>The HTML.</returns>
	public static string Html(string code, string? language)
	{
		var name = Normalize(language);

		return Cache.GetOrAdd((name, code), key => Render(key.Code, Classify(key.Code, key.Language)));
	}

	/// <summary>The colored ranges of code.</summary>
	/// <param name="code">The code.</param>
	/// <param name="language">Its language, as in a Markdown info string; null or empty for plain text.</param>
	/// <returns>The ranges, in order and without overlaps.</returns>
	public static List<CodeSpan> Classify(string code, string? language)
	{
		var spans = Normalize(language) switch
		{
			"csharp" => CSharpClassifier.Classify(code),
			"xml" => XmlClassifier.Classify(code),
			"json" => JsonClassifier.Classify(code),
			"shell" => ShellClassifier.Classify(code),
			"csv" => CsvClassifier.Classify(code),
			"markdown" => MarkdownClassifier.Classify(code),
			_ => [],
		};

		return Order(spans);
	}

	/// <summary>The CSS class of a kind of token.</summary>
	/// <param name="kind">The kind.</param>
	/// <returns>The class: <c>hl-keyword</c>.</returns>
	public static string Class(TokenKind kind) => kind switch
	{
		TokenKind.Keyword => "hl-keyword",
		TokenKind.Control => "hl-control",
		TokenKind.Type => "hl-type",
		TokenKind.Method => "hl-method",
		TokenKind.Variable => "hl-variable",
		TokenKind.Constant => "hl-constant",
		TokenKind.String => "hl-string",
		TokenKind.Escape => "hl-escape",
		TokenKind.Number => "hl-number",
		TokenKind.Comment => "hl-comment",
		TokenKind.Punctuation => "hl-punctuation",
		TokenKind.Tag => "hl-tag",
		TokenKind.Attribute => "hl-attribute",
		TokenKind.Value => "hl-value",
		TokenKind.Heading => "hl-heading",
		TokenKind.ListMarker => "hl-list",
		TokenKind.Muted => "hl-muted",
		_ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
	};

	/// <summary>The language a Markdown info string names, under one name per highlighter.</summary>
	/// <param name="language">The language: <c>cs</c>, <c>bash</c>, <c>csproj</c>.</param>
	/// <returns><c>csharp</c>, <c>xml</c>, <c>json</c>, <c>shell</c>, <c>csv</c>, <c>markdown</c>, or <c>text</c>.</returns>
	private static string Normalize(string? language) => language?.ToLowerInvariant() switch
	{
		"csharp" or "cs" or "c#" => "csharp",
		"xml" or "csproj" or "props" or "targets" or "html" or "xaml" or "svg" => "xml",
		"json" or "jsonc" => "json",
		"shell" or "sh" or "bash" or "console" or "powershell" or "pwsh" or "ps1" or "cmd" or "bat" => "shell",
		"csv" or "tsv" => "csv",
		"markdown" or "md" => "markdown",
		_ => "text",
	};

	/// <summary>Sorts ranges and drops the empty ones and the ones that overlap one before them.</summary>
	/// <param name="spans">The ranges.</param>
	/// <returns>The ranges, in order and without overlaps.</returns>
	private static List<CodeSpan> Order(List<CodeSpan> spans)
	{
		var ordered = new List<CodeSpan>(spans.Count);
		var end = 0;
		foreach (var span in spans.Where(s => s.Length > 0).OrderBy(s => s.Start))
		{
			if (span.Start < end)
			{
				continue;
			}

			ordered.Add(span);
			end = span.Start + span.Length;
		}

		return ordered;
	}

	/// <summary>The HTML of code and its colored ranges.</summary>
	/// <param name="code">The code.</param>
	/// <param name="spans">The ranges, in order and without overlaps.</param>
	/// <returns>The HTML.</returns>
	private static string Render(string code, List<CodeSpan> spans)
	{
		var html = new StringBuilder(code.Length * 2);
		var position = 0;
		foreach (var span in spans)
		{
			html.Append(E(code[position..span.Start]));
			html.Append("<span class=\"").Append(Class(span.Kind)).Append("\">");
			html.Append(E(code.Substring(span.Start, span.Length)));
			html.Append("</span>");
			position = span.Start + span.Length;
		}

		html.Append(E(code[position..]));

		return html.ToString();
	}
}
