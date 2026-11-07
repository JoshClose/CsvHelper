using System;
using System.Globalization;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// The signs and decimal separator of a culture's numbers, as UTF-8, to read the numbers a writer formats with that
/// culture back as invariant text (<see cref="DotNetText.ToInvariantNumber"/>).
/// </summary>
internal sealed class NumberCulture
{
	private readonly byte[] negative;
	private readonly byte[] positive;
	private readonly byte[] separator;

	/// <summary>Creates the signs and separator of a culture.</summary>
	/// <param name="format">The culture's number format.</param>
	private NumberCulture(NumberFormatInfo format)
	{
		negative = Encoding.UTF8.GetBytes(format.NegativeSign);
		positive = Encoding.UTF8.GetBytes(format.PositiveSign);
		separator = Encoding.UTF8.GetBytes(format.NumberDecimalSeparator);
	}

	/// <summary>The signs and separator of a format provider's numbers.</summary>
	/// <param name="provider">The provider.</param>
	/// <returns>
	/// Them, or <see langword="null"/> when they are the invariant culture's (<c>-</c>, <c>+</c> and <c>.</c>), whose text
	/// is read as it is.
	/// </returns>
	public static NumberCulture? For(IFormatProvider provider)
	{
		var format = NumberFormatInfo.GetInstance(provider);

		return format.NegativeSign == "-" && format.PositiveSign == "+" && format.NumberDecimalSeparator == "." ? null : new NumberCulture(format);
	}

	/// <summary>
	/// Writes a number's text with the invariant signs and point in place of the culture's: its negative sign first, its
	/// decimal separator, and the sign of its exponent, after <c>E</c>. Digits are copied; anything else fails.
	/// </summary>
	/// <param name="text">The culture's text.</param>
	/// <param name="destination">The invariant text, which is no longer than <paramref name="text"/>.</param>
	/// <returns>The invariant text's length, or -1 when the text has something else.</returns>
	public int ToInvariant(ReadOnlySpan<byte> text, Span<byte> destination)
	{
		var length = 0;
		var i = 0;
		if (negative.Length > 0 && text.StartsWith(negative))
		{
			destination[length++] = (byte)'-';
			i = negative.Length;
		}

		while (i < text.Length)
		{
			var b = text[i];
			if ((uint)(b - '0') <= 9)
			{
				destination[length++] = b;
				i++;
			}
			else if (text.Slice(i).StartsWith(separator))
			{
				destination[length++] = (byte)'.';
				i += separator.Length;
			}
			else if (b == (byte)'E')
			{
				destination[length++] = b;
				i++;
				if (negative.Length > 0 && text.Slice(i).StartsWith(negative))
				{
					destination[length++] = (byte)'-';
					i += negative.Length;
				}
				else if (positive.Length > 0 && text.Slice(i).StartsWith(positive))
				{
					destination[length++] = (byte)'+';
					i += positive.Length;
				}
			}
			else
			{
				return -1;
			}
		}

		return length;
	}
}
