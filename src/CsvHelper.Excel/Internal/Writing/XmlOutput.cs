using System;
using System.Buffers;
using System.Buffers.Text;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CsvHelper.Excel;

/// <summary>
/// A growable buffer of UTF-8 XML, which a part is written into before it goes to its zip entry: markup, numbers, cell
/// references, and text escaped as Excel writes it (<see cref="AppendText"/>). Its array comes from the shared pool, so it
/// must be disposed.
/// </summary>
internal sealed class XmlOutput : IDisposable
{
	/// <summary>The most UTF-16 code units a cell's text has: Excel opens no workbook with a longer one, inline or shared.</summary>
	public const int MaxCellLength = 32767;

#if NET
	/// <summary>
	/// The bytes <see cref="AppendText"/> stops at: the entities' <c>&amp;</c>, <c>&lt;</c> and <c>&gt;</c>, <c>_</c> (which
	/// may start text that reads as an escape), the control bytes but tab and LF, and <c>EF</c>, the lead byte of U+FFFE
	/// and U+FFFF.
	/// </summary>
	private static readonly SearchValues<byte> Specials = SearchValues.Create(SpecialBytes());
#else
	/// <summary>Whether each byte is one that <see cref="AppendText"/> stops at (see <see cref="SpecialBytes"/>).</summary>
	private static readonly bool[] IsSpecial = SpecialTable();
#endif

	private byte[] buffer;
	private int length;

	/// <summary>Creates a buffer.</summary>
	/// <param name="capacity">The bytes it holds before it grows.</param>
	public XmlOutput(int capacity) => buffer = ArrayPool<byte>.Shared.Rent(capacity);

	/// <summary>The number of bytes written.</summary>
	public int Length => length;

	/// <summary>The bytes written.</summary>
	public ReadOnlySpan<byte> Written => buffer.AsSpan(0, length);

	/// <summary>Writes the first bytes written to a stream, synchronously or not.</summary>
	/// <param name="stream">The stream.</param>
	/// <param name="count">The number of bytes, at most <see cref="Length"/>.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="cancellationToken">Cancels an asynchronous write.</param>
	/// <returns>A task that completes when the bytes are written.</returns>
	public async ValueTask WriteToAsync(Stream stream, int count, bool async, CancellationToken cancellationToken)
	{
		if (async)
		{
			await stream.WriteAsync(buffer, 0, count, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			stream.Write(buffer, 0, count);
		}
	}

	/// <summary>Drops the bytes written from a position on.</summary>
	/// <param name="position">The new length, at most <see cref="Length"/>.</param>
	public void Truncate(int position) => length = position;

	/// <summary>Drops the first bytes written, moving the rest to the start.</summary>
	/// <param name="count">The number of bytes, at most <see cref="Length"/>.</param>
	public void RemoveFirst(int count)
	{
		buffer.AsSpan(count, length - count).CopyTo(buffer);
		length -= count;
	}

	/// <summary>Appends bytes as they are.</summary>
	/// <param name="bytes">The bytes: markup, or text that needs no escaping.</param>
	public void Append(ReadOnlySpan<byte> bytes)
	{
		Reserve(bytes.Length);
		bytes.CopyTo(buffer.AsSpan(length));
		length += bytes.Length;
	}

	/// <summary>Appends one byte.</summary>
	/// <param name="value">The byte.</param>
	public void Append(byte value)
	{
		Reserve(1);
		buffer[length++] = value;
	}

	/// <summary>Appends a number's invariant digits.</summary>
	/// <param name="value">The number.</param>
	public void AppendNumber(long value)
	{
		Reserve(20);
		Utf8Formatter.TryFormat(value, buffer.AsSpan(length), out var written);
		length += written;
	}

	/// <summary>Appends a double as the shortest invariant text that reads back as it, as .NET writes it by default.</summary>
	/// <param name="value">The double, which is finite.</param>
	public void AppendDouble(double value)
	{
		Reserve(FloatFormatter.MaxDoubleLength);
		FloatFormatter.TryFormat(value, buffer.AsSpan(length), out var written);
		length += written;
	}

	/// <summary>Appends a cell's reference, such as <c>C14</c>.</summary>
	/// <param name="column">The column's letters, as UTF-8.</param>
	/// <param name="row">The 1-based row.</param>
	public void AppendReference(ReadOnlySpan<byte> column, long row)
	{
		Append(column);
		AppendNumber(row);
	}

	/// <summary>
	/// Appends text as Excel writes it in a <c>t</c> element or an attribute value: <c>&amp;</c>, <c>&lt;</c> and
	/// <c>&gt;</c> as entities, <c>"</c> as <c>&amp;quot;</c> when <paramref name="attribute"/> is set; CR, the other control
	/// characters but tab and LF, U+FFFE and U+FFFF as <c>_xHHHH_</c> escapes; and <c>_x005F_</c> (an escaped <c>_</c>) for
	/// an <c>_</c> that starts text that reads as an escape, so that it reads back as written: <c>_x0041_</c>, and
	/// <c>_x0041</c> before a character written as an escape, whose <c>_</c> would close it. Tab and LF are written as
	/// they are.
	/// </summary>
	/// <remarks>
	/// A literal CR would read back as LF, as XML's line ends do, in Excel and LibreOffice; a reference to a control
	/// character (<c>&amp;#1;</c>) makes a file Excel can't open; and Excel decodes <c>_xHHHH_</c>, with hexadecimal digits
	/// of either case, in text and in sheet names alike (checked on 2026-11-06).
	/// </remarks>
	/// <param name="text">The text, which must be valid UTF-8 (<see cref="IsValidUtf8"/>).</param>
	/// <param name="attribute">Whether the text is an attribute value, quoted with <c>"</c>.</param>
	public void AppendText(ReadOnlySpan<byte> text, bool attribute = false)
	{
		while (true)
		{
			var i = IndexOfSpecial(text, attribute);
			if (i < 0)
			{
				Append(text);

				return;
			}

			Append(text.Slice(0, i));
			var b = text[i];
			var consumed = 1;
			switch (b)
			{
				case (byte)'&':
					Append("&amp;"u8);
					break;
				case (byte)'<':
					Append("&lt;"u8);
					break;
				case (byte)'>':
					Append("&gt;"u8);
					break;
				case (byte)'"':
					Append("&quot;"u8);
					break;
				case (byte)'_':
					// The '_' itself becomes the escape of a '_', and what follows it stays literal.
					Append(ReadsAsEscape(text.Slice(i)) ? "_x005F_"u8 : "_"u8);
					break;
				case 0xEF:
					if (i + 2 < text.Length && text[i + 1] == 0xBF && text[i + 2] is 0xBE or 0xBF)
					{
						Append(text[i + 2] == 0xBE ? "_xFFFE_"u8 : "_xFFFF_"u8);
						consumed = 3;
					}
					else
					{
						Append(b);
					}

					break;
				default:
					AppendEscape(b);
					break;
			}

			text = text.Slice(i + consumed);
		}
	}

	/// <summary>Appends a control character's <c>_xHHHH_</c> escape, such as <c>_x000D_</c>.</summary>
	/// <param name="control">The character, U+0000 to U+001F.</param>
	public void AppendEscape(byte control)
	{
		Reserve(7);
		"_x00"u8.CopyTo(buffer.AsSpan(length));
		buffer[length + 4] = HexDigit(control >> 4);
		buffer[length + 5] = HexDigit(control & 0xF);
		buffer[length + 6] = (byte)'_';
		length += 7;
	}

	/// <summary>Returns the array to the pool.</summary>
	public void Dispose()
	{
		var array = buffer;
		buffer = [];
		length = 0;
		if (array.Length > 0)
		{
			ArrayPool<byte>.Shared.Return(array);
		}
	}

	/// <summary>
	/// Whether text is valid UTF-8: the field of <see cref="CsvWriter.WriteField(ReadOnlySpan{byte})"/> is not checked by
	/// the writer, and a sink can't tell it from other text, so every text field is checked.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <returns>Whether it is well-formed UTF-8, without surrogates.</returns>
	public static bool IsValidUtf8(ReadOnlySpan<byte> text)
	{
#if NET
		return System.Text.Unicode.Utf8.IsValid(text);
#else
		var i = 0;
		while (i < text.Length)
		{
			var b = text[i];
			if (b < 0x80)
			{
				i++;

				continue;
			}

			// The well-formed sequences of the Unicode Standard's table 3-7.
			int size;
			byte low = 0x80;
			byte high = 0xBF;
			if (b is >= 0xC2 and <= 0xDF)
			{
				size = 2;
			}
			else if (b is >= 0xE0 and <= 0xEF)
			{
				size = 3;
				low = b == 0xE0 ? (byte)0xA0 : (byte)0x80;
				high = b == 0xED ? (byte)0x9F : (byte)0xBF;
			}
			else if (b is >= 0xF0 and <= 0xF4)
			{
				size = 4;
				low = b == 0xF0 ? (byte)0x90 : (byte)0x80;
				high = b == 0xF4 ? (byte)0x8F : (byte)0xBF;
			}
			else
			{
				return false;
			}

			if (i + size > text.Length || text[i + 1] < low || text[i + 1] > high)
			{
				return false;
			}

			for (var j = 2; j < size; j++)
			{
				if ((text[i + j] & 0xC0) != 0x80)
				{
					return false;
				}
			}

			i += size;
		}

		return true;
#endif
	}

	/// <summary>The number of UTF-16 code units of valid UTF-8 text, which is how Excel counts a cell's length.</summary>
	/// <param name="text">The text, which must be valid UTF-8 (<see cref="IsValidUtf8"/>).</param>
	/// <returns>The number of code units: one for each character, two for one past U+FFFF.</returns>
	public static int Utf16Length(ReadOnlySpan<byte> text)
	{
#if NET
		return Encoding.UTF8.GetCharCount(text);
#else
		// A character starts at each byte that isn't a continuation byte, and one of four bytes takes two code units.
		var units = 0;
		foreach (var b in text)
		{
			if ((b & 0xC0) != 0x80)
			{
				units += b >= 0xF0 ? 2 : 1;
			}
		}

		return units;
#endif
	}

	/// <summary>
	/// The number of UTF-16 code units of valid UTF-8 text as <see cref="AppendText"/> writes it, its entities read: each
	/// <c>_xHHHH_</c> escape takes 7 for the one character it stands for. The schema's limits count them (a sheet's name
	/// has at most 31), while Excel's count the characters.
	/// </summary>
	/// <param name="text">The text, which must be valid UTF-8 (<see cref="IsValidUtf8"/>).</param>
	/// <returns>The number of code units.</returns>
	public static int WrittenLength(ReadOnlySpan<byte> text)
	{
		var units = Utf16Length(text);
		for (var i = 0; i < text.Length; i++)
		{
			if ((text[i] == (byte)'_' && ReadsAsEscape(text.Slice(i))) || IsWrittenAsEscape(text.Slice(i)))
			{
				units += 6;
			}
		}

		return units;
	}

	/// <summary>Invalid UTF-8 made valid, as .NET decodes it: each invalid sequence becomes U+FFFD.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The valid text.</returns>
	public static byte[] ReplaceInvalidUtf8(ReadOnlySpan<byte> text) => Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(text.ToArray()));

	/// <summary>
	/// Whether text starts with what Excel reads as an escape: <c>_x</c>, four hexadecimal digits of either case, and
	/// <c>_</c>. <c>_X0041_</c> (a capital <c>X</c>) and <c>_x0041</c> (no closing <c>_</c>) are literal.
	/// </summary>
	/// <param name="text">The text, from an <c>_</c>.</param>
	/// <returns>Whether it starts with an escape's form.</returns>
	public static bool IsEscapeLike(ReadOnlySpan<byte> text) => text.Length >= 7 && HasEscapeStart(text) && text[6] == (byte)'_';

	/// <summary>
	/// Whether text, from an <c>_</c>, reads as an escape once <see cref="AppendText"/> writes it: it starts with an
	/// escape's form (<see cref="IsEscapeLike"/>), or with <c>_x</c> and four hexadecimal digits before a character that
	/// is written as an escape, whose own <c>_</c> would close them. <c>_x0041</c> before U+0000 would be
	/// <c>_x0041_x0000_</c>, which reads as <c>A</c> and <c>x0000_</c>.
	/// </summary>
	/// <param name="text">The text, from an <c>_</c>.</param>
	/// <returns>Whether its <c>_</c> must be written as <c>_x005F_</c>.</returns>
	private static bool ReadsAsEscape(ReadOnlySpan<byte> text) =>
		text.Length >= 7 && HasEscapeStart(text) && (text[6] == (byte)'_' || IsWrittenAsEscape(text.Slice(6)));

	/// <summary>Whether text starts with <c>_x</c> and four hexadecimal digits, of either case.</summary>
	/// <param name="text">The text, at least 6 bytes long.</param>
	/// <returns>Whether it does.</returns>
	private static bool HasEscapeStart(ReadOnlySpan<byte> text)
	{
		if (text[0] != (byte)'_' || text[1] != (byte)'x')
		{
			return false;
		}

		for (var i = 2; i < 6; i++)
		{
			if (!IsHexDigit(text[i]))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Whether text starts with a character that <see cref="AppendText"/> writes as an <c>_xHHHH_</c> escape: a control
	/// character other than tab and LF, U+FFFE or U+FFFF.
	/// </summary>
	/// <param name="text">The text, which isn't empty.</param>
	/// <returns>Whether it does.</returns>
	private static bool IsWrittenAsEscape(ReadOnlySpan<byte> text) =>
		(text[0] < 0x20 && text[0] is not ((byte)'\t' or (byte)'\n')) || (text.Length >= 3 && text[0] == 0xEF && text[1] == 0xBF && text[2] is 0xBE or 0xBF);

	/// <summary>Whether text starts or ends with what Excel trims from text without <c>xml:space="preserve"</c>.</summary>
	/// <param name="text">The text.</param>
	/// <returns>Whether its first or last byte is a space, a tab or LF (CR is written as an escape, which isn't trimmed).</returns>
	public static bool NeedsPreserve(ReadOnlySpan<byte> text) =>
		text.Length > 0 && (IsTrimmed(text[0]) || IsTrimmed(text[text.Length - 1]));

	/// <summary>The bytes <see cref="AppendText"/> stops at, but <c>"</c>, which it stops at in attribute values only.</summary>
	/// <returns>The bytes.</returns>
	private static byte[] SpecialBytes()
	{
		var bytes = new System.Collections.Generic.List<byte> { (byte)'&', (byte)'<', (byte)'>', (byte)'_', 0xEF };
		for (var b = 0; b < 0x20; b++)
		{
			if (b is not ('\t' or '\n'))
			{
				bytes.Add((byte)b);
			}
		}

		return bytes.ToArray();
	}

#if !NET
	/// <summary>The table of <see cref="IsSpecial"/>.</summary>
	/// <returns>The table.</returns>
	private static bool[] SpecialTable()
	{
		var table = new bool[256];
		foreach (var b in SpecialBytes())
		{
			table[b] = true;
		}

		return table;
	}
#endif

	/// <summary>The index of the first byte that <see cref="AppendText"/> must look at.</summary>
	/// <param name="text">The text.</param>
	/// <param name="attribute">Whether <c>"</c> is one of them.</param>
	/// <returns>The index, or -1.</returns>
	private static int IndexOfSpecial(ReadOnlySpan<byte> text, bool attribute)
	{
#if NET
		var i = text.IndexOfAny(Specials);
#else
		var i = -1;
		for (var j = 0; j < text.Length; j++)
		{
			if (IsSpecial[text[j]])
			{
				i = j;

				break;
			}
		}
#endif
		if (attribute)
		{
			var quote = (i < 0 ? text : text.Slice(0, i)).IndexOf((byte)'"');
			if (quote >= 0)
			{
				return quote;
			}
		}

		return i;
	}

	/// <summary>Whether a byte is a space, a tab or LF.</summary>
	/// <param name="b">The byte.</param>
	/// <returns>Whether Excel trims it.</returns>
	private static bool IsTrimmed(byte b) => b is (byte)' ' or (byte)'\t' or (byte)'\n';

	/// <summary>Whether a byte is a hexadecimal digit of either case.</summary>
	/// <param name="b">The byte.</param>
	/// <returns>Whether it is one.</returns>
	private static bool IsHexDigit(byte b) => b is >= (byte)'0' and <= (byte)'9' or >= (byte)'A' and <= (byte)'F' or >= (byte)'a' and <= (byte)'f';

	/// <summary>An upper-case hexadecimal digit, as Excel writes escapes.</summary>
	/// <param name="value">The value, 0 to 15.</param>
	/// <returns>The digit.</returns>
	private static byte HexDigit(int value) => (byte)(value < 10 ? '0' + value : 'A' + value - 10);

	/// <summary>Makes room for bytes.</summary>
	/// <param name="count">The number of bytes.</param>
	private void Reserve(int count)
	{
		if (buffer.Length - length >= count)
		{
			return;
		}

		var size = Math.Max((long)buffer.Length * 2, (long)length + count);
		var bigger = ArrayPool<byte>.Shared.Rent((int)Math.Min(size, Array.MaxLength));
		buffer.AsSpan(0, length).CopyTo(bigger);
		ArrayPool<byte>.Shared.Return(buffer);
		buffer = bigger;
	}
}
