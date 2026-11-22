using System;
using System.Buffers.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// The shared strings of a workbook, from its shared strings part: the text of each <c>si</c> element, in order, which a
/// cell of type <c>s</c> indexes. The text is UTF-8 in byte arrays (the chunks), each string in one, with the start of
/// each string in an <see cref="int"/> array, so that a cell copies its string with one copy and no <see cref="string"/>.
/// It is immutable once loaded, so the readers of a workbook share it.
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
/// A string's start is an offset in stretches of 4 MiB, and each chunk starts at a stretch's first offset: an offset's
/// chunk is at <c>offset &gt;&gt; 22</c>, and its place there is <c>offset &amp; (4 MiB - 1)</c>. A string never crosses
/// two chunks; when the next string starts in a later stretch, a string ends where its chunk's text does, which the table
/// keeps for each chunk. A chunk of several strings takes one stretch. It is sized from the text still to come, as the
/// part's declared size less the markup of the strings <c>uniqueCount</c> declares estimates it, from 256 bytes to 4 MiB,
/// so that a part that declares both honestly fills its chunks; since either can be wrong, once the estimate is used up
/// each chunk is twice the one before. A string that outgrows its chunk while its text arrives moves, with what it has,
/// to a new chunk, or grows the chunk when it has it to itself. A string longer than 4 MiB has a chunk of its own, which
/// grows by doubling, over as many stretches as it needs; the string after it starts at the next.
/// </para>
/// <para>
/// The part is read through its window, never whole. Growing the text copies only the strings that move, and the starts
/// grow from a capacity for <c>uniqueCount</c>, capped. A chunk is trimmed when it closes (when a string moves out of it,
/// when the long string it holds ends, and at the end of the part) if more than an eighth of it is spare, and the starts
/// at the end by the same rule: the table takes the UTF-8 size of its strings plus 4 bytes each, and at most an eighth
/// more. An estimate too large allocates at most one chunk of 4 MiB more, which the trim gives back. A table past what an
/// array holds (<c>Array.MaxLength</c> bytes, or as many strings), or whose offsets would pass <see cref="int.MaxValue"/>
/// (past about 2 GB), throws <see cref="InvalidDataException"/>.
/// </para>
/// </remarks>
internal sealed class SharedStrings
{
	/// <summary>The bits of an offset that are its place in its stretch: a stretch is 4 MiB of offsets.</summary>
	private const int SlotBits = 22;

	/// <summary>The place of an offset in its stretch, and so in its chunk: <c>offset &amp; SlotMask</c>.</summary>
	private const int SlotMask = (1 << SlotBits) - 1;

	/// <summary>The most bytes a chunk of several strings has: one stretch.</summary>
	private const int MaxChunk = 1 << SlotBits;

	/// <summary>The stretches an <see cref="int"/> offset can be in.</summary>
	private const int MaxSlots = 1 << (31 - SlotBits);

	/// <summary>The fewest bytes a chunk of several strings has.</summary>
	private const int MinChunk = 256;

	/// <summary>The most strings the first capacity is for, whatever <c>uniqueCount</c> says.</summary>
	private const int MaxInitialCount = 1 << 20;

	/// <summary>The fewest strings the table makes room for.</summary>
	private const int MinCount = 16;

	/// <summary>
	/// The markup of a plain string, <c>&lt;si&gt;&lt;t&gt;&lt;/t&gt;&lt;/si&gt;</c>, which the estimate of the text takes off
	/// the part's declared size for each string <c>uniqueCount</c> declares.
	/// </summary>
	private const int MarkupPerString = 16;

	// The chunks at the stretch each starts at (null at the others), and the offset where each chunk's text ends.
	private readonly byte[]?[] chunks;
	private readonly int[] ends;
	private readonly int[] starts;

	/// <summary>Creates the table.</summary>
	/// <param name="chunks">
	/// The chunks of text, each at the stretch it starts at and <see langword="null"/> at a stretch none starts at: as many
	/// as the stretch of the last string's end needs.
	/// </param>
	/// <param name="ends">The offset where each chunk's text ends, at the same index as the chunk.</param>
	/// <param name="starts">The offset of each string, and then the end of the last: at least <paramref name="count"/> + 1 entries.</param>
	/// <param name="count">The number of strings.</param>
	/// <param name="partName">The part the strings are from, or <see langword="null"/> for <see cref="Empty"/>.</param>
	private SharedStrings(byte[]?[] chunks, int[] ends, int[] starts, int count, string? partName)
	{
		this.chunks = chunks;
		this.ends = ends;
		this.starts = starts;
		Count = count;
		PartName = partName;

		var size = 4L * starts.Length;
		foreach (var chunk in chunks)
		{
			size += chunk?.Length ?? 0;
		}

		MemorySize = size;
	}

	/// <summary>The shared strings of a workbook without a shared strings part: none.</summary>
	public static SharedStrings Empty { get; } = new([null], [0], [0], 0, null);

	/// <summary>The number of strings.</summary>
	public int Count { get; }

	/// <summary>
	/// The part the strings are from, or <see langword="null"/> when the workbook has none: it names no shared strings
	/// part, or names one the package lacks.
	/// </summary>
	public string? PartName { get; }

	/// <summary>
	/// The bytes the table holds on to: the lengths of its chunks, and 4 bytes for each entry of the starts. The arrays that
	/// find a chunk (under 6 KB) aren't counted.
	/// </summary>
	public long MemorySize { get; }

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
		var end = starts[index + 1];
		var slot = start >> SlotBits;
		if (((start ^ end) >> SlotBits) != 0)
		{
			// The next string starts in a later chunk: this one ends where its chunk's text does.
			end = ends[slot];
		}

		// A string starts in the stretch its chunk starts at; an empty one can start where no chunk does.
		text = chunks[slot].AsSpan(start & SlotMask, end - start);

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
	/// <param name="declaredSize">The part's size as its zip declares it: a hint for the chunks' sizes.</param>
	/// <param name="async">Whether to read asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous read.</param>
	/// <param name="maxLength">The most bytes, and one more than the most strings, the table holds: <c>Array.MaxLength</c> unless a test says otherwise.</param>
	/// <param name="maxChunk">
	/// The most bytes a chunk of several strings has: 4 MiB unless a test says less, so that small parts reach a chunk's
	/// edges. Each chunk still takes a stretch of 4 MiB, and under 256 bytes every chunk of several strings has this size.
	/// </param>
	/// <returns>A task whose result is the shared strings.</returns>
	/// <exception cref="InvalidDataException">The part can't be read, passes a limit, or holds more than <paramref name="maxLength"/> allows.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxChunk"/> is not from 1 byte to 4 MiB.</exception>
	internal static async ValueTask<SharedStrings> ReadAsync(XmlScanner scanner, long declaredSize, bool async, CancellationToken cancellationToken, int? maxLength = null, int? maxChunk = null)
	{
		var parser = new Parser(scanner, declaredSize, maxLength ?? Array.MaxLength, maxChunk ?? MaxChunk);
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
	/// come between any two tokens: the text already decoded is in the chunks, not the window.
	/// </summary>
	private sealed class Parser
	{
		private readonly XmlScanner scanner;
		private readonly long declaredSize;
		private readonly int maxLength;
		private readonly int maxChunk;
		// The chunks at the stretch each starts at, and the offset where each one's text ends, once it has an end.
		private byte[]?[] chunks = [];
		private int[] ends = [];
		// The chunk text goes in, its stretch, and whether it is an oversized string's own: no chunk before the first text,
		// nor after an oversized string until the next text.
		private byte[]? chunk;
		private int slot;
		private bool oversized;
		// The bytes of text the part's declared size suggests, from the root, and the size of the last chunk of several
		// strings, which the next doubles once the estimate is used up.
		private long estimate;
		private int lastSize;
		// The offset the next text goes at, and the bytes of the strings before the current one.
		private int length;
		private int done;
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
		/// <param name="maxChunk">The most bytes a chunk of several strings has: from 1 byte to 4 MiB.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxChunk"/> is out of its range.</exception>
		public Parser(XmlScanner scanner, long declaredSize, int maxLength, int maxChunk)
		{
			if (maxChunk is < 1 or > MaxChunk)
			{
				throw new ArgumentOutOfRangeException(nameof(maxChunk));
			}

			this.scanner = scanner;
			this.declaredSize = declaredSize;
			this.maxLength = maxLength;
			this.maxChunk = maxChunk;
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

		/// <summary>
		/// Makes the table: the current chunk's text ends at the last string's end, the last chunk and the starts are trimmed
		/// when more than an eighth of them is spare, and the chunks reach the stretch of the last string's end, where an
		/// empty string can start.
		/// </summary>
		/// <returns>The shared strings.</returns>
		public SharedStrings ToTable()
		{
			if (chunk is not null)
			{
				Close(length);
			}

			var used = count + 1;
			if (starts.Length - used > used / 8)
			{
				Array.Resize(ref starts, used);
			}

			var slots = (starts[count] >> SlotBits) + 1;
			if (chunks.Length != slots)
			{
				Array.Resize(ref chunks, slots);
				Array.Resize(ref ends, slots);
			}

			return new SharedStrings(chunks, ends, starts, count, scanner.Window.PartName);
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
					// A t with text has it in the chunk, from textStart: a string moves whole.
					if (length > textStart)
					{
						var place = textStart - (slot << SlotBits);
						if (trimText)
						{
							length = textStart + XmlText.TrimWhitespace(chunk.AsSpan(place, length - textStart));
						}

						length = textStart + XmlText.DecodeEscapes(chunk.AsSpan(place, length - textStart));
					}

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
		/// <exception cref="InvalidDataException">The text has a bad reference, or the strings would pass <see cref="maxLength"/> bytes or the last offset.</exception>
		private void Append()
		{
			var raw = scanner.Content.Length;
			if (raw == 0)
			{
				return;
			}

			// The bytes the strings can still take: the text of each, a moved string's counted once.
			var room = maxLength - done - (length - starts[count]);
			if (raw <= room)
			{
				MakeRoom(raw);
				length += scanner.DecodeContent(chunk.AsSpan(length - (slot << SlotBits)));

				return;
			}

			// At the limit, the XML can be longer than the room left while its decoded text still fits.
			var decoded = new byte[raw];
			var written = scanner.DecodeContent(decoded);
			if (written > room)
			{
				throw TooLong();
			}

			MakeRoom(written);
			decoded.AsSpan(0, written).CopyTo(chunk.AsSpan(length - (slot << SlotBits)));
			length += written;
		}

		/// <summary>
		/// Makes room in the chunk for more of the current string. A string that has its chunk to itself (it starts at the
		/// chunk's start) grows it; else it moves, with what it has, to a new chunk at the next stretch, and the old chunk is
		/// closed where the string began. A chunk of several strings takes what the estimate says is still to come, from 256
		/// bytes to <see cref="maxChunk"/>; once the estimate is used up, or has less left than the string needs, it is twice
		/// the last. A string that needs more than <see cref="maxChunk"/> has a chunk of its own, at least twice what it
		/// holds. Either way a string of many runs grows by doubling, never by one copy a run.
		/// </summary>
		/// <param name="bytes">The bytes to make room for: no more than the strings can still take.</param>
		/// <exception cref="InvalidDataException">The chunk would pass the last offset.</exception>
		private void MakeRoom(int bytes)
		{
			var first = slot << SlotBits;
			if (chunk is not null && first + chunk.Length - length >= bytes)
			{
				return;
			}

			var start = starts[count];
			var held = length - start;
			var need = held + bytes;

			// Without a chunk, the next text goes at a stretch's first offset: the first, or the one after an oversized string.
			var next = chunk is null ? length >> SlotBits : start == first ? slot : slot + 1;
			var nextFirst = (long)next << SlotBits;
			if (next >= MaxSlots || need > int.MaxValue - nextFirst)
			{
				throw TooLong();
			}

			// What the estimate says is still to come includes the string's bytes. Sized only by it, a string that needs more
			// would grow by what it needs, one copy a run.
			var own = need > maxChunk;
			var left = estimate - done;
			var size = own ? 2L * held : Math.Min(Math.Max(left >= need ? left : 2L * lastSize, MinChunk), maxChunk);
			size = Math.Min(Math.Max(size, need), Math.Min(int.MaxValue - nextFirst, maxLength - done));
			var grown = new byte[(int)size];
			if (held > 0)
			{
				chunk.AsSpan(start - first, held).CopyTo(grown);
			}

			if (chunk is not null && next != slot)
			{
				Close(start);
			}

			if (next >= chunks.Length)
			{
				var slots = Math.Min(Math.Max(Math.Max(2 * chunks.Length, next + 1), 4), MaxSlots);
				Array.Resize(ref chunks, slots);
				Array.Resize(ref ends, slots);
			}

			chunks[next] = grown;
			chunk = grown;
			slot = next;
			oversized = own;
			if (!own)
			{
				lastSize = grown.Length;
			}

			// The string's start, and its t's, move with its text.
			textStart = (int)nextFirst + (textStart - start);
			starts[count] = (int)nextFirst;
			length = (int)nextFirst + held;
		}

		/// <summary>Ends a string at the length; after an oversized string, the next starts at the next stretch.</summary>
		/// <exception cref="InvalidDataException">The table would pass <see cref="maxLength"/> entries, or the last offset.</exception>
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

			done += length - starts[count];
			if (oversized)
			{
				// No text follows it in its chunk.
				Close(length);
				oversized = false;
				var next = ((long)length + SlotMask) & ~(long)SlotMask;
				if (next > int.MaxValue)
				{
					throw TooLong();
				}

				length = (int)next;
			}

			starts[++count] = length;
		}

		/// <summary>
		/// Closes the chunk: no text goes in it after <paramref name="end"/>, and it is trimmed when more than an eighth of it is
		/// spare, as a chunk a long string moves out of can be nearly all.
		/// </summary>
		/// <param name="end">The offset where its text ends.</param>
		private void Close(int end)
		{
			ends[slot] = end;
			var used = end - (slot << SlotBits);
			if (chunk!.Length - used > used / 8)
			{
				Array.Resize(ref chunk, used);
				chunks[slot] = chunk;
			}

			chunk = null;
		}

		/// <summary>The error for strings past <see cref="maxLength"/> bytes, or past the last offset.</summary>
		/// <returns>The exception, at the current token.</returns>
		private InvalidDataException TooLong() => scanner.Error($"its strings come to more than {maxLength} bytes, the most the table can hold");

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
		/// Makes the first room for the strings <c>uniqueCount</c> declares, capped, and estimates their text: the part's
		/// declared size less their markup, or 256 bytes when the markup is more. Either can be wrong, and the estimate only
		/// sizes chunks, each at most 4 MiB; the first waits for the first text, so that a table without text has none.
		/// </summary>
		/// <param name="uniqueCount">The strings the root declares; 0 when it declares none.</param>
		private void Reserve(int uniqueCount)
		{
			var strings = Math.Min(Math.Max(uniqueCount, MinCount), MaxInitialCount);
			starts = new int[Math.Min(strings + 1, maxLength)];
			var markup = (long)MarkupPerString * uniqueCount;
			estimate = markup < declaredSize ? declaredSize - markup : MinChunk;
		}
	}
}
