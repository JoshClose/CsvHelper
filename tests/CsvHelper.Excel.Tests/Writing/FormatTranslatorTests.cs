using System.Globalization;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// .NET date and time formats translated to Excel's codes: the standard formats in five cultures, each custom token,
/// what isn't translated, and the reading back of each format's text, which keeps only a text that formats again to itself.
/// </summary>
public class FormatTranslatorTests
{
	/// <summary>A value with every part: a morning time with milliseconds.</summary>
	private static readonly DateTime Value = new(2024, 1, 15, 10, 30, 5, 123);

	/// <summary>The standard formats and their codes, in the invariant culture and four others (as ICU's data has them on .NET).</summary>
	public static TheoryData<string, string, string?> StandardFormats => new()
	{
		{ "", "d", "mm\"/\"dd\"/\"yyyy" },
		{ "", "D", "[$-409]dddd\", \"dd\" \"mmmm\" \"yyyy" },
		{ "", "f", "[$-409]dddd\", \"dd\" \"mmmm\" \"yyyy\" \"hh\":\"mm" },
		{ "", "F", "[$-409]dddd\", \"dd\" \"mmmm\" \"yyyy\" \"hh\":\"mm\":\"ss" },
		{ "", "g", "mm\"/\"dd\"/\"yyyy\" \"hh\":\"mm" },
		{ "", "G", "mm\"/\"dd\"/\"yyyy\" \"hh\":\"mm\":\"ss" },
		{ "", "M", null },
		{ "", "R", "[$-409]ddd\", \"dd\" \"mmm\" \"yyyy\" \"hh\":\"mm\":\"ss\" GMT\"" },
		{ "", "s", "yyyy\"-\"mm\"-\"dd\"T\"hh\":\"mm\":\"ss" },
		{ "", "t", "hh\":\"mm" },
		{ "", "T", "hh\":\"mm\":\"ss" },
		{ "", "u", "yyyy\"-\"mm\"-\"dd\" \"hh\":\"mm\":\"ss\"Z\"" },
		{ "", "U", null },
		{ "", "y", "[$-409]yyyy\" \"mmmm" },
		{ "", "O", null },
		{ "en-US", "d", "m\"/\"d\"/\"yyyy" },
		{ "en-US", "D", "[$-409]dddd\", \"mmmm\" \"d\", \"yyyy" },
		{ "en-US", "g", "[$-409]m\"/\"d\"/\"yyyy\" \"h\":\"mm\" \"AM/PM" },
		{ "en-US", "t", "[$-409]h\":\"mm\" \"AM/PM" },
		{ "en-US", "T", "[$-409]h\":\"mm\":\"ss\" \"AM/PM" },
		{ "en-US", "Y", "[$-409]mmmm\" \"yyyy" },
		{ "de-DE", "d", "dd\".\"mm\".\"yyyy" },
		{ "de-DE", "D", "[$-407]dddd\", \"d\". \"mmmm\" \"yyyy" },
		{ "de-DE", "g", "dd\".\"mm\".\"yyyy\" \"hh\":\"mm" },
		{ "de-DE", "R", "[$-409]ddd\", \"dd\" \"mmm\" \"yyyy\" \"hh\":\"mm\":\"ss\" GMT\"" },
		{ "fr-FR", "d", "dd\"/\"mm\"/\"yyyy" },
		{ "fr-FR", "D", "[$-40C]dddd\" \"d\" \"mmmm\" \"yyyy" },
		{ "fr-FR", "y", "[$-40C]mmmm\" \"yyyy" },
		{ "ja-JP", "d", "yyyy\"/\"mm\"/\"dd" },
#if NET
		{ "ja-JP", "D", "[$-411]yyyy\"\u5e74\"m\"\u6708\"d\"\u65e5\"dddd" },
#else
		// Windows' own data, which .NET Framework has, writes ja-JP's long date without the day's name.
		{ "ja-JP", "D", "yyyy\"\u5e74\"m\"\u6708\"d\"\u65e5\"" },
#endif
		{ "ja-JP", "t", "h\":\"mm" },
		{ "ja-JP", "G", "yyyy\"/\"mm\"/\"dd\" \"h\":\"mm\":\"ss" },
	};

	/// <summary>Custom formats and their codes in the invariant culture, or <see langword="null"/> for those not translated.</summary>
	public static TheoryData<string, string?> CustomFormats => new()
	{
		{ "yyyy-MM-dd", "yyyy\"-\"mm\"-\"dd" },
		{ "dd/MM/yy", "dd\"/\"mm\"/\"yy" },
		{ "d.M.yyyy", "d\".\"m\".\"yyyy" },
		{ "HH:mm:ss.fff", "hh\":\"mm\":\"ss.000" },
		{ "H:mm:ss.ff", "h\":\"mm\":\"ss.00" },
		{ "HH:mm:ss.f", "hh\":\"mm\":\"ss.0" },
		{ "s.f", "s.0" },
		{ "h:mm tt", "[$-409]h\":\"mm\" \"AM/PM" },
		// Excel shows a tagged A/P as AM or PM: t has no tag, and none beside names or tt.
		{ "hh:mm t", "hh\":\"mm\" \"A/P" },
		{ "MMM yyyy h t", null },
		{ "h tt t", null },
		{ "mm:ss", "mm\":\"ss" },
		{ "%H", "h" },
		{ "yyyy'\u5e74'", "yyyy\"\u5e74\"" },
		{ "yyyy\\-MM", "yyyy\"-\"mm" },
		{ "yyyy\"x\"MM", "yyyy\"x\"mm" },
		{ "yyyy'\"'MM", "yyyy\\\"mm" },
		{ "yyyy'a\\'b'MM", "yyyy\"a'b\"mm" },
		{ "'Q' yyyy", "\"Q \"yyyy" },
		{ "yyyy-MM-ddTHH:mm", "yyyy\"-\"mm\"-\"dd\"T\"hh\":\"mm" },
		{ "MMM d, yyyy", "[$-409]mmm\" \"d\", \"yyyy" },
		{ "ddd MMMM yyyy", "[$-409]ddd\" \"mmmm\" \"yyyy" },
		{ "dddddd yyyy", "[$-409]dddd\" \"yyyy" },
		{ "MMMMMM yyyy", "[$-409]mmmm\" \"yyyy" },
		{ "yyyy;MM", "yyyy\";\"mm" },
		{ "HH 'h' mm", "hh\" h \"mm" },
		// Minutes where Excel reads a month, and a month where it reads minutes.
		{ "yyyy-mm-dd", null },
		{ "mm", null },
		{ "HH:MM", null },
		{ "MM:ss", null },
		{ "yyyy HH:MM", null },
		{ "yyyy MM:ss", null },
		{ "HH tt mm", null },
		// Years Excel has no token for (y alone is the standard year and month).
		{ "%y", null },
		{ "yyy", null },
		{ "yyyyy", null },
		// A 12-hour hour without its designator, and a 24-hour one with it.
		{ "hh:mm", null },
		{ "HH:mm tt", null },
		{ "h:mm tt HH", null },
		// Fractions Excel can't show as .NET does.
		{ "ss.ffff", null },
		{ "ss.FFF", null },
		{ "ss,fff", null },
		{ "fff", null },
		{ "HH:mm.fff", null },
		{ "ss'.x'fff", null },
		// Offsets, eras, more letters than .NET's tokens take.
		{ "yyyy zzz", null },
		{ "yyyy K", null },
		{ "gg yyyy", null },
		{ "hhh tt", null },
		{ "HHH", null },
		{ "mmm", null },
		{ "sss", null },
		{ "ttt h", null },
		// Tokens of one letter side by side, which Excel would read as one.
		{ "%H%H", null },
		{ "%HH:mm", null },
		{ "yyyy dddd%d", null },
		// Dates without a year.
		{ "dd MMMM", null },
		{ "dddd", null },
		{ "%d", null },
		// Texts .NET can't format.
		{ "yyyy'abc", null },
		{ "yyyy\\", null },
		{ "yyyy%", null },
		{ "yyyy%%", null },
		// No value at all.
		{ "'x'", null },
	};

	[Theory]
	[MemberData(nameof(StandardFormats))]
	public void TranslatesTheStandardFormats(string culture, string format, string? code)
	{
		var provider = CultureInfo.GetCultureInfo(culture);

		var translation = FormatTranslator.Translate(format, CellKind.DateTime, provider);

		Assert.Equal(code, translation?.Code);
		if (translation is not null)
		{
			AssertReadsBack(translation, Value.ToString(format, provider), format, provider);
		}
	}

	[Theory]
	[MemberData(nameof(CustomFormats))]
	public void TranslatesTheCustomTokens(string format, string? code)
	{
		var translation = FormatTranslator.Translate(format, CellKind.DateTime, CultureInfo.InvariantCulture);

		Assert.Equal(code, translation?.Code);
	}

	[Fact]
	public void UsesTheCulturesSeparators()
	{
		// sv-SE writes dates with -, and de-DE's / is a .; Excel shows them as given whatever its own locale.
		Assert.Equal("yyyy\"-\"mm\"-\"dd", FormatTranslator.Translate("yyyy/MM/dd", CellKind.DateTime, CultureInfo.GetCultureInfo("sv-SE"))?.Code);
		Assert.Equal("dd\".\"mm\".\"yyyy", FormatTranslator.Translate("dd/MM/yyyy", CellKind.DateTime, CultureInfo.GetCultureInfo("de-DE"))?.Code);
		Assert.Equal("hh\":\"mm", FormatTranslator.Translate("HH:mm", CellKind.DateTime, CultureInfo.GetCultureInfo("de-DE"))?.Code);
		// Escaped and quoted, they are the characters themselves.
		Assert.Equal("yyyy\"/\"mm\":\"dd", Translate("yyyy\\/MM':'dd", "de-DE"));
	}

	[Fact]
	public void TagsNamesWithTheCulturesLocale()
	{
		Assert.Equal("[$-407]mmmm\" \"yyyy", Translate("MMMM yyyy", "de-DE"));
		Assert.Equal("[$-407]mmm\" \"yyyy", Translate("MMM yyyy", "de-DE"));
		Assert.Equal("[$-40C]ddd\" \"d\" \"mmm\" \"yyyy", Translate("ddd d MMM yyyy", "fr-FR"));
		Assert.Equal("[$-411]mmm\" \"yyyy\"\u5e74\"", Translate("MMM yyyy'\u5e74'", "ja-JP"));
		Assert.Equal("yyyy\"\u5e74\"m\"\u6708\"", Translate("yyyy'\u5e74'M'\u6708'", "ja-JP"));
		Assert.Equal("[$-411]ddd\" \"yyyy", Translate("ddd yyyy", "ja-JP"));
		Assert.Equal("[$-809]dddd\" \"d\" \"mmmm\" \"yyyy", Translate("dddd d MMMM yyyy", "en-GB"));
#if NET
		// A neutral culture's id (.NET Framework has no formats for neutral cultures).
		Assert.Equal("[$-7]mmmm\" \"yyyy", Translate("MMMM yyyy", "de"));
#endif
		// Numbers alone need none, and AM/PM without names is English's. ICU's de-DE has AM and PM; Windows' own data,
		// which .NET Framework has, has no designators, and nothing with tt is translated then.
		Assert.Equal("dd\".\"mm\".\"yyyy", Translate("dd.MM.yyyy", "de-DE"));
		var german = CultureInfo.GetCultureInfo("de-DE").DateTimeFormat.AMDesignator == "AM";
		Assert.Equal(german ? "[$-409]h\":\"mm\" \"AM/PM" : null, Translate("h:mm tt", "de-DE"));
		Assert.Equal(german ? "[$-407]mmmm\" \"yyyy\" \"h\" \"AM/PM" : null, Translate("MMMM yyyy h tt", "de-DE"));
		Assert.Equal("[$-409]h\":\"mm\" \"AM/PM", Translate("h:mm tt", "en-US"));
	}

	[Fact]
	public void LeavesNamesThatExcelShowsOtherwise()
	{
		// .NET writes pl-PL's and ru-RU's full months in the genitive with a day of the month, and ICU's de-DE its abbreviated
		// ones; Excel shows the nominative (checked through COM). Windows' own data, which .NET Framework has, has de-DE's
		// abbreviated genitives the same as the nominative.
		var german = CultureInfo.GetCultureInfo("de-DE").DateTimeFormat;
		Assert.Equal(
			german.AbbreviatedMonthGenitiveNames.SequenceEqual(german.AbbreviatedMonthNames) ? "[$-407]dd\". \"mmm\" \"yyyy" : null,
			Translate("dd. MMM yyyy", "de-DE"));
		Assert.False(CultureInfo.GetCultureInfo("pl-PL").DateTimeFormat.MonthGenitiveNames.SequenceEqual(CultureInfo.GetCultureInfo("pl-PL").DateTimeFormat.MonthNames));
		Assert.Null(Translate("d MMMM yyyy", "pl-PL"));
		Assert.Null(Translate("d MMMM yyyy", "ru-RU"));
		Assert.Equal("[$-415]mmmm\" \"yyyy", Translate("MMMM yyyy", "pl-PL"));
		Assert.Equal("[$-407]dddd\", \"d\". \"mmmm\" \"yyyy", Translate("dddd, d. MMMM yyyy", "de-DE"));
		// Designators other than AM and PM (ICU's en-GB has am and pm; Windows' has AM and PM), and calendars other than the
		// Gregorian.
		Assert.Null(Translate("h:mm tt", "ja-JP"));
		Assert.Equal(CultureInfo.GetCultureInfo("en-GB").DateTimeFormat.AMDesignator == "AM" ? "[$-409]h\":\"mm\" \"AM/PM" : null, Translate("h:mm tt", "en-GB"));
#if NET
		Assert.Null(Translate("h:mm tt", "en-GB"));
#endif
		Assert.Null(Translate("d", "th-TH"));
		Assert.Null(Translate("yyyy-MM-dd", "ar-SA"));
		// Names without a culture to tag them with: a DateTimeFormatInfo isn't one, and only English names are Excel's own.
		Assert.Null(FormatTranslator.Translate("MMMM yyyy", CellKind.DateTime, CultureInfo.GetCultureInfo("de-DE").DateTimeFormat));
		Assert.Equal("[$-409]mmmm\" \"yyyy", FormatTranslator.Translate("MMMM yyyy", CellKind.DateTime, DateTimeFormatInfo.InvariantInfo)?.Code);
		Assert.Equal("dd\".\"mm\".\"yyyy", FormatTranslator.Translate("d", CellKind.DateTime, CultureInfo.GetCultureInfo("de-DE").DateTimeFormat)?.Code);
	}

	[Fact]
	public void TranslatesEachKindsStandardFormats()
	{
		var invariant = CultureInfo.InvariantCulture;
		Assert.Equal("[$-409]ddd\", \"dd\" \"mmm\" \"yyyy", FormatTranslator.Translate("R", CellKind.DateOnly, invariant)?.Code);
		Assert.Equal("mm\"/\"dd\"/\"yyyy", FormatTranslator.Translate("d", CellKind.DateOnly, invariant)?.Code);
		Assert.Null(FormatTranslator.Translate("g", CellKind.DateOnly, invariant));
		Assert.Null(FormatTranslator.Translate("t", CellKind.DateOnly, invariant));
		Assert.Equal("hh\":\"mm\":\"ss", FormatTranslator.Translate("R", CellKind.TimeOnly, invariant)?.Code);
		Assert.Equal("hh\":\"mm", FormatTranslator.Translate("t", CellKind.TimeOnly, invariant)?.Code);
		Assert.Null(FormatTranslator.Translate("d", CellKind.TimeOnly, invariant));
		Assert.Equal("[$-409]ddd\", \"dd\" \"mmm\" \"yyyy\" \"hh\":\"mm\":\"ss\" GMT\"", FormatTranslator.Translate("r", CellKind.DateTimeOffset, invariant)?.Code);
		Assert.Null(FormatTranslator.Translate("U", CellKind.DateTimeOffset, invariant));
	}

	[Fact]
	public void GivesCodesExcelOpensAsDates()
	{
		var cultures = new[] { "", "en-US", "de-DE", "fr-FR", "ja-JP", "en-GB", "sv-SE", "pl-PL" };
		var formats = new List<string> { "d", "D", "f", "F", "g", "G", "R", "s", "t", "T", "u", "y" };
		formats.AddRange(CustomFormats.Select(row => (string)row[0]));
		var codes = 0;
		foreach (var culture in cultures)
		{
			foreach (var format in formats)
			{
				if (FormatTranslator.Translate(format, CellKind.DateTime, CultureInfo.GetCultureInfo(culture)) is not { } translation)
				{
					continue;
				}

				codes++;
				Assert.Null(FormatCheck.Problem(translation.Code));
				Assert.Equal(SheetCellKind.DateNumber, NumberFormats.Classify(translation.Code));
			}
		}

		Assert.True(codes > 200, codes.ToString(CultureInfo.InvariantCulture));
	}

	[Fact]
	public void ReadsTextBackToWhatItShows()
	{
		// A format that drops the day holds the month's first; one without a date holds the time of day.
		Assert.True(Translation("yyyy-MM").TryRead("2024-03"u8, out var milliseconds));
		Assert.Equal(new DateTime(2024, 3, 1), FromSerial(milliseconds));
		var clock = Translation("HH:mm");
		Assert.False(clock.HasDate);
		Assert.True(clock.TryRead("10:30"u8, out milliseconds));
		Assert.Equal((10 * 60 + 30) * 60_000, milliseconds);
		// .NET truncates fractions, and the text's digits are what the cell holds.
		var text = new DateTime(2024, 1, 15, 10, 30, 5, 999).ToString("yyyy-MM-dd HH:mm:ss.ff", CultureInfo.InvariantCulture);
		Assert.True(Translation("yyyy-MM-dd HH:mm:ss.ff").TryRead(System.Text.Encoding.UTF8.GetBytes(text), out milliseconds));
		Assert.Equal(new DateTime(2024, 1, 15, 10, 30, 5, 990), FromSerial(milliseconds));
		// 1900's first days, past Excel's 1900-02-29, and before 1900.
		var date = Translation("yyyy-MM-dd");
		Assert.True(date.TryRead("1900-01-01"u8, out milliseconds));
		Assert.Equal(Serial.MillisecondsPerDay, milliseconds);
		Assert.True(date.TryRead("1900-03-01"u8, out milliseconds));
		Assert.Equal(61 * Serial.MillisecondsPerDay, milliseconds);
		Assert.True(date.TryRead("1899-12-31"u8, out milliseconds));
		Assert.Equal(-1, milliseconds);
		// Text the format doesn't give: another layout, padding the format doesn't have, or a value that reads back to other text.
		Assert.False(date.TryRead("2024-1-15"u8, out _));
		Assert.False(date.TryRead("15.01.2024"u8, out _));
		Assert.False(date.TryRead(" 2024-01-15"u8, out _));
		Assert.False(date.TryRead("2024-02-30"u8, out _));
		Assert.False(date.TryRead(""u8, out _));
		Assert.False(Translation("ddd yyyy-MM-dd").TryRead("Tue 2024-01-15"u8, out _));
		Assert.True(Translation("ddd yyyy-MM-dd").TryRead("Mon 2024-01-15"u8, out _));
		// .NET parses names in any case, but writes them in one: only that text is the format's.
		Assert.False(Translation("MMM yyyy").TryRead("jan 2024"u8, out _));
		Assert.True(Translation("MMM yyyy").TryRead("Jan 2024"u8, out _));
		// Invalid UTF-8 is no format's text.
		Assert.False(date.TryRead([0x32, 0xFF, 0x30], out _));
	}

	[Fact]
	public void ReadsTwoDigitYearsInTheCalendarsWindow()
	{
		// A year of two digits reads back in .NET's window (to 2049): the text is what the cell shows.
		var translation = Translation("dd/MM/yy");
		Assert.True(translation.TryRead("15/01/49"u8, out var milliseconds));
		Assert.Equal(2049, FromSerial(milliseconds).Year);
		Assert.True(translation.TryRead("15/01/50"u8, out milliseconds));
		Assert.Equal(1950, FromSerial(milliseconds).Year);
	}

	[Theory]
	[InlineData("")]
	[InlineData("en-US")]
	[InlineData("de-DE")]
	[InlineData("fr-FR")]
	[InlineData("ja-JP")]
	public void ReadsRandomValuesBack(string culture)
	{
		var provider = CultureInfo.GetCultureInfo(culture);
		var random = new Random(20261107);
		var formats = new[] { "d", "D", "f", "F", "g", "G", "R", "s", "t", "T", "u", "y", "yyyy-MM-dd HH:mm:ss.fff", "dd.MM.yy h:mm:ss.f tt", "MMM yyyy", "ddd, d MMMM yyyy", "HH:mm:ss" };
		var translated = 0;
		foreach (var format in formats)
		{
			if (FormatTranslator.Translate(format, CellKind.DateTime, provider) is not { } translation)
			{
				continue;
			}

			translated++;
			for (var i = 0; i < 300; i++)
			{
				var value = new DateTime(1900, 1, 1).AddTicks((long)(random.NextDouble() * (new DateTime(9999, 12, 31) - new DateTime(1900, 1, 1)).Ticks));

				AssertReadsBack(translation, value.ToString(format, provider), format, provider);
			}
		}

		Assert.True(translated >= 10, translated.ToString(CultureInfo.InvariantCulture));
	}

	[Fact]
	public void MeasuresTheWidestText()
	{
		// September is the widest month, and Wednesday the widest day: the first value's text needn't be.
		Assert.Equal(ColumnWidths.Of("Wednesday, September 28, 2024".AsSpan()), Translation("dddd, MMMM dd, yyyy").Width);
		Assert.Equal(ColumnWidths.Of("2024-12-28 23:58:58.888".AsSpan()), Translation("yyyy-MM-dd HH:mm:ss.fff").Width);
		// Text that fits the default column needs no width.
		Assert.Equal(0, Translation("HH:mm").Width);
		Assert.Equal(0, Translation("yyyy").Width);
	}

	/// <summary>The code of a format in a culture, for date and time values.</summary>
	/// <param name="format">The format.</param>
	/// <param name="culture">The culture's name.</param>
	/// <returns>The code, or <see langword="null"/>.</returns>
	private static string? Translate(string format, string culture) =>
		FormatTranslator.Translate(format, CellKind.DateTime, CultureInfo.GetCultureInfo(culture))?.Code;

	/// <summary>The translation of a format in the invariant culture, which must have one.</summary>
	/// <param name="format">The format.</param>
	/// <returns>The translation.</returns>
	private static DateFormat Translation(string format) =>
		FormatTranslator.Translate(format, CellKind.DateTime, CultureInfo.InvariantCulture) ?? throw new InvalidOperationException(format);

	/// <summary>
	/// Asserts that a format's text reads back, and that the value its serial is (as Phase 2 reads a serial) formats to the
	/// same text.
	/// </summary>
	/// <param name="translation">The translation.</param>
	/// <param name="text">The text.</param>
	/// <param name="format">The .NET format.</param>
	/// <param name="provider">The provider the text was formatted with.</param>
	private static void AssertReadsBack(DateFormat translation, string text, string format, IFormatProvider provider)
	{
		Assert.True(translation.TryRead(System.Text.Encoding.UTF8.GetBytes(text), out var milliseconds), $"{format}: {text}");
		var shown = translation.HasDate ? FromSerial(milliseconds) : new DateTime(milliseconds * TimeSpan.TicksPerMillisecond);
		Assert.Equal(text, shown.ToString(format, provider));
	}

	/// <summary>A date and time from its serial in milliseconds, as Phase 2 reads it.</summary>
	/// <param name="milliseconds">The serial in milliseconds.</param>
	/// <returns>The date and time.</returns>
	private static DateTime FromSerial(long milliseconds)
	{
		var day = milliseconds / Serial.MillisecondsPerDay;
		var epoch = day < 60 ? new DateTime(1899, 12, 31) : new DateTime(1899, 12, 30);

		return epoch.AddTicks(milliseconds * TimeSpan.TicksPerMillisecond);
	}
}
