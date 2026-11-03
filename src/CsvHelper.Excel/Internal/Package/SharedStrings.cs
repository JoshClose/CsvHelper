using System;
using System.Buffers.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// The shared strings of a workbook, from its shared strings part: the text of each <c>si</c> element, in order, which a
/// cell of type <c>s</c> indexes. The text is UTF-8 in one byte array (the arena), with the start of each string in an
/// <see cref="int"/> array, so that a cell copies its string with one copy and no <see cref="string"/>. It is immutable
/// once loaded, so the readers of a workbook share it.
/// </summary>
/// <remarks>
/// <para>
/// A string's text is the text of the <c>t</c> elements directly under its <c>si</c>, or under an <c>r</c> (a rich text
/// run) there, joined in order. Phonetic runs (<c>rPh</c>), phonetic properties (<c>phoneticPr</c>), run properties
/// (<c>rPr</c>), unknown elements, elements inside a <c>t</c>, and text outside a <c>t</c> are skipped. Each <c>t</c> is
/// decoded as XML (entities, character references, CDATA, line ends), and then for its <c>_xHHHH_</c> escapes
/// (<see cref="XmlText.DecodeEscapes"/>) on its own, as the string value it is. Between the two, a <c>t</c> that
/// <c>xml:space="preserve"</c> doesn't apply to (on it, or inherited from its <c>r</c>, <c>si</c> or the root) loses the
/// white space around it, as Excel trims it (<see cref="XmlText.TrimWhitespace"/>). An <c>si</c> without text is an empty
/// string, which keeps its place.
/// </para>
/// <para>
/// The part is read through its window, never whole. The arrays start at capacities from the part's declared size and
/// <c>uniqueCount</c>, each capped, since either can be wrong; they grow as needed, and are trimmed at the end, so that
/// the table takes the UTF-8 size of its strings plus 4 bytes each. A table past what an array holds
/// (<c>Array.MaxLength</c> bytes, or as many strings) throws <see cref="InvalidDataException"/>.
/// </para>
/// </remarks>
internal sealed class SharedStrings
{
	/// <summary>The most strings the first capacity is for, whatever <c>uniqueCount</c> says.</summary>
	private const int MaxInitialCount = 1 << 20;

	/// <summary>The most bytes the arena's first capacity has, whatever the part's declared size says.</summary>
	private const int MaxInitialArena = 16 << 20;

	/// <summary>The fewest strings the table makes room for.</summary>
	private const int MinCount = 16;

	/// <summary>The fewest bytes the arena makes room for.</summary>
	private const int MinArena = 256;

	/// <summary>
	/// The markup of a plain string, <c>&lt;si&gt;&lt;t&gt;&lt;/t&gt;&lt;/si&gt;</c>, which the arena's first capacity takes off
	/// the part's declared size for each string <c>uniqueCount</c> declares.
	/// </summary>
	private const int MarkupPerString = 16;

	private readonly byte[] arena;
	private readonly int[] starts;

	/// <summary>Creates the table.</summary>
	/// <param name="arena">The text of the strings, one after another; its length may be past the last string's end.</param>
	/// <param name="starts">The start of each string in the arena, and then the end of the last: at least <paramref name="count"/> + 1 entries.</param>
	/// <param name="count">The number of strings.</param>
	/// <param name="partName">The part the strings are from, or <see langword="null"/> for <see cref="Empty"/>.</param>
	private SharedStrings(byte[] arena, int[] starts, int count, string? partName)
	{
		this.arena = arena;
		this.starts = starts;
		Count = count;
		PartName = partName;
	}

	/// <summary>The shared strings of a workbook without a shared strings part: none.</summary>
	public static SharedStrings Empty { get; } = new([], [0], 0, null);

	/// <summary>The number of strings.</summary>
	public int Count { get; }

	/// <summary>
	/// The part the strings are from, or <see langword="null"/> when the workbook has none: it names no shared strings
	/// part, or names one the package lacks.
	/// </summary>
	public string? PartName { get; }

	/// <summary>The bytes the table holds on to: the arena's capacity, and 4 bytes for each entry of the starts.</summary>
	public long MemorySize => arena.Length + 4L * starts.Length;

	/// <summary>Gets a string's text.</summary>
	/// <param name="index">The string's 0-based index: a cell's <c>&lt;v&gt;</c>.</param>
	/// <param name="text">The string's text as UTF-8, decoded; empty when <paramref name="index"/> is out of range.</param>
	/// <returns>Whether <paramref name="index"/> is in range.</returns>
	public bool TryGet(int index, out ReadOnlySpan<byte> text)
	{
		if ((uint)index >= (uint)Count)
		{
			text = default;

			return false;
		}

		var start = starts[index];
		text = arena.AsSpan(start, starts[index + 1] - start);

		return true;
	}

	/// <summary>Reads a workbook's shared strings part.</summary>
	/// <param name="archive">The package.</param>
	/// <param name="partName">The shared strings part; <see langword="null"/>, or a part that is missing, gives <see cref="Empty"/>.</param>
	/// <param name="limits">The limits of the call.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <returns>A task whose result is the shared strings.</returns>
	/// <exception cref="InvalidDataException">The part can't be read, passes a limit, or holds more than an array can.</exception>
	public static async ValueTask<SharedStrings> LoadAsync(PackageArchive archive, string? partName, PartLimits limits, bool async, CancellationToken cancellationToken)
	{
		// As with styles: a cell that indexes a shared string then fails at that cell, and a sheet without one still reads.
		if (partName is null || !archive.Contains(partName))
		{
			return Empty;
		}

		var declaredSize = archive.DeclaredSize(partName);
		var scanner = await archive.OpenXmlAsync(partName, true, limits, async, cancellationToken).ConfigureAwait(false);
		try
		{
			return await ReadAsync(scanner, declaredSize, async, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await scanner.DisposeAsync(async).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Reads shared strings from a scanner at the start of their part. The parsing is synchronous over what the window
	/// holds, and only the refills read the part's stream, synchronously or not as <paramref name="async"/> says.
	/// </summary>
	/// <param name="scanner">The scanner, which the caller disposes.</param>
	/// <param name="declaredSize">The part's size as its zip declares it: a hint for the arena's first capacity.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <param name="maxLength">The most bytes, and one more than the most strings, the table holds: <c>Array.MaxLength</c> unless a test says otherwise.</param>
	/// <returns>A task whose result is the shared strings.</returns>
	/// <exception cref="InvalidDataException">The part can't be read, passes a limit, or holds more than <paramref name="maxLength"/> allows.</exception>
	internal static async ValueTask<SharedStrings> ReadAsync(XmlScanner scanner, long declaredSize, bool async, CancellationToken cancellationToken, int? maxLength = null)
	{
		var parser = new Parser(scanner, declaredSize, maxLength ?? Array.MaxLength);
		while (!parser.TryParse())
		{
			if (async)
			{
				await scanner.RefillAsync(cancellationToken).ConfigureAwait(false);
			}
			else
			{
				scanner.Refill();
			}
		}

		return parser.ToTable();
	}

	/// <summary>Where the parser is in the part.</summary>
	private enum Mode : byte
	{
		/// <summary>Before the root element.</summary>
		Root,

		/// <summary>In <c>sst</c>, between strings.</summary>
		Table,

		/// <summary>In an <c>si</c>.</summary>
		String,

		/// <summary>In an <c>r</c> of an <c>si</c>.</summary>
		Run,

		/// <summary>In a <c>t</c> whose text counts.</summary>
		Text,

		/// <summary>Skipping an element, to go back to the mode it was skipped in.</summary>
		Skip,
	}

	/// <summary>
	/// Parses the part token by token, from what the window holds. Its state is kept between calls, so that a refill can
	/// come between any two tokens: the text already decoded is in the arena, not the window.
	/// </summary>
	private sealed class Parser
	{
		private readonly XmlScanner scanner;
		private readonly long declaredSize;
		private readonly int maxLength;
		private byte[] arena = [];
		private int length;
		private int[] starts = [0];
		private int count;
		private Mode mode;
		// The mode a skip goes back to, and the mode the end of a t goes back to (String or Run).
		private Mode resume;
		private Mode textParent;
		// Whether xml:space="preserve" applies in the root, the current si and r, and whether the current t is trimmed.
		private bool rootPreserves;
		private bool stringPreserves;
		private bool runPreserves;
		private bool trimText;
		private int skipDepth;
		private int textStart;

		/// <summary>Creates the parser.</summary>
		/// <param name="scanner">The scanner, at the start of the part.</param>
		/// <param name="declaredSize">The part's size as its zip declares it.</param>
		/// <param name="maxLength">The most bytes, and one more than the most strings, the table holds.</param>
		public Parser(XmlScanner scanner, long declaredSize, int maxLength)
		{
			this.scanner = scanner;
			this.declaredSize = declaredSize;
			this.maxLength = maxLength;
		}

		/// <summary>Parses the tokens the window holds.</summary>
		/// <returns><see langword="true"/> at the end of the part; <see langword="false"/> when the window needs a refill.</returns>
		/// <exception cref="InvalidDataException">The XML can't be read, its root is not <c>sst</c>, or the table is too large.</exception>
		public bool TryParse()
		{
			while (true)
			{
				if (mode == Mode.Skip)
				{
					if (!scanner.TrySkipTo(skipDepth))
					{
						return false;
					}

					mode = resume;
				}

				if (!scanner.TryRead())
				{
					return false;
				}

				switch (scanner.Kind)
				{
					case XmlTokenKind.StartTag:
						StartTag();

						break;
					case XmlTokenKind.EndTag:
						EndTag();

						break;
					case XmlTokenKind.Text:
					case XmlTokenKind.CData:
						// Outside a t, this is the white space between elements, or text that is not part of a string.
						if (mode == Mode.Text)
						{
							Append();
						}

						break;
					case XmlTokenKind.End:
						return true;
				}
			}
		}

		/// <summary>Makes the table, trimming arrays that have more than an eighth to spare.</summary>
		/// <returns>The shared strings.</returns>
		public SharedStrings ToTable()
		{
			if (arena.Length - length > length / 8)
			{
				Array.Resize(ref arena, length);
			}

			var used = count + 1;
			if (starts.Length - used > used / 8)
			{
				Array.Resize(ref starts, used);
			}

			return new SharedStrings(arena, starts, count, scanner.Window.PartName);
		}

		/// <summary>Handles a start tag: the root, a string, a run or a text, or an element to skip.</summary>
		/// <exception cref="InvalidDataException">The root element is not <c>sst</c>.</exception>
		private void StartTag()
		{
			switch (mode)
			{
				case Mode.Root:
					if (!scanner.IsStartTag("sst"u8))
					{
						throw scanner.Error("its root element is not sst");
					}

					Reserve(UniqueCount());
					rootPreserves = scanner.SpaceOf() ?? false;
					mode = Mode.Table;

					return;
				case Mode.Table:
					if (scanner.IsStartTag("si"u8))
					{
						if (scanner.IsEmptyElement)
						{
							EndString();
						}
						else
						{
							stringPreserves = scanner.SpaceOf() ?? rootPreserves;
							mode = Mode.String;
						}

						return;
					}

					break;
				case Mode.String:
					if (scanner.IsStartTag("t"u8))
					{
						StartText();

						return;
					}

					if (scanner.IsStartTag("r"u8))
					{
						if (!scanner.IsEmptyElement)
						{
							runPreserves = scanner.SpaceOf() ?? stringPreserves;
							mode = Mode.Run;
						}

						return;
					}

					break;
				case Mode.Run:
					if (scanner.IsStartTag("t"u8))
					{
						StartText();

						return;
					}

					break;
			}

			// extLst in sst; rPh and phoneticPr in si; rPr in r; anything inside a t.
			if (!scanner.IsEmptyElement)
			{
				resume = mode;
				skipDepth = scanner.Depth - 1;
				mode = Mode.Skip;
			}
		}

		/// <summary>Handles an end tag: the end of the element the mode is in. An element inside it was skipped whole.</summary>
		private void EndTag()
		{
			// In Table, it is the root's end tag, after which the scanner allows no element.
			switch (mode)
			{
				case Mode.Text:
					if (trimText)
					{
						length = textStart + XmlText.TrimWhitespace(arena.AsSpan(textStart, length - textStart));
					}

					length = textStart + XmlText.DecodeEscapes(arena.AsSpan(textStart, length - textStart));
					mode = textParent;

					break;
				case Mode.Run:
					mode = Mode.String;

					break;
				case Mode.String:
					EndString();
					mode = Mode.Table;

					break;
			}
		}

		/// <summary>Starts a <c>t</c>, whose text the string gets (nothing for an empty one), trimmed unless <c>xml:space="preserve"</c> applies.</summary>
		private void StartText()
		{
			if (scanner.IsEmptyElement)
			{
				return;
			}

			textStart = length;
			textParent = mode;
			trimText = !(scanner.SpaceOf() ?? (mode == Mode.Run ? runPreserves : stringPreserves));
			mode = Mode.Text;
		}

		/// <summary>Adds the decoded XML of a text or CDATA token to the string; the escapes wait for the end of its <c>t</c>.</summary>
		/// <exception cref="InvalidDataException">The text has a bad reference, or the arena would pass <see cref="maxLength"/>.</exception>
		private void Append()
		{
			var raw = scanner.Content.Length;
			if (arena.Length - length < raw && arena.Length < maxLength)
			{
				var size = Math.Max(Math.Max(2L * arena.Length, (long)length + raw), MinArena);
				Array.Resize(ref arena, (int)Math.Min(size, maxLength));
			}

			if (arena.Length - length >= raw)
			{
				length += scanner.DecodeContent(arena.AsSpan(length));

				return;
			}

			// At the limit, the XML can be longer than the room left while its decoded text still fits.
			var decoded = new byte[raw];
			var written = scanner.DecodeContent(decoded);
			if (written > arena.Length - length)
			{
				throw scanner.Error($"its strings come to more than {maxLength} bytes, the most the table can hold");
			}

			decoded.AsSpan(0, written).CopyTo(arena.AsSpan(length));
			length += written;
		}

		/// <summary>Ends a string at the arena's length.</summary>
		/// <exception cref="InvalidDataException">The table would pass <see cref="maxLength"/> entries.</exception>
		private void EndString()
		{
			if (count + 1 == starts.Length)
			{
				if (starts.Length >= maxLength)
				{
					throw scanner.Error($"it has more than {maxLength - 1} strings, the most the table can hold");
				}

				var size = Math.Max(2L * starts.Length, MinCount + 1);
				Array.Resize(ref starts, (int)Math.Min(size, maxLength));
			}

			starts[++count] = length;
		}

		/// <summary>The root's <c>uniqueCount</c>, the number of strings it declares.</summary>
		/// <returns>The count, or 0 when it has none, or one that is not a count: it is only a hint.</returns>
		private int UniqueCount()
		{
			var attributes = scanner.Attributes;
			while (attributes.MoveNext())
			{
				if (attributes.IsName("uniqueCount"u8))
				{
					var value = attributes.Value;

					return Utf8Parser.TryParse(value, out int uniqueCount, out var consumed) && consumed == value.Length && uniqueCount > 0 ? uniqueCount : 0;
				}
			}

			return 0;
		}

		/// <summary>
		/// Makes the first room: for the strings <c>uniqueCount</c> declares, and for the part's declared size less their
		/// markup, each capped.
		/// </summary>
		/// <param name="uniqueCount">The strings the root declares; 0 when it declares none.</param>
		private void Reserve(int uniqueCount)
		{
			var strings = Math.Min(Math.Max(uniqueCount, MinCount), MaxInitialCount);
			starts = new int[Math.Min(strings + 1, maxLength)];
			var markup = (long)MarkupPerString * uniqueCount;
			var text = markup < declaredSize ? declaredSize - markup : MinArena;
			arena = new byte[(int)Math.Min(Math.Min(Math.Max(text, MinArena), MaxInitialArena), maxLength)];
		}
	}
}
