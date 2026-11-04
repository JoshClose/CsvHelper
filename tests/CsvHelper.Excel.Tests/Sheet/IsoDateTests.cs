using System.Globalization;
using System.Text;
using CsvHelper.Excel.Tests.Corpus;

namespace CsvHelper.Excel.Tests.Sheet;

/// <summary>
/// The ISO 8601 text of <c>t="d"</c> cells as serial numbers: Excel's Strict forms to the bit of the serial, both date
/// systems at their limits, and the forms that aren't dates, against <see cref="IsoSerials"/>' independent arithmetic.
/// </summary>
public class IsoDateTests
{
	[Theory]
	[InlineData("2024-01-02", false, "45293")]
	[InlineData("2024-01-02T03:04:05.00000006519257800", false, "45293.127835648149")]
	[InlineData("03:04:05.00000000000072700", false, "0.12783564814814816")]
	[InlineData("2024-12-31", false, "45657")]
	[InlineData("1899-12-31", false, "0")]
	[InlineData("1900-01-01", false, "1")]
	[InlineData("1900-02-28", false, "59")]
	[InlineData("1900-03-01", false, "61")]
	[InlineData("9999-12-31", false, "2958465")]
	[InlineData("1904-01-01", true, "0")]
	[InlineData("1904-01-02", true, "1")]
	[InlineData("2024-01-02", true, "43831")]
	[InlineData("9999-12-31", true, "2957003")]
	[InlineData("12:00", false, "0.5")]
	[InlineData("12:00", true, "0.5")]
	[InlineData("2024-01-02T12:00", false, "45293.5")]
	[InlineData("2024-01-02T12:00:00Z", false, "45293.5")]
	[InlineData("12:00:00.5Z", false, "0.500005787037037037037")]
	[InlineData(" \r\n2024-01-02\t", false, "45293")]
	[InlineData("2024-02-29", false, "45351")]
	public void ConvertsTheFormsExcelWrites(string text, bool date1904, string serial)
	{
		// The Strict forms are those of excel/strict.xlsx, whose serials are in excel/basic.xlsx: 17 digits each.
		Assert.True(IsoDate.TryParse(Encoding.UTF8.GetBytes(text), date1904, out var value));
		Assert.Equal(double.Parse(serial, CultureInfo.InvariantCulture), value);
	}

	[Theory]
	[InlineData("2024-01-02+01:00", false)]
	[InlineData("2024-01-02T03:04:05+01:00", false)]
	[InlineData("2024-01-02T03:04:05-0500", false)]
	[InlineData("2024-W01-1", false)]
	[InlineData("2024-002", false)]
	[InlineData("20240102", false)]
	[InlineData("2024-01-02T24:00", false)]
	[InlineData("24:00", false)]
	[InlineData("2023-02-29", false)]
	[InlineData("2024-13-01", false)]
	[InlineData("2024-00-10", false)]
	[InlineData("2024-01-32", false)]
	[InlineData("2024-01-02T03:04:60", false)]
	[InlineData("03:60", false)]
	[InlineData("03:04:05.", false)]
	[InlineData("03:04:05,5", false)]
	[InlineData("3:04", false)]
	[InlineData("03:4", false)]
	[InlineData("2024-1-02", false)]
	[InlineData("2024-01-02 03:04:05", false)]
	[InlineData("2024-01-02T", false)]
	[InlineData("2024-01-02ZZ", false)]
	[InlineData("2024-01-02T03:04:05.5.5", false)]
	[InlineData("-2024-01-02", false)]
	[InlineData("+2024-01-02", false)]
	[InlineData("0000-01-01", false)]
	[InlineData("1899-12-30", false)]
	[InlineData("1903-12-31", true)]
	[InlineData("", false)]
	[InlineData("Z", false)]
	[InlineData("text", false)]
	[InlineData("P", false)]
	[InlineData("PT", false)]
	[InlineData("P1DT", false)]
	[InlineData("PT1D", false)]
	[InlineData("P1H", false)]
	[InlineData("PT1H1H", false)]
	[InlineData("PT1M1H", false)]
	[InlineData("PT1S1M", false)]
	[InlineData("PT1.5H", false)]
	[InlineData("PT1.0M", false)]
	[InlineData("PT1.S", false)]
	[InlineData("PT.5S", false)]
	[InlineData("PT1HT1M", false)]
	[InlineData("-PT1H", false)]
	[InlineData("PT1HZ", false)]
	[InlineData("P1Y", false)]
	[InlineData("P1M", false)]
	[InlineData("P1W", false)]
	[InlineData("PT 1H", false)]
	[InlineData("pt1h", false)]
	[InlineData("PT1234567890123456H", false)]
	public void LeavesOtherTextAsText(string text, bool date1904) => Assert.False(IsoDate.TryParse(Encoding.UTF8.GetBytes(text), date1904, out _));

	[Theory]
	[InlineData("PT36H0M0.000S", "1.5")]
	[InlineData("PT2160M0.000S", "1.5")]
	[InlineData("PT129600.000S", "1.5")]
	[InlineData("PT1087356H0M0.000S", "45306.5")]
	[InlineData("PT2400H0M0.000S", "100")]
	[InlineData("PT0H0M1.000S", "1.1574074074074073E-05")]
	[InlineData("PT1H", "0.041666666666666664")]
	[InlineData("PT0.5S", "5.787037037037037E-06")]
	[InlineData("P1D", "1")]
	[InlineData("P1DT12H", "1.5")]
	[InlineData(" PT36H\r\n", "1.5")]
	[InlineData("PT999999999999999H", "41666666666666.625")]
	public void ConvertsTheDurationsExcelWrites(string text, string serial)
	{
		// The days of a duration, the same in either date system: Excel's Strict files write elapsed-time cells so.
		foreach (var date1904 in new[] { false, true })
		{
			Assert.True(IsoDate.TryParse(Encoding.UTF8.GetBytes(text), date1904, out var value));
			Assert.Equal(double.Parse(serial, CultureInfo.InvariantCulture), value);
			Assert.Equal(IsoSerials.Of(text.Trim(), date1904), IsoSerials.Shortest(value));
		}
	}

	[Fact]
	public void MatchesIndependentArithmetic()
	{
		var random = new Random(20261103);
		for (var i = 0; i < 5000; i++)
		{
			var date1904 = random.Next(2) == 0;
			var first = date1904 ? new DateTime(1904, 1, 1) : new DateTime(1899, 12, 31);
			var day = first.AddDays(random.Next(0, (int)(new DateTime(9999, 12, 31) - first).TotalDays + 1));
			var fraction = new string(Enumerable.Range(0, random.Next(0, 26)).Select(_ => (char)('0' + random.Next(10))).ToArray());
			var time = $"{random.Next(24):00}:{random.Next(60):00}:{random.Next(60):00}" + (fraction.Length > 0 ? "." + fraction : "");
			var text = random.Next(4) switch
			{
				0 => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
				1 => time,
				_ => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T" + time,
			};

			Assert.True(IsoDate.TryParse(Encoding.UTF8.GetBytes(text), date1904, out var serial), text);
			Assert.True(IsoSerials.Of(text, date1904) == IsoSerials.Shortest(serial), $"{text} ({(date1904 ? 1904 : 1900)}): {serial:R}");
		}
	}

	[Fact]
	public void MatchesIndependentArithmeticForDurations()
	{
		var random = new Random(20261104);
		for (var i = 0; i < 2000; i++)
		{
			var fraction = new string(Enumerable.Range(0, random.Next(0, 26)).Select(_ => (char)('0' + random.Next(10))).ToArray());
			var parts = new[]
			{
				random.Next(3) == 0 ? $"{random.Next(100000)}D" : "",
				random.Next(2) == 0 ? $"{random.Next(10_000_000)}H" : "",
				random.Next(2) == 0 ? $"{random.Next(100_000)}M" : "",
				random.Next(2) == 0 ? $"{random.Next(1_000_000)}" + (fraction.Length > 0 ? "." + fraction : "") + "S" : "",
			};
			var time = parts[1] + parts[2] + parts[3];
			if (parts[0].Length == 0 && time.Length == 0)
			{
				continue;
			}

			var text = "P" + parts[0] + (time.Length > 0 ? "T" + time : "");
			Assert.True(IsoDate.TryParse(Encoding.UTF8.GetBytes(text), false, out var serial), text);
			Assert.True(IsoSerials.Of(text, false) == IsoSerials.Shortest(serial), $"{text}: {serial:R}");
		}
	}

	[Fact]
	public void ReadsAbsurdTextsWithoutGrowing()
	{
		// A fraction of 10,000 digits counts its first 20, and the rest must be digits; a year of 10,000 digits isn't a date.
		var fraction = "03:04:05.1" + new string('9', 10_000);
		Assert.True(IsoDate.TryParse(Encoding.UTF8.GetBytes(fraction), false, out var serial));
		Assert.True(IsoDate.TryParse(Encoding.UTF8.GetBytes("03:04:05.1" + new string('9', 19)), false, out var short20));
		Assert.Equal(short20, serial);
		Assert.False(IsoDate.TryParse(Encoding.UTF8.GetBytes(fraction + "x"), false, out _));
		Assert.False(IsoDate.TryParse(Encoding.UTF8.GetBytes(new string('2', 10_000) + "-01-02"), false, out _));
	}
}
