using System.Globalization;
using System.Text;
using Xunit.Abstractions;

namespace CsvHelper.Excel.Tests.Conversion;

/// <summary>
/// Whether a number of more than 15 significant digits is its double's own text (<c>ColumnTypes.IsDoubleText</c>, seen
/// through <see cref="ColumnTypes.Format"/>), against the text path it replaced (<see cref="DoubleTextReference"/>): random
/// doubles' texts; the same with a digit changed, added or dropped; the same numbers in other notations (exponents,
/// leading and trailing zeros, signs); and the edges of 16 and 17 digits.
/// </summary>
/// <param name="output">Shows how many fields each test asked about, and the answers.</param>
public class DoubleTextTests(ITestOutputHelper output)
{
	[Fact]
	public void AnswersAsTheTextPathForRandomDoublesTexts()
	{
		var random = new Random(4760);
		var checker = new Checker();
		for (var i = 0; i < 40_000; i++)
		{
			var value = NextDouble(random, i);
			var shortest = CoreFloats.Text(value);
			checker.Check(value.ToString("R", CultureInfo.InvariantCulture));
			checker.Check(shortest);
			checker.Check(value.ToString("G17", CultureInfo.InvariantCulture));
			checker.Check(Changed(shortest, random));
			checker.Check(Added(shortest, random));
			checker.Check(Dropped(shortest, random));
			checker.Check(LastDigitMoved(shortest, 1));
			checker.Check(LastDigitMoved(shortest, -1));
		}

		output.WriteLine(checker.Summary);
		checker.AssertNoFailures();
		checker.AssertBothAnswers(30_000);
	}

	[Fact]
	public void AnswersAsTheTextPathForOtherNotations()
	{
		var random = new Random(31);
		var checker = new Checker();
		for (var i = 0; i < 20_000; i++)
		{
			var value = NextDouble(random, i);
			foreach (var text in new[] { CoreFloats.Text(value), Changed(CoreFloats.Text(value), random), Added(CoreFloats.Text(value), random) })
			{
				foreach (var notation in Notations(text))
				{
					checker.Check(notation);
				}
			}
		}

		output.WriteLine(checker.Summary);
		checker.AssertNoFailures();
		checker.AssertBothAnswers(30_000);
	}

	[Fact]
	public void AnswersAsTheTextPathAtTheEdgesOf16And17Digits()
	{
		var random = new Random(1617);
		var checker = new Checker();
		for (var i = 0; i < 20_000; i++)
		{
			// Doubles whose shortest text has 16 digits, and 17: with a digit more, a digit less, and as 17 digits.
			var value = (random.NextDouble() + 0.1) * Math.Pow(10, random.Next(-20, 21));
			var shortest = CoreFloats.Text(value);
			checker.Check(shortest);
			checker.Check(value.ToString("G17", CultureInfo.InvariantCulture));
			checker.Check(value.ToString("G16", CultureInfo.InvariantCulture));
			checker.Check(value.ToString("G15", CultureInfo.InvariantCulture));
			checker.Check(value.ToString("E16", CultureInfo.InvariantCulture));
			checker.Check(value.ToString("E15", CultureInfo.InvariantCulture));
			checker.Check(value.ToString("E17", CultureInfo.InvariantCulture));
			checker.Check(Mantissa(shortest) + random.Next(1, 10).ToString(CultureInfo.InvariantCulture) + Exponent(shortest));
			checker.Check(Mantissa(shortest) + "0" + Exponent(shortest));
			checker.Check(Neighbour(value, 1));
			checker.Check(Neighbour(value, -1));
		}

		// Integers near 2^53 with a fraction or an exponent: 2^53 + 1 isn't a double.
		for (var offset = -2_000L; offset <= 2_000; offset++)
		{
			var integer = (9_007_199_254_740_992L + offset).ToString(CultureInfo.InvariantCulture);
			checker.Check(integer + ".0");
			checker.Check(integer + ".5");
			checker.Check(integer + "E0");
			checker.Check(integer.Substring(0, 1) + "." + integer.Substring(1) + "E15");
			checker.Check("0." + integer + "E16");
		}

		// 17 digits as an integer with an exponent, 18 digits (no double's text), and the range's edges.
		for (var i = 0; i < 5_000; i++)
		{
			var digits = (100_000_000_000_000_000L + (long)(random.NextDouble() * 899_999_999_999_999_999L)).ToString(CultureInfo.InvariantCulture);
			checker.Check(digits.Substring(0, 17) + "E0");
			checker.Check(digits.Substring(0, 17) + "E-" + random.Next(0, 300).ToString(CultureInfo.InvariantCulture));
			checker.Check(digits + "E0");
			checker.Check(digits.Substring(0, 1) + "." + digits.Substring(1) + "E-5");
			checker.Check(digits.Substring(0, 1) + "." + digits.Substring(1, 15) + "E-307");
			checker.Check(digits.Substring(0, 1) + "." + digits.Substring(1, 16) + "E307");
			checker.Check("-" + digits.Substring(0, 1) + "." + digits.Substring(1, 16) + "E-307");
		}

		foreach (var field in new[]
		{
			"1.0000000000000002E-307", "1.000000000000001E-307", "9.999999999999999E307", "9.9999999999999999E307",
			"2.2250738585072014E-308", "1.7976931348623157E+308", "4.505630514819934", "0.30000000000000004", "0.30000000000000005",
			"1234567890123456.7", "12345678901234.567", "1.2345678901234568E+16", "1234567890123456E0", "4.5056305148199340",
			"45056305148199.34E-13", "0.000000004505630514819934", "0.1234567890123456789", "123456789012345.5",
			"9.007199254740993E15", "1.5000000000000000", "-0.0000000000000000000000000000000000000000000004505630514819934E+40",
		})
		{
			checker.Check(field);
		}

		output.WriteLine(checker.Summary);
		checker.AssertNoFailures();
		checker.AssertBothAnswers(10_000);
	}

	/// <summary>A random double: of 16 or 17 digits mostly, of any bits, of a few digits, or near 2^53.</summary>
	/// <param name="random">The random numbers.</param>
	/// <param name="i">Which double this is, to choose the kind.</param>
	/// <returns>The double, finite.</returns>
	private static double NextDouble(Random random, int i)
	{
		var sign = random.Next(4) == 0 ? -1 : 1;
		switch (i % 4)
		{
			case 0:
			{
				var bytes = new byte[8];
				double value;
				do
				{
					random.NextBytes(bytes);
					value = BitConverter.ToDouble(bytes, 0);
				}
				while (double.IsNaN(value) || double.IsInfinity(value) || value == 0);

				return value;
			}

			case 1:
				return sign * Math.Floor(random.NextDouble() * Math.Pow(10, random.Next(1, 16))) * Math.Pow(10, random.Next(-30, 30));
			case 2:
				return sign * (9_007_199_254_740_992.0 + random.Next(-1000, 1000)) * Math.Pow(2, random.Next(-60, 4));
			default:
				return sign * random.NextDouble() * Math.Pow(10, random.Next(-25, 26));
		}
	}

	/// <summary>A text with one of its mantissa's digits changed to another.</summary>
	/// <param name="text">The text.</param>
	/// <param name="random">The random numbers.</param>
	/// <returns>The changed text.</returns>
	private static string Changed(string text, Random random)
	{
		var positions = DigitPositions(text);
		var at = positions[random.Next(positions.Count)];
		var digit = (char)('0' + ((text[at] - '0' + random.Next(1, 10)) % 10));

		return text.Substring(0, at) + digit + text.Substring(at + 1);
	}

	/// <summary>A text with a digit added to its mantissa.</summary>
	/// <param name="text">The text.</param>
	/// <param name="random">The random numbers.</param>
	/// <returns>The longer text.</returns>
	private static string Added(string text, Random random)
	{
		var positions = DigitPositions(text);
		var at = positions[random.Next(positions.Count)] + random.Next(0, 2);

		return text.Substring(0, at) + (char)('0' + random.Next(0, 10)) + text.Substring(at);
	}

	/// <summary>A text with a digit of its mantissa dropped, when it has more than one.</summary>
	/// <param name="text">The text.</param>
	/// <param name="random">The random numbers.</param>
	/// <returns>The shorter text.</returns>
	private static string Dropped(string text, Random random)
	{
		var positions = DigitPositions(text);
		if (positions.Count < 2)
		{
			return text;
		}

		var at = positions[random.Next(positions.Count)];

		return text.Substring(0, at) + text.Substring(at + 1);
	}

	/// <summary>A text with its mantissa's last digit one more or one less, when it stays a digit.</summary>
	/// <param name="text">The text.</param>
	/// <param name="step">1 or -1.</param>
	/// <returns>The text.</returns>
	private static string LastDigitMoved(string text, int step)
	{
		var positions = DigitPositions(text);
		var at = positions[positions.Count - 1];
		var digit = text[at] - '0' + step;

		return digit is < 0 or > 9 ? text : text.Substring(0, at) + (char)('0' + digit) + text.Substring(at + 1);
	}

	/// <summary>The positions of a text's mantissa's digits.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The positions.</returns>
	private static List<int> DigitPositions(string text)
	{
		var positions = new List<int>();
		for (var i = 0; i < text.Length && text[i] is not ('E' or 'e'); i++)
		{
			if (text[i] is >= '0' and <= '9')
			{
				positions.Add(i);
			}
		}

		return positions;
	}

	/// <summary>A text's mantissa.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The text up to its exponent, all of it without one; with a point.</returns>
	private static string Mantissa(string text)
	{
		var e = text.IndexOf('E');
		var mantissa = e < 0 ? text : text.Substring(0, e);

		return mantissa.IndexOf('.') < 0 ? mantissa + "." : mantissa;
	}

	/// <summary>A text's exponent.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The exponent, with its <c>E</c>: empty without one.</returns>
	private static string Exponent(string text)
	{
		var e = text.IndexOf('E');

		return e < 0 ? "" : text.Substring(e);
	}

	/// <summary>The 17-digit text of a double a step away.</summary>
	/// <param name="value">The double.</param>
	/// <param name="step">1 or -1.</param>
	/// <returns>The text.</returns>
	private static string Neighbour(double value, int step)
	{
		var bits = BitConverter.DoubleToInt64Bits(value) + step;

		return BitConverter.Int64BitsToDouble(bits).ToString("G17", CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// A number's text in other notations, of the same significant digits and magnitude: with an exponent of each form,
	/// fixed, with leading zeros and with trailing zeros, the mantissa's point moved, and with a sign.
	/// </summary>
	/// <param name="text">The text: an invariant number.</param>
	/// <returns>The other texts; none for a text that isn't one with a digit other than 0.</returns>
	private static IEnumerable<string> Notations(string text)
	{
		var sign = text.StartsWith("-", StringComparison.Ordinal) ? "-" : "";
		var e = text.IndexOf('E');
		var mantissa = (e < 0 ? text : text.Substring(0, e)).TrimStart('-');
		var power = e < 0 ? 0 : int.Parse(text.Substring(e + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
		var point = mantissa.IndexOf('.');
		var all = mantissa.Replace(".", "");
		var digits = all.TrimStart('0').TrimEnd('0');
		if (digits.Length == 0)
		{
			yield break;
		}

		// The power of ten of the first significant digit.
		var magnitude = (point < 0 ? mantissa.Length : point) - 1 - all.IndexOf(digits[0]) + power;
		var head = digits.Substring(0, 1);
		var tail = digits.Length > 1 ? "." + digits.Substring(1) : "";
		var tens = magnitude.ToString(CultureInfo.InvariantCulture);
		var signedTens = (magnitude < 0 ? "-" : "+") + Math.Abs(magnitude).ToString(CultureInfo.InvariantCulture);
		foreach (var s in new[] { sign, sign == "-" ? "" : "-" })
		{
			yield return s + head + tail + "E" + signedTens;
			yield return s + head + tail + "e" + tens;
			yield return s + head + tail + "E" + (magnitude < 0 ? "-" : "+") + Math.Abs(magnitude).ToString("000", CultureInfo.InvariantCulture);
			yield return s + head + (tail.Length > 0 ? tail : ".") + "000E" + signedTens;
			yield return s + "0.0" + digits + "E" + (magnitude + 2).ToString(CultureInfo.InvariantCulture);
			yield return s + digits + "E" + (magnitude - digits.Length + 1).ToString(CultureInfo.InvariantCulture);
			yield return s + digits.Substring(0, digits.Length / 2) + "." + digits.Substring(digits.Length / 2) + "0E" + (magnitude - (digits.Length / 2) + 1).ToString(CultureInfo.InvariantCulture);
			if (magnitude is >= 0 and < 25)
			{
				// Fixed: the integer part padded with zeros, and a fraction of the rest or of zeros.
				var integer = digits.Length > magnitude + 1 ? digits.Substring(0, magnitude + 1) : digits.PadRight(magnitude + 1, '0');
				var fraction = digits.Length > magnitude + 1 ? digits.Substring(magnitude + 1) : "0";
				yield return s + integer + "." + fraction;
				yield return s + integer + "." + fraction + "000";
			}
			else if (magnitude is < 0 and > -25)
			{
				yield return s + "0." + new string('0', -magnitude - 1) + digits;
				yield return s + "0." + new string('0', -magnitude - 1) + digits + "00";
			}
		}
	}

	/// <summary>
	/// Checks fields' answers against the reference: with long numbers as numbers, whether each is handed to the sink as a
	/// double; with long numbers as text (the default), whether it is a number at all; and the same in <c>de-DE</c>.
	/// </summary>
	private sealed class Checker
	{
		/// <summary>Long numbers as numbers: a number's form is written whatever its digits, and its cell type tells.</summary>
		private static readonly ColumnTypes Numbers = new(CultureInfo.InvariantCulture, new ExcelWriterOptions { LongNumbers = ExcelLongNumbers.Number });

		/// <summary>Long numbers as text: a long number that isn't its double's text is text.</summary>
		private static readonly ColumnTypes Texts = new(CultureInfo.InvariantCulture, ExcelWriterOptions.Default);

		/// <summary>The same in a culture of a comma for a point.</summary>
		private static readonly ColumnTypes German = new(new CultureInfo("de-DE"), new ExcelWriterOptions { LongNumbers = ExcelLongNumbers.Number });

		private readonly byte[] form = new byte[ColumnTypes.MaxFormLength];
		private readonly byte[] other = new byte[ColumnTypes.MaxFormLength];
		private readonly List<string> failures = [];
		private int mismatches;
		private int doubles;
		private int notDoubles;
		private int fields;

		/// <summary>Checks a field, unless it isn't a number in Excel's range, which isn't asked.</summary>
		/// <param name="field">The field, in the invariant culture.</param>
		public void Check(string field)
		{
			fields++;
			var bytes = Encoding.UTF8.GetBytes(field);
			var length = Numbers.Format(bytes, ColumnType.Number, form, out var cellType);
			if (length < 0)
			{
				return;
			}

			var text = form.AsSpan(0, length).ToArray();
			var asked = DoubleTextReference.IsAsked(text, out var significant);
			var expected = asked && DoubleTextReference.IsDoubleText(text, significant);
			if (asked)
			{
				doubles += expected ? 1 : 0;
				notDoubles += expected ? 0 : 1;
			}

			var asText = Texts.Format(bytes, ColumnType.Number, other, out var textCellType);
			var german = German.Format(Encoding.UTF8.GetBytes(field.Replace('.', ',')), ColumnType.Number, other, out var germanCellType);
			var problems = new List<string>();
			if ((cellType == typeof(double)) != expected)
			{
				problems.Add($"a double: {cellType == typeof(double)}");
			}

			if ((asText >= 0) != (significant <= 15 || expected) || (asText >= 0 && textCellType != cellType))
			{
				problems.Add($"with long numbers as text, {asText} as {textCellType.Name}");
			}

			if (german != length || germanCellType != cellType)
			{
				problems.Add($"in de-DE, {german} as {germanCellType.Name}");
			}

			if (problems.Count > 0)
			{
				mismatches++;
				if (failures.Count < 25)
				{
					failures.Add($"{field} ({Encoding.ASCII.GetString(text)}): the text path says {(expected ? "a double" : "not a double")}{(asked ? "" : ", not asked")}; {string.Join("; ", problems)}");
				}
			}
		}

		/// <summary>How many fields were checked, and how many of them were asked about with each answer.</summary>
		public string Summary => $"{fields} fields: {doubles + notDoubles} asked about, {doubles} a double's own text and {notDoubles} not; {mismatches} mismatches.";

		/// <summary>Fails with the first mismatches, if there were any.</summary>
		public void AssertNoFailures()
		{
			Assert.True(mismatches == 0, $"{mismatches} mismatches of {fields} fields (the first {failures.Count}):\n" + string.Join("\n", failures));
		}

		/// <summary>Fails unless both answers were given often enough to tell.</summary>
		/// <param name="least">The fewest of each.</param>
		public void AssertBothAnswers(int least)
		{
			Assert.True(doubles >= least && notDoubles >= least, $"Doubles' texts {doubles}, others {notDoubles}, of {fields} fields: fewer than {least}.");
		}
	}
}
