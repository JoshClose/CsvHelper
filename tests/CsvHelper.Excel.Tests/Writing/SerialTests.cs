using System.Globalization;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>
/// Dates, times and durations as serials of the 1900 date system: Excel's own serials of its first days, the rounding to
/// the millisecond, the ends of the range, and random values read back by Phase 2's conversion.
/// </summary>
public class SerialTests
{
	[Theory]
	[InlineData("1900-01-01", 1)]
	[InlineData("1900-01-31", 31)]
	[InlineData("1900-02-28", 59)]
	[InlineData("1900-03-01", 61)]
	[InlineData("1904-01-01", 1462)]
	[InlineData("2024-01-15", 45306)]
	[InlineData("9999-12-31", 2958465)]
	public void CountsDaysAsExcelDoes(string date, long serial)
	{
		var ticks = DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture).Ticks;

		Assert.Equal(serial * Serial.MillisecondsPerDay, Serial.DateMilliseconds(ticks));
	}

	[Fact]
	public void RefusesDatesBefore1900()
	{
		Assert.Equal(-1, Serial.DateMilliseconds(new DateTime(1899, 12, 31, 23, 59, 59, 999).Ticks));
		Assert.Equal(-1, Serial.DateMilliseconds(0));
		Assert.Equal(-1, Serial.DateMilliseconds(-TimeSpan.TicksPerDay));
		Assert.Equal(Serial.MillisecondsPerDay, Serial.DateMilliseconds(new DateTime(1900, 1, 1).Ticks));
	}

	[Fact]
	public void RoundsToTheMillisecondHalfUp()
	{
		var day = new DateTime(2024, 1, 15).Ticks;
		var serial = 45306 * Serial.MillisecondsPerDay;

		Assert.Equal(serial, Serial.DateMilliseconds(day + 4999));
		Assert.Equal(serial + 1, Serial.DateMilliseconds(day + 5000));
		Assert.Equal(serial + 1, Serial.DateMilliseconds(day + 14999));
		// Half a millisecond before 1900-01-01 rounds to it, and half a millisecond before 1900-03-01 rounds past 1900-02-29.
		Assert.Equal(Serial.MillisecondsPerDay, Serial.DateMilliseconds(new DateTime(1900, 1, 1).Ticks - 5000));
		Assert.Equal(-1, Serial.DateMilliseconds(new DateTime(1900, 1, 1).Ticks - 5001));
		Assert.Equal(61 * Serial.MillisecondsPerDay, Serial.DateMilliseconds(new DateTime(1900, 3, 1).Ticks - 5000));
	}

	[Fact]
	public void KeepsTheLastMillisecond()
	{
		var last = (2958466 * Serial.MillisecondsPerDay) - 1;

		Assert.Equal(last, Serial.DateMilliseconds(DateTime.MaxValue.Ticks));
		Assert.Equal(last, Serial.DateMilliseconds(DateTime.MaxValue.Ticks - 4999));
		Assert.Equal(last, Serial.DateMilliseconds(DateTime.MaxValue.Ticks + TimeSpan.TicksPerDay));
		Assert.Equal(Serial.MillisecondsPerDay - 1, Serial.TimeMilliseconds(TimeSpan.TicksPerDay - 1));
		Assert.Equal(Serial.MillisecondsPerDay - 1, Serial.TimeMilliseconds(TimeSpan.TicksPerDay - 5000));
		Assert.Equal(Serial.MillisecondsPerDay - 2, Serial.TimeMilliseconds(TimeSpan.TicksPerDay - 15001));
		Assert.Equal(0, Serial.TimeMilliseconds(4999));
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(4999, 0)]
	[InlineData(5000, 1)]
	[InlineData(-4999, 0)]
	[InlineData(-5000, -1)]
	[InlineData(long.MinValue, -1)]
	[InlineData(long.MaxValue, 922337203685478)]
	[InlineData(TimeSpan.TicksPerDay, Serial.MillisecondsPerDay)]
	public void RoundsDurations(long ticks, long milliseconds)
	{
		Assert.Equal(milliseconds, Serial.DurationMilliseconds(ticks));
	}

	[Fact]
	public void ReadsBackAsTheSameMillisecond()
	{
		// Random dates and times written as serials, through their shortest text, and read as Phase 2 reads them.
		var random = new Random(6);
		var first = new DateTime(1900, 1, 1).Ticks;
		var values = Enumerable.Range(0, 20000).Select(_ => new DateTime(first + (long)(random.NextDouble() * (DateTime.MaxValue.Ticks - first)))).ToList();
		using var stream = new MemoryStream();
		using (var workbook = ExcelWorkbookWriter.Create(stream, new ExcelWriterOptions { Csv = new CsvWriterOptions { HasHeader = false } }, leaveOpen: true))
		{
			using var writer = workbook.AddSheet("Dates");
			foreach (var value in values)
			{
				writer.WriteField(value);
				writer.EndRecord();
			}
		}

		using var book = ExcelWorkbook.Open(new MemoryStream(stream.ToArray()));
		using var reader = book.OpenReader(book.Sheets[0], Written.AllRows);
		foreach (var value in values)
		{
			Assert.True(reader.Read());
			var expected = new DateTime(Math.Min((value.Ticks + 5000) / 10000 * 10000, DateTime.MaxValue.Ticks / 10000 * 10000));
			Assert.Equal(expected, reader.Parse<DateTime>(0));
		}

		Assert.False(reader.Read());
	}
}