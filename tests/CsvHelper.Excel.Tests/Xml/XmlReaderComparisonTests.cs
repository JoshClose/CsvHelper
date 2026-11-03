using System.Text;

namespace CsvHelper.Excel.Tests.Xml;

/// <summary>
/// The scanner against <see cref="System.Xml.XmlReader"/>: seeded random documents with prefixes, namespaces, entities,
/// character references, line ends, CDATA, comments, processing instructions and non-ASCII text, in UTF-8 (with and
/// without a byte order mark) and UTF-16 (both byte orders), read in every <see cref="ScanMode"/>.
/// </summary>
public class XmlReaderComparisonTests
{
	private static readonly string[] Names = ["a", "row", "c", "v", "t", "si", "x:c", "x:row", "x:v"];
	private static readonly string[] AttributeNames = ["r", "t", "s", "r:id", "val", "x:a", "name"];

	private static readonly string[] TextPieces =
	[
		"abc", "12", " ", "  ", "\t", "\n", "\r\n", "\r", "&amp;", "&lt;", "&gt;", "&quot;", "&apos;", "&#65;", "&#x42;",
		"&#xE9;", "&#x1F600;", "&#128512;", "&#13;", "&#10;", "&#9;", "é", "日本語", char.ConvertFromUtf32(0x1F600), ">", "'",
		"\"", "_x0041_", "=",
	];

	private static readonly string[] AttributePieces =
	[
		"abc", "1", " ", "\t", "\n", "\r\n", "\r", "&amp;", "&lt;", "&quot;", "&apos;", "&#10;", "&#13;", "&#9;", "&#x20AC;",
		"é", char.ConvertFromUtf32(0x1F600), ">", "'", "x:y", "/",
	];

	private static readonly string[] CDataPieces = ["a", "<b>", "&amp;", "]]", "]", "\r\n", "\r", "\n", "é", " ", "&#0;"];

	private static readonly string[] Spaces = [" ", "  ", "\n  ", "\t", "\r\n"];

	[Fact]
	public async Task ReadsWhatXmlReaderReads()
	{
		for (var seed = 0; seed < 200; seed++)
		{
			var xml = Document(new Random(seed), out var encoding);
			var expected = XmlScan.ReferenceEvents(xml);
			Assert.NotEmpty(expected);

			await XmlScan.InEveryModeAsync(async mode =>
			{
				var actual = await XmlScan.EventsAsync(xml, mode);
				if (!expected.SequenceEqual(actual))
				{
					Assert.Fail($"Seed {seed} ({encoding.WebName}): expected{Environment.NewLine}{string.Join(Environment.NewLine, expected)}{Environment.NewLine}but got{Environment.NewLine}{string.Join(Environment.NewLine, actual)}");
				}
			});
		}
	}

	[Fact]
	public async Task EveryTruncationThrowsOrReadsTheWholeDocument()
	{
		// Sync one-byte reads split the input everywhere; async reads take the async paths (one-byte async reads, which
		// yield every byte, would make this slow).
		var modes = new[] { ScanMode.All[2], ScanMode.All[3], ScanMode.All[6] };
		for (var seed = 1000; seed < 1012; seed++)
		{
			var xml = Document(new Random(seed), out _, maxDepth: 2);
			var expected = XmlScan.ReferenceEvents(xml);
			foreach (var mode in modes)
			{
				for (var length = 0; length < xml.Length; length++)
				{
					try
					{
						var actual = await XmlScan.EventsAsync(xml.AsSpan(0, length).ToArray(), mode);

						// Only what follows the root element can be cut off without an error.
						Assert.Equal(expected, actual);
					}
					catch (InvalidDataException)
					{
					}
				}
			}
		}
	}

	/// <summary>Writes a random document.</summary>
	/// <param name="random">The source of the document's choices.</param>
	/// <param name="encoding">The encoding of the document.</param>
	/// <param name="maxDepth">How deep elements nest below the root at most.</param>
	/// <returns>The bytes of the document, with a byte order mark for UTF-16 and for some UTF-8.</returns>
	private static byte[] Document(Random random, out Encoding encoding, int maxDepth = 4)
	{
		var choice = random.Next(4);
		encoding = choice switch
		{
			0 => new UTF8Encoding(false),
			1 => new UTF8Encoding(true),
			2 => new UnicodeEncoding(false, true),
			_ => new UnicodeEncoding(true, true),
		};

		var builder = new StringBuilder();
		if (random.Next(3) > 0)
		{
			builder.Append("<?xml version=\"1.0\" encoding=\"").Append(choice < 2 ? "UTF-8" : "UTF-16").Append("\" standalone=\"yes\"?>");
		}

		Misc(random, builder);
		Element(random, builder, 0, maxDepth);
		Misc(random, builder);

		return [.. encoding.GetPreamble(), .. encoding.GetBytes(builder.ToString())];
	}

	/// <summary>Writes an element, its attributes and its content.</summary>
	/// <param name="random">The source of choices.</param>
	/// <param name="builder">Where the element goes.</param>
	/// <param name="depth">The depth of the element: 0 for the root.</param>
	/// <param name="maxDepth">How deep elements nest below the root at most.</param>
	private static void Element(Random random, StringBuilder builder, int depth, int maxDepth)
	{
		var name = Pick(random, Names);
		builder.Append('<').Append(name);
		if (depth == 0)
		{
			builder.Append(" xmlns=\"urn:main\" xmlns:x=\"urn:x\"").Append(Pick(random, Spaces)).Append("xmlns:r=\"urn:r\"");
		}

		foreach (var attribute in AttributeNames.OrderBy(_ => random.Next()).Take(random.Next(4)))
		{
			builder.Append(Pick(random, Spaces)).Append(attribute).Append(random.Next(4) == 0 ? " = " : "=");
			var quote = random.Next(3) == 0 ? '\'' : '"';
			builder.Append(quote);
			for (var i = random.Next(4); i > 0; i--)
			{
				var piece = Pick(random, AttributePieces);
				builder.Append(piece == quote.ToString() ? (quote == '"' ? "&quot;" : "&apos;") : piece);
			}

			builder.Append(quote);
		}

		if (depth > 0 && random.Next(5) == 0)
		{
			builder.Append(random.Next(2) == 0 ? "/>" : " />");

			return;
		}

		builder.Append(random.Next(6) == 0 ? Pick(random, Spaces) + ">" : ">");
		for (var count = random.Next(depth == 0 ? 3 : 0, 7); count > 0; count--)
		{
			switch (random.Next(depth < maxDepth ? 8 : 5))
			{
				case 0:
				case 1:
					for (var i = random.Next(1, 5); i > 0; i--)
					{
						builder.Append(Pick(random, TextPieces));
					}

					break;
				case 2:
					builder.Append("<![CDATA[");
					for (var i = random.Next(4); i > 0; i--)
					{
						builder.Append(Pick(random, CDataPieces));
					}

					builder.Append("]]>");

					break;
				case 3:
					builder.Append(random.Next(2) == 0 ? "<!-- a comment - <x> -->" : "<!---->");

					break;
				case 4:
					builder.Append(random.Next(2) == 0 ? "<?pi some <data> ?>" : "<?target?>");

					break;
				default:
					Element(random, builder, depth + 1, maxDepth);

					break;
			}
		}

		builder.Append("</").Append(name).Append(random.Next(6) == 0 ? Pick(random, Spaces) + ">" : ">");
	}

	/// <summary>Writes what can come before or after the root element: white space, comments and processing instructions.</summary>
	/// <param name="random">The source of choices.</param>
	/// <param name="builder">Where it goes.</param>
	private static void Misc(Random random, StringBuilder builder)
	{
		for (var count = random.Next(3); count > 0; count--)
		{
			builder.Append(random.Next(3) switch
			{
				0 => Pick(random, Spaces),
				1 => "<!-- misc -->",
				_ => "<?misc data?>",
			});
		}
	}

	/// <summary>Picks one of <paramref name="items"/>.</summary>
	/// <param name="random">The source of choices.</param>
	/// <param name="items">The items.</param>
	/// <returns>The item picked.</returns>
	private static string Pick(Random random, string[] items) => items[random.Next(items.Length)];
}
