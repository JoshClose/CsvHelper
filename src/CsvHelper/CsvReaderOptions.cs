using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CsvHelper;

/// <summary>What the reader does when the input breaks the quoting rules.</summary>
public enum CsvMalformedDataHandling
{
	/// <summary>
	/// Recover and keep reading (the default). A stray quote inside an unquoted field is kept as text,
	/// text after a closing quote is appended to the value (<c>"ab"c</c> reads as <c>abc</c>), and a
	/// quoted field that is never closed runs to the end of the input.
	/// </summary>
	Recover = 0,

	/// <summary>Throw a <see cref="CsvFormatException"/> when a malformed record is reached.</summary>
	Throw = 1,

	/// <summary>Skip records that contain malformed quoting (report them with <see cref="CsvReaderOptions.OnMalformedRecord"/>).</summary>
	Skip = 2,
}

/// <summary>What the reader does when a record's field count differs from the header or first record.</summary>
public enum CsvFieldCountHandling
{
	/// <summary>Records may have any number of fields (the default).</summary>
	Allow = 0,

	/// <summary>Throw a <see cref="CsvFormatException"/> when a record has a different number of fields.</summary>
	Throw = 1,

	/// <summary>Skip records with a different number of fields (report them with <see cref="CsvReaderOptions.OnMalformedRecord"/>).</summary>
	Skip = 2,
}

/// <summary>Why a record was reported as malformed.</summary>
public enum CsvMalformedReason
{
	/// <summary>A quote inside an unquoted field, such as <c>ab"c</c>.</summary>
	StrayQuote,
	/// <summary>Text after the closing quote of a quoted field, such as <c>"ab"c</c>.</summary>
	TextAfterClosingQuote,
	/// <summary>A quoted field that is never closed before the end of the input.</summary>
	UnterminatedQuote,
	/// <summary>A line break inside a quoted field while <see cref="CsvReaderOptions.AllowLineBreaksInQuotes"/> is off.</summary>
	LineBreakInQuotes,
	/// <summary>The record's field count differs from the header (or first record).</summary>
	FieldCountMismatch,
	/// <summary>
	/// In <see cref="CsvMode.Escape"/> mode, the input ends with an escape character that has nothing to escape. It is
	/// kept as text.
	/// </summary>
	TrailingEscape,
}

/// <summary>Describes a malformed record passed to <see cref="CsvReaderOptions.OnMalformedRecord"/>.</summary>
/// <param name="RecordNumber">
/// The 1-based number of the record, counting the header and the records returned before it (not skipped records,
/// empty lines or comments), as in <see cref="CsvFormatException.RecordNumber"/>.
/// </param>
/// <param name="Reason">
/// What is wrong with the record. A record with several quoting problems is reported once, with the first one.
/// </param>
/// <param name="Skipped">
/// Whether the reader skips the record (<see cref="CsvMalformedDataHandling.Skip"/>, or a field count mismatch);
/// otherwise it recovers and returns the record.
/// </param>
/// <param name="ByteOffset">
/// The offset, in bytes of UTF-8 input, of the problem (malformed quoting) or of the start of the record (field count
/// mismatch).
/// </param>
/// <param name="RawRecord">The record as it appears in the input, without the line break.</param>
/// <param name="LineNumber">
/// The 1-based line of the input where the record starts, counting every line (also those inside quoted fields), when
/// <see cref="CsvReaderOptions.TrackLineNumbers"/> is on; 0 otherwise.
/// </param>
public readonly record struct CsvMalformedRecord(
	long RecordNumber, CsvMalformedReason Reason, bool Skipped, long ByteOffset, string RawRecord, long LineNumber = 0);

/// <summary>
/// Decides whether the reader skips a record (<see cref="CsvReaderOptions.ShouldSkipRecord"/>), such as a total or a
/// section title in the middle of the data.
/// </summary>
/// <param name="record">The record, which is valid during the call only.</param>
/// <returns><see langword="true"/> to skip the record.</returns>
public delegate bool CsvShouldSkipRecord(CsvRow record);

/// <summary>Describes a record that could not be mapped to its type, passed to <see cref="CsvReaderOptions.OnRecordError"/>.</summary>
/// <param name="RowNumber">The 1-based number of the data record, as <see cref="CsvReader.RowNumber"/> counts it.</param>
/// <param name="ByteOffset">The offset, in bytes of UTF-8 input, of the start of the record (<see cref="CsvReader.ByteOffset"/>).</param>
/// <param name="Exception">
/// A <see cref="CsvFormatException"/> that names the record and, when one is at fault, the member and its field
/// (<see cref="CsvFormatException.Member"/>, <see cref="CsvFormatException.FieldIndex"/>). Its
/// <see cref="Exception.InnerException"/> is what the mapper threw, such as a <see cref="FormatException"/> for a field
/// that does not parse, or an exception of a converter. It is the exception thrown when the handler returns
/// <see langword="false"/>.
/// </param>
/// <param name="RawRecord">The record as it appears in the input, without the line break.</param>
public readonly record struct CsvRecordError(long RowNumber, long ByteOffset, Exception Exception, string RawRecord);

/// <summary>What happens when a field past the end of the current record is requested.</summary>
public enum CsvMissingFieldHandling
{
	/// <summary>Missing fields read as empty (the default). Short, ragged records just work.</summary>
	ReturnEmpty = 0,

	/// <summary>Throw an <see cref="IndexOutOfRangeException"/>.</summary>
	Throw = 1,
}

/// <summary>Options for <see cref="CsvReader"/>. The defaults adapt to most real-world files.</summary>
public sealed record CsvReaderOptions
{
	/// <summary>The default options.</summary>
	public static CsvReaderOptions Default { get; } = new();

	/// <summary>
	/// The options for the files of <typeparamref name="T"/>: the defaults with the settings of the type's
	/// <see cref="CsvOptionsAttribute"/>, or <see cref="Default"/> when it has none. Change the rest with a
	/// <see langword="with"/> expression, such as <c>CsvReaderOptions.For&lt;Order&gt;() with { PoolStrings = true }</c>.
	/// </summary>
	/// <typeparam name="T">A <see cref="CsvRecordAttribute"/> type, or a type mapped by a <see cref="CsvRecordMapAttribute"/>.</typeparam>
	/// <returns>The options, the same instance on every call.</returns>
	/// <exception cref="CultureNotFoundException">
	/// The attribute names a culture that does not exist (or the app runs in globalization invariant mode).
	/// </exception>
	public static CsvReaderOptions For<T>() => CsvRecordMapper.ReaderOptions<T>();

	/// <summary>
	/// The field separator. When <see langword="null"/> (the default), the separator is detected from
	/// the first lines of the input, choosing among <c>,</c> <c>;</c> <c>\t</c> and <c>|</c>.
	/// Must be an ASCII character. Not used in <see cref="CsvMode.FixedWidth"/> mode.
	/// </summary>
	public char? Separator { get; init; }

	/// <summary>Whether the first record is a header row. Defaults to <see langword="true"/>.</summary>
	public bool HasHeader { get; init; } = true;

	/// <summary>
	/// How fields are delimited and escaped. Defaults to <see cref="CsvMode.Rfc4180"/>; see <see cref="CsvMode"/> for
	/// the others.
	/// </summary>
	public CsvMode Mode { get; init; } = CsvMode.Rfc4180;

	/// <summary>
	/// The quote character of <see cref="CsvMode.Rfc4180"/> mode. Defaults to <c>"</c>. Must be an ASCII character.
	/// </summary>
	public char Quote { get; init; } = '"';

	/// <summary>
	/// The escape character of <see cref="CsvMode.Escape"/> mode. Defaults to <c>\</c>. Must be an ASCII character other
	/// than space and tab.
	/// </summary>
	public char Escape { get; init; } = '\\';

	/// <summary>
	/// The widths of the columns in <see cref="CsvMode.FixedWidth"/> mode, in characters (UTF-16 code units, as
	/// <see cref="string.Length"/> counts them), from the start of the line. Required in that mode; every width must
	/// be positive. Text after the last column is not part of any field (it is still part of
	/// <see cref="CsvReader.RawRecord"/>), and a short line has empty fields at its end.
	/// </summary>
	public IReadOnlyList<int>? FieldWidths { get; init; }

	/// <summary>
	/// Whether quoted fields may contain line breaks (RFC 4180). Defaults to <see langword="true"/>.
	/// Set to <see langword="false"/> to recover from files with unbalanced quotes: every line break then
	/// ends the record, so one bad quote cannot swallow the rest of the file.
	/// </summary>
	public bool AllowLineBreaksInQuotes { get; init; } = true;

	/// <summary>Whether completely empty lines are skipped. Defaults to <see langword="true"/>.</summary>
	public bool SkipEmptyLines { get; init; } = true;

	/// <summary>
	/// When set, lines that start with this character are skipped as comments. Defaults to
	/// <see langword="null"/> (no comments). Must be an ASCII character.
	/// </summary>
	public char? CommentPrefix { get; init; }

	/// <summary>
	/// Whether spaces and tabs around field values are removed. Whitespace inside quotes is kept, a quote that follows
	/// leading whitespace still opens a quoted field, and escaped whitespace is kept. Defaults to
	/// <see langword="null"/>: values are trimmed in <see cref="CsvMode.FixedWidth"/> mode (where columns are padded)
	/// and not otherwise.
	/// </summary>
	public bool? TrimWhitespace { get; init; }

	/// <summary>
	/// Whether whitespace at the start and end of the value of a quoted field is removed too (<c>" a "</c> reads as
	/// <c>a</c>). Defaults to <see langword="false"/>. Independent of <see cref="TrimWhitespace"/>, which trims outside
	/// the quotes. <see cref="CsvMode.Rfc4180"/> mode only.
	/// </summary>
	public bool TrimInsideQuotes { get; init; }

	/// <summary>
	/// The characters that <see cref="TrimWhitespace"/> and <see cref="TrimInsideQuotes"/> remove, and that may come
	/// before the opening quote of a field when trimming. Defaults to space and tab. Must be ASCII characters other than
	/// CR, LF, the quote and the escape character; the separator still separates fields.
	/// </summary>
	public string WhitespaceCharacters { get; init; } = " \t";

	/// <summary>
	/// The largest record, in bytes of UTF-8 input, that the reader grows its buffer for, which guards against hostile
	/// input such as an unclosed quote at the start of a large file. Defaults to <see langword="null"/>: records grow the
	/// buffer up to about 2 GiB. Checked only when the reader needs more input, so it costs nothing per record: a record
	/// that is not complete yet and longer than the limit throws a <see cref="CsvFormatException"/>, while a longer record
	/// that is already in the buffer (see <see cref="BufferSize"/>) is read, and the buffer never grows past about twice
	/// the limit. In-memory input (a byte array or <see cref="ReadOnlyMemory{T}"/>) is not buffered and not checked.
	/// </summary>
	public int? MaxRecordSize { get; init; }

	/// <summary>
	/// Whether the reader counts the lines of the input, for <see cref="CsvReader.LineNumber"/> and the line numbers of
	/// errors: every line, including those inside quoted fields, empty lines and comments. Defaults to
	/// <see langword="false"/>, since counting looks at every record (reading is a little slower).
	/// </summary>
	public bool TrackLineNumbers { get; init; }

	/// <summary>
	/// Whether exception messages contain field values, such as <c>'x' is not a valid boolean.</c> Defaults to
	/// <see langword="true"/>. Set to <see langword="false"/> to keep values, which may be personal data, out of logs: the
	/// messages of the typed getters, <c>Parse</c> and the generated record mappers then name the type but not the value.
	/// Exceptions of your own parsers and converters are not changed.
	/// </summary>
	public bool IncludeValuesInExceptions { get; init; } = true;

	/// <summary>
	/// Changes header names before they are matched, both those of the input and those that are looked up (by
	/// <see cref="CsvReader.GetFieldIndex(string)"/>, the by-name getters and the record mappers), such as
	/// <c>name => name.Replace("_", "")</c> so that <c>unit_price</c> matches <c>UnitPrice</c>. Matching then works as
	/// usual: exactly, then ignoring case and surrounding whitespace. <see cref="CsvReader.Headers"/> keeps the names as
	/// they are. Defaults to <see langword="null"/>.
	/// </summary>
	public Func<string, string>? NormalizeHeaderName { get; init; }

	/// <summary>
	/// Called for every data record (not the header) before <see cref="CsvReader.Read"/> returns it; returning
	/// <see langword="true"/> skips the record, so <see cref="CsvReader.GetRecords{T}"/> never sees it. The record's
	/// <see cref="CsvRow.RowNumber"/> is the number it gets when it is not skipped. Defaults to <see langword="null"/>.
	/// </summary>
	public CsvShouldSkipRecord? ShouldSkipRecord { get; init; }

	/// <summary>
	/// Called when a record of a <see cref="CsvRecordAttribute"/> type cannot be mapped, such as a field that does not
	/// parse, by <see cref="CsvReader.GetRecords{T}"/>, <see cref="CsvReader.GetRecordsAsync{T}"/>,
	/// <see cref="CsvReader.EnumerateRecords{T}(T)"/> and <see cref="CsvReader.ParallelEnumerate{T}(int)"/> (where it can
	/// be called on several threads at once). Return <see langword="true"/> to skip the record and go on, or
	/// <see langword="false"/> to throw the exception. Defaults to <see langword="null"/>: the exception ends the
	/// enumeration.
	/// </summary>
	public Func<CsvRecordError, bool>? OnRecordError { get; init; }

	/// <summary>
	/// Field values that mean <see langword="null"/>, such as <c>NULL</c> or <c>\N</c>, matched exactly (after trimming):
	/// for <see cref="CsvReader.IsNull(int)"/>, <see cref="CsvReader.GetStringOrNull(int)"/>, <c>ParseOrNull</c>, and the
	/// <see cref="Nullable{T}"/> and nullable string members of <see cref="CsvRecordAttribute"/> types. An empty field is
	/// <see langword="null"/> for those too, except for strings, which read it as <c>""</c> unless <c>""</c> is listed.
	/// Defaults to <see langword="null"/>.
	/// </summary>
	public IReadOnlyList<string>? NullValues { get; init; }

	/// <summary>
	/// The values that <see cref="CsvReader.GetBoolean(int)"/> reads as <see langword="true"/>, instead of <c>true</c>,
	/// <c>yes</c>, <c>y</c>, <c>t</c> and <c>1</c>; matched ignoring ASCII case and surrounding spaces and tabs.
	/// Defaults to <see langword="null"/>: the built-in values.
	/// </summary>
	public IReadOnlyList<string>? TrueValues { get; init; }

	/// <summary>
	/// The values that <see cref="CsvReader.GetBoolean(int)"/> reads as <see langword="false"/>, instead of <c>false</c>,
	/// <c>no</c>, <c>n</c>, <c>f</c> and <c>0</c>; matched ignoring ASCII case and surrounding spaces and tabs.
	/// Defaults to <see langword="null"/>: the built-in values.
	/// </summary>
	public IReadOnlyList<string>? FalseValues { get; init; }

	/// <summary>What to do with malformed quoting. Defaults to <see cref="CsvMalformedDataHandling.Recover"/>.</summary>
	public CsvMalformedDataHandling MalformedData { get; init; } = CsvMalformedDataHandling.Recover;

	/// <summary>What to do when records have different field counts. Defaults to <see cref="CsvFieldCountHandling.Allow"/>.</summary>
	public CsvFieldCountHandling FieldCountMismatch { get; init; } = CsvFieldCountHandling.Allow;

	/// <summary>What happens when a missing field is requested. Defaults to <see cref="CsvMissingFieldHandling.ReturnEmpty"/>.</summary>
	public CsvMissingFieldHandling MissingField { get; init; } = CsvMissingFieldHandling.ReturnEmpty;

	/// <summary>
	/// Called for every malformed record found (bad quoting, or a field count mismatch when
	/// <see cref="FieldCountMismatch"/> is not <see cref="CsvFieldCountHandling.Allow"/>), whether it was
	/// recovered or skipped. Setting it enables malformed quote detection, which is a little slower on
	/// heavily quoted data. Not called for records that cause an exception.
	/// </summary>
	public Action<CsvMalformedRecord>? OnMalformedRecord { get; init; }

	/// <summary>
	/// Whether a first line of the form <c>sep=;</c> (written by Excel) sets the separator and is skipped.
	/// Only applies when <see cref="Separator"/> is not set. Defaults to <see langword="true"/>.
	/// </summary>
	public bool DetectSeparatorHint { get; init; } = true;

	/// <summary>
	/// The encoding of stream input. When <see langword="null"/> (the default), byte order marks are
	/// honored and UTF-8 is assumed otherwise. Non UTF-8 input is transcoded to UTF-8 while reading.
	/// </summary>
	public Encoding? Encoding { get; init; }

	/// <summary>
	/// The format provider used for parsing numbers and dates. Defaults to
	/// <see cref="CultureInfo.InvariantCulture"/>.
	/// </summary>
	public IFormatProvider FormatProvider { get; init; } = CultureInfo.InvariantCulture;

	/// <summary>
	/// Whether repeated string values are de-duplicated per column, so reading many records with the same
	/// values allocates each distinct string once. Columns whose values rarely repeat switch pooling off by themselves.
	/// Defaults to <see langword="false"/>: a lookup costs about as much as creating a short string, so pooling pays off
	/// when the strings are kept (many records held in memory, where it saves memory and garbage collection work), not
	/// when each string is used and dropped.
	/// </summary>
	public bool PoolStrings { get; init; }

	/// <summary>The size in bytes of the read buffer. Defaults to 64 KiB. Grows automatically for long records.</summary>
	public int BufferSize { get; init; } = 64 * 1024;

	internal void Validate()
	{
		if (Mode is < CsvMode.Rfc4180 or > CsvMode.FixedWidth) throw new ArgumentOutOfRangeException(nameof(Mode), Mode, "Unknown mode.");
		if (Separator is char s) ValidateAscii(s, nameof(Separator));
		ValidateAscii(Quote, nameof(Quote));
		ValidateAscii(Escape, nameof(Escape));
		if (CommentPrefix is char c) ValidateAscii(c, nameof(CommentPrefix));
		if (BufferSize < 256) throw new ArgumentOutOfRangeException(nameof(BufferSize), "BufferSize must be at least 256 bytes.");
		if (MaxRecordSize is <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(MaxRecordSize), MaxRecordSize, "MaxRecordSize must be positive.");
		}

		ValidateWhitespace();
		ValidateValues(NullValues, nameof(NullValues));
		ValidateValues(TrueValues, nameof(TrueValues));
		ValidateValues(FalseValues, nameof(FalseValues));
		if (TrueValues is { } trueValues && FalseValues is { } falseValues)
		{
			foreach (var value in trueValues)
			{
				foreach (var other in falseValues)
				{
					if (string.Equals(value.Trim(' ', '\t'), other.Trim(' ', '\t'), StringComparison.OrdinalIgnoreCase))
					{
						throw new ArgumentException($"'{value}' is both a true and a false value.", nameof(FalseValues));
					}
				}
			}
		}

		if (Mode == CsvMode.FixedWidth)
		{
			FixedWidth.Validate(FieldWidths, nameof(FieldWidths));

			return;
		}

		if (Mode == CsvMode.Escape && (Escape == ' ' || Escape == '\t'))
			throw new ArgumentException("Escape cannot be a space or a tab.", nameof(Escape));
		if (Separator is char sep)
		{
			if (Mode == CsvMode.Rfc4180 && sep == Quote)
				throw new ArgumentException("Separator and Quote must differ.", nameof(Separator));
			if (Mode == CsvMode.Escape && sep == Escape)
				throw new ArgumentException("Separator and Escape must differ.", nameof(Separator));
			if (CommentPrefix == sep)
				throw new ArgumentException("Separator and CommentPrefix must differ.", nameof(CommentPrefix));
		}

		if (CommentPrefix is char cp)
		{
			if (Mode == CsvMode.Rfc4180 && cp == Quote)
				throw new ArgumentException("Quote and CommentPrefix must differ.", nameof(CommentPrefix));
			if (Mode == CsvMode.Escape && cp == Escape)
				throw new ArgumentException("Escape and CommentPrefix must differ.", nameof(CommentPrefix));
		}
	}

	internal static void ValidateAscii(char c, string name)
	{
		if (c == '\0' || c > 0x7F || c == '\r' || c == '\n')
			throw new ArgumentException($"{name} must be a printable ASCII character (or tab).", name);
	}

	private void ValidateWhitespace()
	{
		if (WhitespaceCharacters is null)
		{
			throw new ArgumentNullException(nameof(WhitespaceCharacters));
		}

		foreach (var w in WhitespaceCharacters)
		{
			ValidateAscii(w, nameof(WhitespaceCharacters));
			if ((Mode == CsvMode.Rfc4180 && w == Quote) || (Mode == CsvMode.Escape && w == Escape))
			{
				throw new ArgumentException("WhitespaceCharacters cannot contain the quote or the escape character.", nameof(WhitespaceCharacters));
			}
		}
	}

	private static void ValidateValues(IReadOnlyList<string>? values, string name)
	{
		if (values is null)
		{
			return;
		}

		foreach (var value in values)
		{
			if (value is null)
			{
				throw new ArgumentException($"{name} cannot contain null.", name);
			}
		}
	}
}
