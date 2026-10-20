using System.Globalization;

namespace CsvHelper.Tests;

[CsvRecord]
public partial class Account
{
	[CsvColumn(Index = 0, Width = 6)]
	public int Id { get; set; }

	[CsvColumn(Index = 1, Width = 10)]
	public string Name { get; set; } = "";

	[CsvColumn(Index = 2, Width = 8)]
	[CsvFormat("yyyyMMdd")]
	public DateTime Opened { get; set; }

	[CsvColumn(Index = 3, Width = 9)]
	public decimal Balance { get; set; }
}

[CsvRecord]
public partial class Dated
{
	[CsvFormat("yyyyMMdd")]
	public DateTime Day { get; set; }

	[CsvFormat("yyyyMMddHHmmzzz")]
	public DateTimeOffset At { get; set; }

	[CsvFormat("yyyyMMdd")]
	public DateTime? Maybe { get; set; }

	[CsvFormat("hh\\:mm")]
	public TimeSpan Span { get; set; }

#if NET
	[CsvFormat("dd.MM.yyyy")]
	public DateOnly Due { get; set; }
#endif
}

public sealed class UriConverter : ICsvConverter<Uri>
{
	public Uri Read(CsvReader reader, int index) => new(reader.GetString(index));

	public void Write(CsvWriter writer, Uri value) => writer.WriteField(value.OriginalString);
}

public sealed class HexConverter : ICsvConverter<int>
{
	public int Read(CsvReader reader, int index) => int.Parse(reader.GetString(index), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

	public void Write(CsvWriter writer, int value) => writer.WriteField(value, "x");
}

[CsvRecord]
public partial class Link
{
	[CsvConverter(typeof(UriConverter))]
	public Uri Target { get; set; } = null!;

	[CsvConverter(typeof(HexConverter))]
	public int Code { get; set; }

	[CsvConverter(typeof(HexConverter))]
	public int? MaybeCode { get; set; }
}

[CsvRecord]
public partial class Nullables
{
	[CsvNullValues("NULL", "\\N")]
	public string? Name { get; set; }

	[CsvNullValues("NULL")]
	public int? Count { get; set; }

	[CsvNullValues("")]
	public string? Empty { get; set; }
}

[CsvRecord]
public partial class Switches
{
	[CsvBooleanValues("Ja", "Nein")]
	public bool German { get; set; }

	[CsvBooleanValues(new[] { "on", "1" }, new[] { "off", "0" })]
	public bool? Power { get; set; }
}

[CsvRecord]
public partial class Renamed
{
	[CsvColumn("Unit Price", "Price", "unit_price")]
	public decimal Price { get; set; }

	[CsvColumn("Qty", "Quantity", Optional = true)]
	public int Quantity { get; set; } = -1;
}

public enum Status
{
	Active,

	[CsvEnumName("on hold", "hold")]
	OnHold,

	Closed,

	[CsvEnumName("gone")]
	Archived,
}

[CsvRecord]
public partial class Ticket
{
	public Status Status { get; set; }

	public Status? Previous { get; set; }
}

[CsvRecord]
public partial class Defaults
{
	[CsvDefault(0)]
	public int Count { get; set; }

	[CsvDefault(null)]
	public string? Note { get; set; }

	[CsvDefault(1.5)]
	public double Ratio { get; set; }

	[CsvDefault(Status.Closed)]
	public Status Status { get; set; }

	[CsvDefault(5)]
	public int? Maybe { get; set; }
}

[CsvRecord]
public partial class Mixed
{
	public decimal Invariant { get; set; }

	[CsvCulture("de-DE")]
	public decimal German { get; set; }

	[CsvCulture("de-DE")]
	[CsvFormat("d. MMMM yyyy")]
	public DateTime Date { get; set; }

	[CsvCulture("de-DE")]
	public Money Money { get; set; }
}

/// <summary>A type without [CsvRecord], read by a mapper registered by hand.</summary>
public sealed class Temperature
{
	public Temperature(string city, double celsius)
	{
		City = city;
		Celsius = celsius;
	}

	public string City { get; }

	public double Celsius { get; }
}

public sealed class TemperatureMapper : CsvRecordMapper<Temperature>
{
	private readonly int city;
	private readonly int fahrenheit;

	public TemperatureMapper(CsvReader reader)
	{
		city = Column(reader, "City", -1, false, "Temperature.City");
		fahrenheit = Column(reader, ["F", "Fahrenheit"], -1, false, "Temperature.Celsius");
		ThrowIfColumnsMissing(reader);
	}

	public override Temperature Read(CsvReader reader) => new(reader.GetString(city), (reader.GetDouble(fahrenheit) - 32) * 5 / 9);
}

/// <summary>A type whose hand-written mapper the generator registers, and which takes a column prefix.</summary>
[CsvConverter(typeof(AddressMapper))]
public sealed class Address
{
	public string Street { get; set; } = "";

	public string City { get; set; } = "";
}

public sealed class AddressMapper : CsvRecordMapper<Address>
{
	private readonly int street;
	private readonly int city;

	public AddressMapper(CsvReader reader, string columnPrefix)
		: base(columnPrefix)
	{
		street = Column(reader, "Street", -1, false, "Address.Street");
		city = Column(reader, "City", -1, false, "Address.City");
		ThrowIfColumnsMissing(reader);
	}

	public override Address Read(CsvReader reader) => new() { Street = reader.GetString(street), City = reader.GetString(city) };
}

/// <summary>A type whose hand-written mapper the generator registers, without support for column prefixes.</summary>
[CsvConverter(typeof(CoordinatesMapper))]
public readonly struct Coordinates
{
	public Coordinates(double lat, double lon)
	{
		Lat = lat;
		Lon = lon;
	}

	public double Lat { get; }

	public double Lon { get; }
}

internal sealed class CoordinatesMapper : CsvRecordMapper<Coordinates>
{
	private readonly int lat;
	private readonly int lon;

	public CoordinatesMapper(CsvReader reader)
	{
		lat = Column(reader, "Lat", -1, false, "Coordinates.Lat");
		lon = Column(reader, "Lon", -1, false, "Coordinates.Lon");
		ThrowIfColumnsMissing(reader);
	}

	public override Coordinates Read(CsvReader reader) => new(reader.GetDouble(lat), reader.GetDouble(lon));
}

[CsvRecord]
public partial record struct GeoPoint(double Lat, double Lon);

[CsvRecord]
public partial class Place
{
	public string City { get; set; } = "";

	[CsvColumn(Prefix = "Geo ")]
	public GeoPoint Point { get; set; }
}

[CsvRecord]
public partial class Shipment
{
	public string Id { get; set; } = "";

	[CsvColumn(Prefix = "From ")]
	public Place From { get; set; } = new();

	[CsvColumn(Prefix = "To ")]
	public Place To { get; set; } = new();
}

[CsvRecord]
public partial record Customer(string Name, [property: CsvColumn(Prefix = "Home ")] Address Home)
{
	public Coordinates Location { get; init; }
}

[CsvRecord]
public partial class Visit
{
	public string Name { get; set; } = "";

	[CsvColumn(Prefix = "At ")]
	public Coordinates Location { get; set; }
}

public class RecordAttributeTests
{
	[Fact]
	public void TheAssemblyIsClsCompliant()
	{
		var assembly = typeof(CsvReader).Assembly;
		var declared = Assert.Single(assembly.GetCustomAttributes(typeof(CLSCompliantAttribute), false));
		Assert.True(((CLSCompliantAttribute)declared).IsCompliant);

		// CLS allows no arrays as attribute arguments, so every attribute needs a constructor without them (CS3015).
		foreach (var attribute in assembly.GetExportedTypes().Where(t => typeof(Attribute).IsAssignableFrom(t) && !t.IsAbstract))
		{
			Assert.True(attribute.GetConstructors().Any(c => c.GetParameters().All(p => !p.ParameterType.IsArray)),
				$"{attribute.Name} has no constructor without array parameters.");
		}
	}

	[Fact]
	public void AttributesKeepTheirArguments()
	{
		var column = new CsvColumnAttribute("Unit Price", "Price", "unit_price") { Index = 2, Optional = true, Width = 5, Prefix = "P " };
		Assert.Equal("Unit Price", column.Name);
		Assert.Equal(["Price", "unit_price"], column.AlternateNames);
		Assert.Equal(2, column.Index);
		Assert.True(column.Optional);
		Assert.Equal(5, column.Width);
		Assert.Equal("P ", column.Prefix);
		var plain = new CsvColumnAttribute();
		Assert.Null(plain.Name);
		Assert.Empty(plain.AlternateNames);
		Assert.Equal(-1, plain.Index);
		Assert.False(plain.Optional);
		Assert.Equal(0, plain.Width);
		Assert.Null(plain.Prefix);
		Assert.Empty(new CsvColumnAttribute("Name").AlternateNames);
		Assert.Empty(new CsvColumnAttribute("Name", null!).AlternateNames);

		Assert.Equal("yyyyMMdd", new CsvFormatAttribute("yyyyMMdd").Format);
		Assert.Equal(typeof(UriConverter), new CsvConverterAttribute(typeof(UriConverter)).ConverterType);
		Assert.Equal(["NULL", "\\N"], new CsvNullValuesAttribute("NULL", "\\N").Values);
		Assert.Equal(["NULL"], new CsvNullValuesAttribute("NULL").Values);
		Assert.Empty(new CsvNullValuesAttribute((string[])null!).Values);
		Assert.Empty(new CsvNullValuesAttribute((string)null!).Values);
		var pair = new CsvBooleanValuesAttribute("Ja", "Nein");
		Assert.Equal(["Ja"], pair.TrueValues);
		Assert.Equal(["Nein"], pair.FalseValues);
		var lists = new CsvBooleanValuesAttribute(["on", "1"], null!);
		Assert.Equal(["on", "1"], lists.TrueValues);
		Assert.Empty(lists.FalseValues);
		Assert.Equal(["on hold", "hold"], new CsvEnumNameAttribute("on hold", "hold").Names);
		Assert.Equal(["on hold"], new CsvEnumNameAttribute("on hold").Names);
		Assert.Empty(new CsvEnumNameAttribute((string[])null!).Names);
		Assert.Empty(new CsvEnumNameAttribute((string)null!).Names);
		Assert.Equal(0, new CsvDefaultAttribute(0).Value);
		Assert.Null(new CsvDefaultAttribute(null).Value);
		Assert.Equal("de-DE", new CsvCultureAttribute("de-DE").Name);
		Assert.Equal(-1, plain.EndIndex);
		Assert.Equal(4, new CsvColumnAttribute { Index = 1, EndIndex = 4 }.EndIndex);
		Assert.Equal(["yyyyMMdd"], new CsvFormatAttribute("yyyyMMdd").Formats);
		var formats = new CsvFormatAttribute("yyyy-MM-dd", "dd/MM/yyyy");
		Assert.Equal("yyyy-MM-dd", formats.Format);
		Assert.Equal(["yyyy-MM-dd", "dd/MM/yyyy"], formats.Formats);
		Assert.Equal(["x"], new CsvFormatAttribute("x", null!).Formats);
		Assert.Equal("v2", new CsvConstantAttribute("v2").Value);
		var validate = new CsvValidateAttribute("IsAge") { Message = "Bad age." };
		Assert.Equal(("IsAge", "Bad age."), (validate.MethodName, validate.Message!));
		Assert.Null(new CsvValidateAttribute("IsAge").Message);
		Assert.Equal(typeof(Supplier), new CsvRecordMapAttribute(typeof(Supplier)).RecordType);
		var options = new CsvOptionsAttribute();
		Assert.Equal((',', CsvMode.Rfc4180, '\\', true), (options.Separator, options.Mode, options.Escape, options.HasHeader));
		Assert.Null(options.NullValues);
		Assert.NotNull(new CsvIgnoreBaseAttribute());

		// As the compiler stores them on a member.
		var price = (CsvColumnAttribute)typeof(Renamed).GetProperty(nameof(Renamed.Price))!.GetCustomAttributes(typeof(CsvColumnAttribute), false).Single();
		Assert.Equal("Unit Price", price.Name);
		Assert.Equal(["Price", "unit_price"], price.AlternateNames);
		var status = (CsvEnumNameAttribute)typeof(Status).GetField(nameof(Status.OnHold))!.GetCustomAttributes(typeof(CsvEnumNameAttribute), false).Single();
		Assert.Equal(["on hold", "hold"], status.Names);
	}

	[Fact]
	public void FixedWidthLayoutComesFromTheMembers()
	{
		var widths = CsvRecordMapper.GetFieldWidths<Account>();
		Assert.Equal([6, 10, 8, 9], widths);
		Assert.Throws<NotSupportedException>(() => ((IList<int>)widths)[0] = 1);

		var options = new CsvReaderOptions { Mode = CsvMode.FixedWidth, FieldWidths = widths, HasHeader = false };
		using var reader = CsvReader.FromText("1     Anna      20240115  1250.00\n22    Benedikt  20231231    -3.75\n", options);
		var accounts = reader.GetRecords<Account>().ToList();

		Assert.Equal([1, 22], accounts.Select(a => a.Id));
		Assert.Equal(["Anna", "Benedikt"], accounts.Select(a => a.Name));
		Assert.Equal([new DateTime(2024, 1, 15), new DateTime(2023, 12, 31)], accounts.Select(a => a.Opened));
		Assert.Equal([1250.00m, -3.75m], accounts.Select(a => a.Balance));
	}

	[Fact]
	public void FixedWidthLayoutRoundTripsThroughTheWriter()
	{
		var widths = CsvRecordMapper.GetFieldWidths<Account>();
		using var text = new StringWriter();
		using (var writer = CsvWriter.Create(text, new CsvWriterOptions { Mode = CsvMode.FixedWidth, FieldWidths = widths }))
		{
			writer.WriteField(7);
			writer.WriteField("Clara");
			writer.WriteField("20200229");
			writer.WriteField(10.5m);
			writer.EndRecord();
		}

		using var reader = CsvReader.FromText(text.ToString(), new CsvReaderOptions { Mode = CsvMode.FixedWidth, FieldWidths = widths, HasHeader = false });
		var account = Assert.Single(reader.GetRecords<Account>());

		Assert.Equal(7, account.Id);
		Assert.Equal("Clara", account.Name);
		Assert.Equal(new DateTime(2020, 2, 29), account.Opened);
		Assert.Equal(10.5m, account.Balance);
	}

	[Fact]
	public void TypesWithoutWidthsHaveNoLayout()
	{
		var e = Assert.Throws<InvalidOperationException>(() => CsvRecordMapper.GetFieldWidths<Order>());

		Assert.Contains("Width", e.Message);
		Assert.Throws<InvalidOperationException>(() => CsvRecordMapper.GetFieldWidths<NotARecord>());
	}

	[Fact]
	public void ReadsExactFormats()
	{
		using var reader = CsvReader.FromText("Day,At,Maybe,Span,Due\n20240401,202404011230+02:00,,01:30,24.12.2024\n");
		var dated = Assert.Single(reader.GetRecords<Dated>());

		Assert.Equal(new DateTime(2024, 4, 1), dated.Day);
		Assert.Equal(new DateTimeOffset(2024, 4, 1, 12, 30, 0, TimeSpan.FromHours(2)), dated.At);
		Assert.Null(dated.Maybe);
		Assert.Equal(new TimeSpan(1, 30, 0), dated.Span);
#if NET
		Assert.Equal(new DateOnly(2024, 12, 24), dated.Due);
#endif
	}

	[Fact]
	public void RejectsValuesInAnotherFormat()
	{
		using var reader = CsvReader.FromText("Day,At,Maybe,Span,Due\n2024-04-01,202404011230+02:00,,01:30,24.12.2024\n");

		var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<Dated>().ToList());
		Assert.Equal("Dated.Day", e.Member);
	}

	[Fact]
	public void ReadsWithConverters()
	{
		using var reader = CsvReader.FromText("Target,Code,MaybeCode\nhttps://example.com/a,ff,\nhttps://example.com/b,10,1A\n");
		var links = reader.GetRecords<Link>().ToList();

		Assert.Equal([new Uri("https://example.com/a"), new Uri("https://example.com/b")], links.Select(l => l.Target));
		Assert.Equal([255, 16], links.Select(l => l.Code));
		Assert.Equal([null, 26], links.Select(l => l.MaybeCode));
	}

	[Fact]
	public void ReadsNullValues()
	{
		using var reader = CsvReader.FromText("Name,Count,Empty\nNULL,NULL,\n\\N,3,x\nnull,,\"\"\n");
		var rows = reader.GetRecords<Nullables>().ToList();

		Assert.Equal([null, null, "null"], rows.Select(r => r.Name));
		Assert.Equal([null, 3, null], rows.Select(r => r.Count));
		Assert.Equal([null, "x", null], rows.Select(r => r.Empty));
	}

	[Fact]
	public void ReadsBooleanValues()
	{
		using var reader = CsvReader.FromText("German,Power\nJa,on\n nein ,OFF\nJA,1\nnein,\n");
		var rows = reader.GetRecords<Switches>().ToList();

		Assert.Equal([true, false, true, false], rows.Select(r => r.German));
		Assert.Equal([true, false, true, null], rows.Select(r => r.Power));
	}

	[Fact]
	public void RejectsOtherBooleanValues()
	{
		using var reader = CsvReader.FromText("German,Power\ntrue,on\n");
		var e = Assert.Throws<CsvFormatException>(() => reader.GetRecords<Switches>().ToList());

		Assert.Equal("'true' is not a valid boolean. Expected Ja or Nein.", e.InnerException!.Message);
		Assert.Equal("Cannot read Switches.German from field 0 'German' of record 2: 'true' is not a valid boolean. Expected Ja or Nein.", e.Message);
	}

	[Fact]
	public void ReadsAlternateHeaderNames()
	{
		using (var reader = CsvReader.FromText("Price,Quantity\n1.5,2\n"))
		{
			var row = Assert.Single(reader.GetRecords<Renamed>());
			Assert.Equal(1.5m, row.Price);
			Assert.Equal(2, row.Quantity);
		}

		using (var reader = CsvReader.FromText("unit_price\n2.5\n"))
		{
			var row = Assert.Single(reader.GetRecords<Renamed>());
			Assert.Equal(2.5m, row.Price);
			Assert.Equal(-1, row.Quantity);
		}

		using (var reader = CsvReader.FromText("Cost\n2.5\n"))
		{
			var e = Assert.Throws<KeyNotFoundException>(() => reader.GetRecords<Renamed>());
			Assert.Equal("The CSV header has no column for Renamed.Price ('Unit Price' or 'Price' or 'unit_price').", e.Message);
		}
	}

	[Fact]
	public void ReadsEnumNames()
	{
		using var reader = CsvReader.FromText("Status,Previous\non hold,HOLD\nOnHold,\nclosed,on hold\ngone,GONE\n");
		var tickets = reader.GetRecords<Ticket>().ToList();

		Assert.Equal([Status.OnHold, Status.OnHold, Status.Closed, Status.Archived], tickets.Select(t => t.Status));
		Assert.Equal([Status.OnHold, null, Status.OnHold, Status.Archived], tickets.Select(t => t.Previous));
	}

	[Fact]
	public void EmptyFieldsGetTheirDefaults()
	{
		using var reader = CsvReader.FromText("Count,Note,Ratio,Status,Maybe\n,,,,\n3,x,2,Active,1\n");
		var rows = reader.GetRecords<Defaults>().ToList();

		Assert.Equal([0, 3], rows.Select(r => r.Count));
		Assert.Equal([null, "x"], rows.Select(r => r.Note));
		Assert.Equal([1.5, 2], rows.Select(r => r.Ratio));
		Assert.Equal([Status.Closed, Status.Active], rows.Select(r => r.Status));
		Assert.Equal([5, 1], rows.Select(r => r.Maybe));
	}

	[Fact]
	public void ReadsMembersWithTheirCulture()
	{
		using var reader = CsvReader.FromText("Invariant;German;Date;Money\n1.5;1.234,5;3. März 2024;$2,5\n", new CsvReaderOptions { Separator = ';' });
		var row = Assert.Single(reader.GetRecords<Mixed>());

		Assert.Equal(1.5m, row.Invariant);
		Assert.Equal(1234.5m, row.German);
		Assert.Equal(new DateTime(2024, 3, 3), row.Date);
		Assert.Equal(2.5m, row.Money.Amount);
	}

	[Fact]
	public void ReadsWithHandWrittenMappers()
	{
		CsvRecordMapper.Register<Temperature>(reader => new TemperatureMapper(reader));
		using (var reader = CsvReader.FromText("City,Fahrenheit\nOslo,32\nCairo,212\n"))
		{
			var rows = reader.GetRecords<Temperature>().ToList();
			Assert.Equal(["Oslo", "Cairo"], rows.Select(r => r.City));
			Assert.Equal([0.0, 100.0], rows.Select(r => r.Celsius));
		}

		using (var reader = CsvReader.FromText("Town,F\nOslo,32\n"))
		{
			var e = Assert.Throws<KeyNotFoundException>(() => reader.GetRecords<Temperature>());
			Assert.Equal("The CSV header has no column for Temperature.City ('City').", e.Message);
		}
	}

	[Fact]
	public void RegistersMappersNamedByTypeLevelConverters()
	{
		using var reader = CsvReader.FromText("Street,City\nMain St 1,Springfield\n");
		var address = Assert.Single(reader.GetRecords<Address>());

		Assert.Equal("Main St 1", address.Street);
		Assert.Equal("Springfield", address.City);
	}

	[Fact]
	public void ReadsNestedRecordsWithPrefixes()
	{
		const string text = "Id,From City,From Geo Lat,From Geo Lon,To City,To Geo Lat,To Geo Lon\nS1,Oslo,59.9,10.7,Rome,41.9,12.5\n";
		using var reader = CsvReader.FromText(text);
		var shipment = Assert.Single(reader.GetRecords<Shipment>());

		Assert.Equal("S1", shipment.Id);
		Assert.Equal("Oslo", shipment.From.City);
		Assert.Equal(new GeoPoint(59.9, 10.7), shipment.From.Point);
		Assert.Equal("Rome", shipment.To.City);
		Assert.Equal(new GeoPoint(41.9, 12.5), shipment.To.Point);
	}

	[Fact]
	public void NestedRecordsReportTheirMissingColumnsWithTheOuterOnes()
	{
		using var reader = CsvReader.FromText("From City,From Geo Lat,From Geo Lon,To Geo Lat\nOslo,59.9,10.7,41.9\n");
		var e = Assert.Throws<KeyNotFoundException>(() => reader.GetRecords<Shipment>());

		Assert.Equal("The CSV header has no column for Shipment.Id ('Id'), Place.City ('To City'), GeoPoint.Lon ('To Geo Lon').", e.Message);
	}

	[Fact]
	public void NestsHandWrittenMappers()
	{
		using var reader = CsvReader.FromText("Name,Home Street,Home City,Lat,Lon\nAnna,Main St 1,Springfield,1.5,-2\n");
		var customer = Assert.Single(reader.GetRecords<Customer>());

		Assert.Equal("Anna", customer.Name);
		Assert.Equal("Main St 1", customer.Home.Street);
		Assert.Equal("Springfield", customer.Home.City);
		Assert.Equal(1.5, customer.Location.Lat);
		Assert.Equal(-2, customer.Location.Lon);
	}

	[Fact]
	public void MappersWithoutPrefixSupportCannotTakeAPrefix()
	{
		using var reader = CsvReader.FromText("Name,At Lat,At Lon\nAnna,1,2\n");
		var e = Assert.Throws<InvalidOperationException>(() => reader.GetRecords<Visit>());

		Assert.Contains("At ", e.Message);
		Assert.Throws<InvalidOperationException>(() => CsvRecordMapper.Create<Coordinates>(reader, "x"));
	}

	[Fact]
	public void ParallelEnumerateSharesNestedMappersAndConverters()
	{
		var text = new System.Text.StringBuilder("Id,From City,From Geo Lat,From Geo Lon,To City,To Geo Lat,To Geo Lon\n");
		for (var i = 0; i < 20000; i++) text.Append($"S{i},A{i},{i},1,B{i},2,{i}\n");
		using var reader = CsvReader.FromText(text.ToString());
		var shipments = reader.ParallelEnumerate<Shipment>(4).ToList();

		Assert.Equal(20000, shipments.Count);
		for (var i = 0; i < shipments.Count; i++)
		{
			Assert.Equal($"A{i}", shipments[i].From.City);
			Assert.Equal(i, shipments[i].To.Point.Lon);
		}
	}
}
