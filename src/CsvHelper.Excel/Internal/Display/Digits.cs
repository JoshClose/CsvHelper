using System;

namespace CsvHelper.Excel;

/// <summary>
/// A number's decimal digits as Excel shows them: its absolute value taken to at most 15 significant digits, then rounded
/// half away from zero to the places a format shows. The digits are an integer and the power of ten of the first, so
/// that taking them, rounding them and reading them allocates nothing on any target.
/// </summary>
/// <remarks>
/// <para>
/// Excel takes a double to 15 significant digits by its exact binary value, rounded to the nearest, as Excel 365 showed
/// through COM on 2026-11-16 (<c>Csv\_bench\results\phase8\step2\digits</c>): <c>0.00000000000000E+00</c> showed 90
/// doubles whose shortest text has 16 digits ending in 5 (and <c>0.00000000000000</c> 6 more) by the side of that half their exact value lies on
/// (2.379781934212055E-100 is a little below its text, and shows <c>2.37978193421205E-100</c>), where the text rounded
/// half away from zero would always go up. A double that is exactly the half goes down: 634 of 638 such doubles
/// (123928122675506.5 shows <c>1.23928122675506E+14</c>, and 5175114241336295 <c>5.17511424133629E+15</c>), whichever
/// digit precedes it. Excel takes the other four up (548368913012129.5, 506320692698959.5, and 5187285576.265625 of either
/// sign), which no rule found explains, so they are listed differences.
/// </para>
/// <para>
/// The digits come from core's shortest round-trip text (<see cref="FloatFormatter"/>): a double whose shortest text has
/// 15 digits or fewer has those as its nearest 15-digit number, since the 15-digit numbers near it are further apart than
/// its rounding interval; one of 16 or 17 digits rounds by the digits past the 15th, which lie on the same side of the
/// half as the double does, unless they are exactly the half (16 digits ending in 5), where the double is compared with
/// that decimal exactly. Excel holds a number below the smallest normal double as 0 (its <c>Value2</c> for 5E-324 is 0),
/// so such a number's digits are zero, as are those of the low-precision doubles whose shortest text wouldn't be nearest.
/// </para>
/// </remarks>
internal readonly struct Digits
{
	/// <summary>The most significant digits Excel keeps.</summary>
	public const int MaxSignificant = 15;

	/// <summary>The smallest normal double: Excel holds a smaller one as 0 (5E-324 shows <c>0</c>, its <c>Value2</c> 0).</summary>
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

	/// <summary>Creates digits.</summary>
	/// <param name="significand">The significant digits as an integer, without trailing zeros: 0 for zero.</param>
	/// <param name="count">How many digits it has: 0 for zero.</param>
	/// <param name="exponent">The power of ten of the first digit.</param>
	private Digits(ulong significand, int count, int exponent)
	{
		Significand = significand;
		Count = count;
		Exponent = exponent;
	}

	/// <summary>The significant digits as an integer, without trailing zeros: 0 for zero.</summary>
	public ulong Significand { get; }

	/// <summary>How many significant digits there are, from 1 to 15: 0 for zero.</summary>
	public int Count { get; }

	/// <summary>The power of ten of the first significant digit: 2 for 123.4, −1 for 0.5; 0 for zero.</summary>
	public int Exponent { get; }

	/// <summary>Whether the number is zero.</summary>
	public bool IsZero => Count == 0;

	/// <summary>How many digits the integer part has: 0 when it is 0.</summary>
	public int IntegerLength => IsZero || Exponent < 0 ? 0 : Exponent + 1;

	/// <summary>
	/// A number's absolute value at 15 significant digits, as Excel takes it: its exact binary value rounded to the nearest,
	/// and one exactly between two 15-digit numbers to the smaller.
	/// </summary>
	/// <param name="value">The number, finite.</param>
	/// <returns>Its digits; zero for 0, −0, a value that isn't finite, and one below the smallest normal double, which Excel holds as 0.</returns>
	public static Digits Of(double value)
	{
		var abs = Math.Abs(value);
		if (!(abs >= MinNormal) || double.IsInfinity(abs))
		{
			return default;
		}

		Span<byte> text = stackalloc byte[FloatFormatter.MaxDoubleLength];
		FloatFormatter.TryFormat(abs, text, out var written);
		Shortest(text.Slice(0, written), out var significand, out var count, out var exponent);
		if (count <= MaxSignificant)
		{
			return new Digits(significand, count, exponent);
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

	/// <summary>The number times a power of ten: each <c>%</c> is 2, each scaling comma −3.</summary>
	/// <param name="power">The power.</param>
	/// <returns>The number.</returns>
	public Digits Scale(int power) => IsZero ? this : new Digits(Significand, Count, Exponent + power);

	/// <summary>The number rounded half away from zero to some places after the decimal point.</summary>
	/// <param name="decimals">The places: 2 for <c>0.00</c>, 0 for whole numbers, and less for tens and above.</param>
	/// <returns>The number; zero when it rounds to zero.</returns>
	public Digits Round(int decimals)
	{
		var keep = Exponent + 1 + decimals;
		if (IsZero || keep >= Count)
		{
			return this;
		}

		if (keep < 0)
		{
			return default;
		}

		var dropped = Count - keep;
		var kept = Significand / Powers[dropped];
		if (Significand / Powers[dropped - 1] % 10 >= 5)
		{
			kept++;
		}

		// Kept digits all nines, or none kept, carry to one more digit: Normalize counts them again.
		return kept == 0 ? default : Normalize(kept, keep, Exponent);
	}

	/// <summary>The digit at a power of ten: 0 past the significant digits.</summary>
	/// <param name="power">The power: 0 for the units, −1 for the tenths.</param>
	/// <returns>The digit, 0 to 9.</returns>
	public int DigitAt(int power)
	{
		var index = Exponent - power;
		if (IsZero || index < 0 || index >= Count)
		{
			return 0;
		}

		return (int)(Significand / Powers[Count - 1 - index] % 10);
	}

	/// <summary>Digits from an integer whose first digit is at a power of ten, without its trailing zeros.</summary>
	/// <param name="significand">The integer, not zero.</param>
	/// <param name="digits">How many digits it was meant to have: one more when it carried (999 + 1).</param>
	/// <param name="exponent">The power of ten of its first digit, before a carry.</param>
	/// <returns>The digits.</returns>
	private static Digits Normalize(ulong significand, int digits, int exponent)
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

		return new Digits(significand, digits, exponent);
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
