using System;
using System.Globalization;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// What the reader's culture gives the text a cell shows (Phase 8's decisions 6 and 7): the decimal and group separators,
/// the names of months and days and the designators where no locale code names a language, the short date, long date and
/// long time as format codes (the built-in ids 14 and 22, and the system formats <c>[$-F800]</c> and <c>[$-F400]</c>),
/// and the currency of the currency ids.
/// </summary>
/// <remarks>
/// <para>
/// The invariant culture, the reader's default, is Excel en-US exactly: <c>.</c> and <c>,</c>, English names, <c>AM</c>
/// and <c>PM</c>, <c>m/d/yyyy</c>, <c>dddd, mmmm d, yyyy</c>, <c>h:mm:ss AM/PM</c> and <c>$</c>. .NET's own invariant data
/// would give <c>MM/dd/yyyy</c>, <c>dddd, dd MMMM yyyy</c>, 24-hour times and <c>¤</c>.
/// </para>
/// <para>
/// Another culture's patterns become codes token by token: <c>d</c>, <c>M</c> as <c>m</c>, <c>y</c> (two digits for one
/// or two letters, four otherwise), <c>h</c> and <c>H</c> as <c>h</c>, <c>m</c>, <c>s</c>, and <c>t</c> and <c>tt</c> as
/// <c>A/P</c> and <c>AM/PM</c>; the date and time separators that <c>/</c> and <c>:</c> stand for as text, and any other
/// text as itself. Eras, offsets and fractions of a second, which no culture's date and time patterns probed have, are
/// left out.
/// </para>
/// </remarks>
internal sealed class DisplayCulture
{
	/// <summary>Excel en-US's short date, the built-in id 14.</summary>
	private const string EnglishShortDate = "m/d/yyyy";

	/// <summary>Excel en-US's long date, <c>[$-F800]</c>.</summary>
	private const string EnglishLongDate = "dddd, mmmm d, yyyy";

	/// <summary>Excel en-US's long time, <c>[$-F400]</c>.</summary>
	private const string EnglishLongTime = "h:mm:ss AM/PM";

	/// <summary>The characters a code shows as themselves outside quotes, which a pattern's text needn't quote.</summary>
	private const string Unquoted = " /:-,()";

	private readonly string[] builtIns;
	private FormatCode? longDateFormat;
	private FormatCode? longTimeFormat;

	/// <summary>Creates the culture's data.</summary>
	/// <param name="decimalSeparator">The decimal separator.</param>
	/// <param name="groupSeparator">The group separator.</param>
	/// <param name="names">The names where no locale code names a language.</param>
	/// <param name="shortDate">The short date as a code.</param>
	/// <param name="longDate">The long date as a code.</param>
	/// <param name="longTime">The long time as a code.</param>
	/// <param name="currency">The currency, or <see langword="null"/> for Excel en-US's.</param>
	private DisplayCulture(string decimalSeparator, string groupSeparator, DateNames names, string shortDate, string longDate, string longTime, CurrencyPattern? currency)
	{
		DecimalSeparator = Encoding.UTF8.GetBytes(decimalSeparator);
		GroupSeparator = Encoding.UTF8.GetBytes(groupSeparator);
		Names = names;
		ShortDate = shortDate;
		LongDate = longDate;
		LongTime = longTime;
		Currency = currency;
		builtIns = BuiltInFormats.ForCulture(shortDate, currency);
	}

	/// <summary>Excel en-US: the invariant culture's display.</summary>
	public static DisplayCulture Invariant { get; } = new(".", ",", DateNames.English, EnglishShortDate, EnglishLongDate, EnglishLongTime, null);

	/// <summary>The decimal separator, in UTF-8.</summary>
	public byte[] DecimalSeparator { get; }

	/// <summary>The group separator, in UTF-8.</summary>
	public byte[] GroupSeparator { get; }

	/// <summary>The names of months and days, and the designators, where no locale code names a language.</summary>
	public DateNames Names { get; }

	/// <summary>The short date as a code: the built-in id 14, and 22 before its time.</summary>
	public string ShortDate { get; }

	/// <summary>The long date as a code: <c>[$-F800]</c>.</summary>
	public string LongDate { get; }

	/// <summary>The long time as a code: <c>[$-F400]</c>.</summary>
	public string LongTime { get; }

	/// <summary>The currency of the currency ids: <see langword="null"/> for Excel en-US's own codes.</summary>
	public CurrencyPattern? Currency { get; }

	/// <summary>Whether this is Excel en-US, the invariant culture's display.</summary>
	public bool IsInvariant => ReferenceEquals(this, Invariant);

	/// <summary>The long date, compiled: what a section of <c>[$-F800]</c> shows.</summary>
	public FormatCode LongDateFormat => longDateFormat ??= FormatCode.Compile(LongDate);

	/// <summary>The long time, compiled: what a section of <c>[$-F400]</c> shows.</summary>
	public FormatCode LongTimeFormat => longTimeFormat ??= FormatCode.Compile(LongTime);

	/// <summary>A provider's display.</summary>
	/// <param name="provider">The reader's provider: <see langword="null"/> and the invariant culture give <see cref="Invariant"/>.</param>
	/// <returns>The display.</returns>
	public static DisplayCulture For(IFormatProvider? provider)
	{
		if (provider is null || provider is CultureInfo { Name.Length: 0 } || ReferenceEquals(provider, NumberFormatInfo.InvariantInfo) ||
			ReferenceEquals(provider, DateTimeFormatInfo.InvariantInfo))
		{
			return Invariant;
		}

		var numbers = NumberFormatInfo.GetInstance(provider);
		var dates = DateTimeFormatInfo.GetInstance(provider);
		var currency = new CurrencyPattern(numbers.CurrencySymbol, numbers.CurrencyPositivePattern, numbers.CurrencyNegativePattern);

		return new DisplayCulture(numbers.NumberDecimalSeparator, numbers.NumberGroupSeparator, DateNames.Of(dates), Code(dates.ShortDatePattern, dates),
			Code(dates.LongDatePattern, dates), Code(dates.LongTimePattern, dates), currency);
	}

	/// <summary>The code of a built-in number format id, which a style names without defining.</summary>
	/// <param name="id">The id: 0 to 163 are built in; any other is General.</param>
	/// <returns>The code: Excel en-US's, or the culture's for the ids that follow it.</returns>
	public string BuiltIn(int id) => (uint)id < (uint)builtIns.Length ? builtIns[id] : BuiltInFormats.General;

	/// <summary>A .NET date or time pattern as an Excel format code that shows the same text.</summary>
	/// <param name="pattern">The pattern: <c>dd.MM.yyyy</c>.</param>
	/// <param name="info">The culture's data, for the separators <c>/</c> and <c>:</c> stand for.</param>
	/// <returns>The code: <c>dd"."mm"."yyyy</c>.</returns>
	public static string Code(string pattern, DateTimeFormatInfo info)
	{
		var code = new StringBuilder(pattern.Length * 2);
		var text = new StringBuilder();
		for (var i = 0; i < pattern.Length; i++)
		{
			var c = pattern[i];
			if (c is '\'' or '"')
			{
				var close = pattern.IndexOf(c, i + 1);
				var end = close < 0 ? pattern.Length : close;
				text.Append(pattern, i + 1, end - i - 1);
				i = end;

				continue;
			}

			if (c == '\\' && i + 1 < pattern.Length)
			{
				text.Append(pattern[++i]);

				continue;
			}

			if (c == '/' || c == ':')
			{
				text.Append(c == '/' ? info.DateSeparator : info.TimeSeparator);

				continue;
			}

			var run = 1;
			while (i + run < pattern.Length && pattern[i + run] == c)
			{
				run++;
			}

			var token = c switch
			{
				'd' => new string('d', Math.Min(run, 4)),
				'M' => new string('m', Math.Min(run, 4)),
				'y' => run <= 2 ? "yy" : "yyyy",
				'h' or 'H' => run == 1 ? "h" : "hh",
				'm' => run == 1 ? "m" : "mm",
				's' => run == 1 ? "s" : "ss",
				't' => run == 1 ? "A/P" : "AM/PM",
				'g' or 'z' or 'K' or 'f' or 'F' or '%' => "",
				_ => null,
			};
			if (token is null)
			{
				text.Append(c);

				continue;
			}

			AppendText(code, text);
			code.Append(token);
			i += run - 1;
		}

		AppendText(code, text);

		return code.ToString();
	}

	/// <summary>Appends a pattern's text to a code: characters that show as themselves bare, the rest in quotes.</summary>
	/// <param name="code">The code.</param>
	/// <param name="text">The text, which is cleared.</param>
	private static void AppendText(StringBuilder code, StringBuilder text)
	{
		var quoted = false;
		for (var i = 0; i < text.Length; i++)
		{
			var c = text[i];
			var bare = Unquoted.IndexOf(c) >= 0;
			if (bare || c == '"')
			{
				if (quoted)
				{
					code.Append('"');
					quoted = false;
				}

				code.Append(c == '"' ? "\\\"" : c.ToString());

				continue;
			}

			if (!quoted)
			{
				code.Append('"');
				quoted = true;
			}

			code.Append(c);
		}

		if (quoted)
		{
			code.Append('"');
		}

		text.Clear();
	}
}
