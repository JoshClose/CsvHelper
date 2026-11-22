using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>What <see cref="XmlScanner"/> has read.</summary>
internal enum XmlTokenKind : byte
{
	/// <summary>
	/// No token: before the first read, and after a refill, a restore or a move past bytes read by the caller
	/// (<see cref="XmlScanner.SetPosition"/>), which leave nothing to point at.
	/// </summary>
	None = 0,

	/// <summary>A start tag, <c>&lt;x:c r="A1"&gt;</c>, or an empty element, <c>&lt;c r="A1"/&gt;</c> (<see cref="XmlScanner.IsEmptyElement"/>).</summary>
	StartTag = 1,

	/// <summary>An end tag, <c>&lt;/x:c&gt;</c>.</summary>
	EndTag = 2,

	/// <summary>Character data between markup, as it is in the XML (see <see cref="XmlScanner.DecodeContent"/>).</summary>
	Text = 3,

	/// <summary>The content of a CDATA section.</summary>
	CData = 4,

	/// <summary>The end of the part.</summary>
	End = 5,
}

/// <summary>
/// Where an <see cref="XmlScanner"/> is, from <see cref="XmlScanner.Save"/>, to go back to with
/// <see cref="XmlScanner.Restore"/> and read the same tokens again. Valid until the scanner's next refill.
/// </summary>
internal readonly struct XmlScannerState
{
	/// <summary>Creates the state.</summary>
	/// <param name="position">The position in the window.</param>
	/// <param name="depth">The number of open elements.</param>
	/// <param name="rootSeen">Whether the root element has started.</param>
	/// <param name="rootClosed">Whether the root element has ended.</param>
	/// <param name="refills">The number of refills before the state was saved.</param>
	internal XmlScannerState(int position, int depth, bool rootSeen, bool rootClosed, int refills)
	{
		Position = position;
		Depth = depth;
		RootSeen = rootSeen;
		RootClosed = rootClosed;
		Refills = refills;
	}

	/// <summary>The position in the window.</summary>
	internal int Position { get; }

	/// <summary>The number of open elements.</summary>
	internal int Depth { get; }

	/// <summary>Whether the root element has started.</summary>
	internal bool RootSeen { get; }

	/// <summary>Whether the root element has ended.</summary>
	internal bool RootClosed { get; }

	/// <summary>The number of refills before the state was saved, which tells a stale state.</summary>
	internal int Refills { get; }
}

/// <summary>
/// Reads the tokens of an XML part, forward only, from an <see cref="XmlWindow"/>: start and end tags, text and CDATA. It
/// skips comments, processing instructions and the XML declaration, and rejects a DOCTYPE before reading past it.
/// </summary>
/// <remarks>
/// <para>
/// Names are matched by local name: the prefix is dropped (<c>x:c</c> is <c>c</c>), and namespaces are not checked, so
/// the transitional and strict namespaces read alike. Attributes are matched the same way (<c>r:id</c> is <c>id</c>), and
/// namespace declarations are not attributes (<see cref="XmlAttributes"/>).
/// </para>
/// <para>
/// <see cref="TryRead"/> reads a whole token, or returns <see langword="false"/> at the start of one that the window
/// doesn't hold yet; <see cref="Refill"/> then keeps it and reads more. Each call consumes input or asks for more, so
/// it can't loop forever. Tokens point into the window, so they are valid until the next refill. A start tag's
/// <see cref="Attributes"/> are valid until the next start tag or refill: reading the tag, the scanner records where its
/// first values' quotes are, which the next start tag records over.
/// </para>
/// <para>
/// It is not a validating parser. It throws <see cref="InvalidDataException"/> when something stops it from reading: a
/// tag, comment, CDATA section or processing instruction the part ends inside, an end tag without a start tag, a part
/// that ends inside an element or has no root element, a second root element, a DOCTYPE, a <c>&lt;</c> in an attribute
/// value (which Excel refuses, as XML does), and (when they are decoded) undefined entities and invalid character
/// references. It doesn't check that end tags match their start tags, that names are valid, or that attributes are
/// unique.
/// </para>
/// </remarks>
internal sealed class XmlScanner : IDisposable, IAsyncDisposable
{
	private readonly XmlWindow window;
	private int position;
	private int depth;
	private bool rootSeen;
	private bool rootClosed;
	private int refills;
	private int tokenStart;
	private int nameStart;
	private int nameLength;
	// The content of a text or CDATA token, or the attributes of a start tag.
	private int contentStart;
	private int contentLength;
	// A raw skip in progress (TrySkipRaw), across refills: whether it has begun, and the markup it is inside, from where.
	private bool rawSkipping;
	private RawMarkup rawMarkup;
	private long rawMarkupOffset;
	// The quotes of the last start tag's first values, for its attributes (XmlAttributes): each value's opening and
	// closing quote, from after the tag's name. quoteCount is how many of them are the tag's: 0 once a read of a start tag
	// has begun, which may write over them, until that tag is read.
	private readonly int[] quotes = new int[2 * RecordedValues];
	private int quoteCount;
	// The start tags read and the refills: attributes made before the count moved would read another tag's quotes, or
	// bytes a refill moved.
	private int tags;

	/// <summary>Creates a scanner over a window, which it owns: disposing the scanner disposes the window.</summary>
	/// <param name="window">The window over the part.</param>
	/// <exception cref="ArgumentNullException"><paramref name="window"/> is <see langword="null"/>.</exception>
	public XmlScanner(XmlWindow window)
	{
		ArgumentNullException.ThrowIfNull(window);
		this.window = window;
	}

	/// <summary>The window the scanner reads.</summary>
	public XmlWindow Window => window;

	/// <summary>What the scanner has read.</summary>
	public XmlTokenKind Kind { get; private set; }

	/// <summary>Whether the start tag read is an empty element, <c>&lt;c/&gt;</c>, which has no end tag.</summary>
	public bool IsEmptyElement { get; private set; }

	/// <summary>
	/// The number of open elements after the token: a start tag counts its element (unless it is empty), and an end tag
	/// no longer counts its own. The root element is at depth 1.
	/// </summary>
	public int Depth => depth;

	/// <summary>
	/// The depth of the element a start or end tag belongs to: 1 for the root element, 2 for its children, empty or not.
	/// For other tokens, the depth of the element they are in (<see cref="Depth"/>).
	/// </summary>
	public int ElementDepth => Kind switch
	{
		XmlTokenKind.StartTag => IsEmptyElement ? depth + 1 : depth,
		XmlTokenKind.EndTag => depth + 1,
		_ => depth,
	};

	/// <summary>The position in the window of the next token.</summary>
	public int Position => position;

	/// <summary>The position in the window where the token read starts (its <c>&lt;</c>), as <see cref="Error"/> reports it.</summary>
	public int TokenStart => tokenStart;

	/// <summary>The local name of a start or end tag (<c>c</c> for <c>x:c</c>); empty for other tokens.</summary>
	public ReadOnlySpan<byte> LocalName =>
		Kind is XmlTokenKind.StartTag or XmlTokenKind.EndTag ? window.Buffer.AsSpan(nameStart, nameLength) : default;

	/// <summary>The name of a start or end tag with its prefix (<c>x:c</c> for <c>x:c</c>, <c>c</c> for <c>c</c>); empty for other tokens.</summary>
	public ReadOnlySpan<byte> QualifiedName
	{
		get
		{
			if (Kind is not (XmlTokenKind.StartTag or XmlTokenKind.EndTag))
			{
				return default;
			}

			// After the '<' of a start tag, or the '</' of an end tag.
			var start = tokenStart + (Kind == XmlTokenKind.EndTag ? 2 : 1);

			return window.Buffer.AsSpan(start, nameStart + nameLength - start);
		}
	}

	/// <summary>
	/// Whether the readers of the part skip elements whose content they don't need by a raw search (<see cref="TrySkipRaw"/>)
	/// rather than token by token (<see cref="TrySkipTo"/>): the merged cells' read through a sheet skips its
	/// <c>sheetData</c>, and a sheet's scanner the rows it skips whole. The scanner itself doesn't read it. On by default;
	/// tests turn it off, so that both paths are tested.
	/// </summary>
	public bool RawSkips { get; set; } = true;

	/// <summary>The content of a text or CDATA token, as it is in the XML; empty for other tokens.</summary>
	public ReadOnlySpan<byte> Content =>
		Kind is XmlTokenKind.Text or XmlTokenKind.CData ? window.Buffer.AsSpan(contentStart, contentLength) : default;

	/// <summary>
	/// The position in the window of <see cref="Content"/>, for a caller that decodes a value of several tokens once all of
	/// them are read: the bytes stay where they are until the next refill.
	/// </summary>
	public int ContentStart => Kind is XmlTokenKind.Text or XmlTokenKind.CData ? contentStart : position;

	/// <summary>The attributes of a start tag; none for other tokens. Valid until the scanner reads another start tag or refills.</summary>
	public XmlAttributes Attributes => Kind == XmlTokenKind.StartTag ? new XmlAttributes(this, contentStart, contentLength, tags) : default;

	/// <summary>The number of a start tag's values whose quotes the scanner records as it reads the tag, for its <see cref="Attributes"/>.</summary>
	internal const int RecordedValues = 16;

	/// <summary>
	/// The quotes of the first values of the last start tag read, as positions from after its name (where its
	/// <see cref="Attributes"/> start): the opening and the closing quote of each, in pairs. Only the first
	/// <see cref="QuoteCount"/> are the tag's.
	/// </summary>
	internal int[] Quotes => quotes;

	/// <summary>
	/// How many of <see cref="Quotes"/> are the last start tag's: two for each value recorded, of at most
	/// <see cref="RecordedValues"/>. 0 once a read of another start tag has begun, which may have written over them, and
	/// stopped before the tag was read: the window cut it short, or it can't be read.
	/// </summary>
	internal int QuoteCount => quoteCount;

	/// <summary>
	/// The number of start tags read (by any read, a raw skip's nested tags too) and of refills, which a start tag's
	/// attributes check as they are read (<see cref="XmlAttributes.MoveNext"/>): once it has moved, their tag's quotes, or
	/// its bytes, may be gone.
	/// </summary>
	internal int Tags => tags;

	/// <summary>Whether the token is a start tag with the local name <paramref name="localName"/>.</summary>
	/// <param name="localName">The local name, as UTF-8.</param>
	/// <returns>Whether the token is that start tag.</returns>
	public bool IsStartTag(ReadOnlySpan<byte> localName) => Kind == XmlTokenKind.StartTag && LocalName.SequenceEqual(localName);

	/// <summary>
	/// The <c>xml:space</c> of the start tag read: <see langword="true"/> for <c>preserve</c>, <see langword="false"/>
	/// for <c>default</c>, and <see langword="null"/> when it has none (or another value), so that the element's parent's
	/// applies, as XML says.
	/// </summary>
	/// <returns>Whether the element preserves white space, or <see langword="null"/> to inherit it.</returns>
	/// <exception cref="InvalidDataException">An attribute can't be read.</exception>
	public bool? SpaceOf()
	{
		var attributes = Attributes;
		while (attributes.MoveNext())
		{
			if (attributes.IsQualifiedName("xml:space"u8))
			{
				return SpaceValue(attributes.Value);
			}
		}

		return null;
	}

	/// <summary>What an <c>xml:space</c> value says.</summary>
	/// <param name="value">The value, as it is in the XML.</param>
	/// <returns><see langword="true"/> for <c>preserve</c>, <see langword="false"/> for <c>default</c>, and <see langword="null"/> for anything else.</returns>
	public static bool? SpaceValue(ReadOnlySpan<byte> value) =>
		value.SequenceEqual("preserve"u8) ? true : value.SequenceEqual("default"u8) ? false : null;

	/// <summary>Whether the token is an end tag with the local name <paramref name="localName"/>.</summary>
	/// <param name="localName">The local name, as UTF-8.</param>
	/// <returns>Whether the token is that end tag.</returns>
	public bool IsEndTag(ReadOnlySpan<byte> localName) => Kind == XmlTokenKind.EndTag && LocalName.SequenceEqual(localName);

	/// <summary>Reads the next token, if the window holds all of it.</summary>
	/// <returns>
	/// <see langword="true"/> when a token is read (<see cref="XmlTokenKind.End"/> at the end of the part);
	/// <see langword="false"/> when the window ends inside the next token, which <see cref="Refill"/> keeps.
	/// </returns>
	/// <exception cref="InvalidDataException">The XML can't be read.</exception>
	public bool TryRead()
	{
		var buffer = window.Buffer;
		var end = window.End;
		while (true)
		{
			var pos = position;
			tokenStart = pos;
			if (pos == end)
			{
				return TryReadEnd(pos);
			}

			if (buffer[pos] != (byte)'<')
			{
				var length = buffer.AsSpan(pos, end - pos).IndexOf((byte)'<');
				if (length < 0)
				{
					if (!window.IsEof)
					{
						return false;
					}

					length = end - pos;
				}

				SetContent(XmlTokenKind.Text, pos, length);
				position = pos + length;

				return true;
			}

			var markup = buffer.AsSpan(pos, end - pos);
			if (markup.Length < 2)
			{
				return NeedMore(pos, "a tag");
			}

			var second = markup[1];
			if (second == (byte)'/')
			{
				return TryReadEndTag(markup, pos);
			}

			if (second == (byte)'?')
			{
				var close = markup.Slice(2).IndexOf("?>"u8);
				if (close < 0)
				{
					return NeedMore(pos, "a processing instruction");
				}

				position = pos + 2 + close + 2;

				continue;
			}

			if (second != (byte)'!')
			{
				return TryReadStartTag(markup, pos);
			}

			if (markup.StartsWith(CommentStart))
			{
				var close = markup.Slice(CommentStart.Length).IndexOf(CommentEnd);
				if (close < 0)
				{
					return NeedMore(pos, "a comment");
				}

				position = pos + CommentStart.Length + close + CommentEnd.Length;

				continue;
			}

			if (markup.StartsWith(CDataStart))
			{
				var close = markup.Slice(CDataStart.Length).IndexOf(CDataEnd);
				if (close < 0)
				{
					return NeedMore(pos, "a CDATA section");
				}

				SetContent(XmlTokenKind.CData, pos + CDataStart.Length, close);
				position = pos + CDataStart.Length + close + CDataEnd.Length;

				return true;
			}

			// Before anything of it is read: a DTD could define entities that expand without bound, or name external files.
			if (markup.StartsWith(DoctypeStart))
			{
				throw window.Error(pos, "it has a DOCTYPE, which Office Open XML parts must not have");
			}

			if (IsStartOf(markup, CommentStart) || IsStartOf(markup, CDataStart) || IsStartOf(markup, DoctypeStart))
			{
				return NeedMore(pos, "markup");
			}

			throw window.Error(pos, "'<!' starts neither a comment nor a CDATA section");
		}
	}

	/// <summary>Reads the next token, refilling the window as needed.</summary>
	/// <returns><see langword="false"/> at the end of the part (<see cref="XmlTokenKind.End"/>), otherwise <see langword="true"/>.</returns>
	/// <exception cref="InvalidDataException">The XML can't be read, or a token is larger than the window can grow.</exception>
	public bool Read()
	{
		while (!TryRead())
		{
			Refill();
		}

		return Kind != XmlTokenKind.End;
	}

	/// <summary>Asynchronously reads the next token, refilling the window as needed.</summary>
	/// <param name="cancellationToken">Cancels reading the part's stream.</param>
	/// <returns>
	/// A task whose result is <see langword="false"/> at the end of the part (<see cref="XmlTokenKind.End"/>), otherwise
	/// <see langword="true"/>.
	/// </returns>
	/// <exception cref="InvalidDataException">The XML can't be read, or a token is larger than the window can grow.</exception>
	public async ValueTask<bool> ReadAsync(CancellationToken cancellationToken)
	{
		while (!TryRead())
		{
			await RefillAsync(cancellationToken).ConfigureAwait(false);
		}

		return Kind != XmlTokenKind.End;
	}

	/// <summary>
	/// Skips tokens until the depth is <paramref name="targetDepth"/>, if the window holds them: after a start tag at depth
	/// <c>d</c>, skipping to <c>d - 1</c> skips the rest of its element, however deep, with no recursion. Text is skipped
	/// as it comes, so skipped text needn't fit the window.
	/// </summary>
	/// <param name="targetDepth">The depth to stop at, after the end tag that reaches it.</param>
	/// <returns>
	/// <see langword="true"/> at the depth; <see langword="false"/> when the window runs out first, which
	/// <see cref="Refill"/> fixes: what is skipped already stays skipped.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="targetDepth"/> is negative.</exception>
	/// <exception cref="InvalidDataException">The XML can't be read.</exception>
	public bool TrySkipTo(int targetDepth)
	{
		if (targetDepth < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(targetDepth), targetDepth, "The depth can't be negative.");
		}

		while (depth > targetDepth)
		{
			var buffer = window.Buffer;
			var end = window.End;
			var pos = position;
			if (pos < end && buffer[pos] != (byte)'<')
			{
				var length = buffer.AsSpan(pos, end - pos).IndexOf((byte)'<');
				if (length < 0)
				{
					position = end;
					if (!window.IsEof)
					{
						return false;
					}

					continue;
				}

				position = pos + length;
			}

			if (!TryRead())
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Skips the text at the scanner's position, if it is at text, as it comes, so that the text needn't fit the window:
	/// for text the caller ignores, such as the white space between rows. CDATA, comments and other markup stay tokens for
	/// <see cref="TryRead"/>. Skipped text is neither decoded nor checked, as <see cref="TryRead"/> leaves a text token's
	/// content to whoever decodes it.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> when the scanner is past the text, at markup or at the end of what the part holds;
	/// <see langword="false"/> when the window ends inside the text, which <see cref="Refill"/> fixes: what is skipped
	/// already stays skipped.
	/// </returns>
	public bool TrySkipText()
	{
		var buffer = window.Buffer;
		var end = window.End;
		var pos = position;

		// At the end of what the window holds, what comes next is unknown until a refill, unless the part has ended.
		if (pos == end)
		{
			return window.IsEof;
		}

		if (buffer[pos] == (byte)'<')
		{
			return true;
		}

		var length = buffer.AsSpan(pos, end - pos).IndexOf((byte)'<');
		if (length < 0)
		{
			position = end;

			return window.IsEof;
		}

		position = pos + length;

		return true;
	}

	/// <summary>Skips tokens until the depth is <paramref name="targetDepth"/>, refilling the window as needed. See <see cref="TrySkipTo"/>.</summary>
	/// <param name="targetDepth">The depth to stop at.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="targetDepth"/> is negative.</exception>
	/// <exception cref="InvalidDataException">The XML can't be read, or a token is larger than the window can grow.</exception>
	public void SkipTo(int targetDepth)
	{
		while (!TrySkipTo(targetDepth))
		{
			Refill();
		}
	}

	/// <summary>Asynchronously skips tokens until the depth is <paramref name="targetDepth"/>. See <see cref="TrySkipTo"/>.</summary>
	/// <param name="targetDepth">The depth to stop at.</param>
	/// <param name="cancellationToken">Cancels reading the part's stream.</param>
	/// <returns>A task that completes at the depth.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="targetDepth"/> is negative.</exception>
	/// <exception cref="InvalidDataException">The XML can't be read, or a token is larger than the window can grow.</exception>
	public async ValueTask SkipToAsync(int targetDepth, CancellationToken cancellationToken)
	{
		while (!TrySkipTo(targetDepth))
		{
			await RefillAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Skips to after the end tag <c>&lt;/qualifiedName&gt;</c> that brings the depth to <paramref name="targetDepth"/>, if
	/// the window holds it, by a raw search for the name rather than token by token: the element of that name is the one
	/// open at <c>targetDepth + 1</c>, and the elements open inside it, wherever the scanner is in them, end with it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Each hit of the name is looked at: <c>&lt;/</c>, the name, and white space or <c>&gt;</c> is an end tag, which ends
	/// the skip or a nested element of the same name; <c>&lt;</c>, the name, and white space, <c>&gt;</c> or <c>/</c> is
	/// such a nested element's start tag, counted unless it is empty. Both are read as tokens, so they must fit the window.
	/// Anything else, such as a longer name or the name in text, is passed over.
	/// </para>
	/// <para>
	/// A search for <c>!</c> and <c>?</c> beside it finds comments, CDATA sections and processing instructions, which are
	/// skipped to their ends, so that the name in them is no hit; any other <c>&lt;!</c> is passed over. Nothing else in
	/// the skipped bytes is read or checked: not their tags, nor their attribute values, so a literal <c>&lt;/</c> and the
	/// name in a value, which isn't XML (<see cref="TryReadStartTag"/>), would end the skip there.
	/// </para>
	/// <para>
	/// When the window runs out, only the bytes from where a hit could still start are kept, a few more than the name's,
	/// so that <see cref="Refill"/> doesn't grow the window for the skipped bytes, however long their comments, values or
	/// text are.
	/// </para>
	/// </remarks>
	/// <param name="qualifiedName">
	/// The name of the element, with its prefix, as its start tag has it (<see cref="QualifiedName"/>): a copy, not the
	/// window's bytes, which a refill moves.
	/// </param>
	/// <param name="targetDepth">The depth to stop at, after the end tag that reaches it.</param>
	/// <returns>
	/// <see langword="true"/> at the depth, at once when the scanner is there already; <see langword="false"/> when the
	/// window runs out first, which <see cref="Refill"/> fixes: what is skipped already stays skipped.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="targetDepth"/> is negative.</exception>
	/// <exception cref="ArgumentException"><paramref name="qualifiedName"/> is empty.</exception>
	/// <exception cref="InvalidDataException">
	/// The part ends inside the element, a comment, a CDATA section or a processing instruction; or a tag of the name
	/// can't be read.
	/// </exception>
	public bool TrySkipRaw(ReadOnlySpan<byte> qualifiedName, int targetDepth)
	{
		if (targetDepth < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(targetDepth), targetDepth, "The depth can't be negative.");
		}

		if (qualifiedName.IsEmpty)
		{
			throw new ArgumentException("The name can't be empty.", nameof(qualifiedName));
		}

		if (depth <= targetDepth)
		{
			return true;
		}

		if (!rawSkipping)
		{
			// The elements open inside the one skipped end with it: only nested elements of its name are counted from here.
			depth = targetDepth + 1;
			rawSkipping = true;
		}

		var buffer = window.Buffer;
		var end = window.End;
		var pos = position;
		// The next hit of the name at or after pos: -1 to search for it, end when the window holds none.
		var nameAt = -1;
		// The bytes from pos to here hold no '!' or '?' that starts markup: each byte is searched once, and only up to the
		// next hit, so that a skip of a short row doesn't search the rest of the window.
		var marked = pos;
		while (true)
		{
			if (rawMarkup != RawMarkup.None)
			{
				var close = MarkupEnd(rawMarkup);
				var at = buffer.AsSpan(pos, end - pos).IndexOf(close);
				if (at < 0)
				{
					// All but the last bytes, which could start the end.
					position = Math.Max(pos, end - (close.Length - 1));

					return RawNeedMore();
				}

				pos += at + close.Length;
				rawMarkup = RawMarkup.None;
				marked = pos;
				if (nameAt < pos)
				{
					nameAt = -1;
				}

				continue;
			}

			if (nameAt < pos)
			{
				nameAt = IndexOf(buffer, pos, end, qualifiedName);
			}

			// Markup before the hit, which could hold it.
			var markAt = IndexOfMark(buffer, marked, nameAt);
			if (markAt < nameAt)
			{
				var lt = markAt - 1;
				if (lt < pos || buffer[lt] != (byte)'<')
				{
					// '!' or '?' in text, or in a tag passed over.
					marked = markAt + 1;

					continue;
				}

				var markup = buffer.AsSpan(lt, end - lt);
				if (markup[1] == (byte)'?')
				{
					EnterMarkup(RawMarkup.ProcessingInstruction, lt);
					pos = lt + 2;
				}
				else if (markup.StartsWith(CommentStart))
				{
					EnterMarkup(RawMarkup.Comment, lt);
					pos = lt + CommentStart.Length;
				}
				else if (markup.StartsWith(CDataStart))
				{
					EnterMarkup(RawMarkup.CData, lt);
					pos = lt + CDataStart.Length;
				}
				else
				{
					// A DOCTYPE, other markup, or the start of a comment or CDATA section that the window cuts short: passed
					// over. Such a start is in the bytes kept at the window's end, so it is looked at again after the refill.
					marked = markAt + 1;
				}

				continue;
			}

			marked = nameAt;
			if (nameAt == end)
			{
				// From where a hit could still start: the name with its "</" and the byte after it, or "<![CDATA[".
				position = Math.Max(pos, end - Math.Max(qualifiedName.Length + 2, CDataStart.Length - 1));

				return RawNeedMore();
			}

			var after = nameAt + qualifiedName.Length;
			if (after == end)
			{
				if (!window.IsEof)
				{
					// The byte after the name tells whether it is a tag of the name.
					position = Math.Max(pos, nameAt - 2);

					return false;
				}

				nameAt = end;

				continue;
			}

			var next = buffer[after];
			var tag = nameAt - 2 >= pos && buffer[nameAt - 2] == (byte)'<' && buffer[nameAt - 1] == (byte)'/' && (IsWhitespace(next) || next == (byte)'>')
				? nameAt - 2
				: nameAt - 1 >= pos && buffer[nameAt - 1] == (byte)'<' && IsNameEnd(next)
					? nameAt - 1
					: -1;
			if (tag < 0)
			{
				nameAt = IndexOf(buffer, nameAt + 1, end, qualifiedName);

				continue;
			}

			// The tag, read as a token: an end tag lowers the depth, a nested start tag that isn't empty raises it.
			tokenStart = tag;
			var read = buffer[tag + 1] == (byte)'/'
				? TryReadEndTag(buffer.AsSpan(tag, end - tag), tag)
				: TryReadStartTag(buffer.AsSpan(tag, end - tag), tag);
			if (!read)
			{
				position = tag;

				return false;
			}

			if (depth == targetDepth)
			{
				rawSkipping = false;

				return true;
			}

			pos = position;
			marked = pos;
		}
	}

	/// <summary>Skips to the end tag of <paramref name="qualifiedName"/> that reaches <paramref name="targetDepth"/>, refilling the window as needed. See <see cref="TrySkipRaw"/>.</summary>
	/// <param name="qualifiedName">The name of the element, with its prefix.</param>
	/// <param name="targetDepth">The depth to stop at.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="targetDepth"/> is negative.</exception>
	/// <exception cref="ArgumentException"><paramref name="qualifiedName"/> is empty.</exception>
	/// <exception cref="InvalidDataException">The part ends inside the element or its markup, or a tag of the name can't be read.</exception>
	public void SkipRaw(ReadOnlySpan<byte> qualifiedName, int targetDepth)
	{
		while (!TrySkipRaw(qualifiedName, targetDepth))
		{
			Refill();
		}
	}

	/// <summary>Asynchronously skips to the end tag of <paramref name="qualifiedName"/> that reaches <paramref name="targetDepth"/>. See <see cref="TrySkipRaw"/>.</summary>
	/// <param name="qualifiedName">The name of the element, with its prefix.</param>
	/// <param name="targetDepth">The depth to stop at.</param>
	/// <param name="cancellationToken">Cancels reading the part's stream.</param>
	/// <returns>A task that completes at the depth.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="targetDepth"/> is negative.</exception>
	/// <exception cref="ArgumentException"><paramref name="qualifiedName"/> is empty.</exception>
	/// <exception cref="InvalidDataException">The part ends inside the element or its markup, or a tag of the name can't be read.</exception>
	public async ValueTask SkipRawAsync(ReadOnlyMemory<byte> qualifiedName, int targetDepth, CancellationToken cancellationToken)
	{
		while (!TrySkipRaw(qualifiedName.Span, targetDepth))
		{
			await RefillAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Reads more of the part, keeping the bytes from <see cref="Position"/> on: the token that <see cref="TryRead"/> or
	/// <see cref="TrySkipTo"/> found incomplete, or where <see cref="Restore"/> went back to. The current token and saved
	/// states are gone.
	/// </summary>
	/// <exception cref="InvalidDataException">The bytes kept fill a window of the size limit.</exception>
	/// <exception cref="InvalidOperationException">The part has ended.</exception>
	public void Refill()
	{
		Compact();
		window.Read();
	}

	/// <summary>Asynchronously reads more of the part. See <see cref="Refill"/>.</summary>
	/// <param name="cancellationToken">Cancels reading the part's stream; the scanner can then refill again.</param>
	/// <returns>A task that completes when more of the part is read.</returns>
	/// <exception cref="InvalidDataException">The bytes kept fill a window of the size limit.</exception>
	/// <exception cref="InvalidOperationException">The part has ended.</exception>
	public ValueTask RefillAsync(CancellationToken cancellationToken)
	{
		Compact();

		return window.ReadAsync(cancellationToken);
	}

	/// <summary>Saves where the scanner is, to read the same tokens again (<see cref="Restore"/>) until the next refill.</summary>
	/// <returns>The state.</returns>
	public XmlScannerState Save() => new(position, depth, rootSeen, rootClosed, refills);

	/// <summary>Goes back to where the scanner was when it saved <paramref name="state"/>.</summary>
	/// <param name="state">The state from <see cref="Save"/>.</param>
	/// <exception cref="InvalidOperationException">The scanner has refilled since it saved the state, which moved its bytes.</exception>
	public void Restore(XmlScannerState state)
	{
		if (state.Refills != refills)
		{
			throw new InvalidOperationException("The scanner state was saved before a refill, which moved the bytes it points at.");
		}

		position = state.Position;
		depth = state.Depth;
		rootSeen = state.RootSeen;
		rootClosed = state.RootClosed;
		Kind = XmlTokenKind.None;
	}

	/// <summary>
	/// Moves the scanner to <paramref name="position"/> in the window, past bytes the caller has read itself, such as a
	/// sheet's cell read straight from the window. No token is left (<see cref="Kind"/> is <see cref="XmlTokenKind.None"/>),
	/// and <see cref="TokenStart"/> is the position, where an error now is. The depth, the root element's state and the
	/// refills are as they were, so the bytes passed over must leave them so: whole elements, or text.
	/// </summary>
	/// <remarks>
	/// For a hot path: the position is checked only in debug builds. No start tag is read, so the last one's
	/// <see cref="Attributes"/> stay valid.
	/// </remarks>
	/// <param name="position">The position: from <see cref="Position"/> to the end of the window's bytes.</param>
	internal void SetPosition(int position)
	{
		Debug.Assert(position >= this.position && position <= window.End, "The position must be from the scanner's to the end of the window's bytes.");
		this.position = position;
		tokenStart = position;
		Kind = XmlTokenKind.None;
	}

	/// <summary>
	/// Decodes the content of a text or CDATA token into UTF-8 (<see cref="XmlText.DecodeText"/>,
	/// <see cref="XmlText.DecodeCData"/>). The <c>_xHHHH_</c> escapes are left for the caller, over the joined text of a value.
	/// </summary>
	/// <param name="destination">Where the text goes: as long as <see cref="Content"/> is always enough.</param>
	/// <returns>The number of bytes written; 0 for other tokens.</returns>
	/// <exception cref="InvalidDataException">The text has an undefined entity or an invalid character reference.</exception>
	public int DecodeContent(Span<byte> destination)
	{
		if (Kind == XmlTokenKind.CData)
		{
			return XmlText.DecodeCData(Content, destination);
		}

		var written = XmlText.DecodeText(Content, destination);
		if (written < 0)
		{
			throw ReferenceError(contentStart + ~written, contentStart + contentLength);
		}

		return written;
	}

	/// <summary>Creates the exception for XML that can't be read at the current token, with the part's name and the byte offset.</summary>
	/// <param name="detail">What is wrong, as a clause, such as "the workbook has no sheets".</param>
	/// <returns>The exception, to throw.</returns>
	public InvalidDataException Error(string detail) => window.Error(tokenStart, detail);

	/// <summary>Creates the exception for a reference that can't be decoded.</summary>
	/// <param name="position">The position in the window of the reference's <c>&amp;</c>.</param>
	/// <param name="end">The end of the text the reference is in.</param>
	/// <returns>The exception, to throw.</returns>
	internal InvalidDataException ReferenceError(int position, int end)
	{
		var rest = window.Buffer.AsSpan(position, Math.Min(end - position, 16));
		var semicolon = rest.IndexOf((byte)';');
		var reference = Encoding.UTF8.GetString(semicolon < 0 ? rest : rest.Slice(0, semicolon + 1));

		return window.Error(position, $"'{reference}' is neither a predefined entity nor a valid character reference");
	}

	/// <summary>Disposes the window, and so the part's stream.</summary>
	public void Dispose() => window.Dispose();

	/// <summary>Asynchronously disposes the window, and so the part's stream.</summary>
	/// <returns>A task that completes when the stream is disposed.</returns>
	public ValueTask DisposeAsync() => window.DisposeAsync();

	/// <summary>Whether a byte is XML white space: space, tab, LF or CR.</summary>
	/// <param name="b">The byte.</param>
	/// <returns>Whether it is white space.</returns>
	internal static bool IsWhitespace(byte b) => b is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r';

	/// <summary>What is wrong with a <c>&lt;</c> in an attribute value, for <see cref="TryReadStartTag"/> and <see cref="XmlAttributes.MoveNext"/>.</summary>
	internal const string LessThanInValue = "an attribute value has '<', which XML doesn't allow";

	/// <summary><c>&lt;!--</c>.</summary>
	private static ReadOnlySpan<byte> CommentStart => "<!--"u8;

	/// <summary><c>--&gt;</c>.</summary>
	private static ReadOnlySpan<byte> CommentEnd => "-->"u8;

	/// <summary><c>&lt;![CDATA[</c>.</summary>
	private static ReadOnlySpan<byte> CDataStart => "<![CDATA["u8;

	/// <summary><c>]]&gt;</c>.</summary>
	private static ReadOnlySpan<byte> CDataEnd => "]]>"u8;

	/// <summary><c>&lt;!DOCTYPE</c>.</summary>
	private static ReadOnlySpan<byte> DoctypeStart => "<!DOCTYPE"u8;

	/// <summary>Whether a name ends at a byte: white space, or the <c>/</c> or <c>&gt;</c> that ends a tag.</summary>
	/// <param name="b">The byte.</param>
	/// <returns>Whether the name ends there.</returns>
	private static bool IsNameEnd(byte b) => IsWhitespace(b) || b == (byte)'/' || b == (byte)'>';

	/// <summary>Whether <paramref name="markup"/>, which ends with the window, could still become <paramref name="literal"/>.</summary>
	/// <param name="markup">The bytes from the <c>&lt;</c> to the end of the window.</param>
	/// <param name="literal">The markup it could be.</param>
	/// <returns>Whether <paramref name="markup"/> is shorter than <paramref name="literal"/> and starts it.</returns>
	private static bool IsStartOf(ReadOnlySpan<byte> markup, ReadOnlySpan<byte> literal) =>
		markup.Length < literal.Length && literal.StartsWith(markup);

	/// <summary>Moves the bytes from <see cref="Position"/> to the start of the window, which makes them position 0.</summary>
	private void Compact()
	{
		window.Compact(position);
		position = 0;
		// The token is gone; an error now is at the next one.
		tokenStart = 0;
		refills++;
		tags++;
		Kind = XmlTokenKind.None;
	}

	/// <summary>Reads the end of the part, if the stream has ended.</summary>
	/// <param name="pos">The position: the end of the window.</param>
	/// <returns>Whether the part has ended.</returns>
	/// <exception cref="InvalidDataException">The part ends inside an element, or has no root element.</exception>
	private bool TryReadEnd(int pos)
	{
		if (!window.IsEof)
		{
			return false;
		}

		if (depth > 0)
		{
			throw window.Error(pos, "it ends inside an element");
		}

		if (!rootSeen)
		{
			throw window.Error(pos, "it has no root element");
		}

		Kind = XmlTokenKind.End;

		return true;
	}

	/// <summary>
	/// Reads a start tag, from its <c>&lt;</c> on, and records the quotes of its first <see cref="RecordedValues"/> values
	/// as it finds them, so that its attributes needn't search for them again (<see cref="XmlAttributes.MoveNext"/>).
	/// </summary>
	/// <param name="markup">The bytes from the <c>&lt;</c> to the end of the window.</param>
	/// <param name="pos">The position of the <c>&lt;</c>.</param>
	/// <returns>Whether the window holds the whole tag.</returns>
	/// <exception cref="InvalidDataException">The tag can't be read.</exception>
	private bool TryReadStartTag(ReadOnlySpan<byte> markup, int pos)
	{
		var i = 1;
		while (i < markup.Length && !IsNameEnd(markup[i]))
		{
			i++;
		}

		if (i == markup.Length)
		{
			return NeedMore(pos, "a tag");
		}

		if (i == 1)
		{
			throw window.Error(pos, "'<' starts no tag");
		}

		var nameEnd = i;
		// The first values' quotes, for the attributes (XmlAttributes), recorded over the last tag's: none are the last
		// tag's from here, and a tag the window cuts short, or one that can't be read, leaves none.
		var quotes = this.quotes;
		var recorded = 0;
		quoteCount = 0;
		// The '>' that ends the tag, past attribute values, which can hold '>'.
		while (true)
		{
			var next = markup.Slice(i).IndexOfAny((byte)'>', (byte)'"', (byte)'\'');
			if (next < 0)
			{
				return NeedMore(pos, "a tag");
			}

			i += next;
			var quote = markup[i];
			if (quote == (byte)'>')
			{
				break;
			}

			// A '<' in a value isn't XML, and a raw skip (TrySkipRaw) could take one there for a tag.
			var close = markup.Slice(i + 1).IndexOfAny(quote, (byte)'<');
			if (close < 0)
			{
				return NeedMore(pos, "a tag");
			}

			if (markup[i + 1 + close] == (byte)'<')
			{
				throw window.Error(pos + i + 1 + close, LessThanInValue);
			}

			if ((uint)recorded < (uint)quotes.Length)
			{
				quotes[recorded] = i - nameEnd;
				quotes[recorded + 1] = i + 1 + close - nameEnd;
				recorded += 2;
			}

			i += close + 2;
		}

		var empty = markup[i - 1] == (byte)'/';
		if (depth == 0)
		{
			if (rootClosed)
			{
				throw window.Error(pos, "it has a second root element");
			}

			rootSeen = true;
			rootClosed = empty;
		}

		if (!empty)
		{
			if (depth == int.MaxValue)
			{
				throw window.Error(pos, "its elements nest too deeply");
			}

			depth++;
		}

		SetName(pos + 1, pos + nameEnd);
		contentStart = pos + nameEnd;
		contentLength = (empty ? i - 1 : i) - nameEnd;
		quoteCount = recorded;
		tags++;
		Kind = XmlTokenKind.StartTag;
		IsEmptyElement = empty;
		position = pos + i + 1;

		return true;
	}

	/// <summary>Reads an end tag, from its <c>&lt;/</c> on.</summary>
	/// <param name="markup">The bytes from the <c>&lt;</c> to the end of the window.</param>
	/// <param name="pos">The position of the <c>&lt;</c>.</param>
	/// <returns>Whether the window holds the whole tag.</returns>
	/// <exception cref="InvalidDataException">The tag can't be read, or no element is open.</exception>
	private bool TryReadEndTag(ReadOnlySpan<byte> markup, int pos)
	{
		var close = markup.Slice(2).IndexOf((byte)'>');
		if (close < 0)
		{
			return NeedMore(pos, "a tag");
		}

		var name = markup.Slice(2, close);
		var length = 0;
		while (length < name.Length && !IsWhitespace(name[length]))
		{
			length++;
		}

		if (length == 0)
		{
			throw window.Error(pos, "an end tag has no name");
		}

		foreach (var b in name.Slice(length))
		{
			if (!IsWhitespace(b))
			{
				throw window.Error(pos, "an end tag has more than a name");
			}
		}

		if (depth == 0)
		{
			throw window.Error(pos, "an end tag has no start tag");
		}

		depth--;
		if (depth == 0)
		{
			rootClosed = true;
		}

		SetName(pos + 2, pos + 2 + length);
		Kind = XmlTokenKind.EndTag;
		IsEmptyElement = false;
		position = pos + 2 + close + 1;

		return true;
	}

	/// <summary>Returns <see langword="false"/> for a token the window ends inside, or throws if the part ends there.</summary>
	/// <param name="pos">The position of the token.</param>
	/// <param name="what">What the token is, such as "a tag", for the error.</param>
	/// <returns><see langword="false"/>.</returns>
	/// <exception cref="InvalidDataException">The part has ended.</exception>
	private bool NeedMore(int pos, string what)
	{
		if (window.IsEof)
		{
			throw window.Error(pos, $"it ends inside {what}");
		}

		return false;
	}

	/// <summary>Sets the name of a tag: its local name, after the prefix.</summary>
	/// <param name="start">The position of the qualified name.</param>
	/// <param name="end">The end of the qualified name.</param>
	private void SetName(int start, int end)
	{
		var colon = window.Buffer.AsSpan(start, end - start).IndexOf((byte)':');
		nameStart = colon < 0 ? start : start + colon + 1;
		nameLength = end - nameStart;
	}

	/// <summary>Sets a text or CDATA token.</summary>
	/// <param name="kind">The kind of token.</param>
	/// <param name="start">The position of its content.</param>
	/// <param name="length">The length of its content.</param>
	private void SetContent(XmlTokenKind kind, int start, int length)
	{
		Kind = kind;
		IsEmptyElement = false;
		contentStart = start;
		contentLength = length;
	}

	/// <summary>The position of <paramref name="value"/> in the window from <paramref name="from"/>, for a raw skip.</summary>
	/// <param name="buffer">The window's bytes.</param>
	/// <param name="from">Where to search from.</param>
	/// <param name="end">The end of the window's bytes.</param>
	/// <param name="value">The bytes to find.</param>
	/// <returns>The position, or <paramref name="end"/> when the window doesn't hold them.</returns>
	private static int IndexOf(byte[] buffer, int from, int end, ReadOnlySpan<byte> value)
	{
		var at = buffer.AsSpan(from, end - from).IndexOf(value);

		return at < 0 ? end : from + at;
	}

	/// <summary>The position of the next <c>!</c> or <c>?</c> in the window from <paramref name="from"/>, which can start markup, for a raw skip.</summary>
	/// <param name="buffer">The window's bytes.</param>
	/// <param name="from">Where to search from.</param>
	/// <param name="end">The end of the window's bytes.</param>
	/// <returns>The position, or <paramref name="end"/> when the window holds neither.</returns>
	private static int IndexOfMark(byte[] buffer, int from, int end)
	{
		var at = buffer.AsSpan(from, end - from).IndexOfAny((byte)'!', (byte)'?');

		return at < 0 ? end : from + at;
	}

	/// <summary>What ends markup that a raw skip is inside.</summary>
	/// <param name="markup">The markup.</param>
	/// <returns><c>--&gt;</c>, <c>]]&gt;</c> or <c>?&gt;</c>.</returns>
	private static ReadOnlySpan<byte> MarkupEnd(RawMarkup markup) => markup switch
	{
		RawMarkup.Comment => CommentEnd,
		RawMarkup.CData => CDataEnd,
		_ => "?>"u8,
	};

	/// <summary>Begins markup in a raw skip, which is skipped to its end, across refills.</summary>
	/// <param name="markup">The markup.</param>
	/// <param name="start">The position of its <c>&lt;</c>, for the error when the part ends inside it.</param>
	private void EnterMarkup(RawMarkup markup, int start)
	{
		rawMarkup = markup;
		rawMarkupOffset = window.Offset(start);
	}

	/// <summary>Returns <see langword="false"/> when a raw skip needs more of the part, or throws if the part has ended.</summary>
	/// <returns><see langword="false"/>.</returns>
	/// <exception cref="InvalidDataException">The part has ended: inside the element skipped, or inside markup in it.</exception>
	private bool RawNeedMore()
	{
		if (!window.IsEof)
		{
			return false;
		}

		var what = rawMarkup switch
		{
			RawMarkup.Comment => "a comment",
			RawMarkup.CData => "a CDATA section",
			RawMarkup.ProcessingInstruction => "a processing instruction",
			_ => null,
		};
		if (what is not null)
		{
			throw XmlError.Create(window.PartName, rawMarkupOffset, $"it ends inside {what}");
		}

		throw window.Error(window.End, "it ends inside an element");
	}

	/// <summary>The markup a raw skip is inside (<see cref="TrySkipRaw"/>), which it skips to its end.</summary>
	private enum RawMarkup : byte
	{
		/// <summary>None: the skip is searching for the name.</summary>
		None,

		/// <summary>A comment, to <c>--&gt;</c>.</summary>
		Comment,

		/// <summary>A CDATA section, to <c>]]&gt;</c>.</summary>
		CData,

		/// <summary>A processing instruction, to <c>?&gt;</c>.</summary>
		ProcessingInstruction,
	}
}
