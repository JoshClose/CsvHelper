using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using CsvHelper.Excel.Tests.Xml;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// Generated shared strings parts, read in every <see cref="ScanMode"/> and compared with a reference built on
/// <see cref="XmlReader"/>, which decodes the <c>_xHHHH_</c> escapes with its own regular expression.
/// </summary>
public class SharedStringsComparisonTests
{
	/// <summary>The fragments the text of a <c>t</c> is made of: plain text, references, line ends, escapes, CDATA and markup that is not text.</summary>
	private static readonly string[] Fragments =
	[
		"Name", "a b", "  ", "x", "東京", "é", char.ConvertFromUtf32(0x1F600),
		"&amp;", "&lt;", "&gt;", "&quot;", "&apos;", "&#233;", "&#x1F600;", "&#13;", "&#10;", "&#95;", "&#x9;",
		"\r\n", "\r", "\n", "\t",
		"_x0041_", "_x005F_", "_x000D_", "_x000a_", "_xD83D_", "_xDE00_", "_xD83D__xDE00_", "_X0041_", "_x00G1_", "_x", "_", "_x00e9_",
		"<![CDATA[<b>]]>", "<![CDATA[&amp;]]>", "<![CDATA[a]b]]>", "<![CDATA[_x0041_]]>", "<![CDATA[\r\n]]>", "<![CDATA[]]>",
		"<!-- c -->", "<?p x?>", "<b>no</b>", "<b/>",
	];

	[Fact]
	public async Task MatchesXmlReaderOnGeneratedTables()
	{
		for (var seed = 0; seed < 200; seed++)
		{
			var xml = Generate(new Random(seed), out _);
			var expected = Reference(xml);

			await XmlScan.InEveryModeAsync(async mode =>
			{
				var table = await ReadAsync(xml, mode);

				Assert.True(expected.SequenceEqual(Packages.Strings(table)), $"Seed {seed}: the strings differ from XmlReader's.\n{xml}");
			});
		}
	}

	[Fact]
	public async Task ThrowsForEveryTruncation()
	{
		for (var seed = 0; seed < 10; seed++)
		{
			var xml = Generate(new Random(1000 + seed), out var rootEnd);
			var bytes = Encoding.UTF8.GetBytes(xml);
			var end = Encoding.UTF8.GetByteCount(xml.Substring(0, rootEnd));

			foreach (var mode in new[] { ScanMode.All[0], ScanMode.All[3] })
			{
				for (var length = 0; length < end; length++)
				{
					var truncated = bytes.AsSpan(0, length).ToArray();

					// Every cut is before the root or inside it, through a tag, a text or a character.
					var e = await Record.ExceptionAsync(() => ReadAsync(truncated, mode));
					Assert.True(e is InvalidDataException, $"Seed {seed}, {length} of {end} bytes, mode '{mode}': {e?.GetType().Name ?? "no exception"}.");
				}

				Assert.Equal(Reference(xml), Packages.Strings(await ReadAsync(bytes.AsSpan(0, end).ToArray(), mode)));
			}
		}
	}

	/// <summary>Generates a shared strings part: Excel's shape, with rich text, phonetic runs, prefixes, indenting and noise.</summary>
	/// <param name="random">The source of the choices.</param>
	/// <param name="rootEnd">The index in the XML just past the root's end tag.</param>
	/// <returns>The part's XML.</returns>
	private static string Generate(Random random, out int rootEnd)
	{
		var prefix = random.Next(4) == 0 ? "x:" : "";
		var indent = random.Next(3) == 0;
		var count = random.Next(0, 25);
		var uniqueCount = random.Next(4) switch
		{
			0 => "",
			1 => $" uniqueCount=\"{count}\"",
			2 => " uniqueCount=\"1000000\"",
			_ => " uniqueCount=\"x\"",
		};
		var xml = new StringBuilder();
		if (random.Next(2) == 0)
		{
			xml.Append(TestPackage.Declaration);
		}

		var ns = prefix.Length > 0 ? $"xmlns:x=\"{TestPackage.MainNamespace}\"" : $"xmlns=\"{TestPackage.MainNamespace}\"";
		xml.Append($"<{prefix}sst {ns} count=\"{count}\"{uniqueCount}>");
		for (var i = 0; i < count; i++)
		{
			Gap();
			AppendString();
		}

		if (random.Next(5) == 0)
		{
			Gap();
			xml.Append($"<{prefix}extLst><{prefix}ext uri=\"x\"><{prefix}si><{prefix}t>no</{prefix}t></{prefix}si></{prefix}ext></{prefix}extLst>");
		}

		Gap();
		xml.Append($"</{prefix}sst>");
		rootEnd = xml.Length;
		if (indent)
		{
			xml.Append("\r\n");
		}

		return xml.ToString();

		void Gap()
		{
			if (indent)
			{
				xml.Append("\r\n  ");
			}
		}

		void AppendString()
		{
			var kind = random.Next(10);
			if (kind == 0)
			{
				xml.Append($"<{prefix}si/>");

				return;
			}

			xml.Append($"<{prefix}si>");
			Noise();
			if (kind <= 4 || random.Next(4) == 0)
			{
				AppendText();
			}

			if (kind > 4)
			{
				for (var run = random.Next(1, 5); run > 0; run--)
				{
					Noise();
					xml.Append($"<{prefix}r>");
					if (random.Next(2) == 0)
					{
						xml.Append($"<{prefix}rPr><{prefix}b/><{prefix}sz val=\"11\"/></{prefix}rPr>");
					}

					AppendText();
					xml.Append($"</{prefix}r>");
				}
			}

			if (random.Next(4) == 0)
			{
				xml.Append($"<{prefix}rPh sb=\"0\" eb=\"1\"><{prefix}t>ヨミ</{prefix}t></{prefix}rPh><{prefix}phoneticPr fontId=\"1\"/>");
			}

			Noise();
			xml.Append($"</{prefix}si>");
		}

		void AppendText()
		{
			switch (random.Next(8))
			{
				case 0:
					xml.Append($"<{prefix}t/>");

					return;
				case 1:
					xml.Append($"<{prefix}t></{prefix}t>");

					return;
			}

			xml.Append(random.Next(3) == 0 ? $"<{prefix}t xml:space=\"preserve\">" : $"<{prefix}t>");
			for (var fragment = random.Next(1, 7); fragment > 0; fragment--)
			{
				xml.Append(Fragments[random.Next(Fragments.Length)]);
			}

			xml.Append($"</{prefix}t>");
		}

		void Noise()
		{
			switch (random.Next(12))
			{
				case 0:
					xml.Append("no");

					break;
				case 1:
					xml.Append($"<{prefix}u><{prefix}t>no</{prefix}t></{prefix}u>");

					break;
				case 2:
					xml.Append("<!-- c -->");

					break;
			}
		}
	}

	/// <summary>
	/// The strings of a part, read with <see cref="XmlReader"/>: for each <c>si</c> of the root, the text of the <c>t</c>
	/// elements under it or under its <c>r</c> elements, each decoded for its escapes and then as UTF-8 (where a lone
	/// surrogate becomes U+FFFD), joined.
	/// </summary>
	/// <param name="xml">The part.</param>
	/// <returns>The strings.</returns>
	private static List<string> Reference(string xml)
	{
		var settings = new XmlReaderSettings
		{
			DtdProcessing = DtdProcessing.Prohibit,
			IgnoreComments = true,
			IgnoreProcessingInstructions = true,
		};
		using var reader = XmlReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(xml)), settings);
		var strings = new List<string>();
		var path = new List<string>();
		var value = new StringBuilder();
		var text = new StringBuilder();
		while (reader.Read())
		{
			switch (reader.NodeType)
			{
				case XmlNodeType.Element:
					path.Add(reader.LocalName);
					if (IsString())
					{
						value.Clear();
					}
					else if (IsText())
					{
						text.Clear();
					}

					if (reader.IsEmptyElement)
					{
						End();
					}

					break;
				case XmlNodeType.EndElement:
					End();

					break;
				case XmlNodeType.Text:
				case XmlNodeType.CDATA:
				case XmlNodeType.Whitespace:
				case XmlNodeType.SignificantWhitespace:
					if (IsText())
					{
						text.Append(reader.Value);
					}

					break;
			}
		}

		return strings;

		bool IsString() => path.Count == 2 && path[1] == "si";

		bool IsText() => path.Count >= 3 && path[1] == "si" && path[path.Count - 1] == "t" && (path.Count == 3 || (path.Count == 4 && path[2] == "r"));

		void End()
		{
			if (IsText())
			{
				var decoded = Regex.Replace(text.ToString(), "_x([0-9A-Fa-f]{4})_", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
				value.Append(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(decoded)));
			}
			else if (IsString())
			{
				strings.Add(value.ToString());
			}

			path.RemoveAt(path.Count - 1);
		}
	}

	/// <summary>Reads shared strings from XML, as <paramref name="mode"/> says.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="mode">How to read it.</param>
	/// <returns>The shared strings.</returns>
	private static Task<SharedStrings> ReadAsync(string xml, ScanMode mode) => ReadAsync(Encoding.UTF8.GetBytes(xml), mode);

	/// <summary>Reads shared strings from the bytes of a part, as <paramref name="mode"/> says.</summary>
	/// <param name="bytes">The part.</param>
	/// <param name="mode">How to read it.</param>
	/// <returns>The shared strings.</returns>
	private static async Task<SharedStrings> ReadAsync(byte[] bytes, ScanMode mode)
	{
		using var scanner = XmlScan.Open(bytes, mode);

		return mode.Async
			? await SharedStrings.ReadAsync(scanner, bytes.Length, true, CancellationToken.None)
			: SyncOrAsync.Result(SharedStrings.ReadAsync(scanner, bytes.Length, false, CancellationToken.None));
	}
}
