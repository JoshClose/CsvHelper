using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>What <see cref="XmlScanner"/> has read.</summary>
internal enum XmlTokenKind : byte
{
	/// <summary>No token: before the first read, and after a refill or a restore, which leave nothing to point at.</summary>
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
/// it can't loop forever. Tokens point into the window, so they are valid until the next refill.
/// </para>
/// <para>
/// It is not a validating parser. It throws <see cref="InvalidDataException"/> when something stops it from reading: a
/// tag, comment, CDATA section or processing instruction the part ends inside, an end tag without a start tag, a part
/// that ends inside an element or has no root element, a second root element, a DOCTYPE, and (when they are decoded)
/// undefined entities and invalid character references. It doesn't check that end tags match their start tags, that
/// names are valid, or that attributes are unique.
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

	/// <summary>The content of a text or CDATA token, as it is in the XML; empty for other tokens.</summary>
	public ReadOnlySpan<byte> Content =>
		Kind is XmlTokenKind.Text or XmlTokenKind.CData ? window.Buffer.AsSpan(contentStart, contentLength) : default;

	/// <summary>
	/// The position in the window of <see cref="Content"/>, for a caller that decodes a value of several tokens once all of
	/// them are read: the bytes stay where they are until the next refill.
	/// </summary>
	public int ContentStart => Kind is XmlTokenKind.Text or XmlTokenKind.CData ? contentStart : position;

	/// <summary>The attributes of a start tag; none for other tokens.</summary>
	public XmlAttributes Attributes => Kind == XmlTokenKind.StartTag ? new XmlAttributes(this, contentStart, contentLength) : default;

	/// <summary>Whether the token is a start tag with the local name <paramref name="localName"/>.</summary>
	/// <param name="localName">The local name, as UTF-8.</param>
	/// <returns>Whether the token is that start tag.</returns>
	public bool IsStartTag(ReadOnlySpan<byte> localName) => Kind == XmlTokenKind.StartTag && LocalName.SequenceEqual(localName);

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

	/// <summary>Reads a start tag, from its <c>&lt;</c> on.</summary>
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

			var close = markup.Slice(i + 1).IndexOf(quote);
			if (close < 0)
			{
				return NeedMore(pos, "a tag");
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
}
