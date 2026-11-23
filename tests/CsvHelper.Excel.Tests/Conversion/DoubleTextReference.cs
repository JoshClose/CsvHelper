namespace CsvHelper.Excel.Tests.Conversion;

/// <summary>
/// <c>ColumnTypes.IsDoubleText</c> as it was before Phase 9's step 7 (at <c>6b21adca</c>), kept as the reference for its
/// answers: core's shortest text of the double formatted, and the significant digits and magnitude of both texts compared
/// (<c>Significant</c>). Copied as it was but for core's parser and formatter, reached through <see cref="CoreFloats"/>;
/// with <c>ColumnTypes.TryNumber</c>'s test of when it asks.
/// </summary>
internal static class DoubleTextReference
{
	/// <summary>The most significant digits Excel keeps of a number.</summary>
	private const int MaxSignificantDigits = 15;

	/// <summary>The most significant digits of a double's shortest round-trip text.</summary>
	private const int MaxDoubleDigits = 17;

	/// <summary>A cap on an exponent's value while it is read, far past the magnitudes taken, so that it can't overflow.</summary>
	private const int ExponentCap = 100_000;

	/// <summary>
	/// Whether <c>ColumnTypes.TryNumber</c> asks whether a number's invariant text is its double's: when it has more than 15
	/// significant digits, and a fraction or an exponent.
	/// </summary>
	/// <param name="text">The invariant text, of a number in Excel's range.</param>
	/// <param name="significant">Its significant digits' count.</param>
	/// <returns>Whether it asks.</returns>
	public static bool IsAsked(ReadOnlySpan<byte> text, out int significant)
	{
		significant = Significant(text, new byte[MaxDoubleDigits], out _);

		return significant > MaxSignificantDigits && (text.IndexOf((byte)'E') >= 0 || text.IndexOf((byte)'.') >= 0);
	}

	/// <summary>
	/// Whether a number's invariant text is its double's own: it has the significant digits, and the magnitude, of the
	/// shortest text that reads back as the double nearest it, so that the double gives them back.
	/// </summary>
	/// <param name="text">The invariant text: <c>-</c>, digits, <c>.</c>, and <c>E</c> with <c>+</c> or <c>-</c>.</param>
	/// <param name="significant">Its significant digits' count.</param>
	/// <returns>Whether it is.</returns>
	public static bool IsDoubleText(ReadOnlySpan<byte> text, int significant)
	{
		if (significant > MaxDoubleDigits || !CoreFloats.TryParseDouble(text, out var value))
		{
			return false;
		}

		Span<byte> shortest = stackalloc byte[CoreFloats.MaxDoubleLength];
		if (!CoreFloats.TryFormat(value, shortest, out var written))
		{
			return false;
		}

		Span<byte> digits = stackalloc byte[MaxDoubleDigits];
		Span<byte> shortestDigits = stackalloc byte[MaxDoubleDigits];
		var count = Significant(text, digits, out var magnitude);
		var shortestCount = Significant(shortest.Slice(0, written), shortestDigits, out var shortestMagnitude);

		return count == shortestCount && magnitude == shortestMagnitude && digits.Slice(0, count).SequenceEqual(shortestDigits.Slice(0, count));
	}

	/// <summary>
	/// The significant digits of a number's invariant text, from the first that isn't 0 to the last, and the decimal
	/// exponent of the first: <c>0.0123E2</c> has <c>123</c> and 0.
	/// </summary>
	/// <param name="text">The text, of a number that isn't 0, with at most <see cref="MaxDoubleDigits"/> significant digits.</param>
	/// <param name="digits">The digits, of <see cref="MaxDoubleDigits"/> bytes.</param>
	/// <param name="magnitude">The decimal exponent of the first digit.</param>
	/// <returns>The digits' count.</returns>
	public static int Significant(ReadOnlySpan<byte> text, Span<byte> digits, out int magnitude)
	{
		var count = 0;
		var kept = 0;
		var first = -1;
		var integer = 0;
		var inFraction = false;
		var i = 0;
		var position = 0;
		for (; i < text.Length && text[i] is not ((byte)'E' or (byte)'e'); i++)
		{
			var b = text[i];
			if (b == (byte)'.')
			{
				inFraction = true;
			}
			else if (IsDigit(b))
			{
				if (!inFraction)
				{
					integer++;
				}

				if (b != (byte)'0' && first < 0)
				{
					first = position;
				}

				if (first >= 0)
				{
					// Past the room, only zeros at the end can come, which aren't kept.
					if (count < digits.Length)
					{
						digits[count] = b;
					}

					count++;
					if (b != (byte)'0')
					{
						kept = count;
					}
				}

				position++;
			}
		}

		var exponent = 0;
		if (i < text.Length)
		{
			var negative = text[++i] == (byte)'-';
			i += text[i] is (byte)'-' or (byte)'+' ? 1 : 0;
			for (; i < text.Length; i++)
			{
				exponent = Math.Min((exponent * 10) + (text[i] - '0'), ExponentCap);
			}

			exponent = negative ? -exponent : exponent;
		}

		magnitude = integer - 1 - first + exponent;

		return kept;
	}

	/// <summary>Whether a byte is an ASCII digit.</summary>
	/// <param name="b">The byte.</param>
	/// <returns>Whether it is <c>0</c> to <c>9</c>.</returns>
	private static bool IsDigit(byte b) => (uint)(b - '0') <= 9;
}
