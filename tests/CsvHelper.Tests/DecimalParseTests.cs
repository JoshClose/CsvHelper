using System.Globalization;
using System.Text;

namespace CsvHelper.Tests;

/// <summary>
/// Decimals must parse to exactly what <c>decimal.Parse</c> gives (the same bits: value, scale and sign) with any
/// format provider. The fast path (<c>CsvParse.TryDecimalFast</c>) must take every plain number it is meant for, so it
/// is not silently left to the BCL, and leave everything else to the BCL.
/// </summary>
public class DecimalParseTests
{
	private const NumberStyles Styles = NumberStyles.Number | NumberStyles.AllowExponent;

	private static readonly string[] EdgeCases =
	[
		// zeros, signs and scales
		"0", "-0", "00", "0.0", "-0.0", "0.00", "-0.000", "000.000", ".0", "-.0", "0.", "-0.", "12.50", "12.5", "1.0",
		"100", "100.00", "-100.10", ".5", "-.5", "5.", "-5.", "007", "00.10", "-00.001", "123456.789",
		// the most digits the fast path takes, and one more
		"9999999999999999999", "-9999999999999999999", "99999999999999999999", "18446744073709551615",
		"18446744073709551616", "999999999999999999.9", "9999999999999999999.9", ".9999999999999999999",
		"0.0000000000000000001", ".0000000000000000001", "0.000000000000000000000000000001", "1844674407370955161.5",
		"79228162514264337593543950335", "79228162514264337593543950336", "-79228162514264337593543950335",
		"0.00000000000000000000000000000", "1.00000000000000000000000000000",
		// not plain: left to the BCL, which parses some and rejects others
		"", "-", ".", "-.", "--1", "-+1", "+1", "+.5", "1-", " 1", "1 ", "\t1", " .5", "1,234.5", "1,2,3", "1.2.3", "1..2",
		"1e5", "1E-5", "-1.5e+3", "1e", "(1)", "$1", "0x10", "1\0", "\0", "1.\0", "\u0661\u0662", "1\u00a0", "12.50 ",
		"1.234,5", "1,5", ",5", "-,5", "5,", "1\u202f234,5",
	];

	private static readonly IFormatProvider[] Providers =
	[
		CultureInfo.InvariantCulture,
		CultureInfo.GetCultureInfo("en-US"),
		CultureInfo.GetCultureInfo("de-DE"),
		CultureInfo.GetCultureInfo("fr-FR"),
		new NumberFormatInfo { NegativeSign = "~" },
		new NumberFormatInfo { NegativeSign = "\u2212" },
		new NumberFormatInfo { NumberDecimalSeparator = "::" },
		new NumberFormatInfo { NumberDecimalSeparator = " " },
		new NumberFormatInfo { NumberDecimalSeparator = "+" },
		new NumberFormatInfo { NumberDecimalSeparator = ",", NumberGroupSeparator = "." },
		new NumberFormatInfo { NumberDecimalSeparator = ",", PositiveSign = "," },
	];

	[Fact]
	public void EdgeCasesParseAsTheBclParsesThem()
	{
		var failures = new List<string>();
		foreach (var provider in Providers)
		{
			foreach (var text in EdgeCases)
			{
				Check(text, provider, failures, exceptions: true);
			}
		}

		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	public void RandomNumbersParseAsTheBclParsesThem(int seed)
	{
		var rng = new Random(seed);
		var failures = new List<string>();
		var text = new StringBuilder();
		foreach (var provider in new IFormatProvider[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("de-DE") })
		{
			var point = NumberFormatInfo.GetInstance(provider).NumberDecimalSeparator;
			for (var i = 0; i < 200_000 && failures.Count < 20; i++)
			{
				text.Clear();
				RandomNumber(rng, text, point);
				if (rng.Next(3) == 0)
				{
					Mutate(rng, text);
				}

				Check(text.ToString(), provider, failures, exceptions: false);
			}
		}

		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void PlainNumbersTakeTheFastPath()
	{
		// An optional -, then 1 to 19 digits with an optional point among or after them: always parsed directly.
		var rng = new Random(3);
		var text = new StringBuilder();
		foreach (var provider in new IFormatProvider[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("de-DE"), CultureInfo.GetCultureInfo("en-US") })
		{
			var info = NumberFormatInfo.GetInstance(provider);
			var point = CsvParse.DecimalPoint(provider);
			Assert.Equal((byte)info.NumberDecimalSeparator[0], point);
			for (var i = 0; i < 100_000; i++)
			{
				text.Clear();
				RandomNumber(rng, text, info.NumberDecimalSeparator, maxDigits: 19);
				var utf8 = Encoding.UTF8.GetBytes(text.ToString());
				var fast = CsvParse.TryDecimalFast(utf8, point, out _);
#if !NET
				// Zeros are left to the BCL on the builds that may run on .NET Framework.
				if (!fast && decimal.Parse(text.ToString(), Styles, provider) == 0m)
				{
					continue;
				}
#endif
				Assert.True(fast, $"'{text}' was not parsed directly with {Name(provider)}.");
			}
		}

		Assert.Equal(0, CsvParse.DecimalPoint(new NumberFormatInfo { NegativeSign = "\u2212" }));
		Assert.Equal(0, CsvParse.DecimalPoint(new NumberFormatInfo { NumberDecimalSeparator = " " }));
		Assert.False(CsvParse.TryDecimalFast("1"u8, 0, out _));
		Assert.False(CsvParse.TryDecimalFast("1\0"u8, 0, out _));
	}

	[Fact]
	public void ReaderKeepsTheScaleAndSign()
	{
		using var reader = CsvReader.FromText("12.50,-0,0.00,-1.000\n", new CsvReaderOptions { HasHeader = false });
		Assert.True(reader.Read());
		for (var i = 0; i < 4; i++)
		{
			var text = reader.GetString(i);
			var expected = decimal.Parse(text, Styles, CultureInfo.InvariantCulture);
			Assert.Equal(decimal.GetBits(expected), decimal.GetBits(reader.Parse<decimal>(i)));
			Assert.Equal(expected.ToString(CultureInfo.InvariantCulture), reader.Parse<decimal>(i).ToString(CultureInfo.InvariantCulture));
			Assert.True(reader.TryParse<decimal>(i, out var value));
			Assert.Equal(decimal.GetBits(expected), decimal.GetBits(value));
		}
	}

	/// <summary>
	/// Checks that <c>CsvParse.TryDecimal</c> (and, when <paramref name="exceptions"/> is set or the text is valid,
	/// <c>CsvParse.Decimal</c>) agree with the BCL on <paramref name="text"/>: the same bits, or the same failure.
	/// </summary>
	private static void Check(string text, IFormatProvider provider, List<string> failures, bool exceptions)
	{
		var utf8 = Encoding.UTF8.GetBytes(text);
		var expectedOk = decimal.TryParse(text, Styles, provider, out var expected);
		var ok = CsvParse.TryDecimal(utf8, provider, out var actual);
		if (ok != expectedOk || (ok && !SameBits(expected, actual)))
		{
			failures.Add($"TryDecimal('{Escape(text)}', {Name(provider)}): {Show(ok, actual)}, the BCL {Show(expectedOk, expected)}");

			return;
		}

		if (!expectedOk && !exceptions)
		{
			return;
		}

		var thrown = Catch(() => CsvParse.Decimal(utf8, provider, hide: false), out var parsed);
		var expectedThrown = Catch(() => decimal.Parse(text, Styles, provider), out _);
		if (thrown?.GetType() != expectedThrown?.GetType() || (thrown is null && !SameBits(expected, parsed)))
		{
			failures.Add($"Decimal('{Escape(text)}', {Name(provider)}): {thrown?.GetType().Name ?? Show(true, parsed)}, the BCL {expectedThrown?.GetType().Name ?? Show(true, expected)}");
		}
	}

	private static Exception? Catch(Func<decimal> parse, out decimal value)
	{
		try
		{
			value = parse();

			return null;
		}
		catch (Exception e)
		{
			value = 0;

			return e;
		}
	}

	private static bool SameBits(decimal a, decimal b) => decimal.GetBits(a).SequenceEqual(decimal.GetBits(b));

	private static string Show(bool ok, decimal value) =>
		ok ? $"{value.ToString(CultureInfo.InvariantCulture)} [{string.Join(", ", decimal.GetBits(value).Select(b => b.ToString("X8")))}]" : "no";

	private static string Escape(string text) => string.Concat(text.Select(c => c is < ' ' or > '~' ? $"\\u{(int)c:X4}" : c.ToString()));

	private static string Name(IFormatProvider provider) => provider is CultureInfo culture
		? culture.Name.Length == 0 ? "invariant" : culture.Name
		: $"NumberFormatInfo('{Escape(((NumberFormatInfo)provider).NumberDecimalSeparator)}', '{Escape(((NumberFormatInfo)provider).NegativeSign)}')";

	/// <summary>
	/// A random plain number: an optional <c>-</c>, digits (sometimes mostly zeros or nines, with leading or trailing
	/// zeros) and an optional point among or after them.
	/// </summary>
	private static void RandomNumber(Random rng, StringBuilder text, string point, int maxDigits = 24)
	{
		if (rng.Next(3) == 0)
		{
			text.Append('-');
		}

		var digits = rng.Next(1, maxDigits + 1);
		var at = rng.Next(-digits, digits + 1); // negative: no point
		var mode = rng.Next(5);
		for (var k = 0; k < digits; k++)
		{
			if (k == at)
			{
				text.Append(point);
			}

			var d = mode switch
			{
				0 => rng.Next(4) == 0 ? rng.Next(10) : 0,
				1 => rng.Next(4) == 0 ? rng.Next(10) : 9,
				2 => k == 0 || k == digits - 1 ? 0 : rng.Next(10),
				_ => rng.Next(10),
			};
			text.Append((char)('0' + d));
		}

		if (at == digits)
		{
			text.Append(point);
		}
	}

	/// <summary>Inserts, replaces or deletes one random character.</summary>
	private static void Mutate(Random rng, StringBuilder text)
	{
		const string characters = " ,.-+eE0159\0x\t";
		var at = rng.Next(text.Length + 1);
		var c = characters[rng.Next(characters.Length)];
		switch (rng.Next(3))
		{
			case 0:
				text.Insert(at, c);
				break;
			case 1 when at < text.Length:
				text[at] = c;
				break;
			case 2 when at < text.Length:
				text.Remove(at, 1);
				break;
		}
	}
}
