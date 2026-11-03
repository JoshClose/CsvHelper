using System.Text;
using System.Xml;

namespace CsvHelper.Excel.Tests.Xml;

/// <summary>A way of reading a part: the stream, the window's starting size, and sync or async.</summary>
/// <param name="name">The name of the mode, for failures.</param>
/// <param name="trickle">Whether the stream returns one byte per read, rather than all it can.</param>
/// <param name="windowSize">The size the window starts at.</param>
/// <param name="async">Whether to read asynchronously.</param>
public sealed class ScanMode(string name, bool trickle, int windowSize, bool async)
{
	/// <summary>Every mode: whole and one-byte reads, a 64 KB and a 16-byte window, sync and async.</summary>
	public static IReadOnlyList<ScanMode> All { get; } =
	[
		new("whole", false, XmlWindow.DefaultSize, false),
		new("whole async", false, XmlWindow.DefaultSize, true),
		new("whole, 16-byte window", false, 16, false),
		new("whole, 16-byte window, async", false, 16, true),
		new("one byte", true, XmlWindow.DefaultSize, false),
		new("one byte async", true, XmlWindow.DefaultSize, true),
		new("one byte, 16-byte window", true, 16, false),
		new("one byte, 16-byte window, async", true, 16, true),
	];

	/// <summary>The name of the mode, for failures.</summary>
	public string Name => name;

	/// <summary>Whether the stream returns one byte per read, rather than all it can.</summary>
	public bool Trickle => trickle;

	/// <summary>The size the window starts at.</summary>
	public int WindowSize => windowSize;

	/// <summary>Whether to read asynchronously.</summary>
	public bool Async => async;

	/// <inheritdoc/>
	public override string ToString() => name;
}

/// <summary>Reads parts with <see cref="XmlScanner"/>, and with <see cref="XmlReader"/> to compare.</summary>
internal static class XmlScan
{
	/// <summary>The part name of the tests' parts.</summary>
	public const string PartName = "test.xml";

	/// <summary>Opens a scanner over <paramref name="xml"/>.</summary>
	/// <param name="xml">The part.</param>
	/// <param name="mode">How to read it.</param>
	/// <param name="maxSize">The window's size limit.</param>
	/// <returns>The scanner.</returns>
	public static XmlScanner Open(byte[] xml, ScanMode mode, long maxSize = 1 << 20)
	{
		Stream stream = mode.Trickle ? new TrickleStream(xml) : new MemoryStream(xml);

		return new XmlScanner(new XmlWindow(stream, PartName, maxSize, mode.WindowSize));
	}

	/// <summary>Reads the next token as <paramref name="mode"/> says.</summary>
	/// <param name="scanner">The scanner.</param>
	/// <param name="mode">How to read.</param>
	/// <returns>Whether a token other than the end was read.</returns>
	public static async Task<bool> NextAsync(this XmlScanner scanner, ScanMode mode) =>
		mode.Async ? await scanner.ReadAsync(CancellationToken.None) : scanner.Read();

	/// <summary>Skips to a depth as <paramref name="mode"/> says.</summary>
	/// <param name="scanner">The scanner.</param>
	/// <param name="depth">The depth to skip to.</param>
	/// <param name="mode">How to read.</param>
	/// <returns>A task that completes at the depth.</returns>
	public static async Task SkipAsync(this XmlScanner scanner, int depth, ScanMode mode)
	{
		if (mode.Async)
		{
			await scanner.SkipToAsync(depth, CancellationToken.None);
		}
		else
		{
			scanner.SkipTo(depth);
		}
	}

	/// <summary>
	/// Reads a part into events: <c>&lt;name a="v"&gt;</c> for a start tag (with decoded attributes, without namespace
	/// declarations), <c>&lt;/name&gt;</c> for an end tag (an empty element has both), and the decoded text inside the root
	/// element, with adjacent text and CDATA joined.
	/// </summary>
	/// <param name="xml">The part.</param>
	/// <param name="mode">How to read it.</param>
	/// <param name="maxSize">The window's size limit.</param>
	/// <returns>The events.</returns>
	public static async Task<List<string>> EventsAsync(byte[] xml, ScanMode mode, long maxSize = 1 << 20)
	{
		using var scanner = Open(xml, mode, maxSize);
		var events = new Events();
		while (await scanner.NextAsync(mode))
		{
			switch (scanner.Kind)
			{
				case XmlTokenKind.Text:
				case XmlTokenKind.CData:
					if (scanner.Depth > 0)
					{
						events.Text(DecodeContent(scanner));
					}

					break;
				case XmlTokenKind.StartTag:
					var attributes = new List<(string, string)>();
					var enumerator = scanner.Attributes;
					while (enumerator.MoveNext())
					{
						attributes.Add((Encoding.UTF8.GetString(enumerator.LocalName.ToArray()), enumerator.DecodeValue()));
					}

					var name = Encoding.UTF8.GetString(scanner.LocalName.ToArray());
					events.Start(name, attributes);
					if (scanner.IsEmptyElement)
					{
						events.End(name);
					}

					break;
				case XmlTokenKind.EndTag:
					events.End(Encoding.UTF8.GetString(scanner.LocalName.ToArray()));

					break;
			}
		}

		return events.Done();
	}

	/// <summary>Reads a part into events with <see cref="XmlReader"/>, as <see cref="EventsAsync"/> does with the scanner.</summary>
	/// <param name="xml">The part.</param>
	/// <returns>The events.</returns>
	public static List<string> ReferenceEvents(byte[] xml)
	{
		var settings = new XmlReaderSettings
		{
			DtdProcessing = DtdProcessing.Prohibit,
			IgnoreComments = true,
			IgnoreProcessingInstructions = true,
		};
		using var reader = XmlReader.Create(new MemoryStream(xml), settings);
		var events = new Events();
		while (reader.Read())
		{
			switch (reader.NodeType)
			{
				case XmlNodeType.Text:
				case XmlNodeType.CDATA:
				case XmlNodeType.Whitespace:
				case XmlNodeType.SignificantWhitespace:
					if (reader.Depth > 0)
					{
						events.Text(reader.Value);
					}

					break;
				case XmlNodeType.Element:
					var attributes = new List<(string, string)>();
					for (var i = 0; i < reader.AttributeCount; i++)
					{
						reader.MoveToAttribute(i);
						if (reader.Prefix != "xmlns" && !(reader.Prefix.Length == 0 && reader.LocalName == "xmlns"))
						{
							attributes.Add((reader.LocalName, reader.Value));
						}
					}

					reader.MoveToElement();
					events.Start(reader.LocalName, attributes);
					if (reader.IsEmptyElement)
					{
						events.End(reader.LocalName);
					}

					break;
				case XmlNodeType.EndElement:
					events.End(reader.LocalName);

					break;
			}
		}

		return events.Done();
	}

	/// <summary>Decodes the content of the scanner's text or CDATA token.</summary>
	/// <param name="scanner">The scanner.</param>
	/// <returns>The decoded text.</returns>
	public static string DecodeContent(XmlScanner scanner)
	{
		var decoded = new byte[scanner.Content.Length];
		var written = scanner.DecodeContent(decoded);

		return Encoding.UTF8.GetString(decoded, 0, written);
	}

	/// <summary>Runs <paramref name="test"/> in every mode, and says which mode failed.</summary>
	/// <param name="test">The test, for one mode.</param>
	/// <returns>A task that completes when every mode has passed.</returns>
	public static async Task InEveryModeAsync(Func<ScanMode, Task> test)
	{
		foreach (var mode in ScanMode.All)
		{
			try
			{
				await test(mode);
			}
			catch (Exception e)
			{
				throw new Xunit.Sdk.XunitException($"In mode '{mode}': {e.Message}", e);
			}
		}
	}

	/// <summary>Collects events, joining adjacent text.</summary>
	private sealed class Events
	{
		private readonly List<string> events = [];
		private readonly StringBuilder text = new();

		/// <summary>Adds text, to join with adjacent text.</summary>
		/// <param name="value">The decoded text.</param>
		public void Text(string value) => text.Append(value);

		/// <summary>Adds a start tag.</summary>
		/// <param name="name">The local name.</param>
		/// <param name="attributes">The local names and decoded values of the attributes.</param>
		public void Start(string name, List<(string Name, string Value)> attributes)
		{
			FlushText();
			events.Add($"<{name}{string.Concat(attributes.Select(a => $" {a.Name}=\"{a.Value}\""))}>");
		}

		/// <summary>Adds an end tag.</summary>
		/// <param name="name">The local name.</param>
		public void End(string name)
		{
			FlushText();
			events.Add($"</{name}>");
		}

		/// <summary>Ends the events.</summary>
		/// <returns>The events.</returns>
		public List<string> Done()
		{
			FlushText();

			return events;
		}

		/// <summary>Adds the joined text, if there is any.</summary>
		private void FlushText()
		{
			if (text.Length > 0)
			{
				events.Add(text.ToString());
				text.Clear();
			}
		}
	}
}
