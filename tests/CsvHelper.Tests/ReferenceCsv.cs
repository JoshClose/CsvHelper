using System.Text;

namespace CsvHelper.Tests;

/// <summary>Options understood by the reference parser (mirrors the relevant CsvReaderOptions).</summary>
internal sealed record ReferenceOptions
{
	public char Separator { get; init; } = ',';
	// A separator of several characters (read through a CsvSeparatorStream); Separator is not used when it is set.
	public string? SeparatorText { get; init; }
	// Quote set: Rfc4180 mode; else Escape set: Escape mode; else NoEscape.
	public char? Quote { get; init; } = '"';
	public char? Escape { get; init; }
	public bool Trim { get; init; }
	// The characters that trimming removes (WhitespaceCharacters).
	public string Whitespace { get; init; } = " \t";
	// Rfc4180 mode: the values of quoted fields are trimmed too.
	public bool TrimInsideQuotes { get; init; }
	public char? Comment { get; init; }
	public bool SkipEmptyLines { get; init; } = true;
	public bool LineBreaksInQuotes { get; init; } = true;
	public bool SkipMalformed { get; init; }
	public bool SkipFieldCountMismatch { get; init; }
	// Not a reading rule: whether the reader under test pools strings (on, so that tests exercise the pool too).
	public bool PoolStrings { get; init; } = true;

	public CsvReaderOptions ToReaderOptions(bool hasHeader = false, int bufferSize = 64 * 1024) => new()
	{
		MalformedData = SkipMalformed ? CsvMalformedDataHandling.Skip : CsvMalformedDataHandling.Recover,
		FieldCountMismatch = SkipFieldCountMismatch ? CsvFieldCountHandling.Skip : CsvFieldCountHandling.Allow,
		Separator = Separator,
		Quote = Quote ?? '"',
		Escape = Escape ?? '\\',
		Mode = Quote is not null ? CsvMode.Rfc4180 : Escape is not null ? CsvMode.Escape : CsvMode.NoEscape,
		TrimWhitespace = Trim,
		WhitespaceCharacters = Whitespace,
		TrimInsideQuotes = TrimInsideQuotes,
		CommentPrefix = Comment,
		SkipEmptyLines = SkipEmptyLines,
		AllowLineBreaksInQuotes = LineBreaksInQuotes,
		HasHeader = hasHeader,
		BufferSize = bufferSize,
		PoolStrings = PoolStrings,
	};
}

/// <summary>
/// A deliberately simple, character-at-a-time CSV parser implementing the documented reading rules.
/// The SIMD reader is checked against it.
/// </summary>
internal static class ReferenceCsv
{
	private const int FieldStart = 0, Unquoted = 1, Quoted = 2, QuoteInQuoted = 3, AfterQuoted = 4, Comment = 5, Escaped = 6;

	public static List<string[]> Parse(string text, ReferenceOptions o) => Parse(text, o, out _, out _);

	public static List<string[]> Parse(string text, ReferenceOptions o, out int malformed) => Parse(text, o, out malformed, out _);

	/// <summary>Parses, applying the skip options; <paramref name="malformed"/> counts records with bad quoting.</summary>
	public static List<string[]> Parse(string text, ReferenceOptions o, out int malformed, out List<string> badRecords)
	{
		var bads = new List<string>();
		badRecords = bads;
		var sep = o.SeparatorText ?? o.Separator.ToString();
		bool AtSeparator(int at) => at + sep.Length <= text.Length && string.CompareOrdinal(text, at, sep, 0, sep.Length) == 0;
		var records = new List<string[]>();
		var rawFields = new List<string>();
		var raw = new StringBuilder();
		var state = FieldStart;
		var rowStart = true;
		var pending = false;
		var bad = false;
		var badCount = 0;
		var expectedFields = -1;

		void EndField()
		{
			rawFields.Add(raw.ToString());
			raw.Clear();
		}

		void EndRecord()
		{
			EndField();
			var comment = state == Comment;
			if (bad)
			{
				badCount++;
				bads.Add(string.Join(sep, rawFields));
			}

			var skip = comment || (bad && o.SkipMalformed) ||
						(o.SkipEmptyLines && rawFields.Count == 1 && IsBlank(rawFields[0], o));
			if (!skip && o.SkipFieldCountMismatch)
			{
				if (expectedFields < 0) expectedFields = rawFields.Count;
				else if (rawFields.Count != expectedFields) skip = true;
			}

			if (!skip) records.Add(rawFields.Select(f => Decode(f, o)).ToArray());
			rawFields.Clear();
			pending = false;
			bad = false;
		}

		var i = 0;
		while (i < text.Length)
		{
			var c = text[i];
			if (state == Escaped)
			{
				// The character after an escape is text, line breaks too (CR LF as a whole).
				raw.Append(c);
				if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
				{
					raw.Append('\n');
					i++;
				}

				state = Unquoted;
				pending = true;
				i++;
				continue;
			}

			if (c == '\r' || c == '\n')
			{
				if (state == Quoted && o.LineBreaksInQuotes)
				{
					raw.Append(c);
					pending = true;
					i++;
					continue;
				}

				if (state == Quoted) bad = true;
				EndRecord();
				i += c == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
				state = FieldStart;
				rowStart = true;
				continue;
			}

			pending = true;
			var atRowStart = rowStart;
			rowStart = false;
			switch (state)
			{
				case FieldStart:
					if (AtSeparator(i)) { EndField(); i += sep.Length - 1; }
					else if (c == o.Quote) { raw.Append(c); state = Quoted; }
					else if (atRowStart && c == o.Comment) state = Comment;
					else if (c == o.Escape) { raw.Append(c); state = Escaped; }
					else if (o.Trim && IsWhitespace(c, o)) raw.Append(c);
					else { raw.Append(c); state = Unquoted; }
					break;
				case Unquoted:
					if (AtSeparator(i)) { EndField(); state = FieldStart; i += sep.Length - 1; }
					else
					{
						if (c == o.Quote) bad = true;
						raw.Append(c);
						if (c == o.Escape) state = Escaped;
					}

					break;
				case Quoted:
					raw.Append(c);
					if (c == o.Quote) state = QuoteInQuoted;
					break;
				case QuoteInQuoted:
					if (c == o.Quote) { raw.Append(c); state = Quoted; }
					else if (AtSeparator(i)) { EndField(); state = FieldStart; i += sep.Length - 1; }
					else if (o.Trim && IsWhitespace(c, o)) { raw.Append(c); state = AfterQuoted; }
					else { raw.Append(c); state = Unquoted; bad = true; }
					break;
				case AfterQuoted:
					if (AtSeparator(i)) { EndField(); state = FieldStart; i += sep.Length - 1; }
					else
					{
						raw.Append(c);
						if (!IsWhitespace(c, o)) { state = Unquoted; bad = true; }
					}

					break;
				case Comment:
					break;
			}

			i++;
		}

		if (pending)
		{
			if (state == Quoted || state == Escaped) bad = true;
			EndRecord();
		}

		malformed = badCount;

		return records;
	}

	private static bool IsWhitespace(char c, ReferenceOptions o) => o.Whitespace.IndexOf(c) >= 0;

	private static bool IsBlank(string raw, ReferenceOptions o) =>
		raw.Length == 0 || (o.Trim && raw.All(ch => IsWhitespace(ch, o)));

	public static string Decode(string raw, ReferenceOptions o)
	{
		if (o.Quote is null && o.Escape is char e) return Unescape(raw, e, o);
		var s = o.Trim ? raw.Trim(o.Whitespace.ToCharArray()) : raw;
		if (o.Quote is not char q || s.Length == 0 || s[0] != q) return s;
		var sb = new StringBuilder();
		var i = 1;
		while (i < s.Length)
		{
			var c = s[i];
			if (c == q)
			{
				if (i + 1 < s.Length && s[i + 1] == q)
				{
					sb.Append(q);
					i += 2;
					continue;
				}

				sb.Append(s, i + 1, s.Length - i - 1);
				break;
			}

			sb.Append(c);
			i++;
		}

		// The whole value of a quoted field, text after the closing quote included.
		return o.TrimInsideQuotes ? sb.ToString().Trim(o.Whitespace.ToCharArray()) : sb.ToString();
	}

	/// <summary>Escape mode: each escape is dropped and the character after it is text, which trimming keeps.</summary>
	private static string Unescape(string raw, char escape, ReferenceOptions o)
	{
		var chars = new List<(char C, bool Escaped)>();
		for (var i = 0; i < raw.Length; i++)
		{
			if (raw[i] == escape && i + 1 < raw.Length) chars.Add((raw[++i], true));
			else chars.Add((raw[i], false));
		}

		bool Whitespace((char C, bool Escaped) x) => !x.Escaped && IsWhitespace(x.C, o);
		var a = 0;
		var b = chars.Count;
		if (o.Trim)
		{
			while (a < b && Whitespace(chars[a])) a++;
			while (b > a && Whitespace(chars[b - 1])) b--;
		}

		return new string(chars.Skip(a).Take(b - a).Select(x => x.C).ToArray());
	}
}

/// <summary>A simple fixed-width parser on strings: the reading rules of FixedWidth mode.</summary>
internal static class ReferenceFixedWidth
{
	public static List<string[]> Parse(string text, int[] widths, bool trim, bool skipEmptyLines, char? comment)
	{
		var records = new List<string[]>();
		var start = 0;
		while (start < text.Length)
		{
			var end = start;
			while (end < text.Length && text[end] != '\r' && text[end] != '\n') end++;
			var line = text.Substring(start, end - start);
			start = end < text.Length && text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n' ? end + 2 : end + 1;
			if (comment is char c && line.Length > 0 && line[0] == c) continue;
			if (skipEmptyLines && line.All(ch => trim && (ch == ' ' || ch == '\t'))) continue;
			var fields = new string[widths.Length];
			var position = 0;
			for (var k = 0; k < widths.Length; k++)
			{
				var from = Boundary(line, position);
				position += widths[k];
				var field = line.Substring(from, Boundary(line, position) - from);
				fields[k] = trim ? field.Trim(' ', '\t') : field;
			}

			records.Add(fields);
		}

		return records;
	}

	/// <summary>Character position <paramref name="chars"/> of the line, after a surrogate pair it falls inside.</summary>
	public static int Boundary(string line, int chars)
	{
		if (chars >= line.Length) return line.Length;

		return chars > 0 && char.IsHighSurrogate(line[chars - 1]) && char.IsLowSurrogate(line[chars]) ? chars + 1 : chars;
	}
}
