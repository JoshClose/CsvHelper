using System;
using System.Collections.Generic;
using System.Text;

namespace CsvHelper.Excel;

/// <summary>The kinds of a column's validation, as Excel's <c>dataValidation</c> types them.</summary>
internal enum ValidationKind
{
	/// <summary>Any value, Excel's "Any value": a prompt alone, without a <c>type</c>.</summary>
	Any,

	/// <summary>A dropdown of values (<c>list</c>).</summary>
	List,

	/// <summary>A whole number between two bounds (<c>whole</c>).</summary>
	Whole,

	/// <summary>A number between two bounds (<c>decimal</c>).</summary>
	Decimal,

	/// <summary>A date between two serials (<c>date</c>), with a time too.</summary>
	Date,

	/// <summary>A time between two serials (<c>time</c>): a time of day, or a duration.</summary>
	Time,

	/// <summary>A text of one character (<c>textLength</c>, <c>equal</c> to 1).</summary>
	OneCharacter,
}

/// <summary>
/// What a member's type gives its column with <see cref="ExcelSheetOptions.Validate"/>: a validation of the values the
/// type takes, and the format the cells users add take, its column's default style (Phase 7's decisions 2 to 4).
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="bool"/> is a dropdown of <c>TRUE</c> and <c>FALSE</c>, which Excel enters as booleans, in General; one
/// with <see cref="CsvBooleanValuesAttribute"/> a dropdown of its first true and first false value, in Text. An enum is a
/// dropdown of its members as the record writer writes them, in Text; a <see cref="FlagsAttribute"/> enum, whose values
/// combine, has none. A type's dropdown with a value Excel can't match (empty, or of more than 255 characters) has none.
/// </para>
/// <para>
/// Integers are whole numbers in their type's range, but for <see cref="long"/>, <see cref="ulong"/>, <c>Int128</c> and
/// <c>UInt128</c>, whose numbers of 16 digits or more Excel would keep 15 digits of: at most 15 digits. Floating-point
/// numbers and <see cref="decimal"/> are numbers within their type's range, as Excel holds its bounds (15 digits), and
/// <see cref="double"/>'s within Excel's largest. All are General.
/// </para>
/// <para>
/// Dates (<see cref="DateTime"/>, <c>DateOnly</c>, and <see cref="DateTimeOffset"/> but with
/// <see cref="ExcelDateTimeOffsets.Text"/>) are dates from 1900-01-01 to 9999-12-31 23:59:59.999, in <c>yyyy-mm-dd</c>, or
/// <c>yyyy-mm-dd hh:mm:ss</c> for those with a time; <c>TimeOnly</c> a time of day, in <c>hh:mm:ss</c>; and
/// <see cref="TimeSpan"/> a time from 0 to Excel's last serial, in <c>[h]:mm:ss</c>. A date's or time's member's
/// <see cref="CsvFormatAttribute"/>, translated as Phase 4 writes its cells, is its format, and a time's validation when
/// the format has no date; one Phase 4 can't translate, whose cells are text, gives Text and no validation, as does a
/// duration's format, and a <see cref="DateTimeOffset"/>'s with <see cref="ExcelDateTimeOffsets.Utc"/>.
/// </para>
/// <para>
/// A <see cref="char"/> is a text of one character, in Text. Text (<see cref="string"/>, <see cref="Guid"/>,
/// <see cref="Uri"/> and every other type) has no validation, in Text, so that what users type stays as typed
/// (<c>00123</c>, <c>1/2</c>). A formula has neither. A member a converter writes, or a collection's column, has no type
/// (<see cref="MemberFormat.Type"/>), so neither.
/// </para>
/// </remarks>
/// <param name="Kind">The validation's kind: <see cref="ValidationKind.Any"/> for none.</param>
/// <param name="Minimum">The lower bound of a number, date or time.</param>
/// <param name="Maximum">The upper bound of a number, date or time.</param>
/// <param name="Values">The values of a dropdown, or <see langword="null"/>.</param>
/// <param name="Format">The number format code of the column's default style, or <see langword="null"/> for General.</param>
/// <param name="Widest">
/// The serial of the type's widest value, which a date's, time's or duration's column is as wide as in its format
/// (9999-12-31 23:59:59, 23:59:59, or Excel's last duration); <see cref="double.NaN"/> for other types.
/// </param>
internal sealed record TypeValidation(ValidationKind Kind, double Minimum, double Maximum, IReadOnlyList<string>? Values, string? Format, double Widest)
{
	/// <summary>Excel's Text format, in which what users type stays text.</summary>
	public const string Text = "@";

	/// <summary>The most digits of a number Excel keeps, as a whole number's bound: 999,999,999,999,999.</summary>
	private const double FifteenDigits = 999_999_999_999_999;

	/// <summary><see cref="double"/>'s bound: Excel's largest number, as Excel opens it.</summary>
	private const double DoubleBound = 9.99999999999999E+307;

	/// <summary><see cref="float"/>'s bound: <see cref="float.MaxValue"/> to Excel's 15 digits.</summary>
	private const double FloatBound = 3.40282346638529E+38;

	/// <summary><see cref="decimal"/>'s bound: <see cref="decimal.MaxValue"/> to Excel's 15 digits.</summary>
	private const double DecimalBound = 7.92281625142643E+28;

	/// <summary><c>Half</c>'s bound, its largest value.</summary>
	private const double HalfBound = 65504;

	/// <summary>The serial of Excel's last date and time, 9999-12-31 23:59:59.999: a date's bound, and a duration's.</summary>
	public static readonly double LastDate = Serial.Days(Serial.PastLast - 1);

	/// <summary>The serial of the last time of day, 23:59:59.999.</summary>
	public static readonly double LastTime = Serial.Days(Serial.MillisecondsPerDay - 1);

	/// <summary>The serial of the widest date and of the widest duration, in whole seconds: 9999-12-31 23:59:59.</summary>
	private static readonly double WidestDate = Serial.Days(Serial.PastLast - 1000);

	/// <summary>The serial of the widest time of day, in whole seconds: 23:59:59.</summary>
	private static readonly double WidestTime = Serial.Days(Serial.MillisecondsPerDay - 1000);

	/// <summary>What text gives a column: no validation, in Text.</summary>
	private static readonly TypeValidation TextOnly = new(ValidationKind.Any, 0, 0, null, Text, double.NaN);

	/// <summary>A <see cref="bool"/>'s dropdown, of Excel's booleans.</summary>
	private static readonly TypeValidation Booleans = new(ValidationKind.List, 0, 0, ["TRUE", "FALSE"], null, double.NaN);

	/// <summary>A <see cref="char"/>'s: one character, in Text.</summary>
	private static readonly TypeValidation OneCharacter = new(ValidationKind.OneCharacter, 1, 1, null, Text, double.NaN);

	/// <summary>What a member's type gives its column.</summary>
	/// <param name="member">The column's member, which has a type.</param>
	/// <param name="format">The column's member's format and culture, which a date's format is translated with; or <see langword="null"/> for none.</param>
	/// <param name="offsets">How the workbook writes a <see cref="DateTimeOffset"/>.</param>
	/// <returns>The validation and format, or <see langword="null"/> for neither (a formula).</returns>
	public static TypeValidation? Of(MemberFormat member, ColumnFormat? format, ExcelDateTimeOffsets offsets)
	{
		var type = Nullable.GetUnderlyingType(member.Type!) ?? member.Type!;
		if (type.IsEnum)
		{
			return member.Values is { } names && Listable(names) ? new TypeValidation(ValidationKind.List, 0, 0, names, Text, double.NaN) : TextOnly;
		}

		if (type == typeof(char))
		{
			return OneCharacter;
		}

		switch (CellKinds.Of(type))
		{
			case CellKind.Bool:
				return member.Values is not { } values ? Booleans :
					Listable(values) ? new TypeValidation(ValidationKind.List, 0, 0, values, Text, double.NaN) : TextOnly;
			case CellKind.Integer:
				return Whole(type);
			case CellKind.Decimal:
				return Number(DecimalBound);
			case CellKind.Float:
				return Number(type == typeof(double) ? DoubleBound : type == typeof(float) ? FloatBound : HalfBound);
			case CellKind.DateTime:
				return Dated(format, CellKind.DateTime, FixedFormat.DateTime);
			case CellKind.DateOnly:
				return Dated(format, CellKind.DateOnly, FixedFormat.Date);
			case CellKind.TimeOnly:
				return Dated(format, CellKind.TimeOnly, FixedFormat.Time);
			case CellKind.TimeSpan:
				// Phase 4 writes a duration of another format than c's as text.
				return format is { HasDurationFormat: true } ? TextOnly :
					new TypeValidation(ValidationKind.Time, 0, LastDate, null, StyleRegistry.Code(FixedFormat.Duration), WidestDate);
			case CellKind.DateTimeOffset:
				// Its format's text is a date cell only with ClockTime, as Phase 4 writes it.
				return offsets == ExcelDateTimeOffsets.Text || (offsets == ExcelDateTimeOffsets.Utc && format is { HasDateFormat: true }) ? TextOnly :
					Dated(format, CellKind.DateTimeOffset, FixedFormat.DateTime);
			case CellKind.Formula:
				return null;
			default:
				return TextOnly;
		}
	}

	/// <summary>Whether a type's values can be a dropdown: each has 1 to 255 characters, which Excel matches.</summary>
	/// <param name="values">The values.</param>
	/// <returns>Whether they can.</returns>
	private static bool Listable(IReadOnlyList<string> values)
	{
		if (values.Count == 0)
		{
			return false;
		}

		foreach (var value in values)
		{
			if (value.Length is 0 or > ExcelColumnOptions.MaxListValueLength)
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>An integer type's whole numbers: its range, or 15 digits for the types of more.</summary>
	/// <param name="type">The type.</param>
	/// <returns>The validation, in General.</returns>
	private static TypeValidation Whole(Type type)
	{
		(double Minimum, double Maximum) range =
			type == typeof(int) ? (int.MinValue, int.MaxValue) :
			type == typeof(uint) ? (0, uint.MaxValue) :
			type == typeof(short) ? (short.MinValue, short.MaxValue) :
			type == typeof(ushort) ? (0, ushort.MaxValue) :
			type == typeof(sbyte) ? (sbyte.MinValue, sbyte.MaxValue) :
			type == typeof(byte) ? (0, byte.MaxValue) :
			type == typeof(ulong) || type.FullName == "System.UInt128" ? (0, FifteenDigits) :
			(-FifteenDigits, FifteenDigits);

		return new TypeValidation(ValidationKind.Whole, range.Minimum, range.Maximum, null, null, double.NaN);
	}

	/// <summary>A number within a bound either side of 0.</summary>
	/// <param name="bound">The bound.</param>
	/// <returns>The validation, in General.</returns>
	private static TypeValidation Number(double bound) => new(ValidationKind.Decimal, -bound, bound, null, null, double.NaN);

	/// <summary>
	/// A date's or time's: its type's validation and format, or its member's format's translation, with a time's
	/// validation when the format has no date; Text and none when Phase 4 can't translate the format, whose cells are text.
	/// </summary>
	/// <param name="format">The column's member's format, or <see langword="null"/>.</param>
	/// <param name="kind">The kind of value.</param>
	/// <param name="own">The writer's format of the type, without a member's format.</param>
	/// <returns>The validation and format.</returns>
	private static TypeValidation Dated(ColumnFormat? format, CellKind kind, FixedFormat own)
	{
		var code = StyleRegistry.Code(own);
		var hasDate = kind != CellKind.TimeOnly;
		if (format is { HasDateFormat: true })
		{
			if (format.Translation(kind) is not { } translation)
			{
				return TextOnly;
			}

			code = translation.Code;
			hasDate = translation.HasDate;
		}

		return hasDate ? new TypeValidation(ValidationKind.Date, 1, LastDate, null, code, WidestDate) :
			new TypeValidation(ValidationKind.Time, 0, LastTime, null, code, WidestTime);
	}
}

/// <summary>
/// A column's validation (Phase 7's step 3): its type's (<see cref="TypeValidation"/>), or its
/// <see cref="ExcelColumnOptions.List"/>'s dropdown in place of it; and its <see cref="ExcelColumnOptions.Prompt"/>, with
/// the header's name as its title. Excel applies it to what users type into the column's cells below the header, and shows
/// its own message, in its user's language, with its Stop style, for a value it refuses (decision 6).
/// </summary>
/// <remarks>
/// <para>
/// A dropdown's values are listed once each, in order, matched ordinally. They are written in the validation (inline),
/// quoted, a quote doubled, when, joined by commas, they have at most 255 characters, as Excel holds them, and none has a
/// comma or white space at either end, which Excel's inline lists can't hold (checked: <c>"a ,c"</c> matches neither
/// <c>a </c> nor <c>a</c>); else in the workbook's lists sheet, by a hidden name (<see cref="ValidationLists"/>). Excel
/// matches inline values in their case, and listed ones ignoring it.
/// </para>
/// <para>
/// Texts are written as cells' are (<see cref="XmlOutput.AppendText"/>): Excel decodes <c>_xHHHH_</c> in a prompt, its
/// title and an inline list (checked on 2026-11-15: <c>_x0041_</c> is <c>A</c>, <c>_x005F_x0041_</c> is
/// <c>_x0041_</c>), and line breaks, tabs and control characters written as escapes are themselves, in a prompt and in an
/// inline list, which then matches them typed. A prompt's and a title's tab, LF and CR are written as character references
/// (<c>&amp;#10;</c>), as Excel writes them, which XML reads back as one character (<see cref="AppendAttribute"/>), and
/// their lengths as written are the schema's limits, 32 and 255, an escape of a control character counting 7
/// (<see cref="WrittenLength"/>). Lengths are in UTF-16 code units, as Excel counts them (checked: a prompt of 128
/// surrogate pairs is repaired away, and a list of 128 is a long one).
/// </para>
/// </remarks>
internal sealed class ColumnValidation
{
	/// <summary>The most characters of a prompt's title: the most Excel's dialog takes (decision 17).</summary>
	public const int MaxTitleLength = 32;

	/// <summary>The most characters an inline list has, its values and commas, as Excel holds them.</summary>
	public const int MaxInlineLength = 255;

	// The validation's formula1 as UTF-8: a list's, inline or by name; and its title and prompt, or null.
	private readonly byte[]? list;
	private readonly byte[]? title;
	private readonly byte[]? prompt;

	/// <summary>Creates a column's validation.</summary>
	/// <param name="kind">Its kind.</param>
	/// <param name="minimum">The lower bound of a number, date or time.</param>
	/// <param name="maximum">The upper bound of a number, date or time.</param>
	/// <param name="values">A dropdown's values, each once, or <see langword="null"/>.</param>
	/// <param name="listName">The hidden name of a dropdown's values in the lists sheet, or <see langword="null"/> when they are inline.</param>
	/// <param name="prompt">The prompt, or <see langword="null"/>.</param>
	/// <param name="title">The prompt's title, or <see langword="null"/>.</param>
	private ColumnValidation(ValidationKind kind, double minimum, double maximum, string[]? values, string? listName, string? prompt, string? title)
	{
		Kind = kind;
		Minimum = minimum;
		Maximum = maximum;
		Values = values;
		ListName = listName;
		Prompt = prompt;
		Title = title;
		if (values is not null)
		{
			list = Encoding.UTF8.GetBytes(listName ?? Inline(values));
		}

		this.prompt = prompt is null ? null : Encoding.UTF8.GetBytes(prompt);
		this.title = title is null ? null : Encoding.UTF8.GetBytes(title);
	}

	/// <summary>The validation's kind.</summary>
	public ValidationKind Kind { get; }

	/// <summary>The lower bound of a number, date or time.</summary>
	public double Minimum { get; }

	/// <summary>The upper bound of a number, date or time.</summary>
	public double Maximum { get; }

	/// <summary>A dropdown's values, each once, in order; or <see langword="null"/>.</summary>
	public IReadOnlyList<string>? Values { get; }

	/// <summary>The hidden name of a dropdown's values in the lists sheet, or <see langword="null"/> when they are inline.</summary>
	public string? ListName { get; }

	/// <summary>The prompt, or <see langword="null"/>.</summary>
	public string? Prompt { get; }

	/// <summary>The prompt's title, or <see langword="null"/>.</summary>
	public string? Title { get; }

	/// <summary>A column's validation: its list's or its type's, and its prompt.</summary>
	/// <param name="type">What the column's member's type gives it, or <see langword="null"/> (without <see cref="ExcelSheetOptions.Validate"/>, or a type).</param>
	/// <param name="options">The column's options, or <see langword="null"/>.</param>
	/// <param name="header">The column's header name, which titles its prompt; or <see langword="null"/> without a header.</param>
	/// <param name="lists">The workbook's lists, which name a dropdown that isn't inline.</param>
	/// <returns>The validation, or <see langword="null"/> for a column with neither a validation nor a prompt.</returns>
	/// <exception cref="InvalidOperationException">The list is one more than the lists sheet holds.</exception>
	public static ColumnValidation? Create(TypeValidation? type, ExcelColumnOptions? options, string? header, ValidationLists lists)
	{
		var prompt = options?.Prompt;
		var given = options?.List;
		var kind = given is not null ? ValidationKind.List : type?.Kind ?? ValidationKind.Any;
		if (kind == ValidationKind.Any && prompt is null)
		{
			return null;
		}

		string[]? values = null;
		string? listName = null;
		if (kind == ValidationKind.List)
		{
			values = Distinct(given ?? type!.Values!);
			if (!IsInline(values))
			{
				listName = lists.NameOf(values);
			}
		}

		var title = prompt is not null && !string.IsNullOrEmpty(header) ? TitleOf(header!) : null;

		return new ColumnValidation(kind, type?.Minimum ?? 0, type?.Maximum ?? 0, values, listName, prompt, title);
	}

	/// <summary>
	/// Whether a list's values go in the validation: joined by commas, at most <see cref="MaxInlineLength"/> characters,
	/// and none with a comma or white space at either end.
	/// </summary>
	/// <param name="values">The values, each once.</param>
	/// <returns>Whether they do.</returns>
	public static bool IsInline(IReadOnlyList<string> values)
	{
		var length = values.Count - 1;
		foreach (var value in values)
		{
			if (value.IndexOf(',') >= 0 || char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[value.Length - 1]))
			{
				return false;
			}

			length += value.Length;
			if (length > MaxInlineLength)
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Writes the <c>dataValidation</c>, as Excel writes one: its type (and <c>operator</c> but between's), blanks allowed,
	/// its prompt shown and Excel's own Stop message, the prompt's title and text, the column from a row down to Excel's
	/// last, and the formulas of its list or bounds.
	/// </summary>
	/// <param name="xml">The buffer.</param>
	/// <param name="column">The column.</param>
	/// <param name="firstRow">The first row it covers: the one below the header.</param>
	public void Write(XmlOutput xml, int column, long firstRow)
	{
		xml.Append(Kind switch
		{
			ValidationKind.List => "<dataValidation type=\"list\""u8,
			ValidationKind.Whole => "<dataValidation type=\"whole\""u8,
			ValidationKind.Decimal => "<dataValidation type=\"decimal\""u8,
			ValidationKind.Date => "<dataValidation type=\"date\""u8,
			ValidationKind.Time => "<dataValidation type=\"time\""u8,
			ValidationKind.OneCharacter => "<dataValidation type=\"textLength\" operator=\"equal\""u8,
			_ => "<dataValidation"u8,
		});
		xml.Append(" allowBlank=\"1\" showInputMessage=\"1\" showErrorMessage=\"1\""u8);
		if (title is not null)
		{
			xml.Append(" promptTitle=\""u8);
			AppendAttribute(xml, title);
			xml.Append((byte)'"');
		}

		if (prompt is not null)
		{
			xml.Append(" prompt=\""u8);
			AppendAttribute(xml, prompt);
			xml.Append((byte)'"');
		}

		var letters = SheetSink.ColumnName(column);
		xml.Append(" sqref=\""u8);
		xml.AppendReference(letters, firstRow);
		xml.Append((byte)':');
		xml.AppendReference(letters, CellReference.MaxRow);
		xml.Append((byte)'"');
		switch (Kind)
		{
			case ValidationKind.Any:
				xml.Append("/>"u8);

				return;
			case ValidationKind.List:
				xml.Append("><formula1>"u8);
				xml.AppendText(list!);
				xml.Append("</formula1>"u8);

				break;
			case ValidationKind.OneCharacter:
				xml.Append("><formula1>1</formula1>"u8);

				break;
			default:
				xml.Append("><formula1>"u8);
				xml.AppendDouble(Minimum);
				xml.Append("</formula1><formula2>"u8);
				xml.AppendDouble(Maximum);
				xml.Append("</formula2>"u8);

				break;
		}

		xml.Append("</dataValidation>"u8);
	}

	/// <summary>A list's values, each once, in order, ordinally.</summary>
	/// <param name="values">The values.</param>
	/// <returns>The distinct values.</returns>
	private static string[] Distinct(IReadOnlyList<string> values)
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		var distinct = new List<string>(values.Count);
		foreach (var value in values)
		{
			if (seen.Add(value))
			{
				distinct.Add(value);
			}
		}

		return [.. distinct];
	}

	/// <summary>An inline list's formula: the values joined by commas, quoted, a quote doubled.</summary>
	/// <param name="values">The values.</param>
	/// <returns>The formula, such as <c>"Open,On hold"</c>.</returns>
	private static string Inline(string[] values)
	{
		var builder = new StringBuilder(MaxInlineLength + 16);
		builder.Append('"');
		for (var i = 0; i < values.Length; i++)
		{
			if (i > 0)
			{
				builder.Append(',');
			}

			builder.Append(values[i].Replace("\"", "\"\""));
		}

		return builder.Append('"').ToString();
	}

	/// <summary>
	/// Appends a prompt's or a title's text as an attribute's value: its tab, LF and CR as character references
	/// (<c>&amp;#9;</c>, <c>&amp;#10;</c>, <c>&amp;#13;</c>), as Excel writes them, which XML reads back as themselves and
	/// counts as one character each; the rest as a cell's text is written (<see cref="XmlOutput.AppendText"/>), each part
	/// between them on its own, so that an <c>_</c> before one is escaped as an escape before it would be.
	/// </summary>
	/// <param name="xml">The buffer.</param>
	/// <param name="text">The text, as valid UTF-8.</param>
	public static void AppendAttribute(XmlOutput xml, ReadOnlySpan<byte> text)
	{
		while (true)
		{
			var i = text.IndexOfAny((byte)'\t', (byte)'\n', (byte)'\r');
			if (i < 0)
			{
				xml.AppendText(text, attribute: true);

				return;
			}

			xml.AppendText(text.Slice(0, i), attribute: true);
			xml.Append(text[i] switch
			{
				(byte)'\t' => "&#9;"u8,
				(byte)'\n' => "&#10;"u8,
				_ => "&#13;"u8,
			});
			text = text.Slice(i + 1);
		}
	}

	/// <summary>
	/// The length of a prompt's or a title's text as written (<see cref="AppendAttribute"/>) and read back by XML, in UTF-16
	/// code units: the length the schema limits, and the validator checks. A control character but tab, LF and CR, and
	/// U+FFFE and U+FFFF, written as an escape (<c>_x0001_</c>), count 7, and so does an <c>_</c> escaped (<c>_x005F_</c>);
	/// a lone surrogate, written as U+FFFD, counts 1.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <returns>Its length as written.</returns>
	public static int WrittenLength(string text)
	{
		using var xml = new XmlOutput(text.Length * 3 + 16);
		AppendAttribute(xml, Encoding.UTF8.GetBytes(text));
		var written = Encoding.UTF8.GetString(xml.Written.ToArray());

		// Each entity and reference is one character as XML reads it.
		return written.Length - (4 * Count(written, "&amp;")) - (3 * Count(written, "&lt;")) - (3 * Count(written, "&gt;")) - (5 * Count(written, "&quot;"))
			- (3 * Count(written, "&#9;")) - (4 * Count(written, "&#10;")) - (4 * Count(written, "&#13;"));
	}

	/// <summary>The times a text holds another, apart.</summary>
	/// <param name="text">The text.</param>
	/// <param name="part">The other.</param>
	/// <returns>The count.</returns>
	private static int Count(string text, string part)
	{
		var count = 0;
		for (var i = text.IndexOf(part, StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + part.Length, StringComparison.Ordinal))
		{
			count++;
		}

		return count;
	}

	/// <summary>
	/// A prompt's title: the header's name cut to <see cref="MaxTitleLength"/> characters between characters, and shorter
	/// while its written length is longer (<see cref="WrittenLength"/>), as a name with a control character's escape is.
	/// </summary>
	/// <param name="header">The header's name, not empty.</param>
	/// <returns>The title.</returns>
	private static string TitleOf(string header)
	{
		var title = Cut(header, MaxTitleLength);
		while (title.Length > 1 && WrittenLength(title) > MaxTitleLength)
		{
			title = Cut(title, title.Length - 1);
		}

		return title;
	}

	/// <summary>A text's first characters, not between a surrogate pair's halves.</summary>
	/// <param name="text">The text.</param>
	/// <param name="length">The most characters to keep.</param>
	/// <returns>The text, or its first characters.</returns>
	private static string Cut(string text, int length) =>
		text.Length <= length ? text : text.Substring(0, char.IsHighSurrogate(text[length - 1]) ? length - 1 : length);
}
