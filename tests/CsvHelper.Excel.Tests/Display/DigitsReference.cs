namespace CsvHelper.Excel.Tests.Display;

/// <summary>
/// <see cref="Digits.Of"/> as it was before Phase 9's step 7 (at <c>6b21adca</c>), kept as the reference for its digits:
/// core's shortest text of the double formatted and read back (<c>Shortest</c>), then taken to 15 digits, an exact half by
/// the double's exact value. Copied as it was but for its result, a tuple of <see cref="Digits"/>' properties, and core's
/// formatter, reached through <see cref="CoreFloats"/>.
/// </summary>
internal static class DigitsReference
{
	/// <summary>The most significant digits Excel keeps.</summary>
	private const int MaxSignificant = Digits.MaxSignificant;

	/// <summary>The smallest normal double: Excel holds a smaller one as 0.</summary>
	private const double MinNormal = 2.2250738585072014E-308;

	/// <summary>The 32-bit words of the integers the exact comparison multiplies: 1,280 bits, past the 841 it needs.</summary>
	private const int Words = 40;

	/// <summary>5^13, the largest power of five in 32 bits.</summary>
	private const uint FivePower13 = 1_220_703_125;

	/// <summary>The powers of ten that fit in a <see cref="ulong"/>.</summary>
	private static readonly ulong[] Powers =
	[
		1, 10, 100, 1_000, 10_000, 100_000, 1_000_000, 10_000_000, 100_000_000, 1_000_000_000, 10_000_000_000,
		100_000_000_000, 1_000_000_000_000, 10_000_000_000_000, 100_000_000_000_000, 1_000_000_000_000_000,
		10_000_000_000_000_000, 100_000_000_000_000_000, 1_000_000_000_000_000_000, 10_000_000_000_000_000_000,
	];

	/// <summary>
	/// A number's absolute value at 15 significant digits, as <see cref="Digits.Of"/> took it through core's text.
	/// </summary>
	/// <param name="value">The number, finite.</param>
	/// <returns>Its digits, their count and the power of ten of the first; all 0 for zero.</returns>
	public static (ulong Significand, int Count, int Exponent) Of(double value)
	{
		var abs = Math.Abs(value);
		if (!(abs >= MinNormal) || double.IsInfinity(abs))
		{
			return default;
		}

		Span<byte> text = stackalloc byte[CoreFloats.MaxDoubleLength];
		CoreFloats.TryFormat(abs, text, out var written);
		Shortest(text.Slice(0, written), out var significand, out var count, out var exponent);
		if (count <= MaxSignificant)
		{
			return (significand, count, exponent);
		}

		// 16 or 17 digits: the digits past the 15th decide, but for exactly the half, where the double does.
		var dropped = count - MaxSignificant;
		var kept = significand / Powers[dropped];
		var rest = significand % Powers[dropped];
		var half = 5 * Powers[dropped - 1];
		if (rest > half || (rest == half && IsAbove(abs, significand, exponent - count + 1)))
		{
			kept++;
		}

		return Normalize(kept, MaxSignificant, exponent);
	}

	/// <summary>Digits from an integer whose first digit is at a power of ten, without its trailing zeros.</summary>
	/// <param name="significand">The integer, not zero.</param>
	/// <param name="digits">How many digits it was meant to have: one more when it carried (999 + 1).</param>
	/// <param name="exponent">The power of ten of its first digit, before a carry.</param>
	/// <returns>The digits.</returns>
	private static (ulong Significand, int Count, int Exponent) Normalize(ulong significand, int digits, int exponent)
	{
		if (significand >= Powers[digits])
		{
			exponent++;
			digits++;
		}

		while (significand % 10 == 0)
		{
			significand /= 10;
			digits--;
		}

		return (significand, digits, exponent);
	}

	/// <summary>
	/// Reads core's shortest text of a positive number (<c>123.45</c>, <c>0.0001</c>, <c>1E-05</c>, <c>1.5E+300</c>):
	/// its significant digits as an integer, without leading or trailing zeros, and the power of ten of the first.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <param name="significand">The digits, 17 at most.</param>
	/// <param name="count">How many there are.</param>
	/// <param name="exponent">The power of ten of the first.</param>
	private static void Shortest(ReadOnlySpan<byte> text, out ulong significand, out int count, out int exponent)
	{
		significand = 0;
		count = 0;
		var integerDigits = -1;
		var position = 0;
		var first = -1;
		var trailingZeros = 0;
		var i = 0;
		for (; i < text.Length && text[i] != (byte)'E'; i++)
		{
			var c = text[i];
			if (c == (byte)'.')
			{
				integerDigits = position;

				continue;
			}

			var digit = c - (byte)'0';
			if (first < 0 && digit == 0)
			{
				position++;

				continue;
			}

			first = first < 0 ? position : first;
			position++;
			if (digit == 0)
			{
				trailingZeros++;

				continue;
			}

			// Zeros between significant digits count once a later digit follows them.
			while (trailingZeros > 0)
			{
				significand *= 10;
				count++;
				trailingZeros--;
			}

			significand = (significand * 10) + (ulong)digit;
			count++;
		}

		if (integerDigits < 0)
		{
			integerDigits = position;
		}

		var power = 0;
		if (i < text.Length)
		{
			var negative = text[i + 1] == (byte)'-';
			for (i += 2; i < text.Length; i++)
			{
				power = (power * 10) + (text[i] - (byte)'0');
			}

			power = negative ? -power : power;
		}

		exponent = integerDigits - 1 - first + power;
	}

	/// <summary>Whether a double is above a decimal, exactly: <c>c × 2^e</c> against <c>m × 10^k</c>, in integers on the stack.</summary>
	/// <param name="value">The double, positive and finite.</param>
	/// <param name="decimalSignificand">The decimal's significand, below 10^17.</param>
	/// <param name="decimalExponent">The decimal's power of ten, of its last digit.</param>
	/// <returns>Whether the double is greater.</returns>
	private static bool IsAbove(double value, ulong decimalSignificand, int decimalExponent)
	{
		var bits = (ulong)BitConverter.DoubleToInt64Bits(value);
		var biased = (int)(bits >> 52) & 0x7FF;
		var fraction = bits & 0x000F_FFFF_FFFF_FFFFUL;
		var binarySignificand = biased == 0 ? fraction : fraction | (1UL << 52);
		var binaryExponent = (biased == 0 ? 1 : biased) - 1075;

		// c × 2^e against m × 5^k × 2^k: the power of five goes to the side whose exponent is positive, then the smaller
		// power of two is taken from both.
		Span<uint> left = stackalloc uint[Words];
		Span<uint> right = stackalloc uint[Words];
		var leftLength = Set(left, binarySignificand);
		var rightLength = Set(right, decimalSignificand);
		if (decimalExponent >= 0)
		{
			rightLength = MultiplyByPowerOfFive(right, rightLength, decimalExponent);
		}
		else
		{
			leftLength = MultiplyByPowerOfFive(left, leftLength, -decimalExponent);
		}

		if (binaryExponent > decimalExponent)
		{
			leftLength = ShiftLeft(left, leftLength, binaryExponent - decimalExponent);
		}
		else if (decimalExponent > binaryExponent)
		{
			rightLength = ShiftLeft(right, rightLength, decimalExponent - binaryExponent);
		}

		return Compare(left, leftLength, right, rightLength) > 0;
	}

	/// <summary>Sets an integer of words to a <see cref="ulong"/>.</summary>
	/// <param name="words">The words, least significant first.</param>
	/// <param name="value">The value.</param>
	/// <returns>How many words it takes: 0 for zero.</returns>
	private static int Set(Span<uint> words, ulong value)
	{
		words[0] = (uint)value;
		words[1] = (uint)(value >> 32);

		return words[1] != 0 ? 2 : words[0] != 0 ? 1 : 0;
	}

	/// <summary>Multiplies an integer of words by a power of five.</summary>
	/// <param name="words">The words, least significant first.</param>
	/// <param name="length">How many words it takes.</param>
	/// <param name="power">The power, 0 or more.</param>
	/// <returns>How many words the product takes.</returns>
	private static int MultiplyByPowerOfFive(Span<uint> words, int length, int power)
	{
		for (; power >= 13; power -= 13)
		{
			length = Multiply(words, length, FivePower13);
		}

		var factor = 1u;
		for (; power > 0; power--)
		{
			factor *= 5;
		}

		return Multiply(words, length, factor);
	}

	/// <summary>Multiplies an integer of words by a word.</summary>
	/// <param name="words">The words, least significant first.</param>
	/// <param name="length">How many words it takes.</param>
	/// <param name="factor">The factor.</param>
	/// <returns>How many words the product takes.</returns>
	private static int Multiply(Span<uint> words, int length, uint factor)
	{
		var carry = 0UL;
		for (var i = 0; i < length; i++)
		{
			var product = ((ulong)words[i] * factor) + carry;
			words[i] = (uint)product;
			carry = product >> 32;
		}

		if (carry != 0)
		{
			words[length++] = (uint)carry;
		}

		return length;
	}

	/// <summary>Shifts an integer of words left by some bits.</summary>
	/// <param name="words">The words, least significant first.</param>
	/// <param name="length">How many words it takes.</param>
	/// <param name="bits">The bits, 1 or more.</param>
	/// <returns>How many words the result takes.</returns>
	private static int ShiftLeft(Span<uint> words, int length, int bits)
	{
		if (length == 0)
		{
			return 0;
		}

		var whole = bits / 32;
		var part = bits % 32;
		if (part == 0)
		{
			for (var i = length - 1; i >= 0; i--)
			{
				words[i + whole] = words[i];
			}
		}
		else
		{
			words[length + whole] = words[length - 1] >> (32 - part);
			for (var i = length - 1; i > 0; i--)
			{
				words[i + whole] = (words[i] << part) | (words[i - 1] >> (32 - part));
			}

			words[whole] = words[0] << part;
			length++;
		}

		words.Slice(0, whole).Clear();
		length += whole;
		while (length > 0 && words[length - 1] == 0)
		{
			length--;
		}

		return length;
	}

	/// <summary>Compares two integers of words.</summary>
	/// <param name="left">The first, least significant word first.</param>
	/// <param name="leftLength">How many words it takes, without leading zeros.</param>
	/// <param name="right">The second.</param>
	/// <param name="rightLength">How many words it takes, without leading zeros.</param>
	/// <returns>Less than 0, 0 or more than 0, as the first is less, equal or greater.</returns>
	private static int Compare(ReadOnlySpan<uint> left, int leftLength, ReadOnlySpan<uint> right, int rightLength)
	{
		if (leftLength != rightLength)
		{
			return leftLength - rightLength;
		}

		for (var i = leftLength - 1; i >= 0; i--)
		{
			if (left[i] != right[i])
			{
				return left[i] < right[i] ? -1 : 1;
			}
		}

		return 0;
	}
}
