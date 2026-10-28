namespace CsvHelper;

/// <summary>How fields are delimited and escaped, for <see cref="CsvReader"/> and <see cref="CsvWriter"/>.</summary>
public enum CsvMode
{
	/// <summary>
	/// RFC 4180 (the default). A quote at the start of a field opens a quoted field in which separators and line breaks
	/// are ordinary text and a doubled quote (<c>""</c>) is an escaped quote. When reading, a quote that appears anywhere
	/// else in an unquoted field is kept as an ordinary character.
	/// </summary>
	Rfc4180 = 0,

	/// <summary>
	/// No quoting: the escape character (<see cref="CsvReaderOptions.Escape"/>, <c>\</c> by default) makes the character
	/// after it ordinary text. <c>a\,b</c> is the value <c>a,b</c>, <c>\\</c> is <c>\</c>, and an escape before a line
	/// break keeps the line break in the field (before CR LF, the pair). Any other escaped character is itself:
	/// <c>\n</c> is the letter <c>n</c>. Quotes are ordinary text.
	/// </summary>
	Escape = 1,

	/// <summary>
	/// No quoting or escaping: every character except the separator and line breaks is text, read and written as is
	/// (TSV exports, logs).
	/// </summary>
	NoEscape = 2,

	/// <summary>
	/// Fixed-width columns without separators. Each line is a record, split into columns by
	/// <see cref="CsvReaderOptions.FieldWidths"/>, counted in characters (UTF-16 code units, as <see cref="string.Length"/>
	/// counts them).
	/// </summary>
	FixedWidth = 3,
}
