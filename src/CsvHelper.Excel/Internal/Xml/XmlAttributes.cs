using System;
using System.IO;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// The attributes of a start tag that <see cref="XmlScanner"/> has read, one at a time: <c>while (attributes.MoveNext())</c>.
/// Namespace declarations (<c>xmlns</c>, <c>xmlns:x</c>) are skipped, and names are local names (<c>id</c> for
/// <c>r:id</c>). Like the token, it is valid until the scanner's next refill.
/// </summary>
/// <remarks>
/// The scanner found where the tag ends, past quoted values; the attributes themselves are read only here, so malformed
/// attributes of a tag that is never enumerated go unnoticed.
/// </remarks>
internal struct XmlAttributes
{
	private readonly XmlScanner? scanner;
	// The attributes of the tag in the window: from after its name to its '>' or '/>'.
	private readonly int start;
	private readonly int length;
	// Where the next attribute starts, from start.
	private int offset;
	private int nameStart;
	private int nameLength;
	private int valueStart;
	private int valueLength;

	/// <summary>Creates the attributes of the start tag a scanner has read.</summary>
	/// <param name="scanner">The scanner.</param>
	/// <param name="start">The position in the window of the attributes.</param>
	/// <param name="length">The length of the attributes.</param>
	internal XmlAttributes(XmlScanner scanner, int start, int length)
	{
		this.scanner = scanner;
		this.start = start;
		this.length = length;
		offset = nameStart = nameLength = valueStart = valueLength = 0;
	}

	/// <summary>The local name of the attribute.</summary>
	public readonly ReadOnlySpan<byte> LocalName => scanner is null ? default : scanner.Window.Buffer.AsSpan(nameStart, nameLength);

	/// <summary>The value of the attribute, between its quotes, as it is in the XML (see <see cref="DecodeValue()"/>).</summary>
	public readonly ReadOnlySpan<byte> Value => scanner is null ? default : scanner.Window.Buffer.AsSpan(valueStart, valueLength);

	/// <summary>Moves to the next attribute.</summary>
	/// <returns>Whether there is one.</returns>
	/// <exception cref="InvalidDataException">The attribute can't be read: it has no name, no <c>=</c>, or no quotes.</exception>
	public bool MoveNext()
	{
		if (scanner is null)
		{
			return false;
		}

		var span = scanner.Window.Buffer.AsSpan(start, length);
		while (true)
		{
			SkipWhitespace(span);
			if (offset == span.Length)
			{
				return false;
			}

			var name = offset;
			while (offset < span.Length && span[offset] != (byte)'=' && !XmlScanner.IsWhitespace(span[offset]))
			{
				offset++;
			}

			var nameEnd = offset;
			SkipWhitespace(span);
			if (nameEnd == name || offset == span.Length || span[offset] != (byte)'=')
			{
				throw scanner.Window.Error(start + name, "an attribute has no name, or no value");
			}

			offset++;
			SkipWhitespace(span);
			var quote = offset < span.Length ? span[offset] : (byte)0;
			var close = quote is (byte)'"' or (byte)'\'' ? span.Slice(offset + 1).IndexOf(quote) : -1;
			if (close < 0)
			{
				throw scanner.Window.Error(start + name, "an attribute value has no quotes");
			}

			valueStart = start + offset + 1;
			valueLength = close;
			offset += close + 2;
			var qualified = span.Slice(name, nameEnd - name);
			if (qualified.SequenceEqual("xmlns"u8) || qualified.StartsWith("xmlns:"u8))
			{
				continue;
			}

			var colon = qualified.IndexOf((byte)':');
			nameStart = start + name + colon + 1;
			nameLength = nameEnd - name - colon - 1;

			return true;
		}
	}

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

	/// <summary>Moves <see cref="offset"/> past white space.</summary>
	/// <param name="span">The attributes.</param>
	private void SkipWhitespace(ReadOnlySpan<byte> span)
	{
		while (offset < span.Length && XmlScanner.IsWhitespace(span[offset]))
		{
			offset++;
		}
	}
}
