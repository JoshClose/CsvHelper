using System;
using System.Globalization;
using System.Runtime.CompilerServices;
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
/// that isn't <c>@</c> alone.
/// </para>
/// <para>
/// When it is made, the formatter compiles each format's code once (the custom one, else the built-in id's in the
/// culture), and notes in a table, by cell format, the style a number takes and the one a text or a boolean takes
/// (Phase 9's decision 7), which <see cref="StyleOf"/> reads; <see cref="ChangesText"/> tells whether a text or a boolean
/// can take one at all. A workbook with more distinct formats than <see cref="MaxCompiledFormats"/>, which only a hostile
/// one has, has its codes compiled the first time a field asks for them, and kept, as before the tables: whether a format
/// changes text is learned then.
/// </para>
/// <para>
/// A field declines (its text is <see cref="ExcelText.Raw"/>'s) when its code can't be read, when Excel can't show its
/// value in it (a negative date, a value no section takes, a fraction too large, text past 255 characters), and when its
/// stored value isn't a number or a boolean as its kind says. Rendering allocates nothing.
/// </para>
/// <para>
/// One formatter serves a reader, its parallel workers among them, so it is safe to call from several threads: its tables
/// don't change once it is made, and past <see cref="MaxCompiledFormats"/> a code compiled twice at once is the same code,
/// and either is kept.
/// </para>
/// </remarks>
internal sealed class CellFormatter : CsvCellFormatter
{
	/// <summary>
	/// The most distinct number formats whose codes the formatter compiles when it is made: Excel keeps 200 to 250 number
	/// formats in a workbook, by its specifications. Only a hostile workbook has more, and its codes are compiled as fields
	/// ask for them.
	/// </summary>
	internal const int MaxCompiledFormats = 4096;

	/// <summary>A format whose text section changes text and booleans, learned past <see cref="MaxCompiledFormats"/>.</summary>
	private const sbyte TextChanges = 1;

	/// <summary>
	/// A format that shows text and booleans as they are, learned past <see cref="MaxCompiledFormats"/>: without a text
	/// section, or with <c>@</c> alone.
	/// </summary>
	private const sbyte TextKept = -1;

	/// <summary>A text's style that the formatter doesn't know when it is made: past <see cref="MaxCompiledFormats"/>.</summary>
	private const int Unknown = int.MinValue;

	private readonly StyleTable styles;
	private readonly DisplayCulture culture;
	private readonly bool date1904;
	// By number format index: its compiled code; past MaxCompiledFormats, also whether it changes text (0 until known).
	private readonly FormatCode?[] codes;
	private readonly sbyte[]? texts;
	// By cell format, one for General without cell formats: the styles of its fields.
	private readonly CellStyles[] cells;

	/// <summary>
	/// Creates the formatter of a workbook's reader: compiles each of the workbook's distinct number formats, unless it has
	/// more than <see cref="MaxCompiledFormats"/>, and fills its tables.
	/// </summary>
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

		// Every distinct format is a cell format's (General's without them), so each code compiled is one a field can ask for.
		var compiled = codes.Length <= MaxCompiledFormats;
		if (compiled)
		{
			for (var i = 0; i < codes.Length; i++)
			{
				codes[i] = Compile(i);
			}
		}
		else
		{
			texts = new sbyte[codes.Length];
		}

		// An s past the cell formats is the first one's, and without cell formats every s is General's
		// (StyleTable.NumberFormatIndex): StyleOf reads the first entry for both.
		cells = new CellStyles[Math.Max(styles.Count, 1)];
		var changes = !compiled;
		for (var i = 0; i < cells.Length; i++)
		{
			// More distinct formats than a style can name: only a hostile workbook has them, and their cells read as Raw.
			var index = styles.NumberFormatIndex(i);
			var number = index > MaxStyle ? -1 : index;
			var text = !compiled ? Unknown : number >= 0 && ChangesTextOf(codes[number]!) ? number : -1;
			cells[i] = new CellStyles(number, text);
			changes |= text >= 0;
		}

		ChangesText = changes;
	}

	/// <summary>
	/// Whether a text or a boolean can take a style (<see cref="StyleOf"/>): whether any cell format's number format changes
	/// their text, a code the renderer can read with a text section that isn't <c>@</c> alone. <see langword="true"/> past
	/// <see cref="MaxCompiledFormats"/>, where the formatter doesn't know when it is made. When <see langword="false"/>,
	/// <see cref="StyleOf"/> gives -1 for every text and boolean, so that a scanner needn't ask.
	/// </summary>
	public bool ChangesText { get; }

	/// <summary>
	/// The style a field takes, if its cell's number format can change its text: the format's index for a number, date,
	/// time or duration, and for a text or a boolean when the format has a text section that isn't <c>@</c> alone. It reads
	/// the formatter's tables, and is small enough to inline into the scanner.
	/// </summary>
	/// <param name="kind">The field's kind.</param>
	/// <param name="cellStyle">The cell's <c>s</c>: an index into the cell formats; one past them, or negative, means the first.</param>
	/// <returns>The style, from 0 to <see cref="CsvCellFormatter.MaxStyle"/>; or -1 when the field's text is its own.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int StyleOf(SheetCellKind kind, int cellStyle)
	{
		var table = cells;
		ref readonly var cell = ref table[(uint)cellStyle < (uint)table.Length ? cellStyle : 0];

		return kind switch
		{
			SheetCellKind.Number or SheetCellKind.DateNumber or SheetCellKind.Duration => cell.Number,
			SheetCellKind.Text or SheetCellKind.Bool => cell.Text != Unknown ? cell.Text : TextStyleOf(cell.Number),
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

	/// <summary>Whether a compiled code changes the text of a text or a boolean: it can be read, and has a text section that isn't <c>@</c> alone.</summary>
	/// <param name="code">The code.</param>
	/// <returns>Whether it does.</returns>
	private static bool ChangesTextOf(FormatCode code)
	{
		var tokens = code.IsReadable ? code.Text?.Tokens : null;

		return tokens is not null && !(tokens.Length == 1 && tokens[0].Kind == FormatTokenKind.At);
	}

	/// <summary>
	/// The style a text or a boolean takes past <see cref="MaxCompiledFormats"/> (<see cref="StyleOf"/>), whose format's code
	/// is compiled the first time a field asks for it.
	/// </summary>
	/// <param name="number">The cell format's number style: its format's index, or -1 past <see cref="CsvCellFormatter.MaxStyle"/>.</param>
	/// <returns>The index, when the format changes text; else -1.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private int TextStyleOf(int number) => number >= 0 && ShowsText(number) ? number : -1;

	/// <summary>
	/// Compiles a number format's code: the custom code, else the built-in id's in the culture. An empty custom code is
	/// General, whatever its id, as Excel shows it (ClosedXML writes id 0 so).
	/// </summary>
	/// <param name="index">The number format's index.</param>
	/// <returns>The code.</returns>
	private FormatCode Compile(int index)
	{
		var id = styles.NumberFormatIdAt(index);
		var custom = styles.FormatCode(id);

		return FormatCode.Compile(custom is null ? culture.BuiltIn(id) : custom.Length == 0 ? "General" : custom);
	}

	/// <summary>
	/// The compiled code of a number format: compiled when the formatter was made, or past <see cref="MaxCompiledFormats"/>
	/// the first time it is asked for.
	/// </summary>
	/// <param name="index">The number format's index.</param>
	/// <returns>The code.</returns>
	private FormatCode Code(int index)
	{
		var code = Volatile.Read(ref codes[index]);
		if (code is null)
		{
			code = Compile(index);
			Volatile.Write(ref codes[index], code);
		}

		return code;
	}

	/// <summary>
	/// Whether a number format changes the text of a text or a boolean, past <see cref="MaxCompiledFormats"/>: learned the
	/// first time it is asked for, and kept.
	/// </summary>
	/// <param name="index">The number format's index.</param>
	/// <returns>Whether it does.</returns>
	private bool ShowsText(int index)
	{
		var known = Volatile.Read(ref texts![index]);
		if (known == 0)
		{
			known = ChangesTextOf(Code(index)) ? TextChanges : TextKept;
			Volatile.Write(ref texts[index], known);
		}

		return known == TextChanges;
	}

	/// <summary>The styles a cell format's fields take (<see cref="StyleOf"/>), 8 bytes a cell format.</summary>
	/// <param name="number">A number's, date's, time's or duration's: its format's index, or -1 past <see cref="CsvCellFormatter.MaxStyle"/>.</param>
	/// <param name="text">
	/// A text's or a boolean's: the index when the format changes text, else -1; <see cref="Unknown"/> past
	/// <see cref="MaxCompiledFormats"/>.
	/// </param>
	private readonly struct CellStyles(int number, int text)
	{
		/// <summary>A number's, date's, time's or duration's style: its format's index, or -1 past <see cref="CsvCellFormatter.MaxStyle"/>.</summary>
		public int Number { get; } = number;

		/// <summary>
		/// A text's or a boolean's style: the index when the format changes text, else -1; <see cref="Unknown"/> past
		/// <see cref="MaxCompiledFormats"/>.
		/// </summary>
		public int Text { get; } = text;
	}
}
