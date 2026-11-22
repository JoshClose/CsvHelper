using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using CsvHelper.Excel.Tests.Api;
using CsvHelper.Excel.Tests.Package;
using CsvHelper.Excel.Tests.Security;
using CsvHelper.Excel.Tests.Sheet;

namespace CsvHelper.Excel.Tests.Display;

/// <summary>
/// The formatter's tables (Phase 9's step 3, decision 7). What <see cref="CellFormatter.StyleOf"/> gives, for every kind and
/// every cell style, negative and past the cell formats too, is what it gave when it looked each style up and compiled each
/// code the first time a field asked (<see cref="Lookup"/>, a copy of it at <c>6a51d85d</c>), in styles of every shape and
/// in three cultures, up to <see cref="CellFormatter.MaxCompiledFormats"/> distinct formats and past them; so is the text
/// <see cref="CellFormatter.TryFormat"/> shows. <see cref="CellFormatter.ChangesText"/> is false exactly where no text or
/// boolean takes a style, and true past the cap; a fresh formatter gives many threads at once the same answers; and
/// <see cref="CellFormatter.StyleOf"/> allocates nothing, a fresh formatter's first answers among them.
/// </summary>
public class CellFormatterTablesTests
{
	/// <summary>The prefix of a number format id among <see cref="Styles"/>'s formats: <c>id:14</c>.</summary>
	private const string Id = "id:";

	/// <summary>Core's <c>CsvCellFormatter.MaxStyle</c>, which the tests can't name: the largest style a field can have.</summary>
	private const int MaxStyle = (1 << 23) - 1;

	/// <summary>The most mismatches a check lists.</summary>
	private const int MaxMismatches = 20;

	/// <summary>
	/// <see cref="CellFormatter.TryFormat"/> with a <see cref="SheetCellKind"/>: core's kind of the same value, which the
	/// tests can't name, bound once through reflection (<see cref="Bind{TKind}"/>).
	/// </summary>
	private static readonly Formatting TryFormat = (Formatting)typeof(CellFormatterTablesTests).GetMethod(nameof(Bind), BindingFlags.NonPublic | BindingFlags.Static)!
		.MakeGenericMethod(typeof(CsvReader).Assembly.GetType("CsvHelper.CsvCellKind", true)!).Invoke(null, null)!;

	/// <summary>The kinds a field has, and two values no kind has, which take no style.</summary>
	private static readonly SheetCellKind[] Kinds = [.. Enum.GetValues(typeof(SheetCellKind)).Cast<SheetCellKind>(), (SheetCellKind)7, (SheetCellKind)255];

	/// <summary>The cultures the formatters are made in, with a date system each: Excel en-US, and German and Japanese, whose built-in codes differ.</summary>
	private static readonly (DisplayCulture Culture, bool Date1904)[] Cultures =
	[
		(DisplayCulture.Invariant, false), (DisplayCulture.For(CultureInfo.GetCultureInfo("de-DE")), true), (DisplayCulture.For(CultureInfo.GetCultureInfo("ja-JP")), false),
	];

	/// <summary>The values each style shows, by kind, as the scanner stores them (a boolean as 1 or 0); and values that aren't what their kind says.</summary>
	private static readonly (SheetCellKind Kind, string Value)[] Values =
	[
		(SheetCellKind.Number, "1234.5"), (SheetCellKind.Number, "-0.25"), (SheetCellKind.Number, "0"), (SheetCellKind.DateNumber, "45306.5208333"),
		(SheetCellKind.DateNumber, "-1"), (SheetCellKind.Duration, "1.5"), (SheetCellKind.Bool, "1"), (SheetCellKind.Bool, "0"), (SheetCellKind.Text, "abc"),
		(SheetCellKind.Text, ""), (SheetCellKind.Number, "x"), (SheetCellKind.Bool, "2"), (SheetCellKind.Error, "#N/A"),
	];

	/// <summary>
	/// Custom codes: without a text section and with one, <c>@</c> alone or not, ones Excel refuses or the renderer can't
	/// read, an empty one (General), and codes with locales, conditions, fills and colors.
	/// </summary>
	private static readonly string[] Codes =
	[
		"0.00", "#,##0", "0;-0;0;@", "0;-0;0;\"x\"@", "@", "\"pre \"@", ";;;", "", "0.00e+00", "\"open", "[foo]0", "0;-0;0;0;0", "[Red]@", "@@", "@ \"kg\"",
		"yyyy-mm-dd;@", "[$-407]mmmm", "[$-F800]", "0.00e+00;@", "0;;;\"t:\"@", "[h]:mm", "# ?/?", "General", "\"=\"0", "0.0%", "[>5]0;@", "_(@_)", "*-@",
		"[$-x-sysdate]dddd", "[$-de-DE]mmmm;@", "0;[=0]\"z\"@", "\\", "[Color10]0;[Color57]@", "0;-0;;@", "mmm-yy", "\"Note: \"@",
	];

	/// <summary><see cref="CellFormatter.TryFormat"/>, given a <see cref="SheetCellKind"/> for core's kind of the same value.</summary>
	/// <param name="formatter">The formatter.</param>
	/// <param name="value">The field's stored value.</param>
	/// <param name="kind">The field's kind.</param>
	/// <param name="style">The field's style.</param>
	/// <param name="destination">Receives the text.</param>
	/// <param name="length">The text's length; -1; or the room it needs.</param>
	/// <returns>Whether the text was written.</returns>
	private delegate bool Formatting(CellFormatter formatter, ReadOnlySpan<byte> value, SheetCellKind kind, int style, Span<byte> destination, out int length);

	/// <summary><see cref="CellFormatter.TryFormat"/> as an open delegate, of core's kind's type.</summary>
	/// <typeparam name="TKind">Core's kind.</typeparam>
	/// <param name="formatter">The formatter.</param>
	/// <param name="value">The field's stored value.</param>
	/// <param name="kind">The field's kind.</param>
	/// <param name="style">The field's style.</param>
	/// <param name="destination">Receives the text.</param>
	/// <param name="length">The text's length; -1; or the room it needs.</param>
	/// <returns>Whether the text was written.</returns>
	private delegate bool Formatting<TKind>(CellFormatter formatter, ReadOnlySpan<byte> value, TKind kind, int style, Span<byte> destination, out int length);

	/// <summary>No styles part: every cell style is General's.</summary>
	[Fact]
	public void AnswersAsTheLookupDidWithoutStyles()
	{
		Assert.False(AssertSameAnswers(StyleTable.Empty));
		var formatter = new CellFormatter(StyleTable.Empty, DisplayCulture.Invariant, false);
		Assert.False(formatter.ChangesText);
		Assert.Equal(0, formatter.StyleOf(SheetCellKind.Number, 0));
		Assert.Equal(0, formatter.StyleOf(SheetCellKind.DateNumber, -1));
		Assert.Equal(0, formatter.StyleOf(SheetCellKind.Duration, 7));
		Assert.Equal(-1, formatter.StyleOf(SheetCellKind.Text, 0));
	}

	/// <summary>One cell format, General.</summary>
	[Fact]
	public void AnswersAsTheLookupDidForGeneralAlone()
	{
		Assert.False(AssertSameAnswers(Styles(Id + "0")));
	}

	/// <summary>Every built-in id, the accounting ones with their text sections among them, and ids no part defines.</summary>
	[Fact]
	public void AnswersAsTheLookupDidForBuiltInIds()
	{
		string[] formats = [.. Enumerable.Range(0, 170).Select(i => Id + i.ToString(CultureInfo.InvariantCulture)), Id + "1000", Id + "49", Id + "14", Id + "0"];
		Assert.True(AssertSameAnswers(Styles(formats)));
	}

	/// <summary>Custom codes, cell formats that share them, and built-in ids among them.</summary>
	[Fact]
	public void AnswersAsTheLookupDidForCustomCodes()
	{
		Assert.True(AssertSameAnswers(Styles([.. Codes, Id + "164", Id + "167", Id + "0", Id + "49", Id + "44", Id + "14", Id + "166"])));
		Assert.False(AssertSameAnswers(Styles("0.00", "@", "[Red]@", "yyyy-mm-dd;@", Id + "49", "0;-0;0;@", "0.00e+00;@", "0;\"open@", "General")));
	}

	/// <summary>The styles the reader's and the scanner's tests read with, and the test package's own.</summary>
	/// <returns>A task.</returns>
	[Fact]
	public async Task AnswersAsTheLookupDidForTheTestsStyles()
	{
		Assert.True(AssertSameAnswers(await FormattedTextTests.Book("").LoadStylesAsync(false)));
		Assert.False(AssertSameAnswers(await TestPackage.Workbook("Data").LoadStylesAsync(false)));
	}

	/// <summary>Phase 5's random codes (<see cref="FormatCodeFuzzTests.Code"/>), each a format of its own, those Excel and the renderer refuse among them.</summary>
	[Fact]
	public void AnswersAsTheLookupDidForRandomCodes()
	{
		AssertSameAnswers(Styles([.. Enumerable.Range(1, 600).Select(FormatCodeFuzzTests.Code)]));
	}

	/// <summary>
	/// A styles part Excel doesn't write: custom codes for built-in ids, a cell format without a number format, a format
	/// defined twice, and a text-changing format no cell format has.
	/// </summary>
	[Fact]
	public void AnswersAsTheLookupDidForCustomCodesOfBuiltInIds()
	{
		var part = Part(
			"<numFmt numFmtId=\"49\" formatCode=\"0.00\"/><numFmt numFmtId=\"14\" formatCode=\"&quot;d:&quot;@\"/><numFmt numFmtId=\"0\" formatCode=\"@\"/>" +
			"<numFmt numFmtId=\"170\" formatCode=\"&quot;first&quot;@\"/><numFmt numFmtId=\"170\" formatCode=\"0\"/><numFmt numFmtId=\"171\" formatCode=\"&quot;unused&quot;@\"/>",
			"<xf numFmtId=\"49\"/><xf/><xf numFmtId=\"0\"/><xf numFmtId=\"170\"/><xf numFmtId=\"14\"/>");
		Assert.True(AssertSameAnswers(Load(part)));

		// A text-changing format that no cell format has can't be a field's.
		Assert.False(AssertSameAnswers(Load(Part("<numFmt numFmtId=\"171\" formatCode=\"&quot;unused&quot;@\"/>", "<xf numFmtId=\"0\"/><xf numFmtId=\"49\"/>"))));
	}

	/// <summary>Many more cell formats than formats: the tables are by cell format.</summary>
	[Fact]
	public void AnswersAsTheLookupDidForManyCellFormatsOfFewFormats()
	{
		string[] shared = ["0.00", "\"pre \"@", Id + "14", Id + "49", Id + "164", Id + "165", Id + "44"];
		Assert.True(AssertSameAnswers(Styles([.. shared.Take(2), .. Enumerable.Range(0, 10_000).Select(i => shared[2 + (i % (shared.Length - 2))])])));
	}

	/// <summary>
	/// <see cref="CellFormatter.MaxCompiledFormats"/> distinct formats, which the formatter compiles when it is made, and one
	/// more, which it compiles as fields ask: the same answers either way.
	/// </summary>
	/// <param name="count">The distinct formats.</param>
	[Theory]
	[InlineData(CellFormatter.MaxCompiledFormats)]
	[InlineData(CellFormatter.MaxCompiledFormats + 1)]
	public void AnswersAsTheLookupDidAroundTheCap(int count)
	{
		var styles = Styles(Distinct(count));
		Assert.Equal(count, styles.NumberFormatCount);
		Assert.True(AssertSameAnswers(styles));
	}

	/// <summary>
	/// Whether a text or a boolean can take a style: not without a text section that changes text, even with <c>@</c> alone,
	/// a color beside it, or one the renderer can't read; yes with one that a cell format has, whatever the cells; and past
	/// <see cref="CellFormatter.MaxCompiledFormats"/>, where the formatter doesn't know.
	/// </summary>
	[Fact]
	public void ChangesTextWhereATextOrABooleanCanTakeAStyle()
	{
		Assert.False(ChangesText(StyleTable.Empty));
		Assert.False(ChangesText(Styles(Id + "0")));
		Assert.False(ChangesText(Styles(Id + "0", Id + "14", "0.00", "@", Id + "49", "yyyy-mm-dd;@", "0;-0;0;@", "[Red]@", "0.00e+00;@", "0;\"open@", "")));
		Assert.True(ChangesText(Styles(Id + "0", "0.00", "\"pre \"@")));
		Assert.True(ChangesText(Styles(Id + "0", ";;;")));
		Assert.True(ChangesText(Styles(Id + "44")));
		Assert.True(ChangesText(Styles(Id + "0", "@@")));
		Assert.False(ChangesText(Load(Part("<numFmt numFmtId=\"171\" formatCode=\"&quot;unused&quot;@\"/>", "<xf numFmtId=\"0\"/>"))));

		// At the cap the formatter knows; past it, it doesn't.
		var plain = Enumerable.Repeat("0.00", CellFormatter.MaxCompiledFormats + 1).ToArray();
		Assert.False(ChangesText(Styles([.. plain.Take(CellFormatter.MaxCompiledFormats)])));
		Assert.True(ChangesText(Styles(plain)));
	}

	/// <summary>
	/// Many threads at once ask a fresh formatter every kind's style of every cell style, and the text of each style, in
	/// orders of their own: the lookup's answers, below <see cref="CellFormatter.MaxCompiledFormats"/>, where the formatter
	/// only reads, and past it, where its threads compile codes as they ask.
	/// </summary>
	/// <param name="count">The distinct formats.</param>
	[Theory]
	[InlineData(300)]
	[InlineData(CellFormatter.MaxCompiledFormats + 1)]
	public void GivesManyThreadsAtOnceTheLookupsAnswers(int count)
	{
		var styles = Styles([.. Distinct(count), Id + "164", Id + "44", Id + "14"]);
		var lookup = new Lookup(styles, DisplayCulture.Invariant, false);
		var cellStyles = CellStyles(styles).ToArray();
		var expectedStyles = cellStyles.Select(s => Kinds.Select(k => lookup.StyleOf(k, s)).ToArray()).ToArray();
		var expectedTexts = Enumerable.Range(0, styles.NumberFormatCount).Select(s => Values.Select(v => lookup.Show(v.Value, v.Kind, s)).ToArray()).ToArray();
		var formatter = new CellFormatter(styles, DisplayCulture.Invariant, false);
		var threads = Math.Max(8, Environment.ProcessorCount);
		var mismatches = new ConcurrentQueue<string>();
		using var start = new Barrier(threads);
		var workers = Enumerable.Range(0, threads).Select(t => new Thread(() =>
		{
			start.SignalAndWait();
			for (var i = 0; i < cellStyles.Length && mismatches.Count < MaxMismatches; i++)
			{
				// Each thread starts at a cell style of its own, so that they meet over the same codes at different times.
				var at = (i + (t * 7919)) % cellStyles.Length;
				for (var k = 0; k < Kinds.Length; k++)
				{
					var actual = formatter.StyleOf(Kinds[k], cellStyles[at]);
					if (actual != expectedStyles[at][k])
					{
						mismatches.Enqueue($"Thread {t}: {Kinds[k]} in {cellStyles[at]} has {actual}, not {expectedStyles[at][k]}.");
					}
				}

				var style = at % expectedTexts.Length;
				for (var v = 0; v < Values.Length; v++)
				{
					var shown = Show(formatter, Values[v].Value, Values[v].Kind, style);
					if (shown != expectedTexts[style][v])
					{
						mismatches.Enqueue($"Thread {t}: {Values[v].Kind} {Values[v].Value} in {style} shows {shown ?? "(raw)"}, not {expectedTexts[style][v] ?? "(raw)"}.");
					}
				}
			}
		})).ToList();
		workers.ForEach(w => w.Start());
		workers.ForEach(w => w.Join());

		Assert.Empty(mismatches);
	}

#if NET
	/// <summary>
	/// <see cref="CellFormatter.StyleOf"/> allocates nothing, a fresh formatter's first answers too: it compiled its codes
	/// when it was made. Past <see cref="CellFormatter.MaxCompiledFormats"/> a text's first answers compile them, as before
	/// the tables, and then allocate nothing.
	/// </summary>
	[Fact]
	public void AnswersWithoutAllocating()
	{
		var styles = Styles([.. Codes, Id + "14", Id + "44", Id + "49"]);
		Answer(new CellFormatter(styles, DisplayCulture.Invariant, false), styles);

		// Two fresh formatters are measured, and the smaller kept: the runtime allocates once on the thread at a moment the
		// JIT's tiering sets (see ExcelToCsvTests.AllocatesTheSameForAnyNumberOfRows), which can fall in one pass but not in both.
		var first = Answer(new CellFormatter(styles, DisplayCulture.Invariant, false), styles);
		var second = Answer(new CellFormatter(styles, DisplayCulture.Invariant, false), styles);
		Assert.Equal(0, Math.Min(first, second));

		var hostile = Styles(Distinct(CellFormatter.MaxCompiledFormats + 1));
		var lazy = new CellFormatter(hostile, DisplayCulture.Invariant, false);
		Assert.True(Answer(lazy, hostile) > 0);
		Assert.Equal(0, Math.Min(Answer(lazy, hostile), Answer(lazy, hostile)));

		static long Answer(CellFormatter formatter, StyleTable styles)
		{
			var sum = 0L;
			var before = GC.GetAllocatedBytesForCurrentThread();
			for (var style = -2; style < styles.Count + 2; style++)
			{
				foreach (var kind in Kinds)
				{
					sum += formatter.StyleOf(kind, style);
				}
			}

			var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
			Assert.NotEqual(long.MinValue, sum);

			return allocated;
		}
	}
#endif

#if NET10_0_OR_GREATER
	/// <summary>
	/// A fresh formatter renders its first fields allocating nothing: <see cref="CellFormatter.TryFormat"/> takes the codes
	/// compiled when it was made.
	/// </summary>
	[OptimizedFact]
	public void RendersAFreshFormattersFirstFieldsWithoutAllocating()
	{
		var styles = Styles([.. Codes, Id + "14", Id + "44", Id + "49"]);
		var room = new byte[1024];
		var values = Values.Select(v => (v.Kind, Bytes: Encoding.UTF8.GetBytes(v.Value))).ToArray();
		Render(new CellFormatter(styles, DisplayCulture.Invariant, false));

		// As AnswersWithoutAllocating: the smaller of two fresh formatters.
		Assert.Equal(0, Math.Min(Render(new CellFormatter(styles, DisplayCulture.Invariant, false)), Render(new CellFormatter(styles, DisplayCulture.Invariant, false))));

		long Render(CellFormatter formatter)
		{
			var sum = 0L;
			var before = GC.GetAllocatedBytesForCurrentThread();
			for (var style = 0; style < styles.NumberFormatCount; style++)
			{
				foreach (var (kind, bytes) in values)
				{
					TryFormat(formatter, bytes, kind, style, room, out var length);
					sum += length;
				}
			}

			var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
			Assert.NotEqual(long.MinValue, sum);

			return allocated;
		}
	}
#endif

	/// <summary>Binds <see cref="TryFormat"/>: <see cref="CellFormatter.TryFormat"/>, each <see cref="SheetCellKind"/> passed as core's kind of its value.</summary>
	/// <typeparam name="TKind">Core's kind.</typeparam>
	/// <returns>The call.</returns>
	private static Formatting Bind<TKind>()
		where TKind : struct
	{
		var tryFormat = (Formatting<TKind>)Delegate.CreateDelegate(typeof(Formatting<TKind>), typeof(CellFormatter).GetMethod(nameof(CellFormatter.TryFormat))!);
		var kinds = Enumerable.Range(0, 256).Select(k => (TKind)Enum.ToObject(typeof(TKind), k)).ToArray();

		return (CellFormatter formatter, ReadOnlySpan<byte> value, SheetCellKind kind, int style, Span<byte> destination, out int length) =>
			tryFormat(formatter, value, kinds[(byte)kind], style, destination, out length);
	}

	/// <summary>
	/// Checks a fresh formatter in each culture against the lookup: every kind's style in every cell style from -2 to two
	/// past the cell formats, and the extremes; the text of <see cref="Values"/> in every style, and in styles that aren't
	/// any; and <see cref="CellFormatter.ChangesText"/>, which is exact up to <see cref="CellFormatter.MaxCompiledFormats"/>
	/// distinct formats and true past them.
	/// </summary>
	/// <param name="styles">The styles.</param>
	/// <returns>Whether a text or a boolean takes a style in any culture.</returns>
	private static bool AssertSameAnswers(StyleTable styles)
	{
		var mismatches = new List<string>();
		var anyText = false;
		foreach (var (culture, date1904) in Cultures)
		{
			var formatter = new CellFormatter(styles, culture, date1904);
			var lookup = new Lookup(styles, culture, date1904);
			var texts = false;
			foreach (var style in CellStyles(styles))
			{
				foreach (var kind in Kinds)
				{
					var expected = lookup.StyleOf(kind, style);
					var actual = formatter.StyleOf(kind, style);
					if (actual != expected && mismatches.Count < MaxMismatches)
					{
						mismatches.Add($"{Name(culture)}: {kind} in {style} has {actual}, not {expected}.");
					}

					texts |= expected >= 0 && kind is SheetCellKind.Text or SheetCellKind.Bool;
				}
			}

			var changes = styles.NumberFormatCount > CellFormatter.MaxCompiledFormats || texts;
			if (formatter.ChangesText != changes)
			{
				mismatches.Add($"{Name(culture)}: ChangesText is {formatter.ChangesText}, not {changes}.");
			}

			foreach (var style in Enumerable.Range(0, styles.NumberFormatCount).Concat([-1, styles.NumberFormatCount, MaxStyle]))
			{
				foreach (var (kind, value) in Values)
				{
					var expected = lookup.Show(value, kind, style);
					var actual = Show(formatter, value, kind, style);
					if (actual != expected && mismatches.Count < MaxMismatches)
					{
						mismatches.Add($"{Name(culture)}: {kind} {value} in {style} shows {actual ?? "(raw)"}, not {expected ?? "(raw)"}.");
					}
				}
			}

			anyText |= texts;
		}

		Assert.Empty(mismatches);

		return anyText;
	}

	/// <summary>Whether a fresh formatter of a workbook's styles, in the invariant culture, says a text or a boolean can take a style.</summary>
	/// <param name="styles">The styles.</param>
	/// <returns>Its <see cref="CellFormatter.ChangesText"/>.</returns>
	private static bool ChangesText(StyleTable styles) => new CellFormatter(styles, DisplayCulture.Invariant, false).ChangesText;

	/// <summary>The cell styles a check asks for: from -2 to two past the cell formats, and the extremes.</summary>
	/// <param name="styles">The styles.</param>
	/// <returns>The cell styles.</returns>
	private static IEnumerable<int> CellStyles(StyleTable styles) => Enumerable.Range(-2, styles.Count + 5).Concat([int.MinValue, int.MaxValue]);

	/// <summary>
	/// A number of custom codes, each a format of its own: <see cref="Codes"/> over and over, the same code under new ids,
	/// so that the formats are distinct and as varied as they.
	/// </summary>
	/// <param name="count">The number of formats.</param>
	/// <returns>The codes.</returns>
	private static string[] Distinct(int count) => [.. Enumerable.Range(0, count).Select(i => Codes[i % Codes.Length])];

	/// <summary>A culture's name, for a mismatch.</summary>
	/// <param name="culture">The culture.</param>
	/// <returns>Its short date, which tells the three apart.</returns>
	private static string Name(DisplayCulture culture) => culture.IsInvariant ? "Excel en-US" : culture.ShortDate;

	/// <summary>The text a formatter shows a value in, through <see cref="CellFormatter.TryFormat"/>.</summary>
	/// <param name="formatter">The formatter.</param>
	/// <param name="value">The value, as the scanner stores it.</param>
	/// <param name="kind">Its kind.</param>
	/// <param name="style">Its style.</param>
	/// <returns>The text; <see langword="null"/> when the field's text is its own.</returns>
	private static string? Show(CellFormatter formatter, string value, SheetCellKind kind, int style)
	{
		var room = new byte[1024];
		if (TryFormat(formatter, Encoding.UTF8.GetBytes(value), kind, style, room, out var length))
		{
			return Encoding.UTF8.GetString(room, 0, length);
		}

		return length < 0 ? null : $"(needs {length} bytes)";
	}

	/// <summary>
	/// The styles of a workbook whose cell formats have these number formats, in order: <c>id:14</c> is the id 14, built in or
	/// a custom one an earlier format defined; any other is a custom code with an id of its own, from 164 on.
	/// </summary>
	/// <param name="formats">The cell formats' number formats.</param>
	/// <returns>The styles.</returns>
	private static StyleTable Styles(params string[] formats)
	{
		var custom = new StringBuilder();
		var cells = new StringBuilder();
		var next = 164;
		foreach (var format in formats)
		{
			int id;
			if (format.StartsWith(Id, StringComparison.Ordinal))
			{
				id = int.Parse(format.Substring(Id.Length), CultureInfo.InvariantCulture);
			}
			else
			{
				id = next++;
				custom.Append($"<numFmt numFmtId=\"{id}\" formatCode=\"{Attribute(format)}\"/>");
			}

			cells.Append($"<xf numFmtId=\"{id}\" fontId=\"0\" applyNumberFormat=\"1\"/>");
		}

		return Load(Part(custom.ToString(), cells.ToString()));
	}

	/// <summary>A styles part of these custom formats and cell formats.</summary>
	/// <param name="numberFormats">The <c>numFmt</c> elements.</param>
	/// <param name="cellFormats">The <c>xf</c> elements of <c>cellXfs</c>.</param>
	/// <returns>The part's XML.</returns>
	private static string Part(string numberFormats, string cellFormats) =>
		TestPackage.Declaration + $"<styleSheet xmlns=\"{TestPackage.MainNamespace}\"><numFmts>{numberFormats}</numFmts>" +
		"<fonts count=\"1\"><font><sz val=\"11\"/></font></fonts><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\"/></cellStyleXfs>" +
		$"<cellXfs>{cellFormats}</cellXfs></styleSheet>";

	/// <summary>Loads a styles part, in a workbook of one sheet.</summary>
	/// <param name="part">The part's XML.</param>
	/// <returns>The styles.</returns>
	private static StyleTable Load(string part) => TestPackage.Workbook("Data").Set("xl/styles.xml", part).LoadStylesAsync(false).GetAwaiter().GetResult();

	/// <summary>A code as an attribute's value: escaped, without the control characters XML can't hold, and tab, line feed and carriage return as references.</summary>
	/// <param name="code">The code.</param>
	/// <returns>The attribute's value.</returns>
	private static string Attribute(string code) =>
		Sheets.Escape(new string(code.Where(c => c >= ' ' || c is '\t' or '\n' or '\r').ToArray()))
			.Replace("\"", "&quot;").Replace("\t", "&#9;").Replace("\n", "&#10;").Replace("\r", "&#13;");

	/// <summary>
	/// The formatter as it was before its tables, at <c>6a51d85d</c>: the oracle. Each style is looked up in the styles, each
	/// code compiled the first time a field asks for it, and whether it changes text learned then. It renders as the
	/// formatter did, with .NET's parser for numbers (core's isn't the tests' to call), which reads <see cref="Values"/>
	/// the same.
	/// </summary>
	/// <param name="styles">The workbook's styles.</param>
	/// <param name="culture">The reader's culture.</param>
	/// <param name="date1904">Whether the workbook uses the 1904 date system.</param>
	private sealed class Lookup(StyleTable styles, DisplayCulture culture, bool date1904)
	{
		/// <summary>A format whose text section changes text and booleans.</summary>
		private const sbyte ChangesText = 1;

		/// <summary>A format that shows text and booleans as they are.</summary>
		private const sbyte KeepsText = -1;

		private readonly FormatCode?[] codes = new FormatCode?[styles.NumberFormatCount];
		private readonly sbyte[] texts = new sbyte[styles.NumberFormatCount];

		/// <summary>The style a field takes, as the formatter gave it.</summary>
		/// <param name="kind">The field's kind.</param>
		/// <param name="cellStyle">The cell's <c>s</c>.</param>
		/// <returns>The style, or -1.</returns>
		public int StyleOf(SheetCellKind kind, int cellStyle)
		{
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

		/// <summary>The text a value shows in a style, as the formatter's <c>TryFormat</c> wrote it.</summary>
		/// <param name="value">The value, as the scanner stores it.</param>
		/// <param name="kind">Its kind.</param>
		/// <param name="style">Its style.</param>
		/// <returns>The text; <see langword="null"/> when the field's text is its own.</returns>
		public string? Show(string value, SheetCellKind kind, int style)
		{
			if ((uint)style >= (uint)codes.Length)
			{
				return null;
			}

			var code = Code(style);
			var room = new byte[1024];
			var text = new ShownText(room);
			var bytes = Encoding.UTF8.GetBytes(value);
			bool shown;
			if (kind is SheetCellKind.Number or SheetCellKind.DateNumber or SheetCellKind.Duration)
			{
				shown = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && NumberText.TryWrite(code, number, culture, date1904, ref text);
			}
			else if (kind == SheetCellKind.Bool)
			{
				shown = value is "0" or "1" && NumberText.TryWriteText(code, value == "1" ? "TRUE"u8 : "FALSE"u8, ref text);
			}
			else if (kind == SheetCellKind.Text)
			{
				shown = NumberText.TryWriteText(code, bytes, ref text);
			}
			else
			{
				shown = false;
			}

			if (!shown)
			{
				return null;
			}

			return text.Fits ? Encoding.UTF8.GetString(room, 0, text.Length) : $"(needs {text.Length} bytes)";
		}

		/// <summary>The compiled code of a number format, compiled the first time it is asked for.</summary>
		/// <param name="index">The number format's index.</param>
		/// <returns>The code.</returns>
		private FormatCode Code(int index)
		{
			var code = codes[index];
			if (code is null)
			{
				var id = styles.NumberFormatIdAt(index);
				var custom = styles.FormatCode(id);
				code = FormatCode.Compile(custom is null ? culture.BuiltIn(id) : custom.Length == 0 ? "General" : custom);
				codes[index] = code;
			}

			return code;
		}

		/// <summary>Whether a number format changes the text of a text or a boolean: it has a text section that isn't <c>@</c> alone.</summary>
		/// <param name="index">The number format's index.</param>
		/// <returns>Whether it does.</returns>
		private bool ShowsText(int index)
		{
			var known = texts[index];
			if (known == 0)
			{
				var code = Code(index);
				var tokens = code.IsReadable ? code.Text?.Tokens : null;
				known = tokens is null || (tokens.Length == 1 && tokens[0].Kind == FormatTokenKind.At) ? KeepsText : ChangesText;
				texts[index] = known;
			}

			return known == ChangesText;
		}
	}
}
