using System;
using System.Collections.Generic;
using System.Globalization;

namespace CsvHelper;

/// <summary>When <see cref="CsvWriter"/> encloses fields in quotes.</summary>
public enum CsvQuoting
{
	/// <summary>
	/// Quote only fields that contain the separator, a quote, CR or LF, as RFC 4180 requires (the default).
	/// A record whose only field is empty is written as <c>""</c> so it is not mistaken for an empty line.
	/// </summary>
	WhenNeeded = 0,

	/// <summary>Quote every field. Also valid RFC 4180.</summary>
	Always = 1,

	/// <summary>
	/// Quote the fields that need it and those that start or end with a space or a tab, so that readers that trim
	/// whitespace (outside quotes) keep it, as CsvHelper 33 and earlier did by default. Also valid RFC 4180.
	/// </summary>
	WhenNeededOrWhitespace = 2,
}

/// <summary>
/// What <see cref="CsvWriter"/> does with a text field that starts with one of <see cref="CsvWriterOptions.InjectionCharacters"/>,
/// which a spreadsheet such as Excel would run as a formula when it opens the file (CSV injection).
/// </summary>
public enum CsvInjection
{
	/// <summary>Write the field as it is (the default).</summary>
	None = 0,

	/// <summary>
	/// Put <see cref="CsvWriterOptions.InjectionEscape"/> in front of the field, so a spreadsheet shows it as text. In
	/// <see cref="CsvMode.Rfc4180"/> mode the field is also quoted.
	/// </summary>
	Escape = 1,

	/// <summary>Remove the injection characters from the start of the field.</summary>
	Strip = 2,

	/// <summary>Throw an <see cref="InvalidOperationException"/>; the record is discarded.</summary>
	Throw = 3,
}

/// <summary>
/// Decides whether <see cref="CsvWriter"/> quotes a field in <see cref="CsvMode.Rfc4180"/> mode
/// (<see cref="CsvWriterOptions.ShouldQuote"/>). A field that contains the separator, a quote, CR or LF is quoted whatever
/// it returns.
/// </summary>
/// <param name="field">The text of the field, as it will be written (before quoting).</param>
/// <param name="fieldType">
/// The type of the value: <see cref="string"/> for text (strings, character and UTF-8 spans), else the type passed to
/// <c>WriteField</c>, such as <see cref="int"/> or <see cref="DateTime"/>.
/// </param>
/// <param name="fieldIndex">The 0-based index of the field in its record.</param>
/// <param name="recordIndex">
/// The 0-based index of the record (<see cref="CsvWriter.RecordCount"/> before it ends): 0 is the header when the output
/// has one. In a <see cref="CsvWriter.ParallelWrite{T}(IEnumerable{T}, Action{CsvWriter, T}, int)"/> callback, records
/// are numbered as if every item wrote one.
/// </param>
/// <returns><see langword="true"/> to quote the field.</returns>
public delegate bool CsvShouldQuote(ReadOnlySpan<char> field, Type fieldType, int fieldIndex, long recordIndex);

/// <summary>
/// Options for <see cref="CsvWriter"/>. By default the output follows RFC 4180: records end with CRLF, fields
/// that need it are enclosed in double quotes, and quotes inside them are doubled. <see cref="Mode"/> selects the
/// other formats.
/// </summary>
public sealed record CsvWriterOptions
{
	/// <summary>The default options: RFC 4180, comma separated, quote when needed, consistent field counts.</summary>
	public static CsvWriterOptions Default { get; } = new();

	/// <summary>
	/// The options for the files of <typeparamref name="T"/>: the defaults with the settings of the type's
	/// <see cref="CsvOptionsAttribute"/>, or <see cref="Default"/> when it has none. Change the rest with a
	/// <see langword="with"/> expression, such as <c>CsvWriterOptions.For&lt;Order&gt;() with { Quoting = CsvQuoting.Always }</c>.
	/// </summary>
	/// <typeparam name="T">A <see cref="CsvRecordAttribute"/> type, or a type mapped by a <see cref="CsvRecordMapAttribute"/>.</typeparam>
	/// <returns>The options, the same instance on every call.</returns>
	/// <exception cref="System.Globalization.CultureNotFoundException">
	/// The attribute names a culture that does not exist (or the app runs in globalization invariant mode).
	/// </exception>
	public static CsvWriterOptions For<T>() => CsvRecordMapper.WriterOptions<T>();

	/// <summary>
	/// How fields are delimited and escaped. Records end with <see cref="NewLine"/> in every mode.
	/// <list type="bullet">
	/// <item><see cref="CsvMode.Rfc4180"/> (the default): fields are quoted as <see cref="Quoting"/> says.</item>
	/// <item>
	/// <see cref="CsvMode.Escape"/>: no quotes; <see cref="Escape"/> is written before every separator, escape
	/// character, CR and LF in a value. A record whose only field is empty is an empty line.
	/// </item>
	/// <item>
	/// <see cref="CsvMode.NoEscape"/>: values are written as they are, so a value that contains the separator or a line
	/// break changes the fields or records that a reader sees. A record whose only field is empty is an empty line.
	/// </item>
	/// <item>
	/// <see cref="CsvMode.FixedWidth"/>: no separators; each value is padded on the right with spaces to the width of
	/// its column (<see cref="FieldWidths"/>). A longer value, a value with a line break, or a field past the last column
	/// throws <see cref="InvalidOperationException"/> and the record is discarded.
	/// </item>
	/// </list>
	/// </summary>
	public CsvMode Mode { get; init; } = CsvMode.Rfc4180;

	/// <summary>
	/// The field separator. RFC 4180 specifies a comma (the default). Another ASCII separator can be used
	/// (for example <c>;</c> or tab); quoting still follows the RFC rules for that separator. Not used in
	/// <see cref="CsvMode.FixedWidth"/> mode.
	/// </summary>
	public char Separator { get; init; } = ',';

	/// <summary>
	/// Whether <see cref="CsvWriter.WriteRecords{T}(IEnumerable{T})"/> and the other methods that write a sequence of
	/// records start the output with the header of the record type. Defaults to <see langword="true"/>.
	/// <see cref="CsvWriter.WriteHeader{T}"/> writes a header either way.
	/// </summary>
	public bool HasHeader { get; init; } = true;

	/// <summary>
	/// When fields are quoted in <see cref="CsvMode.Rfc4180"/> mode. Defaults to <see cref="CsvQuoting.WhenNeeded"/>.
	/// </summary>
	public CsvQuoting Quoting { get; init; } = CsvQuoting.WhenNeeded;

	/// <summary>
	/// Called for each field in <see cref="CsvMode.Rfc4180"/> mode to decide whether to quote a field that does not need
	/// quotes, such as text fields but not numbers. A field that contains the separator, a quote, CR or LF is always
	/// quoted, and with <see cref="CsvQuoting.Always"/> every field is. Defaults to <see langword="null"/>. Writing is
	/// slower with a callback, which gets the text of every field. The parallel writes of <see cref="CsvWriter"/> call it
	/// on several threads at once.
	/// </summary>
	public CsvShouldQuote? ShouldQuote { get; init; }

	/// <summary>
	/// The text written for <see langword="null"/>: a <see langword="null"/> string, object or <see cref="Nullable{T}"/>,
	/// such as <c>NULL</c> or <c>\N</c> for a database import. Defaults to <see langword="null"/>: an empty field. Character
	/// and UTF-8 spans cannot be <see langword="null"/>, so an empty span is an empty field whatever this is. A reader
	/// with the same value in <see cref="CsvReaderOptions.NullValues"/> reads it back as <see langword="null"/>.
	/// </summary>
	public string? NullValue { get; init; }

	/// <summary>The text written for <see langword="true"/>. Defaults to <c>True</c>.</summary>
	public string TrueValue { get; init; } = "True";

	/// <summary>The text written for <see langword="false"/>. Defaults to <c>False</c>.</summary>
	public string FalseValue { get; init; } = "False";

	/// <summary>
	/// The line ending written after each record and comment: <c>"\r\n"</c> (the default, which RFC 4180 specifies),
	/// <c>"\n"</c> or <c>"\r"</c>. Fields that contain CR or LF are still quoted or escaped whichever is chosen. In
	/// <see cref="CsvMode.Escape"/> mode with <c>"\n"</c>, a record whose last value ends with a CR ends with CRLF, because a
	/// reader takes an LF right after an escaped CR as part of the value.
	/// </summary>
	public string NewLine { get; init; } = "\r\n";

	/// <summary>
	/// The character that starts the comment lines that <see cref="CsvWriter.WriteComment(string)"/> writes, such as
	/// <c>#</c>. Defaults to <see langword="null"/>: no comments. When it is set, a record whose first field starts with
	/// it has that field quoted (<see cref="CsvMode.Rfc4180"/>) or escaped (<see cref="CsvMode.Escape"/>), so that a
	/// reader with the same <see cref="CsvReaderOptions.CommentPrefix"/> does not skip the record; such a record
	/// throws <see cref="InvalidOperationException"/> in <see cref="CsvMode.FixedWidth"/> mode and is written as it is in
	/// <see cref="CsvMode.NoEscape"/> mode. Must be an ASCII character other than the separator, the quote (in
	/// <see cref="CsvMode.Rfc4180"/> mode) and <see cref="Escape"/> (in <see cref="CsvMode.Escape"/> mode).
	/// </summary>
	public char? CommentPrefix { get; init; }

	/// <summary>
	/// What to do with text fields that start with one of <see cref="InjectionCharacters"/>, which a spreadsheet would run
	/// as a formula. Defaults to <see cref="CsvInjection.None"/>. It applies to text (strings, character and UTF-8 spans,
	/// and values written with <see cref="object.ToString"/>), not to numbers, dates, times, <see cref="Guid"/>s and
	/// booleans, so <c>-5</c> stays a number.
	/// </summary>
	public CsvInjection Injection { get; init; }

	/// <summary>
	/// The characters that make a text field a possible formula when it starts with one of them. Defaults to
	/// <c>= @ + -</c>, tab and CR, as OWASP recommends. Must be ASCII.
	/// </summary>
	public string InjectionCharacters { get; init; } = "=@+-\t\r";

	/// <summary>
	/// The character that <see cref="CsvInjection.Escape"/> puts in front of a possible formula. Defaults to <c>'</c>,
	/// which Excel reads as "this cell is text". Must be an ASCII character other than CR, LF, the separator and the
	/// <see cref="InjectionCharacters"/>.
	/// </summary>
	public char InjectionEscape { get; init; } = '\'';

	/// <summary>
	/// The escape character of <see cref="CsvMode.Escape"/> mode. Defaults to <c>\</c>. Must be an ASCII character other
	/// than space, tab and the separator.
	/// </summary>
	public char Escape { get; init; } = '\\';

	/// <summary>
	/// The widths of the columns in <see cref="CsvMode.FixedWidth"/> mode, in characters (UTF-16 code units, as
	/// <see cref="string.Length"/> counts them). Required in that mode; every width must be positive.
	/// </summary>
	public IReadOnlyList<int>? FieldWidths { get; init; }

	/// <summary>
	/// Whether every record must have the same number of fields as the first one, as RFC 4180 requires (in
	/// <see cref="CsvMode.FixedWidth"/> mode, a field for every column). Defaults to <see langword="true"/>; a mismatch
	/// throws <see cref="InvalidOperationException"/>.
	/// </summary>
	public bool ValidateFieldCount { get; init; } = true;

	/// <summary>
	/// The format provider for numbers and dates. Defaults to <see cref="CultureInfo.InvariantCulture"/>.
	/// </summary>
	public IFormatProvider FormatProvider { get; init; } = CultureInfo.InvariantCulture;

	/// <summary>Whether to start the output with a UTF-8 byte order mark. Defaults to <see langword="false"/>.</summary>
	public bool WriteByteOrderMark { get; init; }

	/// <summary>The size in bytes of the write buffer. Defaults to 64 KiB.</summary>
	public int BufferSize { get; init; } = 64 * 1024;

	internal void Validate()
	{
		if (Mode is < CsvMode.Rfc4180 or > CsvMode.FixedWidth) throw new ArgumentOutOfRangeException(nameof(Mode), Mode, "Unknown mode.");
		if (BufferSize < 256) throw new ArgumentOutOfRangeException(nameof(BufferSize), "BufferSize must be at least 256 bytes.");
		if (Quoting is < CsvQuoting.WhenNeeded or > CsvQuoting.WhenNeededOrWhitespace) throw new ArgumentOutOfRangeException(nameof(Quoting), Quoting, "Unknown quoting.");
		if (TrueValue is null)
		{
			throw new ArgumentNullException(nameof(TrueValue));
		}

		if (FalseValue is null)
		{
			throw new ArgumentNullException(nameof(FalseValue));
		}
		if (NewLine is not ("\r\n" or "\n" or "\r"))
		{
			throw new ArgumentException("NewLine must be \"\\r\\n\", \"\\n\" or \"\\r\".", nameof(NewLine));
		}

		ValidateInjection();
		if (Mode == CsvMode.FixedWidth)
		{
			FixedWidth.Validate(FieldWidths, nameof(FieldWidths));
			if (CommentPrefix is char prefix)
			{
				CsvReaderOptions.ValidateAscii(prefix, nameof(CommentPrefix));
			}

			return;
		}

		var s = Separator;
		if (s == '\0' || s > 0x7F || s == '\r' || s == '\n' || (s == '"' && Mode == CsvMode.Rfc4180))
			throw new ArgumentException("Separator must be an ASCII character other than CR, LF and the quote.", nameof(Separator));
		if (Mode == CsvMode.Escape)
		{
			CsvReaderOptions.ValidateAscii(Escape, nameof(Escape));
			if (Escape == ' ' || Escape == '\t') throw new ArgumentException("Escape cannot be a space or a tab.", nameof(Escape));
			if (Escape == s) throw new ArgumentException("Separator and Escape must differ.", nameof(Escape));
		}

		if (CommentPrefix is char c)
		{
			CsvReaderOptions.ValidateAscii(c, nameof(CommentPrefix));
			if (c == s)
			{
				throw new ArgumentException("Separator and CommentPrefix must differ.", nameof(CommentPrefix));
			}

			if (Mode == CsvMode.Rfc4180 && c == '"')
			{
				throw new ArgumentException("CommentPrefix cannot be the quote.", nameof(CommentPrefix));
			}

			if (Mode == CsvMode.Escape && c == Escape)
			{
				throw new ArgumentException("Escape and CommentPrefix must differ.", nameof(CommentPrefix));
			}
		}

		if (Injection == CsvInjection.Escape && InjectionEscape == s)
		{
			throw new ArgumentException("InjectionEscape cannot be the separator.", nameof(InjectionEscape));
		}
	}

	private void ValidateInjection()
	{
		if (Injection is < CsvInjection.None or > CsvInjection.Throw)
		{
			throw new ArgumentOutOfRangeException(nameof(Injection), Injection, "Unknown injection handling.");
		}

		if (InjectionCharacters is null)
		{
			throw new ArgumentNullException(nameof(InjectionCharacters));
		}

		foreach (var c in InjectionCharacters)
		{
			if (c == '\0' || c > 0x7F)
			{
				throw new ArgumentException("InjectionCharacters must be ASCII characters.", nameof(InjectionCharacters));
			}
		}

		if (Injection != CsvInjection.Escape)
		{
			return;
		}

		var e = InjectionEscape;
		if (e == '\0' || e > 0x7F || e == '\r' || e == '\n' || InjectionCharacters.IndexOf(e) >= 0)
		{
			throw new ArgumentException("InjectionEscape must be an ASCII character other than CR, LF and the InjectionCharacters.", nameof(InjectionEscape));
		}
	}
}
