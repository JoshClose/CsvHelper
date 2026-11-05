using System;

namespace CsvHelper.Excel;

/// <summary>
/// How Excel reads the attributes that hide rows and columns, as checked through COM: a row's <c>hidden</c>, and its
/// <c>ht</c> of 0 with <c>customHeight</c>; a column's <c>hidden</c>, and its <c>width</c>; and a sheet's
/// <c>sheetFormatPr</c>, whose <c>zeroHeight</c>, or <c>customHeight</c> with a <c>defaultRowHeight</c> of 0, hides
/// the rows the file lacks, and whose <c>defaultColWidth</c> of 0 hides the columns no <c>col</c> covers.
/// </summary>
/// <remarks>
/// Excel decodes a value's references, then reads it strictly. A boolean is true only as <c>1</c> or <c>true</c>. A
/// number has no white space and no plus sign: <c>-0</c>, <c>.0</c>, <c>0.</c> and <c>0e+0</c> are 0, and so is an empty
/// value. A value it can't read is ignored rather than an error, so it hides nothing; except a column's width, which is
/// then 0, as a missing one is. Excel also hides a height or width too small to show a pixel, which depends on the screen
/// and the font; only 0 is read as hidden here.
/// </remarks>
internal static class HiddenState
{
	/// <summary>The room on the stack for a value whose references are decoded.</summary>
	private const int ScratchSize = 64;

	/// <summary>What an attribute of a row says about its being hidden.</summary>
	/// <param name="attributes">The row's attributes, at the attribute.</param>
	/// <returns>The flag a true <c>hidden</c>, a true <c>customHeight</c> or an <c>ht</c> of 0 sets; otherwise none.</returns>
	public static RowHiding RowFlagOf(XmlAttributes attributes)
	{
		if (attributes.IsName("hidden"u8))
		{
			return IsTrue(attributes) ? RowHiding.Hidden : RowHiding.None;
		}

		if (attributes.IsName("customHeight"u8))
		{
			return IsTrue(attributes) ? RowHiding.CustomHeight : RowHiding.None;
		}

		if (attributes.IsName("ht"u8))
		{
			return ZeroOf(attributes) == true ? RowHiding.ZeroHeight : RowHiding.None;
		}

		return RowHiding.None;
	}

	/// <summary>Whether a row is hidden: it says so, or its height is 0 and custom (without <c>customHeight</c>, Excel ignores <c>ht</c>).</summary>
	/// <param name="flags">The flags of the row's attributes.</param>
	/// <returns>Whether the row is hidden.</returns>
	public static bool IsHiddenRow(RowHiding flags) =>
		(flags & RowHiding.Hidden) != 0 || (flags & (RowHiding.CustomHeight | RowHiding.ZeroHeight)) == (RowHiding.CustomHeight | RowHiding.ZeroHeight);

	/// <summary>Whether an attribute is true as Excel reads the booleans that hide rows and columns: <c>1</c> or <c>true</c>, once decoded.</summary>
	/// <param name="attributes">The attributes, at the attribute.</param>
	/// <returns>Whether it is true; any other value is false.</returns>
	/// <exception cref="System.IO.InvalidDataException">The value has a reference that can't be decoded.</exception>
	public static bool IsTrue(XmlAttributes attributes)
	{
		Span<byte> scratch = stackalloc byte[ScratchSize];
		var value = Decoded(attributes, scratch);

		return value.SequenceEqual("1"u8) || value.SequenceEqual("true"u8);
	}

	/// <summary>Whether an attribute is a height or width of 0, as Excel reads one.</summary>
	/// <param name="attributes">The attributes, at the attribute.</param>
	/// <returns>Whether it is 0; <see langword="null"/> when it is no number Excel reads.</returns>
	/// <exception cref="System.IO.InvalidDataException">The value has a reference that can't be decoded.</exception>
	public static bool? ZeroOf(XmlAttributes attributes)
	{
		Span<byte> scratch = stackalloc byte[ScratchSize];

		return ZeroOf(Decoded(attributes, scratch));
	}

	/// <summary>
	/// Whether a value is a number of 0, as Excel reads one: an optional <c>-</c>, digits with an optional <c>.</c> (at
	/// least one digit), and an optional exponent (<c>e</c> or <c>E</c>, an optional sign, digits). Empty is 0.
	/// </summary>
	/// <param name="value">The value, decoded.</param>
	/// <returns>Whether it is 0; <see langword="null"/> when it is no such number.</returns>
	public static bool? ZeroOf(ReadOnlySpan<byte> value)
	{
		if (value.IsEmpty)
		{
			return true;
		}

		var i = value[0] == (byte)'-' ? 1 : 0;
		var digits = 0;
		var zero = true;
		var point = false;
		for (; i < value.Length; i++)
		{
			var b = value[i];
			if (b == (byte)'.' && !point)
			{
				point = true;
			}
			else if (b is >= (byte)'0' and <= (byte)'9')
			{
				digits++;
				zero &= b == (byte)'0';
			}
			else
			{
				break;
			}
		}

		if (digits == 0)
		{
			return null;
		}

		if (i < value.Length && (value[i] | 0x20) == (byte)'e')
		{
			i++;
			if (i < value.Length && value[i] is (byte)'+' or (byte)'-')
			{
				i++;
			}

			var start = i;
			while (i < value.Length && value[i] is >= (byte)'0' and <= (byte)'9')
			{
				i++;
			}

			if (i == start)
			{
				return null;
			}
		}

		return i == value.Length ? zero : null;
	}

	/// <summary>An attribute's value, with its references decoded when it has any.</summary>
	/// <param name="attributes">The attributes, at the attribute.</param>
	/// <param name="scratch">Room for the decoded value; a longer one gets its own array.</param>
	/// <returns>The value.</returns>
	/// <exception cref="System.IO.InvalidDataException">The value has a reference that can't be decoded.</exception>
	private static ReadOnlySpan<byte> Decoded(XmlAttributes attributes, Span<byte> scratch)
	{
		var value = attributes.Value;
		if (value.IndexOf((byte)'&') < 0)
		{
			return value;
		}

		var destination = value.Length <= scratch.Length ? scratch : new byte[value.Length];

		return destination.Slice(0, attributes.DecodeValue(destination));
	}
}

/// <summary>What a row's attributes say about its being hidden.</summary>
[Flags]
internal enum RowHiding : byte
{
	/// <summary>Nothing.</summary>
	None = 0,

	/// <summary>A true <c>hidden</c>.</summary>
	Hidden = 1,

	/// <summary>A true <c>customHeight</c>.</summary>
	CustomHeight = 2,

	/// <summary>An <c>ht</c> of 0.</summary>
	ZeroHeight = 4,
}
