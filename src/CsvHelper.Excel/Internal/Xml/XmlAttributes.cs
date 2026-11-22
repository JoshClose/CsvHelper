using System;
using System.Buffers.Text;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// The attributes of a start tag that <see cref="XmlScanner"/> has read, one at a time: <c>while (attributes.MoveNext())</c>.
/// Namespace declarations (<c>xmlns</c>, <c>xmlns:x</c>) are skipped, and names are local names (<c>id</c> for
/// <c>r:id</c>). It is valid until the scanner's next start tag or refill: <see cref="MoveNext"/> then throws.
/// </summary>
/// <remarks>
/// <para>
/// The scanner found where the tag ends, past quoted values; the attributes themselves are read only here, so malformed
/// attributes of a tag that is never enumerated go unnoticed.
/// </para>
/// <para>
/// The scanner also recorded where the first values' quotes are (<see cref="XmlScanner.RecordedValues"/> of them), so that
/// for those only the bytes before each value are read here: white space, the name, white space, <c>=</c>, white space.
/// Past them, and from an attribute whose bytes before its recorded value are not that (in a malformed tag, whose quotes
/// the scanner paired otherwise than a name, <c>=</c> and a value would), the attributes are read from the bytes alone,
/// each value's closing quote searched for. That is also how every attribute was read before the quotes were recorded,
/// and both ways read the same attributes, and find the same errors at the same offsets, for any tag: the bytes before
/// the value are read alike, and a value read from the bytes ends at the quote the scanner found for it.
/// </para>
/// </remarks>
internal struct XmlAttributes
{
	/// <summary>The room on the stack for a value with references to decode.</summary>
	private const int ScratchSize = 64;

	/// <summary>The most digits of a value <see cref="GetInt32"/> parses where it is: any 9 digits fit an <see cref="int"/>.</summary>
	private const int MaxPlainDigits = 9;

	private readonly XmlScanner? scanner;
	// The attributes of the tag in the window: from after its name to its '>' or '/>'.
	private readonly int start;
	private readonly int length;
	// Where the next attribute starts, from start.
	private int offset;
	private int qualifiedStart;
	private int qualifiedLength;
	private int nameStart;
	private int nameLength;
	private int valueStart;
	private int valueLength;
	// The scanner's count of start tags and refills when the attributes were made (XmlScanner.Tags).
	private readonly int tags;
	// The index in the scanner's quotes of the next value's opening quote; int.MaxValue once the attributes are read from
	// the bytes alone.
	private int quoteIndex;

	/// <summary>Creates the attributes of the start tag a scanner has read.</summary>
	/// <param name="scanner">The scanner.</param>
	/// <param name="start">The position in the window of the attributes.</param>
	/// <param name="length">The length of the attributes.</param>
	/// <param name="tags">The scanner's count of start tags and refills (<see cref="XmlScanner.Tags"/>), after the tag's.</param>
	internal XmlAttributes(XmlScanner scanner, int start, int length, int tags)
	{
		this.scanner = scanner;
		this.start = start;
		this.length = length;
		this.tags = tags;
		offset = qualifiedStart = qualifiedLength = nameStart = nameLength = valueStart = valueLength = quoteIndex = 0;
	}

	/// <summary>The local name of the attribute.</summary>
	public readonly ReadOnlySpan<byte> LocalName => scanner is null ? default : scanner.Window.Buffer.AsSpan(nameStart, nameLength);

	/// <summary>The value of the attribute, between its quotes, as it is in the XML (see <see cref="DecodeValue()"/>).</summary>
	public readonly ReadOnlySpan<byte> Value => scanner is null ? default : scanner.Window.Buffer.AsSpan(valueStart, valueLength);

	/// <summary>Moves to the next attribute.</summary>
	/// <returns>Whether there is one.</returns>
	/// <exception cref="InvalidDataException">
	/// The attribute can't be read: it has no name, no <c>=</c>, or no quotes (at its name), or its value has a <c>&lt;</c>
	/// (at the <c>&lt;</c>).
	/// </exception>
	/// <exception cref="InvalidOperationException">The scanner has read another start tag, or refilled, since the attributes were made.</exception>
	public bool MoveNext()
	{
		if (scanner is null)
		{
			return false;
		}

		if (scanner.Tags != tags)
		{
			throw new InvalidOperationException("The attributes of a start tag were read after the scanner read another start tag or refilled: they are gone.");
		}

		var span = scanner.Window.Buffer.AsSpan(start, length);
		while (true)
		{
			var name = 0;
			var nameEnd = 0;
			var found = false;
			if (quoteIndex < scanner.QuoteCount)
			{
				var quotes = scanner.Quotes;
				var open = quotes[quoteIndex];
				// From the end of the value before (or of the tag's name) to this value's opening quote, which the scanner
				// found: white space, the name, white space, '=' and white space, with no search for the closing quote.
				var before = span.Slice(offset, open - offset);
				var at = PastWhitespace(before, 0);
				name = at;
				while (at < before.Length && before[at] != (byte)'=' && !XmlScanner.IsWhitespace(before[at]))
				{
					at++;
				}

				nameEnd = at;
				at = PastWhitespace(before, at);
				if (nameEnd > name && at < before.Length && before[at] == (byte)'=' && PastWhitespace(before, at + 1) == before.Length)
				{
					var close = quotes[quoteIndex + 1];
					name += offset;
					nameEnd += offset;
					valueStart = start + open + 1;
					valueLength = close - open - 1;
					offset = close + 1;
					quoteIndex += 2;
					found = true;
				}
				else
				{
					// A malformed tag, whose quotes the scanner paired otherwise: from here, read from the bytes alone.
					quoteIndex = int.MaxValue;
				}
			}

			if (!found && !TryReadFromBytes(span, out name, out nameEnd))
			{
				return false;
			}

			var qualified = span.Slice(name, nameEnd - name);
			if (qualified.SequenceEqual("xmlns"u8) || qualified.StartsWith("xmlns:"u8))
			{
				continue;
			}

			var colon = qualified.IndexOf((byte)':');
			qualifiedStart = start + name;
			qualifiedLength = nameEnd - name;
			nameStart = start + name + colon + 1;
			nameLength = nameEnd - name - colon - 1;

			return true;
		}
	}

	/// <summary>Whether the attribute's name, with its prefix, is <paramref name="qualifiedName"/>, such as <c>xml:space</c>.</summary>
	/// <param name="qualifiedName">The name with its prefix, as UTF-8.</param>
	/// <returns>Whether the names are the same.</returns>
	public readonly bool IsQualifiedName(ReadOnlySpan<byte> qualifiedName) =>
		scanner is not null && scanner.Window.Buffer.AsSpan(qualifiedStart, qualifiedLength).SequenceEqual(qualifiedName);

	/// <summary>Whether the attribute's local name is <paramref name="localName"/>.</summary>
	/// <param name="localName">The local name, as UTF-8.</param>
	/// <returns>Whether the names are the same.</returns>
	public readonly bool IsName(ReadOnlySpan<byte> localName) => LocalName.SequenceEqual(localName);

	/// <summary>Decodes the value into UTF-8 (<see cref="XmlText.DecodeAttribute"/>).</summary>
	/// <param name="destination">Where the value goes: as long as <see cref="Value"/> is always enough.</param>
	/// <returns>The number of bytes written.</returns>
	/// <exception cref="InvalidDataException">The value has an undefined entity or an invalid character reference.</exception>
	public readonly int DecodeValue(Span<byte> destination)
	{
		var written = XmlText.DecodeAttribute(Value, destination);
		if (written < 0)
		{
			throw scanner!.ReferenceError(valueStart + ~written, valueStart + valueLength);
		}

		return written;
	}

	/// <summary>Decodes the value into a string (<see cref="XmlText.DecodeAttribute"/>).</summary>
	/// <returns>The value.</returns>
	/// <exception cref="InvalidDataException">The value has an undefined entity or an invalid character reference.</exception>
	public readonly string DecodeValue()
	{
		var value = Value;
		if (value.IsEmpty)
		{
			return "";
		}

		var decoded = new byte[value.Length];
		var written = DecodeValue(decoded);

		return Encoding.UTF8.GetString(decoded, 0, written);
	}

	/// <summary>
	/// Reads the value as an XML Schema boolean: <c>1</c>, <c>0</c>, <c>true</c> or <c>false</c>, with white space around it
	/// allowed. Excel writes <c>1</c> and <c>0</c>, LibreOffice <c>true</c> and <c>false</c>.
	/// </summary>
	/// <returns>The boolean.</returns>
	/// <exception cref="InvalidDataException">The value is not a boolean.</exception>
	public readonly bool GetBoolean()
	{
		Span<byte> scratch = stackalloc byte[ScratchSize];
		var value = Trimmed(scratch);
		if (value.SequenceEqual("1"u8) || value.SequenceEqual("true"u8))
		{
			return true;
		}

		if (value.SequenceEqual("0"u8) || value.SequenceEqual("false"u8))
		{
			return false;
		}

		throw InvalidValue("a boolean");
	}

	/// <summary>Reads the value as a 32-bit integer, with white space around it allowed.</summary>
	/// <remarks>
	/// A value of XML white space around 1 to 9 ASCII digits, as Excel writes a style, a row or a count, is parsed where it
	/// is; any other (a sign, a reference, more digits, none) is decoded and parsed by <see cref="Utf8Parser"/>, which gives
	/// the plain ones the same numbers.
	/// </remarks>
	/// <returns>The integer.</returns>
	/// <exception cref="InvalidDataException">The value is not an integer, or doesn't fit 32 bits.</exception>
	public readonly int GetInt32()
	{
		var value = Value;
		var at = 0;
		while (at < value.Length && XmlScanner.IsWhitespace(value[at]))
		{
			at++;
		}

		var first = at;
		var result = 0;
		while (at < value.Length && at - first < MaxPlainDigits && (uint)(value[at] - '0') <= 9)
		{
			result = (result * 10) + (value[at] - '0');
			at++;
		}

		var digits = at - first;
		while (at < value.Length && XmlScanner.IsWhitespace(value[at]))
		{
			at++;
		}

		if (digits > 0 && at == value.Length)
		{
			return result;
		}

		return ParseInt32();
	}

	/// <summary>Reads the value as a 32-bit integer, decoded and trimmed, with <see cref="Utf8Parser"/>: <see cref="GetInt32"/> for any value but plain digits.</summary>
	/// <returns>The integer.</returns>
	/// <exception cref="InvalidDataException">The value is not an integer, or doesn't fit 32 bits.</exception>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private readonly int ParseInt32()
	{
		Span<byte> scratch = stackalloc byte[ScratchSize];
		var value = Trimmed(scratch);
		if (!Utf8Parser.TryParse(value, out int result, out var consumed) || consumed != value.Length)
		{
			throw InvalidValue("an integer");
		}

		return result;
	}

	/// <summary>The value, decoded when it has references, without the white space around it.</summary>
	/// <param name="scratch">Room for the decoded value; a longer one gets its own array.</param>
	/// <returns>The value.</returns>
	private readonly ReadOnlySpan<byte> Trimmed(Span<byte> scratch)
	{
		ReadOnlySpan<byte> value = Value;
		if (value.IndexOf((byte)'&') >= 0)
		{
			var destination = value.Length <= scratch.Length ? scratch : new byte[value.Length];
			value = destination.Slice(0, DecodeValue(destination));
		}

		var start = 0;
		var end = value.Length;
		while (start < end && XmlScanner.IsWhitespace(value[start]))
		{
			start++;
		}

		while (end > start && XmlScanner.IsWhitespace(value[end - 1]))
		{
			end--;
		}

		return value.Slice(start, end - start);
	}

	/// <summary>Creates the exception for a value that is not what it should be, with the part's name and the byte offset.</summary>
	/// <param name="what">What it should be, such as "a boolean".</param>
	/// <returns>The exception, to throw.</returns>
	public readonly InvalidDataException InvalidValue(string what)
	{
		var name = Encoding.UTF8.GetString(LocalName);
		var value = Encoding.UTF8.GetString(Value);

		return scanner!.Window.Error(valueStart, $"the attribute {name}=\"{value}\" is not {what}");
	}

	/// <summary>
	/// Reads the next attribute's name and value from the bytes alone, from <see cref="offset"/>: white space, the name (to
	/// <c>=</c> or white space), white space, <c>=</c>, white space, and a value in quotes, whose closing quote it searches
	/// for. Sets the value, and <see cref="offset"/> past it.
	/// </summary>
	/// <param name="span">The attributes.</param>
	/// <param name="name">The name's start, from the attributes' start.</param>
	/// <param name="nameEnd">The name's end, from the attributes' start.</param>
	/// <returns>Whether there is an attribute; <see langword="false"/> at the end of the attributes.</returns>
	/// <exception cref="InvalidDataException">
	/// The attribute can't be read: it has no name, no <c>=</c>, or no quotes (at its name), or its value has a <c>&lt;</c>
	/// (at the <c>&lt;</c>).
	/// </exception>
	private bool TryReadFromBytes(ReadOnlySpan<byte> span, out int name, out int nameEnd)
	{
		SkipWhitespace(span);
		name = offset;
		nameEnd = offset;
		if (offset == span.Length)
		{
			return false;
		}

		while (offset < span.Length && span[offset] != (byte)'=' && !XmlScanner.IsWhitespace(span[offset]))
		{
			offset++;
		}

		nameEnd = offset;
		SkipWhitespace(span);
		if (nameEnd == name || offset == span.Length || span[offset] != (byte)'=')
		{
			throw scanner!.Window.Error(start + name, "an attribute has no name, or no value");
		}

		offset++;
		SkipWhitespace(span);
		var quote = offset < span.Length ? span[offset] : (byte)0;
		// The tokenizer found no '<' between the quotes it paired, but a malformed tag can pair them differently here.
		var close = quote is (byte)'"' or (byte)'\'' ? span.Slice(offset + 1).IndexOfAny(quote, (byte)'<') : -1;
		if (close < 0)
		{
			throw scanner!.Window.Error(start + name, "an attribute value has no quotes");
		}

		if (span[offset + 1 + close] == (byte)'<')
		{
			throw scanner!.Window.Error(start + offset + 1 + close, XmlScanner.LessThanInValue);
		}

		valueStart = start + offset + 1;
		valueLength = close;
		offset += close + 2;

		return true;
	}

	/// <summary>Moves <see cref="offset"/> past white space.</summary>
	/// <param name="span">The attributes.</param>
	private void SkipWhitespace(ReadOnlySpan<byte> span)
	{
		while (offset < span.Length && XmlScanner.IsWhitespace(span[offset]))
		{
			offset++;
		}
	}

	/// <summary>Where the white space at a position of some bytes ends.</summary>
	/// <param name="bytes">The bytes.</param>
	/// <param name="at">The position.</param>
	/// <returns>The position of the first byte at or after <paramref name="at"/> that isn't white space, or the bytes' length.</returns>
	private static int PastWhitespace(ReadOnlySpan<byte> bytes, int at)
	{
		while (at < bytes.Length && XmlScanner.IsWhitespace(bytes[at]))
		{
			at++;
		}

		return at;
	}
}
