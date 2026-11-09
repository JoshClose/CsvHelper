using System.Globalization;

namespace CsvHelper.Excel.Tests.Writing;

/// <summary>Members with formats under their own names, other names, and none.</summary>
[CsvRecord]
public partial class NamedFormats
{
	/// <summary>A format under the member's name.</summary>
	[CsvFormat("yyyy-MM-dd")]
	public DateTime Plain { get; set; }

	/// <summary>A format under another name.</summary>
	[CsvColumn("Renamed")]
	[CsvFormat("dd.MM.yyyy")]
	public DateTime Other { get; set; }

	/// <summary>A culture.</summary>
	[CsvCulture("de-DE")]
	public decimal Amount { get; set; }

	/// <summary>Neither.</summary>
	public DateTime NoFormat { get; set; }

	/// <summary>Ignored: no column.</summary>
	[CsvIgnore]
	[CsvFormat("yyyy")]
	public DateTime Ignored { get; set; }

	/// <summary>Internal, which the generator reads.</summary>
	[CsvFormat("HH:mm")]
	internal DateTime Internal { get; set; }

	/// <summary>A field.</summary>
	[CsvFormat("yyyy/MM")]
	public DateTime Field;

	/// <summary>Private, which the generator reads only with a <see cref="CsvColumnAttribute"/>.</summary>
	[CsvColumn]
	[CsvFormat("MM/yyyy")]
	private DateTime Private { get; set; }

	/// <summary>Private without a <see cref="CsvColumnAttribute"/>: not read.</summary>
	[CsvFormat("yyyy")]
	private DateTime Hidden { get; set; }

	/// <summary>A private field without a <see cref="CsvColumnAttribute"/>: not read.</summary>
	[CsvFormat("ss")]
	private DateTime hiddenField;

	/// <summary>Read only: not read.</summary>
	[CsvFormat("yyyy")]
	public DateTime ReadOnly => Hidden;

	/// <summary>A private setter, which the generator doesn't set without a <see cref="CsvColumnAttribute"/>: not read.</summary>
	[CsvFormat("ss")]
	public DateTime PrivateSet { get; private set; }

	/// <summary>A read-only field: not read.</summary>
	[CsvFormat("ss")]
	public readonly DateTime ReadOnlyField = DateTime.MinValue;

	/// <summary>A record type's member read by a converter: one column, not the record's, whose members' formats don't apply.</summary>
	[CsvConverter(typeof(StreetConverter))]
	public FormattedAddress Home { get; set; } = new();

	/// <summary>Sets the private members, for the tests.</summary>
	/// <param name="value">Their value.</param>
	public void SetPrivate(DateTime value)
	{
		Private = value;
		Hidden = value;
		hiddenField = value;
	}
}

/// <summary>Columns of the same name, in <see cref="CsvColumnAttribute.NameIndex"/> order, and one no member reads.</summary>
[CsvRecord]
public partial class RepeatedFormats
{
	/// <summary>The first <c>When</c>.</summary>
	[CsvColumn("When")]
	[CsvFormat("yyyy-MM-dd")]
	public DateTime First { get; set; }

	/// <summary>The fourth <c>When</c>, after an empty third.</summary>
	[CsvColumn("When", NameIndex = 3)]
	[CsvFormat("dd/MM/yyyy")]
	public DateTime Fourth { get; set; }

	/// <summary>The second <c>When</c>.</summary>
	[CsvColumn("When", NameIndex = 1)]
	[CsvFormat("MM.yyyy")]
	public DateTime Second { get; set; }
}

/// <summary>An address, nested under prefixes.</summary>
[CsvRecord]
public partial class FormattedAddress
{
	/// <summary>A format, under the prefix.</summary>
	[CsvFormat("yyyy-MM")]
	public DateTime Since { get; set; }

	/// <summary>Text.</summary>
	public string Street { get; set; } = "";
}

/// <summary>A customer with two nested addresses and a member of the same name.</summary>
[CsvRecord]
public partial class FormattedCustomer
{
	/// <summary>Text.</summary>
	public string Name { get; set; } = "";

	/// <summary>Under <c>Ship </c>.</summary>
	[CsvColumn(Prefix = "Ship ")]
	public FormattedAddress Ship { get; set; } = new();

	/// <summary>Under <c>Bill </c>.</summary>
	[CsvColumn(Prefix = "Bill ")]
	public FormattedAddress? Bill { get; set; }

	/// <summary>The name of the addresses' member, without a prefix.</summary>
	[CsvFormat("dd.MM.yyyy")]
	public DateTime Since { get; set; }
}

/// <summary>A span, a struct nested as a nullable.</summary>
[CsvRecord]
public partial struct FormattedSpan
{
	/// <summary>A format, under the prefixes.</summary>
	[CsvFormat("HH:mm")]
	public DateTime From { get; set; }
}

/// <summary>Records nested two deep, and a nullable struct.</summary>
[CsvRecord]
public partial class FormattedOrder
{
	/// <summary>Under <c>Customer </c>, whose addresses are under <c>Customer Ship </c> and <c>Customer Bill </c>.</summary>
	[CsvColumn(Prefix = "Customer ")]
	public FormattedCustomer Customer { get; set; } = new();

	/// <summary>Under <c>Span </c>.</summary>
	[CsvColumn(Prefix = "Span ")]
	public FormattedSpan? Span { get; set; }
}

/// <summary>A positional record: formats on a parameter and on its property.</summary>
/// <param name="Start">A format on the property.</param>
/// <param name="End">A format on the parameter.</param>
/// <param name="Rate">A culture on the parameter.</param>
[CsvRecord]
public partial record FormattedPosition([property: CsvFormat("yyyy/MM/dd")] DateTime Start, [CsvFormat("HH:mm")] DateTime End, [CsvCulture("fr-FR")] double Rate);

/// <summary>A constructor whose parameter, named in lower case, sets a property with a format.</summary>
[CsvRecord]
public partial class FormattedConstructor
{
	/// <summary>Creates a record.</summary>
	/// <param name="when">The value of <see cref="When"/>, whose column is named after the parameter.</param>
	public FormattedConstructor(DateTime when) => When = when;

	/// <summary>A copy constructor, which the generator doesn't use, though it has one parameter too.</summary>
	/// <param name="other">The record copied.</param>
	public FormattedConstructor(FormattedConstructor other) => When = other.When;

	/// <summary>A format.</summary>
	[CsvFormat("yyyy.MM.dd")]
	public DateTime When { get; }
}

/// <summary>A parameterless constructor beside one with a parameter: the generator uses the parameterless one.</summary>
[CsvRecord]
public partial class TwoConstructors
{
	/// <summary>Creates a record.</summary>
	public TwoConstructors()
	{
	}

	/// <summary>Creates a record, with a parameter the generator doesn't use.</summary>
	/// <param name="when">The value of <see cref="When"/>.</param>
	public TwoConstructors(DateTime when) => When = when;

	/// <summary>A format, under the member's name.</summary>
	[CsvFormat("yyyy.MM.dd")]
	public DateTime When { get; set; }
}

/// <summary>A <see cref="CsvConstructorAttribute"/> on a constructor with a parameter, beside a parameterless one.</summary>
[CsvRecord]
public partial class MarkedConstructor
{
	/// <summary>Creates a record.</summary>
	public MarkedConstructor()
	{
	}

	/// <summary>Creates a record: the constructor the generator uses.</summary>
	/// <param name="when">The value of <see cref="When"/>, whose column is named after the parameter.</param>
	[CsvConstructor]
	public MarkedConstructor(DateTime when) => When = when;

	/// <summary>A format.</summary>
	[CsvFormat("yyyy.MM.dd")]
	public DateTime When { get; set; }
}

/// <summary>A base type's members, which a derived record type reads.</summary>
public class FormattedBase
{
	/// <summary>A public member.</summary>
	[CsvFormat("yyyy")]
	public DateTime Year { get; set; }

	/// <summary>Protected without a <see cref="CsvColumnAttribute"/>: not read.</summary>
	[CsvFormat("MM")]
	protected DateTime Month { get; set; }

	/// <summary>Protected with one.</summary>
	[CsvColumn]
	[CsvFormat("dd")]
	protected DateTime Day { get; set; }

	/// <summary>Hidden by the derived type's member of the same name.</summary>
	[CsvFormat("HH")]
	public DateTime Hour { get; set; }

	/// <summary>Private, which a derived type can't read, even with a <see cref="CsvColumnAttribute"/>.</summary>
	[CsvColumn]
	[CsvFormat("ss")]
	private DateTime Second { get; set; }

	/// <summary>Sets the protected members, for the tests.</summary>
	/// <param name="value">Their value.</param>
	public void SetProtected(DateTime value)
	{
		Month = value;
		Day = value;
	}
}

/// <summary>A record type with a base type's members.</summary>
[CsvRecord]
public partial class FormattedDerived : FormattedBase
{
	/// <summary>Its own.</summary>
	[CsvFormat("yyyy-MM-dd")]
	public DateTime Own { get; set; }

	/// <summary>Hides the base type's, with another format.</summary>
	[CsvFormat("hh tt")]
	public new DateTime Hour { get; set; }
}

/// <summary>A record type that leaves its base type's members out.</summary>
[CsvRecord]
[CsvIgnoreBase]
public partial class FormattedOwnOnly : FormattedBase
{
	/// <summary>Its own.</summary>
	[CsvFormat("yyyy-MM-dd")]
	public DateTime Own { get; set; }
}

/// <summary>Writes a date as its ticks.</summary>
public sealed class TicksConverter : ICsvConverter<DateTime>
{
	/// <inheritdoc/>
	public DateTime Read(CsvReader reader, int index) => new(reader.Parse<long>(index));

	/// <inheritdoc/>
	public void Write(CsvWriter writer, DateTime value) => writer.WriteField(value.Ticks);
}

/// <summary>A converter's member, collections at indexes, and an indexed member.</summary>
[CsvRecord]
public partial class IndexedFormats
{
	/// <summary>Three columns from index 0, each with the format.</summary>
	[CsvColumn("Day", Index = 0, EndIndex = 2)]
	[CsvFormat("yyyy-MM-dd")]
	public List<DateTime> Days { get; set; } = [];

	/// <summary>Column 4: a gap is left at 3.</summary>
	[CsvColumn(Index = 4)]
	[CsvFormat("HH:mm")]
	public DateTime Time { get; set; }

	/// <summary>Read by its converter.</summary>
	[CsvColumn(Index = 5)]
	[CsvConverter(typeof(TicksConverter))]
	public DateTime Converted { get; set; }
}

/// <summary>Writes an address as its street.</summary>
public sealed class StreetConverter : ICsvConverter<FormattedAddress>
{
	/// <inheritdoc/>
	public FormattedAddress Read(CsvReader reader, int index) => new() { Street = reader.GetString(index) };

	/// <inheritdoc/>
	public void Write(CsvWriter writer, FormattedAddress value) => writer.WriteField(value.Street);
}

/// <summary>A type a map maps: its attributes are on the map.</summary>
public class MappedFormats
{
	/// <summary>A date.</summary>
	public DateTime When { get; set; }
}

/// <summary>The map of <see cref="MappedFormats"/>.</summary>
[CsvRecordMap(typeof(MappedFormats))]
public partial class MappedFormatsMap
{
	/// <summary>A format, for <see cref="MappedFormats.When"/>.</summary>
	[CsvFormat("yyyy")]
	public DateTime When { get; set; }
}

/// <summary>A format and Excel options on one member, and Excel options alone on another.</summary>
[CsvRecord]
public partial class FormatsAndExcelColumns
{
	/// <summary>Both.</summary>
	[CsvFormat("dd.MM.yyyy")]
	[ExcelColumn(Width = 14)]
	public DateTime When { get; set; }

	/// <summary>Excel options alone.</summary>
	[ExcelColumn(Wrap = true)]
	public string Note { get; set; } = "";
}

/// <summary>
/// The members' formats as <see cref="MemberFormats"/> reads them by reflection, checked against the record writers
/// themselves: each column given a format is one that its record writer writes in that format.
/// </summary>
public class MemberFormatsTests
{
	/// <summary>The value of every date: each column's format gives it another text.</summary>
	private static readonly DateTime When = new(2024, 3, 9, 22, 5, 9, 7);

	[Fact]
	public void FindsMembersByTheirNames()
	{
		var record = new NamedFormats { Plain = When, Other = When, Amount = 1234.5m, NoFormat = When, Ignored = When, Internal = When, Field = When };
		record.SetPrivate(When);

		var columns = AssertColumnsAreTheMembers(record);

		Assert.Equal(["Plain=yyyy-MM-dd", "Renamed=dd.MM.yyyy", "Amount=de-DE", "Internal=HH:mm", "Field=yyyy/MM", "Private=MM/yyyy"], columns);
		// The members the generator doesn't read have no entries: ignored, private, read only, a private setter, a read-only field.
		Assert.Equal(["Amount", "Field", "Internal", "Plain", "Private", "Renamed"], Entries<NamedFormats>());
	}

	[Fact]
	public void FindsColumnsOfTheSameNameByNameIndex()
	{
		var columns = AssertColumnsAreTheMembers(new RepeatedFormats { First = When, Second = When, Fourth = When });

		Assert.Equal(["When=yyyy-MM-dd", "When=MM.yyyy", "When=dd/MM/yyyy"], columns);
	}

	[Fact]
	public void FindsNestedMembersAfterTheirPrefixes()
	{
		var customer = new FormattedCustomer { Name = "a", Ship = new FormattedAddress { Since = When, Street = "s" }, Bill = new FormattedAddress { Since = When }, Since = When };

		var columns = AssertColumnsAreTheMembers(customer);

		Assert.Equal(["Ship Since=yyyy-MM", "Bill Since=yyyy-MM", "Since=dd.MM.yyyy"], columns);
	}

	[Fact]
	public void AddsThePrefixesOfRecordsNestedTwoDeep()
	{
		var customer = new FormattedCustomer { Ship = new FormattedAddress { Since = When }, Bill = new FormattedAddress { Since = When }, Since = When };
		var order = new FormattedOrder { Customer = customer, Span = new FormattedSpan { From = When } };

		var columns = AssertColumnsAreTheMembers(order);

		Assert.Equal(["Customer Ship Since=yyyy-MM", "Customer Bill Since=yyyy-MM", "Customer Since=dd.MM.yyyy", "Span From=HH:mm"], columns);
	}

	[Fact]
	public void FindsConstructorParameters()
	{
		Assert.Equal(["Start=yyyy/MM/dd", "End=HH:mm", "Rate=fr-FR"], AssertColumnsAreTheMembers(new FormattedPosition(When, When, 0.25)));
		// Once each: the properties the parameters set aren't read again.
		Assert.Equal(["End", "Rate", "Start"], Entries<FormattedPosition>());
		// A copy constructor isn't the generator's, so the other one is, with its parameter's name.
		Assert.Equal(["when=yyyy.MM.dd"], AssertColumnsAreTheMembers(new FormattedConstructor(When)));
		// The parameterless constructor comes first, unless another has [CsvConstructor].
		Assert.Equal(["When=yyyy.MM.dd"], AssertColumnsAreTheMembers(new TwoConstructors { When = When }));
		Assert.Equal(["when=yyyy.MM.dd"], AssertColumnsAreTheMembers(new MarkedConstructor(When)));
	}

	[Fact]
	public void FindsTheBaseTypesMembers()
	{
		var derived = new FormattedDerived { Own = When, Year = When, Hour = When };
		derived.SetProtected(When);

		// The base type's members come first, as the record writer writes them; its Hour is hidden.
		Assert.Equal(["Year=yyyy", "Day=dd", "Own=yyyy-MM-dd", "Hour=hh tt"], AssertColumnsAreTheMembers(derived));
		// Its protected member without [CsvColumn], and its private one with it, aren't read.
		Assert.Equal(["Day", "Hour", "Own", "Year"], Entries<FormattedDerived>());
		Assert.Equal(["Own=yyyy-MM-dd"], AssertColumnsAreTheMembers(new FormattedOwnOnly { Own = When, Year = When }));
		Assert.Equal(["Own"], Entries<FormattedOwnOnly>());
		// A base type without [CsvRecord] has no record writer of its own.
		Assert.Null(MemberFormats.For<FormattedBase>());
	}

	[Fact]
	public void FindsIndexedColumnsAndSkipsConverters()
	{
		var columns = AssertColumnsAreTheMembers(new IndexedFormats { Days = [When, When, When], Time = When, Converted = When });

		Assert.Equal(["Day=yyyy-MM-dd", "Day=yyyy-MM-dd", "Day=yyyy-MM-dd", "Time=HH:mm"], columns);
		Assert.Equal(["Day", "Day", "Day", "Time"], Entries<IndexedFormats>());
	}

	[Fact]
	public void HasNoFormatsForTypesWithoutThem()
	{
		// A mapped type's attributes are on its map; other types have no record writer of their own.
		Assert.Null(MemberFormats.For<MappedFormats>());
		Assert.Null(MemberFormats.For<TypedRecord>());
		Assert.Null(MemberFormats.For<object>());
		Assert.Null(MemberFormats.For<string>());
		Assert.Null(MemberFormats.For<System.Dynamic.ExpandoObject>());
		// The record writer of a type with formats still writes the mapped type's.
		Assert.Equal("2024", Fields(new MappedFormats { When = When })[0]);
	}

	[Fact]
	public void FindsColumnsInTheHeadersNames()
	{
		var formats = MemberFormats.For<NamedFormats>()!;

		// A header written by hand: its names say where the members are, ordinally; a name it lacks gives no column.
		var columns = formats.Columns(["x", "Renamed", "plain", "Amount"], CultureInfo.InvariantCulture);

		Assert.Equal(4, columns.Length);
		Assert.Null(columns[0]);
		Assert.Equal("dd.MM.yyyy", columns[1]!.Format);
		Assert.Null(columns[2]);
		Assert.Equal(CultureInfo.GetCultureInfo("de-DE"), columns[3]!.Provider);
		// A member's column past the header's, by its index, still gets one.
		Assert.Equal(5, MemberFormats.For<IndexedFormats>()!.Columns(["Day"], CultureInfo.InvariantCulture).Length);
	}

	[Fact]
	public void ReadsExcelColumnsBesideFormats()
	{
		var formats = MemberFormats.For<FormatsAndExcelColumns>()!;

		var columns = formats.Columns(["When", "Note"], CultureInfo.InvariantCulture);
		var excel = formats.ExcelColumns(["When", "Note"]);

		// One entry for both of a member's; a member with Excel options alone gives its column no format.
		Assert.Equal(["When", "Note"], formats.Entries.Select(e => e.Name));
		Assert.True(formats.HasFormats);
		Assert.Equal("dd.MM.yyyy", columns[0]!.Format);
		Assert.Null(columns[1]);
		Assert.Equal<IEnumerable<ExcelColumnOptions?>>([new ExcelColumnOptions { Width = 14 }, new ExcelColumnOptions { Wrap = true }], excel);
		Assert.Null(formats.Sheet);
		Assert.Null(formats.Problem);
		// A type without Excel attributes has none, and the others' columns have no Excel options.
		Assert.All(MemberFormats.For<NamedFormats>()!.ExcelColumns(["Plain", "Renamed"]), Assert.Null);
	}

	[Fact]
	public void ReadsEachTypeOnce()
	{
		Assert.Same(MemberFormats.For<NamedFormats>(), MemberFormats.For<NamedFormats>());
		Assert.Same(MemberFormats.For<FormattedCustomer>(), MemberFormats.For(typeof(FormattedCustomer)));
	}

	[Fact]
	public void CapturesTheRecordWritersHeader()
	{
		Assert.Equal(["Name", "Ship Since", "Ship Street", "Bill Since", "Bill Street", "Since"], HeaderCapture.Of<FormattedCustomer>());
		Assert.Equal(["When", "When", "When", "When"], HeaderCapture.Of<RepeatedFormats>());
		Assert.Null(HeaderCapture.Of<MemberFormatsTests>());
	}

	/// <summary>
	/// Checks that each column a type's formats give a format to is one its record writer writes in that format and culture
	/// (every date of the record is <see cref="When"/>, and every number 1234.5 or 0.25).
	/// </summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="record">A record.</param>
	/// <returns>The columns with a format, as their header name and the format or the culture's name.</returns>
	private static List<string> AssertColumnsAreTheMembers<T>(T record)
	{
		var names = HeaderCapture.Of<T>()!;
		var fields = Fields(record);
		Assert.Equal(names.Count, fields.Length);
		var columns = MemberFormats.For<T>()!.Columns(names, CultureInfo.InvariantCulture);
		var found = new List<string>();
		for (var i = 0; i < columns.Length; i++)
		{
			if (columns[i] is not { } column)
			{
				continue;
			}

			if (column.Format is { } format)
			{
				Assert.Equal(When.ToString(format, column.Provider), fields[i]);
				found.Add(names[i] + "=" + format);
			}
			else
			{
				var culture = (CultureInfo)column.Provider;
				Assert.Contains(fields[i], new[] { 1234.5m.ToString(culture), 0.25.ToString(culture) });
				found.Add(names[i] + "=" + culture.Name);
			}
		}

		return found;
	}

	/// <summary>The names of a type's members with formats, in order.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <returns>The names, sorted.</returns>
	private static List<string> Entries<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(MemberFormats.Members)] T>() =>
		[.. MemberFormats.For<T>()!.Entries.Select(e => e.Name).OrderBy(n => n, StringComparer.Ordinal)];

	/// <summary>The fields of a record as its record writer writes them in CSV.</summary>
	/// <typeparam name="T">The record type.</typeparam>
	/// <param name="record">The record.</param>
	/// <returns>The fields.</returns>
	private static string[] Fields<T>(T record)
	{
		using var text = new StringWriter();
		using (var writer = CsvWriter.Create(text, new CsvWriterOptions { HasHeader = false }, leaveOpen: true))
		{
			writer.WriteRecord(record);
		}

		using var reader = CsvReader.Create(new StringReader(text.ToString()), new CsvReaderOptions { HasHeader = false });
		Assert.True(reader.Read());

		return [.. Enumerable.Range(0, reader.FieldCount).Select(reader.GetString)];
	}
}
