using System.Globalization;
using System.Text;

namespace CsvHelper.Excel.Tests.Conversion;

/// <summary>
/// <see cref="ColumnTypes"/>: the types a CSV field matches, a column's narrowest type, and a field's form for the sheet's
/// sink. The forms of <c>infer.csv</c> (<c>Csv\_bench\results\phase6\design</c>) are each read as the design reads them;
/// Excel's and LibreOffice's readings, in the comments, differ where they lose what a field shows.
/// </summary>
public class ColumnTypesTests
{
	/// <summary>The types of the default options: invariant numbers, offsets as text, dates before 1900 as text.</summary>
	private static readonly ColumnTypes Default = new(CultureInfo.InvariantCulture, ExcelWriterOptions.Default);

	/// <summary>Every form of <c>infer.csv</c>, with the type a field of it alone gives its column, and Excel's reading.</summary>
	public static TheoryData<string, object> InferForms => new()
	{
		{ "123", ColumnType.Number },
		{ "00123", ColumnType.Text },                        // Excel and LibreOffice: 123
		{ "007", ColumnType.Text },                          // Excel: 7
		{ "-45", ColumnType.Number },
		{ "+45", ColumnType.Text },                          // Excel: 45
		{ "1.5", ColumnType.Number },
		{ ".5", ColumnType.Text },                           // Excel: 0.5
		{ "5.", ColumnType.Text },                           // Excel: 5
		{ "1e5", ColumnType.Number },
		{ "1E5", ColumnType.Number },
		{ "1.5E+20", ColumnType.Number },
		{ "123456789012345", ColumnType.Number },
		{ "1234567890123456", ColumnType.Text },             // Excel: 1.23456789012345E+15
		{ "1234567890123456789", ColumnType.Text },          // Excel: 1.23456789012345E+18
		{ "3.14159265358979323846", ColumnType.Text },       // Excel: 3.14159265358979
		{ "1.50", ColumnType.Number },
		{ "1.0", ColumnType.Number },
		{ "1,234", ColumnType.Text },                        // Excel: 1234 in #,##0
		{ "1,234.5", ColumnType.Text },                      // Excel: 1234.5 in #,##0.00
		{ "1,5", ColumnType.Text },
		{ "$5", ColumnType.Text },                           // Excel: 5 as currency
		{ "50%", ColumnType.Text },                          // Excel: 0.5 in 0%
		{ " 5", ColumnType.Text },                           // Excel: 5
		{ "5 ", ColumnType.Text },                           // Excel: 5
		{ "-0", ColumnType.Number },
		{ "0x10", ColumnType.Text },
		{ "NaN", ColumnType.Text },
		{ "Infinity", ColumnType.Text },
		{ "2024-01-15", ColumnType.Date },
		{ "2024-01-15T10:30:00", ColumnType.DateTime },      // Excel: text
		{ "2024-01-15 10:30:00", ColumnType.DateTime },
		{ "2024-01-15 10:30", ColumnType.DateTime },
		{ "2024-01-15T10:30:00.123", ColumnType.DateTime },  // Excel: text
		{ "2024-01-15T10:30:00.1234567", ColumnType.DateTime }, // Excel: text
		{ "2024-01-15T10:30:00Z", ColumnType.Text },         // DateTimeOffsets is Text
		{ "2024-01-15T10:30:00+01:00", ColumnType.Text },    // DateTimeOffsets is Text
		{ "1/15/2024", ColumnType.Text },                    // Excel (en-US): a date
		{ "15/01/2024", ColumnType.Text },
		{ "2024/01/15", ColumnType.Text },                    // Excel: a date
		{ "15-Jan-2024", ColumnType.Text },                  // Excel: a date
		{ "Jan 15 2024", ColumnType.Text },
		{ "1900-02-29", ColumnType.Text },                   // Excel: serial 60
		{ "1899-12-31", ColumnType.Date },                   // a date, which OutOfRangeDates makes a text cell
		{ "0001-01-01", ColumnType.Date },
		{ "9999-12-31", ColumnType.Date },
		{ "2024-02-30", ColumnType.Text },
		{ "10:30", ColumnType.Text },                        // Excel: a time
		{ "10:30:45", ColumnType.Text },                     // Excel: a time
		{ "10:30:45.123", ColumnType.Text },                 // Excel: a time
		{ "36:00:00", ColumnType.Text },                     // Excel: a duration
		{ "TRUE", ColumnType.Bool },
		{ "true", ColumnType.Bool },
		{ "True", ColumnType.Bool },
		{ "FALSE", ColumnType.Bool },
		{ "yes", ColumnType.Text },
		{ "1", ColumnType.Number },
		{ "#N/A", ColumnType.Text },                         // Excel: an error
		{ "#DIV/0!", ColumnType.Text },                      // Excel: an error
		{ "=1+1", ColumnType.Text },                         // Excel: a formula
		{ "+1+1", ColumnType.Text },                         // Excel: a formula
		{ "@SUM(1)", ColumnType.Text },                      // Excel: a formula
	};

	/// <summary>Numbers near the grammar's edges, in the invariant culture, and whether they match.</summary>
	public static TheoryData<string, bool> Numbers => new()
	{
		{ "0", true },
		{ "0.0", true },
		{ "-0.0", true },
		{ "0E999999", true },
		{ "10000000000000000000", true },          // one significant digit
		{ "0.00000000000000000001", true },
		{ "123456789012345.5", true },             // 16 significant digits, its double's own text
		{ "9.007199254740993E15", false },         // 16, 2^53 + 1, not its double's own text (9007199254740992)
		{ "0.000123456789012345", true },          // 15
		{ "1.5000000000000000", true },            // zeros at the end don't count, as the sink counts them
		{ "9.99E307", true },
		{ "1E307", true },
		{ "1E308", false },                        // past Excel's numbers
		{ "1E-307", true },
		{ "1E-308", false },
		{ "-1E-308", false },
		{ "100E306", false },                      // 1E308
		{ "0.01E-305", true },                     // 1E-307
		{ "0.01E-306", false },                    // 1E-308
		{ "99.9E305", true },                      // 9.99E306
		{ "1E+5", true },
		{ "1E-5", true },
		{ "1e-5", true },
		{ "1E", false },
		{ "1E+", false },
		{ "1.E5", false },
		{ "1.5.5", false },
		{ "--1", false },
		{ "-", false },
		{ "", false },
		{ "01", false },
		{ "0.", false },
		{ "1 000", false },
		{ "١٢٣", false },                         // digits other than ASCII
	};

	[Theory]
	[MemberData(nameof(InferForms))]
	public void ReadsEachFormOfInferCsv(string field, object expected)
	{
		Assert.Equal((ColumnType)expected, ColumnTypes.Narrowest(Default.Matches(Bytes(field))));
	}

	[Theory]
	[MemberData(nameof(Numbers))]
	public void MatchesNumbersByTheGrammarAndExcelsRange(string field, bool expected)
	{
		Assert.Equal(expected, Default.Matches(Bytes(field)) == ColumnTypes.Bit(ColumnType.Number));
	}

	[Theory]
	[InlineData("123", "123")]
	[InlineData("-45", "-45")]
	[InlineData("1.50", "1.50")]
	[InlineData("1e5", "1E5")]
	[InlineData("1e-5", "1E-5")]
	[InlineData("1.5E+20", "1.5E+20")]
	[InlineData("-0", "-0")]
	public void WritesANumbersInvariantText(string field, string expected)
	{
		Assert.Equal(expected, Form(Default, field, ColumnType.Number));
	}

	[Theory]
	[InlineData("de-DE", "1,5", "1.5")]
	[InlineData("de-DE", "-1,5", "-1.5")]
	[InlineData("de-DE", "1,5E-3", "1.5E-3")]
	[InlineData("de-DE", "1234", "1234")]
	[InlineData("fr-FR", "1,5", "1.5")]
	[InlineData("fr-FR", "-0,25", "-0.25")]
	[InlineData("de-DE", "1.5", null)]
	[InlineData("de-DE", "1.234,5", null)]
	[InlineData("de-DE", "1,", null)]
	[InlineData("fr-FR", "1.5", null)]
	public void ReadsNumbersInTheCsvsCulture(string culture, string field, string? expected)
	{
		var types = new ColumnTypes(new CultureInfo(culture), ExcelWriterOptions.Default);

		Assert.Equal(expected, Form(types, field, ColumnType.Number));
		Assert.Equal(expected is null ? 0 : ColumnTypes.Bit(ColumnType.Number), types.Matches(Bytes(field)));
	}

	[Theory]
	[InlineData("−1,5", "-1.5")]
	[InlineData("1,5E−3", "1.5E-3")]
	[InlineData("1,5E⁺3", "1.5E+3")]
	[InlineData("-1,5", null)]
	public void ReadsSignsOfMoreThanOneByte(string field, string? expected)
	{
		// The minus sign (U+2212), which ICU gives some cultures, and a positive sign of the superscript plus.
		var format = new NumberFormatInfo { NegativeSign = "−", PositiveSign = "⁺", NumberDecimalSeparator = "," };
		var types = new ColumnTypes(format, ExcelWriterOptions.Default);

		Assert.Equal(expected, Form(types, field, ColumnType.Number));
	}

	[Theory]
	[InlineData(ExcelLongNumbers.Text, false)]
	[InlineData(ExcelLongNumbers.Number, true)]
	[InlineData(ExcelLongNumbers.Throw, true)]
	public void MatchesLongNumbersUnlessTheyAreText(ExcelLongNumbers longNumbers, bool matches)
	{
		var types = new ColumnTypes(CultureInfo.InvariantCulture, new ExcelWriterOptions { LongNumbers = longNumbers });

		// 15 digits always; 16 and 19 as the option says, so that the sink writes them as it says, as decimals.
		Assert.Equal(ColumnTypes.Bit(ColumnType.Number), types.Matches(Bytes("123456789012345")));
		Assert.Equal(matches, types.Matches(Bytes("1234567890123456")) != 0);
		Assert.Equal(matches, types.Matches(Bytes("-12345678901234567.89")) != 0);
		Assert.Equal(matches ? "1234567890123456789" : null, Form(types, "1234567890123456789", ColumnType.Number));
		if (matches)
		{
			Assert.Equal(typeof(decimal), CellTypeOf(types, "1234567890123456789"));
		}

		// A double's own text always, as a double, which the option doesn't apply to.
		Assert.Equal("4.505630514819934", Form(types, "4.505630514819934", ColumnType.Number));
		Assert.Equal(typeof(double), CellTypeOf(types, "4.505630514819934"));
		Assert.Equal(typeof(decimal), CellTypeOf(types, "4.50563051481993"));
	}

	/// <summary>
	/// Numbers of more than 15 significant digits with a fraction or an exponent, and whether each is its double's own
	/// text (the shortest that reads back as its double, as .NET's <c>"R"</c> gives it), which matches as a double.
	/// </summary>
	public static TheoryData<string, bool> DoubleTexts => new()
	{
		{ "4.505630514819934", true },               // 16 digits, as .NET writes the double
		{ "-4.505630514819934", true },
		{ "0.30000000000000004", true },             // 17: 0.1 + 0.2
		{ "0.30000000000000005", false },            // its double's text is 0.30000000000000004
		{ "1234567890123456.7", false },             // its double's text is 1234567890123456.8
		{ "12345678901234.567", false },             // 12345678901234.566
		{ "1.2345678901234568E+16", true },          // .NET writes 12345678901234568, the same digits
		{ "1.2345678901234568e16", true },
		{ "1234567890123456E0", true },              // an exponent makes it a double's text
		{ "4.5056305148199340", true },              // zeros at the end don't count
		{ "45056305148199.34E-13", true },           // 4.505630514819934, the same digits and magnitude
		{ "0.000000004505630514819934", true },
		{ "1.7976931348623157E+308", false },        // past Excel's numbers, whatever it is
		{ "0.1234567890123456789", false },          // 19 digits: no double's text has more than 17
		{ "1234567890123456", false },               // an integer's digits alone: an ID, not a double
	};

	[Theory]
	[MemberData(nameof(DoubleTexts))]
	public void MatchesADoublesOwnText(string field, bool matches)
	{
		Assert.Equal(matches, Default.Matches(Bytes(field)) == ColumnTypes.Bit(ColumnType.Number));
		if (matches)
		{
			Assert.Equal(typeof(double), CellTypeOf(Default, field));
		}
	}

	[Theory]
	[InlineData("de-DE", "4,505630514819934", "4.505630514819934")]
	[InlineData("de-DE", "-4,505630514819934E-3", "-4.505630514819934E-3")]
	[InlineData("fr-FR", "0,30000000000000004", "0.30000000000000004")]
	[InlineData("de-DE", "0,30000000000000005", null)]
	public void MatchesADoublesOwnTextInTheCsvsCulture(string culture, string field, string? expected)
	{
		var types = new ColumnTypes(new CultureInfo(culture), ExcelWriterOptions.Default);

		Assert.Equal(expected, Form(types, field, ColumnType.Number));
		if (expected is not null)
		{
			Assert.Equal(typeof(double), CellTypeOf(types, field));
		}
	}

	[Theory]
	[InlineData("2024-02-29", true)]
	[InlineData("2023-02-29", false)]
	[InlineData("1900-02-29", false)]
	[InlineData("2024-02-30", false)]
	[InlineData("2024-04-31", false)]
	[InlineData("2024-13-01", false)]
	[InlineData("2024-00-10", false)]
	[InlineData("2024-01-00", false)]
	[InlineData("0000-01-01", false)]
	[InlineData("2024-1-15", false)]
	[InlineData("24-01-15", false)]
	[InlineData("2024-01-15 ", false)]
	[InlineData("2024_01_15", false)]
	public void MatchesDaysThatExist(string field, bool matches)
	{
		Assert.Equal(matches ? ColumnTypes.Bit(ColumnType.Date) | ColumnTypes.Bit(ColumnType.DateTime) : 0, Default.Matches(Bytes(field)));
	}

	[Theory]
	[InlineData("2024-01-15T10:30", "2024-01-15T10:30:00.0000000")]
	[InlineData("2024-01-15 10:30", "2024-01-15T10:30:00.0000000")]
	[InlineData("2024-01-15T10:30:45", "2024-01-15T10:30:45.0000000")]
	[InlineData("2024-01-15T10:30:45.1", "2024-01-15T10:30:45.1000000")]
	[InlineData("2024-01-15T10:30:45.12", "2024-01-15T10:30:45.1200000")]
	[InlineData("2024-01-15T10:30:45.123", "2024-01-15T10:30:45.1230000")]
	[InlineData("2024-01-15T10:30:45.1234", "2024-01-15T10:30:45.1234000")]
	[InlineData("2024-01-15T10:30:45.12345", "2024-01-15T10:30:45.1234500")]
	[InlineData("2024-01-15T10:30:45.123456", "2024-01-15T10:30:45.1234560")]
	[InlineData("2024-01-15T10:30:45.1234567", "2024-01-15T10:30:45.1234567")]
	[InlineData("2024-01-15T00:00:00", "2024-01-15T00:00:00.0000000")]
	[InlineData("2024-01-15T23:59:59.9999999", "2024-01-15T23:59:59.9999999")]
	[InlineData("2024-01-15", "2024-01-15T00:00:00.0000000")]
	[InlineData("2024-01-15T10:30:45.12345678", null)]
	[InlineData("2024-01-15T10:30:45.", null)]
	[InlineData("2024-01-15T10:30.5", null)]
	[InlineData("2024-01-15T10:30:5", null)]
	[InlineData("2024-01-15T10:3", null)]
	[InlineData("2024-01-15T24:00", null)]
	[InlineData("2024-01-15T10:60", null)]
	[InlineData("2024-01-15T10:30:60", null)]
	[InlineData("2024-01-15t10:30", null)]
	[InlineData("2024-01-15  10:30", null)]
	[InlineData("2024-01-15T1030", null)]
	[InlineData("2024-01-15T10:30:00Z", null)]
	[InlineData("2024-02-30T10:30", null)]
	public void WritesADateAndTimesRoundTripText(string field, string? expected)
	{
		Assert.Equal(expected, Form(Default, field, ColumnType.DateTime));
	}

	[Theory]
	[InlineData("2024-01-15T10:30Z", "2024-01-15T10:30:00.0000000Z")]
	[InlineData("2024-01-15 10:30:45Z", "2024-01-15T10:30:45.0000000Z")]
	[InlineData("2024-01-15T10:30:00+01:00", "2024-01-15T10:30:00.0000000+01:00")]
	[InlineData("2024-01-15T10:30:00.5-05:30", "2024-01-15T10:30:00.5000000-05:30")]
	[InlineData("2024-01-15T10:30+14:00", "2024-01-15T10:30:00.0000000+14:00")]
	[InlineData("2024-01-15T10:30-14:00", "2024-01-15T10:30:00.0000000-14:00")]
	[InlineData("2024-01-15T10:30+00:00", "2024-01-15T10:30:00.0000000+00:00")]
	[InlineData("2024-01-15T10:30:45.12345678", null)]
	[InlineData("2024-01-15T10:30:45.12345678Z", null)]
	[InlineData("2024-01-15T10:30+14:01", null)]
	[InlineData("2024-01-15T10:30+15:00", null)]
	[InlineData("2024-01-15T10:30+01:60", null)]
	[InlineData("2024-01-15T10:30+0100", null)]
	[InlineData("2024-01-15T10:30+01", null)]
	[InlineData("2024-01-15T10:30z", null)]
	[InlineData("2024-01-15T10:30ZZ", null)]
	[InlineData("2024-01-15T10:30", null)]
	[InlineData("2024-01-15", null)]
	public void WritesAnOffsetsRoundTripText(string field, string? expected)
	{
		var types = new ColumnTypes(CultureInfo.InvariantCulture, new ExcelWriterOptions { DateTimeOffsets = ExcelDateTimeOffsets.Utc });

		Assert.Equal(expected, Form(types, field, ColumnType.DateTimeOffset));
		Assert.Equal(expected is null ? 0 : ColumnTypes.Bit(ColumnType.DateTimeOffset), types.Matches(Bytes(field)) & ColumnTypes.Bit(ColumnType.DateTimeOffset));
	}

	[Theory]
	[InlineData(ExcelDateTimeOffsets.Text, false)]
	[InlineData(ExcelDateTimeOffsets.Utc, true)]
	[InlineData(ExcelDateTimeOffsets.ClockTime, true)]
	public void MatchesOffsetsUnlessTheyAreText(ExcelDateTimeOffsets offsets, bool matches)
	{
		var types = new ColumnTypes(CultureInfo.InvariantCulture, new ExcelWriterOptions { DateTimeOffsets = offsets });

		Assert.Equal(matches ? ColumnTypes.Bit(ColumnType.DateTimeOffset) : 0, types.Matches(Bytes("2024-01-15T10:30:00+01:00")));
		Assert.Equal(matches ? ColumnTypes.Bit(ColumnType.DateTimeOffset) : 0, types.Matches(Bytes("2024-01-15T10:30:00Z")));
	}

	[Theory]
	[InlineData("true", "True")]
	[InlineData("TRUE", "True")]
	[InlineData("tRuE", "True")]
	[InlineData("false", "False")]
	[InlineData("FALSE", "False")]
	[InlineData("fAlSe", "False")]
	[InlineData("t", null)]
	[InlineData("tru", null)]
	[InlineData("truee", null)]
	[InlineData("f", null)]
	[InlineData("fals", null)]
	[InlineData("yes", null)]
	[InlineData("1", null)]
	[InlineData("true ", null)]
	[InlineData("trüe", null)]
	public void WritesABooleansText(string field, string? expected)
	{
		Assert.Equal(expected, Form(Default, field, ColumnType.Bool));
		Assert.Equal(expected is null ? 0 : ColumnTypes.Bit(ColumnType.Bool), Default.Matches(Bytes(field)) & ColumnTypes.Bit(ColumnType.Bool));
	}

	[Theory]
	[InlineData(new[] { "1", "2.5", "-3" }, ColumnType.Number)]
	[InlineData(new[] { "1", "00123" }, ColumnType.Text)]
	[InlineData(new[] { "1", "N/A" }, ColumnType.Text)]
	[InlineData(new[] { "2024-01-15", "2024-01-16" }, ColumnType.Date)]
	[InlineData(new[] { "2024-01-15", "2024-01-15 10:30" }, ColumnType.DateTime)]
	[InlineData(new[] { "2024-01-15 10:30", "2024-01-15" }, ColumnType.DateTime)]
	[InlineData(new[] { "2024-01-15", "45306" }, ColumnType.Text)]
	[InlineData(new[] { "true", "1" }, ColumnType.Text)]
	[InlineData(new[] { "true", "FALSE" }, ColumnType.Bool)]
	[InlineData(new[] { "2024-01-15 10:30", "2024-01-15T10:30Z" }, ColumnType.Text)]
	[InlineData(new[] { "2024-01-15T10:30Z", "2024-01-15T10:30+01:00" }, ColumnType.DateTimeOffset)]
	[InlineData(new[] { "2024-01-15", "2024-01-15T10:30Z" }, ColumnType.Text)]
	[InlineData(new[] { "abc" }, ColumnType.Text)]
	public void TypesAColumnByWhatAllItsFieldsMatch(string[] fields, object expected)
	{
		var types = new ColumnTypes(CultureInfo.InvariantCulture, new ExcelWriterOptions { DateTimeOffsets = ExcelDateTimeOffsets.Utc });
		var set = -1;
		foreach (var field in fields)
		{
			var matches = types.Matches(Bytes(field));
			set = set < 0 ? matches : set & matches;
		}

		Assert.Equal((ColumnType)expected, ColumnTypes.Narrowest(set));
	}

	[Theory]
	[InlineData(ColumnType.Number, "N/A")]
	[InlineData(ColumnType.Number, "2024-01-15")]
	[InlineData(ColumnType.Date, "2024-01-15 10:30")]
	[InlineData(ColumnType.Date, "45306")]
	[InlineData(ColumnType.DateTime, "2024-01-15T10:30Z")]
	[InlineData(ColumnType.DateTimeOffset, "2024-01-15")]
	[InlineData(ColumnType.DateTimeOffset, "2024-01-15T10:30")]
	[InlineData(ColumnType.Bool, "1")]
	public void LeavesAFieldThatDoesntMatchItsColumnAsText(object type, string field)
	{
		var types = new ColumnTypes(CultureInfo.InvariantCulture, new ExcelWriterOptions { DateTimeOffsets = ExcelDateTimeOffsets.Utc });

		Assert.Null(Form(types, field, (ColumnType)type));
	}

	[Theory]
	[InlineData(ExcelOutOfRangeDates.Text, ColumnType.Date, "1899-12-31", null)]
	[InlineData(ExcelOutOfRangeDates.Throw, ColumnType.Date, "1899-12-31", "1899-12-31")]
	[InlineData(ExcelOutOfRangeDates.Text, ColumnType.Date, "1900-01-01", "1900-01-01")]
	[InlineData(ExcelOutOfRangeDates.Text, ColumnType.DateTime, "1899-12-31 23:59", null)]
	[InlineData(ExcelOutOfRangeDates.Text, ColumnType.DateTime, "1899-12-31", null)]
	[InlineData(ExcelOutOfRangeDates.Throw, ColumnType.DateTime, "1899-12-31 23:59", "1899-12-31T23:59:00.0000000")]
	[InlineData(ExcelOutOfRangeDates.Text, ColumnType.DateTime, "1900-01-01 00:00", "1900-01-01T00:00:00.0000000")]
	public void LeavesADateBefore1900AsItsTextWhenItsCellIsText(ExcelOutOfRangeDates outOfRange, object type, string field, string? expected)
	{
		var types = new ColumnTypes(CultureInfo.InvariantCulture, new ExcelWriterOptions { OutOfRangeDates = outOfRange });

		Assert.Equal(expected, Form(types, field, (ColumnType)type));
	}

	[Theory]
	[InlineData(ExcelDateTimeOffsets.Utc, "1900-01-01T00:30+01:00", null)]
	[InlineData(ExcelDateTimeOffsets.ClockTime, "1900-01-01T00:30+01:00", "1900-01-01T00:30:00.0000000+01:00")]
	[InlineData(ExcelDateTimeOffsets.Utc, "1899-12-31T23:30-01:00", "1899-12-31T23:30:00.0000000-01:00")]
	[InlineData(ExcelDateTimeOffsets.ClockTime, "1899-12-31T23:30-01:00", null)]
	public void TakesAnOffsetsDateAsItsCellHoldsIt(ExcelDateTimeOffsets offsets, string field, string? expected)
	{
		var types = new ColumnTypes(CultureInfo.InvariantCulture, new ExcelWriterOptions { DateTimeOffsets = offsets });

		Assert.Equal(expected, Form(types, field, ColumnType.DateTimeOffset));
	}

	[Fact]
	public void HandsEachTypeAsTheTypeTheSinkWritesItFrom()
	{
		Assert.Equal(typeof(decimal), ColumnTypes.CellType(ColumnType.Number));
		Assert.Equal(CellKind.DateOnly, CellKinds.Of(ColumnTypes.CellType(ColumnType.Date)));
		Assert.Equal(typeof(DateTime), ColumnTypes.CellType(ColumnType.DateTime));
		Assert.Equal(typeof(DateTimeOffset), ColumnTypes.CellType(ColumnType.DateTimeOffset));
		Assert.Equal(typeof(bool), ColumnTypes.CellType(ColumnType.Bool));
		Assert.Equal(typeof(string), ColumnTypes.CellType(ColumnType.Text));
	}

	/// <summary>A field's UTF-8.</summary>
	private static byte[] Bytes(string field) => Encoding.UTF8.GetBytes(field);

	/// <summary>A field's form for a column of a type, or <see langword="null"/> when it is text.</summary>
	private static string? Form(ColumnTypes types, string field, ColumnType type)
	{
		var destination = new byte[ColumnTypes.MaxFormLength];
		var length = types.Format(Bytes(field), type, destination, out _);

		return length < 0 ? null : Encoding.UTF8.GetString(destination, 0, length);
	}

	/// <summary>The type a number column's field is handed to the sink as, or <see langword="null"/> when it is text.</summary>
	private static Type? CellTypeOf(ColumnTypes types, string field)
	{
		var destination = new byte[ColumnTypes.MaxFormLength];
		var length = types.Format(Bytes(field), ColumnType.Number, destination, out var cellType);

		return length < 0 ? null : cellType;
	}
}
