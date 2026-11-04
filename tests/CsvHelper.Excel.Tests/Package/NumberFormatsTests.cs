using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using CsvHelper.Excel.Tests.Corpus;

namespace CsvHelper.Excel.Tests.Package;

/// <summary>
/// Which number formats make a number a date, a duration or a number, against Excel's answers in
/// <c>Files/excel-formats.json</c>, which the corpus tool's <c>formats</c> command asks Excel for through COM.
/// </summary>
/// <remarks>
/// <para>Excel says it three ways, each about some formats:</para>
/// <list type="bullet">
/// <item><description>
/// <c>Range.Value</c> is a date when the format's first section shows a day, a month or a year, not for a time alone.
/// </description></item>
/// <item><description><c>CELL("format")</c> is <c>D6</c> to <c>D9</c> for its four time formats.</description></item>
/// <item><description>The text it shows for 1.5 in an elapsed-time format is 36 hours, 2,160 minutes or 129,600 seconds.</description></item>
/// </list>
/// <para>A format none of them flags is a number, except those of <see cref="TimesAndCalendarParts"/>.</para>
/// </remarks>
public class NumberFormatsTests
{
	/// <summary>The answers' file.</summary>
	private static readonly XElement Answers = Load();

	/// <summary>
	/// The codes that show a time or part of a date, which none of Excel's answers flags: a time alone outside
	/// <c>CELL("format")</c>'s four (Excel's own <c>mm:ss</c> included), and an era, a Buddhist year or a weekday alone,
	/// which <c>Range.Value</c> doesn't take for a date. Each shows it: <c>12:00</c>, <c>PM</c>, <c>2024</c>, <c>Mon</c>.
	/// </summary>
	internal static readonly HashSet<string> TimesAndCalendarParts =
	[
		"mm:ss", "mm:ss.0", "h", "hh", "s", "ss", "ss.000", "h AM/PM", "am/pm", "A/P", "[$-409]h:mm:ss AM/PM;@", "[$-F400]h:mm:ss AM/PM",
		"[$-x-systime]h:mm:ss AM/PM", "e", "ee", "g", "gg", "ggg", "b", "bb", "bbbb", "aaa", "aaaa",
	];

	/// <summary>The text of an elapsed-time format: 1.5 days in hours, minutes or seconds, or 45306.5 in hours.</summary>
	private static readonly Regex Elapsed = new(@"(^|\D)0*(36|2160|129600|1087356)(\D|$)");

	[Fact]
	public void ClassifiesFormatCodesAsExcelDoes()
	{
		var differences = new List<string>();
		var codes = Answers.Element("codes")!.Elements("item").ToList();
		Assert.True(codes.Count > 150);
		foreach (var item in codes)
		{
			var code = Text(item, "code")!;
			var kind = NumberFormats.Classify(code);
			if (Text(item, "valid") != "true")
			{
				// Excel refuses it, so there's no answer: any kind will do.
				continue;
			}

			var expected = TimesAndCalendarParts.Contains(code)
				? SheetCellKind.DateNumber
				: Expected(Text(item, "date") == "true", Text(item, "category"), Text(item, "durationText"));
			if (kind != expected)
			{
				differences.Add($"{code}: {kind}, Excel {expected} ({Text(item, "category")}, {Text(item, "text")}, {Text(item, "durationText")})");
			}
		}

		Assert.True(differences.Count == 0, string.Join("\n", differences));
		Assert.All(TimesAndCalendarParts, code => Assert.Contains(codes, item => Text(item, "code") == code && Text(item, "valid") == "true"));
	}

	[Fact]
	public void ClassifiesTheBuiltInFormatsAsExcelDoes()
	{
		var ids = Answers.Element("cells")!.Elements("item").Where(item => Text(item, "name")!.StartsWith("id ", StringComparison.Ordinal) && Text(item, "name")!.Length <= 6).ToList();
		Assert.Equal(Enumerable.Range(0, 164), ids.Select(item => int.Parse(Text(item, "id")!, System.Globalization.CultureInfo.InvariantCulture)));
		foreach (var item in ids)
		{
			var id = int.Parse(Text(item, "id")!, System.Globalization.CultureInfo.InvariantCulture);
			var excelCode = Text(item, "excelCode")!;
			var kind = NumberFormats.BuiltIn(id);

			// The format Excel shows for the id is classified the same, and where Excel's answers flag it, as they say.
			Assert.True(NumberFormats.Classify(excelCode) == kind, $"id {id} ({excelCode}): {kind}, its code {NumberFormats.Classify(excelCode)}");
			var expected = TimesAndCalendarParts.Contains(excelCode)
				? SheetCellKind.DateNumber
				: Expected(Text(item, "date") == "true", Text(item, "category"), Text(item, "text"));
			Assert.True(expected == kind, $"id {id} ({excelCode}): {kind}, Excel {expected}");
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task FollowsExcelForRedefinedIdsAndFormatsNotApplied(bool async)
	{
		// The cases besides the built-in ids: a custom date, applyNumberFormat="0", and numFmts redefining built-in ids.
		var cases = Answers.Element("cells")!.Elements("item").Where(item => !(Text(item, "name")!.StartsWith("id ", StringComparison.Ordinal) && Text(item, "name")!.Length <= 6)).ToList();
		Assert.Equal(8, cases.Count);
		foreach (var item in cases)
		{
			var id = Text(item, "id")!;
			var code = Text(item, "code");
			var formats = code is null ? "" : $"<numFmts count=\"1\"><numFmt numFmtId=\"{id}\" formatCode=\"{System.Security.SecurityElement.Escape(code)}\"/></numFmts>";
			var applied = Text(item, "applied") == "true" ? "1" : "0";
			var styles = await TestPackage.Workbook("Data")
				.Replace("xl/styles.xml", "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"yyyy\\-mm\\-dd\"/></numFmts>", formats)
				.Replace("xl/styles.xml", "<xf numFmtId=\"14\" applyNumberFormat=\"1\"/>", $"<xf numFmtId=\"{id}\" applyNumberFormat=\"{applied}\"/>")
				.LoadStylesAsync(async);
			var expected = Expected(Text(item, "date") == "true", Text(item, "category"), Text(item, "text"));

			Assert.True(expected == styles.NumberKind(1), $"{Text(item, "name")}: {styles.NumberKind(1)}, Excel {expected}");
		}
	}

	[Theory]
	[InlineData("yyyy-mm-dd", "DateNumber")]
	[InlineData("h:mm", "DateNumber")]
	[InlineData("[h]:mm", "Duration")]
	[InlineData("[HHH]:MM", "Duration")]
	[InlineData("[mm]:ss", "Duration")]
	[InlineData("[s]", "Duration")]
	[InlineData("yyyy-mm-dd [h]:mm", "DateNumber")]
	[InlineData("[h]:mm;yyyy", "Duration")]
	[InlineData("0;yyyy", "Number")]
	[InlineData("General", "Number")]
	[InlineData("General d", "DateNumber")]
	[InlineData("0.00E+00", "Number")]
	[InlineData("0.00e+00", "Number")]
	[InlineData("e-m-d", "DateNumber")]
	[InlineData("\"yyyy\"0", "Number")]
	[InlineData("0\\d", "Number")]
	[InlineData("0_d", "Number")]
	[InlineData("*d0", "Number")]
	[InlineData("[Red][$-409]0", "Number")]
	[InlineData("[hh0]0", "Number")]
	[InlineData("[h", "Number")]
	[InlineData("\"unclosed d", "Number")]
	[InlineData("0\\", "Number")]
	[InlineData("aa", "Number")]
	[InlineData("aaa", "DateNumber")]
	[InlineData("", "Number")]
	[InlineData(";;;", "Number")]
	public void ClassifiesTheTokensOfTheFirstSection(string code, string kind) => Assert.Equal(kind, NumberFormats.Classify(code).ToString());

	/// <summary>What Excel's answers say a format is.</summary>
	/// <param name="date">Whether <c>Range.Value</c> was a date.</param>
	/// <param name="category">What <c>CELL("format")</c> said.</param>
	/// <param name="elapsedText">The text of a number that an elapsed-time format shows in hours, minutes or seconds.</param>
	/// <returns>The kind.</returns>
	internal static SheetCellKind Expected(bool date, string? category, string? elapsedText)
	{
		if (date || category is "D6" or "D7" or "D8" or "D9")
		{
			return SheetCellKind.DateNumber;
		}

		return elapsedText is not null && Elapsed.IsMatch(elapsedText) ? SheetCellKind.Duration : SheetCellKind.Number;
	}

	/// <summary>A property of an item of the answers, or <see langword="null"/> when it is missing or null.</summary>
	/// <param name="item">The item.</param>
	/// <param name="name">The property.</param>
	/// <returns>Its value as text.</returns>
	private static string? Text(XElement item, string name) =>
		item.Element(name) is { } element && (string?)element.Attribute("type") != "null" ? element.Value : null;

	/// <summary>Reads the answers' JSON as XML (<see cref="JsonReaderWriterFactory"/>), which every target has.</summary>
	/// <returns>The root element.</returns>
	private static XElement Load()
	{
		var bytes = File.ReadAllBytes(CorpusFiles.PathOf("excel-formats.json"));
		using var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, XmlDictionaryReaderQuotas.Max);

		return XElement.Load(reader);
	}
}
