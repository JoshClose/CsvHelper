using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// The names a date or time shows in a language, in UTF-8: the months, whole and abbreviated, and their first letters
/// (<c>mmmmm</c>), the days of the week, whole and abbreviated, and the designators of <c>AM/PM</c>. Excel names months in
/// the nominative (<c>15 Январь 2024</c>), so the names are .NET's month names, never the genitive ones.
/// </summary>
/// <remarks>
/// A designator that the culture data leaves empty, as NLS does for de-DE, is <c>AM</c> or <c>PM</c>, which Excel shows
/// there. A language the runtime has no data for, as under invariant globalization, names in English.
/// </remarks>
internal sealed class DateNames
{
	/// <summary>The language id of the invariant culture.</summary>
	private const int InvariantLanguage = 0x7F;

	/// <summary>The language id of en-US, whose names are English's.</summary>
	private const int EnglishLanguage = 0x409;

	/// <summary>The names of each language a locale code has named, by language id.</summary>
	private static readonly ConcurrentDictionary<int, DateNames> Languages = new();

	/// <summary>The names of each language a locale code has named by its tag (<c>[$-de-DE]</c>).</summary>
	private static readonly ConcurrentDictionary<string, DateNames> Tags = new(StringComparer.OrdinalIgnoreCase);

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
	/// <returns>The names; English's where the runtime has no data for the language.</returns>
	public static DateNames ForLanguage(int language)
	{
		if (language is EnglishLanguage or InvariantLanguage)
		{
			return English;
		}

		return Languages.GetOrAdd(language, static id =>
		{
			try
			{
				var culture = CultureInfo.GetCultureInfo(id);

				return IsKnown(culture) ? new DateNames(culture.DateTimeFormat) : English;
			}
			catch (CultureNotFoundException)
			{
				return English;
			}
			catch (ArgumentException)
			{
				return English;
			}
		});
	}

	/// <summary>The names of the language a locale code names by its tag (<c>[$-de-DE]</c>).</summary>
	/// <param name="tag">The tag: <c>de-DE</c>.</param>
	/// <returns>The names; English's where the runtime has no data for the language.</returns>
	public static DateNames ForTag(string tag)
	{
		return Tags.GetOrAdd(tag, static name =>
		{
			try
			{
				var culture = CultureInfo.GetCultureInfo(name);

				return IsKnown(culture) ? new DateNames(culture.DateTimeFormat) : English;
			}
			catch (CultureNotFoundException)
			{
				return English;
			}
			catch (ArgumentException)
			{
				return English;
			}
		});
	}

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
