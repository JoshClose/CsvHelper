using System;

namespace CsvHelper.Excel;

/// <summary>
/// Decodes the text of XML parts into UTF-8: character data, CDATA sections and attribute values (the XML layer), and
/// then the <c>_xHHHH_</c> escapes of Office Open XML strings (ST_Xstring), over the joined text of a value.
/// </summary>
/// <remarks>
/// <para>
/// Decoded text is never longer than its XML: every reference and escape is longer than the UTF-8 it stands for, and
/// line ends only shrink. So a destination as long as the source always has room, and <see cref="DecodeEscapes"/> works in
/// place.
/// </para>
/// <para>
/// The XML decoders return the number of bytes written, or, when a reference can't be decoded, the bitwise complement
/// (<c>~index</c>, a negative number) of the index in the source where it starts, so that the caller can say where it is.
/// </para>
/// </remarks>
internal static class XmlText
{
	/// <summary>The length of an escape, <c>_xHHHH_</c>.</summary>
	private const int EscapeLength = 7;

	/// <summary>U+FFFD, for a surrogate that is not part of a pair.</summary>
	private const int ReplacementCharacter = 0xFFFD;

	/// <summary>The length of the longest predefined entity reference, <c>&amp;quot;</c>.</summary>
	private const int MaxEntityLength = 6;

	/// <summary>What starts something to decode in character data: a reference, or a CR to normalize.</summary>
	private static ReadOnlySpan<byte> TextSpecials => "&\r"u8;

	/// <summary>What starts something to decode in an attribute value: a reference, or white space to normalize.</summary>
	private static ReadOnlySpan<byte> AttributeSpecials => "&\r\n\t"u8;

	/// <summary>
	/// Decodes the character data of a text token: the predefined entities and character references, and CR LF and lone CR
	/// as LF, as XML's end-of-line handling does. A CR from a character reference (<c>&amp;#13;</c>) stays a CR.
	/// </summary>
	/// <param name="source">The text as it is in the XML.</param>
	/// <param name="destination">Where the decoded text goes; as long as <paramref name="source"/> is always enough.</param>
	/// <returns>The number of bytes written, or <c>~index</c> of a reference that can't be decoded.</returns>
	public static int DecodeText(ReadOnlySpan<byte> source, Span<byte> destination) => Decode(source, destination, false);

	/// <summary>
	/// Decodes an attribute value: as <see cref="DecodeText"/>, and then each literal tab, LF and CR as a space (CR LF as one),
	/// as XML's attribute-value normalization does for attributes that are not declared otherwise. Characters from references
	/// stay as they are.
	/// </summary>
	/// <param name="source">The value between its quotes, as it is in the XML.</param>
	/// <param name="destination">Where the decoded value goes; as long as <paramref name="source"/> is always enough.</param>
	/// <returns>The number of bytes written, or <c>~index</c> of a reference that can't be decoded.</returns>
	public static int DecodeAttribute(ReadOnlySpan<byte> source, Span<byte> destination) => Decode(source, destination, true);

	/// <summary>
	/// Decodes the content of a CDATA section: only its line ends, CR LF and lone CR as LF. Everything else, references
	/// included, is literal.
	/// </summary>
	/// <param name="source">The content between <c>&lt;![CDATA[</c> and <c>]]&gt;</c>.</param>
	/// <param name="destination">Where the decoded text goes; as long as <paramref name="source"/> is always enough.</param>
	/// <returns>The number of bytes written.</returns>
	public static int DecodeCData(ReadOnlySpan<byte> source, Span<byte> destination)
	{
		var written = 0;
		while (true)
		{
			var next = source.IndexOf((byte)'\r');
			if (next < 0)
			{
				source.CopyTo(destination.Slice(written));

				return written + source.Length;
			}

			source.Slice(0, next).CopyTo(destination.Slice(written));
			written += next;
			destination[written++] = (byte)'\n';
			source = source.Slice(next + 1);
			if (source.Length > 0 && source[0] == (byte)'\n')
			{
				source = source.Slice(1);
			}
		}
	}

	/// <summary>
	/// Decodes the <c>_xHHHH_</c> escapes of an Office Open XML string in place, after its XML is decoded: <c>_x</c>, four
	/// hexadecimal digits of either case, and <c>_</c> stand for that UTF-16 code unit, so <c>_x005F_</c> is a literal
	/// <c>_</c>. Two escapes that form a surrogate pair stand for one character; a surrogate that is not part of a pair
	/// becomes U+FFFD. Anything else that starts with <c>_</c> is literal.
	/// </summary>
	/// <param name="text">The decoded text of a value, which this rewrites.</param>
	/// <returns>The length of the text after decoding.</returns>
	public static int DecodeEscapes(Span<byte> text)
	{
		var read = text.IndexOf((byte)'_');
		if (read < 0)
		{
			return text.Length;
		}

		var written = read;
		while (read < text.Length)
		{
			if (!TryParseEscape(text.Slice(read), out var unit))
			{
				// A literal '_', and what follows up to the next one.
				var next = text.Slice(read + 1).IndexOf((byte)'_');
				var length = next < 0 ? text.Length - read : next + 1;
				text.Slice(read, length).CopyTo(text.Slice(written));
				read += length;
				written += length;

				continue;
			}

			read += EscapeLength;
			var codePoint = (int)unit;
			if (char.IsHighSurrogate(unit))
			{
				if (TryParseEscape(text.Slice(read), out var low) && char.IsLowSurrogate(low))
				{
					codePoint = char.ConvertToUtf32(unit, low);
					read += EscapeLength;
				}
				else
				{
					codePoint = ReplacementCharacter;
				}
			}
			else if (char.IsLowSurrogate(unit))
			{
				codePoint = ReplacementCharacter;
			}

			// The escape was read before it is written over, and its UTF-8 is shorter than it.
			written += WriteUtf8(codePoint, text.Slice(written));
		}

		return written;
	}

	/// <summary>
	/// Trims the XML white space (space, tab, LF, CR) around a decoded text in place: what is left moves to the start of
	/// <paramref name="text"/>. Excel trims a text this way when <c>xml:space="preserve"</c> doesn't apply to it, after its
	/// references are decoded and before its <c>_xHHHH_</c> escapes are (so <c>_x0020_</c> stays).
	/// </summary>
	/// <param name="text">The text, from its start.</param>
	/// <returns>The length of the trimmed text.</returns>
	public static int TrimWhitespace(Span<byte> text)
	{
		var start = 0;
		var end = text.Length;
		while (start < end && XmlScanner.IsWhitespace(text[start]))
		{
			start++;
		}

		while (end > start && XmlScanner.IsWhitespace(text[end - 1]))
		{
			end--;
		}

		if (start > 0)
		{
			text.Slice(start, end - start).CopyTo(text);
		}

		return end - start;
	}

	/// <summary>Whether a code point is a character XML allows (the Char production of XML 1.0).</summary>
	/// <param name="codePoint">The code point.</param>
	/// <returns><see langword="true"/> for tab, LF, CR and U+0020 to U+10FFFF without the surrogates, U+FFFE and U+FFFF.</returns>
	public static bool IsXmlChar(int codePoint) =>
		codePoint is 0x9 or 0xA or 0xD or >= 0x20 and <= 0xD7FF or >= 0xE000 and <= 0xFFFD or >= 0x10000 and <= 0x10FFFF;

	/// <summary>Decodes references, and either line ends (text) or white space (attribute values).</summary>
	/// <param name="source">The XML text.</param>
	/// <param name="destination">Where the decoded text goes.</param>
	/// <param name="attribute">Whether <paramref name="source"/> is an attribute value.</param>
	/// <returns>The number of bytes written, or <c>~index</c> of a reference that can't be decoded.</returns>
	private static int Decode(ReadOnlySpan<byte> source, Span<byte> destination, bool attribute)
	{
		var specials = attribute ? AttributeSpecials : TextSpecials;
		var written = 0;
		var read = 0;
		while (true)
		{
			var rest = source.Slice(read);
			var next = rest.IndexOfAny(specials);
			if (next < 0)
			{
				rest.CopyTo(destination.Slice(written));

				return written + rest.Length;
			}

			rest.Slice(0, next).CopyTo(destination.Slice(written));
			written += next;
			read += next;
			var b = source[read];
			if (b == (byte)'&')
			{
				var length = DecodeReference(source.Slice(read), destination.Slice(written), out var bytes);
				if (length == 0)
				{
					return ~read;
				}

				read += length;
				written += bytes;
			}
			else
			{
				destination[written++] = attribute ? (byte)' ' : (byte)'\n';
				read++;
				if (b == (byte)'\r' && read < source.Length && source[read] == (byte)'\n')
				{
					read++;
				}
			}
		}
	}

	/// <summary>Decodes one reference: a predefined entity, or a decimal or hexadecimal character reference.</summary>
	/// <param name="source">The text from the reference's <c>&amp;</c> on.</param>
	/// <param name="destination">Where its character goes, as UTF-8.</param>
	/// <param name="written">The number of bytes written.</param>
	/// <returns>The length of the reference, with its <c>;</c>, or 0 when it can't be decoded.</returns>
	private static int DecodeReference(ReadOnlySpan<byte> source, Span<byte> destination, out int written)
	{
		written = 0;
		if (source.Length > 1 && source[1] == (byte)'#')
		{
			var hex = source.Length > 2 && source[2] == (byte)'x';
			var i = hex ? 3 : 2;
			var digits = i;
			var value = 0;
			while (i < source.Length)
			{
				var digit = hex ? HexValue(source[i]) : DecimalValue(source[i]);
				if (digit < 0)
				{
					break;
				}

				// Past U+10FFFF it is invalid however it goes on; stop growing so that it can't overflow.
				if (value <= 0x10FFFF)
				{
					value = value * (hex ? 16 : 10) + digit;
				}

				i++;
			}

			if (i == digits || i == source.Length || source[i] != (byte)';' || !IsXmlChar(value))
			{
				return 0;
			}

			written = WriteUtf8(value, destination);

			return i + 1;
		}

		var semicolon = source.Slice(0, Math.Min(source.Length, MaxEntityLength)).IndexOf((byte)';');
		if (semicolon < 0)
		{
			return 0;
		}

		var name = source.Slice(1, semicolon - 1);
		byte character;
		if (name.SequenceEqual("lt"u8))
		{
			character = (byte)'<';
		}
		else if (name.SequenceEqual("gt"u8))
		{
			character = (byte)'>';
		}
		else if (name.SequenceEqual("amp"u8))
		{
			character = (byte)'&';
		}
		else if (name.SequenceEqual("quot"u8))
		{
			character = (byte)'"';
		}
		else if (name.SequenceEqual("apos"u8))
		{
			character = (byte)'\'';
		}
		else
		{
			return 0;
		}

		destination[0] = character;
		written = 1;

		return semicolon + 1;
	}

	/// <summary>Parses an escape, <c>_xHHHH_</c>, at the start of <paramref name="text"/>.</summary>
	/// <param name="text">The text.</param>
	/// <param name="unit">The UTF-16 code unit of the escape.</param>
	/// <returns>Whether <paramref name="text"/> starts with an escape.</returns>
	private static bool TryParseEscape(ReadOnlySpan<byte> text, out char unit)
	{
		unit = default;
		if (text.Length < EscapeLength || text[0] != (byte)'_' || text[1] != (byte)'x' || text[6] != (byte)'_')
		{
			return false;
		}

		var value = 0;
		for (var i = 2; i < 6; i++)
		{
			var digit = HexValue(text[i]);
			if (digit < 0)
			{
				return false;
			}

			value = value * 16 + digit;
		}

		unit = (char)value;

		return true;
	}

	/// <summary>The value of a hexadecimal digit of either case.</summary>
	/// <param name="b">The byte.</param>
	/// <returns>0 to 15, or -1 when <paramref name="b"/> is not a hexadecimal digit.</returns>
	private static int HexValue(byte b) => b switch
	{
		>= (byte)'0' and <= (byte)'9' => b - '0',
		>= (byte)'a' and <= (byte)'f' => b - 'a' + 10,
		>= (byte)'A' and <= (byte)'F' => b - 'A' + 10,
		_ => -1,
	};

	/// <summary>The value of a decimal digit.</summary>
	/// <param name="b">The byte.</param>
	/// <returns>0 to 9, or -1 when <paramref name="b"/> is not a decimal digit.</returns>
	private static int DecimalValue(byte b) => b is >= (byte)'0' and <= (byte)'9' ? b - '0' : -1;

	/// <summary>Writes a code point as UTF-8.</summary>
	/// <param name="codePoint">The code point: not a surrogate.</param>
	/// <param name="destination">Where it goes: at least as many bytes as its UTF-8 takes.</param>
	/// <returns>The number of bytes written, 1 to 4.</returns>
	private static int WriteUtf8(int codePoint, Span<byte> destination)
	{
		if (codePoint < 0x80)
		{
			destination[0] = (byte)codePoint;

			return 1;
		}

		if (codePoint < 0x800)
		{
			destination[1] = (byte)(0x80 | (codePoint & 0x3F));
			destination[0] = (byte)(0xC0 | (codePoint >> 6));

			return 2;
		}

		if (codePoint < 0x10000)
		{
			destination[2] = (byte)(0x80 | (codePoint & 0x3F));
			destination[1] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
			destination[0] = (byte)(0xE0 | (codePoint >> 12));

			return 3;
		}

		destination[3] = (byte)(0x80 | (codePoint & 0x3F));
		destination[2] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
		destination[1] = (byte)(0x80 | ((codePoint >> 12) & 0x3F));
		destination[0] = (byte)(0xF0 | (codePoint >> 18));

		return 4;
	}
}
