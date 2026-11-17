using System;
using System.Globalization;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>
/// A .NET date and time format that Excel can show, as <see cref="FormatTranslator"/> made it: the Excel code, and the
/// reading back of a field's text, so that a cell holds what its text shows.
/// </summary>
/// <remarks>
/// A field formatted with the format is parsed with it (<see cref="DateTime.TryParseExact(string, string, IFormatProvider, DateTimeStyles, out DateTime)"/>),
/// and the value formatted again: only the same text gives a cell, of the value the text shows (<c>yyyy-MM</c> drops the
/// day). The format is the custom pattern of a standard format (<c>d</c> is the culture's short date), which formats a
/// <see cref="DateTime"/>, a <see cref="DateTimeOffset"/>'s clock time, a <c>DateOnly</c> and a <c>TimeOnly</c> alike.
/// </remarks>
internal sealed class DateFormat
{
	/// <summary>A day of a leap year at noon, which <see cref="FormatWidth"/> measures a date's or time's code at: its widest over the year.</summary>
	private const double LeapDay = 45292.5;

	// The .NET pattern and its provider, to read a field's text back.
	private readonly string pattern;
	private readonly IFormatProvider provider;
	// The pixels of its widest text, once measured; -1 before.
	private double pixels = -1;

	/// <summary>Creates a translated format.</summary>
	/// <param name="code">The Excel code, which <see cref="FormatCheck"/> passes.</param>
	/// <param name="pattern">The .NET custom pattern the field was formatted with (the expansion of a standard format).</param>
	/// <param name="provider">The provider it was formatted with: the invariant culture for <c>R</c>, <c>s</c> and <c>u</c>.</param>
	/// <param name="hasDate">Whether the pattern has a date, rather than only a time of day.</param>
	public DateFormat(string code, string pattern, IFormatProvider provider, bool hasDate)
	{
		Code = code;
		this.pattern = pattern;
		this.provider = provider;
		HasDate = hasDate;
	}

	/// <summary>The Excel code, such as <c>dd"."mm"."yyyy</c>.</summary>
	public string Code { get; }

	/// <summary>Whether the format has a date: without one, a cell holds a time of day, a fraction of a day.</summary>
	public bool HasDate { get; }

	/// <summary>
	/// The width a column needs for the format's widest text (<see cref="ColumnWidths.OfPixels"/> of <see cref="Pixels"/>),
	/// or 0 when the default column fits it.
	/// </summary>
	public double Width => ColumnWidths.OfPixels(Pixels);

	/// <summary>
	/// The pixels of the format's widest text (<see cref="ColumnWidths.TextPixels"/>), as Excel shows it: its code's widest
	/// over a leap year's days at 11:59:59 and 23:59:59 (<see cref="FormatWidth"/>).
	/// </summary>
	public double Pixels
	{
		get
		{
			if (pixels < 0)
			{
				pixels = Measure();
			}

			return pixels;
		}
	}

	/// <summary>
	/// Reads a field's text back: parses it with the format, and formats the value again, which must give the same text.
	/// </summary>
	/// <param name="utf8">The field's text.</param>
	/// <param name="milliseconds">
	/// The value's serial in milliseconds (<see cref="Serial"/>): of its date and time, or of its time of day when the format
	/// has no date; -1 for a date before 1900-01-01.
	/// </param>
	/// <returns>Whether the text is the format's text of a value.</returns>
	public bool TryRead(ReadOnlySpan<byte> utf8, out long milliseconds)
	{
		milliseconds = 0;
		var text = Encoding.UTF8.GetString(utf8.ToArray());
		if (!DateTime.TryParseExact(text, pattern, provider, DateTimeStyles.NoCurrentDateDefault, out var value) ||
			!string.Equals(value.ToString(pattern, provider), text, StringComparison.Ordinal))
		{
			return false;
		}

		milliseconds = HasDate ? Serial.DateMilliseconds(value.Ticks) : Serial.TimeMilliseconds(value.TimeOfDay.Ticks);

		return true;
	}

	/// <summary>Measures the widest text of the format's code, as Excel shows it.</summary>
	/// <returns>Its pixels.</returns>
	private double Measure() => new FormatWidth(Code).NumberPixels(LeapDay);
}
