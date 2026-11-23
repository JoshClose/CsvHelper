using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper.Excel.Tests.Writing;
using Xunit.Abstractions;
using static CsvHelper.Excel.Tests.Security.WriterFuzz;

namespace CsvHelper.Excel.Tests.Security;

/// <summary>
/// Seeded fuzzing of CSV → <c>.xlsx</c> inference, as Phase 6's "Testing" describes: CSVs of random columns of fields near
/// each form (signs, separators and digit counts of numbers in a culture's form, exponents at Excel's edges, days that
/// don't exist, times past their ranges, fractions of a second, offsets past 14 hours, booleans in any case and their
/// look-alikes), clean or not, with null fields and null values, in several cultures (multi-byte signs and separators
/// among them), with or without a header, under each <see cref="ExcelWriterOptions.LongNumbers"/>,
/// <see cref="ExcelWriterOptions.DateTimeOffsets"/> and <see cref="ExcelWriterOptions.OutOfRangeDates"/>, and with
/// <see cref="CsvToExcelOptions.InferTypes"/> from 0 to past the records. An oracle of the design's rules, written with
/// regular expressions and .NET's parsing rather than the converter's bytes, gives each column's type from the records
/// that decide it, and each cell: typed only when its field matches its column's type, and then read back by ClosedXML as
/// the same value; otherwise a text cell of the field; or no cell for a null field; or the conversion's failure, naming the
/// first cell an option makes it throw for. Each workbook passes the Open XML SDK's validator.
/// <c>CSVHELPER_EXCEL_FUZZ_INFERENCE</c> sets the number of CSVs, and <c>CSVHELPER_EXCEL_FUZZ_SEED</c> the first seed.
/// </summary>
/// <param name="output">Shows what the run converted.</param>
public class InferenceFuzzTests(ITestOutputHelper output)
{
	/// <summary>The number of CSVs a run converts, unless <c>CSVHELPER_EXCEL_FUZZ_INFERENCE</c> says otherwise.</summary>
	private const int DefaultCsvs = 1000;

	/// <summary>The most significant digits Excel keeps of a number.</summary>
	private const int MaxDigits = 15;

	/// <summary>The decimal exponents of the smallest and largest numbers Excel takes typed in: 1E-307 and below 1E+308.</summary>
	private const int MinMagnitude = -307;

	private const int MaxMagnitude = 307;

	/// <summary>The longest number, in bytes of UTF-8, that is read as one: longer text is text.</summary>
	private const int MaxNumberBytes = 128;

	private const long TicksPerMillisecond = 10_000;

	/// <summary>Excel's first date, 1900-01-01, in milliseconds from 0001-01-01.</summary>
	private static readonly long FirstDate = new DateTime(1900, 1, 1).Ticks / TicksPerMillisecond;

	/// <summary>The last millisecond a date cell holds, 9999-12-31's.</summary>
	private static readonly long LastDate = DateTime.MaxValue.Ticks / TicksPerMillisecond;

	/// <summary>A boolean: <c>true</c> or <c>false</c>, in any ASCII case.</summary>
	private static readonly Regex Boolean = new(@"\A(?:[Tt][Rr][Uu][Ee]|[Ff][Aa][Ll][Ss][Ee])\z", RegexOptions.CultureInvariant);

	/// <summary>An invariant number's text, as .NET formats a double or a form holds it: its integer digits, its fraction's and its exponent.</summary>
	private static readonly Regex InvariantNumber = new(@"\A-?([0-9]+)(?:\.([0-9]+))?(?:[Ee]([+-]?[0-9]+))?\z", RegexOptions.CultureInvariant);

	/// <summary>
	/// A date, or a date and time with <c>T</c> or a space, minutes, seconds and a fraction of 1 to 7 digits if any, and an
	/// offset if any: the forms, before their ranges are checked.
	/// </summary>
	private static readonly Regex Moment = new(
		@"\A([0-9]{4})-([0-9]{2})-([0-9]{2})(?:[T ]([0-9]{2}):([0-9]{2})(?::([0-9]{2})(?:\.([0-9]{1,7}))?)?(?:(Z)|([+-])([0-9]{2}):([0-9]{2}))?)?\z",
		RegexOptions.CultureInvariant);

	/// <summary>Text fields: words, separators, quotes, line breaks, letters past ASCII, escapes, formulas and null values.</summary>
	private static readonly string[] Words =
	[
		"apple", "x y", "a, b", "a; b", "say \"hi\"", "two\nlines", "tab\there", "Äpfel", "日本語", "NULL", "n/a", "-", "=1+1", "_x0041_",
		" padded ", "007", "😀",
	];

	/// <summary>Look-alikes of booleans that aren't: cut, longer, other words, a long s, a full-width letter, a zero-width space.</summary>
	private static readonly string[] NotBooleans = ["tru", "truee", "t", "f", "yes", "1", "0", "falſe", "true ", "ｔrue", "TRUE​"];

	/// <summary>The exponents that put numbers at and past Excel's edges.</summary>
	private static readonly string[] EdgeExponents = ["307", "308", "-307", "-308", "-324", "400", "99999999999", "0"];

	/// <summary>A culture of signs and a separator of more than one byte each, and white space as its group separator.</summary>
	private static readonly CultureInfo MultiByte = MakeMultiByte();

	/// <summary>The cultures of the CSVs' numbers: the invariant one most often, and those that write numbers otherwise.</summary>
	private static readonly CultureInfo[] Cultures =
	[
		CultureInfo.InvariantCulture, CultureInfo.InvariantCulture, CultureInfo.InvariantCulture, new CultureInfo("de-DE"), new CultureInfo("fr-FR"),
		new CultureInfo("en-US"), new CultureInfo("sv-SE"), MultiByte,
	];

	/// <summary>What a column is near.</summary>
	private enum Family
	{
		Number,
		Date,
		DateTime,
		Offset,
		Bool,
		Text,
		Mixed,
	}

	/// <summary>A column's type, in the order the narrowest is chosen.</summary>
	private enum Kind
	{
		Number,
		Date,
		DateTime,
		Offset,
		Bool,
		Text,
	}

	[Fact]
	public async Task TypesOnlyTheFieldsThatMatchTheirColumns()
	{
		var csvs = Setting("CSVHELPER_EXCEL_FUZZ_INFERENCE", DefaultCsvs);
		var first = Setting("CSVHELPER_EXCEL_FUZZ_SEED", 1);
		var failures = new List<string>();
		var tally = new SortedDictionary<string, int>(StringComparer.Ordinal);
		for (var seed = first; seed < first + csvs && failures.Count < MaxFailures; seed++)
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

		output.WriteLine($"Seeds {first} to {first + csvs - 1}:");
		foreach (var pair in tally)
		{
			output.WriteLine($"  {pair.Key}: {pair.Value}");
		}

		AssertNone(failures, "CSVHELPER_EXCEL_FUZZ_INFERENCE");
	}

	/// <summary>
	/// Converts an input's CSV and checks the workbook against the oracle: the failure it expects, or the validator and each
	/// cell as ClosedXML reads it.
	/// </summary>
	/// <param name="input">The input.</param>
	/// <param name="tally">Takes what was converted.</param>
	/// <returns>A task whose result is what went wrong, or <see langword="null"/>.</returns>
	private static async Task<string?> CheckAsync(Input input, SortedDictionary<string, int> tally)
	{
		var oracle = new Oracle(input.Culture, input.Options.Excel, input.Options.Csv.NullValues);
		var (cells, failure, kinds) = oracle.Expect(input);
		using var csv = new MemoryStream(Encoding.UTF8.GetBytes(input.Csv()));
		using var workbook = new MemoryStream();
		try
		{
			if (input.Async)
			{
				await ExcelConvert.ToExcelAsync(csv, workbook, input.Options);
			}
			else
			{
				ExcelConvert.ToExcel(csv, workbook, input.Options);
			}
		}
		catch (InvalidOperationException e) when (failure is not null)
		{
			Count(tally, "conversions that failed as an option says: " + failure.Value.Option);

			return e.Message.StartsWith($"Cell Sheet1!{failure.Value.Cell} holds ", StringComparison.Ordinal) && e.Message.EndsWith($"(ExcelWriterOptions.{failure.Value.Option} is Throw).", StringComparison.Ordinal)
				? null
				: $"expected a failure at {failure.Value.Cell} for {failure.Value.Option}, and the conversion threw: {e.Message}";
		}

		if (failure is not null)
		{
			return $"expected a failure at {failure.Value.Cell} for {failure.Value.Option}, and the conversion succeeded";
		}

		var bytes = Written.Checked(workbook.ToArray());
		var errors = Written.ValidationErrors(bytes);
		if (errors.Count > 0)
		{
			return "the validator says: " + string.Join("; ", errors.Take(3));
		}

		// ClosedXML reads a line feed in a cell's text as CR LF, which the cell doesn't hold.
		var read = Written.ClosedXmlValues(bytes).Select(v => v.Replace("\r\n", "\n")).ToList();
		for (var i = 0; i < Math.Max(cells.Count, read.Count); i++)
		{
			var (expected, actual) = (i < cells.Count ? cells[i] : "nothing", i < read.Count ? read[i] : "nothing");
			if (expected != actual)
			{
				return $"expected {Show(expected)}, ClosedXML read {Show(actual)}; the columns: {string.Join(", ", kinds)}";
			}
		}

		Count(tally, "workbooks");
		Count(tally, "culture " + input.CultureName);
		foreach (var kind in kinds.Skip(1))
		{
			Count(tally, "columns of " + kind);
		}

		foreach (var cell in cells.Select(c => c.Substring(c.IndexOf('=') + 1)))
		{
			Count(tally, "cells " + cell.Substring(0, cell.IndexOf(':')));
		}

		return null;
	}

	/// <summary>A culture of signs and a separator of more than one byte each: − and ⁺, and the Arabic decimal separator.</summary>
	/// <returns>The culture.</returns>
	private static CultureInfo MakeMultiByte()
	{
		var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
		culture.NumberFormat.NegativeSign = "−";
		culture.NumberFormat.PositiveSign = "⁺";
		culture.NumberFormat.NumberDecimalSeparator = "٫";
		culture.NumberFormat.NumberGroupSeparator = " ";

		return culture;
	}

	/// <summary>Random digits, the first of which isn't 0.</summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <param name="count">How many, at least 1.</param>
	/// <returns>The digits.</returns>
	private static string Digits(Random random, int count)
	{
		var digits = new StringBuilder().Append((char)('1' + random.Next(9)));
		for (var i = 1; i < count; i++)
		{
			digits.Append((char)('0' + random.Next(10)));
		}

		return digits.ToString();
	}

	/// <summary>Any digits, 0 first too.</summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <param name="count">How many.</param>
	/// <returns>The digits.</returns>
	private static string AnyDigits(Random random, int count) => string.Concat(Enumerable.Range(0, count).Select(_ => (char)('0' + random.Next(10))));

	/// <summary>Two digits of a number, or now and then one.</summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <param name="value">The number.</param>
	/// <param name="clean">Whether to write two digits always.</param>
	/// <returns>The digits.</returns>
	private static string Two(Random random, int value, bool clean) =>
		value.ToString(!clean && value < 10 && random.Next(8) == 0 ? "0" : "00", CultureInfo.InvariantCulture);

	/// <summary>
	/// A number in a culture's form: a sign or none, digits with no 0 first, the culture's separator and a fraction, and an
	/// exponent; at most 15 digits when clean. Not clean, one change near the form: a sign the grammar doesn't take, a 0
	/// first, a point or a comma alone, the other separator, grouping, more digits, an exponent at or past Excel's edges, a
	/// zero's exponent, characters around it, or a word.
	/// </summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <param name="format">The culture's numbers.</param>
	/// <param name="clean">Whether to keep to the form.</param>
	/// <returns>The field.</returns>
	private static string NumberField(Random random, NumberFormatInfo format, bool clean)
	{
		var sign = random.Next(3) == 0 ? format.NegativeSign : "";
		var integer = random.Next(4) == 0 ? "0" : Digits(random, random.Next(1, 9));
		var fraction = random.Next(2) == 0 ? "" : format.NumberDecimalSeparator + AnyDigits(random, random.Next(1, 8));
		var exponent = random.Next(5) == 0 ? (random.Next(2) == 0 ? "e" : "E") + new[] { "", format.NegativeSign, format.PositiveSign }[random.Next(3)] + random.Next(100).ToString(CultureInfo.InvariantCulture) : "";
		if (clean)
		{
			return sign + integer + fraction + exponent;
		}

		if (random.Next(3) == 0)
		{
			return NumberEdge(random, format);
		}

		switch (random.Next(14))
		{
			case 0:
				sign = new[] { "+", "-", "−", "--", format.PositiveSign, " " }[random.Next(6)];
				break;
			case 1:
				integer = "0" + integer;
				break;
			case 2:
				fraction = new[] { ".", ",", format.NumberDecimalSeparator }[random.Next(3)];
				break;
			case 3:
				integer = "";
				fraction = format.NumberDecimalSeparator + AnyDigits(random, random.Next(1, 5));
				break;
			case 4:
				fraction = (format.NumberDecimalSeparator == "." ? "," : ".") + AnyDigits(random, random.Next(1, 4));
				break;
			case 5:
				integer = Digits(random, random.Next(1, 4)) + format.NumberGroupSeparator + AnyDigits(random, 3);
				break;
			case 6:
				integer = Digits(random, random.Next(14, 23));
				break;
			case 7:
				integer = random.Next(2) == 0 ? "1" : Digits(random, 15);
				fraction = "";
				exponent = "E" + EdgeExponents[random.Next(EdgeExponents.Length)].Replace("-", format.NegativeSign);
				break;
			case 8:
				integer = "0";
				fraction = random.Next(2) == 0 ? "" : format.NumberDecimalSeparator + new string('0', random.Next(1, 4));
				exponent = "E" + EdgeExponents[random.Next(EdgeExponents.Length)].Replace("-", format.NegativeSign);
				break;
			case 9:
				exponent = new[] { "e", "E+", "E" + format.NegativeSign, "E1.5" }[random.Next(4)];
				break;
			case 10:
				var around = new[] { " ", "%", "$", " " }[random.Next(4)];

				return random.Next(2) == 0 ? around + sign + integer + fraction : sign + integer + fraction + around;
			case 11:
				return new[] { "NaN", "Infinity", "-Infinity", "0x1F", "１", "٣", "1_000" }[random.Next(7)];
			case 12:
				integer = new string('0', random.Next(2, 4));
				break;
			default:
				fraction = format.NumberDecimalSeparator + AnyDigits(random, random.Next(8, 16));
				break;
		}

		return sign + integer + fraction + exponent;
	}

	/// <summary>
	/// A number at an edge of what inference types, in the culture's form, on either side: 15 or 16 significant digits, a
	/// point among them or not, zeros around them; a magnitude of 307 or 308, positive or negative; or a double's text, as
	/// .NET writes it (<c>"R"</c>), or with its last digit changed, which is mostly not its double's own.
	/// </summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <param name="format">The culture's numbers.</param>
	/// <returns>The field.</returns>
	private static string NumberEdge(Random random, NumberFormatInfo format)
	{
		var sign = random.Next(3) == 0 ? format.NegativeSign : "";
		var point = format.NumberDecimalSeparator;
		if (random.Next(3) == 0)
		{
			var text = (random.NextDouble() * Math.Pow(10, random.Next(-12, 18))).ToString("R", CultureInfo.InvariantCulture);
			var end = text.IndexOfAny(['E', 'e']) is var e and >= 0 ? e : text.Length;
			if (random.Next(2) == 0 && char.IsDigit(text[end - 1]))
			{
				var digit = (char)('0' + ((text[end - 1] - '0' + random.Next(1, 10)) % 10));
				text = text.Substring(0, end - 1) + digit + text.Substring(end);
			}

			return sign + text.Replace(".", point).Replace("E-", "E" + format.NegativeSign).Replace("E+", "E" + format.PositiveSign);
		}

		if (random.Next(2) == 0)
		{
			var digits = Digits(random, random.Next(15, 17));
			var at = random.Next(1, digits.Length + 1);
			var number = at == digits.Length ? digits : digits.Substring(0, at) + point + digits.Substring(at);

			return sign + (random.Next(4) == 0 ? "0" + point + "000" + digits : number + (random.Next(4) == 0 && at < digits.Length ? "000" : ""));
		}

		var mantissa = new[] { "1", "12", "9" + point + "99999999999999", "0" + point + "1", "0" + point + "01", "1" + point + "5" }[random.Next(6)];
		var exponent = new[] { "306", "307", "308", "-306", "-307", "-308" }[random.Next(6)].Replace("-", format.NegativeSign);

		return sign + mantissa + "E" + exponent;
	}

	/// <summary>
	/// A date in <c>yyyy-MM-dd</c>: from 1800 to 9999 when clean, now and then before 1900. Not clean, one change near the
	/// form: a month or day out of range, February's 29th in any year, a year of 0 or of five digits, one digit, another
	/// separator, or a character after it.
	/// </summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <param name="clean">Whether to keep to the form.</param>
	/// <returns>The field.</returns>
	private static string DateField(Random random, bool clean)
	{
		var date = random.Next(8) == 0
			? new DateTime(1800, 1, 1).AddDays(random.Next(100 * 366))
			: new DateTime(1900, 1, 1).AddDays(random.Next((DateTime.MaxValue - new DateTime(1900, 1, 1)).Days + 1));
		if (random.Next(10) == 0)
		{
			date = new[] { new DateTime(1900, 1, 1), new DateTime(1900, 2, 28), new DateTime(1900, 3, 1), new DateTime(1899, 12, 31), new DateTime(9999, 12, 31), new DateTime(2024, 2, 29) }[random.Next(6)];
		}

		var (year, month, day) = (date.Year.ToString("0000", CultureInfo.InvariantCulture), date.Month, date.Day);
		var separator = "-";
		var after = "";
		if (!clean)
		{
			switch (random.Next(8))
			{
				case 0:
					month = new[] { 0, 13, 99 }[random.Next(3)];
					break;
				case 1:
					day = new[] { 0, 30, 31, 32 }[random.Next(4)];
					month = random.Next(1, 13);
					break;
				case 2:
					(month, day) = (2, 29);
					year = new[] { "1900", "2023", "2100", "2000", "2024" }[random.Next(5)];
					break;
				case 3:
					year = new[] { "0000", "10000", "999", "0001" }[random.Next(4)];
					break;
				case 4:
					separator = new[] { "/", ".", "_", "--" }[random.Next(4)];
					break;
				case 5:
					after = new[] { " ", "T", "Z", "t" }[random.Next(4)];
					break;
				default:
					return year + "-" + month.ToString(CultureInfo.InvariantCulture) + "-" + day.ToString(CultureInfo.InvariantCulture);
			}
		}

		return year + separator + Two(random, month, clean) + separator + Two(random, day, clean) + after;
	}

	/// <summary>
	/// A date and time: a date, <c>T</c> or a space, <c>HH:mm</c>, then seconds and a fraction of 1 to 7 digits or not; a
	/// date alone now and then. Not clean, one change near the form: an hour, minute or second past its range, a fraction of
	/// 8 digits or none, another separator, a time of one part, or a comma before the fraction.
	/// </summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <param name="clean">Whether to keep to the form.</param>
	/// <returns>The field.</returns>
	private static string DateTimeField(Random random, bool clean)
	{
		var date = DateField(random, clean || random.Next(3) > 0);
		if (random.Next(6) == 0)
		{
			return date;
		}

		var (hour, minute, second) = (random.Next(24), random.Next(60), random.Next(60));
		if (random.Next(8) == 0)
		{
			(hour, minute, second) = (23, 59, 59);
		}

		var separator = random.Next(2) == 0 ? "T" : " ";
		var time = Two(random, hour, clean) + ":" + Two(random, minute, clean);
		var seconds = random.Next(3) > 0;
		var fraction = seconds && random.Next(2) == 0 ? "." + AnyDigits(random, random.Next(1, 8)) : "";
		if (random.Next(10) == 0)
		{
			fraction = "." + new string('9', 7);
		}

		if (!clean)
		{
			switch (random.Next(8))
			{
				case 0:
					time = new[] { "24:00", "10:60", "99:00", "1030", "10" }[random.Next(5)];
					break;
				case 1:
					(seconds, fraction) = (true, ":60");
					break;
				case 2:
					(seconds, fraction) = (true, "." + AnyDigits(random, 8));
					break;
				case 3:
					(seconds, fraction) = (true, ".");
					break;
				case 4:
					separator = new[] { "t", "  ", "_", "" }[random.Next(4)];
					break;
				case 5:
					(seconds, fraction) = (true, "," + AnyDigits(random, 3));
					break;
				default:
					(seconds, fraction) = (false, ":" + second.ToString(CultureInfo.InvariantCulture));
					break;
			}
		}

		return date + separator + time + (seconds && !fraction.StartsWith(":", StringComparison.Ordinal) ? ":" + Two(random, second, true) : "") + fraction;
	}

	/// <summary>
	/// A date and time with an offset: <c>Z</c> or a sign, hours and minutes of at most 14 hours. Not clean, one change
	/// near the form: past 14 hours, a minute past its range, a lowercase <c>z</c>, no colon, hours alone, or none.
	/// </summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <param name="clean">Whether to keep to the form.</param>
	/// <returns>The field.</returns>
	private static string OffsetField(Random random, bool clean)
	{
		if (random.Next(6) == 0)
		{
			// A clock time on one side of 1900 and its UTC time on the other.
			var separator = random.Next(2) == 0 ? "T" : " ";
			var minutes = Two(random, random.Next(60), true);
			var ahead = random.Next(1, 15);

			return random.Next(2) == 0
				? $"1900-01-01{separator}0{random.Next(Math.Min(ahead, 2))}:{minutes}+{Two(random, ahead, true)}:00"
				: $"1899-12-31{separator}{Two(random, 24 - random.Next(1, ahead + 1), true)}:{minutes}-{Two(random, ahead, true)}:00";
		}

		var moment = DateTimeField(random, clean || random.Next(3) > 0);
		var hours = random.Next(15);
		var offset = random.Next(5) == 0 ? "Z" : (random.Next(2) == 0 ? "+" : "-") + Two(random, hours, true) + ":" + (hours == 14 ? "00" : new[] { "00", "30", "45", "59" }[random.Next(4)]);
		if (!clean)
		{
			offset = new[] { "+14:01", "-15:00", "+05:60", "z", "+0530", "+05", "", "+99:99", " +01:00" }[random.Next(9)];
		}

		return moment + offset;
	}

	/// <summary>A boolean in a random case of each letter; not clean, one of its look-alikes.</summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <param name="clean">Whether to keep to the form.</param>
	/// <returns>The field.</returns>
	private static string BoolField(Random random, bool clean) =>
		clean ? string.Concat((random.Next(2) == 0 ? "true" : "false").Select(c => random.Next(2) == 0 ? char.ToUpperInvariant(c) : c)) : NotBooleans[random.Next(NotBooleans.Length)];

	/// <summary>A field of a family, clean or not.</summary>
	/// <param name="random">The seed's random numbers.</param>
	/// <param name="family">The family; a mixed column's fields are of any.</param>
	/// <param name="format">The culture's numbers.</param>
	/// <param name="clean">Whether to keep to the form.</param>
	/// <returns>The field.</returns>
	private static string Field(Random random, Family family, NumberFormatInfo format, bool clean) => family switch
	{
		Family.Number => NumberField(random, format, clean),
		Family.Date => DateField(random, clean),
		Family.DateTime => DateTimeField(random, clean),
		Family.Offset => OffsetField(random, clean),
		Family.Bool => BoolField(random, clean),
		Family.Text => Words[random.Next(Words.Length)],
		_ => Field(random, (Family)random.Next((int)Family.Mixed), format, random.Next(2) == 0),
	};

	/// <summary>
	/// A text cell's text as ClosedXML reads it: the writer escapes an <c>_</c> that starts what reads as an escape
	/// (<c>_x0041_</c> as <c>_x005F_x0041_</c>), as Excel writes it, and ClosedXML leaves that escape as it is.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <returns>The text ClosedXML reads.</returns>
	private static string AsClosedXmlReads(string text) => Regex.Replace(text, "_(?=x[0-9A-Fa-f]{4}_)", "_x005F_");

	/// <summary>A cell's reference, by its column from 0 and its row from 1.</summary>
	/// <param name="column">The column.</param>
	/// <param name="row">The row.</param>
	/// <returns>The reference, such as <c>B3</c>.</returns>
	private static string Reference(int column, int row) => CellReference.ColumnName(column) + row.ToString(CultureInfo.InvariantCulture);

	/// <summary>
	/// The design's rules, apart from the converter: which types a field matches, each column's type, and each cell, as
	/// ClosedXML reads it.
	/// </summary>
	private sealed class Oracle
	{
		/// <summary>A number in the culture's form, before its digits and magnitude are counted.</summary>
		private readonly Regex number;

		private readonly string negative;
		private readonly ExcelWriterOptions options;
		private readonly IReadOnlyList<string>? nullValues;

		/// <summary>Creates the oracle of a CSV's culture and a workbook's options.</summary>
		/// <param name="culture">The CSV's culture.</param>
		/// <param name="options">The workbook's options.</param>
		/// <param name="nullValues">The CSV reader's null values.</param>
		public Oracle(CultureInfo culture, ExcelWriterOptions options, IReadOnlyList<string>? nullValues)
		{
			var format = culture.NumberFormat;
			negative = format.NegativeSign;
			this.options = options;
			this.nullValues = nullValues;
			var sign = Regex.Escape(negative);
			number = new Regex(
				$@"\A({sign})?(0|[1-9][0-9]*)(?:{Regex.Escape(format.NumberDecimalSeparator)}([0-9]+))?(?:[Ee]({sign}|{Regex.Escape(format.PositiveSign)})?([0-9]+))?\z",
				RegexOptions.CultureInvariant);
		}

		/// <summary>
		/// The cells an input's workbook should have, as <see cref="Written.ClosedXmlValues"/> gives them; or the failure an
		/// option makes, at the first cell it throws for; and each column's type.
		/// </summary>
		/// <param name="input">The input.</param>
		/// <returns>The cells, the failure or <see langword="null"/>, and the columns' types.</returns>
		public (List<string> Cells, (string Cell, string Option)? Failure, List<Kind> Kinds) Expect(Input input)
		{
			var records = input.Records;
			var held = Math.Min(input.Options.InferTypes, records.Count);
			var kinds = new List<Kind>();
			for (var column = 0; column < input.Header.Count; column++)
			{
				// The types every non-null field of the column among the held records matches.
				var fields = records.Take(held).Select(r => r[column]).Where(f => !IsNull(f)).ToList();
				var types = fields.Aggregate(fields.Count == 0 ? 0 : -1, (set, field) => set & Matches(field));
				kinds.Add(Enumerable.Range(0, (int)Kind.Text).Cast<Kind>().Where(k => (types & (1 << (int)k)) != 0).DefaultIfEmpty(Kind.Text).First());
			}

			var cells = new List<string>();
			var row = 1;
			if (input.Options.Csv.HasHeader)
			{
				cells.AddRange(input.Header.Select((name, i) => $"{Reference(i, row)}=Text:{name}"));
				row++;
			}

			foreach (var record in records)
			{
				for (var column = 0; column < record.Length; column++)
				{
					if (IsNull(record[column]))
					{
						continue;
					}

					var (value, option) = Cell(record[column], kinds[column]);
					if (option is not null)
					{
						return (cells, (Reference(column, row), option), kinds);
					}

					cells.Add($"{Reference(column, row)}={value}");
				}

				row++;
			}

			return (cells, null, kinds);
		}

		/// <summary>Whether a field is null: empty, or one of the null values, exactly.</summary>
		/// <param name="field">The field.</param>
		/// <returns>Whether it is.</returns>
		private bool IsNull(string field) => field.Length == 0 || (nullValues?.Contains(field) ?? false);

		/// <summary>
		/// The types a field matches, as bits of <see cref="Kind"/>: a number of the culture's form, in Excel's range, of at
		/// most 15 significant digits unless it is a double's own text or long numbers aren't text; a date of a day that
		/// exists, which a date and time takes too; a date and time; one with an offset, unless offsets are text; a boolean.
		/// </summary>
		/// <param name="field">The field.</param>
		/// <returns>The bits.</returns>
		private int Matches(string field)
		{
			if (Boolean.IsMatch(field))
			{
				return 1 << (int)Kind.Bool;
			}

			if (TryNumber(field, out var form, out var digits, out var isDouble) && (digits <= MaxDigits || isDouble || options.LongNumbers != ExcelLongNumbers.Text))
			{
				return 1 << (int)Kind.Number;
			}

			if (!TryMoment(field, out _, out _, out var dateAlone, out var offset))
			{
				return 0;
			}

			return dateAlone ? (1 << (int)Kind.Date) | (1 << (int)Kind.DateTime)
				: !offset ? 1 << (int)Kind.DateTime
				: options.DateTimeOffsets != ExcelDateTimeOffsets.Text ? 1 << (int)Kind.Offset
				: 0;
		}

		/// <summary>
		/// A cell of a field in a column of a type: the typed value, when the field matches the type; a text cell of the field
		/// otherwise, and for a date before 1900 when such dates are text; or the option that throws for it.
		/// </summary>
		/// <param name="field">The field, not null.</param>
		/// <param name="kind">The column's type.</param>
		/// <returns>The cell as ClosedXML gives it, or the option that throws.</returns>
		private (string? Value, string? Option) Cell(string field, Kind kind)
		{
			var text = ("Text:" + AsClosedXmlReads(field), (string?)null);
			switch (kind)
			{
				case Kind.Number when TryNumber(field, out var form, out var digits, out var isDouble) && (digits <= MaxDigits || isDouble || options.LongNumbers != ExcelLongNumbers.Text):
					// A double's own text is a double's cell, which long numbers don't apply to.
					return digits > MaxDigits && !isDouble && options.LongNumbers == ExcelLongNumbers.Throw
						? (null, nameof(ExcelWriterOptions.LongNumbers))
						: ("Number:" + double.Parse(form, NumberStyles.Float, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture), null);
				case Kind.Bool when Boolean.IsMatch(field):
					return (field.Length == 4 ? "Boolean:TRUE" : "Boolean:FALSE", null);
				case Kind.Date or Kind.DateTime or Kind.Offset when TryMoment(field, out var ticks, out var offset, out var dateAlone, out var hasOffset):
					if (kind == Kind.Date ? !dateAlone : kind == Kind.DateTime ? hasOffset : !hasOffset)
					{
						return text;
					}

					return Date(field, kind == Kind.Offset && options.DateTimeOffsets == ExcelDateTimeOffsets.Utc ? ticks - offset : ticks);
				default:
					return text;
			}
		}

		/// <summary>
		/// A date cell: its ticks rounded to the millisecond, half up, and 9999-12-31's last at most; a text cell of its field
		/// before 1900, or the option that throws for it.
		/// </summary>
		/// <param name="field">The field.</param>
		/// <param name="ticks">Its date and time's ticks, in UTC when offsets are.</param>
		/// <returns>The cell as ClosedXML gives it, or the option that throws.</returns>
		private (string? Value, string? Option) Date(string field, long ticks)
		{
			var milliseconds = Math.Min((ticks / TicksPerMillisecond) + (ticks % TicksPerMillisecond >= TicksPerMillisecond / 2 ? 1 : 0), LastDate);
			if (milliseconds < FirstDate)
			{
				return options.OutOfRangeDates == ExcelOutOfRangeDates.Throw ? (null, nameof(ExcelWriterOptions.OutOfRangeDates)) : ("Text:" + AsClosedXmlReads(field), null);
			}

			return ("DateTime:" + new DateTime(milliseconds * TicksPerMillisecond).ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture), null);
		}

		/// <summary>
		/// Reads a number of the culture's form: its invariant text, which the cell holds, and its significant digits, from
		/// the first digit that isn't 0 to the last. A number that isn't 0 must be from 1E-307 to below 1E+308.
		/// </summary>
		/// <param name="field">The field.</param>
		/// <param name="form">The invariant text.</param>
		/// <param name="digits">The significant digits.</param>
		/// <param name="isDouble">
		/// Whether it has more than 15 significant digits, a fraction or an exponent, and is its double's own text
		/// (<see cref="IsDoubleText"/>).
		/// </param>
		/// <returns>Whether the field is such a number.</returns>
		private bool TryNumber(string field, out string form, out int digits, out bool isDouble)
		{
			form = "";
			digits = 0;
			isDouble = false;
			var match = number.Match(field);
			if (!match.Success || Encoding.UTF8.GetByteCount(field) > MaxNumberBytes)
			{
				return false;
			}

			var (integer, fraction) = (match.Groups[2].Value, match.Groups[3].Value);
			var mantissa = integer + fraction;
			var exponent = match.Groups[5].Success ? BigInteger.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture) : BigInteger.Zero;
			var negativeExponent = match.Groups[4].Value == negative && match.Groups[4].Success;
			form = (match.Groups[1].Success ? "-" : "") + integer + (match.Groups[3].Success ? "." + fraction : "") +
				(match.Groups[5].Success ? "E" + (match.Groups[4].Success ? (negativeExponent ? "-" : "+") : "") + match.Groups[5].Value : "");
			var firstDigit = mantissa.IndexOfAny(['1', '2', '3', '4', '5', '6', '7', '8', '9']);
			if (firstDigit < 0)
			{
				// Zero, of any exponent.
				return true;
			}

			digits = mantissa.LastIndexOfAny(['1', '2', '3', '4', '5', '6', '7', '8', '9']) - firstDigit + 1;
			var magnitude = integer.Length - 1 - firstDigit + (negativeExponent ? -exponent : exponent);
			if (magnitude < MinMagnitude || magnitude > MaxMagnitude)
			{
				return false;
			}

			isDouble = digits > MaxDigits && (match.Groups[3].Success || match.Groups[5].Success) && IsDoubleText(form);

			return true;
		}

		/// <summary>
		/// Whether a number's invariant text is its double's own: the significant digits and magnitude of the shortest text
		/// that reads back as the double nearest it. Worked out exactly, with <see cref="BigInteger"/>, apart from the
		/// converter's parser and formatter and from the runtime's, which .NET Framework rounds to 15 digits.
		/// </summary>
		/// <param name="form">The invariant text, of a number in Excel's range that isn't 0.</param>
		/// <returns>Whether it is.</returns>
		private static bool IsDoubleText(string form)
		{
			var (digits, magnitude) = Significant(form);
			if (digits.Length > 17)
			{
				return false;
			}

			var value = NearestDouble(BigInteger.Parse(digits, CultureInfo.InvariantCulture), (int)magnitude - (digits.Length - 1));
			for (var precision = 1; precision <= 17; precision++)
			{
				var (rounded, scale) = RoundToDigits(value, precision);
				if (NearestDouble(rounded, scale) == value)
				{
					var shortest = rounded.ToString(CultureInfo.InvariantCulture).TrimEnd('0');

					return shortest == digits && scale + (rounded.ToString(CultureInfo.InvariantCulture).Length - 1) == magnitude;
				}
			}

			return false;
		}

		/// <summary>
		/// The double nearest a positive number, rounding half to even, as its mantissa (2^52 to below 2^53) and binary
		/// exponent: a normal double, as every number in Excel's range is.
		/// </summary>
		/// <param name="digits">The number's digits, an integer.</param>
		/// <param name="scale">The power of ten they are multiplied by.</param>
		/// <returns>The mantissa and the exponent, the double being their product.</returns>
		private static (BigInteger Mantissa, int Exponent) NearestDouble(BigInteger digits, int scale)
		{
			var (numerator, denominator) = scale >= 0 ? (digits * BigInteger.Pow(10, scale), BigInteger.One) : (digits, BigInteger.Pow(10, -scale));
			var exponent = (int)Math.Floor(BigInteger.Log(numerator, 2) - BigInteger.Log(denominator, 2)) - 52;
			while (true)
			{
				var (top, bottom) = exponent >= 0 ? (numerator, denominator << exponent) : (numerator << -exponent, denominator);
				var mantissa = BigInteger.DivRem(top, bottom, out var remainder);
				if (mantissa < BigInteger.One << 52)
				{
					exponent--;

					continue;
				}

				if (mantissa >= BigInteger.One << 53)
				{
					exponent++;

					continue;
				}

				var twice = remainder * 2;
				if (twice > bottom || (twice == bottom && !mantissa.IsEven))
				{
					mantissa++;
				}

				return mantissa == BigInteger.One << 53 ? (BigInteger.One << 52, exponent + 1) : (mantissa, exponent);
			}
		}

		/// <summary>A double rounded to a number of significant digits, half to even, exactly.</summary>
		/// <param name="value">The double's mantissa and exponent.</param>
		/// <param name="precision">The digits.</param>
		/// <returns>The rounded digits, an integer, and the power of ten they are multiplied by.</returns>
		private static (BigInteger Digits, int Scale) RoundToDigits((BigInteger Mantissa, int Exponent) value, int precision)
		{
			var (numerator, denominator) = value.Exponent >= 0 ? (value.Mantissa << value.Exponent, BigInteger.One) : (value.Mantissa, BigInteger.One << -value.Exponent);
			var magnitude = (int)Math.Floor(BigInteger.Log10(numerator) - BigInteger.Log10(denominator));
			while (Below(numerator, denominator, magnitude))
			{
				magnitude--;
			}

			while (!Below(numerator, denominator, magnitude + 1))
			{
				magnitude++;
			}

			var scale = magnitude - precision + 1;
			var (top, bottom) = scale >= 0 ? (numerator, denominator * BigInteger.Pow(10, scale)) : (numerator * BigInteger.Pow(10, -scale), denominator);
			var digits = BigInteger.DivRem(top, bottom, out var remainder);
			var twice = remainder * 2;
			if (twice > bottom || (twice == bottom && !digits.IsEven))
			{
				digits++;
			}

			return digits == BigInteger.Pow(10, precision) ? (BigInteger.Pow(10, precision - 1), scale + 1) : (digits, scale);
		}

		/// <summary>Whether a fraction is below a power of ten.</summary>
		/// <param name="numerator">The fraction's numerator.</param>
		/// <param name="denominator">Its denominator.</param>
		/// <param name="power">The power of ten.</param>
		/// <returns>Whether the fraction is below 10 to the power.</returns>
		private static bool Below(BigInteger numerator, BigInteger denominator, int power) =>
			power >= 0 ? numerator < denominator * BigInteger.Pow(10, power) : numerator * BigInteger.Pow(10, -power) < denominator;

		/// <summary>The significant digits of an invariant number's text, and the decimal exponent of the first.</summary>
		/// <param name="text">The text, of a number that isn't 0.</param>
		/// <returns>The digits, from the first that isn't 0 to the last, and the exponent.</returns>
		private static (string Digits, BigInteger Magnitude) Significant(string text)
		{
			var match = InvariantNumber.Match(text);
			var integer = match.Groups[1].Value;
			var mantissa = integer + match.Groups[2].Value;
			var first = mantissa.IndexOfAny(['1', '2', '3', '4', '5', '6', '7', '8', '9']);
			var last = mantissa.LastIndexOfAny(['1', '2', '3', '4', '5', '6', '7', '8', '9']);
			var exponent = match.Groups[3].Success ? BigInteger.Parse(match.Groups[3].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) : BigInteger.Zero;

			return (mantissa.Substring(first, last - first + 1), integer.Length - 1 - first + exponent);
		}

		/// <summary>
		/// Reads a date, or a date and time with an offset or not, of a day that exists, an hour to 23, a minute and a second
		/// to 59, and an offset of at most 14 hours.
		/// </summary>
		/// <param name="field">The field.</param>
		/// <param name="ticks">Its clock time's ticks.</param>
		/// <param name="offset">Its offset's ticks.</param>
		/// <param name="dateAlone">Whether it is a date alone.</param>
		/// <param name="hasOffset">Whether it has an offset.</param>
		/// <returns>Whether the field is one.</returns>
		private static bool TryMoment(string field, out long ticks, out long offset, out bool dateAlone, out bool hasOffset)
		{
			(ticks, offset, dateAlone, hasOffset) = (0, 0, false, false);
			var match = Moment.Match(field);
			if (!match.Success || !DateTime.TryParseExact(field.Substring(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
			{
				return false;
			}

			dateAlone = !match.Groups[4].Success;
			if (dateAlone)
			{
				ticks = date.Ticks;

				return true;
			}

			var (hour, minute) = (int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture));
			var second = match.Groups[6].Success ? int.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture) : 0;
			if (hour > 23 || minute > 59 || second > 59)
			{
				return false;
			}

			var fraction = match.Groups[7].Success ? long.Parse(match.Groups[7].Value.PadRight(7, '0'), CultureInfo.InvariantCulture) : 0;
			ticks = date.Ticks + new TimeSpan(hour, minute, second).Ticks + fraction;
			hasOffset = match.Groups[8].Success || match.Groups[9].Success;
			if (match.Groups[9].Success)
			{
				var (offsetHours, offsetMinutes) = (int.Parse(match.Groups[10].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[11].Value, CultureInfo.InvariantCulture));
				if (offsetMinutes > 59 || (offsetHours * 60) + offsetMinutes > 14 * 60)
				{
					return false;
				}

				offset = (match.Groups[9].Value == "-" ? -1 : 1) * new TimeSpan(offsetHours, offsetMinutes, 0).Ticks;
			}

			return true;
		}
	}

	/// <summary>
	/// An input: a CSV of an id column and random columns, in a culture, with or without a header, null values or not, and
	/// the options that change what inference types, converted synchronously or not.
	/// </summary>
	private sealed class Input
	{
		/// <summary>The seed.</summary>
		public int Seed { get; private init; }

		/// <summary>Whether to convert asynchronously.</summary>
		public bool Async { get; private init; }

		/// <summary>The CSV's culture.</summary>
		public CultureInfo Culture { get; private init; } = CultureInfo.InvariantCulture;

		/// <summary>The conversion's options.</summary>
		public CsvToExcelOptions Options { get; private init; } = CsvToExcelOptions.Default;

		/// <summary>The culture's name, for messages and the tally.</summary>
		public string CultureName => Culture.Name.Length > 0 ? Culture.Name : Culture == MultiByte ? "multi-byte" : "invariant";

		/// <summary>The columns' names, the id column's first.</summary>
		public List<string> Header { get; private init; } = [];

		/// <summary>What each column is near, and whether it keeps to its form, for messages.</summary>
		public List<string> Columns { get; private init; } = [];

		/// <summary>The records, each field's text, empty for an empty field.</summary>
		public List<string[]> Records { get; private init; } = [];

		/// <summary>The input of a seed.</summary>
		/// <param name="seed">The seed.</param>
		/// <returns>The input.</returns>
		public static Input Of(int seed)
		{
			var random = new Random(seed);
			var culture = Cultures[random.Next(Cultures.Length)];
			var format = culture.NumberFormat;
			var rows = random.Next(1, 31);
			var nullValues = random.Next(4) == 0 ? new[] { "NULL", "-" } : null;
			var columns = new List<string> { "id" };
			var fields = new List<string[]> { Enumerable.Range(1, rows).Select(r => "r" + r.ToString(CultureInfo.InvariantCulture)).ToArray() };
			for (var count = random.Next(1, 9); count > 0; count--)
			{
				// A clean column keeps to its form, but for nulls and, one in four, a field that doesn't; any other has fields
				// that keep to it or not, one in two.
				var family = (Family)random.Next((int)Family.Mixed + 1);
				var clean = random.Next(5) < 3;
				var odd = clean && random.Next(4) == 0 ? random.Next(rows) : -1;
				var column = new string[rows];
				for (var r = 0; r < rows; r++)
				{
					column[r] = random.Next(10) == 0 ? (nullValues is not null && random.Next(2) == 0 ? nullValues[random.Next(2)] : "")
						: Field(random, family, format, clean ? r != odd : random.Next(2) == 0);
				}

				columns.Add($"{family}{(clean ? odd >= 0 ? " but one" : " clean" : "")}");
				fields.Add(column);
			}

			var hasHeader = random.Next(5) > 0;
			var inferTypes = random.Next(6) switch { 0 => 0, 1 => 1, 2 => 2, 3 => random.Next(1, rows + 1), _ => 1000 };
			var options = new CsvToExcelOptions
			{
				Csv = new CsvReaderOptions
				{
					FormatProvider = culture,
					Separator = random.Next(4) == 0 ? '\t' : format.NumberDecimalSeparator == "," ? ';' : ',',
					HasHeader = hasHeader,
					NullValues = nullValues,
				},
				Excel = new ExcelWriterOptions
				{
					LongNumbers = (ExcelLongNumbers)random.Next(3),
					DateTimeOffsets = (ExcelDateTimeOffsets)random.Next(3),
					OutOfRangeDates = random.Next(4) == 0 ? ExcelOutOfRangeDates.Throw : ExcelOutOfRangeDates.Text,
				},
				InferTypes = inferTypes,
			};

			return new Input
			{
				Seed = seed,
				Async = seed % 2 == 1,
				Culture = culture,
				Options = options,
				Header = [.. columns.Select((_, i) => i == 0 ? "id" : "c" + i.ToString(CultureInfo.InvariantCulture))],
				Columns = columns,
				Records = [.. Enumerable.Range(0, rows).Select(r => fields.Select(c => c[r]).ToArray())],
			};
		}

		/// <summary>The CSV: the header, when the options have one, then the records, each field quoted when it needs to be.</summary>
		/// <returns>The CSV's text.</returns>
		public string Csv()
		{
			var separator = Options.Csv.Separator ?? ',';
			var csv = new StringBuilder();
			IEnumerable<string[]> lines = Options.Csv.HasHeader ? [[.. Header], .. Records] : Records;
			foreach (var line in lines)
			{
				csv.Append(string.Join(separator.ToString(), line.Select(f => f.IndexOfAny([separator, '"', '\r', '\n']) >= 0 ? "\"" + f.Replace("\"", "\"\"") + "\"" : f))).Append("\r\n");
			}

			return csv.ToString();
		}

		/// <inheritdoc/>
		public override string ToString() =>
			$"{(Async ? "async" : "sync")}, culture {CultureName}, " +
			$"separator {Show((Options.Csv.Separator ?? ',').ToString())}, header {Options.Csv.HasHeader}, null values {(Options.Csv.NullValues is null ? "none" : string.Join(" ", Options.Csv.NullValues))}, " +
			$"infer {Options.InferTypes}, long numbers {Options.Excel.LongNumbers}, offsets {Options.Excel.DateTimeOffsets}, dates before 1900 {Options.Excel.OutOfRangeDates}, " +
			$"{Records.Count} records, columns {string.Join(", ", Columns)}";
	}
}
