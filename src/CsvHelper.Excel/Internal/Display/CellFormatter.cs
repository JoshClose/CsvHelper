using System;
using System.Globalization;
using System.Threading;

namespace CsvHelper.Excel;

/// <summary>
/// Core's <see cref="CsvCellFormatter"/> for a reader of a workbook under <see cref="ExcelText.Formatted"/>: a field's text
/// in its style's number format, as Excel shows it (<see cref="NumberText"/>, <see cref="DateText"/>), in the reader's
/// culture and the workbook's date system.
/// </summary>
/// <remarks>
/// <para>
/// A field's style is the index of its cell's number format among the workbook's distinct ones
/// (<see cref="StyleTable.NumberFormatIndex"/>), which the sheet scanner gives a field whose text the format can change
/// (<see cref="StyleOf"/>): every number, date, time and duration, and a text or a boolean whose format has a text section
/// that isn't <c>@</c> alone. Each format's code (the custom one, else the built-in id's in the culture) is compiled the
/// first time a field asks for it, and kept.
/// </para>
/// <para>
/// A field declines (its text is <see cref="ExcelText.Raw"/>'s) when its code can't be read, when Excel can't show its
/// value in it (a negative date, a value no section takes, a fraction too large, text past 255 characters), and when its
/// stored value isn't a number or a boolean as its kind says. Rendering allocates nothing.
/// </para>
/// <para>
/// One formatter serves a reader, its parallel workers among them, so it is safe to call from several threads: a code
/// compiled twice at once is the same code, and either is kept.
/// </para>
/// </remarks>
internal sealed class CellFormatter : CsvCellFormatter
{
	/// <summary>A format whose text section changes text and booleans.</summary>
	private const sbyte ChangesText = 1;

	/// <summary>A format that shows text and booleans as they are: without a text section, or with <c>@</c> alone.</summary>
	private const sbyte KeepsText = -1;

	private readonly StyleTable styles;
	private readonly DisplayCulture culture;
	private readonly bool date1904;
	// By number format index: its compiled code, and whether it changes text (0 until known).
	private readonly FormatCode?[] codes;
	private readonly sbyte[] texts;

	/// <summary>Creates the formatter of a workbook's reader.</summary>
	/// <param name="styles">The workbook's styles.</param>
	/// <param name="culture">The reader's culture (<see cref="DisplayCulture.For"/>).</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	/// <exception cref="ArgumentNullException"><paramref name="styles"/> or <paramref name="culture"/> is <see langword="null"/>.</exception>
	public CellFormatter(StyleTable styles, DisplayCulture culture, bool date1904)
	{
		ArgumentNullException.ThrowIfNull(styles);
		ArgumentNullException.ThrowIfNull(culture);
		this.styles = styles;
		this.culture = culture;
		this.date1904 = date1904;
		codes = new FormatCode?[styles.NumberFormatCount];
		texts = new sbyte[codes.Length];
	}

	/// <summary>
	/// The style a field takes, if its cell's number format can change its text: the format's index for a number, date,
	/// time or duration, and for a text or a boolean when the format has a text section that isn't <c>@</c> alone.
	/// </summary>
	/// <param name="kind">The field's kind.</param>
	/// <param name="cellStyle">The cell's <c>s</c>: an index into the cell formats.</param>
	/// <returns>The style, from 0 to <see cref="CsvCellFormatter.MaxStyle"/>; or -1 when the field's text is its own.</returns>
	public int StyleOf(SheetCellKind kind, int cellStyle)
	{
		// More distinct formats than a style can name: only a hostile workbook has them, and their cells read as Raw.
		var index = styles.NumberFormatIndex(cellStyle);
		if (index > MaxStyle)
		{
			return -1;
		}

		return kind switch
		{
			SheetCellKind.Number or SheetCellKind.DateNumber or SheetCellKind.Duration => index,
			SheetCellKind.Text or SheetCellKind.Bool => ShowsText(index) ? index : -1,
			_ => -1,
		};
	}

	/// <inheritdoc/>
	public override bool TryFormat(ReadOnlySpan<byte> value, CsvCellKind kind, int style, Span<byte> destination, out int length)
	{
		length = -1;
		if ((uint)style >= (uint)codes.Length)
		{
			return false;
		}

		var code = Code(style);
		var text = new ShownText(destination);
		bool shown;
		switch (kind)
		{
			case CsvCellKind.Number:
			case CsvCellKind.DateNumber:
			case CsvCellKind.Duration:
			{
				shown = CsvParse.TryDouble(value, CultureInfo.InvariantCulture, out var number) && NumberText.TryWrite(code, number, culture, date1904, ref text);

				break;
			}
			case CsvCellKind.Bool:
			{
				// The scanner stores a boolean as 1 or 0.
				shown = value.Length == 1 && (value[0] is (byte)'0' or (byte)'1') && NumberText.TryWriteText(code, value[0] == (byte)'1' ? "TRUE"u8 : "FALSE"u8, ref text);

				break;
			}
			case CsvCellKind.Text:
			{
				shown = NumberText.TryWriteText(code, value, ref text);

				break;
			}
			default:
			{
				shown = false;

				break;
			}
		}

		if (!shown)
		{
			return false;
		}

		length = text.Length;

		return text.Fits;
	}

	/// <summary>
	/// The compiled code of a number format, compiled the first time it is asked for: the custom code, else the built-in
	/// id's in the culture. An empty custom code is General, whatever its id, as Excel shows it (ClosedXML writes id 0 so).
	/// </summary>
	/// <param name="index">The number format's index.</param>
	/// <returns>The code.</returns>
	private FormatCode Code(int index)
	{
		var code = Volatile.Read(ref codes[index]);
		if (code is null)
		{
			var id = styles.NumberFormatIdAt(index);
			var custom = styles.FormatCode(id);
			code = FormatCode.Compile(custom is null ? culture.BuiltIn(id) : custom.Length == 0 ? "General" : custom);
			Volatile.Write(ref codes[index], code);
		}

		return code;
	}

	/// <summary>Whether a number format changes the text of a text or a boolean: it has a text section that isn't <c>@</c> alone.</summary>
	/// <param name="index">The number format's index.</param>
	/// <returns>Whether it does.</returns>
	private bool ShowsText(int index)
	{
		var known = Volatile.Read(ref texts[index]);
		if (known == 0)
		{
			var code = Code(index);
			var tokens = code.IsReadable ? code.Text?.Tokens : null;
			known = tokens is null || (tokens.Length == 1 && tokens[0].Kind == FormatTokenKind.At) ? KeepsText : ChangesText;
			Volatile.Write(ref texts[index], known);
		}

		return known == ChangesText;
	}
}
