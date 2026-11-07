using System;

namespace CsvHelper.Excel;

/// <summary>
/// Dates, times and durations as serial numbers in Excel's 1900 date system, through whole milliseconds: each value is
/// rounded to the millisecond (half away from zero), as Excel shows it and Phase 2 reads it, so that a write and a read
/// give the same value to the millisecond. A count of milliseconds is exact in a <see cref="double"/>, and one division
/// by the milliseconds of a day gives the nearest serial, which reads back to the same millisecond.
/// </summary>
internal static class Serial
{
	/// <summary>The milliseconds of a day.</summary>
	public const long MillisecondsPerDay = 86_400_000;

	private const long TicksPerMillisecond = 10_000;

	// Milliseconds from 0001-01-01: Excel's first date, serial 1; the first day after its 1900-02-29, which never existed;
	// the days that serials before and after it count from; and DateTime's last millisecond.
	private static readonly long January1900 = new DateTime(1900, 1, 1).Ticks / TicksPerMillisecond;
	private static readonly long March1900 = new DateTime(1900, 3, 1).Ticks / TicksPerMillisecond;
	private static readonly long Before = new DateTime(1899, 12, 31).Ticks / TicksPerMillisecond;
	private static readonly long After = new DateTime(1899, 12, 30).Ticks / TicksPerMillisecond;
	private static readonly long Last = DateTime.MaxValue.Ticks / TicksPerMillisecond;

	/// <summary>
	/// A date and time's serial, in milliseconds: from 1899-12-31 (so that 1900-01-01 is day 1) before 1900-03-01, and
	/// from 1899-12-30 from then on, past the 1900-02-29 that Excel counts. A time within half a millisecond of the end of
	/// 9999-12-31 is its last millisecond, the last Excel shows.
	/// </summary>
	/// <param name="ticks">The date and time's ticks, which may be negative or past the last date (a UTC time off its offset).</param>
	/// <returns>The milliseconds, or -1 for a time before 1900-01-01.</returns>
	public static long DateMilliseconds(long ticks)
	{
		var milliseconds = Math.Min(Round(ticks), Last);
		if (milliseconds < January1900)
		{
			return -1;
		}

		return milliseconds - (milliseconds < March1900 ? Before : After);
	}

	/// <summary>
	/// A time of day's serial (a fraction of a day), in milliseconds. A time within half a millisecond of midnight's end is
	/// its last millisecond, since a time of day can't be 24:00.
	/// </summary>
	/// <param name="ticks">The ticks since midnight, less than a day's.</param>
	/// <returns>The milliseconds.</returns>
	public static long TimeMilliseconds(long ticks) => Math.Min(Round(ticks), MillisecondsPerDay - 1);

	/// <summary>A duration's serial (days), in milliseconds.</summary>
	/// <param name="ticks">The duration's ticks.</param>
	/// <returns>The milliseconds, negative for a negative duration of half a millisecond or more.</returns>
	public static long DurationMilliseconds(long ticks) => ticks >= 0 ? Round(ticks) : ticks <= -5000 ? -1 : 0;

	/// <summary>A serial in days, from its milliseconds.</summary>
	/// <param name="milliseconds">The milliseconds.</param>
	/// <returns>The days.</returns>
	public static double Days(long milliseconds) => milliseconds / (double)MillisecondsPerDay;

	/// <summary>Ticks rounded to the millisecond, half away from zero; negative ticks toward zero, which only need to stay negative.</summary>
	/// <param name="ticks">The ticks.</param>
	/// <returns>The milliseconds.</returns>
	private static long Round(long ticks) => (ticks / TicksPerMillisecond) + (ticks % TicksPerMillisecond >= TicksPerMillisecond / 2 ? 1 : 0);
}
