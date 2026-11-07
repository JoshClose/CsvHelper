using System.Globalization;
using System.Text;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// .NET's own texts read back: dates and times as <c>"O"</c> formats them, durations as <c>"c"</c> does, checked against
/// .NET's formatting of random values, and numbers in the invariant culture and others.
/// </summary>
public class DotNetTextTests
{
	[Fact]
	public void ReadsDateTimesOfEachKind()
	{
		var clock = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Unspecified).AddTicks(1234567);

		foreach (var kind in new[] { DateTimeKind.Unspecified, DateTimeKind.Utc, DateTimeKind.Local })
		{
			var value = DateTime.SpecifyKind(clock, kind);

			Assert.True(DotNetText.TryDateTime(Utf8(value.ToString("O", CultureInfo.InvariantCulture)), out var ticks, out _));
			Assert.Equal(clock.Ticks, ticks);
		}
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(1, 30)]
	[InlineData(-5, 0)]
	[InlineData(14, 0)]
	[InlineData(-14, 0)]
	[InlineData(-3, -30)]
	public void ReadsDateTimeOffsets(int hours, int minutes)
	{
		var offset = new TimeSpan(hours, minutes, 0);
		var value = new DateTimeOffset(2024, 1, 15, 10, 30, 45, offset).AddTicks(7);

		Assert.True(DotNetText.TryDateTime(Utf8(value.ToString("O", CultureInfo.InvariantCulture)), out var ticks, out var read));
		Assert.Equal(value.DateTime.Ticks, ticks);
		Assert.Equal(offset.Ticks, read);
		Assert.Equal(value.UtcDateTime.Ticks, ticks - read);
	}

	[Fact]
	public void ReadsRandomDatesAndTimesAsDotNetFormatsThem()
	{
		var random = new Random(2);
		for (var n = 0; n < 20000; n++)
		{
			// Away from the ends, where an offset would take the UTC time out of range.
			var ticks = TimeSpan.TicksPerDay + (long)(random.NextDouble() * (DateTime.MaxValue.Ticks - (2 * TimeSpan.TicksPerDay)));
			var value = new DateTime(ticks);
			var offset = new DateTimeOffset(value, TimeSpan.FromMinutes(random.Next(-14 * 60, (14 * 60) + 1))).ToString("O", CultureInfo.InvariantCulture);

			Assert.True(DotNetText.TryDateTime(Utf8(value.ToString("O", CultureInfo.InvariantCulture)), out var read, out _));
			Assert.Equal(ticks, read);
			Assert.True(DotNetText.TryDate(Utf8(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), out var day));
			Assert.Equal(value.Date.Ticks, day);
			Assert.True(DotNetText.TryTime(Utf8(value.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture)), out var time));
			Assert.Equal(value.TimeOfDay.Ticks, time);
			Assert.True(DotNetText.TryDateTime(Utf8(offset), out _, out _), offset);
		}
	}

	[Theory]
	[InlineData("2024-01-15T10:30:00.000000")]
	[InlineData("2024-01-15 10:30:00.0000000")]
	[InlineData("2024-01-15T10:30:00.0000000z")]
	[InlineData("2024-01-15T10:30:00.0000000+01")]
	[InlineData("2024-01-15T10:30:00.0000000+15:00")]
	[InlineData("2024-01-15T10:30:00.0000000+01:60")]
	[InlineData("2024-01-15T10:30:00.0000000*01:00")]
	[InlineData("2024-01-15T10:30:00.0000000+01:00 ")]
	[InlineData("2024-13-15T10:30:00.0000000")]
	[InlineData("2024-02-30T10:30:00.0000000")]
	[InlineData("2023-02-29T10:30:00.0000000")]
	[InlineData("0000-01-01T00:00:00.0000000")]
	[InlineData("2024-01-15T24:00:00.0000000")]
	[InlineData("2024-01-15T10:60:00.0000000")]
	[InlineData("2024-01-15T10:30:60.0000000")]
	[InlineData("2024-01-15T10:30:00,0000000")]
	[InlineData("2024-01-15T10:30:00.000000x")]
	[InlineData("15/01/2024 10:30:00")]
	[InlineData("")]
	public void RefusesOtherDateTimeTexts(string text)
	{
		Assert.False(DotNetText.TryDateTime(Utf8(text), out _, out _));
	}

	[Theory]
	[InlineData("2024-1-15")]
	[InlineData("2024/01/15")]
	[InlineData("2024-01-15T")]
	[InlineData("2024-00-15")]
	[InlineData("2024-01-00")]
	[InlineData("2024-01-32")]
	[InlineData("１９00-01-01")]
	public void RefusesOtherDates(string text)
	{
		Assert.False(DotNetText.TryDate(Utf8(text), out _));
	}

	[Fact]
	public void ReadsLeapDays()
	{
		Assert.True(DotNetText.TryDate(Utf8("2024-02-29"), out var ticks));
		Assert.Equal(new DateTime(2024, 2, 29).Ticks, ticks);
		Assert.False(DotNetText.TryDate(Utf8("1900-02-29"), out _));
	}

	[Theory]
	[InlineData("10:30:00")]
	[InlineData("10:30:00.000000")]
	[InlineData("10:30:00.00000000")]
	[InlineData("23:59:60.0000000")]
	[InlineData("10-30-00.0000000")]
	public void RefusesOtherTimes(string text)
	{
		Assert.False(DotNetText.TryTime(Utf8(text), out _));
	}

	[Fact]
	public void ReadsRandomDurationsAsDotNetFormatsThem()
	{
		var random = new Random(3);
		var values = new List<TimeSpan> { TimeSpan.Zero, TimeSpan.MaxValue, TimeSpan.MinValue, TimeSpan.FromTicks(1), TimeSpan.FromTicks(-1), TimeSpan.FromDays(1), TimeSpan.FromDays(-1) };
		for (var n = 0; n < 20000; n++)
		{
			// Durations of every size: up to minutes, days or the whole range, either way.
			var scale = random.Next(3) switch { 0 => TimeSpan.TicksPerHour, 1 => TimeSpan.TicksPerDay * 1000, _ => long.MaxValue };
			var ticks = (long)((random.NextDouble() - 0.5) * 2 * scale);
			values.Add(TimeSpan.FromTicks(ticks));
		}

		foreach (var value in values)
		{
			var text = value.ToString("c", CultureInfo.InvariantCulture);

			Assert.True(DotNetText.TryDuration(Utf8(text), out var ticks), text);
			Assert.Equal(value.Ticks, ticks);
		}
	}

	[Theory]
	[InlineData("1:00:00")]
	[InlineData("01:00")]
	[InlineData("01:00:00.")]
	[InlineData("01:00:00.000")]
	[InlineData("1.01:00:00.00000000")]
	[InlineData("1:01:00:00")]
	[InlineData("10675200.00:00:00")]
	[InlineData("10675199.02:48:05.4775808")]
	[InlineData("-10675199.02:48:05.4775809")]
	[InlineData("123456789.00:00:00")]
	[InlineData("1.24:00:00")]
	[InlineData("--01:00:00")]
	[InlineData("+01:00:00")]
	[InlineData("")]
	public void RefusesOtherDurations(string text)
	{
		Assert.False(DotNetText.TryDuration(Utf8(text), out _));
	}

	[Theory]
	[InlineData("0", 0)]
	[InlineData("-0", 0)]
	[InlineData("7", 1)]
	[InlineData("-123", 3)]
	[InlineData("1.5", 2)]
	[InlineData("0.10", 1)]
	[InlineData("1234.50", 5)]
	[InlineData("0.00120", 2)]
	[InlineData("10000000000000000000", 1)]
	[InlineData("12345678901234567890", 19)]
	[InlineData("1E-05", 1)]
	[InlineData("1.5E+20", 2)]
	[InlineData("-2.2250738585072014E-308", 17)]
	[InlineData("1E5", 1)]
	public void ReadsInvariantNumbers(string text, int significant)
	{
		var bytes = Utf8(text);
		var destination = new byte[DotNetText.MaxNumberLength];

		Assert.Equal(bytes.Length, DotNetText.ToInvariantNumber(bytes, null, destination, out var digits));
		Assert.Equal(text, Encoding.UTF8.GetString(destination, 0, bytes.Length));
		Assert.Equal(significant, digits);
	}

	[Theory]
	[InlineData("0042")]
	[InlineData("00")]
	[InlineData("-")]
	[InlineData("+5")]
	[InlineData("5.")]
	[InlineData(".5")]
	[InlineData("1,234.50")]
	[InlineData("1 234")]
	[InlineData("1e5")]
	[InlineData("1E")]
	[InlineData("1E+")]
	[InlineData("1.5.5")]
	[InlineData("NaN")]
	[InlineData("Infinity")]
	[InlineData("-Infinity")]
	[InlineData("0x10")]
	[InlineData(" 5")]
	[InlineData("5 ")]
	[InlineData("")]
	public void RefusesOtherNumbers(string text)
	{
		Assert.Equal(-1, DotNetText.ToInvariantNumber(Utf8(text), null, new byte[DotNetText.MaxNumberLength], out _));
	}

	[Fact]
	public void RefusesNumbersLongerThanItReads()
	{
		var text = "1." + new string('0', DotNetText.MaxNumberLength);

		Assert.Equal(-1, DotNetText.ToInvariantNumber(Utf8(text), null, new byte[DotNetText.MaxNumberLength], out _));
	}

	[Theory]
	[InlineData("de-DE", -1234.5, "-1234.5")]
	[InlineData("de-DE", 1.5E+20, "1.5E+20")]
	[InlineData("de-DE", 1E-05, "1E-05")]
	[InlineData("fr-FR", -0.25, "-0.25")]
	[InlineData("en-US", 1234.5, "1234.5")]
	public void ReadsCulturesNumbers(string name, double value, string invariant)
	{
		var culture = CultureInfo.GetCultureInfo(name);
		var text = Utf8(value.ToString(culture));
		var destination = new byte[DotNetText.MaxNumberLength];

		var length = DotNetText.ToInvariantNumber(text, NumberCulture.For(culture), destination, out _);

		Assert.Equal(invariant, Encoding.UTF8.GetString(destination, 0, length));
	}

	[Fact]
	public void ReadsSignsOfMoreThanOneCharacter()
	{
		// A culture whose signs aren't ASCII, as some are on ICU (sv-SE's minus sign), and whose separator is two characters.
		var format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
		format.NegativeSign = ((char)0x2212).ToString();
		format.PositiveSign = "pos";
		format.NumberDecimalSeparator = "::";
		var destination = new byte[DotNetText.MaxNumberLength];

		foreach (var (value, invariant) in new[] { (-1.5, "-1.5"), (1.5E+20, "1.5E+20"), (-1E-05, "-1E-05"), (42.0, "42") })
		{
			var length = DotNetText.ToInvariantNumber(Utf8(value.ToString(format)), NumberCulture.For(format), destination, out _);

			Assert.Equal(invariant, Encoding.UTF8.GetString(destination, 0, length));
		}
	}

	[Theory]
	[InlineData("1.234")]
	[InlineData("1.234,5")]
	[InlineData("-1")]
	[InlineData("1,5e5")]
	[InlineData("0042")]
	public void RefusesWhatTheCultureDoesNotWrite(string text)
	{
		// de-DE writes 1234,5 and 1,5E+20; a point is its group separator, and its minus sign is '-', so "-1" reads but
		// grouping does not.
		var culture = NumberCulture.For(CultureInfo.GetCultureInfo("de-DE"));
		var length = DotNetText.ToInvariantNumber(Utf8(text), culture, new byte[DotNetText.MaxNumberLength], out _);

		Assert.Equal(text == "-1" ? 2 : -1, length);
	}

	[Fact]
	public void TakesInvariantSignsAsTheInvariantCulture()
	{
		Assert.Null(NumberCulture.For(CultureInfo.InvariantCulture));
		Assert.Null(NumberCulture.For(CultureInfo.GetCultureInfo("en-US")));
		Assert.NotNull(NumberCulture.For(CultureInfo.GetCultureInfo("de-DE")));
	}

	[Fact]
	public void ReadsRandomNumbersAsDotNetFormatsThem()
	{
		var random = new Random(5);
		var cultures = new[] { "de-DE", "fr-FR", "sv-SE", "ar-SA", "fa-IR", "en-US" }.Select(CultureInfo.GetCultureInfo).ToArray();
		var destination = new byte[DotNetText.MaxNumberLength];
		for (var n = 0; n < 20000; n++)
		{
			var bits = (long)(random.NextDouble() * long.MaxValue) ^ ((long)random.Next() << 40);
			var value = BitConverter.Int64BitsToDouble(bits);
			if (double.IsNaN(value) || double.IsInfinity(value))
			{
				continue;
			}

			var culture = cultures[random.Next(cultures.Length)];
			var text = value.ToString(culture);
			var length = DotNetText.ToInvariantNumber(Utf8(text), NumberCulture.For(culture), destination, out _);

			Assert.True(length > 0, $"{culture.Name}: {text}");
			// .NET Framework writes 15 digits, which needn't read back as the value, so the two texts are compared.
			Assert.Equal(double.Parse(text, culture), double.Parse(Encoding.UTF8.GetString(destination, 0, length), CultureInfo.InvariantCulture));
		}
	}

	/// <summary>Text as UTF-8.</summary>
	/// <param name="text">The text.</param>
	/// <returns>Its bytes.</returns>
	private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
