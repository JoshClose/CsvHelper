using System;
using System.Buffers.Text;
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
	/// <summary>The room on the stack for a value with references to decode.</summary>
	private const int ScratchSize = 64;

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

	/// <summary>Creates the attributes of the start tag a scanner has read.</summary>
	/// <param name="scanner">The scanner.</param>
	/// <param name="start">The position in the window of the attributes.</param>
	/// <param name="length">The length of the attributes.</param>
	internal XmlAttributes(XmlScanner scanner, int start, int length)
	{
		this.scanner = scanner;
		this.start = start;
		this.length = length;
		offset = qualifiedStart = qualifiedLength = nameStart = nameLength = valueStart = valueLength = 0;
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
	/// <returns>The integer.</returns>
	/// <exception cref="InvalidDataException">The value is not an integer, or doesn't fit 32 bits.</exception>
	public readonly int GetInt32()
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
