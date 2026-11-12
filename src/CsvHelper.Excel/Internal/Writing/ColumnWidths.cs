using System;

namespace CsvHelper.Excel;

/// <summary>
/// The width a column needs for a cell's text in Calibri 11, so that Excel doesn't show a typed cell as <c>########</c>,
/// and auto-fit's widths: the text's pixels at 96 DPI from a table of Calibri 11's characters (regular and bold), and
/// Excel's padding, in the units of a <c>col</c>'s <c>width</c> (the width of a digit, 7 pixels at 96 DPI).
/// </summary>
/// <remarks>
/// <para>
/// The table was measured through Excel's COM on 2026-11-06 (Excel 365, at 144 DPI): the auto-fit width of text cells of
/// 10 and of 30 copies of each character, whose difference is 20 copies' width without the padding. Excel lays out text
/// at fractional widths, so a width at 96 DPI is two thirds of one at 144 DPI; a digit, for one, is 7.5 pixels. Bold
/// Calibri 11 was measured the same way on 2026-11-08: it has regular's widths but for 20 characters. A line feed, which
/// Excel shows on the same line in a cell that doesn't wrap, and a tab were measured on 2026-11-09.
/// </para>
/// <para>
/// The widths are for 96 DPI, where they need the most room: a column's unit is the width of a digit rounded to whole
/// pixels, 7 at 96 DPI where a digit is 7.5, and 11 at 144 DPI where it is 11.25. So a width that fits at 96 DPI fits at
/// 120 and 144 DPI too, and is a little wider there than Excel's own auto-fit (18.7 against 17.8 for a date and time in
/// seconds, at 144 DPI).
/// </para>
/// </remarks>
internal static class ColumnWidths
{
	/// <summary>The pixels of a column's padding, which Excel adds to its text's, at 96 DPI.</summary>
	private const int Padding = 5;

	/// <summary>The pixels of a digit, a column's unit, at 96 DPI.</summary>
	private const int DigitPixels = 7;

	/// <summary>The pixels of the default column, 8 digits and the padding, at 96 DPI.</summary>
	private const int DefaultPixels = (8 * DigitPixels) + Padding;

	/// <summary>The pixels of a line feed in a cell that doesn't wrap, at 96 DPI: 6 points at 144 DPI.</summary>
	private const double LineFeedPixels = 8.0;

	/// <summary>The pixels of a tab, at 96 DPI: 5.5 points at 144 DPI.</summary>
	private const double TabPixels = 7.333;

	/// <summary>
	/// The pixels a header's filter button adds to its column, at 96 DPI: Excel's AutoFit gives a header with one 12.5
	/// points more at 144 DPI (measured on 2026-11-08).
	/// </summary>
	public const double FilterButtonPixels = 17;

	/// <summary>The pixels of the characters from space to <c>~</c>, at 96 DPI.</summary>
	private static readonly double[] Ascii =
	[
		3.367, 4.733, 6.133, 7.500, 7.500, 10.900, 10.233, 3.367, // space ! " # $ % & '
		4.733, 4.733, 7.500, 7.500, 3.367, 4.733, 4.067, 6.133, // ( ) * + , - . /
		7.500, 7.500, 7.500, 7.500, 7.500, 7.500, 7.500, 7.500, // 0 1 2 3 4 5 6 7
		7.500, 7.500, 4.067, 4.067, 7.500, 7.500, 7.500, 6.800, // 8 9 : ; < = > ?
		13.633, 8.867, 8.200, 8.200, 9.533, 7.500, 6.800, 9.533, // @ A B C D E F G
		9.533, 4.067, 4.733, 7.500, 6.133, 12.933, 9.533, 10.233, // H I J K L M N O
		7.500, 10.233, 8.200, 6.800, 7.500, 9.533, 8.200, 13.633, // P Q R S T U V W
		7.500, 7.500, 6.800, 4.733, 6.133, 4.733, 7.500, 7.500, // X Y Z [ \ ] ^ _
		4.067, 7.500, 8.200, 6.133, 8.200, 7.500, 4.733, 6.800, // ` a b c d e f g
		8.200, 3.367, 3.367, 6.800, 3.367, 12.267, 8.200, 8.200, // h i j k l m n o
		8.200, 8.200, 5.433, 6.133, 4.733, 8.200, 6.800, 10.900, // p q r s t u v w
		6.800, 6.800, 6.133, 4.733, 6.800, 4.733, 7.500, // x y z { | } ~
	];

	/// <summary>
	/// The width of the default column, 8.43 characters of Calibri 11 and the padding (64 pixels at 96 DPI), as Excel
	/// writes it: for a <c>col</c> with a style but no width of its own, since Excel hides a <c>col</c> without one.
	/// </summary>
	public const double Default = 9.140625;

	/// <summary>
	/// The widest column written: 255, the most the Open XML schema's <c>width</c> takes, about 254.29 characters. Excel's
	/// own widest, 255 characters (<see cref="ExcelColumnOptions.MaxWidth"/>), is wider as stored: Excel wrote 255.6328125
	/// for it at 144 DPI, and <see cref="FromCharacters"/> gives 255.7109375 at 96, which the schema refuses (step 7's
	/// fuzzer, through the validator; <c>Csv\_bench\results\phase5\step7\widths.txt</c>).
	/// </summary>
	public const double Widest = 255;

	/// <summary>The pixels of a date, <c>yyyy-mm-dd</c>.</summary>
	public static readonly double DatePixels = TextPixels("2024-12-31".AsSpan());

	/// <summary>The pixels of a date and time in seconds, <c>yyyy-mm-dd hh:mm:ss</c>.</summary>
	public static readonly double DateTimePixels = TextPixels("2024-12-31 23:59:59".AsSpan());

	/// <summary>The pixels of a date and time with milliseconds, <c>yyyy-mm-dd hh:mm:ss.000</c>.</summary>
	public static readonly double DateTimeMillisecondsPixels = TextPixels("2024-12-31 23:59:59.999".AsSpan());

	/// <summary>The pixels of a time of day in seconds, <c>hh:mm:ss</c>.</summary>
	public static readonly double TimePixels = TextPixels("23:59:59".AsSpan());

	/// <summary>The pixels of a time of day with milliseconds, <c>hh:mm:ss.000</c>.</summary>
	public static readonly double TimeMillisecondsPixels = TextPixels("23:59:59.999".AsSpan());

	/// <summary>The width of a date, <c>yyyy-mm-dd</c>.</summary>
	public static readonly double Date = Of("2024-12-31");

	/// <summary>The width of a date and time in seconds, <c>yyyy-mm-dd hh:mm:ss</c>.</summary>
	public static readonly double DateTime = Of("2024-12-31 23:59:59");

	/// <summary>The width of a date and time with milliseconds, <c>yyyy-mm-dd hh:mm:ss.000</c>.</summary>
	public static readonly double DateTimeMilliseconds = Of("2024-12-31 23:59:59.999");

	/// <summary>The width of a time of day in seconds, <c>hh:mm:ss</c>: 0, since it fits the default column.</summary>
	public static readonly double Time = Of("23:59:59");

	/// <summary>The width of a time of day with milliseconds, <c>hh:mm:ss.000</c>.</summary>
	public static readonly double TimeMilliseconds = Of("23:59:59.999");

	/// <summary>The width a column needs for a typed cell's text, when that is wider than the default column.</summary>
	/// <param name="text">The text as Excel shows it.</param>
	/// <returns>
	/// The width, in whole 256ths of a digit, as Excel stores widths; or 0 when the text fits the default column, which
	/// then needs none.
	/// </returns>
	public static double Of(ReadOnlySpan<char> text) => OfPixels(TextPixels(text));

	/// <summary>The width a column needs for a typed cell's text of some pixels, when that is wider than the default column.</summary>
	/// <param name="textPixels">The text's pixels at 96 DPI (<see cref="TextPixels"/>), without the padding.</param>
	/// <returns>The width, in whole 256ths of a digit; or 0 when the text fits the default column.</returns>
	public static double OfPixels(double textPixels)
	{
		var pixels = (int)Math.Ceiling(textPixels) + Padding;
		if (pixels <= DefaultPixels)
		{
			return 0;
		}

		return Math.Ceiling(pixels * 256.0 / DigitPixels) / 256;
	}

	/// <summary>
	/// The width auto-fit gives a column for its widest text, as Excel's AutoFit does: the text's pixels and the padding,
	/// in whole 256ths of a digit, narrower than the default column when the text is.
	/// </summary>
	/// <param name="textPixels">The text's pixels at 96 DPI, without the padding.</param>
	/// <returns>The width.</returns>
	public static double Fit(double textPixels)
	{
		var pixels = (int)Math.Ceiling(textPixels) + Padding;

		return Math.Ceiling(pixels * 256.0 / DigitPixels) / 256;
	}

	/// <summary>
	/// The pixels of a text on one line at 96 DPI, without the padding: a line feed takes the room Excel gives it in a cell
	/// that doesn't wrap, and a carriage return none.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <param name="bold">Whether it is in bold Calibri 11, rather than regular.</param>
	/// <returns>The pixels.</returns>
	public static double TextPixels(ReadOnlySpan<char> text, bool bold = false)
	{
		var sum = 0.0;
		if (bold)
		{
			foreach (var c in text)
			{
				sum += BoldPixels(c);
			}
		}
		else
		{
			foreach (var c in text)
			{
				sum += Pixels(c);
			}
		}

		return sum;
	}

	/// <summary>A character's pixels at 96 DPI, in regular or bold Calibri 11.</summary>
	/// <param name="c">The character.</param>
	/// <param name="bold">Whether it is bold.</param>
	/// <returns>Its pixels.</returns>
	public static double CharPixels(char c, bool bold) => bold ? BoldPixels(c) : Pixels(c);

	/// <summary>
	/// The width of a column of a number of characters (Excel's <c>ColumnWidth</c>), as Excel writes it at 96 DPI: the
	/// characters' pixels and the padding, in whole 256ths of a digit. Below one character, the padding shrinks with the
	/// width, as Excel's does.
	/// </summary>
	/// <param name="characters">The characters, 0 to 255.</param>
	/// <returns>The width: 10.7109375 for 10 characters, 0 for 0.</returns>
	public static double FromCharacters(double characters)
	{
		var pixels = characters < 1 ? characters * (DigitPixels + Padding) : (characters * DigitPixels) + Padding;

		return Math.Truncate(pixels * 256 / DigitPixels) / 256;
	}

	/// <summary>
	/// A character's pixels in Calibri 11 at 96 DPI: measured for ASCII, the characters of numbers and dates in the common
	/// cultures, line feeds and tabs; for the others, the width of East Asian characters (which Excel shows in another
	/// font), or of <c>W</c>, the widest letter.
	/// </summary>
	/// <param name="c">The character.</param>
	/// <returns>Its pixels.</returns>
	private static double Pixels(char c)
	{
		if (c is >= ' ' and <= '~')
		{
			return Ascii[c - ' '];
		}

		return c switch
		{
			'\n' => LineFeedPixels,
			'\t' => TabPixels,
			'\r' => 0,
			// Thin space, alef.
			(char)0x2009 or (char)0x0627 => 2.667,
			// No-break space, narrow no-break space (fr-FR's group separator), right single quotation mark.
			(char)0x00A0 or (char)0x202F or (char)0x2019 => 3.367,
			// Degree sign.
			(char)0x00B0 => 4.733,
			// c with cedilla.
			(char)0x00E7 => 6.133,
			// Euro, pound, yen, currency and section signs, minus sign, é, è, à, ä, É, Cyrillic а.
			(char)0x20AC or (char)0x00A3 or (char)0x00A5 or (char)0x00A4 or (char)0x00A7 or (char)0x2212 or (char)0x00E9 or (char)0x00E8 or
				(char)0x00E0 or (char)0x00E4 or (char)0x00C9 or (char)0x0430 => 7.500,
			// Micro sign, ö, ü, ß, ñ, Greek alpha, Hebrew alef.
			(char)0x00B5 or (char)0x00F6 or (char)0x00FC or (char)0x00DF or (char)0x00F1 or (char)0x03B1 or (char)0x05D0 => 8.200,
			// Å, Thai ko kai.
			(char)0x00C5 or (char)0x0E01 => 8.867,
			// Ü.
			(char)0x00DC => 9.533,
			// Ö, Cyrillic zhe, Greek omega.
			(char)0x00D6 or (char)0x0436 or (char)0x03A9 => 10.233,
			// Cyrillic capital zhe.
			(char)0x0416 => 12.267,
			// Per mille sign.
			(char)0x2030 => 15.667,
			// The second half of a surrogate pair, which the first counts for.
			_ when char.IsLowSurrogate(c) => 0,
			_ when IsWide(c) => 15.000,
			_ => 13.633,
		};
	}

	/// <summary>
	/// A character's pixels in bold Calibri 11 at 96 DPI: regular's, but for the 20 characters measured wider (each by about
	/// 0.7 pixels, the Thai ko kai by 1.4), and East Asian characters, which are taken to be as wide as the bold year sign
	/// (U+5E74) measured.
	/// </summary>
	/// <param name="c">The character.</param>
	/// <returns>Its pixels.</returns>
	private static double BoldPixels(char c) => c switch
	{
		'"' => 6.800,
		// Comma, j, right single quotation mark.
		',' or 'j' or (char)0x2019 => 4.067,
		'&' => 10.900,
		'K' or 'P' or 'X' => 8.200,
		'V' => 8.867,
		'Z' or 'k' => 7.500,
		'`' => 4.733,
		// t, braces, degree sign.
		't' or '{' or '}' or (char)0x00B0 => 5.433,
		// Greek alpha.
		(char)0x03B1 => 8.867,
		// Thai ko kai.
		(char)0x0E01 => 10.233,
		// Cyrillic zhe.
		(char)0x0436 => 10.900,
		_ when IsWide(c) => 15.667,
		_ => Pixels(c),
	};

	/// <summary>
	/// Whether a character is East Asian and full width, as the year, month and day of Japanese, Chinese and Korean dates
	/// are (all measured 15 pixels), or the first half of a surrogate pair, which is taken to be as wide.
	/// </summary>
	/// <param name="c">The character.</param>
	/// <returns>Whether it is.</returns>
	private static bool IsWide(char c) =>
		c is >= (char)0x1100 and <= (char)0x115F or >= (char)0x2E80 and <= (char)0xA4CF or >= (char)0xAC00 and <= (char)0xD7A3 or
			>= (char)0xF900 and <= (char)0xFAFF or >= (char)0xFE30 and <= (char)0xFE4F or >= (char)0xFF00 and <= (char)0xFF60 or
			>= (char)0xFFE0 and <= (char)0xFFE6 || char.IsHighSurrogate(c);
}
