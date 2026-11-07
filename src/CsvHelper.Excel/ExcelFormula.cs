using System;
#if NET
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
#endif

namespace CsvHelper.Excel;

/// <summary>
/// A formula, such as <c>SUM(A1:A3)</c>, which a workbook writer writes as a formula cell, for Excel to compute when it
/// opens the workbook. Its text (<see cref="ToString()"/>) is <c>=</c> and the formula, which is what a CSV writer
/// writes and what <see cref="ExcelReaderOptions.Formulas"/> = <see cref="ExcelFormulas.FormulaText"/> reads.
/// </summary>
/// <remarks>
/// <para>
/// A member of this type is written as a formula cell by the generated record writer, as any formattable value is, with
/// no converter; <see cref="ExcelWriterExtensions.WriteFormula"/> writes one by hand. On .NET it is read by the generated
/// mapper as an <c>ISpanParsable&lt;ExcelFormula&gt;</c>; on the other targets a member needs
/// <c>[CsvConverter(typeof(ExcelFormulaConverter))]</c>.
/// </para>
/// <para>
/// The cell has no cached value: Excel computes the formula on open, and so does LibreOffice. Readers that don't compute
/// formulas read the cell as empty, CsvHelper.Excel among them with <see cref="ExcelFormulas.CachedValue"/>.
/// </para>
/// <para>
/// The formula is written as given, in Excel's file form: references in A1 style, <c>,</c> between arguments and <c>.</c>
/// in numbers whatever the culture, and functions newer than Excel 2007 with the prefix the file stores them with
/// (<c>_xlfn.XLOOKUP</c>), which Excel shows without it; without it Excel computes <c>#NAME?</c>. The constructor refuses
/// what makes Excel refuse the whole workbook (see <see cref="ExcelFormula(string)"/>), but not every formula Excel can't
/// parse; those make a workbook it can't open too. A formula that Excel parses but can't compute (an unknown function, a
/// missing sheet) opens, as <c>#NAME?</c> or <c>#REF!</c>.
/// </para>
/// <para>
/// <c>default(ExcelFormula)</c> has no formula: its text is empty, and it is written as an empty cell.
/// </para>
/// </remarks>
public readonly struct ExcelFormula : IEquatable<ExcelFormula>, IFormattable
#if NET
	, ISpanFormattable, IUtf8SpanFormattable, ISpanParsable<ExcelFormula>
#endif
{
	// = and the formula, as ToString gives it; null for default(ExcelFormula).
	private readonly string? text;

	/// <summary>Creates a formula from its text, with or without its <c>=</c>.</summary>
	/// <remarks>
	/// The formula has at most 8,192 UTF-16 code units after its <c>=</c>; its parentheses, brackets and braces, strings
	/// and quoted sheet names are closed; it doesn't start with an operator other than <c>+</c>, <c>-</c> and <c>@</c>,
	/// nor end with one; and outside its strings it has no control character but line breaks. Its line breaks outside its
	/// strings are kept as LF, the only one Excel takes there, so a CR LF becomes LF. Inside its strings anything goes.
	/// </remarks>
	/// <param name="formula">The formula, such as <c>SUM(A1:A3)</c> or <c>=SUM(A1:A3)</c>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="formula"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The formula is empty, or Excel opens no workbook with it.</exception>
	public ExcelFormula(string formula)
	{
		ArgumentNullException.ThrowIfNull(formula);
		text = Checked(formula.AsSpan(), formula, out var problem) ?? throw new ArgumentException(problem, nameof(formula));
	}

	/// <summary>Creates a formula from its text, which <see cref="Checked"/> gave.</summary>
	/// <param name="text"><c>=</c> and the formula.</param>
	/// <param name="isChecked">Always <see langword="true"/>: it tells this constructor from the public one.</param>
	private ExcelFormula(string text, bool isChecked) => this.text = text;

	/// <summary>The formula, without its <c>=</c>: <c>SUM(A1:A3)</c>. Empty for <c>default(ExcelFormula)</c>.</summary>
	public string Formula => text is null ? "" : text.Substring(1);

	/// <summary>
	/// Parses a formula's text: <c>=</c> and the formula, as <see cref="ToString()"/> gives it and
	/// <see cref="ExcelFormulas.FormulaText"/> reads it. Empty text is <c>default(ExcelFormula)</c>, as an empty cell.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <param name="provider">Not used: a formula's text is the same in every culture.</param>
	/// <returns>The formula.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
	/// <exception cref="FormatException">
	/// The text doesn't start with <c>=</c>, or the formula is empty or one Excel opens no workbook with (see
	/// <see cref="ExcelFormula(string)"/>).
	/// </exception>
	public static ExcelFormula Parse(string text, IFormatProvider? provider = null)
	{
		ArgumentNullException.ThrowIfNull(text);

		return TryParse(text.AsSpan(), text, out var formula, out var problem) ? formula : throw new FormatException(problem);
	}

	/// <summary>Tries to parse a formula's text, as <see cref="Parse(string, IFormatProvider?)"/> does.</summary>
	/// <param name="text">The text, or <see langword="null"/>.</param>
	/// <param name="provider">Not used: a formula's text is the same in every culture.</param>
	/// <param name="formula">The formula, or <c>default(ExcelFormula)</c> when the text can't be parsed.</param>
	/// <returns>Whether the text was parsed.</returns>
	public static bool TryParse(
#if NET
		[NotNullWhen(true)]
#endif
		string? text, IFormatProvider? provider, out ExcelFormula formula)
	{
		if (text is null)
		{
			formula = default;

			return false;
		}

		return TryParse(text.AsSpan(), text, out formula, out _);
	}

#if NET
	/// <summary>Parses a formula's text, as <see cref="Parse(string, IFormatProvider?)"/> does.</summary>
	/// <param name="s">The text.</param>
	/// <param name="provider">Not used.</param>
	/// <returns>The formula.</returns>
	/// <exception cref="FormatException">The text is not a formula's, or the formula is one Excel opens no workbook with.</exception>
	static ExcelFormula ISpanParsable<ExcelFormula>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) =>
		TryParse(s, null, out var formula, out var problem) ? formula : throw new FormatException(problem);

	/// <summary>Tries to parse a formula's text, as <see cref="Parse(string, IFormatProvider?)"/> does.</summary>
	/// <param name="s">The text.</param>
	/// <param name="provider">Not used.</param>
	/// <param name="result">The formula, or <c>default(ExcelFormula)</c> when the text can't be parsed.</param>
	/// <returns>Whether the text was parsed.</returns>
	static bool ISpanParsable<ExcelFormula>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out ExcelFormula result) =>
		TryParse(s, null, out result, out _);
#endif

	/// <summary>Whether two formulas have the same text, compared ordinally.</summary>
	/// <param name="left">A formula.</param>
	/// <param name="right">Another.</param>
	/// <returns>Whether they are equal.</returns>
	public static bool operator ==(ExcelFormula left, ExcelFormula right) => left.Equals(right);

	/// <summary>Whether two formulas have different texts, compared ordinally.</summary>
	/// <param name="left">A formula.</param>
	/// <param name="right">Another.</param>
	/// <returns>Whether they are not equal.</returns>
	public static bool operator !=(ExcelFormula left, ExcelFormula right) => !left.Equals(right);

	/// <summary>Whether this formula has the same text as another, compared ordinally: <c>sum(a1)</c> isn't <c>SUM(A1)</c>.</summary>
	/// <param name="other">The other formula.</param>
	/// <returns>Whether they are equal.</returns>
	public bool Equals(ExcelFormula other) => string.Equals(text, other.text, StringComparison.Ordinal);

	/// <summary>Whether an object is a formula with the same text.</summary>
	/// <param name="obj">The object.</param>
	/// <returns>Whether it is an equal formula.</returns>
	public override bool Equals(object? obj) => obj is ExcelFormula other && Equals(other);

	/// <summary>A hash code of the formula's text.</summary>
	/// <returns>The hash code.</returns>
	public override int GetHashCode() => text is null ? 0 : StringComparer.Ordinal.GetHashCode(text);

	/// <summary>The formula's text: <c>=</c> and the formula, such as <c>=SUM(A1:A3)</c>; empty for <c>default(ExcelFormula)</c>.</summary>
	/// <returns>The text.</returns>
	public override string ToString() => text ?? "";

	/// <summary>The formula's text, as <see cref="ToString()"/> gives it: a formula has no formats, and is the same in every culture.</summary>
	/// <param name="format">Not used.</param>
	/// <param name="formatProvider">Not used.</param>
	/// <returns>The text.</returns>
	public string ToString(string? format, IFormatProvider? formatProvider) => text ?? "";

#if NET
	/// <summary>Writes the formula's text, as <see cref="ToString()"/> gives it.</summary>
	/// <param name="destination">Where the text goes.</param>
	/// <param name="charsWritten">The characters written.</param>
	/// <param name="format">Not used.</param>
	/// <param name="provider">Not used.</param>
	/// <returns>Whether the text fits.</returns>
	bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
	{
		var value = text.AsSpan();
		if (value.TryCopyTo(destination))
		{
			charsWritten = value.Length;

			return true;
		}

		charsWritten = 0;

		return false;
	}

	/// <summary>Writes the formula's text as UTF-8, as <see cref="ToString()"/> gives it.</summary>
	/// <param name="utf8Destination">Where the text goes.</param>
	/// <param name="bytesWritten">The bytes written.</param>
	/// <param name="format">Not used.</param>
	/// <param name="provider">Not used.</param>
	/// <returns>Whether the text fits.</returns>
	bool IUtf8SpanFormattable.TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
	{
		if (System.Text.Unicode.Utf8.FromUtf16(text.AsSpan(), utf8Destination, out _, out bytesWritten) == OperationStatus.Done)
		{
			return true;
		}

		bytesWritten = 0;

		return false;
	}
#endif

	/// <summary>
	/// Parses a formula's text: empty for <c>default(ExcelFormula)</c>, or <c>=</c> and a formula that passes the checks.
	/// </summary>
	/// <param name="s">The text.</param>
	/// <param name="original">The text as a string, when it is one, to keep rather than copy.</param>
	/// <param name="formula">The formula, or <c>default(ExcelFormula)</c>.</param>
	/// <param name="problem">Why the text can't be parsed, or <see langword="null"/>.</param>
	/// <returns>Whether it was parsed.</returns>
	private static bool TryParse(ReadOnlySpan<char> s, string? original, out ExcelFormula formula, out string? problem)
	{
		formula = default;
		problem = null;
		if (s.IsEmpty)
		{
			return true;
		}

		if (s[0] != '=')
		{
			problem = "A formula's text starts with =, and this one doesn't.";

			return false;
		}

		if (Checked(s, original, out problem) is not { } text)
		{
			return false;
		}

		formula = new ExcelFormula(text, isChecked: true);

		return true;
	}

	/// <summary>Checks a formula given with or without its <c>=</c>, and gives its text, <c>=</c> and the formula.</summary>
	/// <param name="formula">The formula.</param>
	/// <param name="original">The formula as a string, when it is one, to keep when it starts with <c>=</c> and needs no change.</param>
	/// <param name="problem">What makes Excel refuse it, or <see langword="null"/>.</param>
	/// <returns>The text, or <see langword="null"/> when the formula is refused.</returns>
	private static string? Checked(ReadOnlySpan<char> formula, string? original, out string? problem)
	{
		var hasSign = formula.Length > 0 && formula[0] == '=';
		var body = hasSign ? formula.Slice(1) : formula;
		problem = FormulaCheck.Check(body, out var normalized);
		if (problem is not null)
		{
			return null;
		}

		if (normalized is not null)
		{
			return "=" + normalized;
		}

		return hasSign ? original ?? formula.ToString() : "=" + body.ToString();
	}
}
