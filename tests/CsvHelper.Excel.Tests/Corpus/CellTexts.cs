using System.Globalization;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// The text a reader's text accessors give a cell (<c>GetString</c>), worked out here from the cell's stored form with
/// .NET's own date arithmetic, apart from CsvHelper's: a boolean is <c>TRUE</c> or <c>FALSE</c>, a date ISO 8601 and a
/// duration <c>[h]:mm:ss</c> (Phase 2's "Text of date and duration cells").
/// </summary>
internal static class CellTexts
{
	private const long MillisecondsPerDay = 86_400_000;

	/// <summary>The text of a cell.</summary>
	/// <param name="cell">The cell, as <see cref="CorpusFiles.Read"/> gives it.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <returns>The text.</returns>
	public static string Of(CellValue cell, bool date1904) => cell.Kind switch
	{
		SheetCellKind.Bool => cell.Text switch
		{
			"1" => "TRUE",
			"0" => "FALSE",
			_ => cell.Text,
		},
		SheetCellKind.DateNumber => Date(cell.Text, date1904) ?? cell.Text,
		SheetCellKind.Duration => Duration(cell.Text) ?? cell.Text,
		_ => cell.Text,
	};

	/// <summary>
	/// The text of a date cell: <c>yyyy-MM-dd</c>, <c>HH:mm:ss</c> for a serial below 1, or both, with milliseconds when
	/// there are any. The 1900 date system is the OLE Automation date from serial 61 on and a day later before 60, whose
	/// 1900-02-29 never existed; the 1904 system counts from 1904-01-01.
	/// </summary>
	/// <param name="stored">The serial.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <returns>The text, or <see langword="null"/> for a serial that isn't a date.</returns>
	private static string? Date(string stored, bool date1904)
	{
		if (!double.TryParse(stored, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) || serial < 0)
		{
			return null;
		}

		var milliseconds = (long)Math.Round(serial * MillisecondsPerDay, MidpointRounding.AwayFromZero);
		var day = milliseconds / MillisecondsPerDay;
		var time = TimeSpan.FromMilliseconds(milliseconds % MillisecondsPerDay);
		var timeText = new DateTime(time.Ticks).ToString("HH:mm:ss.FFF", CultureInfo.InvariantCulture);
		if (day == 0)
		{
			return timeText;
		}

		string dayText;
		if (!date1904 && day == 60)
		{
			dayText = "1900-02-29";
		}
		else
		{
			var oleDay = date1904 ? day + 1462 : day < 60 ? day + 1 : day;
			if (oleDay >= 2958466)
			{
				return null;
			}

			dayText = DateTime.FromOADate(oleDay).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		}

		return time == TimeSpan.Zero ? dayText : dayText + "T" + timeText;
	}

	/// <summary>The text of a duration cell: hours of at least two digits, minutes, seconds, and milliseconds when there are any.</summary>
	/// <param name="stored">The number of days.</param>
	/// <returns>The text, or <see langword="null"/> for a number that isn't a duration.</returns>
	private static string? Duration(string stored)
	{
		if (!double.TryParse(stored, NumberStyles.Float, CultureInfo.InvariantCulture, out var days) || Math.Abs(days) > TimeSpan.MaxValue.TotalDays - 1)
		{
			return null;
		}

		var span = TimeSpan.FromMilliseconds(Math.Round(days * MillisecondsPerDay, MidpointRounding.AwayFromZero));
		var sign = span < TimeSpan.Zero ? "-" : "";
		span = span.Duration();
		var fraction = span.Milliseconds == 0 ? "" : "." + span.Milliseconds.ToString("000", CultureInfo.InvariantCulture).TrimEnd('0');

		return FormattableString.Invariant($"{sign}{(long)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}{fraction}");
	}
}
