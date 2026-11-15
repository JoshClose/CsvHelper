using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Excel.Tests.Writing;

[assembly: CsvConverter(typeof(MoneyConverter))]

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>A ticket's status: a name of another text, and an alias of another member's value.</summary>
public enum TicketStatus
{
	/// <summary>Open.</summary>
	Open,

	/// <summary>On hold, written by its first other name.</summary>
	[CsvEnumName("On hold", "Held")]
	OnHold,

	/// <summary>Closed.</summary>
	Closed,

	/// <summary>Closed's value again: written as Closed.</summary>
	Done = Closed,
}

/// <summary>Flags, whose values combine: no dropdown.</summary>
[Flags]
public enum TicketAccess
{
	/// <summary>None.</summary>
	None = 0,

	/// <summary>Read.</summary>
	Read = 1,

	/// <summary>Write.</summary>
	Write = 2,
}

/// <summary>An enum whose names, joined by commas, have more than 255 characters: the lists sheet.</summary>
public enum TicketRegion
{
	/// <summary>A region.</summary>
	Region01,

	/// <summary>A region.</summary>
	Region02,

	/// <summary>A region.</summary>
	Region03,

	/// <summary>A region.</summary>
	Region04,

	/// <summary>A region.</summary>
	Region05,

	/// <summary>A region.</summary>
	Region06,

	/// <summary>A region.</summary>
	Region07,

	/// <summary>A region.</summary>
	Region08,

	/// <summary>A region.</summary>
	Region09,

	/// <summary>A region.</summary>
	Region10,

	/// <summary>A region.</summary>
	Region11,

	/// <summary>A region.</summary>
	Region12,

	/// <summary>A region.</summary>
	Region13,

	/// <summary>A region.</summary>
	Region14,

	/// <summary>A region.</summary>
	Region15,

	/// <summary>A region.</summary>
	Region16,

	/// <summary>A region.</summary>
	Region17,

	/// <summary>A region.</summary>
	Region18,

	/// <summary>A region.</summary>
	Region19,

	/// <summary>A region.</summary>
	Region20,

	/// <summary>A region.</summary>
	Region21,

	/// <summary>A region.</summary>
	Region22,

	/// <summary>A region.</summary>
	Region23,

	/// <summary>A region.</summary>
	Region24,

	/// <summary>A region.</summary>
	Region25,

	/// <summary>A region.</summary>
	Region26,

	/// <summary>A region.</summary>
	Region27,

	/// <summary>A region.</summary>
	Region28,

	/// <summary>A region.</summary>
	Region29,

	/// <summary>A region.</summary>
	Region30,
}

/// <summary>An amount that the assembly's converter reads and writes, as text of its own.</summary>
/// <param name="Cents">The amount, in cents.</param>
public readonly record struct Money(long Cents);

/// <summary>The assembly's converter of <see cref="Money"/>: <c>12.34 EUR</c>.</summary>
public sealed class MoneyConverter : ICsvConverter<Money>
{
	/// <summary>Reads an amount.</summary>
	/// <param name="reader">The reader.</param>
	/// <param name="index">The field.</param>
	/// <returns>The amount.</returns>
	public Money Read(CsvReader reader, int index) => new((long)(decimal.Parse(reader.GetString(index).Replace(" EUR", ""), CultureInfo.InvariantCulture) * 100));

	/// <summary>Writes an amount.</summary>
	/// <param name="writer">The writer.</param>
	/// <param name="value">The amount.</param>
	public void Write(CsvWriter writer, Money value) => writer.WriteField((value.Cents / 100m).ToString("0.00", CultureInfo.InvariantCulture) + " EUR");
}

/// <summary>A converter that writes text in capitals.</summary>
public sealed class CapitalsConverter : ICsvConverter<string>
{
	/// <summary>Reads text.</summary>
	/// <param name="reader">The reader.</param>
	/// <param name="index">The field.</param>
	/// <returns>The text.</returns>
	public string Read(CsvReader reader, int index) => reader.GetString(index);

	/// <summary>Writes text in capitals.</summary>
	/// <param name="writer">The writer.</param>
	/// <param name="value">The text.</param>
	public void Write(CsvWriter writer, string value) => writer.WriteField(value.ToUpperInvariant());
}

/// <summary>A member of every type of the design's table, by its header name.</summary>
[CsvRecord]
public partial class ValidatedTypes
{
	/// <summary>A dropdown of booleans.</summary>
	public bool Paid { get; set; }

	/// <summary>A dropdown of its first values, trimmed.</summary>
	[CsvBooleanValues(new[] { " Yes ", "Y" }, new[] { "No", "N" })]
	public bool Shipped { get; set; }

	/// <summary>A dropdown of its names as written.</summary>
	public TicketStatus Status { get; set; }

	/// <summary>Flags: none.</summary>
	public TicketAccess Access { get; set; }

	/// <summary>A dropdown in the lists sheet.</summary>
	public TicketRegion Region { get; set; }

	/// <summary>Whole numbers.</summary>
	public sbyte SByte { get; set; }

	/// <summary>Whole numbers.</summary>
	public byte Byte { get; set; }

	/// <summary>Whole numbers.</summary>
	public short Short { get; set; }

	/// <summary>Whole numbers.</summary>
	public ushort UShort { get; set; }

	/// <summary>Whole numbers.</summary>
	public int Int { get; set; }

	/// <summary>Whole numbers.</summary>
	public uint UInt { get; set; }

	/// <summary>Whole numbers of 15 digits.</summary>
	public long Long { get; set; }

	/// <summary>Whole numbers of 15 digits.</summary>
	public ulong ULong { get; set; }

	/// <summary>Numbers.</summary>
	public float Float { get; set; }

	/// <summary>Numbers.</summary>
	public double Double { get; set; }

	/// <summary>Numbers.</summary>
	public decimal Decimal { get; set; }

	/// <summary>A date and time.</summary>
	public DateTime When { get; set; }

	/// <summary>A duration.</summary>
	public TimeSpan Took { get; set; }

	/// <summary>Text, as DateTimeOffsets is by default.</summary>
	public DateTimeOffset Stamp { get; set; }

	/// <summary>One character.</summary>
	public char Grade { get; set; }

	/// <summary>Text.</summary>
	public string Name { get; set; } = "";

	/// <summary>Text.</summary>
	public Guid Key { get; set; }

	/// <summary>Text.</summary>
	public Uri? Site { get; set; }

	/// <summary>Whole numbers, or none.</summary>
	public int? Maybe { get; set; }

	/// <summary>A date and time, or none.</summary>
	public DateTime? Someday { get; set; }

	/// <summary>A format Phase 4 translates.</summary>
	[CsvFormat("dd.MM.yyyy")]
	public DateTime Dotted { get; set; }

	/// <summary>A format without a date: a time's validation.</summary>
	[CsvFormat("HH:mm")]
	public DateTime Clock { get; set; }

	/// <summary>.NET's default format, as without one.</summary>
	[CsvFormat("O")]
	public DateTime Roundtrip { get; set; }

	/// <summary>A format with an offset, which Phase 4 writes as text.</summary>
	[CsvFormat("yyyy-MM-ddTHH:mm:sszzz")]
	public DateTime Zoned { get; set; }

	/// <summary>A duration's format, which Phase 4 writes as text.</summary>
	[CsvFormat(@"hh\:mm")]
	public TimeSpan Brief { get; set; }

	/// <summary>A converter's: none.</summary>
	[CsvConverter(typeof(CapitalsConverter))]
	public string Capitals { get; set; } = "";

	/// <summary>The assembly's converter's: none.</summary>
	public Money Cost { get; set; }

	/// <summary>A converter's, with a prompt: the prompt alone.</summary>
	[CsvConverter(typeof(CapitalsConverter))]
	[ExcelColumn(Prompt = "In capitals.")]
	public string Shouted { get; set; } = "";

	/// <summary>A column's list in place of its enum's.</summary>
	[ExcelColumn(List = new[] { "Open", "Closed" })]
	public TicketStatus Stage { get; set; }

	/// <summary>A column's format and prompt beside its type's validation.</summary>
	[ExcelColumn(Format = "0.00", Prompt = "Two decimals.")]
	public decimal Price { get; set; }

	/// <summary>A column's list in place of its type's validation.</summary>
	[ExcelColumn(List = new[] { "1", "2", "3" })]
	public int Rating { get; set; }

	/// <summary>A formula: neither.</summary>
	public ExcelFormula Total { get; set; }

#if NET
	/// <summary>Numbers.</summary>
	public Half Half { get; set; }

	/// <summary>Whole numbers of 15 digits.</summary>
	public Int128 Huge { get; set; }

	/// <summary>Whole numbers of 15 digits.</summary>
	public UInt128 UHuge { get; set; }

	/// <summary>A date.</summary>
	public DateOnly Day { get; set; }

	/// <summary>A time of day.</summary>
	public TimeOnly At { get; set; }
#endif
}

/// <summary>Offsets, plain and in a format, for <see cref="ExcelWriterOptions.DateTimeOffsets"/>.</summary>
[CsvRecord]
public partial class ValidatedOffsets
{
	/// <summary>An offset.</summary>
	public DateTimeOffset Stamp { get; set; }

	/// <summary>An offset in a format that Phase 4 translates.</summary>
	[CsvFormat("dd.MM.yyyy HH:mm")]
	public DateTimeOffset Local { get; set; }
}

/// <summary>A record with a nested record and a collection, whose members validate under their prefixed names.</summary>
[CsvRecord]
public partial class ValidatedNesting
{
	/// <summary>Whole numbers.</summary>
	public int Id { get; set; }

	/// <summary>A nested record, after its prefix.</summary>
	[CsvColumn(Prefix = "Ship ")]
	public ValidatedAddress Ship { get; set; } = new();
}

/// <summary>A nested record.</summary>
[CsvRecord]
public partial class ValidatedAddress
{
	/// <summary>A date.</summary>
	public DateTime Since { get; set; }

	/// <summary>Text.</summary>
	public string Street { get; set; } = "";
}

/// <summary>A collection's columns, by index: no type, so neither a validation nor a format.</summary>
[CsvRecord]
public partial class ValidatedCollection
{
	/// <summary>Three columns of numbers.</summary>
	[CsvColumn("Score", Index = 0, EndIndex = 2)]
	public int[] Scores { get; set; } = [];

	/// <summary>Whole numbers.</summary>
	[CsvColumn(Index = 3)]
	public int Count { get; set; }
}

/// <summary>
/// A column's validation (Phase 7's step 3): the design's table of what each member's type gives its column with
/// <see cref="ExcelSheetOptions.Validate"/>, lists inline and in the lists sheet, prompts and their titles, and the XML.
/// </summary>
public class ColumnValidationTests
{
	/// <summary>The validation of Excel's dates, 1900-01-01 to 9999-12-31 23:59:59.999.</summary>
	private const string Dates = "date 1 2958465.9999999884";

	/// <summary>The validation of a time of day, 00:00 to 23:59:59.999.</summary>
	private const string Times = "time 0 0.999999988425926";

	/// <summary>Sync and async.</summary>
	public static TheoryData<bool> Modes => new() { false, true };

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ValidatesEachTypeAsTheDesignsTableSays(bool async)
	{
		var workbook = await Template<ValidatedTypes>(async);

		var columns = SheetValidations.Describe(workbook);
		var expected = new Dictionary<string, string>
		{
			["Paid"] = "list \"TRUE,FALSE\" | General",
			["Shipped"] = "list \"Yes,No\" | @",
			["Status"] = "list \"Open,On hold,Closed\" | @",
			["Access"] = "none | @",
			["Region"] = "list _CsvHelperList1 | @",
			["SByte"] = "whole -128 127 | General",
			["Byte"] = "whole 0 255 | General",
			["Short"] = "whole -32768 32767 | General",
			["UShort"] = "whole 0 65535 | General",
			["Int"] = "whole -2147483648 2147483647 | General",
			["UInt"] = "whole 0 4294967295 | General",
			["Long"] = "whole -999999999999999 999999999999999 | General",
			["ULong"] = "whole 0 999999999999999 | General",
			["Float"] = "decimal -3.40282346638529E+38 3.40282346638529E+38 | General",
			["Double"] = "decimal -9.99999999999999E+307 9.99999999999999E+307 | General",
			["Decimal"] = "decimal -7.92281625142643E+28 7.92281625142643E+28 | General",
			["When"] = Dates + " | yyyy-mm-dd hh:mm:ss",
			["Took"] = "time 0 2958465.9999999884 | [h]:mm:ss",
			["Stamp"] = "none | @",
			["Grade"] = "textLength equal 1 | @",
			["Name"] = "none | @",
			["Key"] = "none | @",
			["Site"] = "none | @",
			["Maybe"] = "whole -2147483648 2147483647 | General",
			["Someday"] = Dates + " | yyyy-mm-dd hh:mm:ss",
			["Dotted"] = Dates + " | dd\".\"mm\".\"yyyy",
			["Clock"] = Times + " | hh\":\"mm",
			["Roundtrip"] = Dates + " | yyyy-mm-dd hh:mm:ss",
			["Zoned"] = "none | @",
			["Brief"] = "none | @",
			["Capitals"] = "none | General",
			["Cost"] = "none | General",
			["Shouted"] = "any title=Shouted prompt=In capitals. | General",
			["Stage"] = "list \"Open,Closed\" | @",
			["Price"] = "decimal -7.92281625142643E+28 7.92281625142643E+28 title=Price prompt=Two decimals. | 0.00",
			["Rating"] = "list \"1,2,3\" | General",
			["Total"] = "none | General",
#if NET
			["Half"] = "decimal -65504 65504 | General",
			["Huge"] = "whole -999999999999999 999999999999999 | General",
			["UHuge"] = "whole 0 999999999999999 | General",
			["Day"] = Dates + " | yyyy-mm-dd",
			["At"] = Times + " | hh:mm:ss",
#endif
		};
		Assert.Equal(expected.OrderBy(e => e.Key, StringComparer.Ordinal), columns.OrderBy(e => e.Key, StringComparer.Ordinal));

		// The enum of 30 names (269 characters) is the lists sheet's first column, very hidden, under its hidden name.
		var workbookXml = Written.Entry(workbook, "xl/workbook.xml");
		Assert.Contains("<sheet name=\"Data\" sheetId=\"1\" r:id=\"rId1\"/><sheet name=\"Lists\" sheetId=\"2\" state=\"veryHidden\" r:id=\"rId2\"/></sheets>", workbookXml);
		Assert.Contains("<definedNames><definedName name=\"_CsvHelperList1\" hidden=\"1\">Lists!$A$1:$A$30</definedName></definedNames>", workbookXml);
		Assert.Equal([.. Enum.GetNames(typeof(TicketRegion))], Written.Read(workbook).Single(s => s.Sheet == "Lists").Rows);
		Assert.Empty(Written.ValidationErrors(workbook));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ValidatesOffsetsAsDatesUnlessTheyAreText(bool async)
	{
		var clock = await Template<ValidatedOffsets>(async, new ExcelWriterOptions { DateTimeOffsets = ExcelDateTimeOffsets.ClockTime });
		var utc = await Template<ValidatedOffsets>(async, new ExcelWriterOptions { DateTimeOffsets = ExcelDateTimeOffsets.Utc });
		var text = await Template<ValidatedOffsets>(async);

		// A format's text is a date cell only as a clock time; in UTC, the cells are text.
		Assert.Equal(new Dictionary<string, string> { ["Stamp"] = Dates + " | yyyy-mm-dd hh:mm:ss", ["Local"] = Dates + " | dd\".\"mm\".\"yyyy\" \"hh\":\"mm" }, SheetValidations.Describe(clock));
		Assert.Equal(new Dictionary<string, string> { ["Stamp"] = Dates + " | yyyy-mm-dd hh:mm:ss", ["Local"] = "none | @" }, SheetValidations.Describe(utc));
		Assert.Equal(new Dictionary<string, string> { ["Stamp"] = "none | @", ["Local"] = "none | @" }, SheetValidations.Describe(text));
	}

	[Theory]
	[MemberData(nameof(Modes))]
	public async Task ValidatesNestedMembersAndNotCollections(bool async)
	{
		var nesting = await Template<ValidatedNesting>(async);
		var collection = await Template<ValidatedCollection>(async);

		Assert.Equal(new Dictionary<string, string>
		{
			["Id"] = "whole -2147483648 2147483647 | General",
			["Ship Since"] = Dates + " | yyyy-mm-dd hh:mm:ss",
			["Ship Street"] = "none | @",
		}, SheetValidations.Describe(nesting));
		// The collection's columns A to C have neither; its count, in D, has its type's.
		Assert.Equal(new Dictionary<string, string> { ["D"] = "whole -2147483648 2147483647" }, SheetValidations.Of(collection));
		Assert.Empty(SheetValidations.Formats(collection));
	}

	[Fact]
	public void ReadsEachMembersTypeAndWrittenValues()
	{
		var members = MemberFormats.For<ValidatedTypes>()!;
		var typed = members.Typed(["Status", "Shipped", "Access", "Capitals", "Cost", "Price", "Unknown", "Shouted"]);

		Assert.Equal(typeof(TicketStatus), typed[0]!.Type);
		Assert.Equal(["Open", "On hold", "Closed"], typed[0]!.Values!);
		Assert.Equal(["Yes", "No"], typed[1]!.Values!);
		Assert.Null(typed[2]!.Values);
		// A converter's member, its own or the assembly's, has no type.
		Assert.Null(typed[3]);
		Assert.Null(typed[4]);
		Assert.Equal(typeof(decimal), typed[5]!.Type);
		Assert.Null(typed[6]);
		// A converter's member with Excel options has an entry for them, and no type.
		Assert.Null(typed[7]);
		Assert.Contains(members.Entries, e => e.Name == "Shouted" && e.Excel is { Prompt: "In capitals." } && e.Type is null);
		Assert.True(members.HasTypes);
		Assert.Null(MemberFormats.For<ValidatedCollection>()!.Typed(["Score", "Score", "Score", "Count"])[0]);
	}

	[Theory]
	[InlineData(1, true)]
	[InlineData(255, true)]
	[InlineData(256, false)]
	public void PutsListsOfAtMost255CharactersInline(int length, bool inline)
	{
		// Values of 7 characters and commas: the last value makes up the length.
		var values = new List<string>();
		var left = length;
		while (left > 8)
		{
			values.Add("abcdefg");
			left -= 8;
		}

		values.Add(new string('z', left));

		Assert.Equal(length, string.Join(",", values).Length);
		Assert.Equal(inline, ColumnValidation.IsInline(values));
	}

	[Theory]
	[InlineData("a\"\"b", true)]
	[InlineData("a, b", false)]
	[InlineData(" a", false)]
	[InlineData("a ", false)]
	[InlineData("a\t", false)]
	[InlineData("\na", false)]
	[InlineData("a ", false)]
	[InlineData("a b", true)]
	[InlineData("a\nb", true)]
	[InlineData("_x0041_", true)]
	public void PutsListsWithACommaOrSpaceAtAnEndInTheListsSheet(string value, bool inline) =>
		Assert.Equal(inline, ColumnValidation.IsInline([value, "c"]));

	[Fact]
	public void CountsAQuoteOnceAsExcelHoldsIt()
	{
		// 245 characters and ten quotes: 255 as Excel holds them, 265 written with each quote doubled.
		var value = new string('"', 10) + new string('a', 245);

		var validation = Create(new ExcelColumnOptions { List = [value] });

		Assert.Null(validation.ListName);
		Assert.Equal("<formula1>\"" + new string('"', 20) + new string('a', 245) + "\"</formula1>", Formula1(validation));
	}

	[Fact]
	public void ListsAValueOnceInOrder()
	{
		var validation = Create(new ExcelColumnOptions { List = ["b", "a", "b", "B", "a"] });

		Assert.Equal(["b", "a", "B"], validation.Values!);
		Assert.Equal("<formula1>\"b,a,B\"</formula1>", Formula1(validation));
	}

	[Fact]
	public void TitlesAPromptWithTheHeadersNameCutTo32()
	{
		var name = new string('n', 31) + "\U0001F600" + "rest";

		Assert.Equal(new string('n', 31), Create(new ExcelColumnOptions { Prompt = "p" }, name).Title);
		Assert.Equal(new string('n', 32), Create(new ExcelColumnOptions { Prompt = "p" }, new string('n', 40)).Title);
		Assert.Equal("Status", Create(new ExcelColumnOptions { Prompt = "p" }, "Status").Title);
		// No title without a prompt, a header or a header's name.
		Assert.Null(Create(new ExcelColumnOptions { List = ["a"] }, "Status").Title);
		Assert.Null(Create(new ExcelColumnOptions { Prompt = "p" }, null).Title);
		Assert.Null(Create(new ExcelColumnOptions { Prompt = "p" }, "").Title);
	}

	[Fact]
	public void IsNothingWithoutAValidationOrAPrompt()
	{
		var lists = new ValidationLists(new TableNames());

		Assert.Null(ColumnValidation.Create(null, null, "Name", lists));
		Assert.Null(ColumnValidation.Create(null, new ExcelColumnOptions { Width = 10 }, "Name", lists));
		Assert.Equal(ValidationKind.Any, ColumnValidation.Create(null, new ExcelColumnOptions { Prompt = "p" }, "Name", lists)!.Kind);
		Assert.Equal(0, lists.Count);
	}

	[Fact]
	public void WritesEachKindAsExcelDoes()
	{
		var lists = new ValidationLists(new TableNames());
		var whole = TypeValidation.Of(new MemberFormat("Count", 0, -1, null, null, Type: typeof(byte)), null, ExcelDateTimeOffsets.Text);
		var grade = TypeValidation.Of(new MemberFormat("Grade", 0, -1, null, null, Type: typeof(char)), null, ExcelDateTimeOffsets.Text);

		Assert.Equal(
			"<dataValidation type=\"whole\" allowBlank=\"1\" showInputMessage=\"1\" showErrorMessage=\"1\" sqref=\"C2:C1048576\"><formula1>0</formula1><formula2>255</formula2></dataValidation>",
			Xml(ColumnValidation.Create(whole, null, "Count", lists)!, 2, 2));
		Assert.Equal(
			"<dataValidation type=\"textLength\" operator=\"equal\" allowBlank=\"1\" showInputMessage=\"1\" showErrorMessage=\"1\" promptTitle=\"Grade\" prompt=\"A to F.\" sqref=\"A3:A1048576\"><formula1>1</formula1></dataValidation>",
			Xml(ColumnValidation.Create(grade, new ExcelColumnOptions { Prompt = "A to F." }, "Grade", lists)!, 0, 3));
		// A prompt alone; its line breaks, tabs and quotes escaped as Excel reads them in an attribute, and _x0041_ kept.
		Assert.Equal(
			"<dataValidation allowBlank=\"1\" showInputMessage=\"1\" showErrorMessage=\"1\" promptTitle=\"&lt;Notes&gt;\" prompt=\"One_x000A_&quot;two&quot;_x0009__x005F_x0041_ &amp;\" sqref=\"XFD1:XFD1048576\"/>",
			Xml(ColumnValidation.Create(null, new ExcelColumnOptions { Prompt = "One\n\"two\"\t_x0041_ &" }, "<Notes>", lists)!, 16383, 1));
		// A list inline, in element text: its line breaks as they are, its escapes' form escaped; and one by name.
		Assert.Equal(
			"<dataValidation type=\"list\" allowBlank=\"1\" showInputMessage=\"1\" showErrorMessage=\"1\" sqref=\"B2:B1048576\"><formula1>\"a\nb,_x005F_x0041_,&lt;c&gt;\"</formula1></dataValidation>",
			Xml(ColumnValidation.Create(null, new ExcelColumnOptions { List = ["a\nb", "_x0041_", "<c>"] }, "List", lists)!, 1, 2));
		Assert.Equal(
			"<dataValidation type=\"list\" allowBlank=\"1\" showInputMessage=\"1\" showErrorMessage=\"1\" sqref=\"B2:B1048576\"><formula1>_CsvHelperList1</formula1></dataValidation>",
			Xml(ColumnValidation.Create(null, new ExcelColumnOptions { List = ["a, b"] }, "List", lists)!, 1, 2));
	}

	[Fact]
	public void GivesTypesListsWithAValueExcelCantMatchNone()
	{
		var tooLong = new MemberFormat("Status", 0, -1, null, null, Type: typeof(TicketStatus), Values: ["Open", new string('x', 256)]);
		var empty = new MemberFormat("Shipped", 0, -1, null, null, Type: typeof(bool), Values: ["", "No"]);

		Assert.Equal((ValidationKind.Any, "@"), Kind(TypeValidation.Of(tooLong, null, ExcelDateTimeOffsets.Text)));
		Assert.Equal((ValidationKind.Any, "@"), Kind(TypeValidation.Of(empty, null, ExcelDateTimeOffsets.Text)));
	}

	[Fact]
	public void NamesEachDistinctListOnceAndSkipsTablesNames()
	{
		var tables = new TableNames();
		tables.Add("_CsvHelperList1");
		var lists = new ValidationLists(tables);

		var first = lists.NameOf(["a, b", "c"]);
		var again = lists.NameOf(["a, b", "c"]);
		var other = lists.NameOf(["c", "a, b"]);

		Assert.Equal(("_CsvHelperList2", "_CsvHelperList2", "_CsvHelperList3"), (first, again, other));
		Assert.Equal(2, lists.Count);
		// The names are the workbook's, so a table can't take them.
		Assert.Equal("_CsvHelperList3", tables.Existing("_csvhelperlist3"));
		Assert.True(lists.IsList("_CsvHelperList2"));
		Assert.False(lists.IsList("_CsvHelperList1"));
		using var xml = new XmlOutput(256);
		lists.AppendNames(xml, "My Lists");
		Assert.Equal(
			"<definedName name=\"_CsvHelperList2\" hidden=\"1\">'My Lists'!$A$1:$A$2</definedName><definedName name=\"_CsvHelperList3\" hidden=\"1\">'My Lists'!$B$1:$B$2</definedName>",
			Encoding.UTF8.GetString(xml.Written.ToArray()));
	}

	[Fact]
	public void HoldsAListInEachOfExcelsColumns()
	{
		var lists = new ValidationLists(new TableNames());
		for (var i = 0; i < 16384; i++)
		{
			lists.NameOf([i.ToString(CultureInfo.InvariantCulture)]);
		}

		Assert.Equal("_CsvHelperList1", lists.NameOf(["0"]));
		var e = Assert.Throws<InvalidOperationException>(() => lists.NameOf(["16384"]));
		Assert.Equal("The workbook's dropdown lists that don't fit in a validation go in its lists sheet, a list a column, and it has 16,384, Excel's columns: write fewer distinct long lists.", e.Message);
	}

	/// <summary>Writes a header-only sheet of a type's records with <see cref="ExcelSheetOptions.Validate"/>.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="async">Whether to write asynchronously.</param>
	/// <param name="options">The workbook's options, or <see langword="null"/>.</param>
	/// <returns>The workbook.</returns>
	internal static Task<byte[]> Template<[DynamicallyAccessedMembers(MemberFormats.Members)] T>(bool async, ExcelWriterOptions? options = null) =>
		Written.WorkbookAsync(async (book, a) =>
		{
			if (a)
			{
				await book.WriteSheetAsync<T>("Data", [], new ExcelSheetOptions { Validate = true });
			}
			else
			{
				book.WriteSheet<T>("Data", [], new ExcelSheetOptions { Validate = true });
			}
		}, async, options);

	/// <summary>A column's validation from its options alone.</summary>
	/// <param name="options">The options.</param>
	/// <param name="header">The header's name.</param>
	/// <returns>The validation.</returns>
	private static ColumnValidation Create(ExcelColumnOptions options, string? header = "Column") =>
		ColumnValidation.Create(null, options, header, new ValidationLists(new TableNames()))!;

	/// <summary>A validation's XML.</summary>
	/// <param name="validation">The validation.</param>
	/// <param name="column">Its column.</param>
	/// <param name="firstRow">Its first row.</param>
	/// <returns>The XML.</returns>
	private static string Xml(ColumnValidation validation, int column, long firstRow)
	{
		using var xml = new XmlOutput(256);
		validation.Write(xml, column, firstRow);

		return Encoding.UTF8.GetString(xml.Written.ToArray());
	}

	/// <summary>A validation's <c>formula1</c>, in column A from row 2.</summary>
	/// <param name="validation">The validation.</param>
	/// <returns>The element.</returns>
	private static string Formula1(ColumnValidation validation)
	{
		var xml = Xml(validation, 0, 2);
		var start = xml.IndexOf("<formula1>", StringComparison.Ordinal);

		return xml.Substring(start, xml.IndexOf("</formula1>", StringComparison.Ordinal) + "</formula1>".Length - start);
	}

	/// <summary>A type's validation's kind and format.</summary>
	/// <param name="type">The type's validation.</param>
	/// <returns>Its kind and format.</returns>
	private static (ValidationKind, string?) Kind(TypeValidation? type) => (type!.Kind, type.Format);
}
