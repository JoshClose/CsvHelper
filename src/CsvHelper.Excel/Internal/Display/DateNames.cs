using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// The names a date or time shows in a language, in UTF-8: the months, whole and abbreviated, and their first letters
/// (<c>mmmmm</c>), the days of the week, whole and abbreviated, and the designators of <c>AM/PM</c>. Excel names months in
/// the nominative (<c>15 Январь 2024</c>), so the names are .NET's month names, never the genitive ones.
/// </summary>
/// <remarks>
/// <para>
/// A designator that the culture data leaves empty, as NLS does for de-DE, is <c>AM</c> or <c>PM</c>, which Excel shows
/// there.
/// </para>
/// <para>
/// A locale code's language that the runtime has no data for names nothing, so that the reader's culture names the months
/// and days, as where no locale code names a language (Phase 8's step 8): an id the runtime doesn't know, a tag none of
/// whose prefixes is a culture it has, and every language under invariant globalization. Excel drops an id it doesn't know
/// (<c>[$-1234]</c> reads as <c>[$]</c>), and reads <c>[$-400]</c>, <c>[$-800]</c> and <c>[$-C00]</c> as the user's own
/// language.
/// </para>
/// <para>
/// A workbook's codes can name any id and any tag, so the names are kept by what they resolve to, never by what a code
/// says: by language id, of which there are 65,536, and by the name of a culture the runtime has. A tag is given to the
/// runtime only when it names one of those: the runtime makes up a culture for any tag of a language's shape, and keeps it
/// for the process's life (20,000 such tags kept 14 MB on .NET 10 and 17 MB on .NET Framework).
/// </para>
/// </remarks>
internal sealed class DateNames
{
	/// <summary>The language id of the invariant culture.</summary>
	private const int InvariantLanguage = 0x7F;

	/// <summary>The language id of en-US, whose names are English's.</summary>
	private const int EnglishLanguage = 0x409;

	/// <summary>The names of each language a locale code has named, by language id: <see langword="null"/> for one the runtime has no data for.</summary>
	private static readonly ConcurrentDictionary<int, DateNames?> Languages = new();

	/// <summary>The names of each culture a locale code's tag has resolved to, by the culture's name (<c>de-DE</c>).</summary>
	private static readonly ConcurrentDictionary<string, DateNames?> Tags = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>The names of the cultures the runtime has, compared ignoring case; read the first time a tag asks.</summary>
	private static readonly Lazy<HashSet<string>> CultureNames = new(ReadCultureNames);

	/// <summary>Reads the names of a culture's data.</summary>
	/// <param name="info">The data.</param>
	private DateNames(DateTimeFormatInfo info)
	{
		Months = Encode(info.MonthNames, 12);
		AbbreviatedMonths = Encode(info.AbbreviatedMonthNames, 12);
		MonthLetters = new byte[12][];
		for (var i = 0; i < 12; i++)
		{
			MonthLetters[i] = FirstLetter(info.MonthNames[i]);
		}

		Days = Encode(info.DayNames, 7);
		AbbreviatedDays = Encode(info.AbbreviatedDayNames, 7);
		AM = Encoding.UTF8.GetBytes(info.AMDesignator.Length > 0 ? info.AMDesignator : "AM");
		PM = Encoding.UTF8.GetBytes(info.PMDesignator.Length > 0 ? info.PMDesignator : "PM");
	}

	/// <summary>English's names, as Excel en-US shows them: the invariant culture's.</summary>
	public static DateNames English { get; } = new(CultureInfo.InvariantCulture.DateTimeFormat);

	/// <summary>How many languages' and cultures' names are kept, or known to be none: at most the language ids and the runtime's cultures, whatever codes name.</summary>
	public static int Kept => Languages.Count + Tags.Count;

	/// <summary>The months' names, January first (<c>mmmm</c>).</summary>
	public byte[][] Months { get; }

	/// <summary>The months' abbreviated names (<c>mmm</c>).</summary>
	public byte[][] AbbreviatedMonths { get; }

	/// <summary>The months' first letters (<c>mmmmm</c>): a character, or a surrogate pair.</summary>
	public byte[][] MonthLetters { get; }

	/// <summary>The days' names, Sunday first (<c>dddd</c>, <c>aaaa</c>).</summary>
	public byte[][] Days { get; }

	/// <summary>The days' abbreviated names (<c>ddd</c>, <c>aaa</c>).</summary>
	public byte[][] AbbreviatedDays { get; }

	/// <summary>The designator before noon (<c>AM/PM</c>).</summary>
	public byte[] AM { get; }

	/// <summary>The designator from noon.</summary>
	public byte[] PM { get; }

	/// <summary>The names of a culture's data, the reader's own.</summary>
	/// <param name="info">The data.</param>
	/// <returns>The names: <see cref="English"/> for the invariant culture's.</returns>
	public static DateNames Of(DateTimeFormatInfo info) => ReferenceEquals(info, CultureInfo.InvariantCulture.DateTimeFormat) ? English : new DateNames(info);

	/// <summary>The names of the language a locale code names by its id (<c>[$-407]</c>, the low 16 bits).</summary>
	/// <param name="language">The language id: 0x407 for German (Germany).</param>
	/// <returns>The names; <see langword="null"/> where the runtime has no data for the language, so that the reader's culture names.</returns>
	public static DateNames? ForLanguage(int language)
	{
		if (language is EnglishLanguage or InvariantLanguage)
		{
			return English;
		}

		return Languages.GetOrAdd(language & 0xFFFF, static id =>
		{
			try
			{
				return Known(CultureInfo.GetCultureInfo(id));
			}
			catch (CultureNotFoundException)
			{
				return null;
			}
			catch (ArgumentException)
			{
				return null;
			}
		});
	}

	/// <summary>
	/// The names of the language a locale code names by its tag (<c>[$-de-DE]</c>): those of the tag's longest prefix of
	/// whole subtags that is the name of a culture the runtime has, as Excel reads <c>[$-de-CH-x-foo]</c> as de-CH,
	/// <c>[$-de-ZZ]</c> as German and <c>[$-ja-u-ca-japanese]</c> as Japanese (Phase 8's step 8).
	/// </summary>
	/// <param name="tag">The tag: <c>de-DE</c>.</param>
	/// <returns>The names; <see langword="null"/> where no prefix is a culture the runtime has, so that the reader's culture names.</returns>
	public static DateNames? ForTag(string tag)
	{
		var name = CultureName(tag);
		if (name is null)
		{
			return null;
		}

		return Tags.GetOrAdd(name, static known =>
		{
			try
			{
				return Known(CultureInfo.GetCultureInfo(known));
			}
			catch (CultureNotFoundException)
			{
				return null;
			}
			catch (ArgumentException)
			{
				return null;
			}
		});
	}

	/// <summary>Whether a name is that of a culture the runtime has, ignoring case: <c>de</c> and <c>de-DE</c>, not <c>qq-ZZ</c>.</summary>
	/// <param name="name">The name.</param>
	/// <returns>Whether it is.</returns>
	public static bool IsCultureName(string name) => CultureNames.Value.Contains(name);

	/// <summary>A tag's longest prefix of whole subtags that is the name of a culture the runtime has.</summary>
	/// <param name="tag">The tag.</param>
	/// <returns>The prefix; <see langword="null"/> when none is.</returns>
	private static string? CultureName(string tag)
	{
		var names = CultureNames.Value;
		var length = tag.Length;
		while (length > 0)
		{
			var prefix = tag.Substring(0, length);
			if (names.Contains(prefix))
			{
				return prefix;
			}

			length = tag.LastIndexOf('-', length - 1);
		}

		return null;
	}

	/// <summary>The names of the cultures the runtime has: none but the invariant culture's under invariant globalization.</summary>
	/// <returns>The names, compared ignoring case.</returns>
	private static HashSet<string> ReadCultureNames()
	{
		var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures))
		{
			if (culture.Name.Length > 0)
			{
				names.Add(culture.Name);
			}
		}

		return names;
	}

	/// <summary>A culture's names, when the runtime has data for its language.</summary>
	/// <param name="culture">The culture.</param>
	/// <returns>The names; <see langword="null"/> when it hasn't.</returns>
	private static DateNames? Known(CultureInfo culture) => IsKnown(culture) ? new DateNames(culture.DateTimeFormat) : null;

	/// <summary>
	/// Whether the runtime has data for a culture's language: not the invariant culture, and not one ICU makes up for a tag
	/// it doesn't know (whose months it names M01 to M12), which has no Windows language name.
	/// </summary>
	/// <param name="culture">The culture.</param>
	/// <returns>Whether it has.</returns>
	private static bool IsKnown(CultureInfo culture) => culture.Name.Length > 0 && culture.ThreeLetterWindowsLanguageName != "ZZZ";

	/// <summary>Encodes the first names of a list.</summary>
	/// <param name="names">The names: .NET's months have a 13th, empty.</param>
	/// <param name="count">How many to take.</param>
	/// <returns>Their UTF-8.</returns>
	private static byte[][] Encode(string[] names, int count)
	{
		var encoded = new byte[count][];
		for (var i = 0; i < count; i++)
		{
			encoded[i] = Encoding.UTF8.GetBytes(names[i]);
		}

		return encoded;
	}

	/// <summary>A name's first letter: its first character, or its first surrogate pair.</summary>
	/// <param name="name">The name.</param>
	/// <returns>Its UTF-8; nothing for an empty name.</returns>
	private static byte[] FirstLetter(string name)
	{
		if (name.Length == 0)
		{
			return [];
		}

		var length = char.IsHighSurrogate(name[0]) && name.Length > 1 && char.IsLowSurrogate(name[1]) ? 2 : 1;

		return Encoding.UTF8.GetBytes(name.Substring(0, length));
	}
}
