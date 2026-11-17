using System.Globalization;
using System.Text;

namespace CsvHelper.Excel.Tests.Display;

/// <summary>
/// What a culture gives the text a cell shows (<see cref="DisplayCulture"/>, <see cref="DateNames"/>): the invariant
/// culture as Excel en-US, and other cultures from the runtime's data, ICU on .NET and NLS on .NET Framework, whose
/// differences for the probed locales are in Phase 8's "Checked on 2026-11-16" (<c>Csv\_bench\results\phase8\design\cultures</c>).
/// </summary>
public class DisplayCultureTests
{
	/// <summary>The invariant culture, the reader's default, is Excel en-US exactly, not .NET's invariant data.</summary>
	[Fact]
	public void InvariantIsExcelEnglish()
	{
		var display = DisplayCulture.Invariant;
		Assert.True(display.IsInvariant);
		Assert.Same(display, DisplayCulture.For(null));
		Assert.Same(display, DisplayCulture.For(CultureInfo.InvariantCulture));
		Assert.Same(display, DisplayCulture.For(NumberFormatInfo.InvariantInfo));
		Assert.Same(display, DisplayCulture.For(DateTimeFormatInfo.InvariantInfo));
		Assert.Equal(".", Text(display.DecimalSeparator));
		Assert.Equal(",", Text(display.GroupSeparator));
		Assert.Equal("m/d/yyyy", display.ShortDate);
		Assert.Equal("dddd, mmmm d, yyyy", display.LongDate);
		Assert.Equal("h:mm:ss AM/PM", display.LongTime);
		Assert.Null(display.Currency);
		Assert.Same(DateNames.English, display.Names);
		Assert.True(display.LongDateFormat.IsReadable);
		Assert.True(display.LongTimeFormat.IsReadable);
		Assert.Same(display.LongDateFormat, display.LongDateFormat);
	}

	/// <summary>English's names, as Excel en-US shows them.</summary>
	[Fact]
	public void NamesInEnglish()
	{
		var names = DateNames.English;
		Assert.Equal("January", Text(names.Months[0]));
		Assert.Equal("December", Text(names.Months[11]));
		Assert.Equal("Sep", Text(names.AbbreviatedMonths[8]));
		Assert.Equal("J", Text(names.MonthLetters[0]));
		Assert.Equal("Sunday", Text(names.Days[0]));
		Assert.Equal("Sat", Text(names.AbbreviatedDays[6]));
		Assert.Equal("AM", Text(names.AM));
		Assert.Equal("PM", Text(names.PM));
		Assert.Equal(12, names.Months.Length);
		Assert.Equal(7, names.Days.Length);
	}

	/// <summary>German: its separators, its patterns as codes, its names, and AM and PM where NLS has no designators.</summary>
	[Fact]
	public void TakesGerman()
	{
		var display = DisplayCulture.For(CultureInfo.GetCultureInfo("de-DE"));
		Assert.False(display.IsInvariant);
		Assert.Equal(",", Text(display.DecimalSeparator));
		Assert.Equal(".", Text(display.GroupSeparator));
		Assert.Equal("dd\".\"mm\".\"yyyy", display.ShortDate);
		Assert.Equal("dddd, d\".\" mmmm yyyy", display.LongDate);
		Assert.Equal("hh:mm:ss", display.LongTime);
		Assert.Equal("Januar", Text(display.Names.Months[0]));
		Assert.Equal("Montag", Text(display.Names.Days[1]));
		Assert.Equal("AM", Text(display.Names.AM));
		Assert.Equal("PM", Text(display.Names.PM));
		Assert.Equal(new CurrencyPattern("€", 3, 8), display.Currency);
	}

	/// <summary>French: its group separator, a narrow no-break space in ICU and a no-break space in NLS.</summary>
	[Fact]
	public void TakesFrench()
	{
		var display = DisplayCulture.For(CultureInfo.GetCultureInfo("fr-FR"));
		Assert.Equal(",", Text(display.DecimalSeparator));
		Assert.Contains(Text(display.GroupSeparator), new[] { ((char)0x202F).ToString(), ((char)0xA0).ToString() });
		Assert.Equal("dd/mm/yyyy", display.ShortDate);
		Assert.Equal("janvier", Text(display.Names.Months[0]));
	}

	/// <summary>Japanese: its names, designators and patterns, whose text is quoted.</summary>
	[Fact]
	public void TakesJapanese()
	{
		var display = DisplayCulture.For(CultureInfo.GetCultureInfo("ja-JP"));
		Assert.Equal("yyyy/mm/dd", display.ShortDate);
		Assert.Equal("1月", Text(display.Names.Months[0]));
		Assert.Equal("午前", Text(display.Names.AM));
		Assert.Equal("午後", Text(display.Names.PM));
		Assert.Equal("月曜日", Text(display.Names.Days[1]));
		Assert.True(FormatCode.Compile(display.LongDate).IsReadable);
	}

	/// <summary>Russian's months: the nominative, in lower case in ICU and capitalized in NLS, as Excel shows them.</summary>
	[Fact]
	public void TakesRussianNominative()
	{
		var names = DateNames.Of(CultureInfo.GetCultureInfo("ru-RU").DateTimeFormat);
		Assert.Equal("январь", Text(names.Months[0]), ignoreCase: true);
		Assert.NotEqual("января", Text(names.Months[0]));
	}

	/// <summary>
	/// A locale code's language by its id or its tag, a tag by its longest prefix that is a culture the runtime has; none
	/// where the runtime has no data for it, so that the reader's culture names (Phase 8's step 8).
	/// </summary>
	[Fact]
	public void NamesALocaleCodesLanguage()
	{
		Assert.Same(DateNames.English, DateNames.ForLanguage(0x409));
		Assert.Same(DateNames.English, DateNames.ForLanguage(0x7F));
		Assert.Null(DateNames.ForLanguage(0x3FFF));
		Assert.Null(DateNames.ForLanguage(0x400));
		Assert.Same(DateNames.ForLanguage(0x407), DateNames.ForLanguage(0x407));
		Assert.Equal("Januar", Text(DateNames.ForLanguage(0x407)!.Months[0]));
		Assert.Equal("janvier", Text(DateNames.ForLanguage(0x40C)!.Months[0]));
		Assert.Equal("Januar", Text(DateNames.ForTag("de-DE")!.Months[0]));
		Assert.Same(DateNames.ForTag("de-DE"), DateNames.ForTag("DE-de"));
		Assert.Same(DateNames.ForTag("de-DE"), DateNames.ForTag("de-DE-x-foo"));
		Assert.Same(DateNames.ForTag("de"), DateNames.ForTag("de-ZZ"));
		Assert.Equal("Januar", Text(DateNames.ForTag("de-ZZ")!.Months[0]));
		Assert.Null(DateNames.ForTag("qq-ZZ"));
		Assert.Null(DateNames.ForTag("x-foo"));
		Assert.Null(DateNames.ForTag("not a tag"));
	}

	/// <summary>A .NET pattern becomes a code token by token, its separators and other text shown as written.</summary>
	/// <param name="pattern">The pattern.</param>
	/// <param name="expected">The code.</param>
	[Theory]
	[InlineData("M/d/yyyy", "m/d/yyyy")]
	[InlineData("dd.MM.yyyy", "dd\".\"mm\".\"yyyy")]
	[InlineData("yyyy年M月d日dddd", "yyyy\"年\"m\"月\"d\"日\"dddd")]
	[InlineData("tt h:mm:ss", "AM/PM h:mm:ss")]
	[InlineData("h:mm t", "h:mm A/P")]
	[InlineData("HH:mm", "hh:mm")]
	[InlineData("H:mm:ss", "h:mm:ss")]
	[InlineData("yyyy. M. d.", "yyyy\".\" m\".\" d\".\"")]
	[InlineData("dddd d MMMM yyyy 'г.'", "dddd d mmmm yyyy \"г.\"")]
	[InlineData("'Week' d", "\"Week\" d")]
	[InlineData("\\d d", "\"d\" d")]
	[InlineData("d 'x\"y'", "d \"x\"\\\"\"y\"")]
	[InlineData("y yy yyy yyyyy", "yy yy yyyy yyyy")]
	[InlineData("ddddd MMMMM", "dddd mmmm")]
	[InlineData("g yyyy z K f", " yyyy   ")]
	public void TurnsPatternsIntoCodes(string pattern, string expected)
	{
		var info = (DateTimeFormatInfo)CultureInfo.InvariantCulture.DateTimeFormat.Clone();
		info.DateSeparator = "/";
		info.TimeSeparator = ":";
		Assert.Equal(expected, DisplayCulture.Code(pattern, info));
		Assert.True(FormatCode.Compile(DisplayCulture.Code(pattern, info)).IsReadable);
	}

	/// <summary>A pattern's <c>/</c> and <c>:</c> are the culture's separators.</summary>
	[Fact]
	public void TakesTheSeparatorsPatternsStandFor()
	{
		var info = (DateTimeFormatInfo)CultureInfo.InvariantCulture.DateTimeFormat.Clone();
		info.DateSeparator = "-";
		info.TimeSeparator = ".";
		Assert.Equal("d-m-yyyy h\".\"mm", DisplayCulture.Code("d/M/yyyy H:mm", info));
	}

	/// <summary>Every culture the runtime has gives codes that read, for its dates, times and built-in ids.</summary>
	[Fact]
	public void EveryCultureGivesCodesThatRead()
	{
		var count = 0;
		foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures))
		{
			var display = DisplayCulture.For(culture);
			foreach (var code in new[] { display.ShortDate, display.LongDate, display.LongTime, display.BuiltIn(5), display.BuiltIn(8), display.BuiltIn(22), display.BuiltIn(44) })
			{
				Assert.True(FormatCode.Compile(code).IsReadable, $"{culture.Name}: {code}");
			}

			count++;
		}

		Assert.True(count > 100);
	}

	/// <summary>A provider that isn't a culture gives its number and date data.</summary>
	[Fact]
	public void TakesOtherProviders()
	{
		var numbers = (NumberFormatInfo)NumberFormatInfo.InvariantInfo.Clone();
		numbers.NumberDecimalSeparator = "·";
		numbers.NumberGroupSeparator = "'";
		var display = DisplayCulture.For(numbers);
		Assert.Equal("·", Text(display.DecimalSeparator));
		Assert.Equal("'", Text(display.GroupSeparator));
		Assert.False(display.IsInvariant);
	}

	/// <summary>Decodes UTF-8.</summary>
	/// <param name="bytes">The bytes.</param>
	/// <returns>The text.</returns>
	private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
