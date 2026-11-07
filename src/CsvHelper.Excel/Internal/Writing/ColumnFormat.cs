using System;
using System.Globalization;

namespace CsvHelper.Excel;

/// <summary>
/// The format and culture that a column's member is written with (<see cref="MemberFormats"/>), for one sheet: a date or
/// time's text is read back with them, and a number's with the culture, so that a formatted value is still a typed cell.
/// </summary>
internal sealed class ColumnFormat
{
	// The translation of the format for the last kind of value it was asked for, and its cell format; -1 before.
	private DateFormat? translation;
	private CellKind translatedKind;
	private bool translated;
	private int style = -1;

	/// <summary>Creates a column's format.</summary>
	/// <param name="format">The member's <see cref="CsvFormatAttribute.Format"/>, or <see langword="null"/>.</param>
	/// <param name="culture">The member's <see cref="CsvCultureAttribute"/> culture, or <see langword="null"/> for the writer's.</param>
	/// <param name="writerProvider">The writer's format provider, which the member is formatted with without a culture.</param>
	public ColumnFormat(string? format, CultureInfo? culture, IFormatProvider writerProvider)
	{
		Format = string.IsNullOrEmpty(format) ? null : format;
		Provider = culture ?? writerProvider;
		HasCulture = culture is not null;
		Numbers = culture is null ? null : NumberCulture.For(culture);
	}

	/// <summary>The member's format, or <see langword="null"/> for none (an empty one is .NET's default).</summary>
	public string? Format { get; }

	/// <summary>The provider the member is formatted with: its culture, or the writer's.</summary>
	public IFormatProvider Provider { get; }

	/// <summary>Whether the member has a culture of its own, whose numbers are read with <see cref="Numbers"/>.</summary>
	public bool HasCulture { get; }

	/// <summary>The signs and separator of the member's culture's numbers, or <see langword="null"/> when they are the invariant ones.</summary>
	public NumberCulture? Numbers { get; }

	/// <summary>Whether a date or time of the column has a format other than .NET's default, <c>O</c>, which the sink reads without one.</summary>
	public bool HasDateFormat => Format is not null and not "O" and not "o";

	/// <summary>Whether a duration of the column has a format other than .NET's default, <c>c</c> (and its other names <c>t</c> and <c>T</c>).</summary>
	public bool HasDurationFormat => Format is not null and not "c" and not "t" and not "T";

	/// <summary>The translation of the format for a kind of value, made the first time it is asked for.</summary>
	/// <param name="kind">The kind: a date and time, a date or a time of day.</param>
	/// <returns>The translation, or <see langword="null"/> when Excel can't show the format's text.</returns>
	public DateFormat? Translation(CellKind kind)
	{
		if (!translated || translatedKind != kind)
		{
			translation = FormatTranslator.Translate(Format!, kind, Provider);
			translatedKind = kind;
			translated = true;
			style = -1;
		}

		return translation;
	}

	/// <summary>The cell format of the last translation, added to the workbook's the first time.</summary>
	/// <param name="styles">The workbook's cell formats.</param>
	/// <returns>Its index.</returns>
	public int Style(StyleRegistry styles)
	{
		if (style < 0)
		{
			style = styles.IndexOf(translation!.Code);
		}

		return style;
	}
}
