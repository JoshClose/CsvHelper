using System.Globalization;
using System.Text.RegularExpressions;

namespace CsvHelper.Excel.Tests.Corpus;

/// <summary>
/// The serial number of a <c>t="d"</c> cell's ISO 8601 text, worked out apart from CsvHelper.Excel's <c>IsoDate</c>: the
/// day from .NET's OLE Automation date, the time in <see cref="decimal"/> seconds, rounded once to a <see cref="double"/>.
/// </summary>
internal static class IsoSerials
{
	private static readonly Regex DateAndTime = new(@"^(\d{4}-\d{2}-\d{2})(?:T(\d{2}):(\d{2})(?::(\d{2})(?:\.(\d+))?)?)?Z?$");
	private static readonly Regex TimeAlone = new(@"^(\d{2}):(\d{2})(?::(\d{2})(?:\.(\d+))?)?Z?$");

	private static readonly Regex Duration = new(@"^P(?:(\d{1,15})D)?(?:T(?:(\d{1,15})H)?(?:(\d{1,15})M)?(?:(\d{1,15})(?:\.(\d+))?S)?)?$");

	/// <summary>The serial of an ISO 8601 date, date and time, time alone, or duration of days, hours, minutes and seconds.</summary>
	/// <param name="text">The text, without white space around it.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <returns>The serial in its shortest round-trip form, or <see langword="null"/> when the text isn't such a date.</returns>
	public static string? Of(string text, bool date1904)
	{
		if (Duration.Match(text) is { Success: true } duration)
		{
			// A T needs a part after it, and the whole needs a part.
			if (text.EndsWith("T", StringComparison.Ordinal) || !Enumerable.Range(1, 4).Any(g => duration.Groups[g].Success))
			{
				return null;
			}

			decimal Part(int group) => duration.Groups[group].Success ? decimal.Parse(duration.Groups[group].Value, CultureInfo.InvariantCulture) : 0;
			var fraction = duration.Groups[5].Success ? decimal.Parse("0." + (duration.Groups[5].Value.Length > 20 ? duration.Groups[5].Value.Substring(0, 20) : duration.Groups[5].Value), CultureInfo.InvariantCulture) : 0;
			var days = ((Part(1) * 86400) + (Part(2) * 3600) + (Part(3) * 60) + Part(4) + fraction) / 86400m;

			return Shortest(double.Parse(days.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
		}

		decimal day = 0;
		Match time;
		var date = DateAndTime.Match(text);
		if (date.Success)
		{
			if (!DateTime.TryParseExact(date.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
			{
				return null;
			}

			// The 1900 system counts 1900-02-29, so its days before March are a day later than OLE Automation's.
			var ole = (decimal)parsed.ToOADate();
			day = date1904 ? ole - 1462 : ole < 61 ? ole - 1 : ole;
			if (day < 0)
			{
				return null;
			}

			time = date;
		}
		else
		{
			time = TimeAlone.Match(text);
			if (!time.Success)
			{
				return null;
			}
		}

		var offset = date.Success ? 1 : 0;
		var hours = time.Groups[1 + offset];
		decimal seconds = 0;
		if (hours.Success)
		{
			var h = int.Parse(hours.Value, CultureInfo.InvariantCulture);
			var m = int.Parse(time.Groups[2 + offset].Value, CultureInfo.InvariantCulture);
			var s = time.Groups[3 + offset].Success ? int.Parse(time.Groups[3 + offset].Value, CultureInfo.InvariantCulture) : 0;
			if (h > 23 || m > 59 || s > 59)
			{
				return null;
			}

			var fraction = time.Groups[4 + offset];
			seconds = (h * 3600) + (m * 60) + s;
			if (fraction.Success)
			{
				var digits = fraction.Value.Length > 27 ? fraction.Value.Substring(0, 27) : fraction.Value;
				seconds += decimal.Parse("0." + digits, CultureInfo.InvariantCulture);
			}
		}

		var serial = day + (seconds / 86400m);

		return Shortest(double.Parse(serial.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
	}

	/// <summary>
	/// The shortest text that parses back to <paramref name="value"/>, as CsvHelper writes numbers: .NET Framework's
	/// <c>"R"</c> gives 17 digits where fewer would do.
	/// </summary>
	/// <param name="value">The number.</param>
	/// <returns>The text.</returns>
	public static string Shortest(double value)
	{
		for (var digits = 1; digits < 17; digits++)
		{
			var text = value.ToString("G" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
			if (double.Parse(text, CultureInfo.InvariantCulture) == value)
			{
				return text;
			}
		}

		return value.ToString("G17", CultureInfo.InvariantCulture);
	}
}
