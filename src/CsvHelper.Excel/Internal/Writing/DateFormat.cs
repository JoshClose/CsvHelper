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
	/// <summary>The month of each sample value of <see cref="Width"/>: every month, so that the longest name is among them.</summary>
	private const int Months = 12;

	// The .NET pattern and its provider, to read a field's text back.
	private readonly string pattern;
	private readonly IFormatProvider provider;
	// The width its column needs, once measured; -1 before.
	private double width = -1;

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
	/// The width a column needs for the format's widest text (<see cref="ColumnWidths.Of"/>), or 0 when the default column
	/// fits it: of sample values on every month and every day of the week, with two-digit days and hours, in the morning
	/// (<c>AM</c> is wider than <c>PM</c> in Calibri, and the formats Excel can show have no other designators).
	/// </summary>
	public double Width
	{
		get
		{
			if (width < 0)
			{
				width = Measure();
			}

			return width;
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

	/// <summary>Measures the widest text of the format over its sample values.</summary>
	/// <returns>The width, or 0.</returns>
	private double Measure()
	{
		var widest = 0.0;
		for (var month = 1; month <= Months; month++)
		{
			// Seven days in a row are every day of the week; all have two digits.
			for (var day = 22; day <= 28; day++)
			{
				var text = new DateTime(2024, month, day, 11, 58, 58, 888).ToString(pattern, provider);
				widest = Math.Max(widest, ColumnWidths.Of(text.AsSpan()));
			}
		}

		return widest;
	}
}
