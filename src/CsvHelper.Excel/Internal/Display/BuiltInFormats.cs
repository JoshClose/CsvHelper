using System;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// The codes of the built-in number format ids, which a style names without defining (Phase 8's decision 7): Excel en-US's
/// for every id, as Excel 365 reported each through COM on 2026-11-16 (<c>NumberFormat</c> of a cell in a style of the id;
/// <c>Csv\_bench\results\phase8\design</c>), but for the ids that follow the reader's culture.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>
/// 0 to 22 and 37 to 49 are Excel en-US's (<c>m/d/yyyy</c>, <c>$#,##0_);($#,##0)</c>, <c>mm:ss.0</c> for 47); 23 to 26,
/// and 82 to 163, are General; 27 to 36 and 50 to 58 are <c>m/d/yyyy</c> or <c>h:mm:ss</c>; 59 to 81 repeat 1 to 22 and
/// 45 to 47 (59 to 62 are 1 to 4, 63 to 66 are 5 to 8, 67 and 68 are 9 and 10, 69 and 70 are 12 and 13, 71 and 72 are
/// 14, 73 to 75 are 15 to 17, 76 to 78 are 20 to 22, and 79 to 81 are 45 to 47).
/// </description></item>
/// <item><description>
/// In a culture other than the invariant one, 14 is its short date and 22 its short date and <c>h:mm</c>; and the currency
/// ids 5 to 8, 63 to 66 and 41 to 44 show its currency symbol where its patterns put it, and negatives in its negative
/// pattern. A pattern in parentheses keeps Excel en-US's shapes, a space (<c>_)</c>) after positive numbers to line them
/// up; any other pads the accounting formats with <c>_-</c>, as Excel's own European ones do
/// (<c>_-* #,##0 "€"_-;-* #,##0 "€"_-;_-* "-" "€"_-;_-@_-</c>). Only Excel en-US was checked: these follow ECMA-376, which
/// leaves the currency ids to the locale, and .NET's culture data.
/// </description></item>
/// </list>
/// </remarks>
internal static class BuiltInFormats
{
	/// <summary>General, the code of id 0 and of every id Excel en-US has no format for.</summary>
	public const string General = "General";

	/// <summary>The first custom number format id: the ids below it are built in.</summary>
	public const int FirstCustom = 164;

	/// <summary>.NET's positive currency patterns (<see cref="System.Globalization.NumberFormatInfo.CurrencyPositivePattern"/>): <c>$</c> the symbol, <c>n</c> the number.</summary>
	private static readonly string[] PositivePatterns = ["$n", "n$", "$ n", "n $"];

	/// <summary>.NET's negative currency patterns (<see cref="System.Globalization.NumberFormatInfo.CurrencyNegativePattern"/>).</summary>
	private static readonly string[] NegativePatterns =
	[
		"($n)", "-$n", "$-n", "$n-", "(n$)", "-n$", "n-$", "n$-", "-n $", "-$ n", "n $-", "$ n-", "$ -n", "n- $", "($ n)", "(n $)", "$- n",
	];

	/// <summary>Excel en-US's code of each built-in id from 0 to 81; null for General.</summary>
	private static readonly string?[] EnglishCodes =
	[
		General, "0", "0.00", "#,##0", "#,##0.00", "$#,##0_);($#,##0)", "$#,##0_);[Red]($#,##0)", "$#,##0.00_);($#,##0.00)",
		"$#,##0.00_);[Red]($#,##0.00)", "0%", "0.00%", "0.00E+00", "# ?/?", "# ??/??", "m/d/yyyy", "d-mmm-yy", "d-mmm", "mmm-yy",
		"h:mm AM/PM", "h:mm:ss AM/PM", "h:mm", "h:mm:ss", "m/d/yyyy h:mm", null, null, null, null, "m/d/yyyy", "m/d/yyyy",
		"m/d/yyyy", "m/d/yyyy", "m/d/yyyy", "h:mm:ss", "h:mm:ss", "h:mm:ss", "h:mm:ss", "m/d/yyyy", "#,##0_);(#,##0)",
		"#,##0_);[Red](#,##0)", "#,##0.00_);(#,##0.00)", "#,##0.00_);[Red](#,##0.00)", "_(* #,##0_);_(* (#,##0);_(* \"-\"_);_(@_)",
		"_($* #,##0_);_($* (#,##0);_($* \"-\"_);_(@_)", "_(* #,##0.00_);_(* (#,##0.00);_(* \"-\"??_);_(@_)",
		"_($* #,##0.00_);_($* (#,##0.00);_($* \"-\"??_);_(@_)", "mm:ss", "[h]:mm:ss", "mm:ss.0", "##0.0E+0", "@", "m/d/yyyy",
		"m/d/yyyy", "m/d/yyyy", "m/d/yyyy", "m/d/yyyy", "m/d/yyyy", "m/d/yyyy", "m/d/yyyy", "m/d/yyyy", "0", "0.00", "#,##0",
		"#,##0.00", "$#,##0_);($#,##0)", "$#,##0_);[Red]($#,##0)", "$#,##0.00_);($#,##0.00)", "$#,##0.00_);[Red]($#,##0.00)",
		"0%", "0.00%", "# ?/?", "# ??/??", "m/d/yyyy", "m/d/yyyy", "d-mmm-yy", "d-mmm", "mmm-yy", "h:mm", "h:mm:ss",
		"m/d/yyyy h:mm", "mm:ss", "[h]:mm:ss", "mm:ss.0",
	];

	/// <summary>Excel en-US's code of a built-in id.</summary>
	/// <param name="id">The id.</param>
	/// <returns>The code: General for an id Excel en-US has no format for.</returns>
	public static string English(int id) => (uint)id < (uint)EnglishCodes.Length ? EnglishCodes[id] ?? General : General;

	/// <summary>The codes of the built-in ids 0 to 163 in a culture.</summary>
	/// <param name="shortDate">The culture's short date as a code.</param>
	/// <param name="currency">The culture's currency, or <see langword="null"/> for Excel en-US's codes.</param>
	/// <returns>The codes, by id.</returns>
	public static string[] ForCulture(string shortDate, CurrencyPattern? currency)
	{
		var codes = new string[FirstCustom];
		for (var id = 0; id < codes.Length; id++)
		{
			codes[id] = English(id);
		}

		codes[14] = shortDate;
		codes[22] = shortDate + " h:mm";
		if (currency is null)
		{
			return codes;
		}

		for (var i = 0; i < 4; i++)
		{
			// 5 to 8 and 63 to 66: whole and two places, each in black and red.
			var number = i < 2 ? "#,##0" : "#,##0.00";
			var red = i % 2 == 1;
			codes[5 + i] = Currency(currency, number, red);
			codes[63 + i] = codes[5 + i];

			// 41 to 44: whole without and with the symbol, then two places without and with it.
			codes[41 + i] = Accounting(currency, i < 2 ? "#,##0" : "#,##0.00", i < 2 ? "\"-\"" : "\"-\"??", i % 2 == 1);
		}

		return codes;
	}

	/// <summary>A currency code: <c>5</c> to <c>8</c> in en-US are <c>$#,##0_);($#,##0)</c> and its kin.</summary>
	/// <param name="currency">The culture's currency.</param>
	/// <param name="number">The number's placeholders.</param>
	/// <param name="red">Whether negatives are red, as 6 and 8 have them; colors don't change the text.</param>
	/// <returns>The code.</returns>
	private static string Currency(CurrencyPattern currency, string number, bool red)
	{
		var negative = NegativePattern(currency);
		var positive = Expand(PositivePattern(currency), currency.Symbol, number) + (negative[0] == '(' ? "_)" : "");

		return positive + ";" + (red ? "[Red]" : "") + Expand(negative, currency.Symbol, number);
	}

	/// <summary>An accounting code: <c>41</c> to <c>44</c>, with a fill before the number and a space at each end.</summary>
	/// <param name="currency">The culture's currency.</param>
	/// <param name="number">The number's placeholders.</param>
	/// <param name="zero">What zero shows: a dash, and spaces for the decimals.</param>
	/// <param name="symbol">Whether it shows the symbol, as 42 and 44 do.</param>
	/// <returns>The code.</returns>
	private static string Accounting(CurrencyPattern currency, string number, string zero, bool symbol)
	{
		var negative = NegativePattern(currency);
		var positive = PositivePattern(currency);
		if (!symbol)
		{
			negative = WithoutSymbol(negative);
			positive = WithoutSymbol(positive);
		}

		var shown = symbol ? currency.Symbol : "";
		if (negative[0] == '(')
		{
			// Excel en-US's shape: the symbol outside the parentheses, which take the place of the space after.
			var at = positive.IndexOf('n');
			var dollar = positive.IndexOf('$');
			var before = dollar >= 0 && dollar < at ? Expand(positive.Substring(0, at), shown, "") : "";
			var after = dollar > at ? Expand(positive.Substring(at + 1), shown, "") : "";

			return $"_({before}* {number}{after}_);_({before}* ({number}){after};_({before}* {zero}{after}_);_(@_)";
		}

		var negativeText = Expand(negative, shown, "* " + number);
		var negativeSection = negative[0] == '-' ? negativeText + "_-" : "_-" + negativeText + "_-";

		return $"_-{Expand(positive, shown, "* " + number)}_-;{negativeSection};_-{Expand(positive, shown, "* " + zero)}_-;_-@_-";
	}

	/// <summary>A currency's positive pattern, or the first for one .NET doesn't define.</summary>
	/// <param name="currency">The currency.</param>
	/// <returns>The pattern.</returns>
	private static string PositivePattern(CurrencyPattern currency) =>
		(uint)currency.Positive < (uint)PositivePatterns.Length ? PositivePatterns[currency.Positive] : PositivePatterns[0];

	/// <summary>A currency's negative pattern, or the first for one .NET doesn't define.</summary>
	/// <param name="currency">The currency.</param>
	/// <returns>The pattern.</returns>
	private static string NegativePattern(CurrencyPattern currency) =>
		(uint)currency.Negative < (uint)NegativePatterns.Length ? NegativePatterns[currency.Negative] : NegativePatterns[0];

	/// <summary>A pattern without its symbol, and the space beside it.</summary>
	/// <param name="pattern">The pattern: <c>n $</c>.</param>
	/// <returns>The pattern: <c>n</c>.</returns>
	private static string WithoutSymbol(string pattern) => pattern.Replace(" $", "").Replace("$ ", "").Replace("$", "");

	/// <summary>A pattern as a code: the symbol in quotes, the number's placeholders, and the rest as itself.</summary>
	/// <param name="pattern">The pattern: <c>-n $</c>.</param>
	/// <param name="symbol">The symbol: <c>€</c>.</param>
	/// <param name="number">The placeholders: <c>#,##0</c>.</param>
	/// <returns>The code: <c>-#,##0 "€"</c>.</returns>
	private static string Expand(string pattern, string symbol, string number)
	{
		var code = new StringBuilder();
		foreach (var c in pattern)
		{
			if (c == '$')
			{
				code.Append(Quote(symbol));
			}
			else if (c == 'n')
			{
				code.Append(number);
			}
			else
			{
				code.Append(c);
			}
		}

		return code.ToString();
	}

	/// <summary>Text in quotes for a code, a quote in it escaped: nothing for empty text.</summary>
	/// <param name="text">The text.</param>
	/// <returns>The code's text.</returns>
	private static string Quote(string text) => text.Length == 0 ? "" : "\"" + text.Replace("\"", "\"\\\"\"") + "\"";
}

/// <summary>A culture's currency: its symbol, and where its patterns put it and a negative number's sign.</summary>
/// <param name="Symbol">The symbol: <c>€</c>.</param>
/// <param name="Positive">.NET's positive pattern, 0 to 3.</param>
/// <param name="Negative">.NET's negative pattern, 0 to 16.</param>
internal sealed record CurrencyPattern(string Symbol, int Positive, int Negative);
