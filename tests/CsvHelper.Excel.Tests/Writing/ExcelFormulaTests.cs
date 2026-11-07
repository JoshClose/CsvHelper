using System.Globalization;
using System.Text;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>A record with a formula, written and read as CSV through the converter.</summary>
[CsvRecord]
public partial class ConvertedFormula
{
	/// <summary>A name.</summary>
	public string Name { get; set; } = "";

	/// <summary>A formula, read and written by its converter.</summary>
	[CsvConverter(typeof(ExcelFormulaConverter))]
	public ExcelFormula Total { get; set; }
}

/// <summary>
/// <see cref="ExcelFormula"/>: its text with and without its <c>=</c>, its checks, equality, parsing and formatting, its
/// converter, and <see cref="ExcelWriterExtensions.WriteFormula"/> in a CSV writer.
/// </summary>
public class ExcelFormulaTests
{
	[Fact]
	public void HoldsAFormulaGivenWithOrWithoutItsSign()
	{
		var plain = new ExcelFormula("SUM(A1:A3)");
		var signed = new ExcelFormula("=SUM(A1:A3)");

		Assert.Equal("SUM(A1:A3)", plain.Formula);
		Assert.Equal("SUM(A1:A3)", signed.Formula);
		Assert.Equal("=SUM(A1:A3)", plain.ToString());
		Assert.Equal("=SUM(A1:A3)", signed.ToString());
		Assert.Equal(plain, signed);

		// Text with its sign is kept as it is.
		var text = "=A1*2";
		Assert.Same(text, new ExcelFormula(text).ToString());
	}

	[Fact]
	public void KeepsLineBreaksOutsideItsStringsAsLineFeeds()
	{
		var formula = new ExcelFormula("IF(A1>0,\r\n  \"a\r\nb\",\r\n  2)");

		Assert.Equal("IF(A1>0,\n  \"a\r\nb\",\n  2)", formula.Formula);
		Assert.Equal(new ExcelFormula("=IF(A1>0,\n  \"a\r\nb\",\n  2)"), formula);
	}

	[Fact]
	public void HasNoFormulaByDefault()
	{
		var none = default(ExcelFormula);

		Assert.Equal("", none.Formula);
		Assert.Equal("", none.ToString());
		Assert.Equal("", none.ToString("G", CultureInfo.InvariantCulture));
		Assert.Equal(0, none.GetHashCode());
		Assert.Equal(default, none);
		Assert.NotEqual(new ExcelFormula("A1"), none);
	}

	[Fact]
	public void RefusesWhatMakesExcelRefuseTheWorkbook()
	{
		Assert.Equal("formula", Assert.Throws<ArgumentNullException>(() => new ExcelFormula(null!)).ParamName);

		var open = Assert.Throws<ArgumentException>(() => new ExcelFormula("SUM(A1:A3"));
		Assert.Equal("formula", open.ParamName);
		Assert.StartsWith("The formula \"SUM(A1:A3\" has a parenthesis that isn't closed, and Excel opens no workbook with it.", open.Message);

		var longer = Assert.Throws<ArgumentException>(() => new ExcelFormula("=" + new string('1', 9000)));
		Assert.StartsWith("A formula is at most 8,192 characters long, and this one has 9,000.", longer.Message);
		_ = new ExcelFormula("=" + new string('1', 8192));

		// The check is of the formula after its one =.
		Assert.StartsWith("A formula can't be empty", Assert.Throws<ArgumentException>(() => new ExcelFormula("")).Message);
		Assert.StartsWith("A formula can't be empty", Assert.Throws<ArgumentException>(() => new ExcelFormula("=")).Message);
		Assert.StartsWith("The formula \"=A1\" starts with the operator =", Assert.Throws<ArgumentException>(() => new ExcelFormula("==A1")).Message);
	}

	[Fact]
	public void ComparesTextOrdinally()
	{
		var formula = new ExcelFormula("SUM(A1)");

		Assert.True(formula == new ExcelFormula("=SUM(A1)"));
		Assert.False(formula != new ExcelFormula("=SUM(A1)"));
		Assert.True(formula != new ExcelFormula("sum(a1)"));
		Assert.False(formula == new ExcelFormula("sum(a1)"));
		Assert.True(formula.Equals((object)new ExcelFormula("SUM(A1)")));
		Assert.False(formula.Equals("=SUM(A1)"));
		Assert.False(formula.Equals(null));
		Assert.Equal(new ExcelFormula("SUM(A1)").GetHashCode(), formula.GetHashCode());
		Assert.Equal(StringComparer.Ordinal.GetHashCode("=SUM(A1)"), formula.GetHashCode());
	}

	[Fact]
	public void ParsesItsText()
	{
		Assert.Equal("SUM(A1:A3)", ExcelFormula.Parse("=SUM(A1:A3)").Formula);
		Assert.Equal("SUM(A1:A3)", ExcelFormula.Parse("=SUM(A1:A3)", CultureInfo.GetCultureInfo("de-DE")).Formula);
		Assert.Equal(default, ExcelFormula.Parse(""));
		var text = "=A1";
		Assert.Same(text, ExcelFormula.Parse(text).ToString());
		Assert.Equal("IF(A1,\n1)", ExcelFormula.Parse("=IF(A1,\r\n1)").Formula);

		// Every text ToString gives reads back.
		foreach (var formula in new[] { default, new ExcelFormula("A1"), new ExcelFormula("\"a\r\nb\""), new ExcelFormula("'Q1 Data'!A1+Table1[a'[b]") })
		{
			Assert.Equal(formula, ExcelFormula.Parse(formula.ToString()));
		}

		Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => ExcelFormula.Parse(null!)).ParamName);
		Assert.Equal("A formula's text starts with =, and this one doesn't.", Assert.Throws<FormatException>(() => ExcelFormula.Parse("SUM(A1)")).Message);
		Assert.Equal("A formula's text starts with =, and this one doesn't.", Assert.Throws<FormatException>(() => ExcelFormula.Parse(" =A1")).Message);
		Assert.Equal("The formula \"SUM(\" has a parenthesis that isn't closed, and Excel opens no workbook with it.", Assert.Throws<FormatException>(() => ExcelFormula.Parse("=SUM(")).Message);
		Assert.StartsWith("A formula can't be empty", Assert.Throws<FormatException>(() => ExcelFormula.Parse("=")).Message);
	}

	[Fact]
	public void TriesToParseItsText()
	{
		Assert.True(ExcelFormula.TryParse("=A1", null, out var formula));
		Assert.Equal(new ExcelFormula("A1"), formula);
		Assert.True(ExcelFormula.TryParse("", null, out formula));
		Assert.Equal(default, formula);

		Assert.False(ExcelFormula.TryParse(null, null, out formula));
		Assert.Equal(default, formula);
		Assert.False(ExcelFormula.TryParse("A1", null, out formula));
		Assert.Equal(default, formula);
		Assert.False(ExcelFormula.TryParse("=A1+", null, out formula));
		Assert.Equal(default, formula);
	}

	[Fact]
	public void FormatsTheSameInEveryCultureAndFormat()
	{
		var formula = new ExcelFormula("SUM(1.5,2)");

		Assert.Equal("=SUM(1.5,2)", formula.ToString("N2", CultureInfo.GetCultureInfo("de-DE")));
		Assert.Equal("=SUM(1.5,2)", formula.ToString(null, null));
		Assert.Equal("=SUM(1.5,2)", string.Format(CultureInfo.GetCultureInfo("fr-FR"), "{0:X}", formula));
	}

#if NET
	[Fact]
	public void FormatsIntoSpans()
	{
		ISpanFormattable chars = new ExcelFormula("\"日本\"&A1");
		var buffer = new char[16];
		Assert.True(chars.TryFormat(buffer, out var written, "", null));
		Assert.Equal("=\"日本\"&A1", new string(buffer, 0, written));
		Assert.True(chars.TryFormat(buffer.AsSpan(0, 8), out written, "", null));
		Assert.Equal(8, written);
		Assert.False(chars.TryFormat(buffer.AsSpan(0, 7), out written, "", null));
		Assert.Equal(0, written);

		IUtf8SpanFormattable utf8 = new ExcelFormula("\"日本\"&A1");
		var bytes = new byte[16];
		Assert.True(utf8.TryFormat(bytes, out written, "", null));
		Assert.Equal("=\"日本\"&A1", Encoding.UTF8.GetString(bytes, 0, written));
		Assert.Equal(12, written);
		Assert.True(utf8.TryFormat(bytes.AsSpan(0, 12), out written, "", null));
		Assert.False(utf8.TryFormat(bytes.AsSpan(0, 11), out written, "", null));
		Assert.Equal(0, written);

		Assert.True(((IUtf8SpanFormattable)default(ExcelFormula)).TryFormat(bytes, out written, "", null));
		Assert.Equal(0, written);
		Assert.True(((ISpanFormattable)default(ExcelFormula)).TryFormat([], out written, "", null));
		Assert.Equal(0, written);
	}

	[Fact]
	public void ParsesSpans()
	{
		Assert.Equal(new ExcelFormula("A1*2"), ParseSpan<ExcelFormula>("=A1*2"));
		Assert.Equal(default, ParseSpan<ExcelFormula>(""));
		Assert.Equal("A formula's text starts with =, and this one doesn't.", Assert.Throws<FormatException>(() => ParseSpan<ExcelFormula>("A1")).Message);
		Assert.Equal("The formula \"A1*\" ends with the operator *, and Excel opens no workbook with it.", Assert.Throws<FormatException>(() => ParseSpan<ExcelFormula>("=A1*")).Message);

		Assert.True(TryParseSpan<ExcelFormula>("=A1", out var formula));
		Assert.Equal(new ExcelFormula("A1"), formula);
		Assert.False(TryParseSpan("=A1*", out formula));
		Assert.Equal(default, formula);
	}

	/// <summary>Parses text as a span through <see cref="ISpanParsable{TSelf}"/>, as the generated mapper does.</summary>
	/// <typeparam name="T">The type.</typeparam>
	/// <param name="text">The text.</param>
	/// <returns>The value.</returns>
	private static T ParseSpan<T>(string text) where T : ISpanParsable<T> => T.Parse(text.AsSpan(), CultureInfo.InvariantCulture);

	/// <summary>Tries to parse text as a span through <see cref="ISpanParsable{TSelf}"/>.</summary>
	/// <typeparam name="T">The type.</typeparam>
	/// <param name="text">The text.</param>
	/// <param name="value">The value.</param>
	/// <returns>Whether it was parsed.</returns>
	private static bool TryParseSpan<T>(string text, out T value) where T : ISpanParsable<T> => T.TryParse(text.AsSpan(), CultureInfo.InvariantCulture, out value!);
#endif

	[Fact]
	public void WritesItsTextInCsv()
	{
		using var text = new StringWriter();
		using (var writer = CsvWriter.Create(text))
		{
			writer.WriteFormula("SUM(A1:A3)");
			writer.WriteField(new ExcelFormula("=\"a,b\""));
			writer.WriteField((object)new ExcelFormula("A1"));
			writer.WriteField(default(ExcelFormula));
			writer.EndRecord();
		}

		Assert.Equal("=SUM(A1:A3),\"=\"\"a,b\"\"\",=A1,\r\n", text.ToString());
		Assert.Equal("writer", Assert.Throws<ArgumentNullException>(() => ExcelWriterExtensions.WriteFormula(null!, "A1")).ParamName);
		using var other = CsvWriter.Create(new StringWriter());
		Assert.Equal("formula", Assert.Throws<ArgumentNullException>(() => other.WriteFormula(null!)).ParamName);
		Assert.Equal("formula", Assert.Throws<ArgumentException>(() => other.WriteFormula("A1+")).ParamName);
	}

	[Fact]
	public void IsTextToCsvsInjectionProtection()
	{
		using var text = new StringWriter();
		using (var writer = CsvWriter.Create(text, new CsvWriterOptions { Injection = CsvInjection.Escape }))
		{
			writer.WriteFormula("SUM(A1:A3)");
			writer.EndRecord();
		}

		Assert.Equal("\"'=SUM(A1:A3)\"\r\n", text.ToString());
	}

	[Fact]
	public void ConvertsCsvFields()
	{
		using var text = new StringWriter();
		using (var writer = CsvWriter.Create(text))
		{
			writer.WriteRecords([new ConvertedFormula { Name = "a", Total = new ExcelFormula("SUM(B1:B3)") }, new ConvertedFormula { Name = "b" }]);
		}

		Assert.Equal("Name,Total\r\na,=SUM(B1:B3)\r\nb,\r\n", text.ToString());
		using var reader = CsvReader.Create(new StringReader(text.ToString()));
		var read = reader.GetRecords<ConvertedFormula>().ToList();
		Assert.Equal(new ExcelFormula("SUM(B1:B3)"), read[0].Total);
		Assert.Equal(default, read[1].Total);

		using var bad = CsvReader.Create(new StringReader("Name,Total\r\na,SUM(B1)\r\n"));
		Assert.Contains("A formula's text starts with =", Assert.ThrowsAny<Exception>(() => bad.GetRecords<ConvertedFormula>().ToList()).ToString());
	}
}
