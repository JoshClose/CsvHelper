using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper.Excel.Tests.Writing;
using Xunit.Abstractions;
using static CsvHelper.Excel.Tests.Security.WriterFuzz;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// Seeded fuzzing of the writer's text cells, as Phase 4's "Security and fuzzing" describes: workbooks of random fields,
/// written as UTF-16 strings, UTF-16 spans or UTF-8 bytes, of control characters, CR and LF, U+FFFE and U+FFFF, lone and
/// reversed surrogates, invalid UTF-8 (lone continuation bytes, overlong forms, encoded surrogates, sequences past
/// U+10FFFF, cut sequences), the entities' characters, near escapes (<c>_x0041_</c> and its near misses), white space at
/// either end, and lengths around 32,767 code units, with inline or shared strings, synchronously or not. Each workbook
/// passes the Open XML SDK's validator and reads back through CsvHelper.Excel's reader as written, with U+FFFD for what
/// was invalid UTF-16 or UTF-8; a field of more than 32,767 code units throws <see cref="InvalidOperationException"/> and
/// leaves no cell. With shared strings, the part holds each distinct text once. <c>CSVHELPER_EXCEL_FUZZ_TEXTS</c> sets the
/// number of workbooks, and <c>CSVHELPER_EXCEL_FUZZ_SEED</c> the first seed.
/// </summary>
/// <param name="output">Shows what the run wrote.</param>
public class TextFuzzTests(ITestOutputHelper output)
{
	/// <summary>The number of workbooks a run writes, unless <c>CSVHELPER_EXCEL_FUZZ_TEXTS</c> says otherwise.</summary>
	private const int DefaultWorkbooks = 500;

	/// <summary>The most UTF-16 code units a cell's text has.</summary>
	private const int MaxCell = 32767;

	/// <summary>
	/// The pieces short fields are made of: text, numbers, white space and line breaks, the CSV writer's own specials,
	/// the entities' characters and markup, escapes and their near misses, control characters, the non-characters and
	/// the BOM, names and accents, and surrogates, paired, lone and reversed.
	/// </summary>
	private static readonly string[] Pieces =
	[
		"a", "Z", "0", "007", "1.5", "-2", "+", ".5", " ", "  ", "\t", "\n", "\r", "\r\n", "\n\r", ",", "\"", "'", "|", "=", "=A1",
		"&", "<", ">", "&amp;", "&#1;", "<t>", "</t>", "]]>", "<![CDATA[",
		"_", "x", "_x", "_x0041_", "_X0041_", "_x004_", "_x0041", "_x00410_", "_x005F_", "_x005f_", "__x0041_", "_x_", "_x00G1_",
		"_xD800_", "_xFFFF_", "_x000D_", "_x000d_", "_x0000_", "_x001F_",
		Units(0), Units(1), Units(8), Units(0xB), Units(0xC), Units(0x1F), Units(0x7F), Units(0x80), Units(0x85), Units(0x9F), Units(0xA0),
		Units(0xE9), Units(0x65, 0x301), Units(0x20AC), Units(0x65E5, 0x672C), Units(0x200B), Units(0xFEFF), Units(0x2028), Units(0xF8FF),
		Units(0xFFEE), Units(0xFFFD), Units(0xFFFE), Units(0xFFFF), Units(0xFDD0),
		Units(0xD83D, 0xDE00), Units(0xD83F, 0xDFFE), Units(0xDBFF, 0xDFFF), Units(0xD800, 0xDC00),
		Units(0xD800), Units(0xDBFF), Units(0xDC00), Units(0xDFFF), Units(0xDE00, 0xD83D),
	];

	/// <summary>
	/// Byte sequences a UTF-8 field has besides its pieces' encodings: invalid ones (a lone continuation byte, a lead byte
	/// alone or cut, overlong forms, encoded surrogates, past U+10FFFF, bytes UTF-8 never has) and the valid ones at the
	/// edges of the invalid (U+D7FF, U+E000, U+10FFFF).
	/// </summary>
	private static readonly byte[][] Sequences =
	[
		[0x80], [0xBF], [0xC0, 0x80], [0xC1, 0xBF], [0xC2], [0xC2, 0x41], [0xE0, 0x80, 0x80], [0xE0, 0x9F, 0xBF], [0xED, 0xA0, 0x80],
		[0xED, 0xBF, 0xBF], [0xEF, 0xBF], [0xEF], [0xE2, 0x82], [0xF0, 0x80, 0x80, 0x80], [0xF0, 0x8F, 0xBF, 0xBF], [0xF4, 0x90, 0x80, 0x80],
		[0xF5, 0x80, 0x80, 0x80], [0xF8, 0x88, 0x80, 0x80, 0x80], [0xFE], [0xFF], [0xF0, 0x9F, 0x98], [0xC0], [0xE0], [0xF0],
		[0xED, 0x9F, 0xBF], [0xEE, 0x80, 0x80], [0xF4, 0x8F, 0xBF, 0xBF],
	];

	/// <summary>
	/// The characters long fields repeat: of one to four UTF-8 bytes, a surrogate pair, and those the writer escapes,
	/// whose XML is several times their length.
	/// </summary>
	private static readonly string[] LongUnits = ["x", Units(0xE9), Units(0x20AC), Units(0xD83D, 0xDE00), "\r", "&", "_x0041_", "\t", " ", Units(0xFFFE), Units(1)];

	/// <summary>The lengths of long fields, in code units, around a cell's limit.</summary>
	private static readonly int[] LongLengths = [MaxCell - 2, MaxCell - 1, MaxCell, MaxCell + 1, MaxCell + 2];

	/// <summary>How a field is written.</summary>
	private enum FieldKind
	{
		/// <summary><see cref="CsvWriter.WriteField(string?)"/>.</summary>
		String,

		/// <summary><see cref="CsvWriter.WriteField(ReadOnlySpan{char})"/>.</summary>
		Span,

		/// <summary><see cref="CsvWriter.WriteField(ReadOnlySpan{byte})"/>.</summary>
		Utf8,

		/// <summary><see cref="CsvWriter.WriteField(string?)"/> with <see langword="null"/>, which is no cell.</summary>
		Null,
	}

	[Fact]
	public async Task WritesAndReadsBackRandomText()
	{
		var workbooks = Setting("CSVHELPER_EXCEL_FUZZ_TEXTS", DefaultWorkbooks);
		var first = Setting("CSVHELPER_EXCEL_FUZZ_SEED", 1);
		var failures = new List<string>();
		var tally = new SortedDictionary<string, int>(StringComparer.Ordinal);
		for (var seed = first; seed < first + workbooks && failures.Count < MaxFailures; seed++)
		{
			var input = Input.Of(seed);
			try
			{
				if (await CheckAsync(input, tally) is { } problem)
				{
					failures.Add($"Seed {seed} ({input}): {problem}");
				}
			}
			catch (Exception e)
			{
				failures.Add($"Seed {seed} ({input}): {Describe(e)}");
			}
		}

		output.WriteLine($"Seeds {first} to {first + workbooks - 1}:");
		foreach (var pair in tally)
		{
			output.WriteLine($"  {pair.Key}: {pair.Value}");
		}

		AssertNone(failures, "CSVHELPER_EXCEL_FUZZ_TEXTS");
	}

	/// <summary>
	/// Writes an input's workbook and checks it: the long fields refused, the validator, the text read back, and the
	/// shared strings.
	/// </summary>
	/// <param name="input">The input.</param>
	/// <param name="tally">Takes what was written.</param>
	/// <returns>A task whose result is what went wrong, or <see langword="null"/>.</returns>
	private static async Task<string?> CheckAsync(Input input, SortedDictionary<string, int> tally)
	{
		var problems = new List<string>();
		var workbook = await Written.WorkbookAsync(async (book, async) =>
		{
			var writer = book.AddSheet("Text");
			for (var r = 0; r < input.Rows.Count; r++)
			{
				for (var c = 0; c < input.Rows[r].Count; c++)
				{
					var field = input.Rows[r][c];
					try
					{
						field.WriteTo(writer);
						if (field.TooLong)
						{
							problems.Add($"row {r + 1}, column {c + 1}: {field} of {field.Expected.Length} code units was written");
						}
					}
					catch (InvalidOperationException e) when (field.TooLong)
					{
						var count = field.Expected.Length.ToString("N0", CultureInfo.InvariantCulture);
						if (!e.Message.EndsWith($" has {count} characters, and Excel opens no cell of more than 32,767.", StringComparison.Ordinal))
						{
							problems.Add($"row {r + 1}, column {c + 1}: {field} threw \"{e.Message}\"");
						}
					}
				}

				await Written.EndRecordAsync(writer, async);
			}

			await Written.DisposeAsync(writer, async);
		}, input.Async, Written.Ragged(input.Strings));
		if (problems.Count > 0)
		{
			return string.Join("; ", problems);
		}

		var errors = Written.ValidationErrors(workbook);
		if (errors.Count > 0)
		{
			return "the validator says: " + string.Join("; ", errors.Take(3));
		}

		var expected = Trim(input.Rows.Select(row => Trim(row.Select(f => f.TooLong ? "" : f.Expected).ToList())).ToList());
		var read = Trim(Read(workbook));
		for (var r = 0; r < Math.Max(expected.Count, read.Count); r++)
		{
			var wrote = r < expected.Count ? expected[r] : [];
			var got = r < read.Count ? read[r] : [];
			for (var c = 0; c < Math.Max(wrote.Count, got.Count); c++)
			{
				var want = c < wrote.Count ? wrote[c] : "";
				var have = c < got.Count ? got[c] : "";
				if (!string.Equals(want, have, StringComparison.Ordinal))
				{
					var origin = r < input.Rows.Count && c < input.Rows[r].Count ? input.Rows[r][c].ToString() : "no field";

					return $"row {r + 1}, column {c + 1}: wrote {origin}, expected {Show(want)}, read {Show(have)}";
				}
			}
		}

		if (input.Strings == ExcelStrings.Shared && SharedStringsProblem(workbook, input) is { } shared)
		{
			return shared;
		}

		Tally(input, tally);

		return null;
	}

	/// <summary>
	/// Checks the shared strings: as many references as text cells, and each distinct text once (its <c>uniqueCount</c>).
	/// </summary>
	/// <param name="workbook">The workbook.</param>
	/// <param name="input">Its input.</param>
	/// <returns>What is wrong, or <see langword="null"/>.</returns>
	private static string? SharedStringsProblem(byte[] workbook, Input input)
	{
		var cells = input.Rows.SelectMany(r => r).Where(f => !f.TooLong && f.Expected.Length > 0).Select(f => f.Expected).ToList();
		var part = Written.Entry(workbook, "xl/sharedStrings.xml");
		var sst = Regex.Match(part, "<sst [^>]*count=\"(\\d+)\" uniqueCount=\"(\\d+)\"");
		if (!sst.Success)
		{
			return "the shared strings have no counts: " + part.Substring(0, Math.Min(part.Length, 200));
		}

		var count = int.Parse(sst.Groups[1].Value, CultureInfo.InvariantCulture);
		var unique = int.Parse(sst.Groups[2].Value, CultureInfo.InvariantCulture);
		var distinct = cells.Distinct(StringComparer.Ordinal).Count();

		return count == cells.Count && unique == distinct
			? null
			: $"the shared strings count {count} references and {unique} strings, for {cells.Count} text cells of {distinct} distinct texts";
	}

	/// <summary>Reads every row of a workbook's one sheet with CsvHelper.Excel, each as its fields.</summary>
	/// <param name="workbook">The workbook.</param>
	/// <returns>The rows.</returns>
	private static List<List<string>> Read(byte[] workbook)
	{
		using var book = ExcelWorkbook.Open(new MemoryStream(workbook));
		using var reader = book.OpenReader(book.Sheets[0], Written.AllRows);
		var rows = new List<List<string>>();
		while (reader.Read())
		{
			rows.Add(Trim(Enumerable.Range(0, reader.FieldCount).Select(reader.GetString).ToList()));
		}

		return rows;
	}

	/// <summary>A row without the empty fields at its end.</summary>
	/// <param name="row">The row.</param>
	/// <returns>The row, which is the same list.</returns>
	private static List<string> Trim(List<string> row)
	{
		while (row.Count > 0 && row[row.Count - 1].Length == 0)
		{
			row.RemoveAt(row.Count - 1);
		}

		return row;
	}

	/// <summary>Rows without the empty rows at their end.</summary>
	/// <param name="rows">The rows, each trimmed.</param>
	/// <returns>The rows, which are the same list.</returns>
	private static List<List<string>> Trim(List<List<string>> rows)
	{
		while (rows.Count > 0 && rows[rows.Count - 1].Count == 0)
		{
			rows.RemoveAt(rows.Count - 1);
		}

		return rows;
	}

	/// <summary>Counts what an input wrote.</summary>
	/// <param name="input">The input.</param>
	/// <param name="tally">The counts.</param>
	private static void Tally(Input input, SortedDictionary<string, int> tally)
	{
		Count(tally, $"workbooks: {input.Strings}, {(input.Async ? "async" : "sync")}");
		foreach (var field in input.Rows.SelectMany(r => r))
		{
			Count(tally, $"fields: {field.Kind}");
			if (field.TooLong)
			{
				Count(tally, "fields: refused, past 32,767 code units");
			}
			else if (field.Expected.Length > MaxCell - 3)
			{
				Count(tally, "fields: written, within 3 code units of 32,767");
			}

			if (field.Replaced)
			{
				Count(tally, "fields: with invalid UTF-16 or UTF-8 replaced");
			}
		}
	}

	/// <summary>A random field: a few pieces, or a long run of one character near a cell's limit.</summary>
	/// <param name="kind">How it is written.</param>
	/// <param name="text">Its UTF-16 text, for <see cref="FieldKind.String"/> and <see cref="FieldKind.Span"/>.</param>
	/// <param name="utf8">Its bytes, for <see cref="FieldKind.Utf8"/>.</param>
	/// <param name="origin">How it was made, for a failure.</param>
	private sealed class Field(FieldKind kind, string? text, byte[]? utf8, string origin)
	{
		/// <summary>How it is written.</summary>
		public FieldKind Kind { get; } = kind;

		/// <summary>The text it reads back as: its text or bytes decoded with U+FFFD for what is invalid, or empty for null.</summary>
		public string Expected { get; } = text is not null ? Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(text)) : utf8 is not null ? Encoding.UTF8.GetString(utf8) : "";

		/// <summary>Whether its text is past a cell's limit, so that writing it throws.</summary>
		public bool TooLong => Expected.Length > MaxCell;

		/// <summary>Whether it has invalid UTF-16 or UTF-8, which reads back as U+FFFD.</summary>
		public bool Replaced => text is not null ? !string.Equals(text, Expected, StringComparison.Ordinal) : utf8 is not null && !Utf8Equals(utf8, Expected);

		/// <summary>Makes a random field.</summary>
		/// <param name="random">The input's random numbers.</param>
		/// <returns>The field.</returns>
		public static Field Of(Random random)
		{
			if (random.Next(10) == 0)
			{
				return new Field(FieldKind.Null, null, null, "null");
			}

			var kind = (FieldKind)random.Next(3);
			var length = random.Next(14) == 0 ? LongLengths[random.Next(LongLengths.Length)] : -1;
			var origin = new StringBuilder();
			var text = length < 0 ? ShortText(random, origin) : LongText(random, length, origin);
			if (kind != FieldKind.Utf8)
			{
				return new Field(kind, text, null, $"{kind} {origin}");
			}

			// Its pieces' bytes, with sequences of their own between them, valid or not.
			var bytes = new List<byte>(Encoding.UTF8.GetBytes(text));
			for (var n = random.Next(3) == 0 ? random.Next(1, 4) : 0; n > 0; n--)
			{
				var sequence = Sequences[random.Next(Sequences.Length)];
				var at = length < 0 ? random.Next(bytes.Count + 1) : random.Next(3) switch { 0 => 0, 1 => bytes.Count, _ => random.Next(bytes.Count + 1) };
				bytes.InsertRange(at, sequence);
				origin.Append($" + [{Show(sequence)}] at {at}");
			}

			return new Field(kind, null, [.. bytes], $"{kind} {origin}");
		}

		/// <summary>The field again, as shared strings meet it: the same text, written the same way or the other UTF-16 way.</summary>
		/// <param name="random">The input's random numbers.</param>
		/// <returns>The field.</returns>
		public Field Again(Random random) => Kind is FieldKind.String or FieldKind.Span
			? new Field(random.Next(2) == 0 ? FieldKind.String : FieldKind.Span, text, null, "again: " + origin)
			: new Field(Kind, text, utf8, "again: " + origin);

		/// <summary>Writes the field.</summary>
		/// <param name="writer">The sheet's writer.</param>
		public void WriteTo(CsvWriter writer)
		{
			switch (Kind)
			{
				case FieldKind.String:
					writer.WriteField(text);

					break;
				case FieldKind.Span:
					writer.WriteField(text.AsSpan());

					break;
				case FieldKind.Utf8:
					writer.WriteField(new ReadOnlySpan<byte>(utf8));

					break;
				default:
					writer.WriteField((string?)null);

					break;
			}
		}

		/// <summary>How it was made.</summary>
		/// <returns>The description.</returns>
		public override string ToString() => origin;

		/// <summary>A few random pieces.</summary>
		/// <param name="random">The input's random numbers.</param>
		/// <param name="origin">Takes the pieces.</param>
		/// <returns>The text.</returns>
		private static string ShortText(Random random, StringBuilder origin)
		{
			var text = new StringBuilder();
			for (var n = random.Next(1, 6); n > 0; n--)
			{
				var piece = Pieces[random.Next(Pieces.Length)];
				text.Append(piece);
			}

			origin.Append(Show(text.ToString()));

			return text.ToString();
		}

		/// <summary>
		/// A long run of one character to a length, filled with <c>y</c>, and now and then a surrogate pair at its end,
		/// before or across the limit.
		/// </summary>
		/// <param name="random">The input's random numbers.</param>
		/// <param name="length">The length, in code units.</param>
		/// <param name="origin">Takes how it was made.</param>
		/// <returns>The text.</returns>
		private static string LongText(Random random, int length, StringBuilder origin)
		{
			var unit = LongUnits[random.Next(LongUnits.Length)];
			var pair = random.Next(3) == 0;
			var fill = pair ? length - 2 : length;
			var text = new StringBuilder(length + unit.Length);
			while (text.Length + unit.Length <= fill)
			{
				text.Append(unit);
			}

			text.Append('y', fill - text.Length);
			if (pair)
			{
				text.Append(Units(0xD83D, 0xDE00));
			}

			origin.Append($"{Show(unit)} to {length.ToString("N0", CultureInfo.InvariantCulture)} code units{(pair ? ", the last two a surrogate pair" : "")}");

			return text.ToString();
		}

		/// <summary>Whether bytes are the UTF-8 of a text, so that nothing in them was replaced.</summary>
		/// <param name="utf8">The bytes.</param>
		/// <param name="text">The text.</param>
		/// <returns>Whether they are.</returns>
		private static bool Utf8Equals(byte[] utf8, string text) => utf8.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(text));
	}

	/// <summary>A workbook to write: its rows of fields, its strings and its mode.</summary>
	/// <param name="rows">The rows.</param>
	/// <param name="strings">Where text cells keep their strings.</param>
	/// <param name="async">Whether to write asynchronously.</param>
	private sealed class Input(List<List<Field>> rows, ExcelStrings strings, bool async)
	{
		/// <summary>The rows, some of them empty.</summary>
		public List<List<Field>> Rows { get; } = rows;

		/// <summary>Where text cells keep their strings.</summary>
		public ExcelStrings Strings { get; } = strings;

		/// <summary>Whether to write asynchronously.</summary>
		public bool Async { get; } = async;

		/// <summary>
		/// The input of a seed: one to six rows of one to six fields (a record has at least one, and one of null fields has
		/// no cell), now and then one met before.
		/// </summary>
		/// <param name="seed">The seed.</param>
		/// <returns>The input.</returns>
		public static Input Of(int seed)
		{
			var random = new Random(seed);
			var strings = random.Next(2) == 0 ? ExcelStrings.Inline : ExcelStrings.Shared;
			var async = random.Next(2) == 1;
			var rows = new List<List<Field>>();
			var all = new List<Field>();
			for (var r = random.Next(1, 7); r > 0; r--)
			{
				var row = new List<Field>();
				for (var f = random.Next(1, 7); f > 0; f--)
				{
					var field = all.Count > 0 && random.Next(6) == 0 ? all[random.Next(all.Count)].Again(random) : Field.Of(random);
					row.Add(field);
					all.Add(field);
				}

				rows.Add(row);
			}

			return new Input(rows, strings, async);
		}

		/// <summary>The input's settings, for a failure.</summary>
		/// <returns>The description.</returns>
		public override string ToString() => $"{Strings}, {(Async ? "async" : "sync")}, {Rows.Count} rows of {string.Join("/", Rows.Select(r => r.Count))} fields";
	}
}
